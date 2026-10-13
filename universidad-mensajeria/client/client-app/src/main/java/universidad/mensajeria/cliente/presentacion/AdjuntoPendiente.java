package universidad.mensajeria.cliente.presentacion;

import java.util.Locale;

import universidad.mensajeria.cliente.transversal.utilerias.TextoCliente;

/**
 * R3: un archivo pendiente de enviar, ya leído en memoria.
 * <p>
 * Inmutable a propósito: el compositor lo muestra en una tira de miniaturas y
 * el comando lo clona al construir el plan, así que nadie debe poder mutarlo
 * por el camino. El compacto del constructor deja la validación de tamaño y
 * nombre en un solo sitio, en lugar de repetirla en cada carga.
 * </p>
 */
public record AdjuntoPendiente(String nombre, String mime, byte[] bytes) {

    /** Tope por archivo (el mismo que ya usaba el compositor de un solo adjunto). */
    public static final long TOPE_BYTES = TextoCliente.TOPE_ADJUNTO_BYTES;

    /**
     * Tope de archivos por envío. Los bytes viven en memoria y el coordinador los
     * codifica en Base64 (×1,33) al enviar: 10 × 50 MiB serían ~667 MiB. Con
     * 5 el peor caso baja a ~333 MiB.
     */
    public static final int MAXIMO_ARCHIVOS = 5;

    public AdjuntoPendiente {
        if (nombre == null || nombre.isBlank()) {
            throw new IllegalArgumentException("el adjunto necesita nombre");
        }
        if (bytes == null || bytes.length == 0) {
            throw new IllegalArgumentException("el adjunto está vacío: " + nombre);
        }
        if (bytes.length > TOPE_BYTES) {
            throw new IllegalArgumentException("el adjunto supera 50 MiB: " + nombre);
        }
        if (mime == null || mime.isBlank()) {
            mime = TextoCliente.mimeDe(nombre);
        }
    }

    /** true si el archivo es una imagen (misma lista que el filtro del selector). */
    public boolean esImagen() {
        return TextoCliente.esImagenNombre(nombre);
    }

    /** Tamaño legible para la etiqueta de resumen ("1,2 MiB"). */
    public String tamanoLegible() {
        return legible(bytes.length);
    }

    static String legible(long bytes) {
        double mb = bytes / (1024.0 * 1024.0);
        return mb >= 1 ? String.format(Locale.ROOT, "%.1f MiB", mb)
                : Math.max(1, bytes / 1024) + " KiB";
    }
}