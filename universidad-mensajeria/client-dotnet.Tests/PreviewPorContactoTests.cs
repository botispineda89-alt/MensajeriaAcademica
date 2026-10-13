using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Mensajeria.Componentes;
using Mensajeria.Store;
using Xunit;

namespace Mensajeria.Tests;

/// <summary>
/// UIX-26: el preview de la lista y la insignia de no leídos contra el esquema
/// real. Lo que se fixe aquí es la regla que se rompió: el prefijo "Tú: " sale
/// de <c>origen = yo</c>, no de la columna <c>enviado</c>.
/// </summary>
public class PreviewPorContactoTests : IDisposable
{
    private readonly string _ruta;
    private readonly LocalStore _store;

    public PreviewPorContactoTests()
    {
        _ruta = Path.Combine(Path.GetTempPath(), $"preview_{Guid.NewGuid():N}.db");
        _store = new LocalStore(_ruta);
    }

    public void Dispose()
    {
        _store.Dispose();
        try { if (File.Exists(_ruta)) File.Delete(_ruta); } catch (IOException) { }
    }

    private void Guardar(string id, string origen, string destino, string tipo,
        string? contenido, string? nombre = null, int enviado = 1, string? fecha = null)
    {
        using var cmd = _store.Connection!.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO historial_local
                (id_mensaje, origen, destino, tipo, contenido, hash_sha256,
                 num_caracteres, num_palabras, nombre_archivo, ruta_archivo,
                 tamano_archivo, fecha_envio, enviado, descargado, estado,
                 archivo_id, etapas_filtrado, caption)
            VALUES ($id, $o, $d, $t, $c, NULL, NULL, NULL, $n, NULL,
                    NULL, $f, $e, 0, 'ENTREGADO', NULL, NULL, NULL)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$o", origen);
        cmd.Parameters.AddWithValue("$d", destino);
        cmd.Parameters.AddWithValue("$t", tipo);
        cmd.Parameters.AddWithValue("$c", (object?)contenido ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", (object?)nombre ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$f", fecha ?? DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$e", enviado);
        cmd.ExecuteNonQuery();
    }

    private IReadOnlyDictionary<string, PreviewUltimoMensaje> Preview(string yo) =>
        new HistorialLocal(_store).UltimoMensajePorContacto(yo);

    [Fact]
    public void UnMensajeQueLlegaNoSeAnunciaComoPropio()
    {
        // ESTE es el bug: la columna `enviado` de las filas entrantes que escribe
        // el sincronizador de SYNC_LOGIN está a 1. Con ella, el preview de un
        // mensaje que LLEGA salía con "Tú: ".
        Guardar("m1", "OTRO", "YO", "MENSAJE_TEXTO", "hola", enviado: 1);

        var previews = Preview("YO");

        Assert.Equal("hola", previews["OTRO"].Texto);
        Assert.False(previews["OTRO"].Propio);
    }

    [Fact]
    public void UnMensajePropioSiSeAnunciaComoTal()
    {
        Guardar("m1", "YO", "OTRO", "MENSAJE_TEXTO", "yo lo mande", enviado: 1);

        var previews = Preview("YO");

        Assert.True(previews["OTRO"].Propio);
    }

    [Fact]
    public void GanaElMasRecienteYDaigualSiElRemitenteEsElMismo()
    {
        Guardar("m1", "OTRO", "YO", "MENSAJE_TEXTO", "primero",
            fecha: "2026-10-10T10:00:00");
        Guardar("m2", "YO", "OTRO", "MENSAJE_TEXTO", "segundo",
            fecha: "2026-10-10T11:00:00");

        var previews = Preview("YO");

        Assert.Equal("segundo", previews["OTRO"].Texto);
        Assert.True(previews["OTRO"].Propio);
    }

    [Fact]
    public void UnaImagenSeAnunciaConSuNombreYConElPieSiLoTiene()
    {
        Guardar("m1", "YO", "OTRO", "MENSAJE_IMAGEN", null, nombre: "foto.png");

        var previews = Preview("YO");

        // Sin nombre no hay nada que decir; con nombre, se ve de qué foto es.
        Assert.Equal("[imagen] foto.png", previews["OTRO"].Texto);
        Assert.True(previews["OTRO"].Propio);
    }

    [Fact]
    public void UnContactoSinMensajesNoAparece()
    {
        Guardar("m1", "YO", "OTRO", "MENSAJE_TEXTO", "hola");

        var previews = Preview("YO");

        Assert.False(previews.ContainsKey("NADIE"));
    }

    [Fact]
    public void LosMensajesDeSinCronizacionTambienCuentan()
    {
        // La sincronización de arranque es de donde venían las filas con
        // `enviado` mal; el preview no puede ignorarlas.
        Guardar("m1", "OTRO", "YO", "SYNC_LOGIN", "del servidor",
            fecha: "2026-10-10T10:00:00");
        Guardar("m2", "OTRO", "YO", "MENSAJE_TEXTO", "posterior",
            fecha: "2026-10-10T11:00:00");

        var previews = Preview("YO");

        Assert.Equal("posterior", previews["OTRO"].Texto);
        Assert.False(previews["OTRO"].Propio);
    }
}