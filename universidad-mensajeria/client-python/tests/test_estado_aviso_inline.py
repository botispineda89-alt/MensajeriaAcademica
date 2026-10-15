import os
import unittest

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

from PySide6.QtTest import QTest
from PySide6.QtWidgets import QApplication

from ui.estado_aviso_inline import (
    DURACION_ERROR_MS,
    DURACION_EXITO_MS,
    EstadoAvisoInline,
    TipoAviso,
    anuncio_de,
)


class PruebasEstadoAvisoInline(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.app = QApplication.instance() or QApplication([])

    def test_aviso_se_cierra_automaticamente_y_tiene_boton_accesible(self):
        aviso = EstadoAvisoInline()
        aviso.mostrar("No se pudo enviar", duracion_ms=100)
        self.assertTrue(aviso.isVisible())
        self.assertEqual("Cerrar aviso", aviso.boton_cerrar.accessibleName())

        for _ in range(30):
            if not aviso.isVisible():
                break
            QTest.qWait(50)

        self.assertFalse(aviso.isVisible())
        aviso.close()

    def test_cada_severidad_muestra_su_titulo(self):
        aviso = EstadoAvisoInline()
        esperados = {
            TipoAviso.ERROR: "Error",
            TipoAviso.ADVERTENCIA: "Atención",
            TipoAviso.INFORMACION: "Aviso",
            TipoAviso.EXITO: "Listo",
        }
        for tipo, titulo in esperados.items():
            aviso.mostrar("mensaje", tipo)
            self.assertEqual(titulo, aviso.etiqueta_titulo.text(), f"título de {tipo}")
            self.assertIs(tipo, aviso.tipo)
        aviso.close()

    def test_cada_severidad_se_pinta_con_su_familia_de_tokens(self):
        """T2 (2026-10-14): el fondo ya no es el de la tarjeta en todas."""
        from ui.tema import TOKENS_CANONICOS, MODO_OSCURO

        aviso = EstadoAvisoInline()
        t = TOKENS_CANONICOS[MODO_OSCURO]
        for tipo, familia in (
                (TipoAviso.ERROR, "error"),
                (TipoAviso.ADVERTENCIA, "banner_offline"),
                (TipoAviso.EXITO, "aviso_exito"),
                (TipoAviso.INFORMACION, "aviso_info")):
            with self.subTest(severidad=tipo):
                aviso.mostrar("mensaje", tipo)
                hoja = aviso.styleSheet()
                self.assertIn(t[f"{familia}_bg"], hoja)
                self.assertIn(t[f"{familia}_border"], hoja)
                self.assertIn(t[f"{familia}_fg"], aviso.etiqueta.styleSheet())
        aviso.close()

    def test_el_ambar_de_la_advertencia_es_el_del_banner_de_sin_conexion(self):
        """El ámbar unificado de los 3 clientes sale del mismo token."""
        from ui.estado_aviso_inline import FAMILIA_TOKENS

        self.assertEqual("banner_offline", FAMILIA_TOKENS[TipoAviso.ADVERTENCIA])

    def test_el_plazo_depende_de_la_severidad(self):
        aviso = EstadoAvisoInline()
        for tipo, plazo in ((TipoAviso.EXITO, DURACION_EXITO_MS),
                            (TipoAviso.ERROR, DURACION_ERROR_MS)):
            aviso.mostrar("mensaje", tipo)
            # El temporizador queda armado con el plazo del tipo, no con uno fijo
            # de 5000 ms como antes.
            self.assertEqual(plazo, aviso._temporizador.interval())
        aviso.close()

    def test_el_aviso_se_anuncia_con_titulo_y_mensaje(self):
        aviso = EstadoAvisoInline()
        aviso.mostrar("Conexión recuperada.", TipoAviso.EXITO)
        QTest.qWait(10)
        self.assertEqual("Listo: Conexión recuperada.",
                         aviso.accessibleDescription())
        aviso.close()

    def test_el_mismo_aviso_dos_veces_vuelve_anunciarse(self):
        aviso = EstadoAvisoInline()
        aviso.mostrar("No se pudo enviar.", TipoAviso.ERROR)
        QTest.qWait(10)
        self.assertEqual("Error: No se pudo enviar.", aviso.accessibleDescription())

        # Un lector de pantalla solo repite si el texto cambia: el mismo error
        # saltando dos veces (un reintento que vuelve a fallar) debe re-leerse.
        aviso.mostrar("No se pudo enviar.", TipoAviso.ERROR)
        self.assertEqual("", aviso.accessibleDescription(), "se limpia antes de repetir")
        QTest.qWait(10)
        self.assertEqual("Error: No se pudo enviar.", aviso.accessibleDescription())
        aviso.close()

    def test_un_texto_vacio_no_abre_el_aviso(self):
        aviso = EstadoAvisoInline()
        aviso.mostrar("   ")
        self.assertFalse(aviso.isVisible())
        aviso.close()

    def test_el_anuncio_es_igual_en_los_tres_clientes(self):
        self.assertEqual("Atención: Sin red.", anuncio_de("Sin red.", TipoAviso.ADVERTENCIA))
        self.assertEqual("Error: No se pudo enviar.",
                         anuncio_de("No se pudo enviar.", TipoAviso.ERROR))


if __name__ == "__main__":
    unittest.main()