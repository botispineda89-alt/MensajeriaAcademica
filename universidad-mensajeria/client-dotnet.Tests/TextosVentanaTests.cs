using Mensajeria.Transversal;
using Xunit;

namespace Mensajeria.Tests;

/// <summary>
/// P4 (2026-10-14): el título de la ventana de chat lleva la identidad de la
/// cuenta abierta y sale de la plantilla del contrato
/// (<c>contrato/textos-ui.json</c>, id <c>titulo_chat</c>). Antes decía
/// <c>Mensajería Académica | Chat</c>.
/// </summary>
public class TextosVentanaTests
{
    [Fact]
    public void ElTituloDelChatLlevaLaIdentidadDeLaCuenta()
    {
        Assert.Equal("Mensajería Académica | Ana Díaz [A001]",
            TextosVentana.TituloChatCon("Ana Díaz [A001]"));
        // Al arrancar solo se conoce el código: el título es válido antes del directorio.
        Assert.Equal("Mensajería Académica | A001",
            TextosVentana.TituloChatCon("A001"));
    }

    [Fact]
    public void SinIdentidadElTituloNuncaMuestraElMarcador()
    {
        // La constante es la plantilla exacta del contrato y por eso lleva
        // `{identidad}`; lo que se pinta en pantalla pasa siempre por TituloChatCon.
        Assert.Equal("Mensajería Académica | {identidad}", TextosVentana.TituloChat);
        Assert.Equal("Mensajería Académica", TextosVentana.TituloChatCon(null));
        Assert.Equal("Mensajería Académica", TextosVentana.TituloChatCon("   "));
    }
}
