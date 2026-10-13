package universidad.mensajeria.utilerias.imagen;

import javax.imageio.ImageIO;
import java.awt.Graphics2D;
import java.awt.image.BufferedImage;
import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Set;

/**
 * Operaciones puras de imagen (JDK, sin dependencias). Cada transformacion
 * devuelve una NUEVA imagen; la escritura va al directorio de trabajo.
 */
public final class Imagenes {

    /**
     * Marcas mayores de un contenedor ISO-BMFF que son imagen y no video. La
     * marca va en los bytes 8..11, despues de {@code <tamano>ftyp}. {@code mif1}
     * y {@code msf1} son el HEIF generico: lo declaran tanto AVIF como HEIC, asi
     * que aceptarlos es aceptar ambos.
     */
    private static final Set<String> MARCAS_ISO_BMFF = Set.of(
            "avif", "avis",
            "heic", "heix", "heim", "heis",
            "hevc", "hevx", "hevm", "hevs",
            "mif1", "msf1");

    private Imagenes() {
    }

    /**
     * FASE 11.2: valida magic bytes antes de la tuberia (PNG, JPEG, GIF, BMP,
     * WebP, AVIF, HEIC/HEIF y SVG). Rechaza texto/binarios disfrazados sin llegar
     * a decodificar la imagen.
     * <p>
     * UIX-27: WebP entra porque los tres clientes lo ofrecen en su filtro y lo
     * mandan como {@code MENSAJE_IMAGEN}. Antes se rechazaba aqui y el usuario
     * perdia la foto; ahora que pasa el filtro, la tuberia tambien lo sabe
     * decodificar (el lector de {@code imageio-webp} va en el classpath de este
     * mismo módulo).
     * </p>
     * <p>
     * Esta comprobacion es aditiva: los formatos que ya pasaban siguen pasando y
     * los que ya se rechazaban siguen rechazandose. Los formatos nuevos no los
     * decodifica el JDK; de eso se encarga {@link DecodificadorMagick} dentro de
     * {@link #leer(Path)}. Para habilitar otro formato bastan dos cosas: anadir
     * su firma aqui y confiar en que ImageMagick sepa abrirlo.
     * </p>
     */
    public static boolean esImagenValida(byte[] bytes) {
        if (bytes == null || bytes.length < 4) {
            return false;
        }
        return esFormatoDeImageIO(bytes) || esIsoBmffDeImagen(bytes) || esSvg(bytes);
    }

    /**
     * Firmas que el JDK (o {@code imageio-webp}) ya decodifican por si mismos.
     * Se mantiene aparte porque {@link #leer(Path)} la usa para no gastar un
     * proceso externo en un PNG/JPEG corrupto: esos siguen fallando como antes.
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
            // RIFF....WEBP: la marca WebP va en los bytes 8..11, no en los 4
            // primeros. Un RIFF de audio o de video NO es una imagen.
            return esWebp(bytes);
        }
        return b0 == 0x42 && b1 == 0x4D; // BMP
    }

    /**
     * Contenedor ISO-BMFF ({@code ftyp}) con marca de imagen: AVIF y HEIC/HEIF.
     * Se exige la marca y no solo {@code ftyp} porque un {@code .mp4} empieza
     * igual ({@code ftypisom}, {@code ftypmp42}) y no es una imagen.
     */
    private static boolean esIsoBmffDeImagen(byte[] bytes) {
        if (bytes.length < 12) {
            return false;
        }
        if (bytes[4] != 0x66 || bytes[5] != 0x74
                || bytes[6] != 0x79 || bytes[7] != 0x70) { // ftyp
            return false;
        }
        String marca = new String(bytes, 8, 4, StandardCharsets.ISO_8859_1);
        return MARCAS_ISO_BMFF.contains(marca);
    }

