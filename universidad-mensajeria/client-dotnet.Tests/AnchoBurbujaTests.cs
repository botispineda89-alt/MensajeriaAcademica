using Mensajeria.Transversal;
using Xunit;

namespace Mensajeria.Tests;

public class AnchoBurbujaTests
{
    [Theory]
    [InlineData(400, 280)]
    [InlineData(600, 390)]
    [InlineData(2000, 720)]
    [InlineData(double.NaN, double.NaN)]
    [InlineData(double.PositiveInfinity, double.NaN)]
    [InlineData(0, double.NaN)]
    [InlineData(-10, double.NaN)]
    public void LimitaElAnchoDeLaBurbujaAlAnchoDisponible(double ancho, double esperado)
    {
        // Sin medida fiable NO se fija ancho (NaN), para no desbordar el panel.
        if (double.IsNaN(esperado)) Assert.True(double.IsNaN(AnchoBurbuja.Calcular(ancho)));
        else Assert.Equal(esperado, AnchoBurbuja.Calcular(ancho));
    }

    [Fact]
    public void MuestraMensajesPropiosYEntrantesEnLadosOpuestos()
    {
        Assert.Equal(LadoBurbuja.Derecha, BurbujaChat.Alineacion(esPropio: true));
        Assert.Equal(LadoBurbuja.Izquierda, BurbujaChat.Alineacion(esPropio: false));
        Assert.True(BurbujaChat.DebeMostrar(esCabeceraDia: false));
        Assert.False(BurbujaChat.DebeMostrar(esCabeceraDia: true));
    }
}
