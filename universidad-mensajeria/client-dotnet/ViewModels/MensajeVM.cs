using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Mensajeria.Transversal;

namespace Mensajeria.ViewModels
{
    public class MensajeVM : INotifyPropertyChanged
    {
        private BitmapImage? _imagen;
        private string _estadoImagen = "";
        private bool _cargando;
        private bool _solicitada;

        public string Id { get; init; } = "";
        private string _encabezado = "";
        /// <summary>UIX-05: vacío en rachas del mismo autor (se recalcula).</summary>
        public string Encabezado
        {
            get => _encabezado;
            set { _encabezado = value; OnPropertyChanged(); OnPropertyChanged(nameof(MostrarEncabezado)); }
        }
        public bool MostrarEncabezado => !EsCabeceraDia && !string.IsNullOrEmpty(Encabezado);
        public string Autor { get; init; } = "";
        /// <summary>UIX-05: código del remitente (agrupar por código, no por
        /// nombre visible, que puede cambiar entre páginas).</summary>
        public string AutorCrudo { get; init; } = "";
        public string Fecha { get; init; } = "";
        /// <summary>UIX-04/UIX-05: fecha cruda para agrupar (la visible ya va formateada).</summary>
        public string FechaCruda { get; init; } = "";
        /// <summary>UIX-04: true en filas de cabecera de día (sin burbuja).</summary>
        public bool EsCabeceraDia { get; init; }
        public string TextoCabecera { get; init; } = "";
        public Visibility MostrarCabecera => EsCabeceraDia ? Visibility.Visible : Visibility.Collapsed;
        public Visibility MostrarBurbuja => BurbujaChat.DebeMostrar(EsCabeceraDia)
            ? Visibility.Visible : Visibility.Collapsed;
        public string Contenido { get; init; } = "";
        public string Estado { get; init; } = "";
        public string ArchivoId { get; init; } = "";
        /// <summary>Traza JSON de filtros recibida del servidor, sin mostrar sus rutas.</summary>
        public string? EtapasFiltrado { get; init; }
        public string LeyendaEtapasFiltrado =>
            Transversal.FormatoEtapasFiltrado.Resumir(EtapasFiltrado);
        public Visibility MostrarEtapasFiltrado => EsImagen
            && !string.IsNullOrEmpty(LeyendaEtapasFiltrado)
                ? Visibility.Visible : Visibility.Collapsed;
        public string RutaArchivo { get; set; } = "";
        public string NombreArchivo { get; init; } = "";
        public bool EsImagen { get; init; }
        public bool EsArchivo { get; init; }
        /// <summary>
        /// FORMATOS-02: icono del tipo del adjunto (PDF, hoja de cálculo,
        /// CSV, JSON o documento). WPF no pintaba nada del archivo en la
        /// burbuja —ni el nombre— así que el receptor no sabía qué le había
        /// llegado hasta pulsar «Guardar archivo».
        /// </summary>
        public ImageSource? IconoArchivo => EsArchivo
            ? IconosArchivoWpf.Imagen(NombreArchivo) : null;
        /// <summary>La fila del adjunto solo sale si hay nombre que enseñar.</summary>
        public Visibility MostrarArchivo => EsArchivo
            && !string.IsNullOrWhiteSpace(NombreArchivo)
                ? Visibility.Visible : Visibility.Collapsed;
        /// <summary>Sin dibujo para esa extensión no se deja el hueco del icono.</summary>
        public Visibility MostrarIconoArchivo => IconoArchivo is null
            ? Visibility.Collapsed : Visibility.Visible;
        /// <summary>
        /// Texto que acompaña a un adjunto dentro de la MISMA burbuja
        /// (columna <c>caption</c>). Vacío = sin pie.
        /// </summary>
        public string Pie { get; init; } = "";
        /// <summary>
        /// UIX-24: cita dentro del pie de un adjunto (respuesta a una imagen o
        /// archivo). Se separa igual que en el texto; el resto va en <see cref="Pie"/>.
        /// </summary>
        public string PieCita { get; init; } = "";
        public Visibility MostrarPieCita => (EsImagen || EsArchivo)
            && !string.IsNullOrWhiteSpace(PieCita)
                ? Visibility.Visible : Visibility.Collapsed;
        public Visibility MostrarPie => (EsImagen || EsArchivo)
            && !string.IsNullOrWhiteSpace(Pie)
                ? Visibility.Visible : Visibility.Collapsed;
        private string _patronResaltado = "";
        /// <summary>UIX-06: consulta activa para resaltar (vacía = sin marcas).</summary>
        public string PatronResaltado
        {
            get => _patronResaltado;
            set { _patronResaltado = value; OnPropertyChanged(); }
        }
        /// <summary>
        /// UIX-28: los botones de guardar. <b>Dos y no uno</b>, porque guardar
        /// "algo" no dice si vas a obtener una foto o un PDF: el texto de cada uno
        /// dice qué se va a guardar. Un mensaje es imagen o archivo, nunca las dos
        /// cosas, así que nunca salen los dos a la vez. La regla vive en
        /// <see cref="Transversal.BotonGuardar"/>, sin WPF, para que sea testeable.
        /// <para>
        /// El servidor entrega ambos por `archivoId`, y cuando no lo hay el base64
        /// local sirve de respaldo.
        /// </para>
        /// </summary>
        public Visibility MostrarDescargaImagen => EsImagen
                ? Visibility.Visible : Visibility.Collapsed;
        public Visibility MostrarDescargaArchivo => EsArchivo
                ? Visibility.Visible : Visibility.Collapsed;
        /// <summary>True si el remitente es la sesión activa: burbuja a la derecha.</summary>
        public bool EsPropio { get; init; }
        public bool EsLeido { get; init; }
        public HorizontalAlignment Alineacion => BurbujaChat.Alineacion(EsPropio)
            == LadoBurbuja.Derecha ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        public Brush Fondo => Pincel(EsPropio ? "BubbleOwnBrush" : "BubbleInBrush");
        public Brush ColorEstado => Pincel(EsLeido ? "VistoLeidoBrush" : "VistoOtroBrush");
        public Brush ColorContenido => Pincel(EsPropio ? "BubbleOwnFgBrush" : "BubbleInFgBrush");
        /// <summary>
        /// UIX-27: repinta con el tema nuevo sin reconstruir la lista. Los pinceles se
        /// resuelven al acceder, así que basta con notificar para que el binding relea.
        /// </summary>
        public void RefrescarTema()
        {
            OnPropertyChanged(nameof(Fondo));
            OnPropertyChanged(nameof(ColorEstado));
            OnPropertyChanged(nameof(ColorContenido));
        }

