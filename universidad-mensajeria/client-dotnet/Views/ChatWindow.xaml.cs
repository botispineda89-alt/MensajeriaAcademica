using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using Mensajeria.Fachada;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;
using Mensajeria.ViewModels;

namespace Mensajeria.Views
{
    public partial class ChatWindow : Window
    {
        private readonly IFachadaCliente _fachada;
        private readonly EstadoAvisoInline _estadoAvisoInline = new();
        private readonly ObservableCollection<UsuarioVM> _usuarios = new();
        private readonly ObservableCollection<MensajeVM> _mensajes = new();
        private List<UsuarioVM> _directorio = new();
        private IReadOnlyDictionary<string, DateTimeOffset> _ultimosMensajes =
            new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        private string? _usuarioDestino;
        private readonly AdjuntosPanelVM _adjuntos = new();
        private CancellationTokenSource? _ctsEnvio;
        private bool _enviando;
        private bool _cerrarTrasCancelar;
        private bool _cerrarSesionTrasCancelar;
        private int _versionCargaHistorial;
        private int _versionNavegacionCita;
        private bool _cargandoHistorial;
        private bool _errorCargaHistorial;
        private bool _cerrando;
        private int _offsetActual;
        // UIX-06/UIX-07: búsqueda en el historial con debounce.
        private readonly List<string> _idsBusqueda = new();
        private int _indiceBusqueda = -1;
        private string _patronBusqueda = "";
        private string? _saltoPendiente;
        private int _intentosSalto;
        // UIX-16: anclaje y aviso de nuevos sin perder lectura.
        private bool _seguirAbajoAntesDeCargar = true;
        private int _nuevosPendientes;
        private readonly System.Windows.Threading.DispatcherTimer _timerBuscar = new();
        private readonly System.Windows.Threading.DispatcherTimer _timerFiltro = new();
        /// <summary>UIX-26: cierra solo los avisos transitorios del banner.</summary>
        private readonly System.Windows.Threading.DispatcherTimer _timerAviso = new()
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        /// <summary>UIX-26: agrupa los refrescos de la lista de contactos.</summary>
        private const long INTERVALO_MINIMO_LISTA_MS = 400;
        private long _ultimoRefrescoListaMs;
        private bool _refrescoListaEnCurso;
        private bool _refrescoListaPendiente;
        /// <summary>UIX-26: alguien pidió refresco y todavía no se ha atendido.</summary>
        private bool _refrescoListaPedido;
        private readonly Dictionary<string, int> _paginaRemota = new();
        private readonly Dictionary<string, int> _totalPaginasRemotas = new();
        private readonly SemaphoreSlim _limiteImagenes = new(3, 3);
        private bool _restauracionUltimoChatPendiente = true;
        /// <summary>UIX-09: cargas de imagen en vuelo por id, cancelables.</summary>
        private readonly Dictionary<string, CancellationTokenSource> _cargasImagen = new();
        private sealed record AnclajeMensajeVisible(MensajeVM Mensaje, double CoordenadaVertical);

