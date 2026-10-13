using Mensajeria.Transversal;
using Xunit;

namespace Mensajeria.Tests;

public class CatalogoAdjuntosTests
{
    [Theory]
    [InlineData("foto.JPG", true)]
    [InlineData("a.png", true)]
    [InlineData("b.gif", true)]
    [InlineData("c.bmp", true)]
    [InlineData("d.webp", true)]
    [InlineData("g.avif", true)]
    [InlineData("h.heic", true)]
    [InlineData("i.heif", true)]
    [InlineData("j.svg", true)]
    [InlineData("e.pdf", false)]
    [InlineData("f.docx", false)]
    public void ClasificaPorExtensionIgnorandoMayusculas(string nombre, bool esImagen) =>
        Assert.Equal(esImagen, CatalogoAdjuntos.EsImagen(nombre));

    [Fact]
    public void WebpYFormatosAmpliosSeOfrecenYTratanComoImagen()
    {
        // UIX-27 / FORMATOS-01: el .webp y los formatos amplios entran en la
        // lista de imágenes; viajan tal cual como MENSAJE_IMAGEN (el servidor
        // los filtra, los amplios vía ImageMagick) y la vista los convierte
        // a PNG (TranscodificadorImagen). Si no estuvieran, pasarían por el
        // diálogo de archivos y llegarían como un documento cualquiera.
        Assert.Contains("*.webp", CatalogoAdjuntos.FiltroDialogo);
        Assert.True(CatalogoAdjuntos.EsImagen("hola.webp"));
        Assert.Equal("image/webp", CatalogoAdjuntos.Mime("hola.webp"));
        Assert.True(TranscodificadorImagen.NecesitaConversion("hola.webp"));
        Assert.True(CatalogoAdjuntos.EsImagen("hola.avif"));
        Assert.Equal("image/avif", CatalogoAdjuntos.Mime("hola.avif"));
        Assert.Equal("image/heic", CatalogoAdjuntos.Mime("hola.heic"));
        Assert.Equal("image/heif", CatalogoAdjuntos.Mime("hola.heif"));
        Assert.Equal("image/svg+xml", CatalogoAdjuntos.Mime("hola.svg"));
        Assert.True(TranscodificadorImagen.NecesitaConversion("hola.avif"));
        Assert.True(TranscodificadorImagen.NecesitaConversion("hola.heic"));
        Assert.True(TranscodificadorImagen.NecesitaConversion("hola.svg"));
    }

    [Fact]
    public void FiltroCoincideConElContratoDeTresBloques() =>
        Assert.Equal("Todos los archivos|*.*|"
            + "Imágenes|*.png *.jpg *.jpeg *.gif *.bmp *.webp *.avif *.heic *.heif *.svg|"
            + "Documentos|*.pdf *.docx *.xlsx *.pptx *.txt *.zip",
            CatalogoAdjuntos.FiltroDialogo);

    [Theory]
    [InlineData(512, "0.5 KB")]
    [InlineData(1048576, "1.0 MB")]
    public void TamanoLegibleUsaKiBIMB(long bytes, string esperado) =>
        Assert.Equal(esperado, CatalogoAdjuntos.TamanoLegible(bytes));

    [Fact]
    public void TodaImagenTieneElTopeDeTramaUnicaYLosDocumentosVanFragmentados()
    {
        Assert.False(CatalogoAdjuntos.CabeEnTramaUnica("foto.png",
            CatalogoAdjuntos.MaxBytesImagenTramaUnica + 1));
        // El .webp pasó a ser imagen: le aplica el mismo tope de envío directo.
        Assert.False(CatalogoAdjuntos.CabeEnTramaUnica("documento.webp",
            CatalogoAdjuntos.MaxBytesImagenTramaUnica + 1));
        Assert.True(CatalogoAdjuntos.CabeEnTramaUnica("informe.pdf",
            Mensajeria.Fachada.IFachadaCliente.MaxBytesArchivo));
    }
}
