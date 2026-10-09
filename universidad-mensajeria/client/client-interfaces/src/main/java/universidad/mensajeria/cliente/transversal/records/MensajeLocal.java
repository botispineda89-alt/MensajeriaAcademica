package universidad.mensajeria.cliente.transversal.records;

/**
 * Fila de historial_local (§5.2): copia local del mensaje enviado o recibido.
 * destino "*" marca un broadcast propio (sin destinatario único).
 */
public record MensajeLocal(
        String idMensaje,
        String origen,
        String destino,
        String tipo,
        String contenido,
        String hashSha256,
        Integer numCaracteres,
        Integer numPalabras,
        String nombreArchivo,
        String rutaArchivo,
        Long tamanoArchivo,
        String fechaEnvio,
        boolean enviado,
        boolean descargado,
        String estado,
        String archivoId,
        String etapasFiltrado,
        /** Pie/caption de imagen o archivo (misma burbuja); null = sin pie. */
        String caption) {

    /** Compatibilidad: sin pie explícito asume null. */
    public MensajeLocal(String idMensaje, String origen, String destino, String tipo,
            String contenido, String hashSha256, Integer numCaracteres, Integer numPalabras,
            String nombreArchivo, String rutaArchivo, Long tamanoArchivo, String fechaEnvio,
            boolean enviado, boolean descargado, String estado, String archivoId,
            String etapasFiltrado) {
        this(idMensaje, origen, destino, tipo, contenido, hashSha256, numCaracteres,
                numPalabras, nombreArchivo, rutaArchivo, tamanoArchivo, fechaEnvio,
                enviado, descargado, estado, archivoId, etapasFiltrado, null);
    }

    /** Compatibilidad con filas que aún no incluyen metadatos del pipeline de imagen. */
    public MensajeLocal(String idMensaje, String origen, String destino, String tipo,
            String contenido, String hashSha256, Integer numCaracteres, Integer numPalabras,
            String nombreArchivo, String rutaArchivo, Long tamanoArchivo, String fechaEnvio,
            boolean enviado, boolean descargado, String estado, String archivoId) {
        this(idMensaje, origen, destino, tipo, contenido, hashSha256, numCaracteres,
                numPalabras, nombreArchivo, rutaArchivo, tamanoArchivo, fechaEnvio,
                enviado, descargado, estado, archivoId, null, null);
    }

    /** Compatibilidad con filas anteriores sin metadatos de archivo. */
    public MensajeLocal(
            String idMensaje,
            String origen,
            String destino,
            String tipo,
            String contenido,
            String hashSha256,
            Integer numCaracteres,
            Integer numPalabras,
            String nombreArchivo,
            String rutaArchivo,
            Long tamanoArchivo,
            String fechaEnvio,
            boolean enviado,
            boolean descargado,
            String estado) {
        this(idMensaje, origen, destino, tipo, contenido, hashSha256, numCaracteres,
                numPalabras, nombreArchivo, rutaArchivo, tamanoArchivo, fechaEnvio,
                enviado, descargado, estado, null, null, null);
    }

    /** Compatibilidad: sin estado explícito asume ENVIADO. */
    public MensajeLocal(
            String idMensaje,
            String origen,
            String destino,
            String tipo,
            String contenido,
            String hashSha256,
            Integer numCaracteres,
            Integer numPalabras,
            String nombreArchivo,
            String rutaArchivo,
            Long tamanoArchivo,
            String fechaEnvio,
            boolean enviado,
            boolean descargado) {
        this(idMensaje, origen, destino, tipo, contenido, hashSha256, numCaracteres,
                numPalabras, nombreArchivo, rutaArchivo, tamanoArchivo, fechaEnvio,
                enviado, descargado, "ENVIADO", null, null, null);
    }

    /** Copia con pie/caption (misma burbuja); blanco se normaliza a null. */
    public MensajeLocal conCaption(String pie) {
        String normalizado = pie == null || pie.isBlank() ? null : pie;
        return new MensajeLocal(idMensaje, origen, destino, tipo, contenido, hashSha256,
                numCaracteres, numPalabras, nombreArchivo, rutaArchivo, tamanoArchivo,
                fechaEnvio, enviado, descargado, estado, archivoId, etapasFiltrado,
                normalizado);
    }

    /** True si hay pie visible (imagen o archivo con texto debajo). */
    public boolean tieneCaption() {
        return ("MENSAJE_IMAGEN".equals(tipo) || "MENSAJE_ARCHIVO".equals(tipo))
                && caption != null && !caption.isBlank();
    }
}
