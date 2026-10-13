package universidad.mensajeria.utilerias.imagen;

import org.junit.jupiter.api.Test;

import javax.imageio.ImageIO;
import java.awt.image.BufferedImage;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Base64;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * UIX-27: el WebP entra por la puerta y sale por la tubería.
 * <p>
 * Aceptar la cabecera no basta: si {@code ImageIO} no supiera abrirlo, la
 * imagen pasaría el filtro mágico y se perdería una fase después, que es peor
 * que rechazarla. Estos tests fabrican un WebP real y lo pasan por
 * {@link Imagenes#leer}, que es justo lo que hace la tubería de filtros.
 * </p>
 */
class ImagenWebpTest {

    /**
     * Un {@code .webp} real de 30×20 en azul, en Base64 (36 bytes).
     * <p>
     * Va embebido a propósito: {@code imageio-webp} solo trae LECTOR, no
     * escritor, así que el test no puede fabricar el suyo con
     * {@code ImageIO.write}. Lo que hay que demostrar aquí es que el servidor
     * <b>abre</b> el WebP, no que lo codifica.
     * </p>
     */
    private static final String WEBP_AZUL_30x20 =
            "UklGRhwAAABXRUJQVlA4TA8AAAAvHcAEAAcQ0f/+ByKi/wEA";

    @Test
    void laTuberiaAbreUnWebpDeVerdad() throws IOException {
        byte[] webp = Base64.getDecoder().decode(WEBP_AZUL_30x20);

        // Esta es la línea que fallaría sin el lector en el classpath.
        assertTrue(Imagenes.esImagenValida(webp));

        Path temporal = Files.createTempFile("img", ".webp");
        try {
            Files.write(temporal, webp);
            BufferedImage leida = Imagenes.leer(temporal);

            assertNotNull(leida);
            assertEquals(30, leida.getWidth());
            assertEquals(20, leida.getHeight());
        } finally {
            Files.deleteIfExists(temporal);
        }
    }

    @Test
    void elLectorWebpEstaRegistrado() {
        // Si el plugin no estuviera por META-INF/services, el anterior fallaría
        // con "no es una imagen legible". Se mira explícitamente para que el
        // diagnóstico sea claro si algún día se cae del classpath.
        assertTrue(ImageIO.getImageReadersByFormatName("webp").hasNext());
    }
}