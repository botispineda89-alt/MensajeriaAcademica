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
    public partial class ChatWindow
    {
        // Directorio, presencia, filtro y refresco de la lista de contactos
        // Solo code-behind: los campos y el constructor viven en ChatWindow.xaml.cs;
        // este parcial agrupa una sola responsabilidad para poder localizarlo.

        private async void CargarUsuarios()
        {
            List<Dictionary<string, object>> lista;
            try { lista = await _fachada.ListarUsuariosAsync(); }
            catch { lista = await Task.Run(() => _fachada.DirectorioLocal()); }
            await ActualizarFechasRecientesAsync();
            string? seleccionado = (lstUsuarios.SelectedItem as UsuarioVM)?.Codigo ?? UsuarioDestino;
            // P4: identidad de la cuenta (nombre completo + código cuando está disponible) en
            // el sidebar y en el título de la ventana. Los dos salen del mismo dato a
            // propósito: antes el sidebar tenía el nombre y el título seguía con `| Chat`.
            var propia = lista.FirstOrDefault(u => u.TryGetValue("codigo", out var c) && c is string s && s == _fachada.UsuarioActual);
            if (propia != null)
            {
                string nom = (Valor(propia, "nombres") + " " + Valor(propia, "apellidos")).Trim();
                string codigoCuenta = _fachada.UsuarioActual ?? "";
                string identidad = string.IsNullOrWhiteSpace(nom)
                    ? codigoCuenta
                    : $"{nom} [{codigoCuenta}]";
                txtSesion.Text = $"Sesión activa · {identidad}";
                Title = TextosVentana.TituloChatCon(identidad);
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
            // T1 (2026-10-14): aquí se reabría la última conversación de la cuenta
            // (`RestaurarUltimoChatSiPendiente`). Se quitó a propósito: el chat arranca sin
            // selección. La preferencia se sigue guardando al cambiar de chat.
            AplicarFiltro(seleccionado);
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
            AplicarFiltro(UsuarioDestino);
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
            // N2: el conteo del directorio va al sidebar. En la cabecera vive la presencia del
            // contacto abierto, y este método corre en cada aviso de presencia: escribiendo el
            // conteo ahí, el `en línea`/`desconectado` duraba hasta el siguiente refresco.
            txtEstadoRed.Text = $"{_directorio.Count(u => u.Conectado)} contactos en línea";
            RefrescarPresenciaCabecera();
        }

        /// <summary>
        /// T1 (2026-10-14): deja la ventana sin conversación (Esc).
        /// </summary>
        /// <remarks>
        /// Solo suelta la selección: el historial local y la preferencia del último chat
        /// quedan intactos, y ninguna conversación se reabre sola al arrancar. Se vacían la
        /// lista visible, la cita y los adjuntos, que eran de ese chat.
        /// </remarks>
        private void DeseleccionarConversacion()
        {
            if (UsuarioDestino == null) return;
            // Las cargas en vuelo de la conversación que se abandona ya no pintan aquí.
            _versionCargaHistorial++;
            Interlocked.Increment(ref _versionNavegacionCita);
            lstUsuarios.SelectedItem = null;
            UsuarioDestino = null;
            _mensajes.Clear();
            _nuevosPendientes = 0;
            btnNuevos.Visibility = Visibility.Collapsed;
            _adjuntos.Limpiar();
            ActualizarPanelAdjuntos();
            LimpiarCita();
            txtTitulo.Text = TextosVentana.SeleccionaConversacion;
            txtEstado.Text = "";
            ActualizarVacioChat();
            ActualizarEstadoEnvio();
        }

        /// <summary>
        /// N2: presencia del contacto abierto en la cabecera (`en línea` / `desconectado`).
        /// Sin conversación, la cabecera se queda en blanco y el título ya dice
        /// `Selecciona una conversación`.
        /// </summary>
        private void RefrescarPresenciaCabecera()
        {
            if (txtEstado == null) return;
            txtEstado.Text = UsuarioDestino == null
                ? ""
                : _usuarios.FirstOrDefault(u =>
                    string.Equals(u.Codigo, UsuarioDestino, StringComparison.OrdinalIgnoreCase))
                    ?.Presencia ?? "";
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
            AplicarFiltro(UsuarioDestino);
        }

        private void BtnActualizar_Click(object sender, RoutedEventArgs e) => CargarUsuarios();
        private void LstUsuarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstUsuarios.SelectedItem is not UsuarioVM usuario
                || string.Equals(UsuarioDestino, usuario.Codigo, StringComparison.OrdinalIgnoreCase)) return;
            if (_adjuntos.HayAdjuntos && !Enviando)
            {
                int cantidad = _adjuntos.Adjuntos.Count;
                _adjuntos.Limpiar();
                MostrarAvisoInline($"Se descartaron {cantidad} adjunto(s) al cambiar de conversación.",
                    TipoAvisoInline.Informacion);
            }
            ActualizarPanelAdjuntos();
            Interlocked.Increment(ref _versionNavegacionCita);
            UsuarioDestino = usuario.Codigo;
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
            OffsetActual = 0;
            _mensajes.Clear();
            CargandoHistorial = true;
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
                AplicarFiltro(UsuarioDestino);
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
    }
}
