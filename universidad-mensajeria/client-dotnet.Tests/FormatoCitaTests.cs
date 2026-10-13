using System.Globalization;
using Mensajeria.Transversal;
using Xunit;

namespace Mensajeria.Tests;

/// <summary>
/// UIX-24: contrato del prefijo de respuesta. Replica los casos de
/// <c>FormatoRespuesta.java</c> y <c>MensajeCell.extraerCita</c>.
/// </summary>
public class FormatoCitaTests
{
    [Fact]
    public void ColapsaSaltosYAnadeElSeparador()
    {
        // El extracto va en UNA linea: si no, el "\n\n" del cuerpo se pega.
        string prefijo = FormatoCita.Prefijo("Ana", "  linea1\nlinea2  ", esImagen: false, null);
        Assert.Equal("Respuesta a Ana: \"linea1 linea2\"\n\n", prefijo);
    }

    [Fact]
    public void ImagenUsaLaEtiquetaImagen()
    {
        string prefijo = FormatoCita.Prefijo("Ana", null, esImagen: true, "foto  a.png");
        Assert.Equal("Respuesta a Ana: [Imagen: foto a.png]\n\n", prefijo);
    }

    [Fact]
    public void SinContenidoUsaMensaje()
    {
        Assert.Equal("Respuesta a Ana: \"mensaje\"\n\n",
            FormatoCita.Prefijo("Ana", "", esImagen: false, null));
        Assert.Equal("Respuesta a Ana: [Imagen: imagen]\n\n",
            FormatoCita.Prefijo("Ana", null, esImagen: true, ""));
    }

    [Fact]
    public void RecortaA120ElementosSinPartirSurrogates()
    {
        string prefijo = FormatoCita.Prefijo("X", new string('a', 200), esImagen: false, null);
        Assert.Equal("Respuesta a X: \"" + new string('a', 119) + "…\"\n\n", prefijo);

        // 200 caracteres fuera del BMP (cada uno 2 chars de UTF-16) no deben
        // partirse por la mitad: el recorte cuenta unidades de texto.
        string emoji = string.Concat(Enumerable.Repeat("😀", 200));
        string conEmoji = FormatoCita.Prefijo("X", emoji, esImagen: false, null);
        string entre = conEmoji["Respuesta a X: \"".Length..conEmoji.IndexOf("\"\n\n", StringComparison.Ordinal)];
        Assert.Equal(FormatoCita.MaxCita, StringInfo.ParseCombiningCharacters(entre).Length);
        Assert.EndsWith("…", entre);
    }

    [Fact]
    public void SeparaLaCitaCanonica()
    {
        FormatoCita.Separar("Respuesta a Ana: \"hola\"\n\nrespuesta", out string cita, out string cuerpo);
        Assert.Equal("Respuesta a Ana: \"hola\"", cita);
        Assert.Equal("respuesta", cuerpo);
    }

    [Fact]
    public void SinPrefijoEsTodoCuerpo()
    {
        FormatoCita.Separar("hola\n\nmundo", out string cita, out string cuerpo);
        Assert.Equal("", cita);
        Assert.Equal("hola\n\nmundo", cuerpo);
    }

    [Fact]
    public void ToleraFinalesDeLineaDeWindows()
    {
        FormatoCita.Separar("Respuesta a Ana: \"hola\"\r\n\r\nrespuesta", out string cita, out string cuerpo);
        Assert.Equal("Respuesta a Ana: \"hola\"", cita);
        Assert.Equal("respuesta", cuerpo);
    }

    [Fact]
    public void UnPrefijoDemasiadoLargoNoEsCita()
    {
        string crudo = "Respuesta a Ana: \"" + new string('a', 350) + "\"\n\ncuerpo";
        FormatoCita.Separar(crudo, out string cita, out string cuerpo);
        Assert.Equal("", cita);
        Assert.Equal(crudo, cuerpo);
    }

    [Fact]
    public void IdaYVueltaConservaLaReferenciaYElCuerpo()
    {
        string prefijo = FormatoCita.Prefijo("Ana [1]", "original\ncon salto", esImagen: false, null);
        FormatoCita.Separar(prefijo + "mi respuesta", out string cita, out string cuerpo);
        Assert.StartsWith(FormatoCita.Referencia, cita);
        Assert.Contains("original con salto", cita);
        Assert.Equal("mi respuesta", cuerpo);
    }

    [Fact]
    public void SoloLaCabeceraNoTieneCuerpo()
    {
        string prefijo = FormatoCita.Prefijo("Ana", "original", esImagen: false, null);
        FormatoCita.Separar(prefijo, out string cita, out string cuerpo);
        Assert.NotEqual("", cita);
        Assert.Equal("", cuerpo);
    }
}
