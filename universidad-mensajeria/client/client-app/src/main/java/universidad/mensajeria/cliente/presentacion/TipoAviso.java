package universidad.mensajeria.cliente.presentacion;

/**
 * Nivel del aviso inline del chat. El color lo pone el estilo de la vista, no
 * este enum: los CSS del cliente se generan desde el contrato de diseño y aquí
 * solo hace falta distinguir para elegir el plazo y el título.
 */
public enum TipoAviso {
    ERROR,
    ADVERTENCIA,
    INFORMACION,
    EXITO
}