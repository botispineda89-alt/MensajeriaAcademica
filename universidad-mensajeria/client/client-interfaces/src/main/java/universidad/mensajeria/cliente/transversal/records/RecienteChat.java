package universidad.mensajeria.cliente.transversal.records;

/** Fila de la lista de recientes: último mensaje + no leídos + presencia. */
public record RecienteChat(
        String otro,
        String ultimoContenido,
        String ultimoTipo,
        String fechaUltimo,
        int noLeidos,
        boolean conectado,
        /**
         * R1: true si el último mensaje lo escribió la sesión activa. El preview
         * de la lista lo antepone como "Tú: ". Viene del `origen` real de la
         * fila, así que el eco multi-PC (enviado desde otro PC del mismo código)
         * también cuenta como propio.
         */
        boolean ultimoPropio) {

    /** Fila sin conversación: no hay último mensaje y nadie lo escribió. */
    public static RecienteChat vacio(String otro, boolean conectado) {
        return new RecienteChat(otro, "", "", "", 0, conectado, false);
    }
}
