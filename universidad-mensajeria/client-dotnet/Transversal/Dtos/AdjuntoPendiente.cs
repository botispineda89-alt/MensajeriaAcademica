namespace Mensajeria.Transversal.Dtos
{
    /// <summary>Archivo seleccionado para un lote de envío.</summary>
    public sealed record AdjuntoPendiente(
        string Ruta,
        string Nombre,
        long Tamano,
        bool EsImagen,
        string Mime);
}
