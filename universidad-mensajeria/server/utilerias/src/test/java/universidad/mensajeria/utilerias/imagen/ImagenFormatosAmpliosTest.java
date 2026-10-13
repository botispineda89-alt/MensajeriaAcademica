package universidad.mensajeria.utilerias.imagen;

import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import universidad.mensajeria.utilerias.ContextoTuberia;
import universidad.mensajeria.utilerias.Tuberia;
import universidad.mensajeria.utilerias.TuberiaFactory;

import java.awt.image.BufferedImage;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Base64;
import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Formatos que el JDK no decodifica (AVIF, HEIC, SVG): deben entrar por la
 * puerta mágica y salir por la misma tubería de 6 etapas que un PNG.
 * <p>
 * Los dos bloques están separados a propósito: la detección de firma se prueba
 * sin depender de nada externo, y la decodificación real se salta (no falla) si
 * ImageMagick no está instalado en la máquina, porque es una dependencia del
 * sistema operativo y no del repositorio.
 * </p>
 */
class ImagenFormatosAmpliosTest {

    @TempDir
    Path trabajo;

    /**
     * Un {@code .avif} real de 30×20 en azul (291 bytes), generado con
     * {@code magick -size 30x20 xc:blue avif:azul.avif}. Se embebe porque el
     * JDK no sabe escribir AVIF y el test no puede fabricarlo.
     */
    private static final String AVIF_AZUL_30x20 =
            "AAAAHGZ0eXBhdmlmAAAAAG1pZjFhdmlmbWlhZgAAANZtZXRhAAAAAAAAACFoZGxyAAAAAAAAAABwaWN0"
            + "AAAAAAAAAAAAAAAAAAAAACJpbG9jAAAAAERAAAEAAQAAAAAA+gABAAAAAAAAACkAAAAjaWluZgAAAAAA"
            + "AQAAABVpbmZlAgAAAAABAABhdjAxAAAAAA5waXRtAAAAAAABAAAAVmlwcnAAAAA4aXBjbwAAAAxhdjFD"
            + "gUBsAAAAABRpc3BlAAAAAAAAAB4AAAAUAAAAEHBpeGkAAAAAAwwMDAAAABZpcG1hAAAAAAAAAAEAAQOB"
            + "AgMAAAAxbWRhdBIACglYETs2tAQ0G4QyGhlHh4YhiaaaZoAAAIs7m1qDk3iU7vx0SPpg";

    /** El mismo azul de 30×20 en WebP real (82 bytes), salido de ImageMagick. */
    private static final String WEBP_AZUL_30x20 =
            "UklGRkoAAABXRUJQVlA4ID4AAAAQAwCdASoeABQAPpFCnEolo6KhqAgAsBIJZQDMHBapagAA/vVYv/+5"
            + "wP/9nA//2cD+Ov9eqWuTjgLLeAAAAA==";

    private static final String SVG_AZUL_30x20 = """
            <?xml version="1.0" encoding="UTF-8"?>
            <svg xmlns="http://www.w3.org/2000/svg" width="30" height="20" viewBox="0 0 30 20">
              <rect width="30" height="20" fill="#0000ff"/>
            </svg>
            """;

    // ------------------------------------------------------------------
    // Firma y deteccion (sin dependencias del sistema)
    // ------------------------------------------------------------------

    @Test
    void aceptaAvifYHeicPorLaMarcaDelContenedorIsoBmff() {
        assertTrue(Imagenes.esImagenValida(Base64.getDecoder().decode(AVIF_AZUL_30x20)));

        for (String marca : List.of("avif", "avis", "heic", "heix", "mif1", "msf1")) {
            assertTrue(Imagenes.esImagenValida(cabeceraFtyp(marca)),
                    "deberia aceptar la marca " + marca);
        }
    }

    /**
     * {@code ftyp} a secas no basta: los MP4/MOV empiezan igual y no son
     * imágenes. Es el mismo cuidado que ya se tenía con RIFF y el WAV.
     */
    @Test
    void rechazaContenedoresIsoBmffQueSonVideo() {
        for (String marca : List.of("isom", "mp42", "qt  ", "M4V ")) {
            assertFalse(Imagenes.esImagenValida(cabeceraFtyp(marca)),
                    "no deberia aceptar la marca de video " + marca);
        }
    }

    @Test
    void aceptaSvgConDeclaracionDoctypeBomYComentario() {
        assertTrue(Imagenes.esImagenValida(utf8(SVG_AZUL_30x20)));
        assertTrue(Imagenes.esImagenValida(utf8("<svg width='10' height='10'/>")));
        assertTrue(Imagenes.esImagenValida(utf8("\uFEFF  \n<SVG xmlns='a'/>")));
        assertTrue(Imagenes.esImagenValida(utf8("""
                <?xml version="1.0"?>
                <!-- hecho a mano -->
                <!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.1//EN" "http://www.w3.org/x.dtd">
                <svg xmlns="http://www.w3.org/2000/svg" width="4" height="4"/>
                """)));
    }

