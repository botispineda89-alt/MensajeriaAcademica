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
    }
}
