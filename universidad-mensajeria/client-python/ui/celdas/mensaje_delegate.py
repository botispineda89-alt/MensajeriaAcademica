"""Delegado virtualizado que representa cada mensaje visible."""

from PySide6.QtCore import QSize, Qt, QUrl
from PySide6.QtGui import (
    QAbstractTextDocumentLayout, QColor, QFont, QFontMetrics, QImage, QPalette, QPen,
    QTextCharFormat, QTextCursor, QTextDocument,
)
from PySide6.QtWidgets import QApplication, QStyledItemDelegate, QStyle

from transversal import iconos_archivo
from transversal.fechas import hora_corta
from ui.celdas import acciones_burbuja, bloque_cita
from ui.lista_virtual import ROL_ITEM
from ui.renderizado_texto import resaltar_html
from ui.tema import TOKENS_CANONICOS, color_tick, normalizar, tokens
from ui.miniaturas_chat import MiniaturasChat
from transversal.etapas_filtrado import resumir_etapas_filtrado

ANCHO_MAX_BURBUJA = 580
ANCHO_MIN_BURBUJA = 280
MARGEN_FILA = 10
BUBBLE_PAD_H = 12
BUBBLE_PAD_V = 8
ESPACIADO = 5


class MensajeDelegate(QStyledItemDelegate):
    """Pinta burbujas y resuelve clics por coordenadas."""

    def __init__(self, parent=None, modo="oscuro"):
        super().__init__(parent)
        self._modo = modo
        self._patron = ""
        self._miniaturas = MiniaturasChat()
        from collections.abc import Callable
        self.responder: Callable[[dict], None] | None = None
        self.reintentar: Callable[[dict], None] | None = None
        self.descargar: Callable[[dict], None] | None = None
        self.ampliar: Callable[[str], None] | None = None
        self.navegar_cita: Callable[[dict, str], None] | None = None
        self._fuente_remitente = QFont()
        self._fuente_remitente.setPointSize(12)
        self._fuente_remitente.setBold(True)
        self._fuente_cuerpo = QFont()
        self._fuente_cuerpo.setPointSize(13)
        self._fuente_mini = QFont()
        self._fuente_mini.setPointSize(11)

    # -- configuración ---------------------------------------------------

    def set_modo(self, modo):
        self._modo = modo

    def set_patron(self, patron):
        self._patron = patron or ""

    def guardar_pixmap(self, id_mensaje, pixmap):
        self._miniaturas.guardar(id_mensaje, pixmap)

    def pixmap_de(self, id_mensaje):
        return self._miniaturas.obtener(id_mensaje)

    # -- layout compartido (sizeHint + paint + hit-test) -------------------

    def _ancho_burbuja(self, ancho_vista):
        if ancho_vista <= 100:
            return ANCHO_MAX_BURBUJA
        return min(ANCHO_MAX_BURBUJA,
                   max(ANCHO_MIN_BURBUJA, int(ancho_vista * 0.65)))

    def distribuir(self, item, ancho_vista, t):
        """Devuelve rectángulos relativos a la fila (x, y, w, h) y alto total.

        Claves: 'burbuja', 'remitente', 'cuerpo', 'imagen', 'estado_img',
        'retry', 'descarga', 'pie', 'fecha'. Todo lo opcional puede faltar.
        """
        if item.get("es_separador"):
            return {"alto": 28, "separador": True}
        tope = self._ancho_burbuja(ancho_vista)
        propia = bool(item.get("is_own"))
        # Ancho útil dentro de la burbuja.
        interior = tope - BUBBLE_PAD_H * 2
        x_burbuja = ancho_vista - MARGEN_FILA - tope if propia \
            else MARGEN_FILA
        y = 3
        lay = {}
        if item.get("mostrar_remitente", True):
            fm = QFontMetrics(self._fuente_remitente)
            h = fm.height() + 2
            lay["remitente"] = (x_burbuja + BUBBLE_PAD_H, y + BUBBLE_PAD_V,
                                interior, h)
            y += BUBBLE_PAD_V + h + ESPACIADO
        else:
            y += BUBBLE_PAD_V
        color_cuerpo = t["cuerpo_propia"] if propia else t["cuerpo_ajena"]
        cuerpo, y = bloque_cita.repartir(
            lay, item.get("text", "") or "", x_burbuja + BUBBLE_PAD_H, y,
            interior, self._fuente_mini, self._patron, ESPACIADO,
            color_texto=t.get("cita_texto"))
        if cuerpo:
            doc = QTextDocument()
            doc.setDefaultFont(self._fuente_cuerpo)
            doc.setDefaultStyleSheet(
                f"body {{ color: {color_cuerpo}; font-size: 13px; }}")
            # El icono del tipo va DENTRO del documento y no como widget
            # aparte: así sizeHint, paint y el clic por coordenadas siguen
            # midiendo lo mismo sin tocar el reparto de la burbuja.
            icono = (iconos_archivo.ruta(item.get("filename"))
                     if item.get("es_archivo") else None)
            doc.setHtml(iconos_archivo.con_icono(
                icono, resaltar_html(cuerpo, self._patron)))
            if icono is not None:
                doc.addResource(QTextDocument.ResourceType.ImageResource,
                                QUrl(icono.as_uri()), QImage(str(icono)))
            doc.setTextWidth(interior)
            h = int(doc.size().height()) + 2
            lay["cuerpo"] = (x_burbuja + BUBBLE_PAD_H, y, interior, h)
            lay["_doc"] = doc
            y += h + ESPACIADO
        if item.get("is_image"):
            esc = self._miniaturas.escalar(item.get("id"))
            if esc is not None:
                lay["imagen"] = (x_burbuja + BUBBLE_PAD_H, y,
                                 esc.width(), esc.height())
                lay["_escalado"] = esc
                y += esc.height() + ESPACIADO
                resumen = resumir_etapas_filtrado(item.get("etapas_filtrado"))
                if resumen:
                    x_etapas = x_burbuja + BUBBLE_PAD_H
                    caja = QFontMetrics(self._fuente_mini).boundingRect(
                        0, 0, interior, 1000,
                        Qt.AlignmentFlag.AlignLeft | Qt.TextFlag.TextWordWrap,
                        resumen)
                    alto_etapas = max(QFontMetrics(self._fuente_mini).height(),
                                      caja.height())
                    lay["etapas_filtrado"] = (
                        x_etapas, y, interior, alto_etapas, resumen)
                    y += alto_etapas + ESPACIADO
            elif not item.get("image_loaded"):
                fm = QFontMetrics(self._fuente_mini)
                h = fm.height() + 2
                lay["estado_img"] = (x_burbuja + BUBBLE_PAD_H, y, interior, h)
                y += h + ESPACIADO
        caption = (item.get("caption") or "").strip()
        if caption:
            doc = QTextDocument()
            doc.setDefaultFont(self._fuente_mini)
            doc.setPlainText(caption)
            doc.setTextWidth(interior)
            h = int(doc.size().height()) + 2
            lay["caption"] = (x_burbuja + BUBBLE_PAD_H, y, interior, h)
            lay["_caption_doc"] = doc
            y += h + ESPACIADO
        # Reintentar / guardar / responder: la fila de acciones vive aparte
        # (`ui/celdas/acciones_burbuja.py`) y devuelve el `y` siguiente.
        y = acciones_burbuja.repartir(lay, item, x_burbuja, tope, y, ESPACIADO)
        fm = QFontMetrics(self._fuente_mini)
        h = fm.height() + 2
        lay["pie"] = (x_burbuja + BUBBLE_PAD_H, y, interior, h)
        y += h + BUBBLE_PAD_V
        # La burbuja se ciñe al contenido hasta el tope.
        lay["burbuja"] = (x_burbuja, 3, tope, y - 3 + 3)
        lay["alto"] = y + 3
        lay["propia"] = propia
        return lay

    # -- QStyledItemDelegate -------------------------------------------------

    def sizeHint(self, option, index):
        item = index.data(ROL_ITEM)
        if not isinstance(item, dict):
            return QSize()
        t = tokens(self._modo)
        lay = self.distribuir(item, option.rect.width() or 600, t)
        return QSize(option.rect.width(), lay["alto"])

    def paint(self, painter, option, index):
        item = index.data(ROL_ITEM)
        if not isinstance(item, dict):
            return
        t = tokens(self._modo)
        canon = TOKENS_CANONICOS[normalizar(self._modo)]
        painter.save()
        try:
            if option.state & QStyle.StateFlag.State_Selected:
                painter.fillRect(option.rect, QColor(canon["list.selected.bg"]))
            if item.get("es_separador"):
                self._pintar_separador(painter, option, item, t)
                return
            lay = self.distribuir(item, option.rect.width(), t)
            ox, oy = option.rect.x(), option.rect.y()
            # Burbuja.
            propia = lay["propia"]
            color_fondo = t["burbuja_propia"] if propia else t["burbuja_ajena"]
            painter.setBrush(QColor(color_fondo))
            bx, by, bw, bh = lay["burbuja"]
            if option.state & QStyle.StateFlag.State_Selected:
                painter.setPen(QPen(QColor("#FFD166"), 2))
            else:
                painter.setPen(QColor(t["burbuja_propia_borde"] if propia
                                     else t["burbuja_ajena_borde"]))
            painter.drawRoundedRect(ox + bx, oy + by, bw, bh, 8, 8)
            color_remitente = canon["sender.out.fg"] if propia else canon["sender.fg"]
            painter.setPen(QColor(color_remitente))
            if "remitente" in lay:
                painter.setFont(self._fuente_remitente)
                x, y, w, h = lay["remitente"]
                sender = item.get("sender", "")
                painter.drawText(ox + x, oy + y, w, h,
                                 Qt.AlignmentFlag.AlignLeft
                                 | Qt.AlignmentFlag.AlignVCenter, sender)
            bloque_cita.pintar_si_hay(painter, ox, oy, lay, t)
            if "cuerpo" in lay:
                doc = lay["_doc"]
                painter.save()
                painter.translate(ox + lay["cuerpo"][0], oy + lay["cuerpo"][1])
                ctx = QAbstractTextDocumentLayout.PaintContext()
                ctx.palette.setColor(QPalette.ColorRole.Text, QColor(
                    t["cuerpo_propia"] if propia else t["cuerpo_ajena"]))
                doc.documentLayout().draw(painter, ctx)
                painter.restore()
            if "imagen" in lay:
                x, y, w, h = lay["imagen"]
                painter.drawPixmap(ox + x, oy + y, lay["_escalado"])
            if "caption" in lay:
                painter.save()
                x, y, w, h = lay["caption"]
                painter.translate(ox + x, oy + y)
                ctx = QAbstractTextDocumentLayout.PaintContext()
                color_cap = (t["cuerpo_propia"] if propia else t["cuerpo_ajena"]) \
                    if self._modo != "oscuro" else t["texto_suave"]
                ctx.palette.setColor(QPalette.ColorRole.Text, QColor(color_cap))
                lay["_caption_doc"].documentLayout().draw(painter, ctx)
                painter.restore()
            if "etapas_filtrado" in lay:
                x, y, w, h, resumen = lay["etapas_filtrado"]
                painter.setFont(self._fuente_mini)
                painter.setPen(QColor(t["texto_suave"]))
                painter.drawText(
                    ox + x, oy + y, w, h,
                    Qt.AlignmentFlag.AlignLeft | Qt.TextFlag.TextWordWrap,
                    resumen)
            if "estado_img" in lay:
                painter.setFont(self._fuente_mini)
                painter.setPen(QColor(t["texto_suave"]))
                x, y, w, h = lay["estado_img"]
                painter.drawText(ox + x, oy + y, w, h,
                                 Qt.AlignmentFlag.AlignLeft
                                 | Qt.AlignmentFlag.AlignVCenter,
                                 item.get("image_state") or (
                                     "Enviada imagen" if propia else "Imagen"))
            if {"retry", "descarga", "responder"} & lay.keys():
                acciones_burbuja.pintar(painter, option, lay, item, t,
                                        self._fuente_mini)
            if "pie" in lay:
                painter.setFont(self._fuente_mini)
                painter.setPen(QColor(t["fecha"]))
                x, y, w, h = lay["pie"]
                painter.drawText(ox + x, oy + y, w, h,
                                 Qt.AlignmentFlag.AlignLeft
                                 | Qt.AlignmentFlag.AlignVCenter,
                                 hora_corta(item.get("date", "")))
                estado = item.get("state", "")
                if estado and propia:
                    painter.setPen(QColor(color_tick(t, item.get("raw_state"))))
                    fm = QFontMetrics(self._fuente_mini)
                    painter.drawText(ox + x, oy + y, w, h,
                                     Qt.AlignmentFlag.AlignRight
                                     | Qt.AlignmentFlag.AlignVCenter, estado)
        finally:
            painter.restore()

    def _pintar_separador(self, painter, option, item, t):
        painter.setFont(self._fuente_mini)
        painter.setPen(QColor(t["texto_suave"]))
        painter.drawText(option.rect, Qt.AlignmentFlag.AlignCenter,
                         item.get("etiqueta", ""))

    # -- interacción ---------------------------------------------------------

    def editorEvent(self, evento, modelo, option, index):
        from PySide6.QtCore import QEvent
        from PySide6.QtGui import QMouseEvent
        item = index.data(ROL_ITEM)
        if not isinstance(item, dict) or item.get("es_separador"):
            return False
        t = tokens(self._modo)
        lay = self.distribuir(item, option.rect.width(), t)
        ox, oy = option.rect.x(), option.rect.y()
        pos = evento.pos() if hasattr(evento, "pos") else None

        if evento.type() == QEvent.Type.MouseButtonPress and pos is not None:
            boton = evento.button() if hasattr(evento, "button") else None
            from PySide6.QtCore import Qt as _Qt
            if boton == _Qt.MouseButton.RightButton:
                texto = item.get("text", "") or ""
                if texto:
                    from PySide6.QtWidgets import QApplication as _App
                    _App.clipboard().setText(texto)
                    return True
                return False
            if boton == _Qt.MouseButton.LeftButton:
                lx, ly = pos.x() - ox, pos.y() - oy
                if bloque_cita.clic(lx, ly, lay, item, self.navegar_cita):
                    return True
                if "imagen" in lay:
                    x, y, w, h = lay["imagen"]
                    if x <= lx <= x + w and y <= ly <= y + h and self.ampliar:
                        if item.get("id"):
                            self.ampliar(item["id"])
                        return True
                if acciones_burbuja.clic(lx, ly, lay, item, self.reintentar,
                                         self.descargar, self.responder):
                    return True
        if evento.type() == QEvent.Type.MouseButtonDblClick:
            if self.responder:
                self.responder(item)
                return True
            return False
        if evento.type() == QEvent.Type.MouseButtonRelease and pos is not None:
            lx, ly = pos.x() - ox, pos.y() - oy
            if "imagen" in lay:
                x, y, w, h = lay["imagen"]
                if x <= lx <= x + w and y <= ly <= y + h and self.ampliar:
                    if item.get("id"):
                        self.ampliar(item["id"])
                    return True
            if acciones_burbuja.clic(lx, ly, lay, item, self.reintentar,
                                     self.descargar, self.responder):
                return True
        return False
