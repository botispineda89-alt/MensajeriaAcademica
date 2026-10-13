import tempfile
import unittest
from pathlib import Path

import os
os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

from PySide6.QtWidgets import QApplication, QLabel, QPushButton

from ui.bandeja_adjuntos_chat import BandejaAdjuntosChat
from ui.plan_envio_adjuntos import MAXIMO_ADJUNTOS
from ui.tema import MODO_CLARO, MODO_OSCURO, tokens


class PruebasBandejaAdjuntosChat(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.app = QApplication.instance() or QApplication([])

    def test_agrega_archivos_elimina_individualmente_y_no_duplica(self):
        with tempfile.TemporaryDirectory() as temp:
            rutas = []
            for indice in range(3):
                ruta = Path(temp) / f"{indice}.pdf"
                ruta.write_bytes(b"file")
                rutas.append(str(ruta))
            bandeja = BandejaAdjuntosChat()
            cambios = []
            bandeja.rutas_cambiadas.connect(lambda: cambios.append(1))

            agregadas, rechazadas = bandeja.agregar(
                [rutas[0], rutas[0], rutas[1]])

            self.assertEqual(2, len(agregadas))
            self.assertEqual([], rechazadas)
            self.assertEqual(tuple(agregadas), bandeja.rutas)
            self.assertTrue(bandeja.isVisible())
            boton = bandeja._layout.itemAt(0).widget().findChild(QPushButton)
            self.assertEqual(f"Quitar adjunto {Path(rutas[0]).name}",
                             boton.accessibleName())
            bandeja.quitar(rutas[0])
            self.assertEqual((agregadas[1],), bandeja.rutas)
            self.assertEqual(2, len(cambios))
            bandeja.close()

    def test_limita_a_doce_y_reporta_rechazados(self):
        with tempfile.TemporaryDirectory() as temp:
            rutas = []
            for indice in range(MAXIMO_ADJUNTOS + 1):
                ruta = Path(temp) / f"{indice}.txt"
                ruta.write_text("adjunto", encoding="utf-8")
                rutas.append(str(ruta))
            bandeja = BandejaAdjuntosChat()

            agregadas, rechazadas = bandeja.agregar(rutas)

            self.assertEqual(MAXIMO_ADJUNTOS, len(agregadas))
            self.assertEqual([rutas[-1]], rechazadas)
            bandeja.close()

    def test_icono_usa_el_color_del_tema_actual(self):
        with tempfile.TemporaryDirectory() as temp:
            ruta = Path(temp) / "documento.pdf"
            ruta.write_text("archivo", encoding="utf-8")
            bandeja = BandejaAdjuntosChat()
            bandeja.agregar([str(ruta)])

            for modo in (MODO_OSCURO, MODO_CLARO):
                bandeja.actualizar_tema(modo)
                fila = bandeja._layout.itemAt(0).widget()
                icono = fila.findChildren(QLabel)[0]
                self.assertIn(tokens(modo)["texto"], icono.styleSheet())
            bandeja.close()

    def test_montaje_es_vista_previa_local_y_preserva_adjuntos_originales(self):
        with tempfile.TemporaryDirectory() as temp:
            rutas = []
            for indice, color in enumerate(("red", "blue")):
                ruta = Path(temp) / f"{indice}.png"
                from PIL import Image
                Image.new("RGB", (20, 20), color).save(ruta)
                rutas.append(str(ruta))
            bandeja = BandejaAdjuntosChat()
            bandeja.agregar(rutas)

            self.assertTrue(bandeja._montaje_preview.isVisible())
            self.assertEqual(
                "Montaje visual de 2 imágenes. Cada imagen se enviará individualmente.",
                bandeja._montaje_preview.accessibleDescription())
            self.assertTrue(bandeja._montaje_preview.pixmap())
            self.assertEqual(tuple(str(Path(ruta).resolve()) for ruta in rutas),
                             bandeja.rutas)
            self.assertFalse(any(
                Path(ruta).name.startswith("montaje_") for ruta in bandeja.rutas))
            bandeja.close()


if __name__ == "__main__":
    unittest.main()
