package universidad.mensajeria.cliente.transversal.utilerias;

import org.junit.jupiter.api.Test;

import java.awt.image.BufferedImage;
import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.util.Base64;
import javax.imageio.ImageIO;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * UIX-27 / FORMATOS-01: los formatos que la vista lleva a PNG (webp por
 * TwelveMonkeys; avif/heic por NightMonkeys/libheif; svg por jsvg). El envío
 * siempre va tal cual: la conversión es solo para pintar.
 */
class TranscodificadorImagenTest {

    /** Magic bytes de PNG: lo que realmente comprueba {@code Imagenes.esImagenValida}. */
    private static final byte[] MAGICO_PNG = {(byte) 0x89, 'P', 'N', 'G'};

    @Test
    void webpYFormatosAmpliosNecesitanConversionParaLaVista() {
        assertTrue(TranscodificadorImagen.necesitaConversion("foto.webp"));
        assertTrue(TranscodificadorImagen.necesitaConversion("FOTO.WEBP"));
        assertTrue(TranscodificadorImagen.necesitaConversion("foto.avif"));
        assertTrue(TranscodificadorImagen.necesitaConversion("foto.heic"));
        assertTrue(TranscodificadorImagen.necesitaConversion("foto.heif"));
        assertTrue(TranscodificadorImagen.necesitaConversion("figura.svg"));
        assertFalse(TranscodificadorImagen.necesitaConversion("foto.png"));
        assertFalse(TranscodificadorImagen.necesitaConversion("informe.pdf"));
        assertFalse(TranscodificadorImagen.necesitaConversion("sin-extension"));
    }

    @Test
    void loQueNoNecesitaConversionSaleIntactoYSinLeer() throws IOException {
        // Nada de decodificar lo que ya está bien: si un PNG viniera corrupto,
        // esto debe fallar después (al pintarlo), no aquí.
        byte[] original = {1, 2, 3, 4};
        assertArrayEquals(original, TranscodificadorImagen.aPng(original, "foto.png"));
        assertArrayEquals(original, TranscodificadorImagen.aPng(original, "informe.pdf"));
    }

    @Test
    void elNombreConvertidoConservaElOriginalYCambiaLaExtension() {
        assertEquals("foto.png", TranscodificadorImagen.nombreConvertido("foto.webp"));
        assertEquals("mis fotos.png", TranscodificadorImagen.nombreConvertido("mis fotos.webp"));
        assertEquals("a.b.png", TranscodificadorImagen.nombreConvertido("a.b.webp"));
    }

    /**
     * Un .webp real de 30x20 en azul, en Base64 (36 bytes).
     * <p>
     * Va embebido a propósito: {@code imageio-webp} solo trae LECTOR, no escritor,
     * así que el test no puede fabricar el suyo con {@code ImageIO.write}. Si se
     * generara con otro codificador, el test dependería de esa otra librería.
     * </p>
     */
    private static final String WEBP_AZUL_30x20 =
            "UklGRhwAAABXRUJQVlA4TA8AAAAvHcAEAAcQ0f/+ByKi/wEA";

    @Test
    void unWebpRealSeConvierteEnUnPngQueSeVuelveAAbrir() throws IOException {
        byte[] webp = Base64.getDecoder().decode(WEBP_AZUL_30x20);
        byte[] png = TranscodificadorImagen.aPng(webp, "foto.webp");

        // Lo que realmente comprueba Imagenes.esImagenValida en el servidor.
        assertArrayEquals(MAGICO_PNG, primeros(png, 4));
        BufferedImage releida = ImageIO.read(new ByteArrayInputStream(png));
        assertNotNull(releida);
        assertEquals(30, releida.getWidth());
        assertEquals(20, releida.getHeight());
        // Es un .webp con pérdida: el azul se comprueba por dominante, no exacto.
        int argb = releida.getRGB(15, 10);
        int rojo = (argb >> 16) & 0xFF;
        int verde = (argb >> 8) & 0xFF;
        int azul = argb & 0xFF;
        assertTrue(azul > rojo && azul > verde,
                "el pixel central debería ser azul, era #" + Integer.toHexString(argb));
    }

