package universidad.mensajeria.cliente.transversal.utilerias;

import java.util.Locale;
import java.util.Set;

/**
 * Texto del dominio del chat (conteo, extensiones, MIME, citas).
 * Centraliza la lógica antes duplicada en fachada, coordinador y controlador.
 */
public final class TextoCliente {

    /** Tope de adjunto aceptado por la UI (50 MiB). */
    public static final long TOPE_ADJUNTO_BYTES = 50L * 1024 * 1024;

    /** Ancho del corte de cita de respuesta (150 + "…"). */
    public static final int CORTE_CITA = 150;

    /** R1: prefijo del preview cuando el último mensaje lo escribió la sesión activa. */
    public static final String PREFIYO_PROPIO = "Tú: ";

    /** Ancho máximo del preview de la fila de recientes (54 con el "…"). */
    public static final int CORTE_PREVIEW = 54;

    /**
     * R3: la MISMA lista de extensiones que el filtro "Imágenes" del selector de
     * archivos, y la MISMA que el servidor acepta en su tubería
     * (Imagenes.esImagenValida): PNG/JPEG/GIF/BMP, WebP, AVIF, HEIC/HEIF y SVG
     * (FORMATOS-01). Antes cada sitio tenía la suya y el comando de envío solo
     * aceptaba cuatro: un .bmp o un .webp acababa como archivo, no como imagen.
     */
    private static final Set<String> EXT_IMAGEN = Set.of(
            "png", "jpg", "jpeg", "gif", "bmp", "webp",
            "avif", "heic", "heif", "svg");

    private TextoCliente() {
    }

    public static int contarPalabras(String texto) {
        if (texto == null || texto.isBlank()) {
            return 0;
        }
        return texto.trim().split("\\s+").length;
    }

    public static String extension(String nombre) {
        int punto = nombre == null ? -1 : nombre.lastIndexOf('.');
        return punto < 0 ? "" : nombre.substring(punto + 1).toLowerCase(Locale.ROOT);
    }

    public static String mimeDe(String nombre) {
        return switch (extension(nombre)) {
            case "png" -> "image/png";
            case "jpg", "jpeg" -> "image/jpeg";
            case "gif" -> "image/gif";
            case "bmp" -> "image/bmp";
            case "webp" -> "image/webp";
            case "avif" -> "image/avif";
            case "heic" -> "image/heic";
            case "heif" -> "image/heif";
            case "svg" -> "image/svg+xml";
            case "pdf" -> "application/pdf";
            case "txt" -> "text/plain";
            case "zip" -> "application/zip";
            case "docx" -> "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            case "xlsx" -> "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            case "pptx" -> "application/vnd.openxmlformats-officedocument.presentationml.presentation";
            default -> "application/octet-stream";
        };
    }

    public static String truncarCita(String cita) {
        if (cita == null) {
            return "";
        }
        String recorte = cita.trim();
        return recorte.length() > CORTE_CITA
                ? recorte.substring(0, CORTE_CITA - 3) + "…"
                : recorte;
    }

    /**
     * R3: true si el nombre corresponde a una imagen (misma lista que el filtro
     * "Imágenes" del selector). Única fuente: el compositor, el planificador y el
     * comando de envío deben decidir igual.
     */
    public static boolean esImagenNombre(String nombre) {
        return EXT_IMAGEN.contains(extension(nombre));
    }

    /**
     * R1: texto del preview de la fila de recientes.
     * <p>
     * Puro a propósito: {@code UsuarioCell} es JavaFX y no se puede testear sin
     * levantar el toolkit; esta función sí. El prefijo va DESPUÉS de truncar,
     * igual que en el cliente .NET ({@code UsuarioVM.TextoPreview}), para que
     * ambos clientes recorten el mensaje en el mismo punto.
     * </p>
     *
     * @param contenido último mensaje de la conversación (puede venir vacío)
     * @param propio    {@code true} si lo escribió la sesión activa
     */
    public static String previoConversacion(String contenido, boolean propio) {
        // Sin mensaje no hay autor: nunca "Tú: Sin mensajes aún".
        // El literal vive en TextosVentana (VIS-02, mismo paquete).
        if (contenido == null || contenido.isBlank()) {
            return TextosVentana.SIN_MENSAJES_FILA;
        }
        String base = contenido.replaceAll("\\s+", " ").trim();
        if (base.isEmpty()) {
            return TextosVentana.SIN_MENSAJES_FILA;
        }
        if (base.length() > CORTE_PREVIEW) {
            base = base.substring(0, CORTE_PREVIEW - 1) + "…";
        }
        return propio ? PREFIYO_PROPIO + base : base;
    }
}
