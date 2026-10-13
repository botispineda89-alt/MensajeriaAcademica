using System;
using System.IO;
using System.Linq;
using Mensajeria.Componentes;
using Mensajeria.Store;
using Xunit;

namespace Mensajeria.Tests;

/// <summary>
/// UIX-26: migración de las fechas ya guardadas.
/// <para>
/// Corregir solo las capturas nuevas no arregla nada de lo que ya está en la
/// base: las conversaciones existentes seguirían mostrando "Tú:" hasta que
/// llegara un mensaje nuevo. La migración convierte a UTC lo que ya está, y no
/// toca dos veces lo que ya traía "Z".
/// </para>
/// </summary>
public class MigracionFechasTests : IDisposable
{
    private readonly string _ruta;
    private LocalStore _store;

    public MigracionFechasTests()
    {
        _ruta = Path.Combine(Path.GetTempPath(), $"mig_{Guid.NewGuid():N}.db");
        _store = new LocalStore(_ruta);
    }

    public void Dispose()
    {
        _store.Dispose();
        try { if (File.Exists(_ruta)) File.Delete(_ruta); } catch (IOException) { }
    }

    private void Insertar(string id, string fecha, string contenido = "hola")
    {
        using var cmd = _store.Connection!.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO historial_local
                (id_mensaje, origen, destino, tipo, contenido, fecha_envio)
            VALUES ($id, 'OTRO', 'YO', 'MENSAJE_TEXTO', $c, $f)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$c", contenido);
        cmd.Parameters.AddWithValue("$f", fecha);
        cmd.ExecuteNonQuery();
    }

    private string Fecha(string id)
    {
        // Conexión propia: aquí se relee DESPUÉS de reabrir el store, que es
        // cuando corre la migración.
        using var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_ruta}");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT fecha_envio FROM historial_local WHERE id_mensaje = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return (string)cmd.ExecuteScalar()!;
    }

    /// <summary>Cierra y reabre el store: eso es lo que hace el cliente al arrancar.</summary>
    private void Reiniciar()
    {
        _store.Dispose();
        _store = new LocalStore(_ruta);
    }

    [Fact]
    public void LoQueVinoDelServidorPasaAUtc()
    {
        Insertar("a", "2026-10-10T22:14:51");

        Reiniciar();

        string fecha = Fecha("a");
        Assert.EndsWith("Z", fecha);
        Assert.StartsWith("2026-10-11T03:14", fecha);
    }

    [Fact]
    public void LoQueYaEstabaEnUtcNoSeTocaDosVeces()
    {
        string original = "2026-10-11T03:14:51.0000000Z";
        Insertar("b", original);

        Reiniciar();

        Assert.Equal(original, Fecha("b"));
    }

    [Fact]
    public void TrasLaMigracionLoRecienteOrdenaComoToca()
    {
        // El caso real: yo mando y un minuto después me llega. El preview tiene
        // que quedarse con el del otro, sin "Tú:".
        DateTime ahora = DateTime.Now;
        Insertar("mio", ahora.ToUniversalTime().ToString("O"), "escrito por mi");
        Insertar("llegado", ahora.AddMinutes(1)
            .ToString("yyyy-MM-ddTHH:mm:ss"), "me lo ha enviado otro");

        Reiniciar();

        var previews = new HistorialLocal(_store).UltimoMensajePorContacto("YO");
        Assert.Equal("me lo ha enviado otro", previews["OTRO"].Texto);
        Assert.False(previews["OTRO"].Propio);
    }
}