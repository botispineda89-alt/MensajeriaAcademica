package universidad.mensajeria.cliente.presentacion;

/**
 * §7 paridad: el avance de un envío por lotes, en bytes.
 * <p>
 * Réplica de {@code Transversal/ProgresoEnvio.cs} de .NET y del diccionario que
 * emite {@code EnviarMensajeComando} de Python: los tres clientes miden lo mismo
 * (bytes enviados sobre bytes totales del lote) para que la barra se comporte
 * igual en cualquiera.
 * </p>
 * <p>
 * <b>Granularidad:</b> .NET y Python reciben un aviso por cada parte del
 * protocolo fragmentado, así que su barra avanza dentro de un archivo grande.
 * El cliente Java no expone ese callback hacia arriba (exigiría tocar
 * {@code client-interfaces} y el contrato de puertos), así que informa al
 * cerrar cada adjunto del lote. La métrica es la misma; el grano, más grueso.
 * </p>
 *
 * @param indiceActual     adjunto en curso, base 0; {@code total} si ya terminó
 * @param totalArchivos    cuántos adjuntos tiene el lote
 * @param nombreActual     nombre del adjunto en curso, o {@code ""}
 * @param bytesEnviados    bytes ya enviados del lote
 * @param bytesTotales     bytes que suma el lote
 */
public record ProgresoEnvio(int indiceActual, int totalArchivos, String nombreActual,
                            long bytesEnviados, long bytesTotales) {

    /** Fracción global del lote, en 0..1. Sin bytes que medir vale 0. */
    public double fraccionGlobal() {
        return bytesTotales <= 0 ? 0d : Math.clamp((double) bytesEnviados / bytesTotales, 0d, 1d);
    }

    /**
     * El texto de la barra, el mismo en los tres clientes:
     * {@code "Enviando i/n · X,0/Y,0 MB · nombre"}. Vive aquí, y no en la vista,
     * para que se pueda comprobar sin levantar el toolkit de JavaFX.
     */
    public String texto() {
        return "Enviando " + indiceActual + "/" + totalArchivos
                + " · " + unaDecimal(bytesEnviados / MEGA) + "/" + unaDecimal(bytesTotales / MEGA)
                + " MB" + (nombreActual == null || nombreActual.isBlank()
                        ? "" : " · " + nombreActual);
    }

    private static final double MEGA = 1024d * 1024d;

    private static String unaDecimal(double valor) {
        return String.format(java.util.Locale.ROOT, "%.1f", valor);
    }
}