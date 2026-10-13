"""Comprueba que los fondos de chat se cargan y se pintan en el viewport.

Regresión del smoke: en la ventana real el fondo quedaba plano aunque la suite
pasara. La causa era que el pincel de paleta del viewport lo anula cualquier
stylesheet de un ancestro (`_panel_chat.setStyleSheet` en el chat real), así
que aquí el caso que MANDA es el de la lista dentro de un panel con QSS.
"""
import os
import unittest
from pathlib import Path

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

from PySide6.QtWidgets import QApplication, QMainWindow, QVBoxLayout, QWidget

from ui.lista_mensajes_chat import ListaMensajesChat
from ui.tema import MODO_CLARO, MODO_OSCURO, aplicar_tema_fluent


class PruebasFondoChat(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.app = QApplication.instance() or QApplication([])
        cls.directorio_fondos = Path(__file__).resolve().parents[1] / "ui" / "fondos"

    def _colores_renderizados(self, lista):
        captura = lista.grab().toImage()
        viewport = lista.viewport().geometry()
        colores = set()
        for y in range(8, viewport.height() - 8, 7):
            for x in range(8, viewport.width() - 8, 7):
                color = captura.pixelColor(viewport.x() + x, viewport.y() + y)
                colores.add((color.red(), color.green(), color.blue()))
        return colores

    def test_fondos_de_ambos_temas_se_pintan_en_el_viewport(self):
        for nombre in ("fondo_chat_oscuro.jpg", "fondo_chat_claro.jpg"):
            with self.subTest(tema=nombre):
                aplicar_tema_fluent(
                    MODO_OSCURO if "oscuro" in nombre else MODO_CLARO)
                lista = ListaMensajesChat()
                lista.setStyleSheet("QListView { border: 0; padding: 9px; }")
                lista.resize(480, 320)
                lista.fijar_fondo(self.directorio_fondos / nombre)
                lista.show()
                self.app.processEvents()
                self.addCleanup(lista.close)

                self.assertFalse(lista._imagen_fondo.isNull())
                colores = self._colores_renderizados(lista)
                self.assertGreater(
                    len(colores), 15, "El viewport quedó con un fondo plano")

    def test_el_fondo_sobrevive_a_un_ancestro_con_stylesheet(self):
        # El estado real del chat: `aplicar_tema` pinta los paneles con QSS.
        # Antes eso apagaba el pincel de paleta y el fondo no se veía nunca.
        for nombre in ("fondo_chat_oscuro.jpg", "fondo_chat_claro.jpg"):
            with self.subTest(tema=nombre):
                lista = ListaMensajesChat()
                lista.resize(480, 320)
                lista.fijar_fondo(self.directorio_fondos / nombre)
                panel = QWidget()
                panel.setStyleSheet("background: #0E1621;")
                disposicion = QVBoxLayout(panel)
                disposicion.addWidget(lista)
                ventana = QMainWindow()
                ventana.setCentralWidget(panel)
                ventana.resize(480, 320)
                ventana.show()
                self.app.processEvents()
                self.addCleanup(ventana.close)

                colores = self._colores_renderizados(lista)
                self.assertGreater(
                    len(colores), 15,
                    "El fondo ilustrado desaparece si el panel tiene QSS")

    def test_fondo_inexistente_o_ilegible_falla_temprano(self):
        lista = ListaMensajesChat()
        self.addCleanup(lista.close)
        with self.assertRaises(FileNotFoundError):
            lista.fijar_fondo(self.directorio_fondos / "no_existe.jpg")
        vacia = self.directorio_fondos.parent / "tema.py"
        if vacia.is_file():
            with self.assertRaises(RuntimeError):
                lista.fijar_fondo(vacia)


if __name__ == "__main__":
    unittest.main()
