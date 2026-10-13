using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;
using SkiaSharp;
using Xunit;

namespace Mensajeria.Tests;

/// <summary>
/// PLAN_AMBOS FASE 5: varias imágenes en UNA sola burbuja. Las reglas tienen que
/// ser las mismas que en <c>PlanEnvioAdjuntosTest</c> del cliente JavaFX.
/// </summary>
public class PlanEnvioAdjuntosTests
{
    private static AdjuntoPendiente Adj(string nombre, bool imagen) =>
        new($"/tmp/{nombre}", nombre, 100, imagen, imagen ? "image/png" : "application/pdf");

    private static IReadOnlyList<AdjuntoPendiente> A(params AdjuntoPendiente[] a) => a;

    [Fact]
    public void SinNadaNoHayNadaQueEnviar()
    {
        Assert.True(PlanEnvioAdjuntos.Vacio(null, A()));
        Assert.True(PlanEnvioAdjuntos.Vacio("   ", A()));
        Assert.Empty(PlanEnvioAdjuntos.Calcular(null, A()));
    }

    [Fact]
    public void SoloTextoViajaSolo()
    {
        var plan = PlanEnvioAdjuntos.Calcular("hola", A());

        var envio = Assert.IsType<PlanEnvioAdjuntos.Texto>(Assert.Single(plan));
        Assert.Equal("hola", envio.Cuerpo);
    }

    [Fact]
    public void UnaImagenConTextoEsUnaBurbujaConPie()
    {
        var plan = PlanEnvioAdjuntos.Calcular("mira esto", A(Adj("a.png", true)));

        var envio = Assert.IsType<PlanEnvioAdjuntos.Adjunto>(Assert.Single(plan));
        Assert.Equal("mira esto", envio.Caption);
    }

    [Fact]
    public void VariasImagenesSonUnaSolaBurbuja()
    {
        var plan = PlanEnvioAdjuntos.Calcular(null,
            A(Adj("a.png", true), Adj("b.png", true), Adj("c.png", true)));

        var grupo = Assert.IsType<PlanEnvioAdjuntos.Grupo>(Assert.Single(plan));
        Assert.Equal(3, grupo.Imagenes.Count);
        Assert.Null(grupo.Caption);
    }

    [Fact]
    public void VariasImagenesConTextoTambienSonUnaBurbuja()
    {
        var plan = PlanEnvioAdjuntos.Calcular("las tres",
            A(Adj("a.png", true), Adj("b.png", true)));

        var grupo = Assert.IsType<PlanEnvioAdjuntos.Grupo>(Assert.Single(plan));
        Assert.Equal("las tres", grupo.Caption);
        Assert.Equal(2, grupo.Imagenes.Count);
    }

    [Fact]
    public void ImagenesYDocumentosVanEnBurbujasDistintas()
    {
        var plan = PlanEnvioAdjuntos.Calcular("todo junto", A(
            Adj("a.png", true), Adj("b.png", true), Adj("doc.pdf", false)));

        // 3 elementos: el texto suelto + la burbuja de imágenes + el documento.
        // Lo mismo que PlanEnvioAdjuntosTest de Java ("texto + grupo + un documento").
        Assert.Equal(3, plan.Count);
        Assert.Equal("todo junto", Assert.IsType<PlanEnvioAdjuntos.Texto>(plan[0]).Cuerpo);
        var grupo = Assert.IsType<PlanEnvioAdjuntos.Grupo>(plan[1]);
        Assert.Equal(2, grupo.Imagenes.Count);
        Assert.Null(grupo.Caption);
        var archivo = Assert.IsType<PlanEnvioAdjuntos.Adjunto>(plan[2]);
        Assert.Equal("doc.pdf", archivo.Archivo.Nombre);
    }

    [Fact]
    public void ConVariasBurbujasElTextoVaComoMensajeSueltoDelante()
    {
        // Un pie no puede repetirse en N burbujas: el texto se manda solo y las
        // imágenes y el documento detrás, en ese orden.
        var plan = PlanEnvioAdjuntos.Calcular("explicación",
            A(Adj("a.png", true), Adj("doc.pdf", false)));

        Assert.Equal(3, plan.Count);
        Assert.Equal("explicación", Assert.IsType<PlanEnvioAdjuntos.Texto>(plan[0]).Cuerpo);
        Assert.IsType<PlanEnvioAdjuntos.Adjunto>(plan[1]);
        Assert.IsType<PlanEnvioAdjuntos.Adjunto>(plan[2]);
    }
}

