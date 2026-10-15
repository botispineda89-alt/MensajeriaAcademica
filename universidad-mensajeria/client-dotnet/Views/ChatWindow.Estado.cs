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
        // Estado de conexion y aviso inline (PAT-03/UIX-26)
        // Solo code-behind: los campos y el constructor viven en ChatWindow.xaml.cs;
        // este parcial agrupa una sola responsabilidad para poder localizarlo.

        /// <summary>PAT-03: pinta el estado que publica la fachada.</summary>
        private void Fachada_OnEstadoConexion(EstadoConexion estado)
        {
            // Llega desde el hilo de red: el salto al hilo de UI es obligatorio.
            // UIX-20: BeginInvoke y NO Invoke. Invoke es bloqueante, y este
            // aviso se emite desde el hilo lector de TCP; con Invoke, el lector
            // se quedaba esperando al hilo de UI y el socket dejaba de consumirse
            // justo cuando más falta hacía.
            if (_cerrando) return;
            // Los cambios se publican desde la red y Dispatcher los procesa después. Si
            // entretanto llega Conectado, no repintar aquí un SinRed antiguo y dejar el
            // banner fijo mientras se vacía la cola.
            Dispatcher.BeginInvoke(() => PintarEstadoConexion(_fachada.EstadoConexion));
        }

        /// <summary>UIX-20: la sesión volvió sola tras una caída; hay que repintar.</summary>
        private void Fachada_OnSesionRestaurada(string codigo)
        {
            if (_cerrando) return;
            Dispatcher.BeginInvoke(() =>
            {
                if (_cerrando) return;
                PintarEstadoConexion(_fachada.EstadoConexion);
                LimpiarAvisoInline();
                CargarUsuarios();
                if (UsuarioDestino != null)
                    CargarHistorialRemoto(UsuarioDestino, 0);
            });
        }

        private void PintarEstadoConexion(EstadoConexion estado)
        {
            bool degrading = EstadoConexionTexto.MuestraBanner(estado);
            bannerOffline.Visibility = degrading ? Visibility.Visible : Visibility.Collapsed;
            // N2 (2026-10-14): la degradación se cuenta en el banner y en la línea de estado del
            // sidebar. La cabecera se queda con la presencia del contacto abierto —antes se
            // pisaba con el texto de conexión y había que adivinar cuándo restaurarla—.
            if (txtEstadoRed != null)
            {
                txtEstadoRed.Text = degrading
                    ? EstadoConexionTexto.Texto(estado)
                    : $"{_directorio.Count(u => u.Conectado)} contactos en línea";
            }
            if (degrading) bannerOfflineText.Text = EstadoConexionTexto.Texto(estado);
            RefrescarPresenciaCabecera();
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

            // T2 (2026-10-14): los colores de la severidad salen del contrato de diseño
            // (Themes/Tema*.xaml), no de un switch con literales aquí. El ámbar de
            // Advertencia es el mismo del banner de sin conexión en los 3 clientes.
            string familia = _estadoAvisoInline.Tipo switch
            {
                TipoAvisoInline.Error => "AvisoError",
                TipoAvisoInline.Advertencia => "AvisoAtencion",
                TipoAvisoInline.Exito => "AvisoListo",
                _ => "AvisoInfo"
            };
            bordeAvisoInline.Background = TryFindResource(familia + "BgBrush") as Brush;
            bordeAvisoInline.BorderBrush = TryFindResource(familia + "BorderBrush") as Brush;
            Brush? pincelTexto = TryFindResource(familia + "FgBrush") as Brush;
            txtTituloAvisoInline.Foreground = pincelTexto;
            txtAvisoInline.Foreground = pincelTexto;
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
    }
}
