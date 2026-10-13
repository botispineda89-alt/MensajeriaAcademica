import tempfile
import unittest
from pathlib import Path
from PIL import Image

from ui.enviar_mensaje_comando import EnviarMensajeComando
from ui.plan_envio_adjuntos import Adjunto, PlanEnvioAdjuntos, Texto


class PuertoEnvio:
    def __init__(self, resultado=True):
        self.resultado = resultado
        self.llamadas = []

    def enviar_texto(self, usuario, texto, id_msg=None):
        self.llamadas.append(("texto", usuario, texto, id_msg, None))
        return self.resultado

    def enviar_imagen(self, usuario, ruta, id_msg=None, caption=None):
        self.llamadas.append(("imagen", usuario, ruta, id_msg, caption))
        return self.resultado

    def enviar_archivo(self, usuario, ruta, id_msg=None, on_progreso=None,
                       caption=None):
        self.llamadas.append(("archivo", usuario, ruta, id_msg, caption))
        return self.resultado


class PruebasEnviarMensajeComando(unittest.TestCase):
    EXTENSIONES_IMAGEN = frozenset({".png", ".jpg", ".webp"})

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.directorio = Path(self.temp.name)

    def _imagen(self, nombre, color="red", formato="PNG"):
        ruta = self.directorio / nombre
        Image.new("RGB", (24, 18), color).save(ruta, format=formato)
        return str(ruta)

    def _comando(self, plan):
        return EnviarMensajeComando(
            "B002", plan, self.EXTENSIONES_IMAGEN)

    def test_texto_y_un_adjunto_convierten_el_texto_en_caption(self):
        ruta = self._imagen("foto.png")
        plan = PlanEnvioAdjuntos.calcular(
            "hola", [ruta], self.EXTENSIONES_IMAGEN)
        self.assertEqual((Adjunto(plan[0].id_mensaje, str(Path(ruta).resolve()), "hola"),),
                         plan)

        puerto = PuertoEnvio()
        resultado = self._comando(plan).ejecutar(puerto)

        self.assertEqual("B002", resultado[0])
        self.assertTrue(resultado[1])
        self.assertEqual([plan[0].id_mensaje], resultado[2])
        self.assertEqual([], resultado[3])
        self.assertTrue(resultado[4][plan[0].id_mensaje])
        self.assertEqual("imagen", puerto.llamadas[0][0])
        self.assertEqual("hola", puerto.llamadas[0][4])

    def test_dos_imagenes_se_envian_individualmente_y_en_orden(self):
        rutas = [self._imagen("a.png", "red"), self._imagen("b.png", "blue")]
        plan = PlanEnvioAdjuntos.calcular(
            "pie", rutas, self.EXTENSIONES_IMAGEN)
        self.assertEqual(2, len(plan))
        self.assertTrue(all(isinstance(accion, Adjunto) for accion in plan))
        self.assertEqual("pie", plan[0].caption)
        self.assertIsNone(plan[1].caption)
        puerto = PuertoEnvio()

        resultado = self._comando(plan).ejecutar(puerto)

        self.assertTrue(resultado[1])
        self.assertEqual([accion.id_mensaje for accion in plan], resultado[2])
        self.assertEqual(["imagen", "imagen"], [llamada[0] for llamada in puerto.llamadas])
        self.assertEqual([str(Path(ruta).resolve()) for ruta in rutas],
                         [llamada[2] for llamada in puerto.llamadas])
        self.assertEqual(["pie", None], [llamada[4] for llamada in puerto.llamadas])
        self.assertFalse(any(Path(llamada[2]).name.startswith("montaje_")
                             for llamada in puerto.llamadas))

    def test_montaje_no_es_requisito_para_enviar_originales(self):
        rutas = [self._imagen("a.png"), self._imagen("b.png")]
        plan = PlanEnvioAdjuntos.calcular("", rutas, self.EXTENSIONES_IMAGEN)
        puerto = PuertoEnvio()
        resultado = self._comando(plan).ejecutar(puerto)

        self.assertEqual(2, len(puerto.llamadas))
        self.assertTrue(all(llamada[0] == "imagen" for llamada in puerto.llamadas))
        self.assertEqual([str(Path(ruta).resolve()) for ruta in rutas],
                         [llamada[2] for llamada in puerto.llamadas])
        self.assertIsNone(puerto.llamadas[0][4])
        self.assertIsNone(puerto.llamadas[1][4])
        self.assertTrue(resultado[1])
        self.assertEqual([], resultado[3])

    def test_webp_se_envia_como_imagen_original(self):
        ruta = self._imagen("foto.webp", formato="WEBP")
        plan = PlanEnvioAdjuntos.calcular("", [ruta], self.EXTENSIONES_IMAGEN)
        puerto = PuertoEnvio()

        self._comando(plan).ejecutar(puerto)

        self.assertEqual("imagen", puerto.llamadas[0][0])
        self.assertEqual(str(Path(ruta).resolve()), puerto.llamadas[0][2])
        with Image.open(puerto.llamadas[0][2]) as enviada:
            self.assertEqual("WEBP", enviada.format)

    def test_adjuntos_multiples_y_texto_suelto_se_envian_en_orden(self):
        rutas = [self._imagen("foto.png"), str(self.directorio / "manual.pdf")]
        Path(rutas[1]).write_bytes(b"%PDF")
        plan = PlanEnvioAdjuntos.calcular(
            "texto", rutas, self.EXTENSIONES_IMAGEN)
        self.assertIsInstance(plan[0], Texto)
        self.assertIsInstance(plan[1], Adjunto)
        self.assertIsNone(plan[1].caption)
        puerto = PuertoEnvio()

        self._comando(plan).ejecutar(puerto)

        self.assertEqual(["texto", "imagen", "archivo"],
                         [llamada[0] for llamada in puerto.llamadas])

    def test_envio_sin_red_devuelve_aviso_inline(self):
        ruta = self._imagen("offline.png")
        plan = PlanEnvioAdjuntos.calcular("", [ruta], self.EXTENSIONES_IMAGEN)
        resultado = self._comando(plan).ejecutar(PuertoEnvio(resultado=False))

        self.assertFalse(resultado[1])
        self.assertIn("quedó en cola", resultado[3][0])

    def test_error_de_adjunto_no_se_marca_como_pendiente_en_cola(self):
        ruta = self._imagen("fallo.png")
        plan = PlanEnvioAdjuntos.calcular("", [ruta], self.EXTENSIONES_IMAGEN)

        class PuertoFallido(PuertoEnvio):
            def enviar_imagen(self, *_args, **_kwargs):
                raise ValueError("formato inválido")

        resultado = self._comando(plan).ejecutar(PuertoFallido())

        self.assertFalse(resultado[1])
        self.assertIsNone(resultado[4][plan[0].id_mensaje])
        self.assertIn("formato inválido", resultado[3][0])


if __name__ == "__main__":
    unittest.main()
