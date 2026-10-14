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
        // Recepcion de eventos de la fachada
        // Solo code-behind: los campos y el constructor viven en ChatWindow.xaml.cs;
        // este parcial agrupa una sola responsabilidad para poder localizarlo.

        private void Fachada_OnPresencia(Dictionary<string, JsonElement> aviso) =>
            Dispatcher.BeginInvoke(CargarUsuarios);

        private void Fachada_OnMensajeRecibido(Dictionary<string, JsonElement> msg)
        {
            if (!msg.TryGetValue("tipo", out var t) || t.ValueKind != JsonValueKind.String) return;
            string tipo = t.GetString()!;
            if (tipo == TipoMensaje.MENSAJE_ENTREGADO || tipo == TipoMensaje.MENSAJE_LEIDO)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (UsuarioDestino != null) CargarHistorial(UsuarioDestino);
                });
                return;
            }
            // UIX-26: MENSAJE_ARCHIVO también repinta la lista. Estaba fuera de la lista
            // y por eso un archivo que llegaba no ponía ni el preview ni la
            // insignia hasta que se pulsaba "Actualizar".
            if (tipo != TipoMensaje.MENSAJE_TEXTO && tipo != TipoMensaje.MENSAJE_IMAGEN
                && tipo != TipoMensaje.MENSAJE_ARCHIVO
                && tipo != TipoMensaje.BROADCAST && tipo != TipoMensaje.SYNC_LOGIN) return;
            string remitente = GetString(msg, "remitente") ?? "desconocido";
            string destinatario = GetString(msg, "destinatario") ?? "";
            string contacto = tipo == TipoMensaje.BROADCAST
                ? remitente
                : string.Equals(remitente, _fachada.UsuarioActual, StringComparison.OrdinalIgnoreCase)
                    ? destinatario : remitente;
            Dispatcher.BeginInvoke(() =>
            {
                MarcarConversacionReciente(contacto);
                if (UsuarioDestino != contacto) return;
                // UIX-16: solo baja solo si ya se estaba abajo; si no, avisa.
                bool avisar = !EstaAbajo();
                CargarHistorial(contacto);
                if (avisar) AvisarNuevo();
                if (!string.Equals(remitente, _fachada.UsuarioActual, StringComparison.OrdinalIgnoreCase)
                    && UsuarioDestino == remitente)
                    _ = Task.Run(() => _fachada.MarcarLeidosAsync(remitente));
            });
        }
    }
}
