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
        // Tema y fondo del chat
        // Solo code-behind: los campos y el constructor viven en ChatWindow.xaml.cs;
        // este parcial agrupa una sola responsabilidad para poder localizarlo.

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
    }
}
