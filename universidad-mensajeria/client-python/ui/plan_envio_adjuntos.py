"""Política de burbujas para texto y lotes de adjuntos."""

from dataclasses import dataclass
from pathlib import Path
import uuid

MAXIMO_ADJUNTOS = 12
MAXIMO_IMAGENES_POR_MONTAJE = MAXIMO_ADJUNTOS


@dataclass(frozen=True)
class Texto:
    id_mensaje: str
    cuerpo: str


@dataclass(frozen=True)
class Adjunto:
    id_mensaje: str
    ruta: str
    caption: str | None = None


class PlanEnvioAdjuntos:
    @staticmethod
    def calcular(texto, rutas, extensiones_imagen):
        cuerpo = (texto or "").strip()
        archivos = list(dict.fromkeys(str(Path(ruta).resolve()) for ruta in rutas or []))
        if len(archivos) > MAXIMO_ADJUNTOS:
            raise ValueError(f"Máximo {MAXIMO_ADJUNTOS} adjuntos por envío.")

        acciones = [
            Adjunto(str(uuid.uuid4()), ruta)
            for ruta in archivos
        ]

        if not acciones:
            return (Texto(str(uuid.uuid4()), cuerpo),) if cuerpo else ()
        if cuerpo and (len(acciones) == 1 or all(
                Path(accion.ruta).suffix.casefold() in extensiones_imagen
                for accion in acciones)):
            primera = acciones[0]
            acciones[0] = Adjunto(primera.id_mensaje, primera.ruta, cuerpo)
        elif cuerpo:
            acciones.insert(0, Texto(str(uuid.uuid4()), cuerpo))
        return tuple(acciones)
