using System;
using System.IO;
using ImageMagick;
using Mensajeria.Transversal;
using Xunit;

namespace Mensajeria.Tests;

/// <summary>
/// FORMATOS-02: el catálogo de iconos por tipo. Un catálogo que apunte a un PNG
/// que no se copia a la salida devolvería null y el archivo se vería con el 📎
/// genérico sin que nadie se enterara.
/// </summary>
public class IconosArchivoTests
{
    [Theory]
    [InlineData("informe.pdf", "pdf")]
    [InlineData("INFORME.PDF", "pdf")]
    [InlineData(@"C:\descargas\acta final.pdf", "pdf")]
    [InlineData("notas.xlsx", "xlsx")]
    [InlineData("notas.xls", "xlsx")]
    [InlineData("alumnos.csv", "csv")]
    [InlineData("config.json", "json")]
    [InlineData("tesis.docx", "docs")]
    [InlineData("apuntes.odt", "docs")]
    [InlineData("LEEME.md", "docs")]
    public void CadaExtensionApuntaASuIcono(string nombre, string clave)
    {
        Assert.Equal(clave, IconosArchivo.Clave(nombre));
        Assert.Equal(clave + ".png", IconosArchivo.Archivo(nombre));
    }

    [Theory]
    [InlineData("diapositivas.pptx")]
    [InlineData("paquete.zip")]
    [InlineData("video.mp4")]
    [InlineData("sin-extension")]
    [InlineData("termina.en.punto.")]
    [InlineData("")]
    [InlineData(null)]
    public void LoQueNoTieneIconoSeQuedaConElGenerico(string? nombre)
    {
        // Sin dibujo no se inventa uno: quien pinta sigue con el 📎 de siempre.
        Assert.Null(IconosArchivo.Clave(nombre));
        Assert.Null(IconosArchivo.Archivo(nombre));
        Assert.Null(IconosArchivo.Ruta(nombre));
    }

    [Theory]
    [InlineData("informe.pdf", "pdf")]
    [InlineData("notas.xlsx", "xlsx")]
    [InlineData("alumnos.csv", "csv")]
    [InlineData("config.json", "json")]
    [InlineData("tesis.docx", "docs")]
    public void LosCincoIconosSeCopianYsonPngCuadrados(string nombre, string clave)
    {
        string? ruta = IconosArchivo.Ruta(nombre);

        Assert.NotNull(ruta);
        Assert.Equal(clave + ".png", Path.GetFileName(ruta));
        var dibujo = new MagickImageInfo(ruta);
        Assert.Equal(512u, dibujo.Width);
        Assert.Equal(dibujo.Width, dibujo.Height);
    }

    [Fact]
    public void LaRutaSoloValeSiElPngEstaDeVerdad()
    {
        // El catálogo no miente: si los iconos no están junto al ejecutable, la
        // extensión se pinta como antes en vez de dejar un hueco roto.
        Assert.Null(IconosArchivo.Ruta("informe.pdf", Path.GetTempPath()));
        Assert.NotNull(IconosArchivo.Ruta("informe.pdf", AppContext.BaseDirectory));
    }
}
