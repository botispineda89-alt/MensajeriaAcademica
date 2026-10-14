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
        // Cierre de ventana, vuelta al inicio y salida de sesion
        // Solo code-behind: los campos y el constructor viven en ChatWindow.xaml.cs;
        // este parcial agrupa una sola responsabilidad para poder localizarlo.

        /// <summary>UIX-11: persiste el ancho del sidebar al soltar el divisor.</summary>
        private void Splitter_DragCompleted(object sender,
            System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            Transversal.ClienteConfig.Guardar("sidebar.ancho",
                ((int)colSidebar.ActualWidth).ToString());
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            if (Enviando)
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
            if (Enviando)
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
            if (Enviando)
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
    }
}
