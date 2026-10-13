import os
import unittest

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

from PySide6.QtTest import QTest
from PySide6.QtWidgets import QApplication

from ui.estado_aviso_inline import EstadoAvisoInline


class PruebasEstadoAvisoInline(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.app = QApplication.instance() or QApplication([])

    def test_aviso_se_cierra_automaticamente_y_tiene_boton_accesible(self):
        aviso = EstadoAvisoInline()
        aviso.mostrar("No se pudo enviar", duracion_ms=1000)
        self.assertTrue(aviso.isVisible())
        self.assertEqual("Cerrar aviso", aviso.boton_cerrar.accessibleName())

        QTest.qWait(1100)

        self.assertFalse(aviso.isVisible())
        aviso.close()


if __name__ == "__main__":
    unittest.main()
