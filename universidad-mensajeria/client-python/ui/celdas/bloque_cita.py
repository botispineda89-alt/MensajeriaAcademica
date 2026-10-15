"""Bloque citado de la burbuja (UIX-24, PY-2).

Pinta la cita como .NET (`CitaBgBrush`, borde izquierdo de 3 px) y Java
(`message-quote`): fondo + barra lateral + texto. La regla de qué es cita vive
en `transversal/formato_cita.py`; aquí solo el reparto y el pintado Qt, para
que `mensaje_delegate.py` no supere su tope de líneas.
"""

from PySide6.QtCore import Qt
from PySide6.QtGui import QColor, QTextDocument

from transversal.formato_cita import separar as separar_cita
from ui.renderizado_texto import resaltar_html

SANGRIA_TEXTO = 8
ANCHO_BARRA = 3
RADIO = 6


def repartir(lay, texto, x, y, interior, fuente, patron, espaciado, color_texto=None):
    """Guarda el bloque en el layout; devuelve `(cuerpo, nuevo_y)`.

    Sin cita devuelve `(texto, y)` sin tocar nada: el llamante avanza igual
    que con cualquier bloque y no necesita ramas. La regla de qué es cita vive
    en `transversal/formato_cita.py`.
    """
    cita, cuerpo = separar_cita(texto)
    if not cita:
        return texto, y
    doc, alto = crear(cita, interior, fuente, patron, color_texto)
    lay["cita"] = (x, y, interior, alto)
    lay["_cita_doc"] = doc
    lay["_cita"] = cita
    return cuerpo, y + alto + espaciado


def clic(lx, ly, lay, item, navegar):
    """Clic en el bloque → navegar al original. `False` si no era la cita."""
    if "cita" not in lay or navegar is None:
        return False
    x, y, w, h = lay["cita"]
    if x <= lx <= x + w and y <= ly <= y + h:
        navegar(item, lay.get("_cita", ""))
        return True
    return False


def crear(texto_cita, interior, fuente, patron, color_texto=None):
    """Documento del bloque y su alto. El llamante guarda ambos en el layout."""
    doc = QTextDocument()
    doc.setDefaultFont(fuente)
    doc.setHtml(resaltar_html(texto_cita, patron))
    doc.setTextWidth(interior - SANGRIA_TEXTO)
    return doc, int(doc.size().height()) + 10


def pintar(painter, ox, oy, rect, doc, t):
    """Fondo + barra lateral + texto con los colores de los tokens."""
    x, y, w, h = rect
    painter.save()
    painter.setBrush(QColor(t["cita_fondo"]))
    painter.setPen(Qt.PenStyle.NoPen)
    painter.drawRoundedRect(ox + x, oy + y, w, h, RADIO, RADIO)
    painter.setBrush(QColor(t["cita_borde"]))
    painter.drawRoundedRect(ox + x, oy + y, ANCHO_BARRA, h, 1, 1)
    painter.restore()
    painter.save()
    painter.translate(ox + x + SANGRIA_TEXTO, oy + y + 5)
    from PySide6.QtGui import QAbstractTextDocumentLayout, QPalette
    ctx = QAbstractTextDocumentLayout.PaintContext()
    ctx.palette.setColor(QPalette.ColorRole.Text, QColor(t["cita_texto"]))
    doc.documentLayout().draw(painter, ctx)
    painter.restore()


def pintar_si_hay(painter, ox, oy, lay, t):
    """Pinta el bloque si el layout trae cita; si no, no hace nada."""
    if "cita" in lay:
        pintar(painter, ox, oy, lay["cita"], lay["_cita_doc"], t)
