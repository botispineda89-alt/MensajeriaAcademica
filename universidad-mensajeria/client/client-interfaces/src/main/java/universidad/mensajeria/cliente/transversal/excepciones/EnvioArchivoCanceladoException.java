package universidad.mensajeria.cliente.transversal.excepciones;

/** Señala una cancelación explícita para que la cola offline no reenvíe el archivo. */
public final class EnvioArchivoCanceladoException extends RuntimeException {
    public EnvioArchivoCanceladoException() {
        super("envío cancelado por el usuario");
    }
}
