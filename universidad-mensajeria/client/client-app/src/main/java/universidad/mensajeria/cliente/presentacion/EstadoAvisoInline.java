package universidad.mensajeria.cliente.presentacion;

/**
 * UIX-26: el estado del aviso inline del chat, y sobre todo <b>cuándo se quita solo</b>.
 * <p>
 * Va en una clase propia, sin JavaFX, por el mismo motivo que en .NET
 * ({@code EstadoAvisorInline}): el controlador es JavaFX y no se puede probar sin
 * levantar el toolkit, y esta es justo la decisión que fallaba —un aviso que se
 * queda pegado tapando la conversación para siempre—.
 * </p>
 * <p>
 * <b>Todos los avisos se quitan solos</b>, cada uno con su plazo. El error tiene
 * bastante más margen (12 s) porque hay que leerlo, pero tampoco se queda: un
 * error permanente acaba siendo ruido. El ✕ sigue ahí para quien quiera
 * quitarlo antes o no mirar.
 * </p>
 */
public final class EstadoAvisoInline {

    /** Plazo de un "Listo". Corto: informa y no estorba. */
    public static final long DURACION_EXITO_MS = 4_000;
    /** Plazo de un aviso informativo. */
    public static final long DURACION_INFORMACION_MS = 6_000;
    /** Plazo de una advertencia. */
    public static final long DURACION_ADVERTENCIA_MS = 8_000;
    /** Plazo de un error: doble que el éxito, para que se lea. */
    public static final long DURACION_ERROR_MS = 12_000;

    private String mensaje = "";
    private TipoAviso tipo = TipoAviso.INFORMACION;
    private boolean visible;
    private long mostradoEn;

    public String mensaje() {
        return mensaje;
    }

    public TipoAviso tipo() {
        return tipo;
    }

    public boolean visible() {
        return visible;
    }

    public long mostradoEn() {
        return mostradoEn;
    }

    /** Muestra un aviso y arranca su cuenta atrás. Un texto vacío lo borra. */
    public void mostrar(String texto, TipoAviso nuevo, long ahora) {
        if (texto == null || texto.isBlank()) {
            limpiar();
            return;
        }
        this.mensaje = texto.strip();
        this.tipo = nuevo;
        this.visible = true;
        this.mostradoEn = ahora;
    }

    public void limpiar() {
        this.mensaje = "";
        this.tipo = TipoAviso.INFORMACION;
        this.visible = false;
        this.mostradoEn = 0;
    }

    /** true si este aviso se cierra solo. Todos, errores incluidos. */
    public boolean debeOcultarseAutomaticamente() {
        return true;
    }

    /** Plazo de este aviso en milisegundos. */
    public long duracionMs() {
        return switch (tipo) {
            case EXITO -> DURACION_EXITO_MS;
            case INFORMACION -> DURACION_INFORMACION_MS;
            case ADVERTENCIA -> DURACION_ADVERTENCIA_MS;
            case ERROR -> DURACION_ERROR_MS;
        };
    }

    /**
     * Milisegundos que faltan para cerrarlo, o {@code -1} si ya no hay nada que
     * cerrar. La vista solo mira el signo y el cero.
     */
    public long milisegundosRestantes(long ahora) {
        if (!visible || !debeOcultarseAutomaticamente()) {
            return -1;
        }
        return Math.max(0, duracionMs() - (ahora - mostradoEn));
    }
}