"""Fachada Python con red falsa y SQLite real: online, offline, reintento."""
import base64
import os
import json
import tempfile
import unittest

from PySide6.QtCore import QBuffer, QIODevice
from PySide6.QtGui import QImage

from fachada.fachada_cliente import FachadaCliente, DESTINO_BROADCAST
from store.database import Database


def _png_bytes():
    """PNG mínimo de 3x2, para las respuestas de descarga de la red falsa."""
    imagen = QImage(3, 2, QImage.Format.Format_ARGB32)
    buffer = QBuffer()
    buffer.open(QIODevice.OpenModeFlag.WriteOnly)
    imagen.save(buffer, "PNG")
    return bytes(buffer.data())


class RedFalsa:
    """Guion programable: responde ACK o simula corte."""

    def __init__(self, *args, **kwargs):
        self.conectado = True
        self.enviados_async = []
        self.solicitudes = []
        self.caida = False
        self.historial_remoto = []

    def conectar(self):
        self.conectado = True

    def desconectar(self):
        self.conectado = False

    def pedir(self, msg, timeout=15):
        self.solicitudes.append(msg)
        if self.caida or not self.conectado:
            raise ConnectionError("corte simulado")
        tipo = msg.get('tipo')
        if tipo == 'LOGIN':
            return {'tipo': 'LOGIN_RESPUESTA', 'id': msg.get('id'), 'exito': True}
        if tipo == 'MENSAJE_TEXTO':
            if msg.get('destinatario') == 'ZZZ':
                return {'tipo': 'ERROR', 'id': msg.get('id'),
                        'mensajeError': 'destinatario inexistente'}
            return {'tipo': 'ACK', 'id': msg.get('id'), 'exito': True,
                    'hashSha256': 'h', 'numCaracteres': 4, 'numPalabras': 1}
        if tipo == 'MENSAJE_IMAGEN':
            return {'tipo': 'IMAGE_FILTERED_RESULT', 'id': msg.get('id'),
                    'exito': True, 'hashSha256': 'hi',
                    'etapasFiltrado': '[{"etapa":"01_grises.png","ms":12,"ruta":"srv/privado/01_grises.png"}]',
                    'archivoId': '7'}
        if tipo == 'HISTORIAL_REQ':
            return {'tipo': 'HISTORIAL_PAGE', 'id': msg.get('id'),
                    'exito': True, 'contenido': json.dumps(self.historial_remoto),
                    'totalPaginas': 1}
        if tipo == 'BROADCAST':
            return {'tipo': 'ACK', 'id': msg.get('id'), 'exito': True}
        if tipo == 'LISTAR_CONECTADOS':
            return {'tipo': 'LISTAR_CONECTADOS_RESPUESTA', 'id': msg.get('id'),
                    'usuariosConectados': ['A001', 'B002']}
        if tipo == 'DESCARGAR_ARCHIVO':
            return {'tipo': 'DESCARGAR_ARCHIVO_RESPUESTA', 'id': msg.get('id'),
                    'nombreArchivo': 'descarga.png',
                    'contenidoImagen': base64.b64encode(_png_bytes()).decode('ascii')}
        return {'tipo': 'ERROR', 'mensajeError': 'no soportado en falsa'}

    def enviar_async(self, msg):
        self.enviados_async.append(msg)


