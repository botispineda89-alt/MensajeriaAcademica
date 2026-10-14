"""Estado de presentación del chat: lo que la vista pinta y lo que puede enviar.

Espejo de ``ChatViewModel`` de JavaFX y del que se añadirá en WPF. Antes de esto
el estado de la conversación vivía disperso: ``usuario_seleccionado``,
``_offset``, ``_cargando_historial``, ``_enviando`` y la respuesta citada eran
atributos sueltos de la ventana, y «puedo enviar» se recalculaba con un
``setEnabled`` escrito a mano en tres sitios distintos.

Aquí vive el estado, y «puedo enviar» se **deriva** de él: un solo predicado que
los tres clientes comparten en su forma y que la vista solo consulta. Eso es lo que
convierte el botón en algo declarativo en vez de algo imperativo (fase E).

Es ``QObject`` a propósito: el enlace de Qt necesita un objeto con señales, y por
eso este archivo es el único de ``ui/`` que puede importar ``QtCore``. Ni
``QtWidgets``, ni ``sqlite3``, ni ``socket`` — lo comprueba
``test_arquitectura.py::test_view_model_no_depende_de_framework_ni_adaptadores``.
"""

from PySide6.QtCore import Property, QObject, Signal


class ChatViewModel(QObject):
    """Estado de conversación sin widgets ni red."""

    usuarioCambiado = Signal()
    envioCambiado = Signal()
    adjuntosCambiados = Signal()
    respuestaCambiada = Signal()
    cargaCambiada = Signal()

    def __init__(self, parent=None):
        super().__init__(parent)
        self._usuario: str | None = None
        self._enviando = False
        self._tiene_adjuntos = False
        self._texto = ""
        self._respuesta: str | None = None
        self._cargando = False
        self._offset = 0
        # UIX-06: búsqueda en el historial. Planos y públicos a propósito: los
        # colaboradores (paginador, barra de búsqueda) los leen directamente.
        self.ids_coincidencia: list[str] = []
        self.indice_coincidencia: int = -1
        self.patron_busqueda: str = ""
        self.salto_pendiente: tuple[str, int] | None = None

    # ------------------------------------------------------------ conversación
    @Property(str, notify=usuarioCambiado)
    def usuario(self) -> str:
        """Cuenta con la que se está conversando ('' si ninguna)."""
        return self._usuario or ""

    @usuario.setter
    def usuario(self, valor: str | None) -> None:
        if (self._usuario or None) == (valor or None):
            return
        self._usuario = valor
        self._offset = 0
        self.usuarioCambiado.emit()

    @Property(int, notify=usuarioCambiado)
    def offset(self) -> int:
        return self._offset

    @offset.setter
    def offset(self, valor: int) -> None:
        if self._offset != valor:
            self._offset = valor
            self.usuarioCambiado.emit()

    @Property(bool, notify=cargaCambiada)
    def cargando(self) -> bool:
        return self._cargando

    @cargando.setter
    def cargando(self, valor: bool) -> None:
        if self._cargando != valor:
            self._cargando = valor
            self.cargaCambiada.emit()

    # ------------------------------------------------------------------- envío
    @Property(bool, notify=envioCambiado)
    def enviando(self) -> bool:
        return self._enviando

    @enviando.setter
    def enviando(self, valor: bool) -> None:
        if self._enviando != valor:
            self._enviando = valor
            self.envioCambiado.emit()

    @Property(str, notify=envioCambiado)
    def texto(self) -> str:
        return self._texto

    @texto.setter
    def texto(self, valor: str) -> None:
        if self._texto != valor:
            self._texto = valor
            self.envioCambiado.emit()

    @Property(bool, notify=adjuntosCambiados)
    def hay_adjuntos(self) -> bool:
        return self._tiene_adjuntos

    @hay_adjuntos.setter
    def hay_adjuntos(self, valor: bool) -> None:
        if self._tiene_adjuntos != valor:
            self._tiene_adjuntos = valor
            self.adjuntosCambiados.emit()
            self.envioCambiado.emit()

    @Property(str, notify=respuestaCambiada)
    def respuesta(self) -> str:
        """Prefijo de la cita en curso ('' si no hay respuesta)."""
        return self._respuesta or ""

    @respuesta.setter
    def respuesta(self, valor: str | None) -> None:
        if (self._respuesta or None) == (valor or None):
            return
        self._respuesta = valor
        self.respuestaCambiada.emit()

    # ------------------------------------------------------------- predicados
    def puede_enviar(self) -> bool:
        """El único sitio que decide si el botón de enviar se habilita.

        Es una función y no una ``Property`` a propósito: depende de texto,
        adjuntos, destinatario y «enviando» a la vez, y en Qt eso sería una
        propiedad que emite cuatro señales para un solo botón.
        """
        return bool(self._usuario) and not self._enviando \
            and bool(self._texto.strip() or self._tiene_adjuntos)

    # -------------------------------------------------------------- búsqueda
    def limpiar_busqueda(self) -> None:
        self.ids_coincidencia.clear()
        self.indice_coincidencia = -1
        self.patron_busqueda = ""
        self.salto_pendiente = None

    def fijar_coincidencias(self, patron: str, identificadores: list[str]) -> str | None:
        self.patron_busqueda = patron
        self.ids_coincidencia = list(identificadores)
        self.indice_coincidencia = 0 if self.ids_coincidencia else -1
        return self.ids_coincidencia[0] if self.ids_coincidencia else None

    def mover_coincidencia(self, paso: int) -> str | None:
        if not self.ids_coincidencia:
            return None
        self.indice_coincidencia = (
            self.indice_coincidencia + paso) % len(self.ids_coincidencia)
        return self.ids_coincidencia[self.indice_coincidencia]

    def programar_salto(self, identificador: str, intentos: int) -> None:
        self.salto_pendiente = (identificador, intentos)

    def consumir_salto(self) -> tuple[str, int] | None:
        pendiente = self.salto_pendiente
        self.salto_pendiente = None
        return pendiente

    # ------------------------------------------------------------- COMPATIBILIDAD
    # Los atributos de antes se leen desde varios colaboradores con el nombre
    # viejo. Estas propiedades los sirven sin cambiar una sola llamada: es lo que
    # permite mover el estado al ViewModel sin reescribir la vista entera.
    @property
    def usuario_seleccionado(self):
        return self._usuario

    @usuario_seleccionado.setter
    def usuario_seleccionado(self, valor):
        self.usuario = valor

    @property
    def _cargando_historial(self):
        return self._cargando

    @_cargando_historial.setter
    def _cargando_historial(self, valor):
        self.cargando = valor

    @property
    def _respuesta_mensaje(self):
        return self._respuesta

    @_respuesta_mensaje.setter
    def _respuesta_mensaje(self, valor):
        self.respuesta = valor