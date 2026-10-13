using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace Mensajeria.ViewModels
{
    public class UsuarioVM : INotifyPropertyChanged
    {
        public string Codigo { get; init; } = "";
        public string Nombres { get; init; } = "";
        public string Apellidos { get; init; } = "";
        public bool Conectado { get; init; }
        public string Etiqueta => string.IsNullOrWhiteSpace((Nombres + " " + Apellidos).Trim())
            ? Codigo : $"{Nombres} {Apellidos} [{Codigo}]";
        public string Presencia => Conectado ? "en línea" : "desconectado";
        public Brush ColorPresencia => Conectado ? Brushes.SeaGreen : Brushes.LightGray;

        private int _noLeidos;
        /// <summary>UIX-02: distintivo de no leídos (lo único que notifica).</summary>
        public int NoLeidos
        {
            get => _noLeidos;
            set { _noLeidos = value; OnPropertyChanged(); OnPropertyChanged(nameof(TextoInsignia)); OnPropertyChanged(nameof(MostrarInsignia)); }
        }
        /// <summary>UIX-24: preview del ultimo mensaje de la conversacion.</summary>
        private string _preview = "";
        public string Preview
        {
            get => _preview;
            set { _preview = value; OnPropertyChanged(); OnPropertyChanged(nameof(TextoPreview)); OnPropertyChanged(nameof(MostrarPreview)); }
        }
        /// <summary>UIX-26: siempre hay segunda línea; vacía dice "Sin mensajes aún".</summary>
        public Visibility MostrarPreview => Visibility.Visible;
        /// <summary>El "Tú:" solo si el ultimo mensaje lo escribio el usuario.</summary>
        public string TextoPreview => Transversal.FormatoPreview.Preview(Preview, PropioPreview);
        private bool _propioPreview;
        public bool PropioPreview
        {
            get => _propioPreview;
            set
            {
                _propioPreview = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextoPreview));
                OnPropertyChanged(nameof(ColorPreview));
            }
        }
        /// El color viene del tema (como el CSS `chat-list-preview` de Java), no de un
        /// gris fijo que en el tema claro queda ilegible sobre fondo claro.
        public Brush ColorPreview => ResolverPincel("MutedBrush", Brushes.Gray);

        private static Brush ResolverPincel(string clave, Brush porDefecto)
        {
            try
            {
                if (Application.Current?.TryFindResource(clave) is Brush delTema) return delTema;
            }
            catch { }
            return porDefecto;
        }

        public string TextoInsignia => Transversal.FormatoPreview.Insignia(NoLeidos);
        public Visibility MostrarInsignia => NoLeidos > 0 ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
