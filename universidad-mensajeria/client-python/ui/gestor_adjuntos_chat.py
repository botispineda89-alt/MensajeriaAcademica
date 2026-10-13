"""Selección, transferencia y visualización de adjuntos del chat."""

from pathlib import Path
import shutil

from PySide6.QtWidgets import QFileDialog, QMessageBox

from ui.estado_aviso_inline import TipoAviso
from ui.lista_virtual import ROL_ITEM
from ui.visor_imagen_chat import VisorImagenChat as _VisorImagen
from ui.cargador_adjunto_entrada_chat import CargadorAdjuntoEntradaChat


class GestorAdjuntosChat:
    # FORMATOS: lo mismo que el servidor acepta en su tubería
    # (Imagenes.esImagenValida): PNG/JPEG/GIF/BMP, WebP, AVIF, HEIC/HEIF y
    # SVG. Todo lo de esta lista viaja como MENSAJE_IMAGEN y el servidor le
    # aplica los filtros; lo demás viaja como archivo.
    _EXTENSIONES_IMAGEN = {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp",
        ".avif", ".heic", ".heif", ".svg",
    }
    _cargador_adjunto_entrada = CargadorAdjuntoEntradaChat()

    def _fijar_imagen_adjunta(self, ruta):
        if not ruta:
            return
        agregadas, rechazadas = self.bandeja_adjuntos.agregar([ruta])
        if rechazadas:
            self._aviso_red("No se pudo adjuntar el archivo o se alcanzó el máximo de adjuntos.")
        if agregadas:
            self.preview_container.setVisible(True)
            self.btn_quitar_adjunto.setVisible(True)

    def _quitar_adjunto(self):
        self.bandeja_adjuntos.limpiar()
        self.preview_container.setVisible(False)
        self.btn_quitar_adjunto.setVisible(False)

    def adjuntar_imagen(self):
        """Añade archivos múltiples a la bandeja de envío."""
        if not self.usuario_seleccionado:
            QMessageBox.information(self, "Seleccionar conversación",
                                    "Selecciona una conversación primero.")
            return
        rutas, _ = QFileDialog.getOpenFileNames(
            self, "Seleccionar archivo", "",
            "Todos los archivos (*.*);;"
            "Imágenes (*.png *.jpg *.jpeg *.gif *.bmp *.webp *.avif *.heic *.heif *.svg);;"
            "Documentos (*.pdf *.docx *.xlsx *.pptx *.txt *.zip)")
        agregadas, rechazadas = self.bandeja_adjuntos.agregar(rutas)
        if agregadas:
            self.preview_container.setVisible(True)
            self.btn_quitar_adjunto.setVisible(True)
        if rechazadas:
            self._aviso_red(
                "Algunos archivos no se adjuntaron; máximo 12 archivos por envío.",
                TipoAviso.ADVERTENCIA)

    def dragEnterEvent(self, event):
        if event.mimeData().hasUrls():
            for url in event.mimeData().urls():
                if Path(url.toLocalFile()).is_file():
                    event.acceptProposedAction()
                    return
        event.ignore()

    def dragMoveEvent(self, event):
        if event.mimeData().hasUrls():
            for url in event.mimeData().urls():
                if Path(url.toLocalFile()).is_file():
                    event.acceptProposedAction()
                    return
        event.ignore()

    def dropEvent(self, event):
        if event.mimeData().hasUrls():
            if not self.usuario_seleccionado:
                QMessageBox.information(
                    self, "Seleccionar conversación",
                    "Selecciona una conversación primero.")
                event.ignore()
                return
            rutas = [url.toLocalFile() for url in event.mimeData().urls()
                     if url.isLocalFile()]
            agregadas, rechazadas = self.bandeja_adjuntos.agregar(rutas)
            if agregadas:
                self.preview_container.setVisible(True)
                self.btn_quitar_adjunto.setVisible(True)
            if rechazadas:
                self._aviso_red(
                    "Algunos archivos arrastrados no se pudieron adjuntar.",
                    TipoAviso.ADVERTENCIA)
            if agregadas:
                event.acceptProposedAction()
                return
            event.ignore()
            return
        event.ignore()

    def _cargar_imagenes_visibles(self, *_):
        modelo = self._modelo_mensajes
        if not self.isVisible() or modelo.rowCount() == 0:
            return
        viewport = self.lista_mensajes.viewport().rect()
        for indice in range(modelo.rowCount()):
            qi = modelo.index(indice)
            rect = self.lista_mensajes.visualRect(qi)
            if not rect.isValid() or not viewport.intersects(rect):
                continue
            mensaje = modelo.data(qi, ROL_ITEM)
            if not isinstance(mensaje, dict):
                continue
            if not mensaje.get("is_image") or mensaje.get("image_loading") or mensaje.get("image_loaded"):
                continue
            if mensaje.get("raw_state") == "PENDIENTE":
                continue
            mensaje["image_loading"] = True
            mensaje["image_error"] = ""
            modelo.refrescar(mensaje.get("id"))
            def cargar(m=mensaje):
                try:
                    return self._cargar_imagen(m)
                except Exception as error:
                    error.mensaje_id = m.get("id")
                    raise
            # UIX-09: la tarea queda rastreada por id para poder cancelarla.
            self._cargas_imagen[mensaje.get("id")] = self._ejecutar("imagen", cargar)

    def _cargar_imagen(self, mensaje):
        return self._cargador_adjunto_entrada.cargar(
            mensaje, self._directorio_imagenes,
            lambda id_mensaje, ruta: self.fachada.obtener_imagen(id_mensaje, ruta))

    def _mensaje_por_id(self, mensaje_id):
        """Dict del mensaje con ese id, o None si no está cargado."""
        if not mensaje_id:
            return None
        indice = self._modelo_mensajes.indice_por_id(mensaje_id)
        if not indice.isValid():
            return None
        mensaje = self._modelo_mensajes.data(indice, ROL_ITEM)
        return mensaje if isinstance(mensaje, dict) else None

    def _item_y_row_por_id(self, mensaje_id):
        # Compatibilidad: devuelve (dict, None); ya no hay widgets por fila.
        return self._mensaje_por_id(mensaje_id), None

    def _row_por_id(self, mensaje_id):
        return self._mensaje_por_id(mensaje_id)

    def _ampliar_imagen(self, mensaje_id):
        """Un click en imagen abre el visor con zoom (no modal)."""
        try:
            pixmap = self._delegado_mensajes.pixmap_de(mensaje_id)
            if pixmap is None or pixmap.isNull():
                return
            mensaje = self._mensaje_por_id(mensaje_id) or {}
            visor = _VisorImagen(pixmap, mensaje.get("filename") or "Imagen",
                                 self.window())
            visor.show()
        except Exception:
            pass

    def _reintentar_imagen(self, mensaje):
        mensaje["image_loading"] = True
        mensaje["image_error"] = ""
        self._modelo_mensajes.refrescar(mensaje.get("id"))

        def cargar():
            try:
                return self._cargar_imagen(mensaje)
            except Exception as error:
                error.mensaje_id = mensaje.get("id")
                raise

        # UIX-00b: esto vivia DENTRO de cargar(), detras del raise, asi que era
        # codigo inalcanzable y el boton "Reintentar imagen" no reintentaba nada.
        # Se lanza fuera de la funcion; el error lo recoge _ejecutar, que ya
        # muestra el aviso y vuelve a ofrecer Reintentar.
        # UIX-09: rastreada para poder cancelarla al salir de la vista.
        self._cargas_imagen[mensaje.get("id")] = self._ejecutar("imagen", cargar)

    def _descargar_adjunto(self, mensaje):
        es_imagen = bool(mensaje.get("is_image"))
        archivo_id = mensaje.get("archivo_id")
        if not archivo_id and not es_imagen:
            QMessageBox.warning(self, "No se pudo descargar", "El identificador del archivo no está disponible.")
            return
        if es_imagen:
            nombre_defecto = mensaje.get("filename") or "imagen.png"
        else:
            nombre_defecto = mensaje.get("filename") or "archivo.bin"
        ruta_sugerida, _ = QFileDialog.getSaveFileName(
            self, "Guardar archivo", nombre_defecto, "Todos los archivos (*.*)")
        if not ruta_sugerida:
            return

        mensaje["descargando"] = True
        self._modelo_mensajes.refrescar(mensaje.get("id"))

        def descargar():
            try:
                if es_imagen:
                    origen = self.fachada.obtener_imagen(
                        mensaje.get("id"), ruta_sugerida)
                    if Path(origen).resolve() != Path(ruta_sugerida).resolve():
                        shutil.copy2(origen, ruta_sugerida)
                else:
                    self.fachada.descargar_archivo(archivo_id, ruta_sugerida)
                return mensaje.get("id"), ruta_sugerida
            except Exception as error:
                error.mensaje_id = mensaje.get("id")
                raise

        self._ejecutar("descarga", descargar)
