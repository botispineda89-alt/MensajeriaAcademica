using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mensajeria.Componentes;
using Mensajeria.Fachada;
using Mensajeria.Store;
using Xunit;

namespace Mensajeria.Tests
{
    /// <summary>
    /// Regresion del sintoma "No se pudo sincronizar el historial".
    ///
    /// El servidor devuelve la pagina con los nombres cortos (TEXTO / IMAGEN /
    /// ARCHIVO) porque es lo que espera el enum TipoContenido de la entidad
    /// JPA. El cliente normaliza al nombre con prefijo del contrato. Antes de
    /// esa equivalencia el mapeo reventaba con ArgumentException y el chat se
    /// quedaba vacio, con un aviso que decia "Revisa los datos" aunque el
    /// problema no eran los datos.
    ///
    /// El JSON de este test es una recorte real de lo que contesta el servidor.
    /// </summary>
    public class SincronizacionHistorialTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly LocalStore _store;
        private readonly HistorialLocal _historial;

        public SincronizacionHistorialTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
            _store = new LocalStore(_dbPath);
            _historial = new HistorialLocal(_store);
        }

        public void Dispose()
        {
            _store.Dispose();
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
        }

        /// <summary>Recorte fiel de la respuesta HISTORIAL_PAGE del servidor.</summary>
        private const string PaginaDelServidor = @"[
          {""id"":""t1"",""remitente"":""160004726"",""destinatario"":""160004729"",
           ""tipo"":""TEXTO"",""contenido"":""hola"",""numCaracteres"":4,""numPalabras"":1,
           ""hashSha256"":""h1"",""fechaEnvio"":""2026-10-10T17:05:10""},
          {""id"":""i1"",""remitente"":""160004729"",""destinatario"":""160004726"",
           ""tipo"":""IMAGEN"",""contenido"":""mira el atardecer"",""nombreArchivo"":""foto.png"",
           ""tamanoArchivo"":2048,""archivoId"":""77"",""fechaEnvio"":""2026-10-10T17:06:00""},
          {""id"":""a1"",""remitente"":""160004726"",""destinatario"":""160004729"",
           ""tipo"":""ARCHIVO"",""contenido"":null,""nombreArchivo"":""informe.pdf"",
           ""archivoId"":""88"",""fechaEnvio"":""2026-10-10T17:07:00""}
        ]";

        [Fact]
        public void LaPaginaDelServidorSeSincronizaSinReventarYConLosTiposNormalizados()
        {
            var mapeador = new MapeadorDatosServidor();

            var mensajes = mapeador.MapearPaginaHistorial(PaginaDelServidor, "160004726");

            Assert.Equal(3, mensajes.Count);
            Assert.Equal(new[] { "MENSAJE_TEXTO", "MENSAJE_IMAGEN", "MENSAJE_ARCHIVO" },
                mensajes.Select(m => m.Tipo).ToArray());
            Assert.Equal("hola", mensajes[0].Contenido);
            // El pie de la imagen se conserva; el cuerpo del adjunto no.
            Assert.Equal("mira el atardecer", mensajes[1].Caption);
            Assert.Null(mensajes[1].Contenido);
            Assert.Null(mensajes[2].Caption);
            Assert.True(mensajes[1].TieneCaption());
        }

        [Fact]
        public void TrasSincronizarElTextoSeLeeDeVueltaYLaImagenTraeSuPie()
        {
            var mapeador = new MapeadorDatosServidor();
            foreach (var mensaje in mapeador.MapearPaginaHistorial(PaginaDelServidor, "160004726"))
            {
                _historial.UpsertMensaje(
                    mensaje.IdMensaje!, mensaje.Origen!, mensaje.Destino!, mensaje.Tipo!,
                    mensaje.Contenido, mensaje.HashSha256, mensaje.NumCaracteres,
                    mensaje.NumPalabras, mensaje.NombreArchivo, mensaje.RutaArchivo,
                    mensaje.TamanoArchivo, mensaje.FechaEnvio!,
                    mensaje.Enviado ? 1 : 0, mensaje.Descargado ? 1 : 0,
                    mensaje.Estado!, mensaje.ArchivoId, mensaje.EtapasFiltrado, mensaje.Caption);
            }

            var pagina = _historial.PaginaConversacion("160004726", "160004729", 50, 0);

            Assert.Equal(3, pagina.Count);
            var texto = pagina.Single(m => (string)m["id"] == "t1");
            Assert.Equal("hola", texto["contenido"]);

            var imagen = pagina.Single(m => (string)m["id"] == "i1");
            Assert.Equal("MENSAJE_IMAGEN", imagen["tipo"]);
            Assert.Equal("mira el atardecer", imagen["caption"]);
            // El cuerpo del adjunto no se arrastra al listado.
            Assert.False(imagen.ContainsKey("contenido"));
        }

        [Fact]
        public void LaMigracionAnadeLaColumnaDelPieEnUnaBaseVieja()
        {
            // Ruta propia: el store del constructor ya creo el esquema nuevo.
            string rutaVieja = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
            try
            {
                // Base creada por una version anterior: sin columna caption.
                using (var vieja = new Microsoft.Data.Sqlite.SqliteConnection(
                    $"Data Source={rutaVieja}"))
                {
                    vieja.Open();
                    using var crear = vieja.CreateCommand();
                    crear.CommandText = @"
                    CREATE TABLE historial_local (
                        id_mensaje TEXT PRIMARY KEY, origen TEXT NOT NULL, destino TEXT NOT NULL,
                        tipo TEXT NOT NULL, contenido TEXT, hash_sha256 TEXT,
                        num_caracteres INTEGER, num_palabras INTEGER, nombre_archivo TEXT,
                        ruta_archivo TEXT, tamano_archivo INTEGER, fecha_envio TEXT NOT NULL,
                        enviado INTEGER NOT NULL DEFAULT 0, descargado INTEGER NOT NULL DEFAULT 0,
                        estado TEXT NOT NULL DEFAULT 'ENVIADO', archivo_id TEXT, etapas_filtrado TEXT)";
                    crear.ExecuteNonQuery();
                }

                // Abrirla debe migrar y dejar el pie utilizable.
                using var store = new LocalStore(rutaVieja);
                new HistorialLocal(store).UpsertMensaje("m9", "A", "B", "MENSAJE_IMAGEN",
                    null, null, null, null, "f.png", null, 10L, "2026-10-10T10:00:00",
                    1, 1, "ENVIADO", null, null, "pie de la foto");

                var fila = Assert.Single(new HistorialLocal(store)
                    .PaginaConversacion("A", "B", 50, 0));
                Assert.Equal("pie de la foto", fila["caption"]);
            }
            finally
            {
                try { if (File.Exists(rutaVieja)) File.Delete(rutaVieja); } catch { }
            }
        }
        [Fact]
        public void UnaFilaSinIdNoTumbaLaConversacion()
        {
            // Regresion: una base con filas heredadas de id_mensaje NULL hacia
            // que PaginaConversacion reventase con "The data is NULL at ordinal
            // 0" y el chat se quedase entero en negro. Una fila sin id no
            // identifica ningun mensaje: se descarta y el resto se lee.
            using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}"))
            {
                c.Open();
                using var insertar = c.CreateCommand();
                insertar.CommandText = @"
                INSERT INTO historial_local
                    (id_mensaje, origen, destino, tipo, contenido, fecha_envio, enviado, estado)
                VALUES (NULL, 'A001', 'B002', 'MENSAJE_TEXTO', 'basura heredada',
                        '2026-10-01T05:00:00', 0, 'LEIDO')";
                insertar.ExecuteNonQuery();
            }

            _historial.UpsertMensaje("bueno", "A001", "B002", "MENSAJE_TEXTO",
                "hola", null, null, null, null, null, null, "2026-10-10T10:00:00",
                1, 1, "ENVIADO");

            var pagina = _historial.PaginaConversacion("A001", "B002", 50, 0);

            // La fila sin id se ignora; la buena se ve.
            var unica = Assert.Single(pagina);
            Assert.Equal("bueno", unica["id"]);
            Assert.Equal("hola", unica["contenido"]);
        }

        [Fact]
        public void LaBusquegaYLosNoLeidosTambienAguantanLasFilasSinId()
        {
            using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}"))
            {
                c.Open();
                using var insertar = c.CreateCommand();
                insertar.CommandText = @"
                INSERT INTO historial_local
                    (id_mensaje, origen, destino, tipo, contenido, fecha_envio, enviado, estado)
                VALUES (NULL, 'A001', 'B002', 'MENSAJE_TEXTO', 'basura',
                        '2026-10-01T05:00:00', 0, 'PENDIENTE')";
                insertar.ExecuteNonQuery();
            }

            // Estas cuatro leen la misma tabla. Con GetString(0) a pelo, la
            // fila sin id las hacia reventar a la vez.
            Assert.Empty(_historial.BuscarEnConversacion("A001", "B002", "basura", 10));
            Assert.Empty(_historial.IdsNoLeidos("B002", "A001"));
            // Estas dos agregan por origen, no por id: la fila sigue contando,
            // pero ya sin lanzar.
            Assert.NotNull(_historial.UltimosMensajesPorContacto("B002"));
            Assert.Equal(1, _historial.ContarNoLeidosPorContacto("B002").Count);
        }
    }
}
