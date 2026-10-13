package universidad.mensajeria.cliente.transversal.utilerias;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.awt.Graphics2D;
import java.awt.image.BufferedImage;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Locale;
import java.util.Set;
import javax.imageio.ImageIO;

/**
 * Convierte a PNG, SOLO para pintarlo, lo que este cliente no sabe decodificar
 * con su pila nativa (JavaFX).
 * <p>
 * El {@code .webp} es el caso de siempre: se envía tal cual (el servidor lo
 * filtra) y esta conversión solo sirve para la vista. Lo mismo aplica desde
 * FORMATOS-01 a AVIF, HEIC/HEIF y SVG: viajan como {@code MENSAJE_IMAGEN}, el
 * servidor los decodifica vía ImageMagick ({@code DecodificadorMagick}) y aquí
 * se llevan a PNG únicamente para miniatura y visor.
 * </p>
 * <p>
 * Los lectores los aportan por {@code META-INF/services}: {@code imageio-webp}
 * (TwelveMonkeys, BSD-3) cubre WebP. El SVG no es un stream que ImageIO entienda,
 * así que se rasteriza con {@code jsvg} (Apache-2) sobre un {@code Graphics2D}.
 * AVIF/HEIC no tienen lector ImageIO compatible con Java 21 (NightMonkeys exige
 * Java 22 o {@code --enable-preview}); para la vista caen al plan B del
 * servidor: ImageMagick vía im4java
 * ({@link DecodificadorMagickCliente}, con autodetección y ausencia tolerada).
 * </p>
 */
public final class TranscodificadorImagen {

    /** Extensiones (sin punto) que la vista convierte a PNG. */
    private static final Set<String> EXTENSIONES_CONVERSION =
            Set.of("webp", "avif", "heic", "heif", "svg");

    /** Lado máximo al que se rasteriza un SVG (un lienzo gigante no aporta). */
    private static final int LADO_MAXIMO_SVG = 1024;

    private TranscodificadorImagen() {
    }

    /** true si el archivo está en un formato que hay que convertir. */
    public static boolean necesitaConversion(String nombre) {
        return EXTENSIONES_CONVERSION.contains(TextoCliente.extension(nombre).toLowerCase(Locale.ROOT));
    }

    /**
     * Devuelve el PNG equivalente. Devuelve los bytes de entrada sin tocar si el
     * archivo no necesita conversión, para que el llamador no tenga que preguntar
     * dos veces.
     *
     * @throws IOException si los bytes no son una imagen legible
     */
    public static byte[] aPng(byte[] origen, String nombre) throws IOException {
        if (!necesitaConversion(nombre)) {
            return origen;
        }
        BufferedImage imagen = decodificar(origen, nombre);
        ByteArrayOutputStream salida = new ByteArrayOutputStream();
        if (!ImageIO.write(imagen, "png", salida)) {
            throw new IOException("no hay codificador PNG disponible");
        }
        return salida.toByteArray();
    }

    private static BufferedImage decodificar(byte[] origen, String nombre)
            throws IOException {
        if ("svg".equalsIgnoreCase(TextoCliente.extension(nombre))) {
            return rasterizarSvg(origen);
        }
        try (ByteArrayInputStream entrada = new ByteArrayInputStream(origen)) {
            BufferedImage imagen = ImageIO.read(entrada);
            if (imagen != null) {
                return imagen;
            }
        }
        // Igual que Imagenes.leer del servidor: un formato que YA tiene lector
        // en ImageIO (webp incluido) pero vino corrupto se queda con su error
        // de siempre; solo lo que ImageIO no conoce cae a ImageMagick.
        if (esFormatoDeImageIO(origen)) {
            throw new IOException("no se pudo leer " + nombre);
        }
        // FORMATOS-01: plan B igual que el servidor — ImageMagick cubre AVIF,
        // HEIC y cualquier formato que la pila del cliente no abra. Si no está
        // instalado, la IOException llega al placeholder de la burbuja.
        try {
            return DecodificadorMagickCliente.leer(origen);
        } catch (IOException magick) {
            throw new IOException("no se pudo leer " + nombre, magick);
        }
    }

    /**
     * Firmas que ImageIO ya decodifica por sí mismo (PNG, JPEG, GIF, BMP y
     * WebP por su marca en el offset 8): réplica de
     * {@code Imagenes.esFormatoDeImageIO} del servidor, sin sus clases.
     */
    private static boolean esFormatoDeImageIO(byte[] bytes) {
        if (bytes == null || bytes.length < 4) {
            return false;
        }
        int b0 = bytes[0] & 0xFF, b1 = bytes[1] & 0xFF,
                b2 = bytes[2] & 0xFF, b3 = bytes[3] & 0xFF;
        if (b0 == 0x89 && b1 == 0x50 && b2 == 0x4E && b3 == 0x47) {
            return true; // PNG
        }
        if (b0 == 0xFF && b1 == 0xD8 && b2 == 0xFF) {
            return true; // JPEG
        }
        if (b0 == 0x47 && b1 == 0x49 && b2 == 0x46 && b3 == 0x38) {
            return true; // GIF87a/GIF89a
        }
        if (b0 == 0x52 && b1 == 0x49 && b2 == 0x46 && b3 == 0x46) {
            return bytes.length >= 12 && bytes[8] == 0x57 && bytes[9] == 0x45
                    && bytes[10] == 0x42 && bytes[11] == 0x50; // WEBP
        }
        return b0 == 0x42 && b1 == 0x4D; // BMP
    }

    /**
     * Rasteriza un SVG con jsvg sobre un lienzo ARGB (la transparencia se
     * conserva). Con {@code width}/{@code height} ausentes jsvg no sabe qué
     * tamaño dar; se cuelga del viewBox y como último recurso usa 512 px.
     */
    private static BufferedImage rasterizarSvg(byte[] origen) throws IOException {
        Path temporal = Files.createTempFile("svg-", ".svg");
        try {
            Files.write(temporal, origen);
            com.github.weisj.jsvg.SVGDocument documento =
                    new com.github.weisj.jsvg.parser.SVGLoader()
                            .load(temporal.toUri().toURL());
            if (documento == null) {
                throw new IOException("no se pudo leer el SVG");
            }
            float ancho = documento.size().width;
            float alto = documento.size().height;
            if (ancho <= 0 || alto <= 0) {
                ancho = 512;
                alto = 512;
            }
            double escala = Math.min(1.0,
                    LADO_MAXIMO_SVG / (double) Math.max(ancho, alto));
            int lienzoAncho = Math.max(1, Math.round(ancho * (float) escala));
            int lienzoAlto = Math.max(1, Math.round(alto * (float) escala));
            BufferedImage lienzo = new BufferedImage(
                    lienzoAncho, lienzoAlto, BufferedImage.TYPE_INT_ARGB);
            Graphics2D g = lienzo.createGraphics();
            try {
                documento.render(null, g);
            } finally {
                g.dispose();
            }
            return lienzo;
        } catch (IOException e) {
            throw e;
        } catch (RuntimeException e) {
            throw new IOException("no se pudo leer el SVG", e);
        } finally {
            Files.deleteIfExists(temporal);
        }
    }

    /**
     * Nombre con el que viaje el archivo convertido: se cambia la extensión por
     * {@code .png} y se conserva el original, para que el receptor siga
     * reconociendo de qué archivo se trata.
     */
    public static String nombreConvertido(String nombre) {
        int punto = nombre.lastIndexOf('.');
        return (punto < 0 ? nombre : nombre.substring(0, punto)) + ".png";
    }
}
