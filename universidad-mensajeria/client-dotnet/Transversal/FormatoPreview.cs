namespace Mensajeria.Transversal
{
    /// <summary>
    /// UIX-26: texto de la segunda línea de la lista de conversaciones
    /// (preview del último mensaje).
    /// <para>
    /// Va en una clase propia, sin WPF, por el mismo motivo que en Java
    /// (<c>TextoCliente.previoConversacion</c>): <c>UsuarioVM</c> es un
    /// <c>INotifyPropertyChanged</c> que los tests no compilan, así que la regla
    /// —incluido el prefijo "Tú: "— sería imposible de probar. Aquí es una
    /// función pura, y el texto coincide carácter a carácter con el de Java.
    /// </para>
    /// </summary>
    public static class FormatoPreview
    {
        /// <summary>Prefijo cuando el último mensaje lo escribió la sesión activa.</summary>
        public const string PREFIJO_PROPIO = "Tú: ";

        /// <summary>
        /// Lo que se ve cuando la conversación no tiene ningún mensaje.
        /// Vive en <see cref="TextosVentana"/> (VIS-02); aquí solo el alias.
        /// </summary>
        public const string SIN_MENSAJES = TextosVentana.SinMensajesFila;

        /// <summary>Ancho máximo del preview (54 con el "…").</summary>
        public const int CORTE = 54;

        /// <summary>
        /// Texto del preview.
        /// </summary>
        /// <param name="contenido">Último mensaje de la conversación; vacío si no hay.</param>
        /// <param name="propio">true si lo escribió la sesión activa.</param>
        public static string Preview(string? contenido, bool propio)
        {
            // Sin mensaje no hay autor: nunca "Tú: Sin mensajes aún".
            if (string.IsNullOrWhiteSpace(contenido)) return SIN_MENSAJES;
            string base_ = System.Text.RegularExpressions.Regex
                .Replace(contenido, @"\s+", " ").Trim();
            if (base_.Length == 0) return SIN_MENSAJES;
            // El prefijo va DESPUÉS de truncar, igual que en Java y en el
            // cliente Python, para que los tres recorten en el mismo punto.
            if (base_.Length > CORTE) base_ = base_[..(CORTE - 1)] + "…";
            return propio ? PREFIJO_PROPIO + base_ : base_;
        }

        /// <summary>Texto de la insignia: Java pinta el número pelado.</summary>
        public static string Insignia(int noLeidos) =>
            noLeidos > 0 ? noLeidos.ToString() : "";
    }
}