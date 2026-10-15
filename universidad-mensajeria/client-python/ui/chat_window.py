from pathlib import Path

from PySide6.QtCore import QSize, QTimer
from PySide6.QtWidgets import QMainWindow

from transversal.estado_conexion import CONECTADO
from transversal.textos import TITULO_CHAT
from ui.busqueda_historial_chat import BusquedaHistorialChat
from ui.componentes_chat import TareaChat as _Task
from ui.composicion_chat import construir_contexto_chat
# PAT-01: estos dos imports ya no se usan para construir nada aqui, pero siguen
# en el modulo a proposito: son la costura que los tests sustituyen
# (``ui.chat_window.PreferenciasChat``). Quitarlos dejaria la ventana sin forma
# de probarse con dobles.
from ui.preferencias_chat import PreferenciasChat
from ui.constructor_vista_chat import ConstructorVistaChat
from ui.estado_aviso_inline import TipoAviso
from ui.flujo_conversacion_chat import FlujoConversacionChat
from ui.gestor_adjuntos_chat import GestorAdjuntosChat
from ui.gestor_salida_sesion_chat import GestorSalidaSesionChat
from ui.paginador_historial_chat import PaginadorHistorialChat
from ui.procesador_eventos_chat import ProcesadorEventosChat
from ui.procesador_tareas_chat import ProcesadorTareasChat
from ui.refrescador_contactos_chat import RefrescadorContactosChat
from ui.renderizado_texto import resaltar_html
from ui.tema import (
    MODO_OSCURO,
    aplicar_tema_fluent,
    estilo_cita,
    estilo_preview,
    guardar_modo,
    modo_guardado,
    modo_por_indice,
    normalizar,
    tokens,
)
from ui.visor_imagen_chat import VisorImagenChat as _VisorImagen