/// <summary>PLAN_AMBOS FASE 5: la retícula del montaje.</summary>
public class MontajeImagenesDistribucionTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 2, 1)]
    [InlineData(3, 3, 1)]
    [InlineData(4, 2, 2)]
    [InlineData(5, 3, 2)]
    [InlineData(6, 3, 2)]
    [InlineData(9, 3, 3)]
    [InlineData(10, 3, 4)]
    public void LaReticulaNoDejaCeldasHuerfanas(int n, int columnas, int filas)
    {
        var r = MontajeImagenes.Distribucion(n);

        Assert.Equal(columnas, r[0]);
        Assert.Equal(filas, r[1]);
        Assert.True(columnas * filas >= n, "hay celdas de sobra, no huérfanas");
        Assert.True((columnas - 1) * filas < n, "sobra una fila entera");
    }

    [Fact]
    public void TresImagenesVanEnUnaFilaYNoEnUnDosPorDosConHueco()
    {
        Assert.Equal(new[] { 3, 1 }, MontajeImagenes.Distribucion(3));
    }

    [Fact]
    public void CeroImagenesNoTieneReticula()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MontajeImagenes.Distribucion(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MontajeImagenes.Distribucion(-1));
    }
}

/// <summary>
/// PLAN_AMBOS FASE 5: el montaje de verdad, con píxeles. Comprueba que la
/// imagen sale del tamaño esperado y que NO sale en blanco: un montaje
/// compuesto mal se ve perfecto en el código y es un rectángulo de color.
/// </summary>
public class MontajeImagenesTest
{
    private static AdjuntoPendiente Png(string nombre, int ancho, int alto, SKColor color)
    {
        string ruta = Path.Combine(Path.GetTempPath(), $"montaje_{Guid.NewGuid():N}.png");
        using var bitmap = new SKBitmap(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(color);
        }
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.Create(ruta);
        datos.SaveTo(fs);
        return new AdjuntoPendiente(ruta, nombre, new FileInfo(ruta).Length, true, "image/png");
    }

    private static SKColor Aleatorio() =>
        new((byte)Random.Shared.Next(30, 250), (byte)Random.Shared.Next(30, 250),
            (byte)Random.Shared.Next(30, 250));

    [Fact]
    public void ComponeDosImagenesEnUnSoloPng()
    {
        var a = Png("a.png", 100, 100, SKColors.Red);
        var b = Png("b.png", 100, 100, SKColors.Blue);

        var resultado = MontajeImagenes.Componer(new[] { a, b });

        try
        {
            Assert.Equal(2, resultado.Columnas);
            Assert.Equal(1, resultado.Filas);
            Assert.Equal("montaje_2_imagenes.png", resultado.Nombre);
            // 2 celdas de 480 + 1 hueco de 4.
            using var salida = SKBitmap.Decode(resultado.Png);
            Assert.Equal(2 * MontajeImagenes.LadoCelda + MontajeImagenes.Hueco, salida.Width);
            Assert.Equal(MontajeImagenes.LadoCelda, salida.Height);
        }
        finally
        {
            File.Delete(a.Ruta);
            File.Delete(b.Ruta);
        }
    }

    [Fact]
    public void SePintanTodasLasImagenesYNoSoloLaPrimera()
    {
        // El fallo que se quiere cazar: componer de tal manera que solo se pinte
        // la primera. Con 3 colores distintos en 4 celdas, la segunda celda tiene
        // que tener el color de la segunda imagen y no el fondo.
        var a = Png("a.png", 120, 120, SKColors.Red);
        var b = Png("b.png", 120, 120, SKColors.Lime);
        var c = Png("c.png", 120, 120, SKColors.Blue);
        var d = Png("d.png", 120, 120, SKColors.Yellow);

        var resultado = MontajeImagenes.Componer(new[] { a, b, c, d });

        try
        {
            using var bitmap = SKBitmap.Decode(resultado.Png);
            Assert.Equal(2, resultado.Columnas);
            Assert.Equal(2, resultado.Filas);
            // Centro de cada celda: si una sale del fondo, esa imagen no se pintó.
            Assert.Equal(SKColors.Red, ColorEn(bitmap, 0, 0));
            Assert.Equal(SKColors.Lime, ColorEn(bitmap, 1, 0));
            Assert.Equal(SKColors.Blue, ColorEn(bitmap, 0, 1));
            Assert.Equal(SKColors.Yellow, ColorEn(bitmap, 1, 1));
        }
        finally
        {
            foreach (var p in new[] { a, b, c, d }) File.Delete(p.Ruta);
        }
    }

