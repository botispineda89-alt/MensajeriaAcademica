"""Acciones de selección, envío y navegación de la conversación."""

import threading
from pathlib import Path

from PySide6.QtCore import Qt

from transversal.fechas import ahora_utc
from transversal import iconos_archivo
from ui.enviar_mensaje_comando import EnviarMensajeComando
from ui.plan_envio_adjuntos import Adjunto, PlanEnvioAdjuntos, Texto


class FlujoConversacionChat:
    # §7 paridad: cancelación del lote de adjuntos. La UI marca este Event y el
    # comando lo consulta entre adjuntos y entre partes. Se crea en enviar().
    _cancelacion_envio = None


class FlujoConversacionChat:
    def _seguir_abajo(self):
        """UIX-16: True si ya se está al fondo (margen 60 px)."""
        try:
            barra = self.lista_mensajes.verticalScrollBar()
            return barra.value() >= barra.maximum() - 60
        except Exception:
            return True

    def _cancelar_respuesta(self):
        self._respuesta_mensaje = None
        self.lbl_respuesta.clear()
        self.cita_container.setVisible(False)

    def _cancelar_composicion(self):
        """Esc: cierra la búsqueda si está abierta; si no, cita y adjunto."""
        if self.barra_buscar.isVisible():
            self._cerrar_buscar()
            return
        self._cancelar_respuesta()
        self._quitar_adjunto()

    def _avisar_nuevo(self):
        """UIX-16: llegó algo mientras se lee historial viejo."""
        self._nuevos_pendientes = getattr(self, "_nuevos_pendientes", 0) + 1
        self.btn_nuevos.setText(f"↓ {self._nuevos_pendientes} nuevos")
        self.btn_nuevos.setVisible(True)

    def _responder(self, mensaje):
        self._respuesta_mensaje = dict(mensaje)
        self.lbl_respuesta.setText(self._cita_actual())
        self.cita_container.setVisible(True)
        self.entry_msg.setFocus()

    def _cita_actual(self):
        mensaje = getattr(self, "_respuesta_mensaje", None)
        if not mensaje:
            return ""
        if mensaje.get("is_image"):
            referencia = f"[Imagen: {mensaje.get('filename') or 'imagen'}]"
        elif mensaje.get("es_archivo"):
            referencia = f"[Archivo: {mensaje.get('filename') or 'archivo'}]"
        else:
            referencia = '"%s"' % ((mensaje.get("text") or "mensaje")[:120])
        return f"Respuesta a {mensaje.get('sender')}: {referencia}"

    def _responder_dobleclick(self, indice):
        """Telegram Web: doble-click en cualquier mensaje = responder."""
        try:
            mensaje = self._modelo_mensajes.data(indice, ROL_ITEM)
        except Exception:
            mensaje = None
        if isinstance(mensaje, dict) and mensaje.get("id"):
            self._responder(mensaje)

    def enviar(self):
        texto = self.entry_msg.texto().strip()
        usuario = self.usuario_seleccionado
        rutas = self.bandeja_adjuntos.rutas
        if not usuario or (not texto and not rutas):
            return
        cita = self._cita_actual()
        texto_envio = (cita + "\n\n" + texto) if cita and texto else (cita or texto)
        plan = PlanEnvioAdjuntos.calcular(
            texto_envio, rutas, self._EXTENSIONES_IMAGEN)
        if not plan:
            return
        self.btn_enviar.setEnabled(False)
        self._cancelacion_envio = threading.Event()
        self._mostrar_envio_optimista(plan)
        self.entry_msg.clear()
        self._cancelar_respuesta()
        self._quitar_adjunto()
        comando = EnviarMensajeComando(
            usuario, plan, self._EXTENSIONES_IMAGEN,
            al_progreso=lambda dato: self._eventos.progreso.emit(dato),
            cancelado=self._cancelacion_envio.is_set)
        self._ejecutar(
            "envio", lambda: comando.ejecutar(self.fachada))

    def _mostrar_envio_optimista(self, plan):
        ahora = ahora_utc()
        for accion in plan:
            item = {
                "id": accion.id_mensaje, "sender": "Tú", "date": ahora,
                "state": self._estado_visible("PENDIENTE"),
                "raw_state": "PENDIENTE", "is_own": True,
                "is_image": False, "es_archivo": False,
                "archivo_id": None, "filename": "", "path": "",
                "image_loaded": False, "image_loading": False,
                "image_state": "", "caption": "",
            }
            if isinstance(accion, Texto):
                item["text"] = accion.cuerpo
            elif isinstance(accion, Adjunto):
                ruta = Path(accion.ruta)
                item.update(self._datos_adjunto_optimista(
                    ruta, accion.caption))
            self._historial_ids.add(accion.id_mensaje)
            self._agregar_fila(item)
        self.lista_mensajes.scrollToBottom()

    def _datos_adjunto_optimista(self, ruta, caption):
        es_imagen = ruta.suffix.casefold() in self._EXTENSIONES_IMAGEN
        try:
            tamano = ruta.stat().st_size
            tamano_str = (f"{tamano / 1024 / 1024:.1f} MB"
                          if tamano >= 1024 * 1024
                          else f"{tamano / 1024:.1f} KB")
        except OSError:
            tamano_str = "Tamaño no disponible"
        return {
            "text": "" if es_imagen else (
                f"{iconos_archivo.prefijo(ruta.name)}Enviado archivo: "
                f"{ruta.name}  ({tamano_str})"),
            "is_image": es_imagen, "es_archivo": not es_imagen,
            "filename": ruta.name, "path": str(ruta),
            "image_state": f"Enviada imagen: {ruta.name}" if es_imagen else "",
            "image_loading": es_imagen,
            "caption": caption or "",
        }

    def _ir_a_nuevos(self):
        """UIX-16: baja al fondo y apaga el aviso."""
        self._nuevos_pendientes = 0
        self.btn_nuevos.setVisible(False)
        self.lista_mensajes.scrollToBottom()

    def _cancelar_envio_lote(self):
        """§7 paridad: la UI marca la cancelación; el comando la consulta."""
        if self._cancelacion_envio is not None:
            self._cancelacion_envio.set()
        self.lbl_progreso.setText("Cancelando…")
        self.btn_cancelar_envio.setEnabled(False)

    def _procesar_progreso(self, dato):
        """Recibe {hechos, total, bytes, bytes_total, nombre} del hilo de envío."""
        total_bytes = dato.get("bytes_total") or 0
        hecho_bytes = dato.get("bytes") or 0
        mb_hecho, mb_total = hecho_bytes / 1048576, total_bytes / 1048576
        self.lbl_progreso.setText(
            f"Enviando {dato.get('hechos', 0)}/{dato.get('total', 0)} · "
            f"{mb_hecho:.1f}/{mb_total:.1f} MB · {dato.get('nombre', '')}")
        hay_mas = dato.get("hechos", 0) < dato.get("total", 0)
        self.fila_progreso.setVisible(hay_mas)
        self.btn_cancelar_envio.setEnabled(True)

    def _ocultar_progreso_envio(self):
        self.fila_progreso.setVisible(False)
        self.btn_cancelar_envio.setEnabled(True)

    def _al_seleccionar_usuario(self, actual, anterior=None):
        if actual is None or getattr(self, "_seleccionando", False) \
                or getattr(self, "_reconstruyendo", False):
            return
        try:
            codigo = actual.data(Qt.ItemDataRole.UserRole)
            identidad = actual.data(Qt.ItemDataRole.UserRole + 1)
        except RuntimeError:
            # El item fue liberado por una reconstruccion de la lista
            # (_filtrar_usuarios hace clear()): no hay nada que mostrar.
            return
        if not codigo:
            return
        self.usuario_seleccionado = codigo
        self._preferencias_chat.guardar_ultimo_chat(self.codigo, codigo)
        # UIX-09: al cambiar de chat, las descargas del anterior ya no pintan
        # aquí. (Si es el mismo, no se toca nada.)
        if codigo != getattr(self, "_chat_cargas", codigo):
            self.cancelar_cargas_imagen()
        self._chat_cargas = codigo
        self._nuevos_pendientes = 0
        self.btn_nuevos.setVisible(False)
        self._cancelar_respuesta()
        # UIX-06: los resultados son por conversación; al cambiar, fuera.
        if self.barra_buscar.isVisible():
            self._cerrar_buscar()
        # Limpia la insignia de no-leidos EN EL SITIO: antes se reconstruia
        # la lista entera aqui (~236 ms con 1000+ contactos) y el setCurrentItem
        # anidado podia robar la seleccion en plena rafaga de presencia.
        try:
            if isinstance(getattr(self, "_no_leidos", None), dict) and codigo in self._no_leidos:
                self._no_leidos.pop(codigo, None)
                self._limpiar_insignia(codigo)
        except Exception:
            pass
        self.lbl_conversacion.setText(identidad or codigo)
        self.lbl_presencia.setText("en línea" if any(
            u.get("codigo") == codigo and u.get("conectado") for u in self._directorio)
            else "desconectado")
        self._offset = 0
        self._gen_historial += 1
        self._pagina_remota[codigo] = 0
        self._total_remoto.pop(codigo, None)
        self._historial_ids.clear()
        self._modelo_mensajes.limpiar()
        self._solicitar_historial(codigo, 0)
        if hasattr(self.fachada, "traer_historial"):
            self._solicitar_historial_remoto(codigo, 0, False)
        if hasattr(self.fachada, "marcar_leidos"):
            self._ejecutar("marcar_leidos", lambda: self.fachada.marcar_leidos(codigo))

    def _actualizar_envio(self):
        hay_adjuntos = bool(self.bandeja_adjuntos.rutas)
        self.preview_container.setVisible(hay_adjuntos)
        self.btn_quitar_adjunto.setVisible(hay_adjuntos)
        self.btn_enviar.setEnabled(bool(self.usuario_seleccionado)
                                   and bool(self.entry_msg.texto().strip() or hay_adjuntos))
