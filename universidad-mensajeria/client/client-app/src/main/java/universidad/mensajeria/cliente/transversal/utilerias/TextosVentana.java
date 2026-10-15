package universidad.mensajeria.cliente.transversal.utilerias;

/**
 * Literales de ventana y diálogo en un solo sitio (VIS-02).
 *
 * <p>Fuente de verdad: {@code contrato/textos-ui.json} (que a su vez sale de
 * {@code DIAGRAMAS/textos-ui.md}). {@code contrato/verificar_textos_ui.py}
 * comprueba que esta clase contiene el texto exacto del contrato y que ningún
 * otro fuente lo repite inline (los tests sí pueden fijarlos).</p>
 */
public final class TextosVentana {

    private TextosVentana() {
    }

    /** Título de la ventana de login. */
    public static final String TITULO_LOGIN = "Mensajería Académica | Iniciar sesión";

    /**
     * Plantilla del título de la ventana de chat (P4, 2026-10-14): lleva la identidad de la
     * cuenta con la que se entró. Se rellena con {@link #tituloChat(String)}.
     */
    public static final String TITULO_CHAT = "Mensajería Académica | {identidad}";

    /**
     * Título del chat con la identidad ya resuelta ({@code Nombre Apellido [CÓDIGO]}).
     *
     * <p>Sin identidad (todavía no hay cuenta) se cae al nombre de la aplicación: un título
     * nunca debe mostrar el marcador {@code {identidad}} del contrato.</p>
     */
    public static String tituloChat(String identidad) {
        return identidad == null || identidad.isBlank()
                ? "Mensajería Académica" : TITULO_CHAT.replace("{identidad}", identidad);
    }

    /** Título del visor de imagen. */
    public static final String TITULO_VISOR = "Imagen";

    /** Título de los diálogos de salida. */
    public static final String CONFIRMAR_SALIDA = "Confirmar salida";

    /** Confirmación de volver al inicio. */
    public static final String CERRAR_SESION_PREGUNTA =
            "¿Deseas cerrar esta sesión y volver al inicio?";

    /** Confirmación de cerrar el login. */
    public static final String CERRAR_PROGRAMA_PREGUNTA = "¿Deseas cerrar el programa?";

    /** Confirmación de cerrar todas las sesiones. */
    public static final String SALIR_TODOS_PREGUNTA =
            "¿Cerrar todas las sesiones de esta cuenta?";

    /** Aviso tras expulsar otras sesiones. */
    public static final String OTRAS_SESIONES_ESTADO = "Se solicitó cerrar tus otras sesiones";

    /** Aviso de envío sin red (queda en cola). */
    public static final String SIN_RED = "Sin red: guardado para reintentar";

    /** Título del diálogo de guardado. */
    public static final String GUARDAR_ARCHIVO = "Guardar archivo";

    /** Aviso cuando la cita no está en el historial. */
    public static final String CITA_NO_DISPONIBLE =
            "El mensaje citado no está disponible en este historial.";

    /** Cabecera sin conversación. */
    public static final String SELECCIONA_CONVERSACION = "Selecciona una conversación";

    /**
     * Texto del botón de responder de la burbuja (el botón dibuja {@code ↩}; esto es el
     * tooltip y el texto accesible).
     */
    public static final String BOTON_RESPONDER = "Responder a este mensaje";

    /** Fila de contacto sin mensajes. */
    public static final String SIN_MENSAJES_FILA = "Sin mensajes aún";

    /** Placeholder de conversación vacía (guía UIX-13). */
    public static final String VACIO_CHAT = "Sin mensajes aún. Escribe el primero.";

    /** Botón de paginación del historial. */
    public static final String CARGAR_ANTERIORES = "↑ Cargar mensajes anteriores";

    /** Motivo por defecto al cerrar sesión remota (difiere de Python a propósito). */
    public static final String SESION_CERRADA_DEFECTO =
            "Tu sesión se cerró. Inicia sesión nuevamente.";
}
