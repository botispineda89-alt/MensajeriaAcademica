"""Composition root del cliente Python: qué piezas existen y quién las crea.

Espejo de ``FabricaCliente`` + ``FabricaControladores`` del cliente Java. Hasta
ahora ``ChatWindow`` construía sola su estado de búsqueda, su hub de señales, su
pool de hilos y sus preferencias: la ventana era su propia raíz de composición, y
en un cliente en el que lo normal es inyectar, cualquier colaborador que cambie
de implementación había que cambiarlo dentro de la vista.

La ventana **recibe** este contexto desde ``main.py``. Sin contexto (los tests) se
construye aquí, que sigue siendo el único sitio que decide de qué implementación
es cada pieza.
"""

from dataclasses import dataclass
from pathlib import Path

from PySide6.QtCore import QThreadPool

from ui.chat_view_model import ChatViewModel
from ui.componentes_chat import EventosChat
from ui.preferencias_chat import PreferenciasChat


@dataclass(frozen=True)
class ContextoChat:
    """Lo que la vista de chat recibe en vez de construirse ella misma.

    Los cuatro son de por vida de la ventana y ninguno depende de la cuenta: por
    eso pueden construirse una vez y compartirse entre reaperturas del chat.
    """

    vista: ChatViewModel
    eventos: EventosChat
    pool: QThreadPool
    preferencias: PreferenciasChat

    def directorio_imagenes(self) -> Path:
        """Donde viven los iconos por tipo de archivo que se distribuyen."""
        return Path(__file__).resolve().parents[1] / "imagenes"


def construir_contexto_chat(max_hilos: int = 4, parent=None, vista=None, eventos=None,
                            pool=None, preferencias=None) -> ContextoChat:
    """Crea las piezas compartidas de la vista de chat.

    Cada pieza acepta su propia fábrica para que quien la usa pueda sustituir una
    sin las demás: los tests aíslan ``PreferenciasChat`` con un ``QSettings``
    temporal (si no, el «último chat guardado» de una prueba se filtra a la
    siguiente), y un doble de red no necesita señales de Qt.

    ``parent`` es la ventana, si existe: QThreadPool y QObject necesitan dueño
    para morir con ella en vez de quedar colgando hasta que termine el proceso.
    """
    if pool is None:
        pool = QThreadPool(parent)
        pool.setMaxThreadCount(max_hilos)
    return ContextoChat(
        vista=ChatViewModel() if vista is None else vista,
        eventos=EventosChat(parent) if eventos is None else eventos,
        pool=pool,
        preferencias=PreferenciasChat() if preferencias is None else preferencias,
    )
