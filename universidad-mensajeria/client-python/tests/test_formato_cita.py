"""`transversal/formato_cita.py`: la misma regla de separación en los 3 clientes."""

import unittest

from transversal.formato_cita import CITA_CORTE, PREFIJO, extracto, separar


class PruebasFormatoCita(unittest.TestCase):
    def test_respuesta_normal_se_separa_en_cita_y_cuerpo(self):
        cita, cuerpo = separar(
            'Respuesta a Bea Dos [B002]: "hola"\n\n¿qué tal?')

        self.assertEqual('Respuesta a Bea Dos [B002]: "hola"', cita)
        self.assertEqual("¿qué tal?", cuerpo)

    def test_sin_prefijo_todo_es_cuerpo(self):
        cita, cuerpo = separar("hola\n\n¿qué tal?")

        self.assertIsNone(cita)
        self.assertEqual("hola\n\n¿qué tal?", cuerpo)

    def test_corte_mas_alla_del_tope_no_es_cita(self):
        # Un mensaje normal que casualmente empieza por esas palabras y trae
        # un salto doble lejos no se parte (paridad con CITA_CORTE de Java).
        texto = PREFIJO + "x" * CITA_CORTE + "\n\ncuerpo"
        cita, cuerpo = separar(texto)

        self.assertIsNone(cita)
        self.assertEqual(texto, cuerpo)

    def test_la_cita_es_lo_anterior_al_primer_salto_doble(self):
        # Un extracto con saltos no parte el cuerpo por la mitad.
        cita, cuerpo = separar(
            'Respuesta a Ana [A]: "una"\n\ndos\n\ntres')

        self.assertEqual('Respuesta a Ana [A]: "una"', cita)
        self.assertEqual("dos\n\ntres", cuerpo)

    def test_vacio_y_cita_sin_cuerpo(self):
        self.assertEqual((None, ""), separar(""))
        self.assertEqual((None, ""), separar(None))
        # Sin salto doble no hay cita: la regla exige el corte (paridad Java).
        solo = 'Respuesta a Bea [B]: "hola"'
        self.assertEqual((None, solo), separar(solo))

    def test_extracto_es_lo_entrecomillado(self):
        self.assertEqual(
            "hola", extracto('Respuesta a Bea Dos [B002]: "hola"'))
        self.assertEqual("", extracto("Respuesta a Bea: [Imagen: f.png]"))
        self.assertEqual("", extracto(None))
        self.assertEqual("", extracto(""))


if __name__ == "__main__":
    unittest.main()
