using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace Mensajeria.Transversal
{
    /// <summary>
    /// Convierte a PNG, SOLO para pintarlo, lo que este cliente no sabe
    /// decodificar con su pila nativa (WPF + Skia).
    /// <para>
    /// El <c>.webp</c> es el caso de siempre: se envía tal cual (el servidor
    /// lo filtra) y esta conversión solo sirve para la vista. Lo mismo aplica
    /// desde los formatos amplios (FORMATOS-01): AVIF, HEIC/HEIF y SVG viajan
    /// como <c>MENSAJE_IMAGEN</c>, el servidor los decodifica vía ImageMagick
    /// (<c>DecodificadorMagick</c>) y aquí se llevan a PNG únicamente para
    /// mostrar miniatura y visor.
    /// </para>
    /// <para>
    /// Skia cubre WebP; AVIF/HEIC/SVG los abre Magick.NET (la misma librería
    /// ImageMagick que usa el servidor, en forma de paquete NuGet). Si una
    /// ruta no puede abrirse con ninguna de las dos, la excepción conserva el
    /// nombre del archivo para el mensaje de usuario.
    /// </para>
    /// </summary>
    public static class TranscodificadorImagen
    {
        /// <summary>Extensiones que la vista convierte a PNG (el envío va tal cual).</summary>
        public static readonly IReadOnlySet<string> ExtensionesNecesitanConversion =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".webp", ".avif", ".heic", ".heif", ".svg" };

        /// <summary>true si el archivo está en un formato que hay que convertir.</summary>
        public static bool NecesitaConversion(string ruta) =>
            ExtensionesNecesitanConversion.Contains(Path.GetExtension(ruta));

        /// <summary>
        /// Escribe el PNG equivalente en un directorio temporal y devuelve su
        /// ruta. El nombre conserva el original para que el receptor siga
        /// reconociendo de qué archivo se trata.
        /// </summary>
        /// <exception cref="InvalidOperationException">si la imagen no se puede leer.</exception>
        public static string ConvertirAPng(string origen, string? destino = null)
        {
            if (!File.Exists(origen))
                throw new FileNotFoundException("el archivo ya no está disponible", origen);

            using var stream = File.OpenRead(origen);
            using var imagen = SKBitmap.Decode(stream);
            if (imagen != null)
            {
                using var salida = File.Create(destino ?? RutaPngTemporal(origen));
                using var png = imagen.Encode(SKEncodedImageFormat.Png, 100);
                if (png == null)
                    throw new InvalidOperationException("no hay codificador PNG disponible");
                png.SaveTo(salida);
                return salida.Name;
            }

            // AVIF, HEIC/HEIF y SVG: fuera del alcance de Skia/WPF, con
            // ImageMagick (la misma pila del servidor, vía Magick.NET).
            try
            {
                using var magick = new ImageMagick.MagickImage(origen);
                using var salida = File.Create(destino ?? RutaPngTemporal(origen));
                magick.Format = ImageMagick.MagickFormat.Png;
                magick.Write(salida);
                return salida.Name;
            }
            catch (Exception fallo)
            {
                throw new InvalidOperationException(
                    $"no se pudo leer {Path.GetFileName(origen)}", fallo);
            }
        }

        private static string RutaPngTemporal(string origen) =>
            Path.Combine(Path.GetTempPath(),
                Path.GetFileNameWithoutExtension(origen) + ".png");

        /// <summary>
        /// Devuelve la ruta a enviar: la del propio archivo si no hay que
        /// convertirlo, o la del PNG temporal si sí.
        /// </summary>
        public static string RutaParaEnviar(string origen) =>
            NecesitaConversion(origen) ? ConvertirAPng(origen) : origen;
    }
}