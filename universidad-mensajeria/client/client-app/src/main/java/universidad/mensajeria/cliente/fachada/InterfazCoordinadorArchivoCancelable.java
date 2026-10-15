package universidad.mensajeria.cliente.fachada;

import universidad.mensajeria.cliente.transversal.excepciones.ExcepcionCliente;

import java.util.function.BooleanSupplier;

/** Capacidad opcional del coordinador concreto para cancelar una transferencia activa. */
public interface InterfazCoordinadorArchivoCancelable {
    boolean enviarArchivo(String yo, String destinatario, byte[] bytes,
            String nombreArchivo, String mime, String caption, BooleanSupplier cancelado)
            throws ExcepcionCliente;
}
