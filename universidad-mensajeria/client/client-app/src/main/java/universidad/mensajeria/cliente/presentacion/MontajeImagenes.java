package universidad.mensajeria.cliente.presentacion;

import javafx.scene.image.Image;
import javafx.scene.image.PixelReader;

import javax.imageio.ImageIO;
import java.awt.Color;
import java.awt.Graphics2D;
import java.awt.RenderingHints;
import java.awt.image.BufferedImage;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

/**
 * R3: varias imágenes en UNA sola burbuja.
 * <p>
 * El protocolo manda un adjunto por trama y la UI pinta una burbuja por mensaje.
 * En vez de tocar el contrato (que también leería el servidor y los otros dos
 * clientes), el JavaFX compone aquí un <b>montaje</b> y lo envía como una única
 * {@code MENSAJE_IMAGEN}: una trama, una burbuja, y los otros clientes la ven
 * igual sin que sepan nada de grupos.
 * <p>
 * Se decodifica con JavaFX, que admite png, jpg, gif y bmp — <b>no</b> webp: los
 * .webp ya llegan convertidos a PNG por
 * {@link universidad.mensajeria.cliente.transversal.utilerias.TranscodificadorImagen},
 * porque ni JavaFX ni el servidor saben con ellos. Se compone con
 * {@code BufferedImage}. Corre en un hilo de fondo: {@code Image} no es un nodo
 * y no necesita el hilo de JavaFX para construirse.
 * </p>
 */
public final class MontajeImagenes {

    /** Máximo de imágenes por montaje (más allá son 2 montajes, no 1 burbuja). */
    public static final int MAXIMO_IMAGENES = 10;
    /** Máximo de columnas de la retícula: más se sale de la pantalla. */
    public static final int COLUMNAS_MAX = 3;
    /** Lado de cada celda del montaje. */
    public static final int LADO_CELDA = 480;
    /** Separación entre celdas. */
    public static final int HUECO = 4;
    /** Fondo entre celdas: el gris del chat, no un blanco que rompe el tema. */
    private static final Color FONDO = new Color(0x2A2F3A);

    /** Montaje listo para enviar: PNG en memoria + nombre y retícula. */
    public record Resultado(byte[] png, String nombre, int columnas, int filas) { }

    private MontajeImagenes() {
    }

    /**
     * Compone las imágenes en una sola. Falla si alguna no se puede leer: el
     * llamador decide qué hacer (el comando reintenta enviándolas sueltas).
     *
     * @throws IOException si alguna imagen no se decodifica o no hay PNG de salida
     */
    public static Resultado componer(List<AdjuntoPendiente> imagenes) throws IOException {
        if (imagenes == null || imagenes.size() < 2) {
            throw new IOException("un montaje necesita al menos dos imágenes");
        }
        if (imagenes.size() > MAXIMO_IMAGENES) {
            throw new IOException("máximo " + MAXIMO_IMAGENES + " imágenes por montaje");
        }
        List<Image> fuentes = new ArrayList<>();
        for (AdjuntoPendiente a : imagenes) {
            // El tamaño destino va en la propia carga: JavaFX decodifica y
            // reduce de una vez, sin subir a memoria la imagen a tamaño completo.
            Image img = new Image(new ByteArrayInputStream(a.bytes()),
                    LADO_CELDA, LADO_CELDA, true, false);
            if (img.isError() || img.getWidth() <= 0 || img.getHeight() <= 0) {
                throw new IOException("no se pudo leer la imagen " + a.nombre());
            }
            fuentes.add(img);
        }

        int[] reticula = distribucion(fuentes.size());
        int columnas = reticula[0];
        int filas = reticula[1];
        int ancho = columnas * LADO_CELDA + (columnas - 1) * HUECO;
        int alto = filas * LADO_CELDA + (filas - 1) * HUECO;

        BufferedImage lienzo = new BufferedImage(ancho, alto, BufferedImage.TYPE_INT_ARGB);
        Graphics2D g = lienzo.createGraphics();
        try {
            g.setRenderingHint(RenderingHints.KEY_INTERPOLATION,
                    RenderingHints.VALUE_INTERPOLATION_BILINEAR);
            g.setColor(FONDO);
            g.fillRect(0, 0, ancho, alto);
            for (int i = 0; i < fuentes.size(); i++) {
                BufferedImage celda = aBuffered(fuentes.get(i));
                int x = (i % columnas) * (LADO_CELDA + HUECO);
                int y = (i / columnas) * (LADO_CELDA + HUECO);
                // OJO: la sobrecarga de 10 argumentos recibe (dx1,dy1,dx2,dy2),
                // NO ancho/alto. Pasar LADO_CELDA como dx2 hace que la celda sea
                // un rectángulo VACÍO en cuanto x > 0, y solo se pinta la primera.
                g.drawImage(celda, x, y, x + LADO_CELDA, y + LADO_CELDA,
                        0, 0, celda.getWidth(), celda.getHeight(), null);
            }
        } finally {
            g.dispose();
        }

        ByteArrayOutputStream salida = new ByteArrayOutputStream();
        if (!ImageIO.write(lienzo, "png", salida)) {
            throw new IOException("no hay codificador PNG disponible");
        }
        return new Resultado(salida.toByteArray(),
                "montaje_" + fuentes.size() + "_imagenes.png", columnas, filas);
    }

    /**
     * Retícula para n imágenes: hasta 3 columnas y sin celdas huérfanas.
     * <p>
     * Puro y aparte para poder testear la aritmética sin decodificar nada.
     * </p>
     *
     * @return {@code [columnas, filas]}
     */
    public static int[] distribucion(int n) {
        if (n < 1) {
            throw new IllegalArgumentException("n debe ser positivo");
        }
        if (n == 1) {
            return new int[]{1, 1};
        }
        if (n == 3) {
            return new int[]{3, 1};   // una fila se lee mejor que 2x2 con un hueco
        }
        int columnas = Math.min(COLUMNAS_MAX, n <= 4 ? 2 : 3);
        int filas = (n + columnas - 1) / columnas;
        return new int[]{columnas, filas};
    }

    /** JavaFX Image -> BufferedImage. La celda ya viene reducida a LADO_CELDA. */
    private static BufferedImage aBuffered(Image src) {
        int ancho = (int) Math.round(src.getWidth());
        int alto = (int) Math.round(src.getHeight());
        BufferedImage destino = new BufferedImage(ancho, alto, BufferedImage.TYPE_INT_ARGB);
        PixelReader lector = src.getPixelReader();
        for (int y = 0; y < alto; y++) {
            for (int x = 0; x < ancho; x++) {
                destino.setRGB(x, y, lector.getArgb(x, y));
            }
        }
        return destino;
    }
}