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

        private void BtnActualizar_Click(object sender, RoutedEventArgs e) => CargarUsuarios();
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
    }
}
