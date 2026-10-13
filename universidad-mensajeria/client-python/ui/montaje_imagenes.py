"""Compone una vista previa local de las imágenes seleccionadas."""

from io import BytesIO
from pathlib import Path
import uuid

from PIL import Image

from ui.plan_envio_adjuntos import MAXIMO_IMAGENES_POR_MONTAJE

LADO_CELDA = 480
HUECO = 4
COLOR_FONDO = (42, 47, 58, 255)


def _distribucion(cantidad):
    if cantidad < 1:
        raise ValueError("El montaje requiere al menos una imagen.")
    if cantidad == 3:
        return 3, 1
    columnas = min(3, 2 if cantidad <= 4 else 3)
    return columnas, (cantidad + columnas - 1) // columnas


def componer(rutas, directorio_salida):
    lienzo = componer_en_memoria(rutas)
    carpeta = Path(directorio_salida)
    carpeta.mkdir(parents=True, exist_ok=True)
    ruta_salida = carpeta / f"montaje_{uuid.uuid4().hex}.png"
    salida = BytesIO()
    lienzo.save(salida, format="PNG")
    ruta_salida.write_bytes(salida.getvalue())
    return str(ruta_salida)


def componer_en_memoria(rutas):
    fuentes_rutas = tuple(rutas)
    if not 2 <= len(fuentes_rutas) <= MAXIMO_IMAGENES_POR_MONTAJE:
        raise ValueError(
            f"El montaje admite de 2 a {MAXIMO_IMAGENES_POR_MONTAJE} imágenes.")

    columnas, filas = _distribucion(len(fuentes_rutas))
    ancho = columnas * LADO_CELDA + (columnas - 1) * HUECO
    alto = filas * LADO_CELDA + (filas - 1) * HUECO
    lienzo = Image.new("RGBA", (ancho, alto), COLOR_FONDO)
    for indice, ruta in enumerate(fuentes_rutas):
        with Image.open(ruta) as fuente:
            imagen = fuente.convert("RGBA")
            imagen.thumbnail((LADO_CELDA, LADO_CELDA), Image.Resampling.LANCZOS)
        x_celda = (indice % columnas) * (LADO_CELDA + HUECO)
        y_celda = (indice // columnas) * (LADO_CELDA + HUECO)
        x = x_celda + (LADO_CELDA - imagen.width) // 2
        y = y_celda + (LADO_CELDA - imagen.height) // 2
        lienzo.alpha_composite(imagen, (x, y))
    return lienzo
