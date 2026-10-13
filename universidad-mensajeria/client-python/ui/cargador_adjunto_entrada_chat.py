"""Carga imágenes entrantes desde caché, contenido recibido o transporte."""

import base64
from pathlib import Path

from PySide6.QtGui import QImage

from ui.decodificador_imagen import a_png_bytes


class CargadorAdjuntoEntradaChat:
    def cargar(self, mensaje, directorio, obtener_imagen):
        carpeta = Path(directorio)
        carpeta.mkdir(parents=True, exist_ok=True)
        extension = Path(mensaje.get("filename") or "").suffix or ".img"
        ruta = mensaje.get("path")

        if ruta and Path(ruta).is_file() and Path(ruta).stat().st_size > 0:
            imagen = self._decodificar(ruta)
            if not imagen.isNull():
                return mensaje.get("id"), imagen, ruta
            # La copia local existe pero no se puede abrir: la tratamos como
            # ausente y caemos a la re-descarga de abajo.

        # Sin copia utilizable: primera vista o CACHÉ ENVENENADA (un archivo
        # de 0 bytes o truncado que quedó de una descarga cortada — pasaba con
        # un .webp del smoke). Se re-descarga SIEMPRE sobre la ruta de caché.
        ruta = str(carpeta / ((mensaje.get("id") or "imagen") + extension))
        imagen_b64 = mensaje.get("image_base64")
        if imagen_b64:
            Path(ruta).write_bytes(base64.b64decode(imagen_b64))
        else:
            obtener_imagen(mensaje["id"], ruta)

        imagen = self._decodificar(ruta)
        if imagen.isNull():
            raise RuntimeError("El archivo descargado no es una imagen válida")
        return mensaje.get("id"), imagen, ruta

    @staticmethod
    def _decodificar(ruta):
        """QImage primero; los formatos que Qt no abre, vía el decodificador."""
        imagen = QImage(ruta)
        if imagen.isNull():
            try:
                datos_png = a_png_bytes(ruta)
            except Exception as error:
                return QImage()
            imagen = QImage.fromData(datos_png, "PNG")
        return imagen
