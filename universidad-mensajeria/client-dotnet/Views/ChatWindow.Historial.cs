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
        // Paginacion del historial, anclaje de scroll y aviso de nuevos
        // Solo code-behind: los campos y el constructor viven en ChatWindow.xaml.cs;
        // este parcial agrupa una sola responsabilidad para poder localizarlo.

        /// <summary>
        /// Envoltura de seguridad. Esta es una continuation async void: lo que
        /// revienta dentro sube al Dispatcher y CIERRA la aplicacion. Un fallo
        /// al pintar una fila no puede costar la sesion del usuario.
        /// </summary>
        private async void CargarHistorial(string codigo, int offset = 0, bool prepend = false,
            Action? alTerminar = null)
        {
            try
            {
                await CargarHistorialInterno(codigo, offset, prepend, alTerminar);
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceError(
                    "No se pudo pintar el historial de {0}: {1}", codigo, error);
                if (UsuarioDestino == codigo)
                    barraProgreso.Visibility = Visibility.Collapsed;
                MostrarAvisoInline("No se pudo mostrar esta conversacion. Intenta actualizar.",
                    TipoAvisoInline.Error);
            }
        }

        private async Task CargarHistorialInterno(string codigo, int offset, bool prepend,
            Action? alTerminar)
        {
            int version = Interlocked.Increment(ref _versionCargaHistorial);
            List<Dictionary<string, object>> historial;
            int totalLocal;
            // UIX-14: la rueda gira mientras se trae historial.
            barraProgreso.Visibility = Visibility.Visible;
            // UIX-16: se captura ANTES de la carga, porque al terminar la lista cambió.
            if (!prepend) _seguirAbajoAntesDeCargar = EstaAbajo();
            var anclaje = prepend ? CapturarAnclajeScroll() : null;
            try
            {
                (historial, totalLocal) = await Task.Run(() =>
                    (_fachada.CargarHistorial(codigo, offset), _fachada.ContarConversacion(codigo)));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("No se pudo cargar el historial de {0}: {1}", codigo, ex);
                if (UsuarioDestino == codigo && version == _versionCargaHistorial)
                {
                    barraProgreso.Visibility = Visibility.Collapsed;
                    CargandoHistorial = false;
                    _errorCargaHistorial = true;
                    ActualizarVacioChat();
                    MostrarAvisoInline("No se pudo cargar el historial de esta conversación. Intenta actualizar los contactos.",
                        TipoAvisoInline.Error);
                }
                return;
            }
            if (UsuarioDestino != codigo || version != _versionCargaHistorial) return;
            barraProgreso.Visibility = Visibility.Collapsed;
            CargandoHistorial = false;
            _errorCargaHistorial = false;
            if (!prepend) OffsetActual = offset;
            if (!prepend) _mensajes.Clear();
            // Fachada ordena por instante real de más antiguo a más nuevo.
            // La colección conserva ese orden para que lo reciente quede abajo.
            var cronologicas = historial;
            string? fechaPrev = null;
            string? autorPrev = null;
            MensajeVM? vecino = null;
            if (prepend)
            {
                vecino = _mensajes.FirstOrDefault(m => !m.EsCabeceraDia);
            }
            else
            {
                var ultimo = _mensajes.LastOrDefault(m => !m.EsCabeceraDia);
                if (ultimo != null) { fechaPrev = ultimo.FechaCruda; autorPrev = ultimo.AutorCrudo; }
            }
            var lote = new List<MensajeVM>();
            foreach (var row in cronologicas)
            {
                string tipo = Valor(row, "tipo");
                string remitente = Valor(row, "origen");
                // UIX-28: se normaliza el tipo antes de decidir. En la base hay
                // filas viejas con "IMAGEN"/"ARCHIVO" sin el prefijo MENSAJE_, y
                // con comparación en crudo quedaban sin botón de guardar y sin
                // recogniseerse como imagen.
                bool esImagen = Transversal.BotonGuardar.EsImagen(tipo);
                bool esArchivo = Transversal.BotonGuardar.EsArchivo(tipo);
                bool propio = remitente == _fachada.UsuarioActual;
                string identidad = propio ? "Tú" : Identidad(remitente);
                string estadoBruto = Valor(row, "estado");
                string fechaCruda = Valor(row, "fechaEnvio");
                if (Transversal.FilasChat.CambiaElDia(fechaPrev, fechaCruda))
                {
                    lote.Add(new MensajeVM
                    {
                        EsCabeceraDia = true,
                        TextoCabecera = Transversal.FormatoFecha.EtiquetaDia(fechaCruda)
                    });
                }
                bool agrupado = Transversal.FilasChat.Agrupar(
                    fechaPrev, autorPrev, fechaCruda, remitente);
                string caption = (esImagen || esArchivo) ? Valor(row, "caption") : "";
                Transversal.FormatoCita.Separar(caption, out var pieCita, out var pieCuerpo);
                lote.Add(new MensajeVM
                {
                    Id = Valor(row, "id"),
                    Encabezado = agrupado ? "" : identidad,
                    Autor = identidad,
                    AutorCrudo = remitente,
                    Fecha = FormatearFecha(fechaCruda),
                    FechaCruda = fechaCruda,
                    Contenido = esImagen ? "" : Valor(row, "contenido"),
                    Estado = propio ? EstadoVisible(estadoBruto) : "",
                    EsImagen = esImagen,
                    EsArchivo = esArchivo,
                    EtapasFiltrado = Valor(row, "etapasFiltrado"),
                    EsPropio = propio,
                    EsLeido = estadoBruto == "LEIDO",
                    ArchivoId = Valor(row, "archivoId"),
                    NombreArchivo = Valor(row, "nombreArchivo"),
                    RutaArchivo = Valor(row, "rutaArchivo"),
                    Pie = pieCuerpo,
                    PieCita = pieCita
                });
                SepararCita(lote[^1], esImagen ? "" : Valor(row, "contenido"));
                fechaPrev = fechaCruda;
                autorPrev = remitente;
            }
            if (prepend)
            {
                for (int i = 0; i < lote.Count; i++) _mensajes.Insert(i, lote[i]);
                // Unión con el vecino de abajo: separador si cambia el día y
                // encabezado recalculado (ya no va pegado al anterior).
                if (vecino != null && lote.Count > 0)
                {
                    var ultimoLote = lote[^1];
                    if (!ultimoLote.EsCabeceraDia
                        && Transversal.FilasChat.CambiaElDia(ultimoLote.FechaCruda, vecino.FechaCruda))
                    {
                        _mensajes.Insert(lote.Count, new MensajeVM
                        {
                            EsCabeceraDia = true,
                            TextoCabecera = Transversal.FormatoFecha.EtiquetaDia(vecino.FechaCruda)
                        });
                    }
                    if (!ultimoLote.EsCabeceraDia)
                    {
                        vecino.Encabezado = Transversal.FilasChat.Agrupar(
                            ultimoLote.FechaCruda, ultimoLote.AutorCrudo,
                            vecino.FechaCruda, vecino.AutorCrudo) ? "" : vecino.Autor;
                    }
                }
            }
            else
            {
                foreach (var vm in lote) _mensajes.Add(vm);
            }
            if (prepend)
            {
                // Si la carga se inició para completar una búsqueda, el salto al
                // resultado tiene prioridad sobre conservar el viewport anterior.
                if (_saltoPendiente == null)
                    RestaurarAnclajeScroll(anclaje, codigo, version);
                else
                    lstMensajes.ScrollIntoView(_mensajes.FirstOrDefault());
            }
            else if (_seguirAbajoAntesDeCargar)
            {
                lstMensajes.ScrollIntoView(_mensajes.LastOrDefault());
                _nuevosPendientes = 0;
                btnNuevos.Visibility = Visibility.Collapsed;
            }
            // UIX-13: si no hay mensajes, se dice en vez de dejar negro.
            ActualizarVacioChat();
            // UIX-06: si hay un salto pendiente (búsqueda), se reintenta ahora
            // que hay más mensajes cargados.
            if (_saltoPendiente != null)
            {
                string id = _saltoPendiente;
                int intentos = _intentosSalto;
                _saltoPendiente = null;
                SaltarA(id, intentos);
            }
            int paginasRemotas = _totalPaginasRemotas.GetValueOrDefault(codigo);
            btnAnteriores.Visibility = offset + 50 < totalLocal
                || _paginaRemota.GetValueOrDefault(codigo) + 1 < paginasRemotas
                ? Visibility.Visible : Visibility.Collapsed;
            alTerminar?.Invoke();
        }

        /// <summary>
        /// UIX-26: un mensaje NO dispara una consulta. El trabajo se agrupa, como
        /// el intervalo mínimo del cliente Java: veinte mensajes seguidos son un
        /// refresco, no veinte.
        /// <para>
        /// Lo importante es que se <b>fusiona</b>, no que se descarta. Antes el
        /// segundo mensaje de una ráfaga se perdía: el primero programaba el
        /// refresco y el segundo, dentro de la ventana, no programaba nada. Como
        /// la consulta del primero se resuelve en segundo plano, el preview del
        /// segundo ya no estaba en la base y la lista se quedaba desfasada
        /// hasta el siguiente mensaje o hasta que se pulsaba "Actualizar".
        /// </para>
        /// </summary>
        private async void CargarHistorialRemoto(string codigo, int pagina, bool olderPage = false)
        {
            try
            {
                int total = await _fachada.TraerHistorialAsync(codigo, pagina);
                _paginaRemota[codigo] = pagina;
                _totalPaginasRemotas[codigo] = total;
                if (pagina == 0)
                {
                    await ActualizarFechasRecientesAsync();
                    if (UsuarioDestino != null) AplicarFiltro(UsuarioDestino);
                }
                if (UsuarioDestino == codigo)
                    CargarHistorial(codigo, olderPage ? OffsetActual : 0, olderPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("No se pudo sincronizar el historial de {0}: {1}", codigo, ex);
                if (UsuarioDestino == codigo && pagina == 0)
                {
                    txtEstado.Text = "No se pudo sincronizar; mostrando el historial guardado en este equipo.";
                    MostrarAvisoInline(MensajesUsuario.Accion(ex, "sincronizar el historial"),
                        TipoAvisoInline.Error);
                }
            }
        }

        private void BtnAnteriores_Click(object sender, RoutedEventArgs e)
        {
            if (UsuarioDestino == null) return;
            string codigo = UsuarioDestino;
            int totalLocal = _fachada.ContarConversacion(codigo);
            if (OffsetActual + 50 < totalLocal)
            {
                OffsetActual += 50;
                CargarHistorial(codigo, OffsetActual, true);
                return;
            }
            int siguiente = _paginaRemota.GetValueOrDefault(codigo) + 1;
            if (siguiente < _totalPaginasRemotas.GetValueOrDefault(codigo))
            {
                OffsetActual = totalLocal;
                CargarHistorialRemoto(codigo, siguiente, true);
            }
        }
    }
}
