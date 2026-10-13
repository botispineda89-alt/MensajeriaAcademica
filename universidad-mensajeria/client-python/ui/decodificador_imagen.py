"""Decodifica imágenes que Qt no trae de serie (webp, avif, heic/heif, svg).

Patrón igual al de Java (`TranscodificadorImagen`) y .NET: el archivo ORIGINAL
viaja y se guarda tal cual; la conversión a PNG es SOLO para pintarlo. Qt
decodifica PNG/JPEG/GIF/BMP y (desde Qt 6) WebP; lo demás va por aquí:

- AVIF/HEIC/HEIF: plugins de Pillow (`pillow-avif-plugin`, `pillow-heif`).
  Se registran una sola vez al importar; si un paquete falta, ese formato
  simplemente no se decodifica (el llamador muestra su aviso de siempre).
- SVG: `QSvgRenderer` (viene con PySide6, sin dependencias nuevas), rasterizado
  con la transparencia intacta y limitado a un lado máximo razonable.
"""

import io
from pathlib import Path

from PIL import Image
from PySide6.QtCore import QBuffer, QRectF, Qt
from PySide6.QtGui import QImage, QPainter
from PySide6.QtSvg import QSvgRenderer

try:
    import pillow_avif  # noqa: F401  — registra AVIF en PIL al importarse
    _PILLOW_AVIF = True
except ImportError:
    _PILLOW_AVIF = False
try:
    import pillow_heif

    pillow_heif.register_heif_opener()
    _PILLOW_HEIF = True
except ImportError:
    _PILLOW_HEIF = False

#: Formatos que PIL sabe abrir una vez registrados los plugins de arriba.
FORMATOS_PIL = {"WEBP", "AVIF", "AVIFS", "HEIF", "HEIC", "HEIX", "MIF1", "MSF1"}

#: Lado máximo al que se rasteriza un SVG (un SVG sin tamaño acotado puede
#: declarar unidades enormes; rasterizarlo a 4096 ya es más que la pantalla).
LADO_MAXIMO_SVG = 4096


def soporta_avif_heif():
    """True si algún plugin de Pillow abre AVIF/HEIC en esta instalación."""
    return _PILLOW_AVIF or _PILLOW_HEIF


def a_png_bytes(ruta):
    """Bytes PNG de una imagen que Qt no decodificó (webp/avif/heic/svg).

    Levanta `RuntimeError` si ninguna vía puede abrir el archivo; el
    llamador traduce eso a su mensaje de usuario.
    """
    sufijo = Path(ruta).suffix.casefold()
    if sufijo == ".svg":
        return _svg_a_png_bytes(ruta)
    with Image.open(ruta) as imagen:
        if imagen.format not in FORMATOS_PIL:
            # Un documento o un binario cualquiera: no es una imagen.
            raise RuntimeError(f"formato no soportado: {imagen.format or '?'}")
        buffer = io.BytesIO()
        imagen.convert("RGBA").save(buffer, format="PNG")
        return buffer.getvalue()


def a_qimage(ruta):
    """Carga una imagen por ruta: primero Qt; si no puede, los plugins."""
    imagen = QImage(str(ruta))
    if not imagen.isNull():
        return imagen
    return QImage.fromData(a_png_bytes(ruta), "PNG")


def _svg_a_png_bytes(ruta):
    renderer = QSvgRenderer(str(ruta))
    if not renderer.isValid():
        raise RuntimeError("El archivo no es un SVG válido")
    por_defecto = renderer.defaultSize()
    ancho = max(1, por_defecto.width() or 300)
    alto = max(1, por_defecto.height() or 300)
    escala = min(1.0, LADO_MAXIMO_SVG / max(ancho, alto))
    lienzo = QImage(max(1, int(ancho * escala)), max(1, int(alto * escala)),
                    QImage.Format.Format_ARGB32)
    lienzo.fill(Qt.GlobalColor.transparent)
    pintor = QPainter(lienzo)
    try:
        renderer.render(pintor, QRectF(0, 0, lienzo.width(), lienzo.height()))
    finally:
        pintor.end()
    buffer = QBuffer()
    buffer.open(QBuffer.OpenModeFlag.WriteOnly)
    lienzo.save(buffer, "PNG")
    return bytes(buffer.data())
