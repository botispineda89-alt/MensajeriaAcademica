import tempfile
import unittest
from pathlib import Path

from ui.plan_envio_adjuntos import (
    Adjunto, MAXIMO_ADJUNTOS, PlanEnvioAdjuntos, Texto,
)


class PruebasPlanEnvioAdjuntos(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directorio = Path(self.temp.name)
        self.imagenes = []
        for indice in range(MAXIMO_ADJUNTOS):
            ruta = self.directorio / f"{indice}.png"
            ruta.write_bytes(b"imagen")
            self.imagenes.append(str(ruta))

    def test_solo_texto_no_crea_acciones_de_adjunto(self):
        plan = PlanEnvioAdjuntos.calcular(" hola ", [], {".png"})
        self.assertEqual(1, len(plan))
        self.assertIsInstance(plan[0], Texto)
        self.assertEqual("hola", plan[0].cuerpo)

    def test_dos_imagenes_y_caption_crean_dos_adjuntos_originales(self):
        plan = PlanEnvioAdjuntos.calcular(
            "caption", self.imagenes[:2], {".png"})
        self.assertEqual(2, len(plan))
        self.assertTrue(all(isinstance(accion, Adjunto) for accion in plan))
        self.assertEqual("caption", plan[0].caption)
        self.assertIsNone(plan[1].caption)
        self.assertEqual(
            [str(Path(ruta).resolve()) for ruta in self.imagenes[:2]],
            [accion.ruta for accion in plan])

    def test_lote_mixto_envia_texto_separado_y_no_duplica_caption(self):
        documento = self.directorio / "manual.pdf"
        documento.write_bytes(b"pdf")
        plan = PlanEnvioAdjuntos.calcular(
            "texto", [self.imagenes[0], str(documento)], {".png"})
        self.assertEqual(3, len(plan))
        self.assertIsInstance(plan[0], Texto)
        self.assertIsInstance(plan[1], Adjunto)
        self.assertIsInstance(plan[2], Adjunto)
        self.assertTrue(all(getattr(accion, "caption", None) is None
                            for accion in plan[1:]))

    def test_limita_el_lote_sin_agrupar_imagenes_para_el_envio(self):
        plan = PlanEnvioAdjuntos.calcular(
            "", self.imagenes, {".png"})
        self.assertEqual(MAXIMO_ADJUNTOS, len(plan))
        self.assertTrue(all(isinstance(accion, Adjunto) for accion in plan))
        extra = self.directorio / "extra.png"
        extra.write_bytes(b"imagen")
        with self.assertRaisesRegex(ValueError, "Máximo"):
            PlanEnvioAdjuntos.calcular(
                "", self.imagenes + [str(extra)], {".png"})


if __name__ == "__main__":
    unittest.main()
