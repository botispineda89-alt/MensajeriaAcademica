package universidad.mensajeria.cliente.transversal.utilerias;

import universidad.mensajeria.cliente.transversal.excepciones.ExcepcionValidacion;

/**
 * Validaciones de entrada del cliente (puras, sin red ni UI: testeables).
 * Mismas reglas que el servidor exige (TextoCommand/ImagenCommand/ArchivoCommand).
 */
public final class ValidacionCliente {

    private ValidacionCliente() {
    }

    public static void codigo(String codigo) {
        if (codigo == null || codigo.isBlank()) {
            throw new ExcepcionValidacion("el codigo es obligatorio");
        }
    }

    public static void contrasena(String contrasena) {
        if (contrasena == null || contrasena.length() < 6) {
            throw new ExcepcionValidacion("la contrasena debe tener al menos 6 caracteres");
        }
    }

    public static void contenido(String contenido) {
        if (contenido == null || contenido.isBlank()) {
            throw new ExcepcionValidacion("el contenido no puede estar vacio");
        }
    }

    public static void destinatario(String destinatario) {
        if (destinatario == null || destinatario.isBlank()) {
            throw new ExcepcionValidacion("elige un destinatario");
        }
    }

    /** Tope de una imagen en trama única (invariante 2): misma regla y valor
     * que el cliente .NET (CatalogoAdjuntos.MaxBytesImagenTramaUnica) y el
     * cliente Python (IMAGEN_MAX_BYTES). El base64 infla ×4/3 y la trama
     * resultante tiene que caber en los 16 MiB que valida el servidor;
     * mandarla de todos modos corta la conexión. */
    public static final long MAXIMO_IMAGEN_TRAMA = 12L * 1024 * 1024;

    public static void imagen(byte[] bytes) {
        if (bytes == null || bytes.length == 0) {
            throw new ExcepcionValidacion("la imagen esta vacia");
        }
        if (bytes.length > MAXIMO_IMAGEN_TRAMA) {
            throw new ExcepcionValidacion(String.format(
                    "la imagen supera el límite de envío directo de 12 MB (%.1f MB)",
                    bytes.length / 1024.0 / 1024.0));
        }
    }
}
