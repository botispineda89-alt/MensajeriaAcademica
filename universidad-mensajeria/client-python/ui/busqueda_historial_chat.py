"""Acciones de búsqueda del historial desde la interfaz de chat."""

from transversal.textos import CITA_NO_DISPONIBLE


class BusquedaHistorialChat:
    def _mostrar_buscar(self):
        if not self.usuario_seleccionado:
            self.lbl_estado_red.setText("Elige una conversación para buscar en su historial")
            return
        self.barra_buscar.setVisible(True)
        self.entry_buscar_historial.setFocus()
        self.entry_buscar_historial.selectAll()

    def _cerrar_buscar(self):
        self.barra_buscar.setVisible(False)
        self.entry_buscar_historial.clear()
        self.modelo_vista.limpiar_busqueda()
        self.lbl_buscar_cuenta.setText("")
        self._repintar_resaltado("")

    def _buscar_historial_debounced(self, *_):
        """UIX-07: la query a SQLite espera 300 ms tras la última tecla."""
        self._timer_buscar.stop()
        self._timer_buscar.start()

    def _ejecutar_busqueda(self):
        patron = (self.entry_buscar_historial.text() or "").strip()
        if not self.usuario_seleccionado or not patron:
            self.modelo_vista.limpiar_busqueda()
            self._actualizar_cuenta_busqueda()
            self._repintar_resaltado("")
            return
        try:
            ids = self.fachada.buscar_en_historial(self.usuario_seleccionado, patron)
        except Exception:
            self.lbl_buscar_cuenta.setText("Sin resultados")
            return
        primer_id = self.modelo_vista.fijar_coincidencias(patron, ids)
        self._actualizar_cuenta_busqueda()
        self._repintar_resaltado(patron)
        if primer_id:
            self._saltar_a(primer_id, intentos=5)

    def _actualizar_cuenta_busqueda(self):
        modelo = self.modelo_vista
        if not modelo.ids_coincidencia:
            self.lbl_buscar_cuenta.setText(
                "" if not modelo.patron_busqueda else "Sin resultados")
        else:
            self.lbl_buscar_cuenta.setText(
                f"{modelo.indice_coincidencia + 1}/{len(modelo.ids_coincidencia)}")

    def _buscar_siguiente(self):
        id_siguiente = self.modelo_vista.mover_coincidencia(1)
        if id_siguiente is None:
            return
        self._actualizar_cuenta_busqueda()
        self._saltar_a(id_siguiente, intentos=5)

    def _buscar_anterior(self):
        id_anterior = self.modelo_vista.mover_coincidencia(-1)
        if id_anterior is None:
            return
        self._actualizar_cuenta_busqueda()
        self._saltar_a(id_anterior, intentos=5)

    def _repintar_resaltado(self, patron):
        # UIX-01: el patrón vive en el delegado; repinta lo visible.
        self._delegado_mensajes.set_patron(patron or "")

    def _saltar_a(self, id_mensaje, intentos=5):
        """Sube/salta al mensaje citado (Telegram Web style): centrado y con resplandor dorado."""
        if not self.usuario_seleccionado or not id_mensaje:
            return
        indice = self._modelo_mensajes.indice_por_id(id_mensaje)
        if indice.isValid():
            from PySide6.QtCore import QItemSelectionModel, QTimer
            from PySide6.QtWidgets import QAbstractItemView
            self.lista_mensajes.doItemsLayout()
            self.lista_mensajes.scrollTo(
                indice, QAbstractItemView.ScrollHint.PositionAtCenter)
            self.lista_mensajes.doItemsLayout()
            rect = self.lista_mensajes.visualRect(indice)
            barra = self.lista_mensajes.verticalScrollBar()
            if barra is not None and rect.isValid():
                target = barra.value() + rect.top() - (self.lista_mensajes.viewport().height() - rect.height()) // 2
                barra.setValue(max(0, min(barra.maximum(), target)))
            if self.lista_mensajes.selectionModel() is not None:
                self.lista_mensajes.selectionModel().select(
                    indice, QItemSelectionModel.SelectionFlag.ClearAndSelect)
            self.lista_mensajes.setCurrentIndex(indice)
            self.lista_mensajes.viewport().update()
            QTimer.singleShot(1500, self._desmarcar_salto)
            return
        if intentos <= 0 or not self.btn_anteriores.isVisible():
            self.lbl_buscar_cuenta.setText("Fuera del historial cargado")
            return
        self.modelo_vista.programar_salto(id_mensaje, intentos - 1)
        self._cargar_anteriores()

    def _desmarcar_salto(self):
        if self.lista_mensajes.selectionModel() is not None:
            self.lista_mensajes.selectionModel().clearSelection()

    def _navegar_cita(self, item, cita):
        """UIX-24: clic en el bloque citado → salta al mensaje original."""
        from transversal.formato_cita import extracto
        from ui.estado_aviso_inline import TipoAviso

        if not cita or not self.usuario_seleccionado:
            return

        item_id = item.get("id") if isinstance(item, dict) else None
        destino = None
        texto = extracto(cita)
        nombre_adjunto = ""
        es_imagen = "[Imagen" in cita
        if not texto:
            if es_imagen:
                idx = cita.find("[Imagen:")
                if idx >= 0:
                    nombre_adjunto = cita[idx + 8:cita.rfind("]")].strip()
            elif "[Archivo" in cita:
                idx = cita.find("[Archivo:")
                if idx >= 0:
                    nombre_adjunto = cita[idx + 9:cita.rfind("]")].strip()

        # 1. Buscar en el historial por el puerto (PAT-04)
        patron = texto or nombre_adjunto
        if patron and getattr(self.fachada, "historial", None):
            try:
                hallados = self.fachada.historial.buscar(
                    self.codigo, self.usuario_seleccionado, patron, 200)
            except Exception:
                hallados = []
            candidatos = [h for h in hallados if getattr(h, "id_mensaje", None) != item_id]
            no_respuestas = [h for h in candidatos if not (getattr(h, "contenido", "") or "").startswith("Respuesta a ")]
            if no_respuestas:
                destino = no_respuestas[0].id_mensaje
            elif candidatos:
                destino = candidatos[0].id_mensaje

        # 2. Si no devolvió nada, buscar en los mensajes ya cargados en memoria
        if not destino:
            for f in self._modelo_mensajes.mensajes():
                if f.get("id") == item_id or f.get("es_separador"):
                    continue
                if es_imagen and f.get("is_image"):
                    if not nombre_adjunto or nombre_adjunto in (f.get("filename") or ""):
                        destino = f.get("id")
                        break
                elif nombre_adjunto and f.get("es_archivo"):
                    if nombre_adjunto in (f.get("filename") or ""):
                        destino = f.get("id")
                        break
                elif texto:
                    cuerpo_f = (f.get("text") or "").strip()
                    caption_f = (f.get("caption") or "").strip()
                    if cuerpo_f == texto or (texto in cuerpo_f and not cuerpo_f.startswith("Respuesta a ")):
                        destino = f.get("id")
                        break
                    if caption_f and (caption_f == texto or texto in caption_f):
                        destino = f.get("id")
                        break

        if not destino:
            self.aviso_inline.mostrar(CITA_NO_DISPONIBLE, TipoAviso.INFORMACION)
            return

        if not self._modelo_mensajes.indice_por_id(destino).isValid() \
                and not self.btn_anteriores.isVisible():
            self.aviso_inline.mostrar(CITA_NO_DISPONIBLE, TipoAviso.INFORMACION)
            return

        self._saltar_a(destino, intentos=5)
