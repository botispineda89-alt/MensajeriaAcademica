"""Confirmaciones y cierre explícito de sesiones desde la UI."""

from PySide6.QtWidgets import QMessageBox

from transversal.textos import (
    CERRAR_SESION_PREGUNTA,
    CONFIRMAR_SALIDA,
    SALIR_TODOS_PREGUNTA,
)


class GestorSalidaSesionChat:
    def expulsar_otras(self):
        self._ejecutar("kick_otras", self.fachada.expulsar_otras_sesiones)

    def salir_en_todos(self):
        respuesta = QMessageBox.question(
            self, CONFIRMAR_SALIDA, SALIR_TODOS_PREGUNTA,
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
            QMessageBox.StandardButton.No)
        if respuesta != QMessageBox.StandardButton.Yes:
            return
        self._ejecutar("salir_todos", self.fachada.expulsar_otras_sesiones)

    def closeEvent(self, evento):
        if self._cerrando:
            evento.accept()
            return
        respuesta = QMessageBox.question(
            self, CONFIRMAR_SALIDA, CERRAR_SESION_PREGUNTA,
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
            QMessageBox.StandardButton.No)
        if respuesta != QMessageBox.StandardButton.Yes:
            evento.ignore()
            return
        self._cerrando = True
        self.fachada.on_mensaje_recibido_ui = None
        self.fachada.on_cierre_ui = None
        self.fachada.on_presencia_ui = None
        self.fachada.on_desconexion_ui = None
        self.fachada.desconectar()
        self.on_logout()
        evento.accept()
