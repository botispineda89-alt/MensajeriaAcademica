"""Bandeja de selección múltiple con eliminación individual de adjuntos."""

from io import BytesIO
from pathlib import Path

from PySide6.QtCore import Qt, Signal
from PySide6.QtGui import QPixmap
from PySide6.QtWidgets import (
    QFrame, QHBoxLayout, QLabel, QVBoxLayout, QWidget,
)
from qfluentwidgets import PushButton

from transversal import iconos_archivo
from ui.decodificador_imagen import a_png_bytes
from ui.montaje_imagenes import componer_en_memoria
from ui.plan_envio_adjuntos import MAXIMO_ADJUNTOS, MAXIMO_IMAGENES_POR_MONTAJE
from ui.tema import tokens

EXTENSIONES_IMAGEN = frozenset({
    ".bmp", ".gif", ".jpeg", ".jpg", ".png", ".webp",
    ".avif", ".heic", ".heif", ".svg",
})


class BandejaAdjuntosChat(QWidget):
    rutas_cambiadas = Signal()

    def __init__(self, parent=None):
        super().__init__(parent)
        self._rutas = []
        self._modo = "oscuro"
        self._layout = QVBoxLayout(self)
        self._layout.setContentsMargins(0, 0, 0, 0)
        self._layout.setSpacing(4)
        self._montaje_preview = QLabel()
        self._montaje_preview.setObjectName("montajeVistaPreviaAdjuntos")
        self._montaje_preview.setAccessibleName("Montaje visual de imágenes")
        self._montaje_preview.setAlignment(Qt.AlignmentFlag.AlignLeft)
        self._montaje_preview.setMaximumHeight(124)
        self._montaje_preview.setVisible(False)
        self._montaje_en_layout = False
        self.setVisible(False)

    @property
    def rutas(self):
        return tuple(self._rutas)

    def agregar(self, rutas):
        agregadas = []
        rechazadas = []
        existentes = {ruta.casefold() for ruta in self._rutas}
        for entrada in rutas:
            ruta = Path(entrada)
            if not ruta.is_file():
                rechazadas.append(str(entrada))
                continue
            normalizada = str(ruta.resolve())
            if normalizada.casefold() in existentes:
                continue
            if len(self._rutas) >= MAXIMO_ADJUNTOS:
                rechazadas.append(normalizada)
                continue
            self._rutas.append(normalizada)
            existentes.add(normalizada.casefold())
            agregadas.append(normalizada)
        if agregadas:
            self._reconstruir()
            self.rutas_cambiadas.emit()
        return agregadas, rechazadas

    def quitar(self, ruta):
        self._rutas = [actual for actual in self._rutas if actual != ruta]
        self._reconstruir()
        self.rutas_cambiadas.emit()

    def limpiar(self):
        if not self._rutas:
            return
        self._rutas.clear()
        self._reconstruir()
        self.rutas_cambiadas.emit()

    def actualizar_tema(self, modo):
        self._modo = modo
        self._reconstruir()

    def _reconstruir(self):
        while self._layout.count():
            elemento = self._layout.takeAt(0)
            widget = elemento.widget()
            if widget is not None and widget is not self._montaje_preview:
                widget.deleteLater()
        self._montaje_en_layout = False
        t = tokens(self._modo)
        self._actualizar_montaje()
        for ruta in self._rutas:
            self._layout.addWidget(self._crear_fila(ruta, t))
        self.setVisible(bool(self._rutas))

    def _actualizar_montaje(self):
        imagenes = [
            ruta for ruta in self._rutas
            if Path(ruta).suffix.casefold() in EXTENSIONES_IMAGEN
        ]
        if not 2 <= len(imagenes) <= MAXIMO_IMAGENES_POR_MONTAJE:
            if self._montaje_en_layout:
                self._layout.removeWidget(self._montaje_preview)
                self._montaje_en_layout = False
            self._montaje_preview.clear()
            self._montaje_preview.setVisible(False)
            return
        try:
            montaje = componer_en_memoria(imagenes)
            datos = BytesIO()
            montaje.save(datos, format="PNG")
            pixmap = QPixmap()
            if not pixmap.loadFromData(datos.getvalue(), "PNG"):
                raise ValueError("Qt no pudo decodificar la vista previa compuesta.")
        except (OSError, ValueError) as error:
            self._montaje_preview.setPixmap(QPixmap())
            self._montaje_preview.setText(
                f"No se pudo crear el montaje de vista previa: {error}")
            if not self._montaje_en_layout:
                self._layout.insertWidget(0, self._montaje_preview)
                self._montaje_en_layout = True
            self._montaje_preview.setVisible(True)
            return
        self._montaje_preview.setText("")
        self._montaje_preview.setPixmap(pixmap.scaled(
            240, 120, Qt.AspectRatioMode.KeepAspectRatio,
            Qt.TransformationMode.SmoothTransformation))
        self._montaje_preview.setToolTip(
            f"Vista previa de {len(imagenes)} imágenes; se enviarán "
            "por separado.")
        self._montaje_preview.setAccessibleDescription(
            f"Montaje visual de {len(imagenes)} imágenes. "
            "Cada imagen se enviará individualmente.")
        if not self._montaje_en_layout:
            self._layout.insertWidget(0, self._montaje_preview)
            self._montaje_en_layout = True
        self._montaje_preview.setVisible(True)

    def _crear_fila(self, ruta, t):
        fila = QFrame()
        fila.setStyleSheet(
            f"QFrame {{ background: {t['preview_fondo']};"
            f" border: 1px solid {t['preview_borde']}; border-radius: 5px; }}")
        layout = QHBoxLayout(fila)
        layout.setContentsMargins(6, 3, 6, 3)
        icono = QLabel()
        icono.setFixedSize(34, 34)
        pixmap = QPixmap(ruta)
        if pixmap.isNull():
            # Un PDF o una hoja de calculo tienen dibujo propio: si no, se
            # decodifica el archivo, porque los formatos que Qt no abre (avif,
            # heic, svg, webp sin soporte) los lleva a PNG el mismo
            # decodificador del chat.
            de_tipo = iconos_archivo.ruta(ruta)
            if de_tipo is not None:
                pixmap = QPixmap(str(de_tipo))
            else:
                try:
                    pixmap.loadFromData(a_png_bytes(ruta), "PNG")
                except Exception:
                    pixmap = QPixmap()
        if not pixmap.isNull():
            icono.setPixmap(pixmap.scaled(
                iconos_archivo.ANCHO_BANDEJA, iconos_archivo.ANCHO_BANDEJA,
                Qt.AspectRatioMode.KeepAspectRatio,
                Qt.TransformationMode.SmoothTransformation))
        else:
            icono.setText("📎")
        icono.setAlignment(Qt.AlignmentFlag.AlignCenter)
        icono.setStyleSheet(
            f"color: {t['texto']}; border: none; background: transparent;")
        detalle = QLabel(Path(ruta).name)
        detalle.setToolTip(ruta)
        detalle.setStyleSheet(f"color: {t['texto']}; border: none; background: transparent;")
        boton = PushButton("✕")
        boton.setFixedSize(28, 26)
        boton.setAccessibleName(f"Quitar adjunto {Path(ruta).name}")
        boton.setToolTip(f"Quitar {Path(ruta).name}")
        boton.setStyleSheet(
            f"color: {t['texto']}; background: transparent;"
            f" border: 1px solid {t['tarjeta_borde']};")
        boton.clicked.connect(lambda _=False, valor=ruta: self.quitar(valor))
        layout.addWidget(icono)
        layout.addWidget(detalle, 1)
        layout.addWidget(boton)
        return fila