    /**
     * SVG: no tiene firma binaria, es XML de texto. Se mira cual es el primer
     * elemento real del documento, saltando BOM, la declaracion {@code <?xml ?>},
     * los comentarios y el {@code <!DOCTYPE>}. Asi un HTML con un {@code <svg>}
     * incrustado NO pasa (empezaria por {@code <html>} o {@code <!DOCTYPE html>}) ni
     * tampoco un PDF, y solo entra un SVG entero.
     */
    private static boolean esSvg(byte[] bytes) {
        String texto = new String(bytes, 0, Math.min(bytes.length, 4096),
                StandardCharsets.UTF_8);
        if (!texto.isEmpty() && texto.charAt(0) == '\uFEFF') {
            texto = texto.substring(1);
        }
        int i = 0;
        while (true) {
            while (i < texto.length() && Character.isWhitespace(texto.charAt(i))) {
                i++;
            }
            if (i >= texto.length() || texto.charAt(i) != '<') {
                return false;
            }
            int cierre;
            if (texto.startsWith("<?", i)) {
                cierre = texto.indexOf("?>", i);
                i = cierre < 0 ? texto.length() : cierre + 2;
            } else if (texto.startsWith("<!--", i)) {
                cierre = texto.indexOf("-->", i);
                i = cierre < 0 ? texto.length() : cierre + 3;
            } else if (texto.regionMatches(true, i, "<!doctype", 0, 9)) {
                cierre = texto.indexOf('>', i);
                i = cierre < 0 ? texto.length() : cierre + 1;
            } else {
                if (!texto.regionMatches(true, i, "<svg", 0, 4)) {
                    return false;
                }
                int siguiente = i + 4;
                return siguiente >= texto.length()
                        || esDelimitador(texto.charAt(siguiente));
            }
        }
    }

    /** Cierra un nombre de elemento en XML: espacio, {@code >} o {@code /}. */
    private static boolean esDelimitador(char c) {
        return Character.isWhitespace(c) || c == '>' || c == '/';
    }

    /**
     * true si el contenedor RIFF lleva la firma WEBP en el offset 8. Se comprueba
     * la firma y no solo "RIFF": un .wav o un .avi empiezan igual y no son
     * imágenes, y colarlos por la tubería sería justo el agujero que
     * FASE 11.2 quería cerrar.
     */
    private static boolean esWebp(byte[] bytes) {
        if (bytes.length < 12) {
            return false;
        }
        return bytes[8] == 0x57 && bytes[9] == 0x45
                && bytes[10] == 0x42 && bytes[11] == 0x50; // WEBP
    }

    /**
     * Abre una imagen de disco. Primero con {@code ImageIO}; si este no la
     * reconoce, con ImageMagick (AVIF, HEIC, SVG y demas formatos que el JDK no
     * trae). El contrato no cambia: {@code BufferedImage} o {@code IOException},
     * con el mismo mensaje de error de siempre cuando ninguna de las dos vias
     * puede abrirla.
     */
    public static BufferedImage leer(Path archivo) throws IOException {
        byte[] bytes = Files.readAllBytes(archivo);
        try (ByteArrayInputStream in = new ByteArrayInputStream(bytes)) {
            BufferedImage imagen = ImageIO.read(in);
            if (imagen != null) {
                return imagen;
            }
        }
        if (esFormatoDeImageIO(bytes)) {
            // Formato que ya decodificaba el JDK pero el archivo esta corrupto:
            // mismo error que antes y sin arrancar un proceso para nada.
            throw new IOException("no es una imagen legible: " + archivo);
        }
        try {
            return DecodificadorMagick.leer(archivo);
        } catch (IOException externa) {
            IOException fallo = new IOException("no es una imagen legible: " + archivo);
            fallo.addSuppressed(externa);
            throw fallo;
        }
    }

    public static Path escribir(BufferedImage imagen, Path directorio, String nombre) throws IOException {
        Files.createDirectories(directorio);
        Path destino = directorio.resolve(nombre);
        try (OutputStream out = Files.newOutputStream(destino)) {
            ImageIO.write(imagen, "png", out);
        }
        return destino;
    }