        private static Brush Pincel(string clave)
        {
            try
            {
                if (Application.Current?.TryFindResource(clave) is Brush b) return b;
            }
            catch { }
            return EsPropioFallback(clave);
        }

        private static Brush EsPropioFallback(string clave) => clave switch
        {
            "BubbleOwnBrush" => new SolidColorBrush(Color.FromRgb(0x7B, 0x61, 0xB8)),
            "BubbleInBrush" => new SolidColorBrush(Color.FromRgb(0x18, 0x25, 0x33)),
            "VistoLeidoBrush" => new SolidColorBrush(Color.FromRgb(0x5E, 0xB1, 0xFF)),
            "VistoOtroBrush" => new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF2)),
            "BubbleOwnFgBrush" => Brushes.White,
            _ => new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF2)),
        };
        public BitmapImage? Imagen { get => _imagen; set { _imagen = value; OnPropertyChanged(); OnPropertyChanged(nameof(MostrarReintento)); } }
        public string EstadoImagen { get => _estadoImagen; set { _estadoImagen = value; OnPropertyChanged(); } }
        public bool Cargando { get => _cargando; set { _cargando = value; OnPropertyChanged(); OnPropertyChanged(nameof(MostrarReintento)); OnPropertyChanged(nameof(MostrarCancelar)); } }
        public bool Solicitada { get => _solicitada; set { _solicitada = value; OnPropertyChanged(); OnPropertyChanged(nameof(MostrarReintento)); } }
        /// <summary>
        /// UIX-24: la respuesta se ve como en WhatsApp. El prefijo de cita
        /// ("Respuesta a X: "..."") sigue viajando en el texto plano porque es
        /// el contrato compartido con Java y Python; aqui solo se separa para
        /// pintarlo como bloque.
        /// </summary>
        public string Cita { get; set; } = "";
        public string Cuerpo { get; set; } = "";
        public Visibility MostrarCita => string.IsNullOrEmpty(Cita)
            ? Visibility.Collapsed : Visibility.Visible;

        public Visibility MostrarTexto => string.IsNullOrEmpty(Cuerpo) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility MostrarImagen => EsImagen ? Visibility.Visible : Visibility.Collapsed;
        public Visibility MostrarReintento => EsImagen && !Cargando && Imagen == null && Solicitada
            ? Visibility.Visible : Visibility.Collapsed;
        /// <summary>UIX-09: cancelar la carga en vuelo de esta imagen.</summary>
        public Visibility MostrarCancelar => EsImagen && Cargando && !EsCabeceraDia
            ? Visibility.Visible : Visibility.Collapsed;
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
