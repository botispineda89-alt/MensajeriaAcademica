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
        // Compositor: respuesta citada, teclado y envio del lote
        // Solo code-behind: los campos y el constructor viven en ChatWindow.xaml.cs;
        // este parcial agrupa una sola responsabilidad para poder localizarlo.

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
    }
}
