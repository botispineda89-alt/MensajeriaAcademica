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
        // Scroll y aviso de mensajes nuevos (UIX-16)
        // Solo code-behind: los campos y el constructor viven en ChatWindow.xaml.cs;
        // este parcial agrupa una sola responsabilidad para poder localizarlo.

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
    }
}
