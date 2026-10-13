using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Mensajeria.Transversal
{
    /// <summary>
    /// UIX-24: respuesta citada, como TEXTO PLANO (invariante 9: sin vínculo
    /// estructurado en la red). Réplica de <c>FormatoRespuesta.java</c>
    /// (prefijo) y <c>MensajeCell.extraerCita</c> (separación), para que el
    /// mismo mensaje se pinte igual en los 3 clientes:
    /// <c>Respuesta a {identidad}: {referencia}\n\n{cuerpo}</c>.
    ///
    /// El extracto va SIEMPRE en una sola línea (Java usa <c>unaLinea</c>): si
    /// el texto original trae saltos, el separador <c>\n\n</c> de la respuesta
    /// se confundiría con los del propio extracto y el cuerpo quedaría pegado.
    /// </summary>
    public static class FormatoCita
    {
        /// <summary>Prefijo literal del contrato; lo leen Java, Python y .NET.</summary>
        public const string Referencia = "Respuesta a ";

        /// <summary>Tope del extracto citado (Java: <c>FormatoRespuesta.MAX_CITA</c>).</summary>
        public const int MaxCita = 120;

        /// <summary>Tope del bloque de cita al separar (Java: <c>MensajeCell.CITA_CORTE</c>).</summary>
        public const int Corte = 300;

        private static readonly Regex Espacios = new(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Prefijo que viaja por la red. Termina siempre en <c>"\n\n"</c> para
        /// que el cuerpo se pinte debajo de la cita, como en WhatsApp.
        /// </summary>
        /// <param name="identidad">Nombre visible de quien escribió el original.</param>
        /// <param name="contenido">Texto original (se colapsa a una línea).</param>
        /// <param name="esImagen">Solo <c>MENSAJE_IMAGEN</c> usa <c>[Imagen: …]</c>.</param>
        /// <param name="nombreArchivo">Nombre del adjunto, si es imagen.</param>
        public static string Prefijo(string? identidad, string? contenido, bool esImagen, string? nombreArchivo)
        {
            string referencia;
            if (esImagen)
            {
                string nombre = string.IsNullOrWhiteSpace(nombreArchivo) ? "imagen" : nombreArchivo!;
                referencia = "[Imagen: " + UnaLinea(nombre) + "]";
            }
            else
            {
                string texto = UnaLinea(string.IsNullOrEmpty(contenido) ? "mensaje" : contenido!);
                referencia = "\"" + Recortar(texto) + "\"";
            }
            return Referencia + (identidad ?? "") + ": " + referencia + "\n\n";
        }

        /// <summary>
        /// Separa el prefijo del cuerpo. Si no hay cita, todo el texto es cuerpo.
        /// Equivale a <c>MensajeCell.extraerCita</c>: la cita es lo anterior al
        /// primer salto doble, y solo si el texto empieza por <c>"Respuesta a "</c>
        /// y el corte no supera <see cref="Corte"/>.
        /// </summary>
        public static void Separar(string? contenido, out string cita, out string cuerpo)
        {
            cita = "";
            cuerpo = (contenido ?? "").Replace("\r\n", "\n");
            if (string.IsNullOrEmpty(cuerpo)) return;
            int corte = cuerpo.IndexOf("\n\n", StringComparison.Ordinal);
            if (corte > 0 && corte < Corte && cuerpo.StartsWith(Referencia, StringComparison.Ordinal))
            {
                cita = cuerpo[..corte].Trim();
                cuerpo = cuerpo[(corte + 2)..].Trim();
            }
        }

        /// <summary>Colapsa todos los espacios y saltos en uno solo (Java <c>unaLinea</c>).</summary>
        private static string UnaLinea(string texto) => Espacios.Replace(texto, " ").Trim();

        /// <summary>
        /// Recorta a <see cref="MaxCita"/> unidades de texto, sin partir
        /// surrogates (Java <c>recortar</c> cuenta code points).
        /// </summary>
        private static string Recortar(string texto)
        {
            int[] limites = StringInfo.ParseCombiningCharacters(texto);
            if (limites.Length <= MaxCita) return texto;
            return texto[..limites[MaxCita - 1]] + "…";
        }
    }
}