    /**
     * El SVG no tiene firma binaria, así que la comprobación mira el primer
     * elemento: si no es {@code <svg>}, no es un SVG. Un HTML con uno incrustado
     * o un PDF no deben colarse.
     */
    @Test
    void rechazaHtmlPdfYHermanosQueNoSonSvg() {
        assertFalse(Imagenes.esImagenValida(utf8(
                "<html><body><svg width='4' height='4'/></body></html>")));
        assertFalse(Imagenes.esImagenValida(utf8(
                "<!DOCTYPE html><html><svg/></html>")));
        assertFalse(Imagenes.esImagenValida(utf8(
                "<?xml version=\"1.0\"?><root><svg/></root>")));
        assertFalse(Imagenes.esImagenValida(utf8("<svgfoo/>")));
        assertFalse(Imagenes.esImagenValida(utf8("<?xml version=\"1.0\"?><sin-cerrar")));
        assertFalse(Imagenes.esImagenValida(utf8("%PDF-1.4 fake con <svg> dentro")));
    }

    // ------------------------------------------------------------------
    // Decodificacion real + tuberia completa (requiere ImageMagick)
    // ------------------------------------------------------------------

    @Test
    void laTuberiaProcesaUnAvifReal() throws Exception {
        exigirImageMagick();
        BufferedImage leida = pasarPorLaTuberia("original.avif",
                Base64.getDecoder().decode(AVIF_AZUL_30x20));
        assertEquals(15, leida.getWidth());
        assertEquals(10, leida.getHeight());
    }

    @Test
    void laTuberiaProcesaUnWebpReal() throws Exception {
        exigirImageMagick();
        BufferedImage leida = pasarPorLaTuberia("original.webp",
                Base64.getDecoder().decode(WEBP_AZUL_30x20));
        assertEquals(15, leida.getWidth());
        assertEquals(10, leida.getHeight());
    }

    @Test
    void laTuberiaProcesaUnSvgReal() throws Exception {
        exigirImageMagick();
        BufferedImage leida = pasarPorLaTuberia("original.svg",
                utf8(SVG_AZUL_30x20));
        assertEquals(15, leida.getWidth());
        assertEquals(10, leida.getHeight());
    }

    /**
     * Un PNG corrupto sigue fallando igual que antes: la via ImageMagick NO se
     * usa para los formatos que el JDK ya decodificaba, así que ni cambia el
     * mensaje ni se gasta un proceso.
     */
    @Test
    void unPngCorruptoFallaComoAntesSinPasarPorImageMagick() throws IOException {
        Path roto = trabajo.resolve("roto.png");
        Files.write(roto, new byte[]{(byte) 0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x00});

        IOException fallo = assertThrows(IOException.class, () -> Imagenes.leer(roto));
        assertTrue(fallo.getMessage().contains("no es una imagen legible"));
        assertEquals(0, fallo.getSuppressed().length,
                "un PNG corrupto no debe intentar decodificarse con ImageMagick");
    }

    // ------------------------------------------------------------------
    // Apoyo
    // ------------------------------------------------------------------

    /**
     * Escribe los bytes, comprueba que la puerta mágica los acepta y los pasa
     * por la tubería de imagen completa, que es exactamente lo que hace
     * {@code Mensajes.procesarImagen}.
     */
    private BufferedImage pasarPorLaTuberia(String nombre, byte[] bytes) throws Exception {
        assertTrue(Imagenes.esImagenValida(bytes), "la puerta magica rechazo " + nombre);

        Path original = trabajo.resolve(nombre);
        Files.write(original, bytes);

        BufferedImage directa = Imagenes.leer(original);
        assertEquals(30, directa.getWidth(), "ancho de " + nombre);
        assertEquals(20, directa.getHeight(), "alto de " + nombre);

        Tuberia.ResultadoTuberia resultado = new TuberiaFactory().imagen()
                .ejecutar(List.of(original.toFile()), ContextoTuberia.sinOyente(trabajo));

        assertEquals(List.of("SHA256", "EscalaGrises", "Sepia", "Giro180", "Brillo", "Reduccion"),
                resultado.traza().stream().map(e -> e.etapa()).toList());
        assertTrue(resultado.metadatos().obtener("hashSha256").matches("[0-9a-f]{64}"));
        assertTrue(Files.isRegularFile(trabajo.resolve("05_reduccion.png")),
                "la tuberia no genero la ultima derivada para " + nombre);

        return Imagenes.leer(resultado.archivos().get(0).toPath());
    }

    private static void exigirImageMagick() {
        Assumptions.assumeTrue(DecodificadorMagick.disponible(),
                "ImageMagick no esta instalado: la decodificacion de " + DecodificadorMagick.rutaEjecutable()
                        + " no se puede comprobar aqui");
    }

    /** {@code <tamano>ftyp<marca>} con relleno hasta los 12 bytes minimos. */
    private static byte[] cabeceraFtyp(String marca) {
        byte[] bytes = new byte[12];
        bytes[3] = 0x18;
        bytes[4] = 'f';
        bytes[5] = 't';
        bytes[6] = 'y';
        bytes[7] = 'p';
        System.arraycopy(marca.getBytes(StandardCharsets.ISO_8859_1), 0, bytes, 8, 4);
        return bytes;
    }

    private static byte[] utf8(String texto) {
        return texto.getBytes(StandardCharsets.UTF_8);
    }
}
