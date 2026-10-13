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

        /// <summary>UIX-11: persiste el ancho del sidebar al soltar el divisor.</summary>
        private void Splitter_DragCompleted(object sender,
            System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            Transversal.ClienteConfig.Guardar("sidebar.ancho",
                ((int)colSidebar.ActualWidth).ToString());
        }

        /// <summary>PAT-03: pinta el estado que publica la fachada.</summary>
        private void Fachada_OnEstadoConexion(EstadoConexion estado)
        {
            // Llega desde el hilo de red: el salto al hilo de UI es obligatorio.
            // UIX-20: BeginInvoke y NO Invoke. Invoke es bloqueante, y este
            // aviso se emite desde el hilo lector de TCP; con Invoke, el lector
            // se quedaba esperando al hilo de UI y el socket dejaba de consumirse
            // justo cuando más falta hacía.
            if (_cerrando) return;
            Dispatcher.BeginInvoke(() => PintarEstadoConexion(estado));
        }

        /// <summary>UIX-20: la sesión volvió sola tras una caída; hay que repintar.</summary>
        private void Fachada_OnSesionRestaurada(string codigo)
        {
            if (_cerrando) return;
            Dispatcher.BeginInvoke(() =>
            {
                if (_cerrando) return;
                LimpiarAvisoInline();
                CargarUsuarios();
                if (_usuarioDestino != null)
                    CargarHistorialRemoto(_usuarioDestino, 0);
            });
        }

        private void PintarEstadoConexion(EstadoConexion estado)
        {
            bool degrading = EstadoConexionTexto.MuestraBanner(estado);
            bannerOffline.Visibility = degrading ? Visibility.Visible : Visibility.Collapsed;
            if (degrading)
            {
                bannerOfflineText.Text = EstadoConexionTexto.Texto(estado);
                txtEstado.Text = EstadoConexionTexto.Texto(estado);
            }
            else if (txtEstado.Text == EstadoConexionTexto.Texto(EstadoConexion.SinRed)
                || txtEstado.Text == EstadoConexionTexto.Texto(EstadoConexion.SesionCerrada))
            {
                txtEstado.Text = _usuarios.FirstOrDefault(u => u.Codigo == _usuarioDestino)?.Presencia ?? "";
            }
        }

        private void MostrarAvisoInline(string mensaje, TipoAvisoInline tipo)
        {
            _estadoAvisoInline.Mostrar(mensaje, tipo);
            txtTituloAvisoInline.Text = _estadoAvisoInline.Tipo switch
            {
                TipoAvisoInline.Error => "Error",
                TipoAvisoInline.Advertencia => "Atención",
                TipoAvisoInline.Exito => "Listo",
                _ => "Información"
            };
            txtAvisoInline.Text = _estadoAvisoInline.Mensaje;

            (Color fondo, Color borde, Color texto) = _estadoAvisoInline.Tipo switch
            {
                TipoAvisoInline.Error => (Color.FromRgb(0xFD, 0xEC, 0xEA), Color.FromRgb(0xF1, 0xA9, 0xA5), Color.FromRgb(0xB9, 0x1C, 0x1C)),
                TipoAvisoInline.Advertencia => (Color.FromRgb(0xFF, 0xFB, 0xEB), Color.FromRgb(0xF2, 0xCE, 0x84), Color.FromRgb(0xB4, 0x53, 0x09)),
                TipoAvisoInline.Exito => (Color.FromRgb(0xEC, 0xFD, 0xF3), Color.FromRgb(0xA7, 0xF3, 0xD0), Color.FromRgb(0x04, 0x78, 0x57)),
                _ => (Color.FromRgb(0xEF, 0xF6, 0xFF), Color.FromRgb(0xBF, 0xDB, 0xFE), Color.FromRgb(0x1D, 0x4E, 0xD8))
            };
            bordeAvisoInline.Background = new SolidColorBrush(fondo);
            bordeAvisoInline.BorderBrush = new SolidColorBrush(borde);
            txtTituloAvisoInline.Foreground = new SolidColorBrush(texto);
            txtAvisoInline.Foreground = new SolidColorBrush(texto);
            bordeAvisoInline.Visibility = _estadoAvisoInline.Visible ? Visibility.Visible : Visibility.Collapsed;
            // UIX-26: los avisos de éxito e información se cierran solos; los de
            // error y atención no, porque dependen de que alguien los lea.
            _timerAviso.Stop();
            if (_estadoAvisoInline.DebeOcultarseAutomaticamente) _timerAviso.Start();
        }

        /// <summary>
        /// Cierra el aviso cuando se agota su tiempo. Un <c>DispatcherTimer</c> en
        /// lugar de un <c>Task.Delay</c>: vive en el hilo de la UI, se para con
        /// la ventana y no puede intentar tocar un control ya liberado.
        /// </summary>
        private void TimerAviso_Tick(object? sender, EventArgs e)
        {
            if (!_estadoAvisoInline.DebeOcultarseAutomaticamente)
            {
                _timerAviso.Stop();
                return;
            }
            if (_estadoAvisoInline.MilisegundosRestantes(Environment.TickCount64) <= 0)
            {
                LimpiarAvisoInline();
            }
        }

        private void LimpiarAvisoInline()
        {
            _estadoAvisoInline.Limpiar();
            _timerAviso.Stop();
            bordeAvisoInline.Visibility = Visibility.Collapsed;
        }

        private void BtnCerrarAvisoInline_Click(object sender, RoutedEventArgs e) => LimpiarAvisoInline();

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_enviando)
            {
                e.Cancel = true;
                _ctsEnvio?.Cancel();
                _cerrarTrasCancelar = true;
                MostrarAvisoInline("Cancelando el envío antes de cerrar…",
                    TipoAvisoInline.Informacion);
                return;
            }
            if (!_cerrando)
            {
                if (MessageBox.Show("¿Deseas cerrar esta sesión y volver al inicio?", "Confirmar salida",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                VolverAlInicio();
            }
            base.OnClosing(e);
        }

        private void VolverAlInicio(string aviso = "")
        {
            _cerrando = true;
            CancelarTodasLasCargas();
            _timerBuscar.Stop();
            _timerFiltro.Stop();
            _timerAviso.Stop();
            _fachada.OnMensajeRecibido -= Fachada_OnMensajeRecibido;
            _fachada.OnCierre -= Fachada_OnCierre;
            _fachada.OnPresencia -= Fachada_OnPresencia;
            // UIX-20: estos dos también se habían suscrito y no se quitaban. La
            // fachada vive toda la aplicación, así que cada cierre de sesión
            // dejaba una ventana muerta suscrita a los eventos: el aviso de
            // reconexión alcanzaba un Dispatcher ya cerrado.
            _fachada.OnEstadoConexion -= Fachada_OnEstadoConexion;
            _fachada.OnSesionRestaurada -= Fachada_OnSesionRestaurada;
            _fachada.Desconectar();
            new LoginWindow(_fachada, aviso).Show();
        }

        private void Fachada_OnCierre(Dictionary<string, JsonElement> aviso)
        {
            string motivo = MensajesUsuario.SesionCerrada;
            Dispatcher.BeginInvoke(() =>
            {
                if (_cerrando) return;
                VolverAlInicio(motivo);
                Close();
            });
        }

        private async void CargarUsuarios()
        {
            List<Dictionary<string, object>> lista;
            try { lista = await _fachada.ListarUsuariosAsync(); }
            catch { lista = await Task.Run(() => _fachada.DirectorioLocal()); }
            await ActualizarFechasRecientesAsync();
            string? seleccionado = (lstUsuarios.SelectedItem as UsuarioVM)?.Codigo ?? _usuarioDestino;
            // Encabezado de sesión con nombre completo + código cuando está disponible.
            var propia = lista.FirstOrDefault(u => u.TryGetValue("codigo", out var c) && c is string s && s == _fachada.UsuarioActual);
            if (propia != null)
            {
                string nom = (Valor(propia, "nombres") + " " + Valor(propia, "apellidos")).Trim();
                txtSesion.Text = string.IsNullOrWhiteSpace(nom)
                    ? $"Sesión activa · {_fachada.UsuarioActual}"
                    : $"Sesión activa · {nom} [{_fachada.UsuarioActual}]";
            }
            _directorio = lista
                .Where(u => u.TryGetValue("codigo", out var c) && c is string s && s != _fachada.UsuarioActual)
                .Select(u => new UsuarioVM
                {
                    Codigo = Valor(u, "codigo"), Nombres = Valor(u, "nombres"),
                    Apellidos = Valor(u, "apellidos"), Conectado = EsConectado(u)
                }).ToList();
            // UIX-02: insignia de no leídos (una sola query para todos).
            var noLeidos = _fachada.ContarNoLeidos();
            // UIX-24: preview del ultimo mensaje (una sola query, no una por chat).
            var previews = await Task.Run(() => _fachada.UltimoMensajePorContacto());
            foreach (var u in _directorio)
            {
                u.NoLeidos = noLeidos.GetValueOrDefault(u.Codigo);
                if (previews.TryGetValue(u.Codigo, out var prev))
                {
                    u.Preview = prev.Texto;
                    u.PropioPreview = prev.Propio;
                }
            }
            AplicarFiltro(seleccionado);
            RestaurarUltimoChatSiPendiente(seleccionado);
        }

        private void RestaurarUltimoChatSiPendiente(string? seleccionado)
        {
            if (!_restauracionUltimoChatPendiente) return;
            _restauracionUltimoChatPendiente = false;
            string? seleccionadoInicial = Transversal.PreferenciasChat.ResolverSeleccion(
                Transversal.PreferenciasChat.UltimoChat(_fachada.UsuarioActual),
                seleccionado, _usuarios.Select(u => u.Codigo));
            if (seleccionadoInicial == null
                || string.Equals(seleccionadoInicial, seleccionado, StringComparison.OrdinalIgnoreCase))
                return;
            lstUsuarios.SelectedItem = _usuarios.FirstOrDefault(u =>
                string.Equals(u.Codigo, seleccionadoInicial, StringComparison.OrdinalIgnoreCase));
        }

        private async Task ActualizarFechasRecientesAsync()
        {
            try
            {
                var fechas = await Task.Run(() => _fachada.UltimosMensajesPorContacto());
                var ordenReciente = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
                foreach (var par in fechas)
                {
                    if (DateTimeOffset.TryParse(par.Value, CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeLocal, out var fecha))
                        ordenReciente[par.Key] = fecha.ToUniversalTime();
                }
                _ultimosMensajes = ordenReciente;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("No se pudieron ordenar las conversaciones por fecha: {0}", ex);
                _ultimosMensajes = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void MarcarConversacionReciente(string codigo)
        {
            if (string.IsNullOrWhiteSpace(codigo)) return;
            ProgramarRefrescoLista();
            var fechas = new Dictionary<string, DateTimeOffset>(
                _ultimosMensajes, StringComparer.OrdinalIgnoreCase)
            {
                [codigo] = DateTimeOffset.UtcNow
            };
            _ultimosMensajes = fechas;
            AplicarFiltro(_usuarioDestino);
        }

        private void AplicarFiltro(string? seleccionado = null)
        {
            if (txtBuscar == null || txtEstado == null || lstUsuarios == null) return;
            string filtro = (txtBuscar.Text ?? "").Trim();
            string? modo = (cmbFiltro.SelectedItem as System.Windows.Controls.ComboBoxItem)
                ?.Content?.ToString();
            bool soloConectados = modo == "Conectados";
            // UIX-02: filtro "Con no leídos" (paridad con Java y Python).
            bool soloNoLeidos = modo == "Con no leídos";
            var vista = _directorio.Where(u =>
                (!soloConectados || u.Conectado)
                && (!soloNoLeidos || u.NoLeidos > 0)
                && (filtro.Length == 0 || u.Etiqueta.Contains(filtro, StringComparison.CurrentCultureIgnoreCase)
                || u.Codigo.Contains(filtro, StringComparison.CurrentCultureIgnoreCase)))
                .OrderByDescending(u => _ultimosMensajes.GetValueOrDefault(
                    u.Codigo, DateTimeOffset.MinValue))
                .ThenByDescending(u => u.Conectado)
                .ThenBy(u => u.Apellidos, StringComparer.CurrentCultureIgnoreCase).ToList();
            _usuarios.Clear();
            foreach (var usuario in vista) _usuarios.Add(usuario);
            if (seleccionado != null)
                lstUsuarios.SelectedItem = _usuarios.FirstOrDefault(u => u.Codigo == seleccionado);
            txtEstado.Text = $"{_directorio.Count(u => u.Conectado)} contactos en línea";
        }

        private void TxtBuscar_TextChanged(object sender, TextChangedEventArgs e)
        {
            // UIX-07: sin debounce cada tecla reconstruía la lista.
            _timerFiltro.Stop();
            _timerFiltro.Start();
        }

        private void CmbFiltro_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // El evento se dispara durante InitializeComponent (IsSelected en XAML)
            // cuando txtEstado/lstUsuarios aun no existen: ignorar ese disparo inicial.
            if (txtEstado == null || lstUsuarios == null || txtBuscar == null) return;
            AplicarFiltro(_usuarioDestino);
        }

        private void Fachada_OnPresencia(Dictionary<string, JsonElement> aviso) =>
            Dispatcher.BeginInvoke(CargarUsuarios);

        private void Fachada_OnMensajeRecibido(Dictionary<string, JsonElement> msg)
        {
            if (!msg.TryGetValue("tipo", out var t) || t.ValueKind != JsonValueKind.String) return;
            string tipo = t.GetString()!;
            if (tipo == TipoMensaje.MENSAJE_ENTREGADO || tipo == TipoMensaje.MENSAJE_LEIDO)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (_usuarioDestino != null) CargarHistorial(_usuarioDestino);
                });
                return;
            }
            // UIX-26: MENSAJE_ARCHIVO también repinta la lista. Estaba fuera de la lista
            // y por eso un archivo que llegaba no ponía ni el preview ni la
            // insignia hasta que se pulsaba "Actualizar".
            if (tipo != TipoMensaje.MENSAJE_TEXTO && tipo != TipoMensaje.MENSAJE_IMAGEN
                && tipo != TipoMensaje.MENSAJE_ARCHIVO
                && tipo != TipoMensaje.BROADCAST && tipo != TipoMensaje.SYNC_LOGIN) return;
            string remitente = GetString(msg, "remitente") ?? "desconocido";
            string destinatario = GetString(msg, "destinatario") ?? "";
            string contacto = tipo == TipoMensaje.BROADCAST
                ? remitente
                : string.Equals(remitente, _fachada.UsuarioActual, StringComparison.OrdinalIgnoreCase)
                    ? destinatario : remitente;
            Dispatcher.BeginInvoke(() =>
            {
                MarcarConversacionReciente(contacto);
                if (_usuarioDestino != contacto) return;
                // UIX-16: solo baja solo si ya se estaba abajo; si no, avisa.
                bool avisar = !EstaAbajo();
                CargarHistorial(contacto);
                if (avisar) AvisarNuevo();
                if (!string.Equals(remitente, _fachada.UsuarioActual, StringComparison.OrdinalIgnoreCase)
                    && _usuarioDestino == remitente)
                    _ = Task.Run(() => _fachada.MarcarLeidosAsync(remitente));
            });
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e) => CargarUsuarios();

        private void CmbTema_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbTema.SelectedIndex < 0) return;
            string modo = cmbTema.SelectedIndex == 1 ? Temas.Tema.Claro : Temas.Tema.Oscuro;
            Temas.Tema.Guardar(modo);
            Temas.Tema.Aplicar(modo);
            AplicarFondoChat(modo);
            // Las burbujas guardan su pincel: se reconstruyen con el modo nuevo.
            if (_usuarioDestino != null) CargarHistorial(_usuarioDestino);
        }

        private void AplicarFondoChat(string modo)
        {
            string archivo = modo == Temas.Tema.Claro
                ? "fondo_chat_claro.jpg" : "fondo_chat_oscuro.jpg";
            string ruta = Path.Combine(AppContext.BaseDirectory, "Temas", "fondos", archivo);
            if (!File.Exists(ruta))
            {
                panelMensajes.Background = TryFindResource("ChatBgBrush") as Brush;
                lstMensajes.Background = Brushes.Transparent;
                return;
            }

            var imagen = new BitmapImage();
            imagen.BeginInit();
            imagen.CacheOption = BitmapCacheOption.OnLoad;
            imagen.UriSource = new Uri(ruta, UriKind.Absolute);
            imagen.EndInit();
            imagen.Freeze();
            // El fondo vive detrás del ListBox: el template de ListBox/ScrollViewer
            // puede dibujar su propia superficie y tapar un brush aplicado solo a él.
            // UniformToFill recorta como background: cover; las burbujas opacas
            // conservan contraste por encima del patrón.
            panelMensajes.Background = new ImageBrush(imagen)
            {
                Stretch = Stretch.UniformToFill,
                Opacity = 0.62
            };
            lstMensajes.Background = Brushes.Transparent;
        }

        private void LstUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstUsuarios.SelectedItem is not UsuarioVM usuario
                || string.Equals(_usuarioDestino, usuario.Codigo, StringComparison.OrdinalIgnoreCase)) return;
            if (_adjuntos.HayAdjuntos && !_enviando)
            {
                int cantidad = _adjuntos.Adjuntos.Count;
                _adjuntos.Limpiar();
                MostrarAvisoInline($"Se descartaron {cantidad} adjunto(s) al cambiar de conversación.",
                    TipoAvisoInline.Informacion);
            }
            ActualizarPanelAdjuntos();
            Interlocked.Increment(ref _versionNavegacionCita);
            _usuarioDestino = usuario.Codigo;
            ActualizarEstadoEnvio();
            Transversal.PreferenciasChat.GuardarUltimoChat(
                _fachada.UsuarioActual, usuario.Codigo);
            // UIX-02: la insignia se apaga al abrir (el marcado real viaja por
            // MarcarLeidosAsync en segundo plano).
            usuario.NoLeidos = 0;
            // UIX-16: conversación nueva, aviso nuevo apagado.
            _nuevosPendientes = 0;
            btnNuevos.Visibility = Visibility.Collapsed;
            // UIX-06: los resultados son por conversación; al cambiar, fuera.
            if (barraBuscar.Visibility == Visibility.Visible) CerrarBuscar();
            // UIX-09: las descargas del chat anterior ya no pintan aquí.
            CancelarTodasLasCargas();
            _offsetActual = 0;
            _mensajes.Clear();
            _cargandoHistorial = true;
            _errorCargaHistorial = false;
            ActualizarVacioChat();
            LimpiarCita();
            txtTitulo.Text = usuario.Etiqueta;
            txtEstado.Text = usuario.Presencia;
            _paginaRemota[usuario.Codigo] = 0;
            _totalPaginasRemotas.Remove(usuario.Codigo);
            CargarHistorialRemoto(usuario.Codigo, 0);
            CargarHistorial(usuario.Codigo);
            _ = Task.Run(() => _fachada.MarcarLeidosAsync(usuario.Codigo));
        }

        /// <summary>
        /// Envoltura de seguridad. Esta es una continuation async void: lo que
        /// revienta dentro sube al Dispatcher y CIERRA la aplicacion. Un fallo
        /// al pintar una fila no puede costar la sesion del usuario.
        /// </summary>
        private async void CargarHistorial(string codigo, int offset = 0, bool prepend = false,
            Action? alTerminar = null)
        {
            try
            {
                await CargarHistorialInterno(codigo, offset, prepend, alTerminar);
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceError(
                    "No se pudo pintar el historial de {0}: {1}", codigo, error);
                if (_usuarioDestino == codigo)
                    barraProgreso.Visibility = Visibility.Collapsed;
                MostrarAvisoInline("No se pudo mostrar esta conversacion. Intenta actualizar.",
                    TipoAvisoInline.Error);
            }
        }

        private async Task CargarHistorialInterno(string codigo, int offset, bool prepend,
            Action? alTerminar)
        {
            int version = Interlocked.Increment(ref _versionCargaHistorial);
            List<Dictionary<string, object>> historial;
            int totalLocal;
            // UIX-14: la rueda gira mientras se trae historial.
            barraProgreso.Visibility = Visibility.Visible;
            // UIX-16: se captura ANTES de la carga, porque al terminar la lista cambió.
            if (!prepend) _seguirAbajoAntesDeCargar = EstaAbajo();
            var anclaje = prepend ? CapturarAnclajeScroll() : null;
            try
            {
                (historial, totalLocal) = await Task.Run(() =>
                    (_fachada.CargarHistorial(codigo, offset), _fachada.ContarConversacion(codigo)));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("No se pudo cargar el historial de {0}: {1}", codigo, ex);
                if (_usuarioDestino == codigo && version == _versionCargaHistorial)
                {
                    barraProgreso.Visibility = Visibility.Collapsed;
                    _cargandoHistorial = false;
                    _errorCargaHistorial = true;
                    ActualizarVacioChat();
                    MostrarAvisoInline("No se pudo cargar el historial de esta conversación. Intenta actualizar los contactos.",
                        TipoAvisoInline.Error);
                }
                return;
            }
            if (_usuarioDestino != codigo || version != _versionCargaHistorial) return;
            barraProgreso.Visibility = Visibility.Collapsed;
            _cargandoHistorial = false;
            _errorCargaHistorial = false;
            if (!prepend) _offsetActual = offset;
            if (!prepend) _mensajes.Clear();
            // Fachada ordena por instante real de más antiguo a más nuevo.
            // La colección conserva ese orden para que lo reciente quede abajo.
            var cronologicas = historial;
            string? fechaPrev = null;
            string? autorPrev = null;
            MensajeVM? vecino = null;
            if (prepend)
            {
                vecino = _mensajes.FirstOrDefault(m => !m.EsCabeceraDia);
            }
            else
            {
                var ultimo = _mensajes.LastOrDefault(m => !m.EsCabeceraDia);
                if (ultimo != null) { fechaPrev = ultimo.FechaCruda; autorPrev = ultimo.AutorCrudo; }
            }
            var lote = new List<MensajeVM>();
            foreach (var row in cronologicas)
            {
                string tipo = Valor(row, "tipo");
                string remitente = Valor(row, "origen");
                // UIX-28: se normaliza el tipo antes de decidir. En la base hay
                // filas viejas con "IMAGEN"/"ARCHIVO" sin el prefijo MENSAJE_, y
                // con comparación en crudo quedaban sin botón de guardar y sin
                // recogniseerse como imagen.
                bool esImagen = Transversal.BotonGuardar.EsImagen(tipo);
                bool esArchivo = Transversal.BotonGuardar.EsArchivo(tipo);
                bool propio = remitente == _fachada.UsuarioActual;
                string identidad = propio ? "Tú" : Identidad(remitente);
                string estadoBruto = Valor(row, "estado");
                string fechaCruda = Valor(row, "fechaEnvio");
                if (Transversal.FilasChat.CambiaElDia(fechaPrev, fechaCruda))
                {
                    lote.Add(new MensajeVM
                    {
                        EsCabeceraDia = true,
                        TextoCabecera = Transversal.FormatoFecha.EtiquetaDia(fechaCruda)
                    });
                }
                bool agrupado = Transversal.FilasChat.Agrupar(
                    fechaPrev, autorPrev, fechaCruda, remitente);
                string caption = (esImagen || esArchivo) ? Valor(row, "caption") : "";
                Transversal.FormatoCita.Separar(caption, out var pieCita, out var pieCuerpo);
                lote.Add(new MensajeVM
                {
                    Id = Valor(row, "id"),
                    Encabezado = agrupado ? "" : identidad,
                    Autor = identidad,
                    AutorCrudo = remitente,
                    Fecha = FormatearFecha(fechaCruda),
                    FechaCruda = fechaCruda,
                    Contenido = esImagen ? "" : Valor(row, "contenido"),
                    Estado = propio ? EstadoVisible(estadoBruto) : "",
                    EsImagen = esImagen,
                    EsArchivo = esArchivo,
                    EtapasFiltrado = Valor(row, "etapasFiltrado"),
                    EsPropio = propio,
                    EsLeido = estadoBruto == "LEIDO",
                    ArchivoId = Valor(row, "archivoId"),
                    NombreArchivo = Valor(row, "nombreArchivo"),
                    RutaArchivo = Valor(row, "rutaArchivo"),
                    Pie = pieCuerpo,
                    PieCita = pieCita
                });
                SepararCita(lote[^1], esImagen ? "" : Valor(row, "contenido"));
                fechaPrev = fechaCruda;
                autorPrev = remitente;
            }
            if (prepend)
            {
                for (int i = 0; i < lote.Count; i++) _mensajes.Insert(i, lote[i]);
                // Unión con el vecino de abajo: separador si cambia el día y
                // encabezado recalculado (ya no va pegado al anterior).
                if (vecino != null && lote.Count > 0)
                {
                    var ultimoLote = lote[^1];
                    if (!ultimoLote.EsCabeceraDia
                        && Transversal.FilasChat.CambiaElDia(ultimoLote.FechaCruda, vecino.FechaCruda))
                    {
                        _mensajes.Insert(lote.Count, new MensajeVM
                        {
                            EsCabeceraDia = true,
                            TextoCabecera = Transversal.FormatoFecha.EtiquetaDia(vecino.FechaCruda)
                        });
                    }
                    if (!ultimoLote.EsCabeceraDia)
                    {
                        vecino.Encabezado = Transversal.FilasChat.Agrupar(
                            ultimoLote.FechaCruda, ultimoLote.AutorCrudo,
                            vecino.FechaCruda, vecino.AutorCrudo) ? "" : vecino.Autor;
                    }
                }
            }
            else
            {
                foreach (var vm in lote) _mensajes.Add(vm);
            }
            if (prepend)
            {
                // Si la carga se inició para completar una búsqueda, el salto al
                // resultado tiene prioridad sobre conservar el viewport anterior.
                if (_saltoPendiente == null)
                    RestaurarAnclajeScroll(anclaje, codigo, version);
                else
                    lstMensajes.ScrollIntoView(_mensajes.FirstOrDefault());
            }
            else if (_seguirAbajoAntesDeCargar)
            {
                lstMensajes.ScrollIntoView(_mensajes.LastOrDefault());
                _nuevosPendientes = 0;
                btnNuevos.Visibility = Visibility.Collapsed;
            }
            // UIX-13: si no hay mensajes, se dice en vez de dejar negro.
            ActualizarVacioChat();
            // UIX-06: si hay un salto pendiente (búsqueda), se reintenta ahora
            // que hay más mensajes cargados.
            if (_saltoPendiente != null)
            {
                string id = _saltoPendiente;
                int intentos = _intentosSalto;
                _saltoPendiente = null;
                SaltarA(id, intentos);
            }
            int paginasRemotas = _totalPaginasRemotas.GetValueOrDefault(codigo);
            btnAnteriores.Visibility = offset + 50 < totalLocal
                || _paginaRemota.GetValueOrDefault(codigo) + 1 < paginasRemotas
                ? Visibility.Visible : Visibility.Collapsed;
            alTerminar?.Invoke();
        }

        /// <summary>
        /// UIX-26: un mensaje NO dispara una consulta. El trabajo se agrupa, como
        /// el intervalo mínimo del cliente Java: veinte mensajes seguidos son un
        /// refresco, no veinte.
        /// <para>
        /// Lo importante es que se <b>fusiona</b>, no que se descarta. Antes el
        /// segundo mensaje de una ráfaga se perdía: el primero programaba el
        /// refresco y el segundo, dentro de la ventana, no programaba nada. Como
        /// la consulta del primero se resuelve en segundo plano, el preview del
        /// segundo ya no estaba en la base y la lista se quedaba desfasada
        /// hasta el siguiente mensaje o hasta que se pulsaba "Actualizar".
        /// </para>
        /// </summary>
        private void ProgramarRefrescoLista()
        {
            _refrescoListaPedido = true;
            long ahora = Environment.TickCount64;
            if (ahora - _ultimoRefrescoListaMs < INTERVALO_MINIMO_LISTA_MS) return;
            _ultimoRefrescoListaMs = ahora;
            Dispatcher.BeginInvoke(new Action(RefrescarListaContactos));
        }

        /// <summary>
        /// NO LEÍDOS + PREVIEW + ORDEN en una sola pasada. Antes el preview iba
        /// por un lado y el contador de no leídos no iba: por eso la insignia
        /// nunca subía sola. También quita la consulta completa por mensaje.
        /// </summary>
        private async void RefrescarListaContactos()
        {
            _refrescoListaPedido = false;
            if (_refrescoListaEnCurso) { _refrescoListaPendiente = true; return; }
            _refrescoListaEnCurso = true;
            try
            {
                var noLeidos = await Task.Run(() => _fachada.ContarNoLeidos());
                var previews = await Task.Run(() => _fachada.UltimoMensajePorContacto());
                foreach (var u in _directorio)
                {
                    u.NoLeidos = noLeidos.GetValueOrDefault(u.Codigo);
                    // Sin mensajes hay que VACIAR el preview, no dejarlo como
                    // estaba: si no, un contacto que ya no aparece en la consulta
                    // se queda con el preview del mensaje anterior para siempre.
                    if (previews.TryGetValue(u.Codigo, out var prev))
                    {
                        u.Preview = prev.Texto;
                        u.PropioPreview = prev.Propio;
                    }
                    else
                    {
                        u.Preview = "";
                        u.PropioPreview = false;
                    }
                }
                AplicarFiltro(_usuarioDestino);
            }
            catch (Exception error)
            {
                // Son cosméticos: si fallan, el chat sigue igual.
                System.Diagnostics.Trace.TraceWarning(
                    "No se pudo refrescar la lista de contactos: {0}", error.Message);
            }
            finally
            {
                _refrescoListaEnCurso = false;
                // Trailing edge: si llegaron mensajes mientras se consultaba, hay
                // que volver a consultar. Antes solo se miraba `_refrescoListaPendiente`
                // (que solo se activaba por reentrada), y los que caían en la
                // ventana de agrupado se perdían sin más.
                if (_refrescoListaPendiente || _refrescoListaPedido)
                {
                    _refrescoListaPendiente = false;
                    ProgramarRefrescoLista();
                }
            }
        }

        /// <summary>UIX-24: separa la cita del cuerpo para pintarla como bloque.</summary>
        private static void SepararCita(MensajeVM vm, string? contenido)
        {
            // Paridad con Java (MensajeCell.extraerCita): la cita es lo anterior
            // al PRIMER salto doble. El regex anterior paraba en el primer "\n",
            // asi que un extracto con saltos se partia mal y el cuerpo quedaba
            // pegado a la cita.
            Transversal.FormatoCita.Separar(contenido, out var cita, out var cuerpo);
            vm.Cita = cita;
            vm.Cuerpo = cuerpo;
        }

        private async void CargarHistorialRemoto(string codigo, int pagina, bool olderPage = false)
        {
            try
            {
                int total = await _fachada.TraerHistorialAsync(codigo, pagina);
                _paginaRemota[codigo] = pagina;
                _totalPaginasRemotas[codigo] = total;
                if (pagina == 0)
                {
                    await ActualizarFechasRecientesAsync();
                    if (_usuarioDestino != null) AplicarFiltro(_usuarioDestino);
                }
                if (_usuarioDestino == codigo)
                    CargarHistorial(codigo, olderPage ? _offsetActual : 0, olderPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("No se pudo sincronizar el historial de {0}: {1}", codigo, ex);
                if (_usuarioDestino == codigo && pagina == 0)
                {
                    txtEstado.Text = "No se pudo sincronizar; mostrando el historial guardado en este equipo.";
                    MostrarAvisoInline(MensajesUsuario.Accion(ex, "sincronizar el historial"),
                        TipoAvisoInline.Error);
                }
            }
        }

        private void BtnAnteriores_Click(object sender, RoutedEventArgs e)
        {
            if (_usuarioDestino == null) return;
            string codigo = _usuarioDestino;
            int totalLocal = _fachada.ContarConversacion(codigo);
            if (_offsetActual + 50 < totalLocal)
            {
                _offsetActual += 50;
                CargarHistorial(codigo, _offsetActual, true);
                return;
            }
            int siguiente = _paginaRemota.GetValueOrDefault(codigo) + 1;
            if (siguiente < _totalPaginasRemotas.GetValueOrDefault(codigo))
            {
                _offsetActual = totalLocal;
                CargarHistorialRemoto(codigo, siguiente, true);
            }
        }

        private async void Cita_MouseLeftButtonUp(object sender,
            System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (sender is not FrameworkElement { Tag: string cita }
                || string.IsNullOrWhiteSpace(cita)) return;
            await NavegarACitaAsync(cita);
        }

        private async Task NavegarACitaAsync(string cita)
        {
            string? codigo = _usuarioDestino;
            if (codigo == null) return;
            int version = Interlocked.Increment(ref _versionNavegacionCita);
            var cargado = _mensajes.FirstOrDefault(m => !m.EsCabeceraDia
                && string.Equals(CitaDeMensaje(m), cita, StringComparison.Ordinal));
            if (cargado != null)
            {
                EnfocarMensaje(cargado);
                return;
            }

            MostrarAvisoInline("Buscando mensaje citado en el historial…",
                TipoAvisoInline.Informacion);
            barraProgreso.Visibility = Visibility.Visible;
            bool cargaDestinoIniciada = false;
            var identidades = _directorio.ToDictionary(u => u.Codigo, u => u.Etiqueta,
                StringComparer.OrdinalIgnoreCase);
            try
            {
                UbicacionCita? ubicacion = await BuscarCitaEnHistorialAsync(
                    codigo, cita, version, identidades);
                if (version != _versionNavegacionCita || codigo != _usuarioDestino) return;
                if (ubicacion == null)
                {
                    MostrarAvisoInline("El mensaje citado no está disponible en este historial.",
                        TipoAvisoInline.Informacion);
                    return;
                }

                LimpiarAvisoInline();
                int offset = (ubicacion.Rango / 50) * 50;
                cargaDestinoIniciada = true;
                CargarHistorial(codigo, offset, false, () =>
                {
                    if (version != _versionNavegacionCita || codigo != _usuarioDestino) return;
                    MensajeVM? destino = _mensajes.FirstOrDefault(m =>
                        !m.EsCabeceraDia && m.Id == ubicacion.Id);
                    if (destino == null)
                    {
                        MostrarAvisoInline("No se pudo mostrar el mensaje citado.",
                            TipoAvisoInline.Advertencia);
                        return;
                    }
                    EnfocarMensaje(destino);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    "No se pudo navegar al mensaje citado de {0}: {1}", codigo, ex);
                if (version == _versionNavegacionCita && codigo == _usuarioDestino)
                    MostrarAvisoInline(MensajesUsuario.Accion(ex, "buscar el mensaje citado"),
                        TipoAvisoInline.Error);
            }
            finally
            {
                if (!cargaDestinoIniciada && version == _versionNavegacionCita)
                    barraProgreso.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<UbicacionCita?> BuscarCitaEnHistorialAsync(
            string codigo, string cita, int version,
            IReadOnlyDictionary<string, string> identidades)
        {
            int totalLocal = await Task.Run(() => _fachada.ContarConversacion(codigo));
            UbicacionCita? encontrada = await Task.Run(() =>
                BuscarCitaEnRango(codigo, cita, 0, totalLocal, identidades, version));
            if (version != _versionNavegacionCita) return null;
            if (encontrada != null) return encontrada;

            int pagina = Math.Max(0, _paginaRemota.GetValueOrDefault(codigo));
            int totalPaginas = _totalPaginasRemotas.GetValueOrDefault(codigo);
            do
            {
                if (version != _versionNavegacionCita || codigo != _usuarioDestino)
                    return null;

                int totalAnterior = totalLocal;
                totalPaginas = await _fachada.TraerHistorialAsync(codigo, pagina);
                _paginaRemota[codigo] = pagina;
                _totalPaginasRemotas[codigo] = totalPaginas;
                totalLocal = await Task.Run(() => _fachada.ContarConversacion(codigo));
                int desde = pagina == 0 ? 0 : totalAnterior;
                encontrada = await Task.Run(() =>
                    BuscarCitaEnRango(codigo, cita, desde, totalLocal, identidades, version));
                if (version != Volatile.Read(ref _versionNavegacionCita)) return null;
                if (encontrada != null) return encontrada;
                pagina++;
            }
            while (pagina < totalPaginas);

            return null;
        }

        private UbicacionCita? BuscarCitaEnRango(string codigo, string cita,
            int desde, int hasta, IReadOnlyDictionary<string, string> identidades, int version)
        {
            for (int offset = (Math.Max(0, desde) / 50) * 50; offset < hasta; offset += 50)
            {
                if (version != Volatile.Read(ref _versionNavegacionCita)) return null;
                List<Dictionary<string, object>> pagina = _fachada.CargarHistorial(codigo, offset);
                for (int indice = 0; indice < pagina.Count; indice++)
                {
                    int rango = offset + pagina.Count - 1 - indice;
                    if (rango < desde || rango >= hasta) continue;
                    var fila = pagina[indice];
                    if (string.Equals(CitaDeFila(fila, identidades), cita,
                        StringComparison.Ordinal))
                        return new UbicacionCita(Valor(fila, "id"), rango);
                }
            }
            return null;
        }

        private string CitaDeFila(Dictionary<string, object> fila,
            IReadOnlyDictionary<string, string> identidades)
        {
            string remitente = Valor(fila, "origen");
            string tipo = Valor(fila, "tipo");
            bool esImagen = Transversal.BotonGuardar.EsImagen(tipo);
            string autor = string.Equals(remitente, _fachada.UsuarioActual,
                StringComparison.OrdinalIgnoreCase) ? "Tú"
                : identidades.GetValueOrDefault(remitente, remitente);
            return FormatoCita.Prefijo(autor,
                esImagen ? "" : Valor(fila, "contenido"), esImagen,
                Valor(fila, "nombreArchivo")).TrimEnd('\n');
        }

        private static string CitaDeMensaje(MensajeVM mensaje) =>
            FormatoCita.Prefijo(mensaje.Autor, mensaje.EsImagen ? "" : mensaje.Contenido,
                mensaje.EsImagen, mensaje.NombreArchivo).TrimEnd('\n');

        private void EnfocarMensaje(MensajeVM mensaje)
        {
            lstMensajes.ScrollIntoView(mensaje);
            lstMensajes.SelectedItem = mensaje;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    if (!ReferenceEquals(mensaje, lstMensajes.SelectedItem)
                        || ! _mensajes.Contains(mensaje)) return;
                    lstMensajes.UpdateLayout();
                    if (lstMensajes.ItemContainerGenerator.ContainerFromItem(mensaje)
                        is not FrameworkElement contenedor) return;
                    var scroll = EncontrarScroll(lstMensajes);
                    if (scroll == null) return;
                    try
                    {
                        double arriba = contenedor.TransformToAncestor(scroll)
                            .Transform(new Point(0, 0)).Y;
                        double centro = Math.Max(0, (scroll.ViewportHeight
                            - contenedor.ActualHeight) / 2);
                        double desplazamiento = Math.Clamp(scroll.VerticalOffset + arriba - centro,
                            0, scroll.ScrollableHeight);
                        scroll.ScrollToVerticalOffset(desplazamiento);
                    }
                    catch (InvalidOperationException)
                    {
                        lstMensajes.ScrollIntoView(mensaje);
                    }
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                        new Action(() => ResaltarMensaje(mensaje)));
                }));
        }

        private void ResaltarMensaje(MensajeVM mensaje)
        {
            if (lstMensajes.ItemContainerGenerator.ContainerFromItem(mensaje)
                is not DependencyObject contenedor) return;
            Border? burbuja = BuscarVisual<Border>(contenedor);
            if (burbuja == null) return;
            var efectoAnterior = burbuja.Effect;
            var resplandor = new DropShadowEffect
            {
                Color = Color.FromRgb(255, 209, 102),
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0
            };
            burbuja.Effect = resplandor;
            var animacion = new DoubleAnimation(0, 0.9, TimeSpan.FromMilliseconds(250))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            animacion.Completed += (_, _) =>
            {
                if (!ReferenceEquals(burbuja.Effect, resplandor)) return;
                resplandor.BeginAnimation(DropShadowEffect.OpacityProperty, null);
                burbuja.Effect = efectoAnterior;
            };
            resplandor.BeginAnimation(DropShadowEffect.OpacityProperty, animacion);
        }

        private static T? BuscarVisual<T>(DependencyObject raiz) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
            {
                DependencyObject hijo = VisualTreeHelper.GetChild(raiz, i);
                if (hijo is T encontrado) return encontrado;
                T? descendiente = BuscarVisual<T>(hijo);
                if (descendiente != null) return descendiente;
            }
            return null;
        }

        private sealed record UbicacionCita(string Id, int Rango);

        /// <summary>UIX-16: True si ya se está al fondo (margen 60 px).</summary>
        private bool EstaAbajo()
        {
            var scroll = EncontrarScroll(lstMensajes);
            if (scroll == null) return true;
            return scroll.VerticalOffset >= scroll.ScrollableHeight - 60;
        }

        private AnclajeMensajeVisible? CapturarAnclajeScroll()
        {
            var scroll = EncontrarScroll(lstMensajes);
            if (scroll == null) return null;
            lstMensajes.UpdateLayout();
            scroll.UpdateLayout();
            for (int indice = 0; indice < _mensajes.Count; indice++)
            {
                var mensaje = _mensajes[indice];
                if (mensaje.EsCabeceraDia
                    || lstMensajes.ItemContainerGenerator.ContainerFromIndex(indice)
                        is not FrameworkElement contenedor)
                    continue;
                try
                {
                    double arriba = contenedor.TransformToAncestor(scroll)
                        .Transform(new Point(0, 0)).Y;
                    if (arriba < scroll.ViewportHeight
                        && arriba + contenedor.ActualHeight > 0)
                        return new AnclajeMensajeVisible(mensaje, arriba);
                }
                catch (InvalidOperationException)
                {
                    // El contenedor puede salir del árbol visual durante virtualización.
                }
            }
            return null;
        }

        private void RestaurarAnclajeScroll(AnclajeMensajeVisible? anclaje,
            string codigo, int version)
        {
            if (anclaje == null || !_mensajes.Contains(anclaje.Mensaje))
            {
                lstMensajes.ScrollIntoView(_mensajes.FirstOrDefault());
                return;
            }

            // ScrollIntoView fuerza la realización del contenedor aun con
            // virtualización; el ajuste se aplica después del layout.
            lstMensajes.ScrollIntoView(anclaje.Mensaje);
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    if (_usuarioDestino != codigo || version != _versionCargaHistorial
                        || !_mensajes.Contains(anclaje.Mensaje)) return;
                    lstMensajes.UpdateLayout();
                    var scroll = EncontrarScroll(lstMensajes);
                    if (scroll == null) return;
                    scroll.UpdateLayout();
                    int indice = lstMensajes.Items.IndexOf(anclaje.Mensaje);
                    if (indice < 0 || lstMensajes.ItemContainerGenerator.ContainerFromIndex(indice)
                        is not FrameworkElement contenedor) return;
                    try
                    {
                        double arriba = contenedor.TransformToAncestor(scroll)
                            .Transform(new Point(0, 0)).Y;
                        double desplazamiento = Transversal.AnclajeScroll.AjustarDesplazamiento(
                            scroll.VerticalOffset, arriba, anclaje.CoordenadaVertical);
                        scroll.ScrollToVerticalOffset(desplazamiento);
                    }
                    catch (InvalidOperationException)
                    {
                        // El siguiente layout/precarga podrá volver a anclar el mismo mensaje.
                    }
                }));
        }

        private static System.Windows.Controls.ScrollViewer? EncontrarScroll(DependencyObject raiz)
        {
            if (raiz is System.Windows.Controls.ScrollViewer scroll) return scroll;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(raiz); i++)
            {
                var hallado = EncontrarScroll(System.Windows.Media.VisualTreeHelper.GetChild(raiz, i));
                if (hallado != null) return hallado;
            }
            return null;
        }

        private void BtnNuevos_Click(object sender, RoutedEventArgs e)
        {
            _nuevosPendientes = 0;
            btnNuevos.Visibility = Visibility.Collapsed;
            if (_usuarioDestino != null) CargarHistorial(_usuarioDestino);
        }

        private void AvisarNuevo()
        {
            _nuevosPendientes++;
            btnNuevos.Content = $"↓ {_nuevosPendientes} nuevos";
            btnNuevos.Visibility = Visibility.Visible;
        }

        /// <summary>UIX-13: si no hay mensajes, se dice en vez de dejar negro.</summary>
        private void ActualizarVacioChat()
        {
            bool hay = _mensajes.Any(m => !m.EsCabeceraDia);
            txtVacioChat.Visibility = hay ? Visibility.Collapsed : Visibility.Visible;
            if (!hay)
            {
                txtVacioChat.Text = _usuarioDestino == null
                    ? "Elige un contacto para empezar a conversar."
                    : _cargandoHistorial ? "Cargando conversación…"
                    : _errorCargaHistorial ? "No se pudo cargar el historial. Revisa la conexión e inténtalo de nuevo."
                    : "Sin mensajes aún. Escribe el primero.";
            }
        }

        // ------------------------------------------------- UIX-06: buscar en el historial

        private void MostrarBuscar()
        {
            if (_usuarioDestino == null)
            {
                txtEstado.Text = "Elige una conversación para buscar en su historial.";
                return;
            }
            barraBuscar.Visibility = Visibility.Visible;
            txtBuscarHistorial.Focus();
            txtBuscarHistorial.SelectAll();
        }

        private void CerrarBuscar()
        {
            barraBuscar.Visibility = Visibility.Collapsed;
            txtBuscarHistorial.Clear();
            _patronBusqueda = "";
            _idsBusqueda.Clear();
            _indiceBusqueda = -1;
            _saltoPendiente = null;
            txtBuscarContador.Text = "";
            foreach (var vm in _mensajes) vm.PatronResaltado = "";
        }

        private void BtnBuscarCerrar_Click(object sender, RoutedEventArgs e) => CerrarBuscar();

        private void TxtBuscarHistorial_TextChanged(object sender, TextChangedEventArgs e)
        {
            // UIX-07: la query a SQLite espera 300 ms tras la última tecla.
            _timerBuscar.Stop();
            _timerBuscar.Start();
        }

        private void TxtBuscarHistorial_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                    BuscarAnterior();
                else
                    BuscarSiguiente();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                CerrarBuscar();
                e.Handled = true;
            }
        }

        private void BtnBuscarSiguiente_Click(object sender, RoutedEventArgs e) => BuscarSiguiente();

        private void BtnBuscarAnterior_Click(object sender, RoutedEventArgs e) => BuscarAnterior();

        private void EjecutarBusqueda()
        {
            string? codigo = _usuarioDestino;
            string patron = (txtBuscarHistorial.Text ?? "").Trim();
            if (codigo == null || patron.Length == 0)
            {
                _patronBusqueda = "";
                _idsBusqueda.Clear();
                _indiceBusqueda = -1;
                ActualizarContadorBusqueda();
                foreach (var vm in _mensajes) vm.PatronResaltado = "";
                return;
            }
            _patronBusqueda = patron;
            _idsBusqueda.Clear();
            _idsBusqueda.AddRange(_fachada.BuscarEnHistorial(codigo, patron));
            _indiceBusqueda = _idsBusqueda.Count > 0 ? 0 : -1;
            ActualizarContadorBusqueda();
            foreach (var vm in _mensajes) vm.PatronResaltado = patron;
            if (_idsBusqueda.Count > 0) SaltarA(_idsBusqueda[0], 5);
        }

        private void ActualizarContadorBusqueda()
        {
            txtBuscarContador.Text = _idsBusqueda.Count == 0
                ? (_patronBusqueda.Length == 0 ? "" : "Sin resultados")
                : $"{_indiceBusqueda + 1}/{_idsBusqueda.Count}";
        }

        private void BuscarSiguiente()
        {
            if (_idsBusqueda.Count == 0) return;
            _indiceBusqueda = (_indiceBusqueda + 1) % _idsBusqueda.Count;
            ActualizarContadorBusqueda();
            SaltarA(_idsBusqueda[_indiceBusqueda], 5);
        }

        private void BuscarAnterior()
        {
            if (_idsBusqueda.Count == 0) return;
            _indiceBusqueda = (_indiceBusqueda - 1 + _idsBusqueda.Count) % _idsBusqueda.Count;
            ActualizarContadorBusqueda();
            SaltarA(_idsBusqueda[_indiceBusqueda], 5);
        }

        /// <summary>
        /// Baja al mensaje: si está cargado, scroll + selección; si no, trae
        /// una página más y reintenta (acotado).
        /// </summary>
        private void SaltarA(string id, int intentos)
        {
            if (_usuarioDestino == null) return;
            var vm = _mensajes.FirstOrDefault(m => !m.EsCabeceraDia && m.Id == id);
            if (vm != null)
            {
                lstMensajes.ScrollIntoView(vm);
                lstMensajes.SelectedItem = vm;
                return;
            }
            if (intentos <= 0 || btnAnteriores.Visibility != Visibility.Visible)
            {
                txtBuscarContador.Text = "Fuera del historial cargado";
                return;
            }
            _saltoPendiente = id;
            _intentosSalto = intentos - 1;
            BtnAnteriores_Click(this, new RoutedEventArgs());
        }

        private async void MessageImage_Loaded(object sender, RoutedEventArgs e)
        {
            // La virtualizacion puede disparar Loaded dos veces para el mismo
            // mensaje: la segunda no debe reprogramar una carga en vuelo.
            if (sender is Image { DataContext: MensajeVM vm } && !vm.Cargando && !vm.Solicitada)
                await CargarImagenAsync(vm);
        }

        /// <summary>
        /// UIX-27: devuelve una ruta que WPF sabe abrir.
        /// <para>
        /// El <c>.webp</c> se guarda tal cual —es el archivo que envió el otro y
        /// el que se descarga con "Guardar"— pero ni WPF ni JavaFX saben
        /// pintarlo. Aquí se hace una <b>copia</b> al lado en PNG solo para pintar.
        /// No se toca el original.
        /// </para>
        /// </summary>
        private static string PrepararParaVer(string original)
        {
            if (!TranscodificadorImagen.NecesitaConversion(original)) return original;
            string copia = Path.ChangeExtension(original, null) + "_vista.png";
            if (File.Exists(copia)) return copia;
            try
            {
                return TranscodificadorImagen.ConvertirAPng(original, copia);
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "No se pudo convertir para mostrar {0}: {1}", original, error.Message);
                return original;
            }
        }

        private async Task CargarImagenAsync(MensajeVM vm)
        {
            if (vm.Imagen != null || vm.Cargando || vm.Solicitada) return;
            // UIX-09: una sola carga por mensaje; la anterior se cancela.
            if (_cargasImagen.TryGetValue(vm.Id, out var previa))
            {
                try { previa.Cancel(); } catch { }
                _cargasImagen.Remove(vm.Id);
            }
            var cts = new CancellationTokenSource();
            _cargasImagen[vm.Id] = cts;
            CancellationToken token = cts.Token;
            vm.Cargando = true;
            vm.EstadoImagen = "Cargando imagen…";
            // Si la espera se cancela ANTES de tomar el turno no se adquirio el
            // semaforo: liberar en ese caso lo desborda (SemaphoreFullException)
            // y, al ser un async void, se lleva por delante la aplicacion.
            bool adquirido = false;
            try
            {
                await _limiteImagenes.WaitAsync(token).ConfigureAwait(true);
                adquirido = true;
                token.ThrowIfCancellationRequested();
                    var bitmap = await Task.Run(async () =>
                    {
                        token.ThrowIfCancellationRequested();
                        string ruta = vm.RutaArchivo;
                    if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
                    {
                        string extension = Path.GetExtension(vm.NombreArchivo);
                        if (string.IsNullOrWhiteSpace(extension)) extension = ".img";
                        string carpeta = Path.Combine(Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData), "Mensajeria", "imagenes");
                        Directory.CreateDirectory(carpeta);
                        ruta = Path.Combine(carpeta, vm.Id + extension);
                        if (!File.Exists(ruta))
                        {
                            if (!string.IsNullOrWhiteSpace(vm.ArchivoId))
                            {
                                await _fachada.DescargarArchivoAsync(vm.Id, vm.ArchivoId, ruta);
                            }
                            else
                            {
                                string? local = _fachada.ContenidoImagenLocal(vm.Id);
                                if (string.IsNullOrWhiteSpace(local) || local == "[Imagen]")
                                    throw new InvalidOperationException("La imagen no tiene archivo disponible.");
                                byte[] imagenLocal = Convert.FromBase64String(local);
                                if (imagenLocal.Length == 0)
                                    throw new InvalidOperationException("La imagen local está vacía.");
                                await File.WriteAllBytesAsync(ruta, imagenLocal);
                            }
                        }
                    }
                    // UIX-27: la conversión es SOLO para la vista. El archivo
                    // que se descarga es el original y se queda en disco tal cual
                    // (por eso "Guardar" entrega el .webp de verdad); lo que se
                    // pinta es una copia al lado, porque WPF no abre WebP.
                    string paraVer = PrepararParaVer(ruta);
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(paraVer, UriKind.Absolute);
                    image.EndInit();
                    image.Freeze();
                    return (Image: image, Path: ruta);
                });
                vm.Imagen = bitmap.Image;
                vm.RutaArchivo = bitmap.Path;
                vm.EstadoImagen = vm.NombreArchivo;
            }
            catch (OperationCanceledException)
            {
                // UIX-09: cancelación del usuario o cambio de vista: silencio,
                // sin ofrecer reintento de algo que ya se dejó atrás.
                vm.EstadoImagen = "Carga cancelada";
            }
            catch (Exception ex)
            {
                vm.EstadoImagen = MensajesUsuario.Archivo(ex);
                vm.Solicitada = true;
            }
            finally
            {
                // Solo se devuelve el turno que se llego a tomar.
                if (adquirido) _limiteImagenes.Release();
                vm.Cargando = false;
                _cargasImagen.Remove(vm.Id);
            }
        }

        private async void BtnReintentarImagen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: MensajeVM vm })
            {
                vm.Solicitada = false;
                await CargarImagenAsync(vm);
            }
        }

        private void BtnCancelarImagen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: MensajeVM vm }) CancelarCargaImagen(vm.Id);
        }

        /// <summary>UIX-09: cancela la carga en vuelo de ese mensaje.</summary>
        private void CancelarCargaImagen(string id)
        {
            if (_cargasImagen.TryGetValue(id, out var cts))
            {
                try { cts.Cancel(); } catch { }
            }
        }

        /// <summary>UIX-09: cancela todas las cargas (cambio de chat o salida).</summary>
        private void CancelarTodasLasCargas()
        {
            foreach (var cts in _cargasImagen.Values)
            {
                try { cts.Cancel(); } catch { }
            }
            _cargasImagen.Clear();
        }

        /// <summary>Prefijo completo de la respuesta en curso ("" si no hay).</summary>
        private string _cita = "";

        private void BtnResponder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: MensajeVM vm }) return;
            ResponderA(vm);
        }

        private void LstMensajes_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Telegram Web: doble-click en cualquier mensaje = responder.
            if (lstMensajes.SelectedItem is MensajeVM vm) ResponderA(vm);
        }

        private void MessageImage_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Un click en imagen abre grande; el doble-click lo gestiona LstMensajes_DoubleClick (responder).
            if (e.ClickCount >= 2) return;
            if (sender is System.Windows.Controls.Image { DataContext: MensajeVM vm, Source: not null })
                AbrirImagenGrande(vm);
        }

        private static void AbrirImagenGrande(MensajeVM vm)
        {
            // UIX-08: visor con zoom y pan en vez de ventana fija.
            try
            {
                new ImagenViewerWindow(vm.Imagen,
                    string.IsNullOrWhiteSpace(vm.NombreArchivo) ? "Imagen" : vm.NombreArchivo).Show();
            }
            catch { }
        }

        private void ResponderA(MensajeVM vm)
        {
            string autor = string.IsNullOrWhiteSpace(vm.Autor)
                ? vm.Encabezado.Split('·')[0].Trim() : vm.Autor;
            // Paridad con Java (FormatoRespuesta): el extracto se colapsa a una
            // sola linea y el prefijo termina en "\n\n". Sin esto un original
            // con saltos hacia que el cuerpo quedara pegado a la cita.
            _cita = Transversal.FormatoCita.Prefijo(autor, vm.Contenido, vm.EsImagen, vm.NombreArchivo);
            txtRespuesta.Text = _cita.TrimEnd('\n');
            bordeRespuesta.Visibility = Visibility.Visible;
            txtMensaje.Focus();
            ActualizarEstadoEnvio();
        }

        private void BtnQuitarRespuesta_Click(object sender, RoutedEventArgs e) => LimpiarCita();

        private void LimpiarCita()
        {
            _cita = "";
            txtRespuesta.Text = "";
            bordeRespuesta.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// UIX-20: Enter envía, Mayús+Enter mete salto de línea.
        /// Va en <c>PreviewKeyDown</c> a propósito: con <c>AcceptsReturn="True"</c>
        /// el <c>TextBox</c> consume Enter en su manejador de clase, que corre
        /// antes que cualquier manejador de instancia de <c>KeyDown</c>; el salto
        /// se insertaba antes de poder cancelarlo y el mensaje nunca salía.
        /// En la fase de túnel el evento se cancela a tiempo.
        /// </summary>
        private void TxtMensaje_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (_enviando)
                    _ctsEnvio?.Cancel();
                else
                {
                    LimpiarCita();
                    _adjuntos.Limpiar();
                    ActualizarPanelAdjuntos();
                }
                e.Handled = true;
                return;
            }
            if (e.Key != System.Windows.Input.Key.Enter) return;
            // Mayús reserva el salto de línea. Se consulta el modificador del
            // propio evento y no el teclado físico: así funciona igual con
            // Caps Lock y con cualquier disposición de teclado.
            if (e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                return;
            e.Handled = true;
            EnviarMensajeActual();
        }

        /// <summary>
        /// Envío compartido por el botón y por Enter. Se separa del manejador
        /// <c>Click</c> para no inventarse un RoutedEventArgs sintético.
        /// </summary>
        private async void EnviarMensajeActual()
        {
            if (string.IsNullOrEmpty(_usuarioDestino) || _enviando) return;
            string texto = (txtMensaje.Text ?? "").Trim();
            IReadOnlyList<AdjuntoPendiente> adjuntos = _adjuntos.Snapshot();
            if (texto.Length == 0 && adjuntos.Count == 0) return;
            LimpiarAvisoInline();
            _enviando = true;
            _ctsEnvio = new CancellationTokenSource();
            btnEnviar.IsEnabled = false;
            panelAdjuntos.IsEnabled = false;
            panelProgresoEnvio.Visibility = Visibility.Visible;
            barraProgresoEnvio.Value = 0;
            txtProgresoEnvio.Text = adjuntos.Count > 0
                ? $"Preparando {adjuntos.Count} archivo(s)…" : "Enviando mensaje…";
            ActualizarEstadoEnvio();
            try
            {
                string destino = _usuarioDestino;
                string cita = _cita;
                // PLAN_AMBOS FASE 5: el texto va pegado a los adjuntos SOLO si
                // estos forman una única burbuja (uno suelto, o varias imágenes
                // montadas). Si van en varias burbujas se manda antes suelto: un
                // pie no puede repetirse en N. Mismo criterio que en Java.
                string? pie = adjuntos.Count > 0 && PlanEnvioAdjuntos.TextoVaComoPie(adjuntos)
                    ? cita + texto
                    : null;
                if (texto.Length > 0 && pie == null)
                {
                    bool textoEnRed = await Task.Run(() =>
                        _fachada.EnviarTextoAsync(destino, cita + texto));
                    txtMensaje.Clear();
                    if (!textoEnRed)
                        MostrarAvisoInline("Sin red: el texto quedó guardado para reintentar.",
                            TipoAvisoInline.Informacion);
                }

                if (adjuntos.Count > 0)
                {
                    // Construir Progress<T> en el dispatcher captura el contexto de UI.
                    var progreso = new Progress<ProgresoEnvio>(p =>
                    {
                        barraProgresoEnvio.Value = p.FraccionGlobal * 100d;
                        txtProgresoEnvio.Text =
                            $"Enviando {p.IndiceActual + 1} de {p.TotalArchivos} · {p.NombreActual} · "
                            + $"{p.FraccionArchivo:P0} · {p.FraccionGlobal:P0} del lote";
                        var vm = _adjuntos.BuscarIndice(p.IndiceActual);
                        if (vm != null) vm.Progreso = p.FraccionArchivo;
                    });
                    ResultadoEnvioMultiple resultado = await Task.Run(() =>
                        _fachada.EnviarAdjuntosAsync(destino, adjuntos, progreso,
                            _ctsEnvio.Token, string.IsNullOrEmpty(pie) ? null : pie));
                    AplicarResultadoAdjuntos(resultado);
                    _adjuntos.Limpiar();
                    ActualizarPanelAdjuntos();
                    MostrarResultado(resultado);
                    // Red de seguridad: el texto iba pegado al adjunto, así que si
                    // NINGÚN adjunto salió (el archivo ya no está, disco lleno, se
                    // perdió la red) el texto se habría perdido con él. Se manda
                    // entonces como mensaje propio: peor una burbuja de más que
                    // perder lo que el usuario escribió.
                    if (pie != null && resultado.Enviados == 0)
                    {
                        await Task.Run(() => _fachada.EnviarTextoAsync(destino, pie));
                    }
                    txtMensaje.Clear();
                }
                LimpiarCita();
                if (_usuarioDestino == destino)
                {
                    CargarHistorial(destino);
                    MarcarConversacionReciente(destino);
                }
            }
            catch (OperationCanceledException)
            {
                MostrarAvisoInline("Envío cancelado. Los archivos ya enviados no se pueden deshacer.",
                    TipoAvisoInline.Informacion);
            }
            catch (NullReferenceException) { MostrarAvisoInline("Sesión no válida, vuelve a iniciar sesión.", TipoAvisoInline.Error); }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Falló el envío de un mensaje: {0}", ex);
                MostrarAvisoInline(MensajesUsuario.Accion(ex, "enviar el mensaje"), TipoAvisoInline.Error);
            }
            finally
            {
                _enviando = false;
                _ctsEnvio?.Dispose();
                _ctsEnvio = null;
                panelProgresoEnvio.Visibility = Visibility.Collapsed;
                panelAdjuntos.IsEnabled = true;
                ActualizarEstadoEnvio();
                if (_cerrarTrasCancelar)
                {
                    bool volverAlInicio = _cerrarSesionTrasCancelar;
                    _cerrarTrasCancelar = false;
                    _cerrarSesionTrasCancelar = false;
                    if (volverAlInicio) VolverAlInicio();
                    Close();
                }
            }
        }

        private void BtnEnviar_Click(object sender, RoutedEventArgs e) => EnviarMensajeActual();

        /// <summary>
        private void TxtMensaje_TextChanged(object sender, TextChangedEventArgs e) => ActualizarEstadoEnvio();

        private void ActualizarEstadoEnvio() =>
            btnEnviar.IsEnabled = !_enviando && _usuarioDestino != null
                && (!string.IsNullOrWhiteSpace(txtMensaje.Text) || _adjuntos.HayAdjuntos);

        private void BtnAdjuntar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_usuarioDestino) || _enviando) return;
            var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = CatalogoAdjuntos.FiltroDialogo,
                Title = "Elegir archivos (imágenes, PDF, documentos...)"
            };
            if (dlg.ShowDialog() != true) return;
            IncorporarRutas(dlg.FileNames);
        }

        private void ZonaEnvio_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void ZonaEnvio_Drop(object sender, DragEventArgs e)
        {
            if (string.IsNullOrEmpty(_usuarioDestino) || _enviando) return;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var archivos = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (archivos is not { Length: > 0 }) return;
            IncorporarRutas(archivos);
        }

        private void IncorporarRutas(IEnumerable<string> rutas)
        {
            var (aceptados, duplicados, invalidos, grandes) = _adjuntos.Agregar(rutas);
            ActualizarPanelAdjuntos();
            if (_adjuntos.ImagenesSobreLimiteTrama > 0)
                MostrarAvisoInline($"{_adjuntos.ImagenesSobreLimiteTrama} imagen(es) supera(n) el límite "
                    + "de envío directo (12 MB) y no se adjuntaron.", TipoAvisoInline.Advertencia);
            else if (grandes > 0)
                MostrarAvisoInline($"{grandes} archivo(s) supera(n) los límites de tamaño y no se adjuntaron.",
                    TipoAvisoInline.Advertencia);
            else if (invalidos > 0)
                MostrarAvisoInline($"{invalidos} archivo(s) no se pudieron leer.",
                    TipoAvisoInline.Advertencia);
            else if (duplicados > 0)
                MostrarAvisoInline($"{duplicados} archivo(s) ya estaban en la lista.",
                    TipoAvisoInline.Informacion);

            if (aceptados > 0 && _adjuntos.TotalBytes > LoteMaximoBytes())
                MostrarAvisoInline($"El lote suma {CatalogoAdjuntos.TamanoLegible(_adjuntos.TotalBytes)}. "
                    + "El envío puede tardar varios minutos.", TipoAvisoInline.Advertencia);
            ActualizarEstadoEnvio();
        }

        private long LoteMaximoBytes() =>
            (long)Transversal.ClienteConfig.Entero("adjuntos.lote-max-mb", 200, 10, 1000)
            * 1024 * 1024;

        private void ActualizarPanelAdjuntos()
        {
            panelAdjuntos.Visibility = _adjuntos.HayAdjuntos
                ? Visibility.Visible : Visibility.Collapsed;
            txtResumenAdjuntos.Text = _adjuntos.TextoResumen;
            ActualizarEstadoEnvio();
        }

        private void BtnQuitarAdjunto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: AdjuntoVM vm })
                _adjuntos.Quitar(vm);
            ActualizarPanelAdjuntos();
        }

        private void BtnQuitarTodosAdjuntos_Click(object sender, RoutedEventArgs e)
        {
            if (_enviando) return;
            _adjuntos.Limpiar();
            ActualizarPanelAdjuntos();
        }

        private void BtnCancelarEnvio_Click(object sender, RoutedEventArgs e)
        {
            _ctsEnvio?.Cancel();
            txtProgresoEnvio.Text = "Cancelando…";
        }

        private void AplicarResultadoAdjuntos(ResultadoEnvioMultiple resultado)
        {
            for (int indice = 0; indice < resultado.Adjuntos.Count; indice++)
            {
                ResultadoEnvioAdjunto detalle = resultado.Adjuntos[indice];
                AdjuntoVM? vm = _adjuntos.BuscarIndice(indice);
                if (vm == null) continue;
                vm.Estado = detalle.Estado;
                vm.Error = detalle.Motivo;
                if (detalle.Estado == EstadoEnvioAdjunto.Enviado) vm.Progreso = 1d;
            }
        }

        private void MostrarResultado(ResultadoEnvioMultiple resultado)
        {
            bool canceladoPorUsuario = resultado.Adjuntos.Any(a => a.Motivo == "cancelado");
            if (resultado.Cancelado && resultado.NoIntentados > 0)
            {
                string prefijo = canceladoPorUsuario
                    ? "Envío cancelado. Los archivos ya enviados no se pueden deshacer."
                    : $"Se enviaron {resultado.Enviados} de {resultado.Total}.";
                string cola = resultado.EnCola > 0
                    ? $" {resultado.EnCola} archivo(s) quedaron en cola para reintentar."
                    : "";
                MostrarAvisoInline($"{prefijo}{cola} El resto no se intentó.",
                    TipoAvisoInline.Advertencia);
            }
            else if (resultado.EnCola > 0)
                MostrarAvisoInline($"{resultado.EnCola} archivo(s) quedaron en cola para reintentar.",
                    TipoAvisoInline.Informacion);
            else if (resultado.TodoEnviado)
                MostrarAvisoInline($"Se enviaron {resultado.Enviados} archivo(s).",
                    TipoAvisoInline.Exito);
            if (resultado.Fallidos > 0)
                MostrarAvisoInline($"{resultado.Fallidos} archivo(s) no se pudieron enviar.",
                    TipoAvisoInline.Error);
        }

        private async void BtnDescargarArchivo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: MensajeVM vm } button) return;
            var dlg = new SaveFileDialog
            {
                FileName = string.IsNullOrWhiteSpace(vm.NombreArchivo) ? "archivo" : vm.NombreArchivo
            };
            if (dlg.ShowDialog() != true) return;
            button.IsEnabled = false;
            try
            {
                string ruta = dlg.FileName;
                if (!string.IsNullOrWhiteSpace(vm.ArchivoId))
                {
                    await Task.Run(() => _fachada.DescargarArchivoAsync(vm.Id, vm.ArchivoId, ruta));
                }
                else
                {
                    // Sin identificador remoto: respaldo con el contenido local
                    // (imágenes cuyo base64 vive en el historial local).
                    System.Diagnostics.Debug.WriteLine(
                        $"Descarga sin archivoId: Id={vm.Id} EsImagen={vm.EsImagen} EsArchivo={vm.EsArchivo} Nombre={vm.NombreArchivo}");
                    string? local = _fachada.ContenidoImagenLocal(vm.Id);
                    if (string.IsNullOrWhiteSpace(local) || local == "[Imagen]")
                        throw new InvalidOperationException("El servidor no entregó el identificador de este archivo.");
                    byte[] bytes = Convert.FromBase64String(local);
                    if (bytes.Length == 0)
                        throw new InvalidOperationException("El contenido local está vacío.");
                    await File.WriteAllBytesAsync(ruta, bytes);
                }
                vm.RutaArchivo = ruta;
                MostrarAvisoInline($"Archivo guardado en {ruta}.", TipoAvisoInline.Exito);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("No se pudo descargar un archivo: {0}", ex);
                MostrarAvisoInline(MensajesUsuario.Accion(ex, "descargar el archivo"), TipoAvisoInline.Error);
            }
            finally
            {
                button.IsEnabled = true;
            }
        }

        private async void BtnKickOtras_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _fachada.ExpulsarOtrasSesionesAsync();
                MostrarAvisoInline("Se solicitó cerrar tus otras sesiones", TipoAvisoInline.Informacion);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("No se pudieron cerrar las otras sesiones: {0}", ex);
                MostrarAvisoInline(MensajesUsuario.Accion(ex, "cerrar las otras sesiones"), TipoAvisoInline.Error);
            }
        }

        private void BtnSalir_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("¿Deseas salir y cerrar esta sesión?", "Confirmar salida",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (_enviando)
            {
                _cerrarTrasCancelar = true;
                _cerrarSesionTrasCancelar = true;
                _ctsEnvio?.Cancel();
                MostrarAvisoInline("Cancelando el envío antes de cerrar…",
                    TipoAvisoInline.Informacion);
                return;
            }
            VolverAlInicio();
            Close();
        }

        private async void BtnSalirTodos_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("¿Cerrar todas las sesiones de esta cuenta?", "Confirmar salida",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (_enviando)
            {
                _cerrarTrasCancelar = true;
                _cerrarSesionTrasCancelar = true;
                _ctsEnvio?.Cancel();
                MostrarAvisoInline("Cancelando el envío antes de cerrar…",
                    TipoAvisoInline.Informacion);
                return;
            }
            try
            {
                await _fachada.ExpulsarOtrasSesionesAsync();
                VolverAlInicio();
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("No se pudieron cerrar las otras sesiones: {0}", ex);
                MostrarAvisoInline(MensajesUsuario.Accion(ex, "cerrar las otras sesiones"), TipoAvisoInline.Error);
            }
        }

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
