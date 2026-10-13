using System.Collections.Generic;
using System.Linq;

namespace Mensajeria.Transversal.Dtos
{
    public enum EstadoEnvioAdjunto
    {
        Enviado,
        EnCola,
        Fallido,
        NoIntentado
    }

    public sealed record ResultadoEnvioAdjunto(
        string Nombre,
        EstadoEnvioAdjunto Estado,
        string? Motivo = null);

    /// <summary>Resumen individual y agregado de un lote de adjuntos.</summary>
    public sealed record ResultadoEnvioMultiple(
        IReadOnlyList<ResultadoEnvioAdjunto> Adjuntos,
        bool Cancelado)
    {
        public int Total => Adjuntos.Count;
        public int Enviados => Adjuntos.Count(a => a.Estado == EstadoEnvioAdjunto.Enviado);
        public int EnCola => Adjuntos.Count(a => a.Estado == EstadoEnvioAdjunto.EnCola);
        public int Fallidos => Adjuntos.Count(a => a.Estado == EstadoEnvioAdjunto.Fallido);
        public int NoIntentados => Adjuntos.Count(a => a.Estado == EstadoEnvioAdjunto.NoIntentado);
        public bool TodoEnviado => Fallidos == 0 && NoIntentados == 0 && !Cancelado;
    }
}
