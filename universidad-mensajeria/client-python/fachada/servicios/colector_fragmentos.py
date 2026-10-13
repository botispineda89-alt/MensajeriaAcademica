"""Reensambla la descarga fragmentada de un archivo grande.

El servidor no manda un archivo de mas de 1 MiB en una trama (invariante 2:
16 MiB), sino una secuencia ``ARCHIVO_INICIO`` + N x ``ARCHIVO_PARTE`` +
``ARCHIVO_FIN`` correlacionada por el ``id`` de la peticion. Sin esto, la primera
trama completaba la espera de la respuesta y el resto se descartaba: el archivo
quedaba de 0 bytes y sin error, que es la peor forma de fallar.

Espejo de ``ColectorFragmentos`` de .NET y del recolector de Java.
"""

import base64
import binascii
import threading


class ColectorFragmentos:
    def __init__(self, id_esperado=None):
        self.id_esperado = str(id_esperado) if id_esperado else None
        self._partes = {}
        self._total_partes = None
        self._evento = threading.Event()
        self._error = None

    def fijar_id(self, nuevo_id):
        self.id_esperado = str(nuevo_id)

    def corresponde(self, msg):
        """True si esta trama es de la descarga que este colector espera."""
        if self.id_esperado is None:
            return False
        return str(msg.get("id") or "") == self.id_esperado

    def alimentar(self, msg):
        """Procesa una trama de la secuencia. Silencioso si no es de esta."""
        if not self.corresponde(msg):
            return
        tipo = msg.get("tipo")
        if tipo == "ARCHIVO_INICIO":
            self._total_partes = msg.get("totalPartes")
            self._evento.clear()
        elif tipo == "ARCHIVO_PARTE":
            indice = msg.get("indiceParte")
            if indice is None:
                self._fallar("parte sin indice")
                return
            try:
                self._partes[int(indice)] = base64.b64decode(
                    msg.get("contenidoImagen") or "", validate=True)
            except (binascii.Error, ValueError) as error:
                self._fallar(f"parte {indice} ilegible: {error}")
                return
        elif tipo == "ARCHIVO_FIN":
            self._evento.set()
        elif tipo == "ERROR":
            self._fallar(msg.get("mensajeError") or "el servidor rechazó la descarga")

    def _fallar(self, motivo):
        self._error = motivo
        self._evento.set()

    def esta_listo(self):
        return self._evento.is_set()

    def bytes(self, espera_seg=30.0):
        """Bloquea hasta que la secuencia termine. Lanza si hubo error o tiempo."""
        if not self._evento.wait(espera_seg):
            raise TimeoutError("la descarga fragmentada no terminó a tiempo")
        if self._error:
            raise RuntimeError(f"no se pudo descargar el archivo: {self._error}")
        if self._total_partes and len(self._partes) < int(self._total_partes):
            raise RuntimeError(
                f"llegaron {len(self._partes)} de {self._total_partes} partes")
        return b"".join(self._partes[indice]
                        for indice in sorted(self._partes))