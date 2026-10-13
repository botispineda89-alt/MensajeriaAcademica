"""Ejecuta de forma secuencial el plan de envío de una conversación.

Con ``al_progreso`` informa el avance del lote (adjuntos hechos, bytes) y con
``cancelado`` se detiene: lo ya enviado queda enviado y lo restante vuelve a la
bandeja (§7 del plan de paridad — paridad con el ``ProgresoEnvio`` de .NET).
"""

from inspect import Parameter, signature
from pathlib import Path

from ui.plan_envio_adjuntos import Adjunto, Texto


class EnviarMensajeComando:
    def __init__(self, usuario, plan, extensiones_imagen,
                 al_progreso=None, cancelado=None):
        self.usuario = usuario
        self.plan = tuple(plan)
        self.extensiones_imagen = frozenset(extensiones_imagen)
        self.al_progreso = al_progreso
        self.cancelado = cancelado
        self.bytes_total = sum(
            Path(accion.ruta).stat().st_size
            for accion in self.plan
            if isinstance(accion, Adjunto) and Path(accion.ruta).is_file())

    def ejecutar(self, puerto):
        ids = []
        avisos = []
        estados = {}
        restantes = []
        bytes_enviados = 0
        total = len(self.plan)
        indice = 1
        for accion in self.plan:
            if self._cancelado():
                restantes.extend(
                    self._restante_de(queda) for queda in self.plan[indice - 1:])
                break
            nombre = self._nombre_de(accion)
            self._informar(indice - 1, total, bytes_enviados, nombre)
            try:
                if isinstance(accion, Texto):
                    ids.append(accion.id_mensaje)
                    salio = self._invocar(
                        puerto.enviar_texto, self.usuario, accion.cuerpo,
                        accion.id_mensaje)
                    estados[accion.id_mensaje] = self._registrar_resultado(
                        salio, "El mensaje de texto", avisos)
                elif isinstance(accion, Adjunto):
                    ids.append(accion.id_mensaje)
                    salio = self._enviar_adjunto(
                        puerto, accion.ruta, accion.id_mensaje, accion.caption,
                        bytes_enviados)
                    estados[accion.id_mensaje] = self._registrar_resultado(
                        salio, Path(accion.ruta).name, avisos)
                    bytes_enviados += self._tamano(accion.ruta)
                else:
                    raise TypeError(f"Acción de envío no reconocida: {type(accion).__name__}")
            except Exception as error:
                id_mensaje = getattr(accion, "id_mensaje", None)
                if id_mensaje is not None:
                    estados[id_mensaje] = None
                avisos.append(f"No se pudo completar el envío: {error}")
            self._informar(indice, total, bytes_enviados, nombre)
            indice += 1
        en_red = all(estado is True for estado in estados.values())
        return self.usuario, en_red, ids, avisos, estados, restantes

    def _enviar_adjunto(self, puerto, ruta, id_mensaje, caption, bytes_previos):
        extension = Path(ruta).suffix.casefold()
        if extension in self.extensiones_imagen:
            return self._invocar(
                puerto.enviar_imagen, self.usuario, ruta, id_mensaje, caption)
        tamano = self._tamano(ruta)
        on_progreso = None
        if self.al_progreso is not None:
            def on_progreso(partes, total_partes):
                self.al_progreso({
                    "bytes": bytes_previos + int(tamano * partes / max(1, total_partes)),
                    "bytes_total": self.bytes_total,
                })
        return self._invocar(
            puerto.enviar_archivo, self.usuario, ruta, id_mensaje, caption,
            on_progreso=on_progreso, cancelado=self.cancelado)

    def _informar(self, hechos, total, bytes_enviados, nombre):
        if self.al_progreso is None:
            return
        self.al_progreso({
            "hechos": hechos, "total": total,
            "bytes": bytes_enviados, "bytes_total": self.bytes_total,
            "nombre": nombre,
        })

    def _cancelado(self):
        return bool(self.cancelado and self.cancelado())

    @staticmethod
    def _restante_de(accion):
        if isinstance(accion, Adjunto):
            return {"ruta": accion.ruta, "caption": accion.caption}
        return {"ruta": None, "caption": accion.cuerpo if isinstance(accion, Texto) else None}

    @staticmethod
    def _nombre_de(accion):
        if isinstance(accion, Adjunto):
            return Path(accion.ruta).name
        return "El mensaje de texto"

    @staticmethod
    def _tamano(ruta):
        try:
            return Path(ruta).stat().st_size
        except OSError:
            return 0

    def _registrar_resultado(self, resultado, nombre, avisos):
        if resultado is False:
            avisos.append(f"Sin red: {nombre} quedó en cola para reintentar.")
            return False
        return True

    @staticmethod
    def _invocar(metodo, usuario, contenido, id_mensaje, caption=None,
                 on_progreso=None, cancelado=None):
        parametros = signature(metodo).parameters
        acepta_kwargs = any(
            parametro.kind == Parameter.VAR_KEYWORD
            for parametro in parametros.values())
        argumentos = {}
        if "id_msg" in parametros or acepta_kwargs:
            argumentos["id_msg"] = id_mensaje
        if caption is not None and ("caption" in parametros or acepta_kwargs):
            argumentos["caption"] = caption
        if on_progreso is not None and (
                "on_progreso" in parametros or acepta_kwargs):
            argumentos["on_progreso"] = on_progreso
        if cancelado is not None and (
                "cancelado" in parametros or acepta_kwargs):
            argumentos["cancelado"] = cancelado
        return metodo(usuario, contenido, **argumentos)
