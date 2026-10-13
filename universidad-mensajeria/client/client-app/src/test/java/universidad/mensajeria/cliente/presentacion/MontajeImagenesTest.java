package universidad.mensajeria.cliente.presentacion;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.awt.Color;
import java.awt.Graphics2D;
import java.awt.image.BufferedImage;
import java.io.ByteArrayOutputStream;
import java.util.ArrayList;
import java.util.List;

import javax.imageio.ImageIO;

import org.junit.jupiter.api.Test;

/**
 * R3: varias imágenes en una sola burbuja.
 * <p>
 * Lo que se comprueba aquí es que el montaje sale de verdad (bytes PNG, retícula
 * correcta) y que la aritmética de la retícula no deja huecos.
 * </p>
 */
class MontajeImagenesTest {

    private static AdjuntoPendiente imagen(int ancho, int alto, Color color, String nombre) {
        BufferedImage original = new BufferedImage(ancho, alto, BufferedImage.TYPE_INT_RGB);
        Graphics2D g = original.createGraphics();
        g.setColor(color);
        g.fillRect(0, 0, ancho, alto);
        g.dispose();
        try {
            ByteArrayOutputStream salida = new ByteArrayOutputStream();
            ImageIO.write(original, "png", salida);
            return new AdjuntoPendiente(nombre, null, salida.toByteArray());
        } catch (Exception fallo) {
            throw new IllegalStateException(fallo);
        }
    }

    private static List<AdjuntoPendiente> imagenes(int n) {
        List<AdjuntoPendiente> lista = new ArrayList<>();
        Color[] paleta = {Color.RED, Color.GREEN, Color.BLUE, Color.YELLOW, Color.MAGENTA};
        for (int i = 0; i < n; i++) {
            lista.add(imagen(40, 30, paleta[i % paleta.length], "f" + i + ".png"));
        }
        return lista;
    }

    @Test
    void laReticulaNoDejaHuecos() {
        assertEquals(1, MontajeImagenes.distribucion(1)[0] * MontajeImagenes.distribucion(1)[1]);
        int[] dos = MontajeImagenes.distribucion(2);
        assertEquals(2 * 1, dos[0] * dos[1]);
        int[] tres = MontajeImagenes.distribucion(3);
        assertEquals(3, tres[0] * tres[1], "tres en una fila, no 2x2 con un hueco");
        int[] cuatro = MontajeImagenes.distribucion(4);
        assertEquals(4, cuatro[0] * cuatro[1]);
        int[] cinco = MontajeImagenes.distribucion(5);
        assertEquals(6, cinco[0] * cinco[1], "5 imágenes en 3x2: una celda vacía inevitable");
        int[] nueve = MontajeImagenes.distribucion(9);
        assertEquals(9, nueve[0] * nueve[1]);
        assertThrows(IllegalArgumentException.class, () -> MontajeImagenes.distribucion(0));
    }

    @Test
    void componeVariasImagenesEnUnSoloPng() throws Exception {
        MontajeImagenes.Resultado resultado = MontajeImagenes.componer(imagenes(3));

        assertEquals(3, resultado.columnas());
        assertEquals(1, resultado.filas());
        assertEquals("montaje_3_imagenes.png", resultado.nombre());
        assertTrue(resultado.png().length > 0, "el montaje tiene bytes");

        BufferedImage salida = ImageIO.read(new java.io.ByteArrayInputStream(resultado.png()));
        assertEquals(MontajeImagenes.LADO_CELDA * 3 + MontajeImagenes.HUECO * 2, salida.getWidth());
        assertEquals(MontajeImagenes.LADO_CELDA, salida.getHeight());
    }

    @Test
    void elMontajeConservaLosColoresDeCadaImagen() throws Exception {
        MontajeImagenes.Resultado resultado = MontajeImagenes.componer(
                List.of(imagen(40, 30, Color.RED, "a.png"), imagen(40, 30, Color.BLUE, "b.png")));
        BufferedImage salida = ImageIO.read(new java.io.ByteArrayInputStream(resultado.png()));
        assertEquals(2, resultado.columnas(), "dos imagenes = 2 columnas");
        assertEquals(2 * MontajeImagenes.LADO_CELDA + MontajeImagenes.HUECO, salida.getWidth());

        int centroIzquierdo = salida.getRGB(MontajeImagenes.LADO_CELDA / 2,
                MontajeImagenes.LADO_CELDA / 2);
        int centroDerecho = salida.getRGB(MontajeImagenes.LADO_CELDA + MontajeImagenes.HUECO
                + MontajeImagenes.LADO_CELDA / 2, MontajeImagenes.LADO_CELDA / 2);

        assertTrue(rojo(centroIzquierdo), "izquierda = roja, era " + Integer.toHexString(centroIzquierdo));
        assertTrue(azul(centroDerecho), "derecha = azul, era " + Integer.toHexString(centroDerecho));
    }

    @Test
    void unaSolaImagenNoEsUnMontaje() {
        assertThrows(java.io.IOException.class,
                () -> MontajeImagenes.componer(imagenes(1)));
    }

    @Test
    void unaImagenIlegibleHaceFallarElMontaje() {
        List<AdjuntoPendiente> conBasura = new ArrayList<>(imagenes(2));
        conBasura.add(new AdjuntoPendiente("rota.png", null, new byte[]{1, 2, 3}));

        assertThrows(java.io.IOException.class, () -> MontajeImagenes.componer(conBasura));
    }

    private static boolean rojo(int argb) {
        int r = (argb >> 16) & 0xFF;
        int g = (argb >> 8) & 0xFF;
        int b = argb & 0xFF;
        return r > 150 && g < 90 && b < 90;
    }

    private static boolean azul(int argb) {
        int r = (argb >> 16) & 0xFF;
        int g = (argb >> 8) & 0xFF;
        int b = argb & 0xFF;
        return b > 150 && r < 90 && g < 90;
    }
}