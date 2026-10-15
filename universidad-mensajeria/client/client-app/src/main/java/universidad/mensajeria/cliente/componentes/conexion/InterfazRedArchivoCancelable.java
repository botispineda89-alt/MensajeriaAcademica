package universidad.mensajeria.cliente.componentes.conexion;

import universidad.mensajeria.cliente.transversal.records.RespuestaRed;

import java.io.IOException;
import java.util.function.BooleanSupplier;

/** Capacidad interna del adaptador TCP para cancelar una subida fragmentada activa. */
public interface InterfazRedArchivoCancelable {
    RespuestaRed enviarArchivoFragmentado(String id, String remitente, String destinatario,
            String nombreArchivo, String mime, byte[] contenido, String caption,
            BooleanSupplier cancelado) throws IOException;
}
