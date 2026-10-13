using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mensajeria.Transversal;

namespace Mensajeria.ViewModels
{
    /// <summary>
    /// FORMATOS-02: los PNG del catálogo (<see cref="IconosArchivo"/>) ya
    /// cargados como <see cref="ImageSource"/> de WPF.
    /// <para>
    /// Vive en ViewModels y no en Transversal porque el proyecto de tests
    /// compila <c>Transversal/</c> para net10.0 sin WPF: un
    /// <c>BitmapImage</c> ahí dejaría la suite sin compilar.
    /// </para>
    /// <para>
    /// Los mapas van congelados (<c>Freeze</c>) y en caché por ruta: una lista
    /// virtualizada vuelve a pedir el icono en cada reciclaje de celda, y
    /// decodificar 512x512 cada vez se notaría al desplazarse.
    /// </para>
    /// </summary>
    internal static class IconosArchivoWpf
    {
        private static readonly object Candado = new();
        private static readonly Dictionary<string, ImageSource?> Cache =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Icono del tipo ya decodificado, o <c>null</c> si esa extensión no
        /// tiene dibujo o el PNG no se empaquetó. El tamaño en pantalla lo fija
        /// quien lo pinta (<c>Width</c>/<c>Height</c> en el XAML).
        /// </summary>
        public static ImageSource? Imagen(string? nombreArchivo)
        {
            string? ruta = IconosArchivo.Ruta(nombreArchivo);
            if (ruta is null) return null;
            lock (Candado)
            {
                if (Cache.TryGetValue(ruta, out ImageSource? guardada)) return guardada;
            }
            ImageSource? imagen = Cargar(ruta);
            lock (Candado)
            {
                Cache[ruta] = imagen;
            }
            return imagen;
        }

        private static ImageSource? Cargar(string ruta)
        {
            try
            {
                var mapa = new BitmapImage();
                mapa.BeginInit();
                mapa.DecodePixelWidth = IconosArchivo.PixelesIcono;
                mapa.CacheOption = BitmapCacheOption.OnLoad;
                mapa.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                mapa.UriSource = new Uri(ruta, UriKind.Absolute);
                mapa.EndInit();
                mapa.Freeze();
                return mapa;
            }
            catch
            {
                return null;
            }
        }
    }
}
