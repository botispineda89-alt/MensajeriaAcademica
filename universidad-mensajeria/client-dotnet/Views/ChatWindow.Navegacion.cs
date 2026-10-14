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
        // Navegacion a cita y resaltado del mensaje
        // Solo code-behind: los campos y el constructor viven en ChatWindow.xaml.cs;
        // este parcial agrupa una sola responsabilidad para poder localizarlo.

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
            string? codigo = UsuarioDestino;
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
                if (version != _versionNavegacionCita || codigo != UsuarioDestino) return;
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
                    if (version != _versionNavegacionCita || codigo != UsuarioDestino) return;
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
                if (version == _versionNavegacionCita && codigo == UsuarioDestino)
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
                if (version != _versionNavegacionCita || codigo != UsuarioDestino)
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
    }
}
