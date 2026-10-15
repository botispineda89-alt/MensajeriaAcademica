package universidad.mensajeria.cliente.fachada;

import universidad.mensajeria.cliente.transversal.excepciones.ExcepcionCliente;

import java.io.IOException;
import java.util.function.BooleanSupplier;

/** Capacidad de aplicación interna para propagar la cancelación del envío de archivo. */
public interface InterfazEnvioArchivoCancelable {
    boolean enviarArchivo(String destinatario, byte[] bytes, String nombreArchivo,
            String mime, String caption, BooleanSupplier cancelado)
            throws ExcepcionCliente, IOException;
}
