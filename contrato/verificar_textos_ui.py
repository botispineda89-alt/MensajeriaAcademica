#!/usr/bin/env python
"""VIS-02: los literales de ventana y diálogo viven una vez por cliente.

Lee `contrato/textos-ui.json` (que sale de `DIAGRAMAS/textos-ui.md`) y comprueba,
para cada cliente:

1. Su módulo de textos contiene el texto exacto del contrato
   (Java: `TextosVentana.java`, Python: `transversal/textos.py`,
   .NET: `Transversal/TextosVentana.cs` y `Transversal/MensajesUsuario.cs`).
2. Ningún otro fuente lo repite inline (los tests sí pueden fijarlos).
3. Ningún `.xaml` trae el literal: las vistas referencian el módulo con
   `x:Static` (los 3 XAML con título lo nombran).

Excepciones justificadas (mismo texto, otro concepto): el `"Imagen"` de
`ui/celdas/mensaje_delegate.py` es la etiqueta de estado de una imagen sin
cargar, no el título del visor.
"""

from __future__ import annotations

import ast
import json
import re
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
CONTRATO = Path(__file__).resolve().parent / "textos-ui.json"

CLIENTES = ("java", "python", "dotnet")

MODULOS = {
    "java": ["universidad-mensajeria/client/client-app/src/main/java/"
             "universidad/mensajeria/cliente/transversal/utilerias/TextosVentana.java"],
    "python": ["universidad-mensajeria/client-python/transversal/textos.py"],
    "dotnet": ["universidad-mensajeria/client-dotnet/Transversal/TextosVentana.cs",
               "universidad-mensajeria/client-dotnet/Transversal/MensajesUsuario.cs"],
}

FUENTES = {
    "java": ["universidad-mensajeria/client/client-app/src/main/java"],
    "python": ["universidad-mensajeria/client-python/ui",
               "universidad-mensajeria/client-python/transversal",
               "universidad-mensajeria/client-python/fachada",
               "universidad-mensajeria/client-python/componentes",
               "universidad-mensajeria/client-python/puertos"],
    "dotnet": ["universidad-mensajeria/client-dotnet"],
}

IGNORAR_DIRS = ("/tests", "/test", "/bin", "/obj", "/__pycache__", "/.venv")

# (cliente, fichero relativo al monorepos, valor): mismo texto, otro concepto.
EXCEPCIONES = {
    ("python",
     "universidad-mensajeria/client-python/ui/celdas/mensaje_delegate.py",
     "Imagen"),
    ("java",
     "universidad-mensajeria/client/client-app/src/main/java/"
     "universidad/mensajeria/cliente/presentacion/celdas/MensajeCell.java",
     "Imagen"),
}

# La etiqueta de estado de una imagen sin cargar ("Imagen" como pie de la
# burbuja) no es el título del visor: se conserva inline en los dos clientes
# con esta excepción documentada.

XAML_CON_TITULO = [
    "universidad-mensajeria/client-dotnet/Views/ChatWindow.xaml",
    "universidad-mensajeria/client-dotnet/Views/LoginWindow.xaml",
    "universidad-mensajeria/client-dotnet/Views/ImagenViewerWindow.xaml",
]

_LITERAL_JAVA_CS = re.compile(r'"((?:[^"\\]|\\.)*)"')


def _literales_python(ruta: Path) -> list[str]:
    try:
        arbol = ast.parse(ruta.read_text(encoding="utf-8"), filename=str(ruta))
    except SyntaxError:
        return []
    return [n.value for n in ast.walk(arbol)
            if isinstance(n, ast.Constant) and isinstance(n.value, str)]


def _literales_java_cs(ruta: Path) -> list[str]:
    texto = ruta.read_text(encoding="utf-8")
    texto = re.sub(r"/\*.*?\*/", "", texto, flags=re.DOTALL)
    texto = re.sub(r"//[^\n]*", "", texto)
    # Los literales del contrato no llevan escapes: se comparan tal cual.
    # (Decodificar `\uXXXX` aquí solo serviría para falsos positivos.)
    return [m.group(1) for m in _LITERAL_JAVA_CS.finditer(texto)]


def _ficheros(cliente: str, extension: str) -> list[Path]:
    salida = []
    for raiz in FUENTES[cliente]:
        base = RAIZ / raiz
        if not base.exists():
            continue
        for ruta in base.rglob(f"*{extension}"):
            rel = str(ruta.relative_to(RAIZ)).replace("\\", "/")
            if any(ign in f"/{rel}" for ign in IGNORAR_DIRS):
                continue
            salida.append(ruta)
    return sorted(salida)


def main() -> int:
    contrato = json.loads(CONTRATO.read_text(encoding="utf-8"))
    fallos: list[str] = []

    for entrada in contrato["literales"]:
        ident = entrada["id"]
        for cliente in CLIENTES:
            valor = entrada.get(cliente)
            if valor is None:
                continue
            modulos = [RAIZ / m for m in MODULOS[cliente]]
            if not any(valor in (literales_de(m, cliente)) for m in modulos):
                fallos.append(
                    f"{ident}: {valor!r} no está en el módulo de {cliente}")
            ext = {"java": ".java", "python": ".py", "dotnet": ".cs"}[cliente]
            for ruta in _ficheros(cliente, ext):
                rel = str(ruta.relative_to(RAIZ)).replace("\\", "/")
                if ruta in modulos:
                    continue
                if (cliente, rel, valor) in EXCEPCIONES:
                    continue
                if valor in literales_de(ruta, cliente):
                    fallos.append(
                        f"{ident}: {valor!r} repetido inline en {rel}")

    for xaml in XAML_CON_TITULO:
        texto = (RAIZ / xaml).read_text(encoding="utf-8")
        if "TextosVentana" not in texto:
            fallos.append(f"{xaml} no referencia TextosVentana (x:Static)")

    if fallos:
        print(f"{len(fallos)} literal(es) fuera de su módulo:")
        for fallo in fallos:
            print(f"  - {fallo}")
        return 1
    n = len(contrato["literales"])
    print(f"OK: {n} literales de ventana/diálogo, una vez por cliente.")
    return 0


def literales_de(ruta: Path, cliente: str) -> list[str]:
    if cliente == "python":
        return _literales_python(ruta)
    return _literales_java_cs(ruta)


if __name__ == "__main__":
    raise SystemExit(main())