    @Test
    void unWebpCorruptoFallaDiciendoCualEs() {
        byte[] basura = {1, 2, 3, 4, 5};

        IOException ex = assertThrows(IOException.class,
                () -> TranscodificadorImagen.aPng(basura, "roto.webp"));
        assertTrue(ex.getMessage().contains("roto.webp"));
    }

    /** Un AVIF real de 8x6 verde, en Base64 (generado con libheif/pillow-avif). */
    private static final String AVIF_VERDE_8x6 =
            "AAAAIGZ0eXBhdmlmAAAAAGF2aWZtaWYxbWlhZk1BMUIAAADrbWV0YQAAAAAAAAAhaGRscgAAAA"
            + "AAAAAAcGljdAAAAAAAAAAAAAAAAAAAAAAOcGl0bQAAAAAAAQAAAB5pbG9jAAAAAEQAAAEA"
            + "AQAAAAEAAAETAAAAKAAAAChpaW5mAAAAAAABAAAAGmluZmUCAAAAAAEAAGF2MDFDb2xvcgA"
            + "AAABqaXBycAAAAEtpcGNvAAAAFGlzcGUAAAAAAAAACAAAAAYAAAAQcGl4aQAAAAADCAgIAA"
            + "AADGF2MUOBAAwAAAAAE2NvbHJuY2x4AAEADQAGgAAAABdpcG1hAAAAAAAAAAEAAQQBAoMEA"
            + "AAAMG1kYXQSAAoIGAi9aICGg0IyGhTHh4ZlAgggnlAAAABIWtlcu9vROwpYZeiA";

    @Test
    void unAvifRealSeConvierteEnUnPngQueSeVuelveAAbrir() throws IOException {
        // La vía del cliente Java para AVIF/HEIC es ImageMagick (im4java), la
        // misma herramienta que el servidor exige; sin ella el test se salta
        // y la burbuja muestra el placeholder "Formato no soportado".
        org.junit.jupiter.api.Assumptions.assumeTrue(
                DecodificadorMagickCliente.disponible(),
                "ImageMagick no está instalado en este equipo");
        byte[] avif = Base64.getDecoder().decode(AVIF_VERDE_8x6);
        byte[] png = TranscodificadorImagen.aPng(avif, "foto.avif");

        assertArrayEquals(MAGICO_PNG, primeros(png, 4));
        BufferedImage releida = ImageIO.read(new ByteArrayInputStream(png));
        assertNotNull(releida);
        assertEquals(8, releida.getWidth());
        assertEquals(6, releida.getHeight());
        // El verde AV1 se comprueba por dominante, no exacto.
        int argb = releida.getRGB(2, 2);
        int rojo = (argb >> 16) & 0xFF;
        int verde = (argb >> 8) & 0xFF;
        assertTrue(verde > rojo, "el pixel debería dominar en verde, era #"
                + Integer.toHexString(argb));
    }

    @Test
    void unSvgSeRasterizaEnUnPngConSuTamanoyColor() throws IOException {
        String svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20\" height=\"10\">"
                + "<rect width=\"20\" height=\"10\" fill=\"#204060\"/></svg>";
        byte[] png = TranscodificadorImagen.aPng(
                svg.getBytes(java.nio.charset.StandardCharsets.UTF_8), "figura.svg");

        assertArrayEquals(MAGICO_PNG, primeros(png, 4));
        BufferedImage releida = ImageIO.read(new ByteArrayInputStream(png));
        assertNotNull(releida);
        assertEquals(20, releida.getWidth());
        assertEquals(10, releida.getHeight());
        assertEquals(0x204060, releida.getRGB(5, 5) & 0xFFFFFF);
    }

    private static byte[] primeros(byte[] datos, int n) {
        byte[] salida = new byte[n];
        System.arraycopy(datos, 0, salida, 0, n);
        return salida;
    }
}