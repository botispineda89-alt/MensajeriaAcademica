import unittest

from PySide6.QtCore import Qt

from ui.lista_virtual import MensajeListModel


class PruebasAccesibilidadListaMensajes(unittest.TestCase):
    def test_exhibe_cuerpo_caption_y_estado_al_lector_de_pantalla(self):
        modelo = MensajeListModel()
        modelo.agregar({
            "id": "mensaje-1", "sender": "Ana", "is_image": True,
            "filename": "foto.png", "caption": "En el campus",
            "date": "2026-01-01T10:00:00", "state": "✓✓",
        })
        indice = modelo.index(0)

        texto = modelo.data(indice, Qt.ItemDataRole.AccessibleTextRole)
        descripcion = modelo.data(
            indice, Qt.ItemDataRole.AccessibleDescriptionRole)

        self.assertIn("Ana", texto)
        self.assertIn("foto.png", texto)
        self.assertIn("En el campus", texto)
        self.assertIn("✓✓", texto)
        self.assertIn("Guardar imagen", descripcion)


if __name__ == "__main__":
    unittest.main()
