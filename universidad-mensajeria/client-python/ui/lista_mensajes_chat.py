"""Lista virtualizada con un fondo de conversación escalado a modo cover.

El fondo se pinta en el ``paintEvent`` de la lista SOBRE el viewport, igual
que el cliente Java lo declara en su CSS (``.message-list
{ -fx-background-image: url(...); -fx-background-size: cover; }``). Dos vías
previas no sirven aquí:

- La paleta (brush TexturePattern en ``QPalette.Base``): basta que un ancestro
  tenga un stylesheet (``_panel_chat.setStyleSheet`` en el chat real) para que
  QStyleSheetStyle tome el control del viewport y el pincel se ignore — el
  fondo quedaba plano y los jpg ``fondo_chat_claro/oscuro`` nunca se veían.
- Un widget-viewport propio con ``paintEvent``: QAbstractScrollArea consume el
  Paint del viewport y NO invoca el paintEvent de ese widget.

Pintar en el ``paintEvent`` de la lista (que sí recibe el paint del viewport,
vía QAbstractScrollArea::paintEvent) sobrevive a cualquier stylesheet de
ancestros y permite escalar a cover con centro, como Java.
"""

from pathlib import Path

from PySide6.QtCore import Qt
from PySide6.QtGui import QImage, QPainter, QPixmap
from PySide6.QtWidgets import QListView


class ListaMensajesChat(QListView):
    def __init__(self, parent=None):
        super().__init__(parent)
        self._imagen_fondo = QImage()
        self._clave_escala = None
        self._pixmap_fondo = QPixmap()

    def fijar_fondo(self, ruta):
        ruta = Path(ruta)
        if not ruta.is_file():
            raise FileNotFoundError(f"No se encontró el fondo de chat: {ruta}")
        imagen = QImage(str(ruta))
        if imagen.isNull():
            raise RuntimeError(f"No se pudo decodificar el fondo de chat: {ruta}")
        self._imagen_fondo = imagen
        self._clave_escala = None
        self.viewport().update()

    def resizeEvent(self, evento):
        super().resizeEvent(evento)
        # El viewport cambia de tamaño con la lista: la escala a cover caduca.
        self._clave_escala = None
        self.viewport().update()

    def paintEvent(self, evento):
        if not self._imagen_fondo.isNull():
            pixmap = self._pixmap_a_cover(self.viewport().size())
            if not pixmap.isNull():
                pintor = QPainter(self.viewport())
                # Cover, centrado: `-fx-background-position: center center`.
                x = max(0, (self.viewport().width() - pixmap.width()) // 2)
                y = max(0, (self.viewport().height() - pixmap.height()) // 2)
                pintor.drawPixmap(x, y, pixmap)
        super().paintEvent(evento)

    def _pixmap_a_cover(self, tamano):
        """Escala la imagen a cover y recorta al viewport, con caché."""
        ancho, alto = tamano.width(), tamano.height()
        if ancho <= 0 or alto <= 0:
            return QPixmap()
        clave = (ancho, alto, self._imagen_fondo.cacheKey())
        if clave != self._clave_escala:
            escalada = self._imagen_fondo.scaled(
                tamano,
                Qt.AspectRatioMode.KeepAspectRatioByExpanding,
                Qt.TransformationMode.SmoothTransformation,
            )
            x = max(0, (escalada.width() - ancho) // 2)
            y = max(0, (escalada.height() - alto) // 2)
            self._pixmap_fondo = QPixmap.fromImage(
                escalada.copy(x, y, ancho, alto))
            self._clave_escala = clave
        return self._pixmap_fondo
