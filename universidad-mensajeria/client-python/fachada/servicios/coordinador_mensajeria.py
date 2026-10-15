"""Coordina envíos: persistencia previa, red y cola offline."""
from __future__ import annotations

import base64
import hashlib
import mimetypes
import uuid
from pathlib import Path
from typing import Callable

from fachada.servicios.respuestas import lanzar_si_error
from transversal.fechas import ahora_utc
from transversal.tipo_mensaje import TipoMensaje

PARTE_MAX_BYTES = 1 * 1024 * 1024
ARCHIVO_MAX_BYTES = 50 * 1024 * 1024
#: Límite de la imagen en UNA trama (invariante 2 + PROTOCOLO.md): misma regla
#: del cliente .NET (`CatalogoAdjuntos.MaxBytesImagenTramaUnica`, 12 MiB). El
#: base64 infla x4/3 y la trama resultante tiene que caber en los 16 MiB del
#: tope del servidor; una imagen mayor se rechaza ANTES de abrir el socket.
IMAGEN_MAX_BYTES = 12 * 1024 * 1024
DESTINO_BROADCAST = "*"


class EnvioCancelado(RuntimeError):
    """El usuario canceló el lote y este adjunto no llegó a enviarse completo.

    Se verifica entre partes de la transferencia fragmentada. NO deja
    pendiente (a diferencia de un corte de red): lo que el usuario canceló no
    se reenvía solo. La fila local queda en ERROR («! No enviado»).
    """


