using System;
using System.Globalization;

namespace Mensajeria.Transversal
{
    /// <summary>
    /// UIX-03: formato de fecha unificado y relativo en los 3 clientes.
    ///
    /// Réplica exacta de <c>FormatoFecha.java</c> y <c>transversal/fechas.py</c>.
    /// Antes la burbuja mostraba <c>yyyy-MM-dd HH:mm</c> siempre; ahora la hora
    /// corta es relativa ("10:00" hoy, "02-01 10:00" este año) y el separador de
    /// día (UIX-04) pone el contexto. Contrato compartido (VIS-02).
    ///
    /// Acepta ISO con T, con espacio (SQLite) y con zona horaria.
    /// Lo ilegible devuelve "" en vez de romper la fila.
    /// </summary>
    public static class FormatoFecha
    {
        /// <summary>Hora corta para el pie de la burbuja.</summary>
        public static string HoraCorta(string? crudo)
        {
            if (!TryLeer(crudo, out var fecha)) return "";
            var hoy = DateTime.Today;
            string hora = fecha.ToString("HH:mm");
            if (fecha.Date == hoy) return hora;
            if (fecha.Year == hoy.Year) return fecha.ToString("dd-MM") + " " + hora;
            return fecha.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture) + " " + hora;
        }

        /// <summary>Etiqueta para el separador de día (UIX-04).</summary>
        public static string EtiquetaDia(string? crudo)
        {
            if (!TryLeer(crudo, out var fecha)) return "";
            var hoy = DateTime.Today;
            if (fecha.Date == hoy) return "Hoy";
            if (fecha.Date == hoy.AddDays(-1)) return "Ayer";
            if (fecha.Year == hoy.Year) return fecha.ToString("dd-MM");
            return fecha.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
        }

        /// <summary>Clave de agrupación por día (UIX-04): "" si ilegible.</summary>
        public static string ClaveDia(string? crudo)
        {
            return TryLeer(crudo, out var fecha) ? fecha.ToString("yyyy-MM-dd") : "";
        }

        /// <summary>
        /// Normaliza una fecha a UTC ISO-8601 con "Z", que es como el cliente
        /// guarda las suyas.
        /// <para>
        /// <b>Por qué hace falta:</b> el servidor manda <c>LocalDateTime.now()</c>,
        /// o sea <b>hora local sin zona</b> ("2026-10-10T22:14:51"), mientras que
        /// lo que envía este cliente es <b>UTC con Z</b> ("2026-10-11T03:14:19Z"
        /// para las 22:14:51 locales). Como <c>fecha_envio</c> es TEXT y las
        /// consultas ordenan por <c>fecha_envio DESC</c> —comparación de texto—,
        /// un mensaje que llega SIEMPRE queda ordered antes que uno que sale, por
        /// muy posterior que sea. El efecto visible era que el preview de la lista
        /// marcaba siempre "Tú:".
        /// </para>
        /// <para>
        /// Una fecha sin zona se interpreta como hora local del servidor y se
        /// convierte a UTC, que es la lectura correcta porque el servidor y el
        /// cliente están en la misma máquina en este despliegue. Si no se puede
        /// leer, se devuelve el texto tal cual para no perder el mensaje.
        /// </para>
        /// </summary>
        public static string AUtc(string? crudo)
        {
            if (string.IsNullOrWhiteSpace(crudo)) return "";
            if (DateTimeOffset.TryParse(crudo, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal, out var instante))
            {
                // `UtcDateTime` y no `ToUniversalTime()`: el "O" de un
                // DateTimeOffset escribe "+00:00" y el de un DateTime en UTC
                // escribe "Z", que es justo lo que leen las filas ya guardadas.
                return instante.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
            }
            return crudo.Trim();
        }

        private static bool TryLeer(string? crudo, out DateTime fecha)
        {
            fecha = default;
            if (string.IsNullOrWhiteSpace(crudo)) return false;
            // Cultura INVARIANTE a proposito: la fecha del protocolo es ISO
            // (`LocalDateTime.now()` en el servidor) y no debe depender de la
            // configuracion regional del PC. Con la cultura del equipo, una
            // fecha ambigua se podia leer con dia/mes invertidos.
            // Acepta "T", espacio (SQLite), microsegundos, zona y solo-fecha.
            if (DateTimeOffset.TryParse(crudo, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var instante))
            {
                fecha = instante.ToLocalTime().DateTime;
                return true;
            }
            return false;
        }
    }
}
