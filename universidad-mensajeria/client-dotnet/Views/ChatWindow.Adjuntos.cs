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
        // Adjuntos: carga de imagenes, bandeja y descarga
        // Solo code-behind: los campos y el constructor viven en ChatWindow.xaml.cs;
        // este parcial agrupa una sola responsabilidad para poder localizarlo.

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
    }
}