    public static BufferedImage nuevaCopia(BufferedImage origen) {
        BufferedImage copia = new BufferedImage(
                origen.getWidth(), origen.getHeight(), BufferedImage.TYPE_INT_RGB);
        Graphics2D g = copia.createGraphics();
        try {
            g.drawImage(origen, 0, 0, null);
        } finally {
            g.dispose();
        }
        return copia;
    }

    public static BufferedImage aGrises(BufferedImage origen) {
        BufferedImage salida = nuevaCopia(origen);
        for (int y = 0; y < salida.getHeight(); y++) {
            for (int x = 0; x < salida.getWidth(); x++) {
                int rgb = salida.getRGB(x, y);
                int r = (rgb >> 16) & 0xFF;
                int g = (rgb >> 8) & 0xFF;
                int b = rgb & 0xFF;
                int gris = (int) (0.299 * r + 0.587 * g + 0.114 * b);
                salida.setRGB(x, y, (gris << 16) | (gris << 8) | gris);
            }
        }
        return salida;
    }

    public static BufferedImage aSepia(BufferedImage origen) {
        BufferedImage salida = nuevaCopia(origen);
        for (int y = 0; y < salida.getHeight(); y++) {
            for (int x = 0; x < salida.getWidth(); x++) {
                int rgb = salida.getRGB(x, y);
                int r = (rgb >> 16) & 0xFF;
                int g = (rgb >> 8) & 0xFF;
                int b = rgb & 0xFF;
                int tr = Math.min(255, (int) (0.393 * r + 0.769 * g + 0.189 * b));
                int tg = Math.min(255, (int) (0.349 * r + 0.686 * g + 0.168 * b));
                int tb = Math.min(255, (int) (0.272 * r + 0.534 * g + 0.131 * b));
                salida.setRGB(x, y, (tr << 16) | (tg << 8) | tb);
            }
        }
        return salida;
    }

    public static BufferedImage girar(BufferedImage origen, int grados) {
        int pasos = ((grados % 360) + 360) % 360 / 90;
        BufferedImage actual = origen;
        for (int i = 0; i < pasos; i++) {
            actual = girar90(actual);
        }
        return actual == origen ? nuevaCopia(origen) : actual;
    }

    private static BufferedImage girar90(BufferedImage origen) {
        int w = origen.getWidth();
        int h = origen.getHeight();
        BufferedImage salida = new BufferedImage(h, w, BufferedImage.TYPE_INT_RGB);
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                salida.setRGB(h - 1 - y, x, origen.getRGB(x, y) & 0xFFFFFF);
            }
        }
        return salida;
    }

    public static BufferedImage ajustarBrillo(BufferedImage origen, double factor) {
        BufferedImage salida = nuevaCopia(origen);
        for (int y = 0; y < salida.getHeight(); y++) {
            for (int x = 0; x < salida.getWidth(); x++) {
                int rgb = salida.getRGB(x, y);
                int r = Math.min(255, Math.max(0, (int) (((rgb >> 16) & 0xFF) * factor)));
                int g = Math.min(255, Math.max(0, (int) (((rgb >> 8) & 0xFF) * factor)));
                int b = Math.min(255, Math.max(0, (int) ((rgb & 0xFF) * factor)));
                salida.setRGB(x, y, (r << 16) | (g << 8) | b);
            }
        }
        return salida;
    }

    public static BufferedImage reducir(BufferedImage origen, double factor) {
        int w = Math.max(1, (int) (origen.getWidth() * factor));
        int h = Math.max(1, (int) (origen.getHeight() * factor));
        BufferedImage salida = new BufferedImage(w, h, BufferedImage.TYPE_INT_RGB);
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                int sx = Math.min(origen.getWidth() - 1, (int) (x / factor));
                int sy = Math.min(origen.getHeight() - 1, (int) (y / factor));
                salida.setRGB(x, y, origen.getRGB(sx, sy) & 0xFFFFFF);
            }
        }
        return salida;
    }
}
