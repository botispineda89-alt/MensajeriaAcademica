namespace Mensajeria.Transversal
{
    /// <summary>
    /// UIX-28: qué botón de guardar lleva una burbuja.
    /// <para>
    /// Antes había un botón único ("⬇ Guardar") al final de la burbuja que salía
    /// para todos los mensajes, incluido el texto, y sin decir si lo que se iba
    /// a guardar era una foto o un PDF. Ahora son <b>dos</b>, y solo en imagen y
    /// archivo.
    /// </para>
    /// <para>
    /// Va en <c>Transversal/</c> y sin WPF a propósito, por el mismo motivo que
    /// <c>FormatoPreview</c>: <c>MensajeVM</c> es un <c>INotifyPropertyChanged</c>
    /// que los tests no compilan, así que la regla —que es lo que se quiere
    /// proteger— sería imposible de probar si viviera allí.
    /// </para>
    /// </summary>
    public static class BotonGuardar
    {
        /// <summary>Tipo de un mensaje que lleva una imagen.</summary>
        public const string TIPO_IMAGEN = "MENSAJE_IMAGEN";

        /// <summary>Tipo de un mensaje que lleva un documento.</summary>
        public const string TIPO_ARCHIVO = "MENSAJE_ARCHIVO";

        /// <summary>Texto del botón de una imagen. Distinto a propósito del otro.</summary>
        public const string TEXTO_IMAGEN = "⬇ Guardar imagen";

        /// <summary>Texto del botón de un documento.</summary>
        public const string TEXTO_ARCHIVO = "⬇ Guardar archivo";

        /// <summary>
        /// Normaliza el tipo. El servidor manda <c>IMAGEN</c>/<c>ARCHIVO</c> y el
        /// cliente guarda <c>MENSAJE_IMAGEN</c>/<c>MENSAJE_ARCHIVO</c>; los dos
        /// tienen que acabar igual aquí.
        /// </summary>
        public static string Normalizar(string? tipo)
        {
            string t = (tipo ?? "").Trim().ToUpperInvariant();
            return t switch
            {
                "IMAGEN" => TIPO_IMAGEN,
                "ARCHIVO" => TIPO_ARCHIVO,
                "TEXTO" => "MENSAJE_TEXTO",
                _ => t
            };
        }

        public static bool EsImagen(string? tipo) => Normalizar(tipo) == TIPO_IMAGEN;

        public static bool EsArchivo(string? tipo) => Normalizar(tipo) == TIPO_ARCHIVO;

        /// <summary>Texto del botón, o vacío si este mensaje no lleva ninguno.</summary>
        public static string Texto(string? tipo)
        {
            string t = Normalizar(tipo);
            if (t == TIPO_IMAGEN) return TEXTO_IMAGEN;
            if (t == TIPO_ARCHIVO) return TEXTO_ARCHIVO;
            return "";
        }

        /// <summary>
        /// true si este mensaje lleva botón de guardar. Un texto NO lo lleva:
        /// no hay nada que guardar y ahí es donde nacía el botón de más.
        /// </summary>
        public static bool LlevaBoton(string? tipo)
        {
            string t = Normalizar(tipo);
            return t == TIPO_IMAGEN || t == TIPO_ARCHIVO;
        }
    }
}