    [Fact]
    public void UnaSolaImagenNoSeMonta()
    {
        var a = Png("a.png", 100, 100, SKColors.Red);
        try
        {
            Assert.Throws<InvalidOperationException>(
                () => MontajeImagenes.Componer(new[] { a }));
        }
        finally { File.Delete(a.Ruta); }
    }

    [Fact]
    public void UnArchivoQueNoEsImagenHaceFallarElMontajeEntero()
    {
        var a = Png("a.png", 100, 100, SKColors.Red);
        string roto = Path.Combine(Path.GetTempPath(), $"roto_{Guid.NewGuid():N}.png");
        File.WriteAllText(roto, "esto no es una imagen");
        var b = new AdjuntoPendiente(roto, "roto.png", 22, true, "image/png");
        try
        {
            Assert.Throws<InvalidOperationException>(
                () => MontajeImagenes.Componer(new[] { a, b }));
        }
        finally
        {
            File.Delete(a.Ruta);
            File.Delete(roto);
        }
    }

    [Fact]
    public void MasDeDiezImagenesSeRechazan()
    {
        var muchas = Enumerable.Range(0, 11)
            .Select(i => new AdjuntoPendiente($"/tmp/{i}.png", $"{i}.png", 1, true, "image/png"))
            .ToList();

        Assert.Throws<InvalidOperationException>(() => MontajeImagenes.Componer(muchas));
    }

    private static SKColor ColorEn(SKBitmap bitmap, int columna, int fila)
    {
        int x = columna * (MontajeImagenes.LadoCelda + MontajeImagenes.Hueco)
                + MontajeImagenes.LadoCelda / 2;
        int y = fila * (MontajeImagenes.LadoCelda + MontajeImagenes.Hueco)
                + MontajeImagenes.LadoCelda / 2;
        return bitmap.GetPixel(x, y);
    }
}

/// <summary>
/// <summary>
/// FORMATOS-01: los formatos que la vista lleva a PNG (webp + los amplios
/// avif/heic/heif/svg); el envío siempre va tal cual.
/// </summary>
public class TranscodificadorImagenTests
{
    [Fact]
    public void WebpYFormatosAmpliosNecesitanConversionParaLaVista()
    {
        Assert.True(TranscodificadorImagen.NecesitaConversion("foto.webp"));
        Assert.True(TranscodificadorImagen.NecesitaConversion("FOTO.WEBP"));
        Assert.True(TranscodificadorImagen.NecesitaConversion("foto.avif"));
        Assert.True(TranscodificadorImagen.NecesitaConversion("foto.heic"));
        Assert.True(TranscodificadorImagen.NecesitaConversion("foto.heif"));
        Assert.True(TranscodificadorImagen.NecesitaConversion("figura.svg"));
        Assert.False(TranscodificadorImagen.NecesitaConversion("foto.png"));
        Assert.False(TranscodificadorImagen.NecesitaConversion("informe.pdf"));
    }

    [Fact]
    public void UnPngNormalViajaTalCualSinCrearArchivos()
    {
        // Nada de convertir lo que ya está bien: crear un temporal por cada
        // adjunto sería tirar el disco.
        Assert.Equal("C:/x/foto.png", TranscodificadorImagen.RutaParaEnviar("C:/x/foto.png"));
    }

    [Fact]
    public void UnWebpRealSeConvierteEnUnPngQueSeVuelveAAbrir()
    {
        // Se genera un WebP real con Skia y se relee: si el decodificador no
        // aceptara .webp, SKBitmap.Decode devolvería null y esto fallaría.
        string origen = Path.Combine(Path.GetTempPath(), $"orig_{Guid.NewGuid():N}.webp");
        using (var bitmap = new SKBitmap(64, 48, SKColorType.Rgba8888, SKAlphaType.Premul))
        {
            using (var canvas = new SKCanvas(bitmap)) canvas.Clear(SKColors.Teal);
            using var imagen = SKImage.FromBitmap(bitmap);
            using var datos = imagen.Encode(SKEncodedImageFormat.Webp, 90);
            using var fs = File.Create(origen);
            datos.SaveTo(fs);
        }

        string destino = Path.Combine(Path.GetTempPath(), $"conv_{Guid.NewGuid():N}.png");
        try
        {
            string ruta = TranscodificadorImagen.ConvertirAPng(origen, destino);

            Assert.Equal(destino, ruta);
            Assert.Equal(".png", Path.GetExtension(ruta).ToLowerInvariant());
            using var releida = SKBitmap.Decode(ruta);
            Assert.NotNull(releida);
            Assert.Equal(64, releida.Width);
            Assert.Equal(48, releida.Height);
            // El pixel de la esquina tiene que ser el del .webp original.
            Assert.Equal(SKColors.Teal, releida.GetPixel(32, 24));
        }
        finally
        {
            File.Delete(origen);
            File.Delete(destino);
        }
    }

