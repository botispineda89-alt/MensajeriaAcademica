import base64
import tempfile
import unittest
from pathlib import Path

from PIL import Image
from PySide6.QtCore import QBuffer, QIODevice
from PySide6.QtGui import QImage

from ui.cargador_adjunto_entrada_chat import CargadorAdjuntoEntradaChat


def _png_bytes():
    imagen = QImage(3, 2, QImage.Format.Format_ARGB32)
    buffer = QBuffer()
    buffer.open(QIODevice.OpenModeFlag.WriteOnly)
    imagen.save(buffer, "PNG")
    return bytes(buffer.data())


class PruebasCargadorAdjuntoEntradaChat(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.cargador = CargadorAdjuntoEntradaChat()

    def test_carga_contenido_base64_a_caché(self):
        mensaje = {
            "id": "imagen-1", "filename": "foto.png",
            "image_base64": base64.b64encode(_png_bytes()).decode("ascii"),
        }

        id_cargado, imagen, ruta = self.cargador.cargar(
            mensaje, self.temp.name, lambda *_: self.fail("no debe ir a red"))

        self.assertEqual("imagen-1", id_cargado)
        self.assertTrue(Path(ruta).is_file())
        self.assertFalse(imagen.isNull())

    def test_reutiliza_ruta_local_existente(self):
        ruta = Path(self.temp.name) / "existente.png"
        ruta.write_bytes(_png_bytes())
        mensaje = {"id": "imagen-2", "path": str(ruta)}

        id_cargado, imagen, ruta_cargada = self.cargador.cargar(
            mensaje, self.temp.name, lambda *_: self.fail("no debe descargar"))

        self.assertEqual(("imagen-2", str(ruta)), (id_cargado, ruta_cargada))
        self.assertFalse(imagen.isNull())

    def test_descarga_si_no_hay_copia_local_ni_base64(self):
        llamadas = []

        def obtener_imagen(id_mensaje, destino):
            llamadas.append(id_mensaje)
            Path(destino).write_bytes(_png_bytes())

        id_cargado, imagen, ruta = self.cargador.cargar(
            {"id": "imagen-3", "filename": "foto.png"}, self.temp.name,
            obtener_imagen)

        self.assertEqual(["imagen-3"], llamadas)
        self.assertEqual("imagen-3", id_cargado)
        self.assertTrue(Path(ruta).is_file())
        self.assertFalse(imagen.isNull())

    def test_webp_se_decodifica_para_vista_sin_alterar_el_original(self):
        ruta = Path(self.temp.name) / "foto.webp"
        Image.new("RGBA", (6, 4), (40, 80, 120, 160)).save(
            ruta, format="WEBP")
        original = ruta.read_bytes()

        _, imagen, ruta_cargada = self.cargador.cargar(
            {"id": "webp-1", "filename": "foto.webp", "path": str(ruta)},
            self.temp.name, lambda *_: self.fail("no debe descargar"))

        self.assertEqual(str(ruta), ruta_cargada)
        self.assertEqual(original, ruta.read_bytes())
        self.assertEqual((6, 4), (imagen.width(), imagen.height()))

    def test_avif_heic_y_svg_se_decodifican_para_vista_sin_alterar_el_original(self):
        # El servidor los filtra como imagen (Imagenes.esImagenValida) y los
        # entrega con los bytes originales: el cliente debe poder pintarlos.
        casos = [
            ("foto.avif", lambda r: Image.new("RGB", (8, 5), (10, 200, 30)).save(r, format="AVIF")),
            ("foto.heic", lambda r: Image.new("RGB", (8, 5), (10, 200, 30)).save(r, format="HEIF")),
            ("figura.svg", lambda r: r.write_text(
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"9\" height=\"6\">"
                "<rect width=\"9\" height=\"6\" fill=\"#204060\"/></svg>",
                encoding="utf-8")),
        ]
        for indice, (nombre, crear) in enumerate(casos):
            with self.subTest(formato=nombre):
                ruta = Path(self.temp.name) / nombre
                crear(ruta)
                original = ruta.read_bytes()

                _, imagen, ruta_cargada = self.cargador.cargar(
                    {"id": f"formato-{indice}", "filename": nombre,
                     "path": str(ruta)},
                    self.temp.name, lambda *_: self.fail("no debe descargar"))

                self.assertEqual(str(ruta), ruta_cargada)
                self.assertEqual(original, ruta.read_bytes(),
                                 "el original se conserva tal cual")
                self.assertFalse(imagen.isNull())

    def test_rechaza_contenido_que_no_es_imagen(self):
        with self.assertRaisesRegex(RuntimeError, "no es una imagen válida"):
            self.cargador.cargar(
                {"id": "imagen-mala", "image_base64": "bm90IGFuIGltYWdl"},
                self.temp.name, lambda *_: None)

    def test_cache_de_cero_bytes_se_repara_descargando_de_nuevo(self):
        # §7 del plan de paridad: el smoke dejó un .webp de 0 bytes en caché y
        # la burbuja quedaba en "Reintentar imagen" para siempre, porque el
        # reintento volvía a leer el veneno. Ahora la caché vacía/rota se
        # re-descarga sobre la misma ruta.
        llamadas = []
        veneno = Path(self.temp.name) / "rota.webp"
        veneno.write_bytes(b"")

        def obtener_imagen(id_mensaje, destino):
            llamadas.append(id_mensaje)
            Image.new("RGB", (6, 4), (1, 2, 3)).save(destino, format="WEBP")

        _, imagen, ruta_cargada = self.cargador.cargar(
            {"id": "veneno-1", "filename": "rota.webp", "path": str(veneno)},
            self.temp.name, obtener_imagen)

        self.assertEqual(["veneno-1"], llamadas, "se re-descargó una vez")
        self.assertFalse(imagen.isNull())
        self.assertTrue(Path(ruta_cargada).stat().st_size > 0)

    def test_cache_corrupta_se_repara_con_el_base64_recibido(self):
        rota = Path(self.temp.name) / "rota.png"
        rota.write_bytes(b"basura que no es imagen")

        _, imagen, _ = self.cargador.cargar(
            {"id": "rota-1", "filename": "rota.png",
             "image_base64": base64.b64encode(_png_bytes()).decode("ascii"),
             "path": str(rota)},
            self.temp.name, lambda *_: self.fail("no debe ir a red"))

        self.assertFalse(imagen.isNull())


if __name__ == "__main__":
    unittest.main()
