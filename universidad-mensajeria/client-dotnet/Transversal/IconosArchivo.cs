using System;
using System.IO;

namespace Mensajeria.Transversal
{
    /// <summary>
    /// FORMATOS-02: icono por tipo de archivo de un adjunto. Réplica de
    /// <c>IconoArchivo.java</c> (JavaFX) y <c>transversal/iconos_archivo.py</c>
    /// (PySide6): la misma extensión pinta el mismo dibujo en los tres
    /// clientes.
    /// <para>
    /// Antes los tres clientes pintaban el mismo 📎 genérico para cualquier
    /// archivo, y el receptor tenía que leer el nombre para saber qué le
    /// habían mandado. La extensión que no está en el catálogo devuelve
    /// <c>null</c> y quien pinta sigue con el 📎 de siempre: el cambio es
    /// aditivo.
    /// </para>
    /// <para>
    /// Aquí SOLO hay clasificación y rutas, sin WPF: el proyecto de tests
    /// compila <c>Transversal/</c> para net10.0 sin WPF, así que meter
    /// <c>BitmapImage</c> en esta carpeta dejaría la suite sin compilar. La
    /// imagen ya decodificada la sirve <c>IconosArchivoWpf</c>.
    /// </para>
    /// </summary>
    public static class IconosArchivo
    {
        /// <summary>Carpeta de iconos junto al ejecutable.</summary>
        public const string Carpeta = "Iconos";

        /// <summary>
        /// Lado del icono dentro de una burbuja (DIP), al estilo de la tarjeta
        /// de archivo de Telegram Web. Es <c>double</c> y NO <c>int</c> porque
        /// el XAML lo consume con <c>x:Static</c> directo sobre
        /// <c>Width</c>/<c>Height</c>: con un <c>int</c> encajonado WPF no
        /// convierte y revienta con
        /// <c>InvalidCastException (Int32→Double)</c> en
        /// <c>FrameworkElement.MinMax..ctor</c> al medir cada burbuja.
        /// </summary>
        public const double LadoIconoBurbuja = 48.0;

        /// <summary>Lado del icono en la bandeja de adjuntos del compositor (DIP). Mismo motivo para el tipo.</summary>
        public const double LadoIconoBandeja = 48.0;

        /// <summary>
        /// Píxeles a los que se decodifica el PNG.
        /// <para>
        /// NO se decodifica al ancho en DIP: WPF convierte píxeles a unidades con
        /// la resolución que el PNG trae en su chunk <c>pHYs</c>, y estos cinco
        /// iconos vienen de fuentes distintas (uno declara 184 ppp, otro 768), así
        /// que el mismo 512x512 se pintaba de 18 DIP en unos y de 2 en otros. Se
        /// decodifica a un número fijo de píxeles y el tamaño lo impone el
        /// elemento (<c>Width</c>/<c>Height</c> en el XAML), que sí es
        /// independiente de la resolución del archivo.
        /// </para>
        /// </summary>
        public const int PixelesIcono = 128;

        /// <summary>
        /// Extensión (en minúsculas, sin punto) al archivo del catálogo. Solo
        /// entran las cinco familias para las que hay dibujo; <c>.pptx</c> o
        /// <c>.zip</c> se quedan fuera a propósito en vez de reutilizar un
        /// icono que miente.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> PorExtension =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["pdf"] = "pdf",
                ["xlsx"] = "xlsx",
                ["xls"] = "xlsx",
                ["csv"] = "csv",
                ["json"] = "json",
                ["doc"] = "docs",
                ["docx"] = "docs",
                ["odt"] = "docs",
                ["rtf"] = "docs",
                ["txt"] = "docs",
                ["md"] = "docs",
            };

        /// <summary>
        /// Nombre del icono del catálogo (<c>pdf</c>, <c>xlsx</c>, <c>csv</c>,
        /// <c>json</c> o <c>docs</c>), o <c>null</c> si esa extensión no tiene
        /// dibujo propio.
        /// </summary>
        public static string? Clave(string? nombreArchivo)
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo)) return null;
            string ultimo = nombreArchivo!.Replace('\\', '/');
            int barra = ultimo.LastIndexOf('/');
            if (barra >= 0) ultimo = ultimo[(barra + 1)..];
            int punto = ultimo.LastIndexOf('.');
            if (punto < 0 || punto == ultimo.Length - 1) return null;
            return PorExtension.TryGetValue(ultimo[(punto + 1)..], out string? clave)
                ? clave : null;
        }

        /// <summary>
        /// Archivo del catálogo (<c>pdf.png</c>…), o <c>null</c> si esa
        /// extensión no tiene dibujo propio.
        /// </summary>
        public static string? Archivo(string? nombreArchivo)
        {
            string? clave = Clave(nombreArchivo);
            return clave is null ? null : clave + ".png";
        }

        /// <summary>
        /// Ruta absoluta del PNG, o <c>null</c> si no hay icono para esa
        /// extensión o el archivo no está. Se comprueba que exista de verdad:
        /// un PNG que no se empaquetó se ve como el 📎 de siempre y no como una
        /// imagen rota.
        /// </summary>
        /// <param name="baseDirectorio">
        /// Carpeta del ejecutable; se inyecta para poder probarlo sin depender
        /// de dónde esté compilado el test.
        /// </param>
        public static string? Ruta(string? nombreArchivo, string? baseDirectorio = null)
        {
            string? archivo = Archivo(nombreArchivo);
            if (archivo is null) return null;
            string raiz = baseDirectorio ?? AppContext.BaseDirectory;
            string candidato = Path.Combine(raiz, Carpeta, archivo);
            return File.Exists(candidato) ? candidato : null;
        }
    }
}
