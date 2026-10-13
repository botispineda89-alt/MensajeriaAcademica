using System;
using System.Globalization;
using Mensajeria.Transversal;
using Xunit;

namespace Mensajeria.Tests;

/// <summary>
/// UIX-26: las fechas tienen que quedar en el MISMO formato o el orden por
/// texto miente.
/// <para>
/// El servidor manda <c>LocalDateTime.now()</c> = hora local <b>sin zona</b>; lo
/// que envía este cliente es <b>UTC con Z</b>. Ordenando por <c>fecha_envio</c>
/// como texto, un mensaje que llegaba a las 22:14 quedaba por detrás de uno que
/// salía a las 22:14, y el preview marcaba siempre "Tú:".
/// </para>
/// </summary>
public class FormatoFechaNormalizadaTests
{
    [Fact]
    public void UnaFechaDelServidorSeConvierteAUtcYAsiOrdenaBien()
    {
        // El servidor nunca manda zona. Esta es la hora local; en UTC son las 03:14.
        string delServidor = "2026-10-10T22:14:51";

        string normalizada = FormatoFecha.AUtc(delServidor);

        Assert.EndsWith("Z", normalizada);
        Assert.StartsWith("2026-10-11T03:14", normalizada);
    }

    [Fact]
    public void LoQueLlegaDespuesDeLoQueSaleSeOrdenaDespues()
    {
        // La regresión exacta, sin fechas fijas para que no dependa del reloj.
        // Yo mando ahora; un minuto después me llega un mensaje del servidor.
        DateTime ahora = DateTime.Now;
        string mio = ahora.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        // El servidor lo manda en hora local y SIN zona.
        string recibido = FormatoFecha.AUtc(
            ahora.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));

        Assert.True(string.CompareOrdinal(recibido, mio) > 0,
            $"\"{recibido}\" debería ordenar DESPUÉS de \"{mio}\"");
    }

    [Fact]
    public void YAlRevesTambien()
    {
        // Y al revés: lo que sale ahora tiene que ir después de lo que llegó
        // hace un momento.
        DateTime ahora = DateTime.Now;
        string recibido = FormatoFecha.AUtc(
            ahora.AddMinutes(-1).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
        string mio = ahora.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        Assert.True(string.CompareOrdinal(mio, recibido) > 0,
            $"\"{mio}\" debería ordenar DESPUÉS de \"{recibido}\"");
    }

    [Fact]
    public void UnaFechaQueYaTraeZNoSeConvierteDosVeces()
    {
        string conZona = "2026-10-11T03:14:51.0000000Z";

        Assert.Equal(conZona, FormatoFecha.AUtc(conZona));
    }

    [Fact]
    public void UnDesfaseDistintoSigueOrdenando()
    {
        string antes = FormatoFecha.AUtc("2026-10-10T22:00:00");
        string despues = FormatoFecha.AUtc("2026-10-10T23:00:00");

        Assert.True(string.CompareOrdinal(antes, despues) < 0);
    }

    [Fact]
    public void UnaFechaIlegibleSeDevuelveTalCualParaNoPerderElMensaje()
    {
        Assert.Equal("basura", FormatoFecha.AUtc("basura"));
        Assert.Equal("", FormatoFecha.AUtc(null));
        Assert.Equal("", FormatoFecha.AUtc("   "));
    }

    [Fact]
    public void FollowWithUtcYConEspacioTambienSeNormalizan()
    {
        // SQLite devuelve a veces con espacio en vez de "T".
        Assert.EndsWith("Z", FormatoFecha.AUtc("2026-10-10 22:14:51"));
    }
}