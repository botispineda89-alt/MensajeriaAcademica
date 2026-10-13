#!/usr/bin/env python
"""Comprueba que los iconos por tipo de archivo son los MISMOS en los 3 clientes.

    python contrato/verificar_iconos.py

Que resuelve: hay cinco PNG (pdf, xlsx, csv, json, docs) copiados en los tres
clientes porque cada distribucion es independiente y ninguno puede leer los
recursos del otro. Es exactamente el caso de `schema-local.sql`: varias copias
que hasta ahora nadie comparaba, asi que un cambio en una sola no fallaba hasta
ver al usuario con un PDF distinto según el cliente que abriera.

Que verifica:

1. Los cinco PNG existen en los recursos de los TRES clientes.
2. Son byte a byte el mismo dibujo (mismo SHA-256) en los tres.
3. Son PNG validos y cuadrados (un icono no cuadrado descuadra la burbuja).
4. Los tres catalogos de extension -> icono dicen lo MISMO (se leen las fuentes:
   `IconoArchivo.java`, `transversal/iconos_archivo.py`, `IconosArchivo.cs`).
5. Ningun catalogo apunta a un PNG que no exista.

Codigo 0 si todo cuadra, 1 si algo no.
"""
from __future__ import annotations

import hashlib
import re
import struct
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

RAIZ = Path(__file__).resolve().parent.parent
MONOREPOS = RAIZ / "universidad-mensajeria"

#: Los tres sitios donde vive una copia, con el nombre del cliente para el aviso.
COPIAS = {
    "JavaFX": MONOREPOS / "client/client-app/src/main/resources/iconos",
    "PySide6": MONOREPOS / "client-python/ui/iconos",
    "WPF": MONOREPOS / "client-dotnet/Iconos",
}

#: Fuente y patron de la tabla extension -> icono de cada cliente.
CATALOGOS = {
    "JavaFX": (MONOREPOS / "client/client-app/src/main/java/universidad/mensajeria/"
               "cliente/transversal/utilerias/IconoArchivo.java",
               r'Map\.entry\("([a-z0-9]+)",\s*"([a-z0-9]+)"\)'),
    "PySide6": (MONOREPOS / "client-python/transversal/iconos_archivo.py",
                r'"([a-z0-9]+)":\s*"([a-z0-9]+)",'),
    "WPF": (MONOREPOS / "client-dotnet/Transversal/IconosArchivo.cs",
            r'\["([a-z0-9]+)"\]\s*=\s*"([a-z0-9]+)",'),
}

FALLOS: list[str] = []


def fallo(mensaje: str) -> None:
    FALLOS.append(mensaje)
    print(f"  FALLA   {mensaje}")


def ok(mensaje: str) -> None:
    print(f"  OK      {mensaje}")


def leer_catalogo(cliente: str) -> dict[str, str]:
    ruta, patron = CATALOGOS[cliente]
    if not ruta.exists():
        fallo(f"{cliente}: no encuentro {ruta.relative_to(RAIZ)}")
        return {}
    texto = ruta.read_text(encoding="utf-8")
    mapa = {extension: icono
            for extension, icono in re.findall(patron, texto, flags=re.S)}
    if not mapa:
        fallo(f"{cliente}: la tabla extension -> icono quedo vacia "
              f"({ruta.relative_to(RAIZ)})")
    return mapa


def medir_png(ruta: Path) -> tuple[int, int] | None:
    """(ancho, alto) leidos del IHDR, o None si no es un PNG."""
    try:
        datos = ruta.read_bytes()
    except OSError as error:
        fallo(f"no se pudo leer {ruta}: {error}")
        return None
    if datos[:8] != b"\x89PNG\r\n\x1a\n":
        fallo(f"{ruta.relative_to(RAIZ)} no es un PNG")
        return None
    ancho, alto = struct.unpack(">II", datos[16:24])
    return ancho, alto


def main() -> int:
    print("=" * 72)
    print("Iconos por tipo de archivo - los mismos en los 3 clientes")
    print("=" * 72)
    print()

    catalogos = {cliente: leer_catalogo(cliente) for cliente in CATALOGOS}

    print(">>> Los tres catalogos dicen lo mismo")
    referencia_cliente, referencia = next(iter(catalogos.items()))
    for cliente, mapa in catalogos.items():
        if mapa == referencia:
            continue
        solo_aqui = {ext: icono for ext, icono in mapa.items()
                     if referencia.get(ext) != icono}
        solo_alli = {ext: icono for ext, icono in referencia.items()
                     if mapa.get(ext) != icono}
        fallo(f"{cliente} y {referencia_cliente} no coinciden: "
              f"{cliente} dice {solo_aqui or '{}'}, "
              f"{referencia_cliente} dice {solo_alli or '{}'}")
    if not FALLOS:
        ok(f"{len(referencia)} extensiones, mismas en JavaFX, PySide6 y WPF")
    print()

    print(">>> Cada catalogo apunta a iconos que existen")
    iconos = sorted(set(referencia.values()))
    for cliente, mapa in catalogos.items():
        for icono in sorted(set(mapa.values())):
            ruta = COPIAS[cliente] / f"{icono}.png"
            if not ruta.exists():
                fallo(f"{cliente}: falta {ruta.relative_to(RAIZ)} "
                      f"(lo pide la extension "
                      f"{next(e for e, i in mapa.items() if i == icono)})")
    if not FALLOS:
        print(f"  OK      {len(iconos)} dibujos presentes en los 3 clientes "
              f"({', '.join(iconos)})")
    print()

    print(">>> El mismo dibujo, byte a byte, en los tres")
    for icono in iconos:
        huellas: dict[str, str] = {}
        for cliente, carpeta in COPIAS.items():
            ruta = carpeta / f"{icono}.png"
            if not ruta.exists():
                continue
            huellas[cliente] = hashlib.sha256(ruta.read_bytes()).hexdigest()
        if len(set(huellas.values())) > 1:
            fallo(f"{icono}.png no es el mismo archivo en los 3 clientes: "
                  f"{ {c: h[:12] for c, h in huellas.items()} }")
            continue
        medidas = medir_png(COPIAS["JavaFX"] / f"{icono}.png")
        if medidas is None:
            continue
        ancho, alto = medidas
        if ancho != alto:
            fallo(f"{icono}.png mide {ancho}x{alto}: un icono no cuadrado "
                  "descuadra la burbuja")
        elif len(huellas) == len(COPIAS):
            ok(f"{icono}.png  {ancho}x{alto}  sha256 {next(iter(huellas.values()))[:12]}")
    print()

    print("=" * 72)
    if FALLOS:
        print(f"{len(FALLOS)} fallo(s).")
        return 1
    print("Todo en verde.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
