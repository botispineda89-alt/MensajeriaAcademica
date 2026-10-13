"""UIX-03: el mismo contrato que FormatoFecha (Java) y FormatoFecha (.NET)."""
import datetime
import unittest

from transversal.fechas import agrupar, a_utc, ahora_utc, clave_dia, etiqueta_dia, hora_corta


class TestFechas(unittest.TestCase):
    def test_hoy_muestra_solo_la_hora(self):
        hoy = datetime.datetime.now().replace(hour=10, minute=5, second=0)
        texto = hoy.strftime("%Y-%m-%d %H:%M:%S")

        self.assertEqual("10:05", hora_corta(texto))
        self.assertEqual("Hoy", etiqueta_dia(texto))

    def test_ayer_se_marca_como_ayer(self):
        ayer = datetime.datetime.now() - datetime.timedelta(days=1)
        texto = ayer.replace(hour=9, minute=30).strftime("%Y-%m-%dT%H:%M:%S")

        self.assertEqual("Ayer", etiqueta_dia(texto))
        self.assertIn("09:30", hora_corta(texto))

    def test_hace_una_semana_muestra_dia_y_mes(self):
        hace = datetime.datetime.now() - datetime.timedelta(days=7)
        texto = hace.replace(hour=8, minute=0).strftime("%Y-%m-%d %H:%M:%S")

        self.assertRegex(hora_corta(texto), r"\d{2}-\d{2} 08:00")
        self.assertRegex(etiqueta_dia(texto), r"\d{2}-\d{2}")

    def test_acepta_los_dos_formatos_de_origen(self):
        hoy = datetime.date.today().isoformat()
        self.assertEqual("10:00", hora_corta(f"{hoy}T10:00:00"))
        self.assertEqual("10:00", hora_corta(f"{hoy} 10:00:00"))

    def test_lo_ilegible_no_rompe_la_fila(self):
        self.assertEqual("", hora_corta(None))
        self.assertEqual("", hora_corta(""))
        self.assertEqual("", hora_corta("ayer a las tres"))
        self.assertEqual("", etiqueta_dia("basura"))
        self.assertEqual("", clave_dia(None))

    def test_clave_dia_agrupa_por_fecha(self):
        hoy = datetime.datetime.now().strftime("%Y-%m-%dT%H:%M:%S")
        ayer = (datetime.datetime.now() - datetime.timedelta(days=1)
                ).strftime("%Y-%m-%d %H:%M:%S")

        self.assertEqual(datetime.date.today().isoformat(), clave_dia(hoy))
        self.assertEqual(
            (datetime.date.today() - datetime.timedelta(days=1)).isoformat(),
            clave_dia(ayer))

    def test_fecha_con_zona_se_convierte_a_hora_local(self):
        # La SQLite la comparten los clientes por cuenta y .NET escribe UTC
        # con sufijo Z (DateTime.ToString("O") aka round-trip).
        local = datetime.datetime.now().replace(microsecond=0)
        en_utc = local.astimezone(datetime.timezone.utc)
        zoned = en_utc.strftime("%Y-%m-%dT%H:%M:%S") + ".1234567Z"

        self.assertEqual(local.strftime("%H:%M"), hora_corta(zoned))
        self.assertEqual(local.date().isoformat(), clave_dia(zoned))

    def test_fecha_con_desplazamiento_se_convierte_a_hora_local(self):
        local = datetime.datetime.now().replace(microsecond=0)
        zona = datetime.timezone(datetime.timedelta(hours=-5))
        zoned = local.astimezone(zona).isoformat()

        self.assertEqual(local.strftime("%H:%M"), hora_corta(zoned))
        self.assertEqual(local.date().isoformat(), clave_dia(zoned))

    def test_agrupar_mezcla_naive_y_con_zona_sin_romper(self):
        # Regresión: mezclar una fila de .NET (aware) con una local (naive)
        # lanzaba "can't subtract offset-naive and offset-aware datetimes".
        base = datetime.datetime.now().replace(microsecond=0)
        cercano = base + datetime.timedelta(minutes=2)
        naive = base.strftime("%Y-%m-%d %H:%M:%S")
        zoned = cercano.astimezone(datetime.timezone.utc
                                   ).strftime("%Y-%m-%dT%H:%M:%SZ")

        anterior = {"origen": "A001", "fecha_envio": naive}
        actual = {"origen": "A001", "fecha_envio": zoned}
        self.assertTrue(agrupar(anterior, actual))
        self.assertTrue(agrupar(actual, anterior))


class TestAUtc(unittest.TestCase):
    """Réplica del contrato de `FormatoFecha.AUtc` (.NET, UIX-26)."""

    def test_hora_local_sin_zona_se_guarda_en_utc_con_z(self):
        # El servidor manda LocalDateTime.now() (local sin zona) y la fila
        # debe quedar en el formato "O" de .NET: 7 decimales + Z.
        local = datetime.datetime(2026, 10, 11, 1, 37, 11, 697890)
        esperado = local.astimezone().astimezone(datetime.timezone.utc)
        texto = a_utc(local.strftime("%Y-%m-%dT%H:%M:%S.%f"))

        self.assertTrue(texto.endswith("Z"))
        self.assertEqual(7, len(texto.split(".")[-1]) - 1)  # 7 decimales
        self.assertEqual(
            esperado.strftime("%Y-%m-%dT%H:%M:%S")
            + f".{esperado.microsecond:07d}Z",
            texto)

    def test_fecha_ya_en_utc_queda_identica_byte_a_byte(self):
        cruda = "2026-10-11T04:52:26.1862985Z"
        self.assertEqual(cruda, a_utc(cruda))
        # Ni las variantes con menos decimales se retocan: lo que ya trae Z
        # se guarda tal cual (la migración tampoco las debe tocar dos veces).
        self.assertEqual("2026-10-11T04:52:26.186298Z",
                         a_utc("2026-10-11T04:52:26.186298Z"))

    def test_ilegible_se_devuelve_tal_cual_y_vacia_da_vacia(self):
        self.assertEqual("", a_utc(None))
        self.assertEqual("", a_utc(""))
        self.assertEqual("ayer a las tres", a_utc("ayer a las tres"))

    def test_ahora_utc_usa_el_mismo_formato(self):
        texto = ahora_utc()
        self.assertRegex(
            texto, r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$")

    def test_orden_de_texto_respeta_el_orden_real_tras_normalizar(self):
        # LA REGRESIÓN DEL SMOKE: la base por cuenta la comparten Python y
        # .NET. .NET guardó a las 23:52 locales ("...23:52...Z" en UTC) y
        # Python a la 01:37 locales del día siguiente; ordenado como TEXTO,
        # la fila de Python quedaba DETRÁS y salía de la página 0.
        dotnet = "2026-10-11T04:52:26.1862985Z"          # 23:52 local del 10-10
        python_local = "2026-10-11T01:37:11.697890"      # 01:37 local del 11-10
        normalizada = a_utc(python_local)

        self.assertGreater(normalizada, dotnet,
                           "el mensaje propio nuevo debe quedar primero")
        self.assertEqual(normalizada,
                         sorted([dotnet, normalizada], reverse=True)[0])


if __name__ == "__main__":
    unittest.main()
