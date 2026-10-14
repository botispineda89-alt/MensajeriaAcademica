"""Procesamiento de eventos de red visibles en la conversación."""

import uuid

from PySide6.QtWidgets import QMessageBox

from transversal import iconos_archivo
from transversal.estado_conexion import CONECTADO, muestra_banner, texto as texto_estado
from transversal.textos import SESION_CERRADA_DEFECTO
from transversal.tipo_mensaje import TipoMensaje


class ProcesadorEventosChat:
    def _procesar_mensaje(self, mensaje):
        tipo = mensaje.get("tipo")
        if tipo in (TipoMensaje.MENSAJE_ENTREGADO, TipoMensaje.MENSAJE_LEIDO):
            id_mensaje = mensaje.get("contenido")
            if id_mensaje:
                nuevo_estado = "LEIDO" if tipo == TipoMensaje.MENSAJE_LEIDO else "ENTREGADO"
                existente = self._mensaje_por_id(id_mensaje)
                if existente is not None and existente.get("is_own"):
                    existente["state"] = self._estado_visible(nuevo_estado)
                    existente["raw_state"] = nuevo_estado
                    self._modelo_mensajes.refrescar(id_mensaje)
            if self.usuario_seleccionado:
                self._solicitar_historial(self.usuario_seleccionado, 0)
            return
        if tipo == TipoMensaje.PRESENCIA:
            return
        # Mensaje nuevo (texto/imagen/archivo): la tarjeta del remitente sube
        # arriba con preview e insignia, como en WhatsApp. Coalesce por ráfagas.
        self._programar_refresco_recientes()
        remitente = mensaje.get("remitente")
        propio = remitente == self.codigo
        conversacion = (
            mensaje.get("destinatario") if propio else remitente
        )
        if conversacion and conversacion == self.usuario_seleccionado:
            id_mensaje = mensaje.get("id") or str(uuid.uuid4())
            existente = self._mensaje_por_id(id_mensaje)
            if existente is not None:
                if propio:
                    existente.update({
                        "sender": "Tú",
                        "is_own": True,
                        "state": self._estado_visible("ENVIADO"),
                        "raw_state": "ENVIADO",
                    })
                existente["archivo_id"] = mensaje.get(
                    "archivoId", existente.get("archivo_id"))
                self._modelo_mensajes.refrescar(id_mensaje)
            else:
                usuario = next((u for u in self._directorio if u.get("codigo") == remitente), {})
                nombre = " ".join(filter(None, [usuario.get("nombres"), usuario.get("apellidos")])).strip()
                identidad = "Tú" if propio else (
                    f"{nombre} [{remitente}]" if nombre else remitente)
                es_imagen = tipo == TipoMensaje.MENSAJE_IMAGEN
                es_archivo = tipo == TipoMensaje.MENSAJE_ARCHIVO
                if es_archivo:
                    nombre_archivo = mensaje.get("nombreArchivo") or "archivo"
                    prefijo_archivo = "Enviado archivo" if propio else "Archivo"
                    gancho = iconos_archivo.prefijo(nombre_archivo)
                    texto_burbuja = f"{gancho}{prefijo_archivo}: {nombre_archivo}"
                else:
                    texto_burbuja = "" if es_imagen else (mensaje.get("contenido") or "")
                item = {
                    "id": id_mensaje, "sender": identidad,
                    "date": mensaje.get("fechaHora", ""),
                    "text": texto_burbuja,
                    "state": self._estado_visible("ENVIADO") if propio else "",
                    "raw_state": "ENVIADO" if propio else "", "is_own": propio,
                    "is_image": es_imagen, "es_archivo": es_archivo,
                    "archivo_id": mensaje.get("archivoId"),
                    "etapas_filtrado": mensaje.get("etapasFiltrado"),
                    "caption": (mensaje.get("contenido") or "")
                    if es_imagen or es_archivo else "",
                    "filename": mensaje.get("nombreArchivo") or "",
                    "path": "", "image_loaded": False, "image_loading": False,
                    "image_state": (
                        f"Enviada imagen: {mensaje.get('nombreArchivo')}"
                        if es_imagen and propio else
                        f"Imagen: {mensaje.get('nombreArchivo') or 'imagen'}"
                        if es_imagen else ""),
                    "image_base64": mensaje.get("contenidoImagen")
                }
                self._historial_ids.add(id_mensaje)
                self._agregar_fila(item)
                # UIX-16: solo baja solo si ya se estaba abajo; si no, avisa.
                if self._seguir_abajo():
                    self.lista_mensajes.scrollToBottom()
                else:
                    self._avisar_nuevo()
            self._solicitar_historial(conversacion, 0)
            if not propio and hasattr(self.fachada, "marcar_leidos"):
                self._ejecutar(
                    "marcar_leidos",
                    lambda: self.fachada.marcar_leidos(conversacion))

    def _procesar_desconexion(self):
        # PAT-03: el estado lo lleva _procesar_estado_conexion. Aquí solo queda
        # el aviso de siempre para no perder texto propio.
        self.lbl_estado_red.setText("Conexión cerrada")

    def _procesar_estado_conexion(self, estado):
        """PAT-03: pinta el estado. Llega ya en el hilo de la GUI (Signal)."""
        self.lbl_offline.setVisible(muestra_banner(estado))
        if estado != CONECTADO:
            self.lbl_estado_red.setText(texto_estado(estado))
        elif self.fachada.codigo_actual:
            # Al volver la red no se pisa el contador de contactos que calcula
            # `_pintar_directorio`; solo se limpa el aviso de caída.
            if "Sin conexi" in self.lbl_estado_red.text() or \
                    "Sesión cerrada" in self.lbl_estado_red.text():
                self.lbl_estado_red.setText("En línea")

    def _procesar_cierre(self, mensaje):
        motivo = mensaje.get("motivo") or mensaje.get("mensajeError") or SESION_CERRADA_DEFECTO
        QMessageBox.information(self, "Sesión cerrada", motivo)
        self._cerrando = True
        self.fachada.desconectar()
        self.on_logout()
        self.close()
