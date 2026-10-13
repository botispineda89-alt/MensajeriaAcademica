namespace Mensajeria.Transversal.Dtos
{
    /// <summary>
    /// Fila de <c>historial_local</c> (18 componentes). Espejo de
    /// <c>transversal/records/MensajeLocal.java</c>; el contrato es
    /// <c>contrato/puertos.json</c> §records.
    ///
    /// <para><b><c>Destino == "*"</c></b> marca una difusión propia.</para>
    ///
    /// <para>Los <c>string</c> son nulables porque en Java todos lo son (y Gson los
    /// omite): <c>contenido</c> es nulo en un adjunto y <c>numCaracteres</c> lo es en
    /// una difusión. <c>Integer</c>/<c>Long</c> del contrato son <c>int?</c>/<c>long?</c>
    /// aquí, no 0.</para>
    /// </summary>
    public sealed record MensajeLocal(
        string? IdMensaje,
        string? Origen,
        string? Destino,
        string? Tipo,
        string? Contenido,
        string? HashSha256,
        int? NumCaracteres,
        int? NumPalabras,
        string? NombreArchivo,
        string? RutaArchivo,
        long? TamanoArchivo,
        string? FechaEnvio,
        bool Enviado,
        bool Descargado,
        string? Estado,
        string? ArchivoId,
        string? EtapasFiltrado = null,
        /// <summary>
        /// Pie de imagen o archivo, en la MISMA burbuja; null = sin pie.
        /// Para un adjunto el <c>contenido</c> es el pie y los bytes van aparte.
        /// </summary>
        string? Caption = null)
    {
        /// <summary>
        /// True si hay pie visible. Espejo de <c>MessagesLocal.tieneCaption()</c>
        /// del cliente Java: solo los adjuntos llevan pie.
        /// </summary>
        public bool TieneCaption() =>
            (Tipo == "MENSAJE_IMAGEN" || Tipo == "MENSAJE_ARCHIVO")
            && !string.IsNullOrWhiteSpace(Caption);
    }
}
