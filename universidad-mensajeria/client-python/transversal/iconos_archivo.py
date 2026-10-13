"""Icono por tipo de archivo de un adjunto.

Réplica de `IconoArchivo.java` (JavaFX) y `CatalogoAdjuntos.IconoArchivo`
(WPF): la misma extensión pinta el mismo dibujo en los tres clientes, para que
un PDF no se vea igual que una hoja de cálculo y el receptor sepa qué le
llegó sin leer el nombre.

Antes los tres clientes pintaban el mismo `📎` genérico para cualquier
archivo. La extensión que no está en el catálogo devuelve ``None`` y quien
pinta sigue con el `📎` de siempre: el cambio es aditivo.
"""
from pathlib import Path

#: Carpeta con los PNG (misma lista que el classpath de Java y `Iconos/` de WPF).
RAIZ_ICONOS = Path(__file__).resolve().parent.parent / "ui" / "iconos"

#: Ancho del icono dentro de la burbuja, en píxeles: estilo tarjeta de
#: archivo de Telegram Web (antes 18, apenas se distinguía).
ANCHO_BURBUJA = 48

#: Ancho del icono en la bandeja de adjuntos del compositor.
ANCHO_BANDEJA = 48

#: Extensión (en minúsculas, sin punto) al archivo del catálogo. Solo entran
#: las cinco familias para las que hay dibujo; `.pptx` o `.zip` se quedan
#: fuera a propósito en vez de reutilizar un icono que miente.
POR_EXTENSION = {
    "pdf": "pdf",
    "xlsx": "xlsx",
    "xls": "xlsx",
    "csv": "csv",
    "json": "json",
    "doc": "docs",
    "docx": "docs",
    "odt": "docs",
    "rtf": "docs",
    "txt": "docs",
    "md": "docs",
}


def clave(nombre):
    """Nombre del icono del catálogo, o ``None`` si esa extensión no tiene uno.

    Acepta el nombre pelado o una ruta completa (`C:\\descargas\\acta.pdf`): la
    extensión se busca en el último tramo, igual que en Java y en .NET.
    """
    if not nombre:
        return None
    ultimo = str(nombre).replace("\\", "/").rsplit("/", 1)[-1]
    if "." not in ultimo:
        return None
    extension = ultimo.rsplit(".", 1)[1].casefold()
    if not extension:
        return None
    return POR_EXTENSION.get(extension)


def ruta(nombre):
    """``Path`` del PNG del tipo, o ``None`` si no hay icono.

    Comprueba que el archivo exista de verdad: un PNG que no se empaquetó se
    ve como el `📎` de siempre y no como una imagen rota.
    """
    clave_icono = clave(nombre)
    if clave_icono is None:
        return None
    candidata = RAIZ_ICONOS / f"{clave_icono}.png"
    return candidata if candidata.is_file() else None


def con_icono(ruta_icono, html, ancho=ANCHO_BURBUJA):
    """Antepone el PNG del tipo a un HTML ya montado.

    ``ruta_icono`` es lo que devuelve :func:`ruta`; ``None`` significa que esa
    extensión no tiene dibujo, y entonces el HTML vuelve intacto para que la
    burbuja siga con el `📎` de siempre. Vive aquí y no en el delegado porque
    `ui/` tiene un tope de líneas por archivo (test_arquitectura).
    """
    if ruta_icono is None:
        return html
    return (f'<img src="{ruta_icono.as_uri()}" width="{ancho}" height="{ancho}"'
            f' align="middle">&nbsp;{html}')


def prefijo(nombre):
    """`"📎 "` solo para las extensiones SIN dibujo propio.

    Cuando el tipo tiene icono en el catálogo, el delegado lo pinta grande
    (estilo tarjeta de archivo de Telegram Web) y el `📎` del texto quedaba
    como un segundo icono al lado; para esas extensiones el prefijo sobra.
    """
    return "" if ruta(nombre) else "📎 "