class TestFachada(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.NamedTemporaryFile(suffix=".db", delete=False)
        tmp.close()
        self.addCleanup(os.unlink, tmp.name)
        esquema = os.path.normpath(os.path.join(
            os.path.dirname(os.path.abspath(__file__)),
            '..', '..', 'common-protocol', 'dto', 'src', 'main', 'resources',
            'schema-local.sql'))
        db = Database("sqlite:///" + tmp.name, esquema_path=esquema)
        self.red = RedFalsa()
        self.avisos = []
        self.presencias = []
        self.cierres = []
        self.f = FachadaCliente(on_mensaje_recibido_ui=self.avisos.append,
                                on_presencia_ui=self.presencias.append,
                                on_cierre_ui=self.cierres.append,
                                db=db, conexion=self.red)
        self.addCleanup(self.f.desconectar)
        self.f.login("A001", "Secreta123")

    def test_enviar_texto_online_guarda_metadatos(self):
        self.assertTrue(self.f.enviar_texto("B002", "hola"))
        conv = self.f.obtener_conversacion("B002")
        self.assertEqual(len(conv), 1)
        self.assertEqual(conv[0]["contenido"], "hola")
        # Paridad Java: el ACK no trae hash; conteos locales.
        self.assertEqual(conv[0]["hash_sha256"], "")
        self.assertEqual(conv[0]["num_caracteres"], 4)
        self.assertEqual(conv[0]["num_palabras"], 1)
        self.assertEqual(self.f.pendientes(), [])

    def test_error_servidor_se_eleva_no_se_traga(self):
        with self.assertRaises(RuntimeError):
            self.f.enviar_texto("ZZZ", "hola")

    def test_enviar_offline_encola_y_reintenta_con_ack(self):
        self.red.caida = True
        self.assertFalse(self.f.enviar_texto("B002", "luego"))
        self.assertEqual(len(self.f.pendientes()), 1)
        self.red.caida = False
        self.assertEqual(self.f.reintentar_pendientes(), 1)
        self.assertEqual(self.f.pendientes(), [])

    def test_broadcast_recibido_en_conversacion(self):
        # Entrante de difusion del servidor: destino = codigo propio (leccion Fase 5).
        self.f._handle_unsolicited_message(
            {'tipo': 'BROADCAST', 'id': 'bc-1', 'remitente': 'B002',
             'contenido': 'hola a todos', 'fechaHora': '2026-01-01T10:00:00'})
        conv = self.f.obtener_conversacion("B002")
        self.assertEqual(len(conv), 1)
        self.assertEqual(conv[0]["contenido"], "hola a todos")
        self.assertEqual(len(self.avisos), 1)

    def test_imagen_recibida_no_guarda_base64(self):
        etapas = '[{"etapa":"01_grises.png","ms":12,"ruta":"srv/privado/01_grises.png"}]'
        self.f._handle_unsolicited_message(
            {'tipo': 'MENSAJE_IMAGEN', 'id': 'im-1', 'remitente': 'B002',
             'contenidoImagen': 'QUJD', 'nombreArchivo': 'f.png',
             'hashSha256': 'hi', 'fechaHora': '2026-01-01T10:00:00',
             'etapasFiltrado': etapas})
        conv = self.f.obtener_conversacion("B002")
        self.assertIsNone(conv[0]["contenido"])
        self.assertEqual(conv[0]["etapas_filtrado"], etapas)

    def test_eco_de_mensaje_propio_conserva_el_destinatario_real(self):
        self.f._handle_unsolicited_message({
            "tipo": "MENSAJE_TEXTO", "id": "eco-propio",
            "remitente": "A001", "destinatario": "B002",
            "contenido": "enviado desde otra sesión",
            "fechaHora": "2026-01-01T10:00:00",
        })

        fila = self.f.historial.obtener_por_id("eco-propio")
        self.assertEqual("A001", fila["origen"])
        self.assertEqual("B002", fila["destino"])
        self.assertEqual("enviado desde otra sesión", fila["contenido"])
        self.assertEqual("eco-propio", self.avisos[-1]["id"])

    def test_envio_imagen_persiste_etapas_del_ack(self):
        etapas = '[{"etapa":"01_grises.png","ms":12,"ruta":"srv/privado/01_grises.png"}]'
        with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as archivo:
            archivo.write(b"imagen")
            ruta = archivo.name
        self.addCleanup(lambda: os.path.exists(ruta) and os.unlink(ruta))

        self.assertTrue(self.f.enviar_imagen("B002", ruta, id_msg="ack-etapas"))

        self.assertEqual(
            self.f.historial.obtener_por_id("ack-etapas")["etapas_filtrado"], etapas)

    def test_historial_remoto_persiste_etapas_filtrado(self):
        etapas = '[{"etapa":"01_grises.png","ms":12,"ruta":"srv/privado/01_grises.png"}]'
        self.red.historial_remoto = [{
            "id": "remoto-etapas", "remitente": "B002", "destinatario": "A001",
            "tipo": "MENSAJE_IMAGEN", "nombreArchivo": "foto.png",
            "fechaEnvio": "2026-01-01T10:00:00", "etapasFiltrado": etapas,
        }]

        self.f.traer_historial("B002", 0)

        self.assertEqual(
            self.f.historial.obtener_por_id("remoto-etapas")["etapas_filtrado"], etapas)

    def test_caption_imagen_se_envia_y_persiste_localmente(self):
        with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as archivo:
            archivo.write(b"imagen")
            ruta = archivo.name
        self.addCleanup(lambda: os.path.exists(ruta) and os.unlink(ruta))

        self.assertTrue(self.f.enviar_imagen(
            "B002", ruta, id_msg="caption-out", caption="Foto del aula"))

        solicitud = next(
            msg for msg in reversed(self.red.solicitudes)
            if msg.get("id") == "caption-out")
        self.assertEqual("Foto del aula", solicitud["contenido"])
        self.assertEqual(
            "Foto del aula",
            self.f.historial.obtener_por_id("caption-out")["caption"])

    def test_caption_imagen_se_conserva_en_entrada_y_en_historial_remoto(self):
        self.f._handle_unsolicited_message({
            "tipo": "MENSAJE_IMAGEN", "id": "caption-in",
            "remitente": "B002", "contenido": "Foto recibida",
            "contenidoImagen": "QUJD", "nombreArchivo": "foto.png",
            "fechaHora": "2026-01-01T10:00:00",
        })
        self.assertEqual(
            "Foto recibida",
            self.f.historial.obtener_por_id("caption-in")["caption"])

        self.red.historial_remoto = [{
            "id": "caption-remote", "remitente": "B002",
            "destinatario": "A001", "tipo": "MENSAJE_IMAGEN",
            "nombreArchivo": "foto.png", "contenido": "Foto del historial",
            "fechaEnvio": "2026-01-01T10:00:00",
        }]
        self.f.traer_historial("B002", 0)

        self.assertEqual(
            "Foto del historial",
            self.f.historial.obtener_por_id("caption-remote")["caption"])

    def test_caption_imagen_se_conserva_al_reintentar_sin_red(self):
        with tempfile.NamedTemporaryFile(suffix=".png", delete=False) as archivo:
            archivo.write(b"imagen")
            ruta = archivo.name
        self.addCleanup(lambda: os.path.exists(ruta) and os.unlink(ruta))
        self.red.caida = True

        self.assertFalse(self.f.enviar_imagen(
            "B002", ruta, id_msg="caption-pending", caption="Aula"))
        pendiente = self.f.pendientes()[0]
        self.assertEqual("Aula", pendiente["contenido"])
        self.red.caida = False

        self.assertEqual(1, self.f.reintentar_pendientes())

        solicitud = next(
            msg for msg in reversed(self.red.solicitudes)
            if msg.get("id") == "caption-pending")
        self.assertEqual("Aula", solicitud["contenido"])

    def test_marcar_leidos_encola_y_envia_confirmacion(self):
        self.f._handle_unsolicited_message(
            {'tipo': 'MENSAJE_TEXTO', 'id': 'msg-leer', 'remitente': 'B002',
             'contenido': 'mensaje', 'fechaHora': '2026-01-01T10:00:00'})

        self.assertEqual(self.f.historial.ids_no_leidos('A001', 'B002'), ['msg-leer'])
        self.assertEqual(self.f.marcar_leidos('B002'), 1)
        self.assertEqual(self.f.historial.ids_no_leidos('A001', 'B002'), [])
        self.assertEqual(self.red.enviados_async[-1]['tipo'], 'MENSAJE_LEIDO')
        self.assertEqual(self.red.enviados_async[-1]['contenido'], 'msg-leer')

    def test_acuse_leido_actualiza_estado_persistido(self):
        self.f.enviar_texto("B002", "verificación", id_msg="acuse-persistido")

        self.f._handle_unsolicited_message({
            "tipo": "MENSAJE_LEIDO", "contenido": "acuse-persistido",
        })

        self.assertEqual(
            "LEIDO", self.f.historial.obtener_por_id("acuse-persistido")["estado"])

    def test_presencia_push_actualiza_callback_y_cierre_limpia_sesion(self):
        self.f._handle_unsolicited_message(
            {'tipo': 'PRESENCIA', 'codigo': 'B002', 'contenido': 'conectado',
             'usuariosConectados': ['A001', 'B002']})
        self.assertEqual(len(self.presencias), 1)

        self.f._handle_cierre({'tipo': 'CLOSE_NOTICE', 'mensajeError': 'inactividad'})
        self.assertIsNone(self.f.codigo_actual)
        self.assertEqual(self.cierres[0]['mensajeError'], 'inactividad')

    def test_persistencia_pre_envio_y_estado_enviado(self):
        # Enviar texto con id específico
        mid = "msg-persistencia-1"
        exito = self.f.enviar_texto("B002", "mensaje persistido", id_msg=mid)
        self.assertTrue(exito)
        msg = self.f.historial.obtener_por_id(mid)
        self.assertIsNotNone(msg)
        self.assertEqual(msg["estado"], "ENVIADO")

    def test_push_entregado_y_leido_mismo_id(self):
        mid = "msg-push-status"
        self.f.enviar_texto("B002", "mensaje prueba status", id_msg=mid)
        self.assertEqual(self.f.historial.obtener_por_id(mid)["estado"], "ENVIADO")

        # Push ENTREGADO
        self.f._handle_unsolicited_message({
            "tipo": "MENSAJE_ENTREGADO",
            "contenido": mid,
            "remitente": "B002"
        })
        self.assertEqual(self.f.historial.obtener_por_id(mid)["estado"], "ENTREGADO")

        # Push LEIDO
        self.f._handle_unsolicited_message({
            "tipo": "MENSAJE_LEIDO",
            "contenido": mid,
            "remitente": "B002"
        })
        self.assertEqual(self.f.historial.obtener_por_id(mid)["estado"], "LEIDO")

        # Intento de push desordenado a ENTREGADO no debe degradar LEIDO
        self.f._handle_unsolicited_message({
            "tipo": "MENSAJE_ENTREGADO",
            "contenido": mid,
            "remitente": "B002"
        })
        self.assertEqual(self.f.historial.obtener_por_id(mid)["estado"], "LEIDO")

    def test_fijar_cuenta_aisla_base_datos(self):
        self.f.fijar_cuenta("C003")
        self.assertEqual(self.f.codigo_actual, "C003")
        self.assertEqual(self.f.db.cuenta, "C003")
        # El historial de C003 debe estar vacío
        self.assertEqual(self.f.obtener_conversacion("B002"), [])

    def test_imagen_gigante_se_rechaza_antes_de_guardar(self):
        # Invariante 2 + regla .NET (CatalogoAdjuntos.MaxBytesImagenTramaUnica):
        # una imagen que no cabe en una trama de 16 MiB se rechaza ANTES de
        # abrirla por el socket; el constant es del módulo coordinador.
        from unittest.mock import patch

        from fachada.servicios import coordinador_mensajeria

        with tempfile.NamedTemporaryFile(suffix=".jpg", delete=False) as archivo:
            archivo.write(b"x" * 64)
            ruta = archivo.name
        self.addCleanup(os.unlink, ruta)
        with patch.object(coordinador_mensajeria, "IMAGEN_MAX_BYTES", 8):
            with self.assertRaises(ValueError):
                self.f.enviar_imagen("B002", ruta, id_msg="img-grande")
        # Nada se persistió ni se mandó.
        self.assertIsNone(self.f.historial.obtener_por_id("img-grande"))
        self.assertFalse([s for s in self.red.solicitudes
                          if s.get("tipo") == "MENSAJE_IMAGEN"])
        self.assertEqual([], self.f.pendientes())

    def test_obtener_imagen_repara_cache_de_cero_bytes(self):
        # §7 del plan de paridad: una fila con ruta_archivo apuntando a un
        # archivo de 0 bytes (descarga cortada) debe RE-DESCARGAR por
        # archivoId y no devolver el veneno para siempre.
        from pathlib import Path as _Path

        temporal = tempfile.TemporaryDirectory()
        self.addCleanup(temporal.cleanup)
        veneno = _Path(temporal.name) / "veneno.webp"
        veneno.write_bytes(b"")
        self.f.historial.guardar_mensaje(
            "img-veneno", "A001", "B002", "MENSAJE_IMAGEN", "[Imagen]",
            "2026-01-01T10:00:00", archivo_id="77", ruta_archivo=str(veneno))

        destino = _Path(temporal.name) / "descarga.png"
        ruta = self.f.obtener_imagen("img-veneno", str(destino))

        self.assertTrue(_Path(ruta).stat().st_size > 0, "re-descargada")
        self.assertFalse(QImage(ruta).isNull())

    def test_reintento_de_imagen_gigante_no_tumba_la_conexion(self):
        # El bug del smoke: un pendiente de imagen de ~35 MB se reenviaba como
        # trama de 47 MB en cada login; el servidor cortaba la conexión y el
        # cliente quedaba en bucle de reconexión. El reintento debe marcarla
        # ERROR, sacarla de la cola y SEGUIR con el resto de la cola.
        from unittest.mock import patch

        from fachada.servicios import coordinador_mensajeria

        id_img = "img-veneno"
        self.f.historial.guardar_mensaje(
            id_img, "A001", "B002", "MENSAJE_IMAGEN", "[Imagen]",
            "2026-01-01T10:00:00", estado="PENDIENTE")
        self.f.historial.guardar_pendiente(
            id_img, "MENSAJE_IMAGEN", "A001", "B002", "", "pesada.jpg",
            b"123456789", "2026-01-01T10:00:00")
        self.f.historial.guardar_mensaje(
            "texto-ok", "A001", "B002", "MENSAJE_TEXTO", "luego",
            "2026-01-01T10:01:00", estado="PENDIENTE")
        self.f.historial.guardar_pendiente(
            "texto-ok", "MENSAJE_TEXTO", "A001", "B002", "luego", None, None,
            "2026-01-01T10:01:00")

        with patch.object(coordinador_mensajeria, "IMAGEN_MAX_BYTES", 8):
            enviados = self.f.reintentar_pendientes()

        self.assertEqual(1, enviados)
        self.assertEqual([], self.f.pendientes())
        self.assertEqual("ERROR", self.f.historial.obtener_por_id(id_img)["estado"])
        self.assertEqual("ENVIADO", self.f.historial.obtener_por_id("texto-ok")["estado"])
        self.assertFalse([s for s in self.red.solicitudes
                          if s.get("tipo") == "MENSAJE_IMAGEN"])


if __name__ == '__main__':
    unittest.main()
