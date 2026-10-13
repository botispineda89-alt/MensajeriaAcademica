"""Aviso visible dentro del chat con cierre automático, severidad y anuncio.

Espejo de ``EstadoAvisoInline`` + ``TipoAviso`` de JavaFX y de
``EstadoAvisoInline``/``TipoAvisoInline`` de .NET: los tres clasifican el aviso
(qué título lleva y cuánto se queda), lo quitan solos y lo anuncian a quien
usa un lector de pantalla. Antes este era un rótulo plano de 5 s: un error y un
"listo" se leían igual y no se distinguían sin color.
"""

from enum import Enum

from PySide6.QtCore import QTimer
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel
from qfluentwidgets import PushButton

from ui.tema import tokens


class TipoAviso(Enum):
    """Nivel del aviso. El color lo pone el tema, no el tipo."""

    ERROR = "error"
    ADVERTENCIA = "advertencia"
    INFORMACION = "informacion"
    EXITO = "exito"


# Plazos idénticos a los de .NET y JavaFX: informa y no estorba, pero el error
# también se va solo (un aviso permanente tapando la conversación es peor).
DURACION_EXITO_MS = 4000
DURACION_INFORMACION_MS = 6000
DURACION_ADVERTENCIA_MS = 8000
DURACION_ERROR_MS = 12000

TITULOS = {
    TipoAviso.ERROR: "Error",
    TipoAviso.ADVERTENCIA: "Atención",
    TipoAviso.INFORMACION: "Aviso",
    TipoAviso.EXITO: "Listo",
}

PLAZOS = {
    TipoAviso.EXITO: DURACION_EXITO_MS,
    TipoAviso.INFORMACION: DURACION_INFORMACION_MS,
    TipoAviso.ADVERTENCIA: DURACION_ADVERTENCIA_MS,
    TipoAviso.ERROR: DURACION_ERROR_MS,
}


def titulo_de(tipo):
    return TITULOS[TipoAviso(tipo)]


def plazo_de(tipo):
    return PLAZOS[TipoAviso(tipo)]


def anuncio_de(mensaje, tipo):
    """Lo que se lee en voz alta: el título da el tono sin depender del color."""
    return f"{titulo_de(tipo)}: {mensaje}"


class EstadoAvisoInline(QFrame):
    def __init__(self, parent=None):
        super().__init__(parent)
        self._modo = "oscuro"
        self._tipo = TipoAviso.INFORMACION
        layout = QHBoxLayout(self)
        layout.setContentsMargins(10, 5, 8, 5)
        self.etiqueta_titulo = QLabel()
        self.etiqueta_titulo.setVisible(False)
        self.etiqueta = QLabel()
        self.etiqueta.setWordWrap(True)
        self.boton_cerrar = PushButton("✕")
        self.boton_cerrar.setFixedSize(28, 26)
        self.boton_cerrar.setAccessibleName("Cerrar aviso")
        self.boton_cerrar.clicked.connect(self.ocultar)
        layout.addWidget(self.etiqueta_titulo)
        layout.addWidget(self.etiqueta, 1)
        layout.addWidget(self.boton_cerrar)
        self.setAccessibleName("Aviso")
        self._temporizador = QTimer(self)
        self._temporizador.setSingleShot(True)
        self._temporizador.timeout.connect(self.ocultar)
        self.setVisible(False)
        self.actualizar_tema(self._modo)

    def mostrar(self, texto, tipo=TipoAviso.INFORMACION, duracion_ms=None):
        """Muestra el aviso con su severidad y lo retira solo a su plazo."""
        if not texto or not texto.strip():
            self.ocultar()
            return
        self._tipo = TipoAviso(tipo)
        self.etiqueta_titulo.setText(titulo_de(self._tipo))
        self.etiqueta_titulo.setVisible(True)
        self.etiqueta.setText(texto.strip())
        self.setVisible(True)
        self._anunciar(anuncio_de(texto.strip(), self._tipo))
        self._temporizador.start(
            duracion_ms if duracion_ms is not None else plazo_de(self._tipo))

    @property
    def tipo(self):
        return self._tipo

    def ocultar(self):
        self._temporizador.stop()
        self.setVisible(False)
        self.etiqueta_titulo.setVisible(False)

    def _anunciar(self, texto):
        """Actualiza la descripción accesible del aviso.

        Un lector de pantalla solo vuelve a leer cuando el texto *cambia*: si el
        mismo aviso salta dos veces seguidas (reintento, segundo error), sin
        limpiar antes se quedaría mudo. Por eso se borra y se vuelve a poner en
        el siguiente turno del bucle de eventos.
        """
        self.setAccessibleDescription("")
        QTimer.singleShot(0, lambda: self.setAccessibleDescription(texto))

    def actualizar_tema(self, modo):
        self._modo = modo
        t = tokens(modo)
        self.setStyleSheet(
            f"QFrame {{ background: {t['tarjeta']};"
            f" border: 1px solid {t['tarjeta_borde']}; border-radius: 6px; }}")
        self.etiqueta.setStyleSheet(
            f"color: {t['texto']}; border: none; background: transparent;")
        self.etiqueta_titulo.setStyleSheet(
            f"color: {t['texto']}; font-weight: 700;"
            f" border: none; background: transparent;")
        self.boton_cerrar.setStyleSheet(
            f"color: {t['texto']}; background: transparent;"
            f" border: 1px solid {t['tarjeta_borde']};")