"""Fila de acciones de la burbuja: reintentar, guardar y responder.

Los tres botones se reparten, se pintan y se resuelven por coordenadas igual, así
que viven juntos y el delegado solo llama. La extracción no es cosmética:
`mensaje_delegate.py` tiene tope de 350 líneas (`test_arquitectura.py`) y el botón
de responder (P2, 2026-10-14) lo pasaba.

Contrato visual: el botón de responder va **a la derecha** de la fila —como el
`Responder` de .NET y el `↩` de Java— y, si la fila no existe, estrena la suya.
"""

from PySide6.QtCore import Qt
from PySide6.QtGui import QColor

from transversal.textos import BOTON_RESPONDER

#: Ancho de los botones de la fila, alto de esa fila y sangría dentro de la burbuja
#: (`BUBBLE_PAD_H` de `mensaje_delegate`).
ANCHO_BOTON = 150
ANCHO_RESPONDER = 90
ALTO_BOTON = 25
SANGRIA = 12


def repartir(lay, item, x_burbuja, tope, y, espaciado):
    """Reparte los tres botones en el layout y devuelve el `y` siguiente.

    Guarda `retry` (la imagen falló), `descarga` (hay archivo o imagen) y
    `responder` (siempre que el mensaje tenga identidad: responder a una fila sin
    id no llevaría a ninguna parte). `descarga` y `retry` comparten fila con
    `responder` cuando existen.
    """
    if item.get("image_error"):
        lay["retry"] = (x_burbuja + SANGRIA, y, ANCHO_BOTON, ALTO_BOTON)
        y += ALTO_BOTON + espaciado
    if (item.get("es_archivo") and item.get("archivo_id")) or item.get("is_image"):
        lay["descarga"] = (x_burbuja + SANGRIA, y, ANCHO_BOTON, ALTO_BOTON)
        y += ALTO_BOTON + espaciado
    if item.get("id"):
        fila_acciones = lay.get("descarga") or lay.get("retry")
        if fila_acciones is not None:
            y_responder = fila_acciones[1]
        else:
            y_responder = y
            y += ALTO_BOTON + espaciado
        lay["responder"] = (x_burbuja + tope - SANGRIA - ANCHO_RESPONDER,
                            y_responder, ANCHO_RESPONDER, ALTO_BOTON)
    return y


def pintar(painter, option, lay, item, t, fuente):
    """Pinta los botones que el layout traiga; ninguno es obligatorio."""
    if "retry" in lay:
        _boton(painter, option, lay["retry"], "Reintentar imagen", t, fuente)
    if "descarga" in lay:
        texto = "Descargando…" if item.get("descargando") else (
            "⬇ Guardar imagen" if item.get("is_image")
            else "⬇ Guardar archivo")
        _boton(painter, option, lay["descarga"], texto, t, fuente)
    if "responder" in lay:
        _boton(painter, option, lay["responder"], BOTON_RESPONDER, t, fuente)


def clic(lx, ly, lay, item, reintentar, descargar, responder):
    """Clic por coordenadas; `True` si algún botón lo consumió."""
    if _dentro(lay, "retry", lx, ly) and reintentar:
        reintentar(item)
        return True
    if _dentro(lay, "descarga", lx, ly) and descargar:
        descargar(item)
        return True
    if _dentro(lay, "responder", lx, ly) and responder:
        responder(item)
        return True
    return False


def _dentro(lay, clave, lx, ly):
    if clave not in lay:
        return False
    x, y, w, h = lay[clave]
    return x <= lx <= x + w and y <= ly <= y + h


def _boton(painter, option, rect, texto, t, fuente):
    painter.save()
    painter.setBrush(QColor(t["burbuja_ajena"]))
    painter.setPen(QColor(t["burbuja_ajena_borde"]))
    x, y, w, h = rect
    painter.drawRoundedRect(option.rect.x() + x, option.rect.y() + y,
                            w, h, 4, 4)
    painter.setFont(fuente)
    painter.setPen(QColor(t["texto"]))
    painter.drawText(option.rect.x() + x, option.rect.y() + y, w, h,
                     Qt.AlignmentFlag.AlignCenter, texto)
    painter.restore()
