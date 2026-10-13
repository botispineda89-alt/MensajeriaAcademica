using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mensajeria.Componentes;
using Mensajeria.Store;
using Mensajeria.Fachada.Servicios;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;

namespace Mensajeria.Fachada
{
    public partial class FachadaCliente
    {
        // Traduccion de tramas de red a eventos de la fachada
        // Solo code-behind: los campos, los eventos y la composicion viven
        // en FachadaCliente.cs; este parcial agrupa una sola responsabilidad.

        private void AlRecibir(Dictionary<string, JsonElement> msg)
        {
            if (!msg.TryGetValue("tipo", out var t) || t.ValueKind != JsonValueKind.String) return;
            string tipo = t.GetString()!;
            if (tipo == TipoMensaje.MENSAJE_ENTREGADO || tipo == TipoMensaje.MENSAJE_LEIDO)
            {
                string? idMensaje = GetString(msg, "contenido");
                if (idMensaje != null)
                    Historial?.MarcarEstado(idMensaje,
                        tipo == TipoMensaje.MENSAJE_LEIDO ? "LEIDO" : "ENTREGADO");
                OnMensajeRecibido?.Invoke(msg);
                return;
            }
            if (tipo == TipoMensaje.PRESENCIA)
            {
                var conectados = new List<string>();
                if (msg.TryGetValue("usuariosConectados", out var lista)
                    && lista.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in lista.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.String && item.GetString() is string codigo)
                            conectados.Add(codigo);
                }
                Cache?.MarcarConectados(conectados);
                if (msg.TryGetValue("codigo", out var codigoMsg)
                    && codigoMsg.ValueKind == JsonValueKind.String)
                    AsegurarEnCache(codigoMsg.GetString()!);
                OnPresencia?.Invoke(msg);
                return;
            }
            if (tipo == TipoMensaje.ARCHIVO_INICIO || tipo == TipoMensaje.ARCHIVO_PARTE)
                return; // Partes de descarga: las consume el colector de DescargarArchivoAsync.
            if (tipo == TipoMensaje.ARCHIVO_FIN)
            {
                OnMensajeRecibido?.Invoke(msg);
                return;
            }
            if (tipo != TipoMensaje.MENSAJE_TEXTO && tipo != TipoMensaje.MENSAJE_IMAGEN
                && tipo != TipoMensaje.MENSAJE_ARCHIVO
                && tipo != TipoMensaje.BROADCAST && tipo != TipoMensaje.SYNC_LOGIN) return;
            try
            {
                // Lo entrante se guarda en (remitente -> destinatario real). El
                // broadcast aparece en la conversacion con su remitente (leccion
                // Fase 5).
                string remitente = GetString(msg, "remitente") ?? "desconocido";
                // Eco multi-PC: lo que envie desde el otro cliente del mismo
                // codigo vuelve con remitente == yo. Si se guardara como yo->yo
                // caeria en una conversacion que no existe y el mensaje NUNCA se
                // veria en el chat. Java ya lo resolvia (ReceptorEntrante).
                bool esEco = UsuarioActual != null
                    && string.Equals(remitente, UsuarioActual, StringComparison.Ordinal);
                string? destinatarioTrama = GetString(msg, "destinatario");
                string destino = esEco
                    ? (destinatarioTrama ?? remitente)
                    : (UsuarioActual ?? destinatarioTrama ?? remitente);
                string id = GetString(msg, "id") ?? Guid.NewGuid().ToString();
                // UIX-26: el servidor manda hora local SIN zona y este cliente
                // guarda UTC CON "Z". Al ordenarse por texto, lo que llegaba
                // quedaba siempre por detrás de lo que salía, y el preview de
                // la lista marcaba "Tú:" aunque el último mensaje fuera del otro.
                string fecha = Transversal.FormatoFecha.AUtc(
                    GetString(msg, "fechaHora")) is { Length: > 0 } f
                        ? f : DateTime.UtcNow.ToString("O");
                bool esImagen = tipo == TipoMensaje.MENSAJE_IMAGEN;
                bool esArchivo = tipo == TipoMensaje.MENSAJE_ARCHIVO;
                // En un adjunto el `contenido` recibido es el pie de la burbuja y
                // los bytes viajan aparte (contenidoImagen en vivo, archivoId
                // para la descarga diferida). Se guarda igual que en Java.
                bool esAdjunto = esImagen || esArchivo;
                string? cuerpo = GetString(msg, "contenido");
                string? pie = esAdjunto && !string.IsNullOrWhiteSpace(cuerpo) ? cuerpo : null;
                string? texto = esAdjunto ? null
                    : cuerpo;
                // SYNC_LOGIN envuelve el mensaje real y lo declara en `tipoOriginal`:
                // sin normalizarlo queda guardado con tipo SYNC_LOGIN.
                string tipoGuardado = tipo;
                if (tipo == TipoMensaje.SYNC_LOGIN)
                {
                    try
                    {
                        tipoGuardado = (_mapeadorDatos ?? new MapeadorDatosServidor())
                            .NormalizarTipoMensaje(GetString(msg, "tipoOriginal") ?? "");
                    }
                    catch (ArgumentException) { tipoGuardado = tipo; }
                }
                Historial?.UpsertMensaje(id, remitente, destino, tipoGuardado,
                    texto,
                    GetString(msg, "hashSha256"), GetInt(msg, "numCaracteres"),
                    GetInt(msg, "numPalabras"), GetString(msg, "nombreArchivo"),
                    null, GetLong(msg, "tamanoArchivo"), fecha, esEco ? 1 : 0,
                    tipoGuardado == TipoMensaje.MENSAJE_TEXTO ? 1 : 0,
                    "ENTREGADO", GetString(msg, "archivoId"),
                    GetString(msg, "etapasFiltrado"), pie);
                AsegurarEnCache(remitente);
            }
            catch { }
            OnMensajeRecibido?.Invoke(msg);
        }
    }
}
