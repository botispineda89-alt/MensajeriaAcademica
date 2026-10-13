using Mensajeria.Transversal;
using Xunit;

namespace Mensajeria.Tests;

/// <summary>
/// UIX-28: los botones de guardar. Antes había uno genérico ("⬇ Guardar") al
/// final de la burbuja, y salía incluso en los mensajes de texto. Ahora son
/// <b>dos distintos</b> —uno para imagen y otro para archivo— y solo aparecen
/// donde hay algo que guardar.
/// </summary>
public class BotonGuardarTests
{
    [Fact]
    public void UnaImagenSoloMuestraElBotonDeGuardarImagen()
    {
        Assert.True(BotonGuardar.LlevaBoton(BotonGuardar.TIPO_IMAGEN));
        Assert.True(BotonGuardar.EsImagen(BotonGuardar.TIPO_IMAGEN));
        Assert.False(BotonGuardar.EsArchivo(BotonGuardar.TIPO_IMAGEN));
        Assert.Equal("⬇ Guardar imagen", BotonGuardar.Texto(BotonGuardar.TIPO_IMAGEN));
    }

    [Fact]
    public void UnArchivoSoloMuestraElBotonDeGuardarArchivo()
    {
        Assert.True(BotonGuardar.LlevaBoton(BotonGuardar.TIPO_ARCHIVO));
        Assert.False(BotonGuardar.EsImagen(BotonGuardar.TIPO_ARCHIVO));
        Assert.True(BotonGuardar.EsArchivo(BotonGuardar.TIPO_ARCHIVO));
        Assert.Equal("⬇ Guardar archivo", BotonGuardar.Texto(BotonGuardar.TIPO_ARCHIVO));
    }

    [Theory]
    [InlineData("MENSAJE_TEXTO")]
    [InlineData("TEXTO")]
    [InlineData("BROADCAST")]
    [InlineData("SYNC_LOGIN")]
    [InlineData("")]
    [InlineData(null)]
    public void UnMensajeQueNoLlevaNadaNoTieneBoton(string? tipo)
    {
        // Esto es lo que se quejó el usuario: el botón salía para todos.
        Assert.False(BotonGuardar.LlevaBoton(tipo));
        Assert.Equal("", BotonGuardar.Texto(tipo));
    }

    [Theory]
    [InlineData("IMAGEN", BotonGuardar.TIPO_IMAGEN)]
    [InlineData("imagen", BotonGuardar.TIPO_IMAGEN)]
    [InlineData("ARCHIVO", BotonGuardar.TIPO_ARCHIVO)]
    [InlineData("  archivo  ", BotonGuardar.TIPO_ARCHIVO)]
    public void ElTipoDelServidorYNombreDeLaPropiaParteSeNormalizan(string entrada, string esperado)
    {
        // El servidor manda "IMAGEN"/"ARCHIVO" y el cliente guarda
        // "MENSAJE_IMAGEN"/"MENSAJE_ARCHIVO". Si no se normalizan, una imagen
        // recién llegada se pintaba sin botón de guardar.
        Assert.Equal(esperado, BotonGuardar.Normalizar(entrada));
        Assert.True(BotonGuardar.LlevaBoton(entrada));
    }
}