    [Fact]
    public void UnArchivoIlegibleFallaConUnMensajeQueDiceCualEs()
    {
        string roto = Path.Combine(Path.GetTempPath(), $"roto_{Guid.NewGuid():N}.webp");
        File.WriteAllText(roto, "esto no es una imagen");
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(
                () => TranscodificadorImagen.ConvertirAPng(roto));
            Assert.Contains("roto_", ex.Message);
        }
        finally { File.Delete(roto); }
    }

    [Fact]
    public void UnSvgSeConvierteEnUnPngLegible()
    {
        // FORMATOS-01: el servidor ya filtra SVG (DecodificadorMagick); para
        // la vista el cliente lo rasteriza a PNG vía ImageMagick.
        string origen = Path.Combine(Path.GetTempPath(), $"fig_{Guid.NewGuid():N}.svg");
        File.WriteAllText(origen,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20\" height=\"10\">"
            + "<rect width=\"20\" height=\"10\" fill=\"#204060\"/></svg>");
        string destino = Path.Combine(Path.GetTempPath(), $"fig_{Guid.NewGuid():N}.png");
        try
        {
            string ruta = TranscodificadorImagen.ConvertirAPng(origen, destino);
            Assert.Equal(destino, ruta);
            using var releida = SKBitmap.Decode(ruta);
            Assert.NotNull(releida);
            Assert.Equal(20, releida.Width);
            Assert.Equal(10, releida.Height);
            Assert.Equal(new SKColor(0x20, 0x40, 0x60), releida.GetPixel(5, 5));
        }
        finally { File.Delete(origen); File.Delete(destino); }
    }

    [Fact]
    public void UnAvifRealSeConvierteEnUnPngLegible()
    {
        // AVIF de 8x6 verde generado con libheif (fixture base64): el cliente
        // debe pintarlo aunque ni WPF ni Skia sepan abrir ese formato.
        const string avifBase64 = "AAAAIGZ0eXBhdmlmAAAAAGF2aWZtaWYxbWlhZk1BMUIAAADrbWV0"
            + "YQAAAAAAAAAhaGRscgAAAAAAAAAAcGljdAAAAAAAAAAAAAAAAAAAAAAOcGl0bQAAA"
            + "AAAAQAAAB5pbG9jAAAAAEQAAAEAAQAAAAEAAAETAAAAKAAAAChpaW5mAAAAAAABAA"
            + "AAGmluZmUCAAAAAAEAAGF2MDFDb2xvcgAAAABqaXBycAAAAEtpcGNvAAAAFGlzcGU"
            + "AAAAAAAAACAAAAAYAAAAQcGl4aQAAAAADCAgIAAAADGF2MUOBAAwAAAAAE2NvbHJu"
            + "Y2x4AAEADQAGgAAAABdpcG1hAAAAAAAAAAEAAQQBAoMEAAAAMG1kYXQSAAoIGAi9a"
            + "ICGg0IyGhTHh4ZlAgggnlAAAABIWtlcu9vROwpYZeiA";
        string origen = Path.Combine(Path.GetTempPath(), $"avif_{Guid.NewGuid():N}.avif");
        File.WriteAllBytes(origen, Convert.FromBase64String(avifBase64));
        string destino = Path.Combine(Path.GetTempPath(), $"avif_{Guid.NewGuid():N}.png");
        try
        {
            string ruta = TranscodificadorImagen.ConvertirAPng(origen, destino);
            using var releida = SKBitmap.Decode(ruta);
            Assert.NotNull(releida);
            Assert.Equal(8, releida.Width);
            Assert.Equal(6, releida.Height);
        }
        finally { File.Delete(origen); File.Delete(destino); }
    }
}