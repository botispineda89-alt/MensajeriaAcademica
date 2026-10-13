using System;
using System.IO;
using System.Linq;
using Mensajeria.Componentes;
using Mensajeria.Store;
using Xunit;

namespace Mensajeria.Tests
{
    /// <summary>
    /// UIX-24: la respuesta se ve como en WhatsApp (bloque citado) y la lista
    /// de contactos muestra el ultimo mensaje.
    ///
    /// El texto que viaja por la red NO cambia: sigue siendo el prefijo de
    /// texto ordinario que comparten Java y Python. Aqui solo se comprueba que
    /// la vista lo separa y que el preview sale bien.
    /// </summary>
    public class PreviewYCitaTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly LocalStore _store;
        private readonly HistorialLocal _historial;

        public PreviewYCitaTests()
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

        private void Guardar(string id, string origen, string destino, string tipo,
            string contenido, string fecha, int enviado = 0, string nombre = null,
            string caption = null) =>
            _historial.UpsertMensaje(id, origen, destino, tipo, contenido, null,
                null, null, nombre, null, null, fecha, enviado, 1, "ENVIADO",
                null, null, caption);

        [Fact]
        public void ElPreviewTomaElUltimoMensajeDeCadaConversacion()
        {
            Guardar("a1", "A001", "B002", "MENSAJE_TEXTO", "hola", "2026-10-10T10:00:00", 1);
            Guardar("a2", "A001", "B002", "MENSAJE_TEXTO", "como estas", "2026-10-10T10:05:00", 1);
            Guardar("b1", "C003", "A001", "MENSAJE_TEXTO", "buenos dias", "2026-10-10T10:01:00");

            var previews = _historial.UltimoMensajePorContacto("A001");

            // Lo mas reciente de B002, no el primero.
            Assert.Equal("como estas", previews["B002"].Texto);
            Assert.True(previews["B002"].Propio);
            Assert.Equal("buenos dias", previews["C003"].Texto);
            Assert.False(previews["C003"].Propio);
        }

        [Fact]
        public void ElPreviewDeUnaImagenMuestraNombreYPieYNoLosBytes()
        {
            Guardar("i1", "B002", "A001", "MENSAJE_IMAGEN", null, "2026-10-10T10:00:00",
                nombre: "foto.png", caption: "mira el atardecer");

            var previews = _historial.UltimoMensajePorContacto("A001");

            // Texto idéntico al de Java (HistorialLocal.leerPreview, R1). Antes
            // .NET decía "[Imagen] <pie>" y sin nombre de archivo, así que dos
            // fotos distintas se veían igual en la lista.
            Assert.Equal("[imagen] foto.png — mira el atardecer", previews["B002"].Texto);
        }

        [Fact]
        public void ElPreviewDeUnArchivoSinPieMuestraElNombre()
        {
            Guardar("x1", "B002", "A001", "MENSAJE_ARCHIVO", null, "2026-10-10T10:00:00",
                nombre: "informe.pdf");

            Assert.Equal("[archivo] informe.pdf",
                _historial.UltimoMensajePorContacto("A001")["B002"].Texto);
        }

        [Fact]
        public void ElPreviewNoSeRompeConFilasSinId()
        {
            using (var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dbPath}"))
            {
                c.Open();
                using var insertar = c.CreateCommand();
                insertar.CommandText = @"
                INSERT INTO historial_local
                    (id_mensaje, origen, destino, tipo, contenido, fecha_envio, enviado, estado)
                VALUES (NULL, 'A001', 'B002', 'MENSAJE_TEXTO', 'basura',
                        '2026-10-10T11:00:00', 0, 'LEIDO')";
                insertar.ExecuteNonQuery();
            }
            Guardar("ok1", "A001", "B002", "MENSAJE_TEXTO", "el bueno", "2026-10-10T10:00:00", 1);

            // La fila sin id es mas reciente: si contara, el preview seria "basura".
            var previews = _historial.UltimoMensajePorContacto("A001");

            Assert.Equal("el bueno", previews["B002"].Texto);
        }

        [Fact]
        public void ElPreviewIgnoraLasConversacionesAjenas()
        {
            Guardar("a1", "A001", "B002", "MENSAJE_TEXTO", "mio", "2026-10-10T10:00:00", 1);
            Guardar("z1", "X999", "Y888", "MENSAJE_TEXTO", "de otros", "2026-10-10T10:00:00");

            var previews = _historial.UltimoMensajePorContacto("A001");

            Assert.True(previews.ContainsKey("B002"));
            Assert.False(previews.ContainsKey("X999"));
            Assert.False(previews.ContainsKey("Y888"));
        }
    }
}
