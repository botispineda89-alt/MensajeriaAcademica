using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;

namespace Mensajeria.ViewModels
{
    public sealed class AdjuntoVM : INotifyPropertyChanged
    {
        private ImageSource? _miniatura;
        private double _progreso;
        private string? _error;
        private EstadoEnvioAdjunto _estado = EstadoEnvioAdjunto.NoIntentado;

        public AdjuntoVM(AdjuntoPendiente datos) => Datos = datos;

        public AdjuntoPendiente Datos { get; }
        public string Ruta => Datos.Ruta;
        public string Nombre => Datos.Nombre;
        public string TamanoLegible => CatalogoAdjuntos.TamanoLegible(Datos.Tamano);
        public bool EsImagen => Datos.EsImagen;
        public string ToolTip => $"{Datos.Nombre}\n{TamanoLegible}\n{Datos.Ruta}"
            + (PathExtensionEsGif() ? "\nSe muestra el primer fotograma del GIF animado." : "");
        public string IconoTexto => CatalogoAdjuntos.Icono(Ruta) switch
        {
            ClaveIconoAdjunto.DocumentoPdf => "PDF",
            ClaveIconoAdjunto.DocumentoTexto => "DOC",
            ClaveIconoAdjunto.DocumentoTabla => "▦",
            ClaveIconoAdjunto.DocumentoMultiple => "▤",
            ClaveIconoAdjunto.Comprimido => "▣",
            _ => "▧"
        };

        public ImageSource? Miniatura
        {
            get => _miniatura;
            internal set
            {
                _miniatura = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EsDocumento));
                // Con la miniatura cargada, el cuadro de texto del icono ya no
                // manda: sin este aviso seguiría visible sobre la foto.
                OnPropertyChanged(nameof(MostrarIconoTexto));
            }
        }

        /// <summary>
        /// FORMATOS-02: icono del tipo de documento (PDF, hoja de cálculo...).
        /// Es un elemento aparte de la miniatura porque el suyo tiene un tamaño
        /// fijo en el XAML: WPF mide un PNG con la resolución que trae en el
        /// archivo, así que dejarlo al tamaño natural daba iconos de 18 y de 2
        /// unidades según de dónde viniera el dibujo.
        /// </summary>
        public ImageSource? IconoArchivo => EsImagen
            ? null : IconosArchivoWpf.Imagen(Ruta);

        public bool EsDocumento => Miniatura is null;

        /// <summary>El cuadro de texto del icono: solo cuando no hay dibujo del tipo.</summary>
        public Visibility MostrarIconoTexto => Miniatura is null && IconoArchivo is null
            ? Visibility.Visible : Visibility.Collapsed;

        public double Progreso
        {
            get => _progreso;
            set
            {
                _progreso = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgresoVisible));
                OnPropertyChanged(nameof(ProgresoTexto));
            }
        }

        public Visibility ProgresoVisible => _progreso > 0d && _progreso < 1d
            ? Visibility.Visible : Visibility.Collapsed;
        public string ProgresoTexto => $"{_progreso:P0}";

        public EstadoEnvioAdjunto Estado
        {
            get => _estado;
            set
            {
                _estado = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EstadoTexto));
                OnPropertyChanged(nameof(EstadoColor));
            }
        }

        public string? Error
        {
            get => _error;
            set
            {
                _error = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ErrorVisible));
            }
        }

        public bool ErrorVisible => !string.IsNullOrEmpty(_error);
        public string EstadoTexto => Estado switch
        {
            EstadoEnvioAdjunto.Enviado => "Enviado",
            EstadoEnvioAdjunto.EnCola => "En cola",
            EstadoEnvioAdjunto.Fallido => "Falló",
            _ => ""
        };

        public Brush EstadoColor => Pincel(Estado switch
        {
            EstadoEnvioAdjunto.Enviado => "BadgeBrush",
            EstadoEnvioAdjunto.EnCola => "OfflineBannerFgBrush",
            EstadoEnvioAdjunto.Fallido => "ErrorFgBrush",
            _ => "MutedBrush"
        });

        public async void CargarMiniatura()
        {
            ImageSource? miniatura = await System.Threading.Tasks.Task.Run(Miniaturizar);
            Application.Current?.Dispatcher.BeginInvoke(() => Miniatura = miniatura);
        }

        private ImageSource? Miniaturizar()
        {
            // FORMATOS-02: un documento ya no genera miniatura aquí — su icono
            // de tipo lo sirve `IconoArchivo` con tamaño fijo.
            if (!EsImagen) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.DecodePixelWidth = 128;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bmp.UriSource = new Uri(Ruta, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private bool PathExtensionEsGif() =>
            string.Equals(System.IO.Path.GetExtension(Ruta), ".gif",
                StringComparison.OrdinalIgnoreCase);

        private static Brush Pincel(string clave)
        {
            try
            {
                if (Application.Current?.TryFindResource(clave) is Brush brush) return brush;
            }
            catch { }
            return Brushes.Gray;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? nombre = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }
}
