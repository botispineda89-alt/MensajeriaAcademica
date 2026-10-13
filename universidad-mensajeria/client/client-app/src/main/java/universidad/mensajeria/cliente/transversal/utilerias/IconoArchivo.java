package universidad.mensajeria.cliente.transversal.utilerias;

import java.io.InputStream;
import java.util.Locale;
import java.util.Map;

import javafx.scene.image.Image;

/**
 * Icono por tipo de archivo de un adjunto.
 * <p>
 * Un PDF, una hoja de cálculo o un JSON se distinguen de un vistazo: antes los
 * tres clientes pintaban el mismo {@code 📎} genérico para cualquier archivo, y
 * el usuario tenía que leer el nombre para saber qué le habían mandado.
 * </p>
 * <p>
 * Los PNG son los mismos en los tres clientes (JavaFX, PySide6 y WPF) y para la
 * misma extensión se ve el mismo dibujo. La extensión que no está en el catálogo
 * devuelve {@code null}, y quien pinta sigue con el {@code 📎} de siempre: el
 * cambio es aditivo, ningún archivo se queda sin representación.
 * </p>
 */
public final class IconoArchivo {

    /** Carpeta de classpath donde viven los PNG. */
    public static final String CARPETA = "/iconos/";

    /** Ancho del icono en la burbuja, estilo tarjeta de archivo de Telegram Web. */
    public static final int ANCHO_BURBUJA = 48;

    /** Ancho con el que se pide el icono en la bandeja de adjuntos. */
    public static final int ANCHO_BANDEJA = 48;

    /**
     * Extensión (en minúsculas, sin punto) a archivo del catálogo. Solo entran
     * las cinco familias para las que hay dibujo; {@code .pptx} o {@code .zip}
     * se quedan fuera a propósito en vez de reutilizar un icono que miente.
     */
    private static final Map<String, String> POR_EXTENSION = Map.ofEntries(
            Map.entry("pdf", "pdf"),
            Map.entry("xlsx", "xlsx"),
            Map.entry("xls", "xlsx"),
            Map.entry("csv", "csv"),
            Map.entry("json", "json"),
            Map.entry("doc", "docs"),
            Map.entry("docx", "docs"),
            Map.entry("odt", "docs"),
            Map.entry("rtf", "docs"),
            Map.entry("txt", "docs"),
            Map.entry("md", "docs"));

    private IconoArchivo() {
    }

    /**
     * Nombre del icono del catálogo ({@code pdf}, {@code xlsx}, {@code csv},
     * {@code json} o {@code docs}), o {@code null} si esa extensión no tiene
     * dibujo propio.
     *
     * @param nombreArchivo nombre o ruta del adjunto; {@code null} se admite
     */
    public static String clave(String nombreArchivo) {
        String extension = extension(nombreArchivo);
        return extension == null ? null : POR_EXTENSION.get(extension);
    }

    /**
     * Ruta de classpath del icono, o {@code null} si no hay. Se comprueba que el
     * recurso exista de verdad: así un PNG que no se empaquetó se ve como
     * {@code 📎} y no como una imagen rota.
     */
    public static String recurso(String nombreArchivo) {
        return recursoDe(clave(nombreArchivo));
    }

    /** Igual que {@link #recurso(String)} partiendo de la clave del catálogo. */
    public static String recursoDe(String clave) {
        if (clave == null) {
            return null;
        }
        String ruta = CARPETA + clave + ".png";
        return IconoArchivo.class.getResource(ruta) == null ? null : ruta;
    }

    /**
     * Icono ya decodificado al ancho pedido, o {@code null} si no hay icono para
     * esa extensión. Se pide a resolución reducida ({@code ancho}) para no
     * decodificar 512x512 por cada celda que se recicla.
     */
    public static Image imagen(String nombreArchivo, int ancho) {
        String ruta = recurso(nombreArchivo);
        if (ruta == null) {
            return null;
        }
        try (InputStream entrada = IconoArchivo.class.getResourceAsStream(ruta)) {
            if (entrada == null) {
                return null;
            }
            Image imagen = new Image(entrada, ancho, 0, true, true);
            return imagen.isError() ? null : imagen;
        } catch (Exception sinIcono) {
            return null;
        }
    }

    /** Extensión en minúsculas sin el punto, o {@code null} si no hay ninguna. */
    private static String extension(String nombreArchivo) {
        if (nombreArchivo == null) {
            return null;
        }
        int punto = nombreArchivo.lastIndexOf('.');
        if (punto < 0 || punto == nombreArchivo.length() - 1) {
            return null;
        }
        return nombreArchivo.substring(punto + 1).toLowerCase(Locale.ROOT);
    }
}