class ChatWindow(GestorSalidaSesionChat, FlujoConversacionChat, ProcesadorEventosChat, ProcesadorTareasChat, RefrescadorContactosChat, BusquedaHistorialChat, GestorAdjuntosChat, PaginadorHistorialChat, ConstructorVistaChat, QMainWindow):
    """PAT-VM: ``usuario_seleccionado``, la respuesta citada y el estado de carga
    son del :class:`ChatViewModel`. Aquí son accesos, no copias: por eso la vista
    no puede quedar desincronizada del estado que habilita el botón de enviar.
    """

    @property
    def usuario_seleccionado(self):
        return self.modelo_vista.usuario_seleccionado

    @usuario_seleccionado.setter
    def usuario_seleccionado(self, valor):
        self.modelo_vista.usuario_seleccionado = valor

    @property
    def _respuesta_mensaje(self):
        return self.modelo_vista._respuesta_mensaje

    @_respuesta_mensaje.setter
    def _respuesta_mensaje(self, valor):
        self.modelo_vista._respuesta_mensaje = valor

    @property
    def _offset(self):
        return self.modelo_vista.offset

    @_offset.setter
    def _offset(self, valor):
        self.modelo_vista.offset = valor

    @property
    def _cargando_historial(self):
        return self.modelo_vista._cargando_historial

    @_cargando_historial.setter
    def _cargando_historial(self, valor):
        self.modelo_vista._cargando_historial = valor

    def __init__(self, fachada, codigo, on_logout, contexto=None):
        super().__init__()
        self.fachada = fachada
        self.codigo = codigo
        self.on_logout = on_logout
        # PAT-01: la ventana recibe sus piezas de por vida desde ``contexto`` en
        # vez de construirlas. ``main.py`` construye uno y lo pasa; sin contexto
        # (los tests) se construye aquí, que sigue siendo el único sitio que
        # decide de qué implementación es cada una. ``PreferenciasChat`` se pasa
        # explícitamente porque es la costura que los tests sustituyen con unos
        # ajustes temporales, para que el último chat guardado no se filtre entre
        # pruebas. Ver ``ui/composicion_chat``.
        self._ctx = contexto or construir_contexto_chat(
            parent=self, preferencias=PreferenciasChat())
        # El estado de conversación ya no se duplica aquí: vive en el ViewModel
        # (propiedades de arriba). Solo lo que es de la ventana se queda.
        self._cerrando = False
        self._pagina_remota = {}
        self._total_remoto = {}
        self._historial_ids = set()
        self._gen_historial = 0
        self._gen_directorio = 0
        self._reconstruyendo = False
        self._directorio = []
        # Tarjetas de conversación: orden fecha-desc, repintado coalescido.
        self._recientes = None
        self._huella_pintada = None
        self._timer_recientes = QTimer(self)
        self._timer_recientes.setSingleShot(True)
        self._timer_recientes.setInterval(400)
        self._timer_recientes.timeout.connect(self._pintar_recientes_desde_cache)
        # UIX-06/UIX-07: búsqueda en el historial con debounce.
        self.modelo_vista = self._ctx.vista
        self._preferencias_chat = self._ctx.preferencias
        # T1 (2026-10-14): se quitó la restauración de la última conversación al
        # arrancar. La preferencia se sigue guardando (flujo_conversacion_chat),
        # pero el chat abre sin selección a propósito.
        self._timer_buscar = QTimer(self)
        self._timer_buscar.setSingleShot(True)
        self._timer_buscar.setInterval(300)
        self._timer_buscar.timeout.connect(self._ejecutar_busqueda)
        # UIX-07: el filtro de contactos también va con debounce.
        self._timer_filtro = QTimer(self)
        self._timer_filtro.setSingleShot(True)
        self._timer_filtro.setInterval(300)
        self._timer_filtro.timeout.connect(self._filtrar_usuarios)
        # UIX-09: cargas de imagen en vuelo por id + ids cancelados. Antes no
        # había forma de abortarlas al salir de la vista.
        self._cargas_imagen = {}
        self._imagen_canceladas = set()
        # UIX-13/UIX-14: estado de carga del historial para el aviso.
        self._cargando_historial = False
        # Coalescencia de rafagas de presencia: un solo refresh por ventana.
        self._timer_presencia = QTimer(self)
        self._timer_presencia.setSingleShot(True)
        self._timer_presencia.setInterval(1500)
        self._timer_presencia.timeout.connect(lambda: self.actualizar_usuarios())
        self._eventos = self._ctx.eventos
        self._eventos.message.connect(self._procesar_mensaje)
        self._eventos.closure.connect(self._procesar_cierre)
        self._eventos.presence.connect(self._procesar_presencia)
        self._eventos.disconnected.connect(self._procesar_desconexion)
        self._eventos.state.connect(self._procesar_estado_conexion)
        self._eventos.task.connect(self._procesar_tarea)
        self._eventos.progreso.connect(self._procesar_progreso)
        self._pool = self._ctx.pool
        self._directorio_imagenes = self._ctx.directorio_imagenes()

        # P4: aquí solo se conoce el código; el nombre completo llega con el
        # directorio y `_entregar_directorio` vuelve a pintar el título.
        self.setWindowTitle(TITULO_CHAT.format(identidad=codigo))
        self.setMinimumSize(QSize(900, 600))
        self.resize(1120, 740)
        self.setAcceptDrops(True)
        self.modo = self._leer_modo()
        self._construir_ui()
        self._restaurar_divisor_lateral()
        self._divisor_lateral.splitterMoved.connect(self._guardar_divisor_lateral)
        self.aplicar_tema(self.modo)

        self.fachada.on_mensaje_recibido_ui = self._eventos.message.emit
        self.fachada.on_cierre_ui = self._eventos.closure.emit
        self.fachada.on_presencia_ui = self._eventos.presence.emit
        self.fachada.on_desconexion_ui = self._eventos.disconnected.emit
        # PAT-03: la vista pinta el estado que publica la fachada. Antes solo se
        # enteraba de la caida por un texto suelto y el banner offline se movia
        # dentro del resultado de `directorio`.
        if hasattr(self.fachada, "on_estado_ui"):
            self.fachada.on_estado_ui = self._eventos.state.emit
            self._procesar_estado_conexion(getattr(self.fachada, "estado_conexion", CONECTADO))
        self.lista_mensajes.verticalScrollBar().valueChanged.connect(self._cargar_imagenes_visibles)
        self.actualizar_usuarios()


    def _leer_modo(self):
        return modo_guardado(self.fachada)

    def _guardar_modo(self):
        guardar_modo(self.fachada, self.modo)

    def _restaurar_divisor_lateral(self):
        tamanos = self._preferencias_chat.divisor_lateral(self.codigo)
        if tamanos is not None:
            self._divisor_lateral.setSizes(list(tamanos))

    def _guardar_divisor_lateral(self, *_):
        self._preferencias_chat.guardar_divisor_lateral(
            self.codigo, self._divisor_lateral.sizes())

    def _cambiar_tema(self, indice):
        self.modo = modo_por_indice(indice)
        self.aplicar_tema(self.modo)
        self._guardar_modo()

    def aplicar_tema(self, modo):
        """Aplica el modo claro/oscuro a paneles y burbujas (UI-09)."""
        self.modo = normalizar(modo)
        aplicar_tema_fluent(self.modo)
        t = tokens(self.modo)
        self._panel_usuarios.setStyleSheet(f"background: {t['tarjeta']};")
        self.lista_usuarios.setStyleSheet(
            f"QListWidget {{ background: {t['tarjeta']}; border: none; }}")
        self._delegado_contactos.actualizar_tema(self.modo)
        self.lista_usuarios.viewport().update()
        self._panel_chat.setStyleSheet(f"background: {t['lista']};")
        self._header.setStyleSheet(
            f"background: {t['tarjeta']}; border-bottom: 1px solid {t['tarjeta_borde']};")
        self._composer.setStyleSheet(
            f"background: {t['tarjeta']}; border-top: 1px solid {t['tarjeta_borde']};")
        self.lbl_sesion.setStyleSheet(
            f"font-size: 16px; font-weight: 700; color: {t['texto']};")
        self.lbl_contactos.setStyleSheet(
            f"font-weight: 700; color: {t['texto']};")
        self.lbl_tema.setStyleSheet(
            f"color: {t['texto']}; background: transparent; border: none;")
        self.lbl_conversacion.setStyleSheet(
            f"font-size: 17px; font-weight: 700; color: {t['texto']};")
        self.lbl_presencia.setStyleSheet(
            f"color: {t['texto_suave']}; font-size: 11px;")
        self.lista_mensajes.setStyleSheet(
            "QListView { border: 0; padding: 9px; }")
        nombre_fondo = "fondo_chat_claro.jpg" if self.modo != MODO_OSCURO \
            else "fondo_chat_oscuro.jpg"
        self.lista_mensajes.fijar_fondo(
            Path(__file__).resolve().parent / "fondos" / nombre_fondo)
        # UIX-01: el delegado pinta con el modo; sin bucle por filas.
        self._delegado_mensajes.set_modo(self.modo)
        self.lista_mensajes.viewport().update()
        self.cita_container.setStyleSheet(estilo_cita(t))
        self.lbl_respuesta.setStyleSheet(
            f"color: {t['cita_texto']}; font-size: 11px; border: none; background: transparent;")
        self.preview_container.setStyleSheet(estilo_preview(t))
        self.bandeja_adjuntos.actualizar_tema(self.modo)
        self.aviso_inline.actualizar_tema(self.modo)
        self.lbl_vacio_chat.setStyleSheet(
            f"color: {t['texto_suave']}; font-size: 12px; padding: 16px;")
        self.lbl_vacio_usuarios.setStyleSheet(
            f"color: {t['texto_suave']}; font-size: 12px; padding: 16px;")
        self.lbl_buscar_cuenta.setStyleSheet(
            f"color: {t['texto_suave']}; font-size: 11px;")
        self.barra_buscar.setStyleSheet(f"background: {t['tarjeta']};")
        if not self.lbl_offline.isVisible():
            self.lbl_estado_red.setStyleSheet(
                f"color: {t['texto_suave']}; font-size: 11px;")
        self.combo_tema.setCurrentIndex(0 if self.modo == MODO_OSCURO else 1)

    def _ejecutar(self, tipo, accion):
        tarea = _Task(tipo, accion, self._eventos)
        self._pool.start(tarea)
        return tarea

    def cancelar_cargas_imagen(self):
        """UIX-09: aborta las descargas en vuelo al salir de la vista.

        Las que aún están en cola se sacan del pool; las que ya corren se
        marcan y su resultado se descarta al llegar (no pueden interrumpirse
        a mitad de socket, pero ya no tocan la UI).
        """
        for mid, tarea in list(self._cargas_imagen.items()):
            try:
                self._pool.tryTake(tarea)
            except Exception:
                pass
            self._imagen_canceladas.add(mid)
        self._cargas_imagen.clear()


























    # ------------------------------------------------- UIX-06/UIX-07: buscar













    # SEG-02: paridad con Java/.NET y con la tuberia del servidor (ImageIO del
    # JDK solo decodifica PNG/JPEG/GIF/BMP; ver Imagenes.esImagenValida +
    # ImagenMagicaTest.rechazaWebpSinSoporteEnTuberia). .webp se OFRECE en el
    # dialogo pero viaja como MENSAJE_ARCHIVO, igual que en los otros clientes.






    def _aviso_red(self, texto, tipo=TipoAviso.ERROR):
        """Banner inline para errores de red/descarga (no abre ventanas)."""
        self.aviso_inline.mostrar(texto, tipo)

















    @staticmethod
    def _estado_visible(estado):
        """Texto del tick de la burbuja.

        Los 5 estados y sus glifos son los de `contrato/estado-mensaje.json`
        (cierra VIS-02). `ERROR` estaba fuera del diccionario: `.get(estado, ...)`
        devolvia la cadena cruda, asi que un mensaje que el servidor rechazo
        se quedaba sin indicador en la burbuja mientras Java si lo mostraba.
        """
        return {
            "PENDIENTE": "⏳ Pendiente",
            "ENVIADO": "✓ Enviado",
            "ENTREGADO": "✓✓ Entregado",
            "LEIDO": "✓✓ Leído",
            "ERROR": "! No enviado",
        }.get(estado, "" if estado == "ERROR" else (estado or ""))
