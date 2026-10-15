"""§5 paridad / P2: el botón de responder es visible en la burbuja (Python).

Antes Python solo respondía con doble clic: el botón existía en Java (`↩`) y en
.NET (`Responder`), pero en Python no había nada que pulsar. Aquí se fija lo
mínimo que lo hace visible y usable: el layout reserva su rectángulo y el clic
por coordenadas llama al mismo callback que usa el doble clic.
"""
import os
import unittest

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

from PySide6.QtCore import QEvent, QPoint, QRect
from PySide6.QtWidgets import QApplication

from transversal.textos import BOTON_RESPONDER
from ui.lista_virtual import ROL_ITEM, MensajeDelegate, MensajeListModel
from ui.tema import MODO_OSCURO, tokens


def _mensaje(**extras):
    base = {"id": "m1", "sender": "B002", "date": "2026-10-02 10:00:00",
            "text": "hola", "is_own": False, "is_image": False,
            "mostrar_remitente": True}
    base.update(extras)
    return base


class _SueltaDeRaton:
    """Doble mínimo del evento de Qt: el delegado solo mira tipo y posición."""

    def __init__(self, x, y):
        self._pos = QPoint(x, y)

    def type(self):
        return QEvent.Type.MouseButtonRelease

    def pos(self):
        return self._pos


class _Opcion:
    """Doble mínimo del `QStyleOptionViewItem`: el delegado lee `rect`."""

    def __init__(self, ancho=600):
        self.rect = QRect(0, 0, ancho, 400)


class PruebasResponderEnBurbuja(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.app = QApplication.instance() or QApplication([])

    def setUp(self):
        self.modelo = MensajeListModel()
        self.delegado = MensajeDelegate()
        self.respondidos = []
        self.delegado.responder = self.respondidos.append

    def test_el_layout_reserva_el_boton_en_cualquier_mensaje(self):
        lay = self.delegado.distribuir(_mensaje(), 600, tokens(MODO_OSCURO))
        self.assertIn("responder", lay)

    def test_el_boton_convive_con_la_fila_de_acciones_del_archivo(self):
        lay = self.delegado.distribuir(
            _mensaje(es_archivo=True, archivo_id="7"), 600, tokens(MODO_OSCURO))
        self.assertIn("descarga", lay)
        x_desc, y_desc, w_desc, _ = lay["descarga"]
        x_res, y_res, w_res, _ = lay["responder"]
        # Misma fila y sin solaparse, incluso con la burbuja en su ancho mínimo.
        self.assertEqual(y_desc, y_res)
        self.assertGreaterEqual(x_res, x_desc + w_desc)
        ancho_min = self.delegado.distribuir(
            _mensaje(es_archivo=True, archivo_id="7"), 280, tokens(MODO_OSCURO))
        x_burbuja, _y, tope, _h = ancho_min["burbuja"]
        x_res_min = ancho_min["responder"][0]
        self.assertLessEqual(x_res_min + w_res, x_burbuja + tope)

    def test_una_fila_sin_id_no_lleva_boton(self):
        """Sin identidad, responder no llevaría a ninguna parte."""
        lay = self.delegado.distribuir(_mensaje(id=None), 600, tokens(MODO_OSCURO))
        self.assertNotIn("responder", lay)

    def test_el_clic_en_el_boton_responde_el_mensaje(self):
        self.modelo.agregar(_mensaje())
        indice = self.modelo.index(0)
        item = self.modelo.data(indice, ROL_ITEM)
        lay = self.delegado.distribuir(item, 600, tokens(MODO_OSCURO))
        x, y, w, h = lay["responder"]

        consumido = self.delegado.editorEvent(
            _SueltaDeRaton(x + w // 2, y + h // 2), self.modelo,
            _Opcion(), indice)

        self.assertTrue(consumido)
        self.assertEqual(["m1"], [m["id"] for m in self.respondidos])

    def test_el_texto_del_boton_sale_del_catalogo(self):
        self.assertEqual("Responder", BOTON_RESPONDER)


if __name__ == "__main__":
    unittest.main()