        public ChatWindow(IFachadaCliente fachada)
        {
            _fachada = fachada;
            InitializeComponent();
            txtSesion.Text = $"Sesión activa · {_fachada.UsuarioActual}";
            lstUsuarios.ItemsSource = _usuarios;
            lstMensajes.ItemsSource = _mensajes;
            lstAdjuntos.ItemsSource = _adjuntos.Adjuntos;
            _fachada.OnMensajeRecibido += Fachada_OnMensajeRecibido;
            _fachada.OnCierre += Fachada_OnCierre;
            _fachada.OnPresencia += Fachada_OnPresencia;
            // PAT-03/UIX-00a: aquí faltaba la suscripción. El banner ámbar existía
            // en el XAML pero ninguna vista lo cambiaba, así que la pérdida de
            // conexión era invisible en .NET.
            _fachada.OnEstadoConexion += Fachada_OnEstadoConexion;
            // UIX-20: la fachada reconecta y reautentica sola tras una caída;
            // la vista solo tiene que repintar cuando la sesión vuelve.
            _fachada.OnSesionRestaurada += Fachada_OnSesionRestaurada;
            PintarEstadoConexion(_fachada.EstadoConexion);
            // UIX-06/UIX-07: debounce compartido para las dos búsquedas (300 ms).
            _timerBuscar.Interval = TimeSpan.FromMilliseconds(300);
            _timerBuscar.Tick += (_, _) => { _timerBuscar.Stop(); EjecutarBusqueda(); };
            _timerFiltro.Interval = TimeSpan.FromMilliseconds(300);
            _timerFiltro.Tick += (_, _) => { _timerFiltro.Stop(); AplicarFiltro(_usuarioDestino); };
            // UIX-26: el aviso de éxito/información se cierra solo; los de error
            // y atención no (quedan hasta que el usuario los cierre).
            _timerAviso.Tick += TimerAviso_Tick;
            // UIX-06: Ctrl+F abre la búsqueda; Esc la cierra si está abierta.
            PreviewKeyDown += (s, e) =>
            {
                bool ctrlF = e.Key == System.Windows.Input.Key.F
                    && System.Windows.Input.Keyboard.Modifiers
                        .HasFlag(System.Windows.Input.ModifierKeys.Control);
                if (ctrlF)
                {
                    MostrarBuscar();
                    e.Handled = true;
                }
                else if (e.Key == System.Windows.Input.Key.Escape
                    && barraBuscar.Visibility == Visibility.Visible)
                {
                    CerrarBuscar();
                    e.Handled = true;
                }
            };
            cmbTema.SelectedIndex = Temas.Tema.Leer() == Temas.Tema.Claro ? 1 : 0;
            AplicarFondoChat(Temas.Tema.Leer());
            // UIX-11: restaura el ancho del sidebar (250–360).
            int anchoSide = Transversal.ClienteConfig.Entero("sidebar.ancho", 300, 250, 360);
            colSidebar.Width = new GridLength(anchoSide);
            // UIX-13: sin conversación, el panel no queda negro sin explicación.
            ActualizarVacioChat();
            CargarUsuarios();
            ActualizarEstadoEnvio();
        }

        /// <summary>Prefijo completo de la respuesta en curso ("" si no hay).</summary>
        private string _cita = "";

        private string Identidad(string codigo)
        {
            var usuario = _directorio.FirstOrDefault(u => u.Codigo == codigo);
            return usuario == null ? codigo : usuario.Etiqueta;
        }

        /// <summary>
        /// Texto del tick de la burbuja. Los 5 estados y sus glifos son los de
        /// <c>contrato/estado-mensaje.json</c> (cierra VIS-02).
        /// </summary>
        /// <remarks>
        /// <c>ERROR</c> estaba en el <c>_ =&gt; ""</c>: el servidor rechaza el envio, el
        /// mensaje queda en estado ERROR y la burbuja se quedaba sin indicador,
        /// mientras Java si lo mostraba. Un rechazo no es un estado de la maquina
        /// (ver ERROR terminal en el contrato) pero si necesita quedar a la vista.
        /// </remarks>
        private static string EstadoVisible(string estado) => estado switch
        {
            // Visto literal con palabra, igual que el cliente Python (UI-06).
            "PENDIENTE" => "⏳ Pendiente",
            "ENVIADO" => "✓ Enviado",
            "ENTREGADO" => "✓✓ Entregado",
            "LEIDO" => "✓✓ Leído",
            "ERROR" => "! No enviado",
            _ => ""
        };

        private static bool EsConectado(Dictionary<string, object> usuario)
        {
            if (!usuario.TryGetValue("conectado", out var c)) return false;
            return c switch
            {
                bool b => b,
                int i => i != 0,
                long l => l != 0,
                _ => false
            };
        }

        private static string Valor(Dictionary<string, object> fila, string clave) =>
            fila.TryGetValue(clave, out var valor) && valor != null ? valor.ToString() ?? "" : "";

        private static DateTimeOffset? ParseFechaConversacion(string fecha) =>
            DateTimeOffset.TryParse(fecha, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out var instante)
                ? instante.ToUniversalTime() : null;

        /// <summary>Hora corta relativa (UIX-03): delega al contrato compartido.</summary>
        private static string FormatearFecha(string crudo) =>
            Transversal.FormatoFecha.HoraCorta(crudo);

        private static string? GetString(Dictionary<string, JsonElement> fila, string clave) =>
            fila.TryGetValue(clave, out var valor) && valor.ValueKind == JsonValueKind.String
                ? valor.GetString() : null;
    }
}
