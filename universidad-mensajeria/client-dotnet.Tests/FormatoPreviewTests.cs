using Mensajeria.Transversal;
using Xunit;

namespace Mensajeria.Tests;

/// <summary>
/// UIX-26: el preview de la lista de conversaciones y la insignia de no leídos.
/// El texto tiene que coincidir carácter a carácter con el cliente Java
/// (<c>TextoCliente.previoConversacion</c> y <c>UsuarioCell</c>).
/// </summary>
public class FormatoPreviewTests
{
    [Fact]
    public void SinMensajeDiceSinMensajesYNoAnteponeElPrefijo()
    {
        Assert.Equal("Sin mensajes aún", FormatoPreview.Preview(null, false));
        Assert.Equal("Sin mensajes aún", FormatoPreview.Preview("", false));
        Assert.Equal("Sin mensajes aún", FormatoPreview.Preview("   ", true));
    }

    [Fact]
    public void LoMioAnteponeElPrefijoYLoDeOtroNo()
    {
        Assert.Equal("Tú: hola", FormatoPreview.Preview("hola", true));
        Assert.Equal("hola", FormatoPreview.Preview("hola", false));
        Assert.Equal("Tú: ", FormatoPreview.PREFIJO_PROPIO);
    }

    [Fact]
    public void ColapsaSaltosYEspacios()
    {
        Assert.Equal("hola mundo", FormatoPreview.Preview("  hola\n  mundo ", false));
        Assert.Equal("Tú: hola mundo", FormatoPreview.Preview("hola\tmundo", true));
    }

    [Fact]
    public void TruncaElMensajeAntesDeAnteponerElPrefijo()
    {
        string largo = new string('a', 80);

        string salida = FormatoPreview.Preview(largo, false);

        Assert.Equal(FormatoPreview.CORTE, salida.Length);
        Assert.EndsWith("…", salida);
        Assert.Equal("Tú: " + salida, FormatoPreview.Preview(largo, true));
    }

    [Fact]
    public void LaInsigniaEsElNumeroPeladoComoEnJava()
    {
        // Java: String.valueOf(item.noLeidos()). Los paréntesis los tenía
        // solo .NET y quedaban descuadrados con el otro cliente.
        Assert.Equal("", FormatoPreview.Insignia(0));
        Assert.Equal("3", FormatoPreview.Insignia(3));
        Assert.Equal("127", FormatoPreview.Insignia(127));
    }
}