class CoordinadorMensajeriaCliente:
    """Envío de texto, imágenes, archivos fragmentados y difusión."""

    def __init__(self, conexion, historial_actual: Callable[[], object],
                 codigo_actual: Callable[[], str | None], exigir_sesion: Callable[[], None],
                 reintentar_pendientes: Callable[[], int] | None = None):
        self.conexion = conexion
        self._historial_actual = historial_actual
        self._codigo_actual = codigo_actual
        self._exigir_sesion = exigir_sesion
        self._reintentar_pendientes = reintentar_pendientes or (lambda: 0)

    @property
    def historial(self):
        return self._historial_actual()

    @property
    def codigo(self):
        return self._codigo_actual()

    def _pedir_archivo(self, solicitud, timeout=None):
        """Usa el timeout de transferencia; conserva adaptadores de prueba antiguos."""
        pedir_archivo = getattr(self.conexion, "pedir_archivo", None)
        if callable(pedir_archivo):
            if timeout is None:
                return pedir_archivo(solicitud)
            return pedir_archivo(solicitud, timeout=timeout)
        return self.conexion.pedir(solicitud)

    def _cancelar_archivo_en_servidor(self, id_msg, fecha):
        try:
            self._pedir_archivo({
                "id": f"{id_msg}#cancel",
                "tipo": TipoMensaje.ARCHIVO_FIN,
                "remitente": self.codigo,
                "archivoId": id_msg,
                "exito": False,
                "fechaHora": fecha,
            }, timeout=5)
        except Exception:
            # La fila local sigue cancelada aunque el servidor no responda.
            pass

    def enviar_texto(self, destinatario, texto, id_msg=None):
        """True si salió por red; False si el texto quedó en pendientes."""
        self._exigir_sesion()
        if not destinatario or not texto:
            raise ValueError("destinatario y contenido son obligatorios")
        id_msg = id_msg or str(uuid.uuid4())
        fecha = ahora_utc()
        self.historial.guardar_mensaje(
            id_msg, self.codigo, destinatario, TipoMensaje.MENSAJE_TEXTO, texto,
            fecha, num_caracteres=len(texto), num_palabras=len(texto.split()),
            enviado=1, descargado=1, estado="PENDIENTE")

        try:
            respuesta = self.conexion.pedir({
                "id": id_msg,
                "tipo": TipoMensaje.MENSAJE_TEXTO,
                "remitente": self.codigo,
                "destinatario": destinatario,
                "contenido": texto,
                "fechaHora": fecha,
            })
        except (ConnectionError, TimeoutError, OSError):
            self.historial.guardar_pendiente(
                id_msg, TipoMensaje.MENSAJE_TEXTO, self.codigo,
                destinatario, texto, "", None, fecha)
            return False

        try:
            lanzar_si_error(respuesta, "envío rechazado")
        except Exception:
            self.historial.marcar_estado(id_msg, "ERROR")
            raise
        self.historial.marcar_estado(id_msg, "ENVIADO")
        return True

    def enviar_imagen(self, destinatario, ruta_archivo, id_msg=None, caption=None):
        """Envía una imagen; la cola conserva bytes crudos si cae la red."""
        self._exigir_sesion()
        if not destinatario:
            raise ValueError("destinatario es obligatorio")
        with open(ruta_archivo, "rb") as archivo:
            crudos = archivo.read()
        if not crudos:
            raise ValueError("la imagen está vacía")
        if len(crudos) > IMAGEN_MAX_BYTES:
            # Misma regla y mensaje que el cliente .NET: una imagen mayor no
            # cabe en una trama de 16 MiB una vez en base64; mandarla corta
            # la conexión en el servidor.
            raise ValueError(
                f"la imagen supera el límite de envío directo de 12 MB "
                f"({len(crudos) / 1024 / 1024:.1f} MB)")
        contenido = base64.b64encode(crudos).decode("utf-8")
        id_msg = id_msg or str(uuid.uuid4())
        fecha = ahora_utc()
        nombre = Path(ruta_archivo).name
        pie = caption.strip() if caption and caption.strip() else None
        self.historial.guardar_mensaje(
            id_msg, self.codigo, destinatario, TipoMensaje.MENSAJE_IMAGEN,
            "[Imagen]", fecha, nombre_archivo=nombre, ruta_archivo=ruta_archivo,
            tamano_archivo=len(crudos), enviado=1, descargado=1,
            estado="PENDIENTE", caption=pie)
        solicitud = {
            "id": id_msg,
            "tipo": TipoMensaje.MENSAJE_IMAGEN,
            "remitente": self.codigo,
            "destinatario": destinatario,
            "contenidoImagen": contenido,
            "nombreArchivo": nombre,
            "fechaHora": fecha,
        }
        if pie:
            solicitud["contenido"] = pie
        try:
            respuesta = self.conexion.pedir(solicitud)
        except (ConnectionError, TimeoutError, OSError):
            self.historial.guardar_pendiente(
                id_msg, TipoMensaje.MENSAJE_IMAGEN, self.codigo,
                destinatario, pie or "", nombre, crudos, fecha)
            return False
        try:
            lanzar_si_error(respuesta, "envío rechazado",
                            ok_tipos=(TipoMensaje.IMAGE_FILTERED_RESULT,))
        except Exception:
            self.historial.marcar_estado(id_msg, "ERROR")
            raise
        self.historial.actualizar_metadatos_remotos(
            id_msg, archivo_id=respuesta.get("archivoId"),
            hash_sha256=respuesta.get("hashSha256"),
            etapas_filtrado=respuesta.get("etapasFiltrado"))
        self.historial.marcar_estado(id_msg, "ENVIADO")
        return True

    def enviar_archivo(self, destinatario, ruta_archivo, id_msg=None,
                       on_progreso=None, caption=None, cancelado=None):
        """Transfiere archivos por partes de 1 MiB hasta el máximo de 50 MiB.

        Con ``cancelado`` (un callable) se verifica ANTES de iniciar y entre
        partes: cancelar a mitad NO deja pendiente — la fila queda en ERROR y
        el lote de la UI recupera los adjuntos no enviados (§7 paridad)."""
        self._exigir_sesion()
        if not destinatario:
            raise ValueError("destinatario es obligatorio")
        ruta = Path(ruta_archivo)
        datos = ruta.read_bytes()
        if len(datos) > ARCHIVO_MAX_BYTES:
            raise ValueError(
                f"Archivo demasiado grande: {len(datos) / 1024 / 1024:.1f} MB (máx 50 MB)")
        sha256 = hashlib.sha256(datos).hexdigest()
        nombre = ruta.name
        mime = mimetypes.guess_type(str(ruta))[0] or "application/octet-stream"
        id_msg = id_msg or str(uuid.uuid4())
        fecha = ahora_utc()
        pie = caption.strip() if caption and caption.strip() else None
        partes = [datos[i:i + PARTE_MAX_BYTES]
                  for i in range(0, len(datos), PARTE_MAX_BYTES)]
        total_partes = max(1, len(partes))
        self.historial.guardar_mensaje(
            id_msg, self.codigo, destinatario, TipoMensaje.MENSAJE_ARCHIVO,
            "[Archivo]", fecha, nombre_archivo=nombre,
            tamano_archivo=len(datos), enviado=1, descargado=1,
            estado="PENDIENTE", caption=pie)

        inicio_enviado = False
        try:
            if cancelado and cancelado():
                raise EnvioCancelado(
                    "envío cancelado antes de iniciar la transferencia")
            solicitud_inicio = {
                "id": id_msg,
                "tipo": TipoMensaje.ARCHIVO_INICIO,
                "remitente": self.codigo,
                "destinatario": destinatario,
                "nombreArchivo": nombre,
                "mime": mime,
                "tamanoArchivo": len(datos),
                "totalPartes": total_partes,
                "fechaHora": fecha,
            }
            if pie:
                solicitud_inicio["contenido"] = pie
            inicio_enviado = True
            inicio = self._pedir_archivo(solicitud_inicio)
            lanzar_si_error(inicio, "inicio de transferencia rechazado")
            for indice, parte in enumerate(partes):
                if cancelado and cancelado():
                    raise EnvioCancelado(
                        "envío cancelado a mitad de la transferencia")
                respuesta_parte = self._pedir_archivo({
                    "id": f"{id_msg}#p{indice}",
                    "tipo": TipoMensaje.ARCHIVO_PARTE,
                    "remitente": self.codigo,
                    "archivoId": id_msg,
                    "indiceParte": indice,
                    "contenidoImagen": base64.b64encode(parte).decode("utf-8"),
                    "fechaHora": fecha,
                })
                lanzar_si_error(respuesta_parte,
                                f"parte {indice + 1}/{total_partes} rechazada")
                if on_progreso:
                    on_progreso(indice + 1, total_partes)
            if cancelado and cancelado():
                raise EnvioCancelado("envío cancelado antes de completar la transferencia")
            respuesta = self._pedir_archivo({
                "id": f"{id_msg}#fin",
                "tipo": TipoMensaje.ARCHIVO_FIN,
                "remitente": self.codigo,
                "archivoId": id_msg,
                "hashSha256": sha256,
                "fechaHora": fecha,
            })
        except EnvioCancelado:
            if inicio_enviado:
                self._cancelar_archivo_en_servidor(id_msg, fecha)
            # Cancelar NO deja pendiente (el usuario no quiere este envío);
            # la fila queda en ERROR para que la burbuja diga «! No enviado».
            self.historial.marcar_estado(id_msg, "ERROR")
            raise
        except (ConnectionError, TimeoutError, OSError):
            if cancelado and cancelado():
                if inicio_enviado:
                    self._cancelar_archivo_en_servidor(id_msg, fecha)
                self.historial.marcar_estado(id_msg, "ERROR")
                raise EnvioCancelado(
                    "envío cancelado antes de completarse tras un fallo de conexión")
            self.historial.guardar_pendiente(
                id_msg, TipoMensaje.MENSAJE_ARCHIVO, self.codigo,
                destinatario, pie or "", nombre, datos, fecha)
            return False

        try:
            lanzar_si_error(respuesta, "archivo rechazado")
        except Exception:
            self.historial.marcar_estado(id_msg, "ERROR")
            raise
        if respuesta.get("archivoId"):
            self.historial.actualizar_archivo_id(id_msg, respuesta["archivoId"])
        self.historial.marcar_estado(id_msg, "ENVIADO")
        return True

    def marcar_leidos(self, destinatario):
        """Encola confirmaciones de lectura y las despacha si hay conexión."""
        self._exigir_sesion()
        for id_mensaje in self.historial.ids_no_leidos(self.codigo, destinatario):
            id_pendiente = "lectura:" + id_mensaje
            if not any(p["id"] == id_pendiente
                       for p in self.historial.obtener_pendientes()):
                self.historial.guardar_pendiente(
                    id_pendiente, TipoMensaje.MENSAJE_LEIDO, self.codigo,
                    destinatario, id_mensaje, None, None,
                    ahora_utc())
        self.historial.marcar_estado_de_conversacion(
            self.codigo, destinatario, "LEIDO")
        return self._reintentar_pendientes()
