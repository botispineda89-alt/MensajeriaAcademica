"""Aviso visible dentro del chat con cierre automático y accesible."""

from PySide6.QtCore import QTimer
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel
from qfluentwidgets import PushButton

from ui.tema import tokens


class EstadoAvisoInline(QFrame):
    def __init__(self, parent=None):
        super().__init__(parent)
        self._modo = "oscuro"
        layout = QHBoxLayout(self)
        layout.setContentsMargins(10, 5, 8, 5)
        self.etiqueta = QLabel()
        self.etiqueta.setWordWrap(True)
        self.boton_cerrar = PushButton("✕")
        self.boton_cerrar.setFixedSize(28, 26)
        self.boton_cerrar.setAccessibleName("Cerrar aviso")
        self.boton_cerrar.clicked.connect(self.ocultar)
        layout.addWidget(self.etiqueta, 1)
        layout.addWidget(self.boton_cerrar)
        self._temporizador = QTimer(self)
        self._temporizador.setSingleShot(True)
        self._temporizador.timeout.connect(self.ocultar)
        self.setVisible(False)
        self.actualizar_tema(self._modo)

    def mostrar(self, texto, duracion_ms=5000):
        self.etiqueta.setText(texto)
        self.setVisible(True)
        self._temporizador.start(max(1000, duracion_ms))

    def ocultar(self):
        self._temporizador.stop()
        self.setVisible(False)

    def actualizar_tema(self, modo):
        self._modo = modo
        t = tokens(modo)
        self.setStyleSheet(
            f"QFrame {{ background: {t['tarjeta']};"
            f" border: 1px solid {t['tarjeta_borde']}; border-radius: 6px; }}")
        self.etiqueta.setStyleSheet(
            f"color: {t['texto']}; border: none; background: transparent;")
        self.boton_cerrar.setStyleSheet(
            f"color: {t['texto']}; background: transparent;"
            f" border: 1px solid {t['tarjeta_borde']};")
