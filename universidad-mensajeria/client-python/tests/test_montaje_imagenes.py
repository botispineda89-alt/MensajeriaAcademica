import tempfile
import unittest
from pathlib import Path

from PIL import Image

from ui.montaje_imagenes import componer


class PruebasMontajeImagenes(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directorio = Path(self.temp.name)

    def _imagen(self, nombre, color):
        ruta = self.directorio / nombre
        Image.new("RGB", (30, 20), color).save(ruta, format="PNG")
        return str(ruta)

    def test_compone_dos_imagenes_en_png_en_un_archivo_persistente(self):
        rutas = [self._imagen("uno.png", "red"), self._imagen("dos.png", "blue")]
        destino = self.directorio / "cache"

        ruta_montaje = Path(componer(rutas, destino))

        self.assertTrue(ruta_montaje.is_file())
        with Image.open(ruta_montaje) as montaje:
            self.assertEqual("PNG", montaje.format)
            self.assertEqual((964, 480), montaje.size)
            self.assertEqual((255, 0, 0), montaje.getpixel((240, 240))[:3])
            self.assertEqual((0, 0, 255), montaje.getpixel((724, 240))[:3])

    def test_rechaza_menos_de_dos_o_mas_de_doce_imagenes(self):
        ruta = self._imagen("una.png", "green")
        with self.assertRaisesRegex(ValueError, "de 2 a 12"):
            componer([ruta], self.directorio)
        with self.assertRaisesRegex(ValueError, "de 2 a 12"):
            componer([ruta] * 13, self.directorio)


if __name__ == "__main__":
    unittest.main()
