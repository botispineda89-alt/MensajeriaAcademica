"""Descarga fragmentada: lo que el servidor manda cuando el archivo no cabe.

Espejo de ``ColectorFragmentos`` de .NET. Sin esto, la primera trama completaba
la espera de la respuesta y las PARTES se descartaban: el archivo se escribia de
0 bytes sin error, que es la peor forma de fallar.
"""

import base64
import threading
import unittest

from fachada.servicios.colector_fragmentos import ColectorFragmentos

UN_MIB = 1024 * 1024


class PruebasColectorFragmentos(unittest.TestCase):

    def _secuencia(self, id_="peticion-1", partes=3, bloque=b"x" * (UN_MIB // 2)):
        """Tramas tal como las arma el servidor (DescargaCaso)."""
        inicio = {"tipo": "ARCHIVO_INICIO", "id": id_, "totalPartes": partes,
                  "nombreArchivo": "grande.bin", "tamanoArchivo": len(bloque) * partes}
        medio = [{"tipo": "ARCHIVO_PARTE", "id": id_, "indiceParte": i,
                  "contenidoImagen": base64.b64encode(bytes([i]) * len(bloque)).decode()}
                 for i in range(partes)]
        fin = {"tipo": "ARCHIVO_FIN", "id": id_, "hashSha256": "abc"}
        return inicio, medio, fin

    def test_reensambla_las_partes_en_orden(self):
        inicio, medio, fin = self._secuencia()
        colector = ColectorFragmentos("peticion-1")

        for trama in (inicio, *medio, fin):
            colector.alimentar(trama)

        self.assertTrue(colector.esta_listo())
        datos = colector.bytes()
        self.assertEqual(len(datos), 3 * (UN_MIB // 2))
        # Orden: la parte 0, luego la 1, luego la 2.
        self.assertEqual(datos[0], 0)
        self.assertEqual(datos[UN_MIB // 2], 1)
        self.assertEqual(datos[UN_MIB], 2)

    def test_las_partes_llegan_desordenadas_y_se_ordenan(self):
        inicio, medio, fin = self._secuencia()
        colector = ColectorFragmentos("peticion-1")

        for trama in (inicio, fin, *reversed(medio)):
            colector.alimentar(trama)

        self.assertEqual(colector.bytes()[:3], b"\x00\x00\x00")

    def test_ignora_tramas_de_otra_descarga(self):
        inicio, medio, fin = self._secuencia(id_="peticion-1")
        colector = ColectorFragmentos("peticion-1")

        colector.alimentar({"tipo": "ARCHIVO_PARTE", "id": "peticion-2",
                            "indiceParte": 0, "contenidoImagen": "WFhY"})
        colector.alimentar(inicio)
        for trama in medio:
            colector.alimentar(trama)
        colector.alimentar(fin)

        self.assertEqual(len(colector.bytes()), 3 * (UN_MIB // 2),
                         "la parte ajena no se coló")

    def test_un_error_del_servidor_no_deja_esperar_para_seguida(self):
        colector = ColectorFragmentos("peticion-1")
        colector.alimentar({"tipo": "ARCHIVO_INICIO", "id": "peticion-1", "totalPartes": 2})
        colector.alimentar({"tipo": "ERROR", "id": "peticion-1",
                            "mensajeError": "archivo no encontrado"})

        self.assertTrue(colector.esta_listo(), "no debe quedarse esperando")
        with self.assertRaises(RuntimeError) as error:
            colector.bytes()
        self.assertIn("archivo no encontrado", str(error.exception))

    def test_faltan_partes_no_se_da_por_buena_la_descarga(self):
        inicio, medio, fin = self._secuencia()
        colector = ColectorFragmentos("peticion-1")
        for trama in (inicio, medio[0], fin):
            colector.alimentar(trama)

        with self.assertRaises(RuntimeError) as error:
            colector.bytes()
        self.assertIn("1 de 3", str(error.exception))

    def test_una_parte_corrupta_no_se_traga_en_silencio(self):
        inicio, medio, fin = self._secuencia()
        colector = ColectorFragmentos("peticion-1")
        for trama in (inicio, medio[0]):
            colector.alimentar(trama)
        colector.alimentar({"tipo": "ARCHIVO_PARTE", "id": "peticion-1",
                            "indiceParte": 1, "contenidoImagen": "no-es-base64!!"})
        colector.alimentar(fin)

        with self.assertRaises(RuntimeError) as error:
            colector.bytes()
        self.assertIn("ilegible", str(error.exception))

    def test_sin_fin_no_termina_nunca(self):
        inicio, medio, _ = self._secuencia()
        colector = ColectorFragmentos("peticion-1")
        for trama in (inicio, *medio):
            colector.alimentar(trama)

        self.assertFalse(colector.esta_listo())
        terminado = threading.Event()

        def esperar():
            try:
                colector.bytes(espera_seg=0.2)
            except TimeoutError:
                terminado.set()

        hilo = threading.Thread(target=esperar, daemon=True)
        hilo.start()
        self.assertTrue(terminado.wait(5), "bytes() tiene que salir por timeout")


if __name__ == "__main__":
    unittest.main()