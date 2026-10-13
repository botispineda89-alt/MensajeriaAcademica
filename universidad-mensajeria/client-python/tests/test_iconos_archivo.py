import os
import tempfile
import unittest
from pathlib import Path

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

from PIL import Image
from PySide6.QtCore import QUrl
from PySide6.QtGui import QColor, QImage, QPainter, QTextDocument
from PySide6.QtWidgets import QApplication, QLabel

from transversal import iconos_archivo
from ui.bandeja_adjuntos_chat import BandejaAdjuntosChat
from ui.celdas.mensaje_delegate import MensajeDelegate
from ui.tema import MODO_OSCURO, tokens


class PruebasIconosArchivo(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.app = QApplication.instance() or QApplication([])

    def test_cada_extension_apunta_a_su_icono(self):
        self.assertEqual("pdf", iconos_archivo.clave("informe.pdf"))
        self.assertEqual("pdf", iconos_archivo.clave("INFORME.PDF"))
        self.assertEqual("pdf", iconos_archivo.clave("C:\\descargas\\acta final.pdf"))
        self.assertEqual("xlsx", iconos_archivo.clave("notas.xlsx"))
        self.assertEqual("xlsx", iconos_archivo.clave("notas.xls"))
        self.assertEqual("csv", iconos_archivo.clave("alumnos.csv"))
        self.assertEqual("json", iconos_archivo.clave("config.json"))
        self.assertEqual("docs", iconos_archivo.clave("tesis.docx"))
        self.assertEqual("docs", iconos_archivo.clave("notas.txt"))

    def test_lo_que_no_tiene_icono_se_queda_con_el_generico(self):
        for nombre in ("diapositivas.pptx", "paquete.zip", "video.mp4",
                       "sin-extension", "termina.en.punto.", None, ""):
            self.assertIsNone(iconos_archivo.clave(nombre), nombre)
            self.assertIsNone(iconos_archivo.ruta(nombre), nombre)

    def test_los_cinco_iconos_estan_y_son_png_cuadrados(self):
        for nombre, clave in (("informe.pdf", "pdf"), ("notas.xlsx", "xlsx"),
                              ("alumnos.csv", "csv"), ("config.json", "json"),
                              ("tesis.docx", "docs")):
            ruta = iconos_archivo.ruta(nombre)
            self.assertIsNotNone(ruta, f"falta el icono de {clave}")
            self.assertEqual(f"{clave}.png", ruta.name)
            with Image.open(ruta) as dibujo:
                self.assertEqual("PNG", dibujo.format)
                self.assertEqual(dibujo.width, dibujo.height,
                                 "un icono no cuadrado descuadraría la burbuja")

    def test_la_bandeja_de_adjuntos_pinta_el_icono_del_tipo(self):
        with tempfile.TemporaryDirectory() as temp:
            ruta = Path(temp) / "acta.pdf"
            ruta.write_bytes(b"%PDF-1.4 documento falso")
            bandeja = BandejaAdjuntosChat()
            bandeja.agregar([str(ruta)])

            icono = bandeja._layout.itemAt(0).widget().findChildren(QLabel)[0]

            self.assertFalse(icono.pixmap().isNull(),
                             "el PDF debe enseñar su icono, no el 📎")
            self.assertEqual("", icono.text())
            bandeja.close()

    def test_una_extension_sin_dibujo_sigue_con_el_papel(self):
        with tempfile.TemporaryDirectory() as temp:
            ruta = Path(temp) / "paquete.rar"
            ruta.write_bytes(b"Rar!\x1a\x07\x00 basura")
            bandeja = BandejaAdjuntosChat()
            bandeja.agregar([str(ruta)])

            icono = bandeja._layout.itemAt(0).widget().findChildren(QLabel)[0]

            self.assertEqual("📎", icono.text())
            bandeja.close()

    def _item(self, nombre):
        return {
            "id": "m-1", "sender": "Tú", "date": "2026-10-11T10:00:00",
            "text": f"📎 Enviado archivo: {nombre}  (1.0 KB)",
            "state": "", "raw_state": "", "is_own": True, "is_image": False,
            "es_archivo": True, "filename": nombre, "caption": "",
            "archivo_id": "a-1",
        }

    def test_la_burbuja_antepone_el_icono_del_tipo(self):
        documento = MensajeDelegate()

        con_icono = documento.distribuir(self._item("acta.pdf"), 600,
                                         tokens(MODO_OSCURO))
        html = con_icono["_doc"].toHtml()

        ruta = iconos_archivo.ruta("acta.pdf")
        self.assertIn(ruta.name, html, "el icono debe viajar en el documento")
        self.assertIsNotNone(
            con_icono["_doc"].resource(QTextDocument.ResourceType.ImageResource,
                                       QUrl(ruta.as_uri())),
            "sin el recurso registrado Qt no pinta la imagen")

    def _rastrerizar(self, item):
        """Pinta el documento de la burbuja sobre un lienzo blanco."""
        lay = MensajeDelegate().distribuir(item, 600, tokens(MODO_OSCURO))
        lienzo = QImage(340, 40, QImage.Format.Format_ARGB32)
        lienzo.fill(QColor("white"))
        pintor = QPainter(lienzo)
        lay["_doc"].drawContents(pintor)
        pintor.end()
        return lienzo

    @staticmethod
    def _hay_rojo(lienzo):
        """El icono del PDF es rojo y ninguna otra cosa de la burbuja lo es."""
        for y in range(lienzo.height()):
            for x in range(20):
                color = QColor(lienzo.pixel(x, y))
                if color.red() > 180 and color.green() < 90 and color.blue() < 90:
                    return True
        return False

    def test_la_burbuja_rastreriza_el_icono_de_verdad(self):
        # Tener el recurso registrado no basta: si Qt no pinta el <img>, el
        # mensaje se ve igual que antes y el test de arriba seguiría pasando.
        con_icono = self._rastrerizar(self._item("acta.pdf"))
        sin_icono = self._rastrerizar(self._item("paquete.rar"))

        self.assertTrue(self._hay_rojo(con_icono),
                        "el icono del PDF no se pintó en la burbuja")
        self.assertFalse(self._hay_rojo(sin_icono),
                         "sin dibujo para la extensión no puede haber icono")

    def test_la_burbuja_no_inventa_icono_para_lo_que_no_lo_tiene(self):
        documento = MensajeDelegate()

        sin_icono = documento.distribuir(self._item("paquete.rar"), 600,
                                         tokens(MODO_OSCURO))

        self.assertNotIn("iconos", sin_icono["_doc"].toHtml())
        self.assertIn("paquete.rar", sin_icono["_doc"].toPlainText())


if __name__ == "__main__":
    unittest.main()
