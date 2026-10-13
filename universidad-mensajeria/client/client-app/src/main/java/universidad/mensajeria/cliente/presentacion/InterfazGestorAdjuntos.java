package universidad.mensajeria.cliente.presentacion;

import universidad.mensajeria.cliente.transversal.records.MensajeLocal;

/**
 * Carga y descarga de adjuntos de la conversación visible (DIP).
 */
public interface InterfazGestorAdjuntos {

    boolean imagenFallida(String idMensaje);

    /**
     * UIX-27: los bytes se descargaron pero este cliente no sabe pintarlos
     * (un {@code .webp}, por ejemplo). No es lo mismo que no poder descargar:
     * reintentar no va a arreglarlo y la celda lo dice con otro texto.
     */
    boolean imagenIlegible(String idMensaje);

    /**
     * UIX-28: ruta del original ya descargado de una imagen, o {@code null} si
     * todavía no se ha descargado. El visor la usa para abrir a resolución real en
     * vez de ampliar la miniatura.
     */
    java.nio.file.Path originalDe(String idMensaje);

    int paginaRemota(String conversacion);

    Integer totalPaginasRemotas(String conversacion);

    void registrarPaginaRemota(String conversacion, int siguiente, int total);

    boolean hayMasHistorialRemoto(String conversacion);

    void registrarPaginaDeMensaje(String idMensaje, int pagina);

    void cambiarConversacion();

    void detener();

    void cargarImagenVisible(MensajeLocal item);

    void reintentarImagen(MensajeLocal item);

    void descargarArchivo(MensajeLocal item);
}
