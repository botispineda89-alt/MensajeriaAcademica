using Mensajeria.Transversal;
using Xunit;

namespace Mensajeria.Tests;

/// <summary>
/// UIX-26: el banner inline se cierra solo cuando el aviso es transitorio.
/// Un error que desaparece solo es un error que nadie llega a leer.
/// </summary>
public class EstadoAvisoInlineTests
{
    [Fact]
    public void TodosLosAvisosSeOcultanSolosCadaUnoConSuPlazo()
    {
        var estado = new EstadoAvisoInline();

        // Un aviso permanente acaba tapando la conversación. Todos se van, pero
        // el error tiene bastante más margen para que se lea.
        foreach (var tipo in new[]
                 {
                     TipoAvisoInline.Exito, TipoAvisoInline.Informacion,
                     TipoAvisoInline.Advertencia, TipoAvisoInline.Error
                 })
        {
            estado.Mostrar("aviso", tipo, ahora: 1_000);
            Assert.True(estado.DebeOcultarseAutomaticamente);
            Assert.True(estado.DuracionMs > 0);
            Assert.Equal(estado.DuracionMs, estado.MilisegundosRestantes(1_000));
        }
    }

    [Fact]
    public void ElErrorTieneMasMargenQueElExito()
    {
        var estado = new EstadoAvisoInline();

        estado.Mostrar("Listo", TipoAvisoInline.Exito, ahora: 0);
        int exito = estado.DuracionMs;
        estado.Mostrar("No se pudo enviar: disco lleno", TipoAvisoInline.Error, ahora: 0);

        Assert.True(estado.DuracionMs >= exito * 2,
            "un error debe dar tiempo de leerse antes de desaparecer");
        Assert.Equal(EstadoAvisoInline.DURACION_ERROR_MS, estado.DuracionMs);
        estado.Mostrar("Ojo", TipoAvisoInline.Advertencia, ahora: 0);
        Assert.Equal(EstadoAvisoInline.DURACION_ADVERTENCIA_MS, estado.DuracionMs);
    }

    [Fact]
    public void TranscurridoSuPlazoQuedaCero()
    {
        var estado = new EstadoAvisoInline();
        estado.Mostrar("Listo", TipoAvisoInline.Exito, ahora: 1_000);

        Assert.True(estado.MilisegundosRestantes(1_000 + EstadoAvisoInline.DURACION_EXITO_MS - 1) > 0);
        Assert.Equal(0, estado.MilisegundosRestantes(1_000 + EstadoAvisoInline.DURACION_EXITO_MS));
        Assert.Equal(0, estado.MilisegundosRestantes(1_000 + EstadoAvisoInline.DURACION_EXITO_MS + 1));
    }

    [Fact]
    public void MostrarReiniciaElPlazo()
    {
        var estado = new EstadoAvisoInline();
        estado.Mostrar("Primero", TipoAvisoInline.Exito, ahora: 1_000);

        // Llega otro aviso a los 3 s: el nuevo debe tener sus 4 s completos,
        // no los que le quedaban al anterior.
        estado.Mostrar("Segundo", TipoAvisoInline.Exito, ahora: 4_000);

        Assert.Equal("Segundo", estado.Mensaje);
        Assert.Equal(EstadoAvisoInline.DURACION_EXITO_MS, estado.MilisegundosRestantes(4_000));
    }

    [Fact]
    public void LimpiarDejaElEstadoEnBlancoYNoExpira()
    {
        var estado = new EstadoAvisoInline();
        estado.Mostrar("Listo", TipoAvisoInline.Exito, ahora: 1_000);

        estado.Limpiar();

        Assert.False(estado.Visible);
        Assert.Equal("", estado.Mensaje);
        Assert.Equal(-1, estado.MilisegundosRestantes(9_999));
    }

    [Fact]
    public void MensajeVacioLimpiaEnLugarDeMostrarEnBlanco()
    {
        var estado = new EstadoAvisoInline();
        estado.Mostrar("Algo", TipoAvisoInline.Exito, ahora: 1_000);

        estado.Mostrar("   ", TipoAvisoInline.Exito, ahora: 2_000);

        Assert.False(estado.Visible);
    }
}