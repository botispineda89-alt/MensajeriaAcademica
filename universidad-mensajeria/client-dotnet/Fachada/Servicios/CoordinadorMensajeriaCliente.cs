using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mensajeria.Componentes;
using Mensajeria.Fachada;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;

namespace Mensajeria.Fachada.Servicios
{
    /// <summary>
    /// Coordina el envío 1-a-1: persiste primero, intenta la red y deja una
    /// pendiente solo ante fallos de transporte.
    /// </summary>
    public sealed class CoordinadorMensajeriaCliente
    {
        public const int ParteArchivoBytes = 1024 * 1024;

        private readonly Func<IHistorialLocal?> _historialActual;

        public CoordinadorMensajeriaCliente(Func<IHistorialLocal?> historialActual)
        {
            _historialActual = historialActual ?? throw new ArgumentNullException(nameof(historialActual));
        }

        public async Task<bool> EnviarTextoAsync(IConexionRed conexion, string remitente,
            string destinatario, string contenido)
        {
            var historial = _historialActual();
            var id = Guid.NewGuid().ToString();
            var fecha = DateTime.UtcNow.ToString("O");
            historial?.UpsertMensaje(id, remitente, destinatario,
                TipoMensaje.MENSAJE_TEXTO, contenido, null, null, null,
                null, null, null, fecha, 1, 1, "PENDIENTE");
            var mensaje = new Dictionary<string, object?>
            {
                { "id", id },
                { "tipo", TipoMensaje.MENSAJE_TEXTO },
                { "remitente", remitente },
                { "destinatario", destinatario },
                { "contenido", contenido },
                { "fechaHora", fecha }
            };
            try
            {
                var respuesta = await conexion.Pedir(mensaje).ConfigureAwait(false);
                var aceptada = ExigirAceptado(respuesta, "envio rechazado", TipoMensaje.ACK);
                historial?.UpsertMensaje(id, remitente, destinatario,
                    TipoMensaje.MENSAJE_TEXTO, contenido,
                    aceptada.HashSha256, aceptada.NumCaracteres, aceptada.NumPalabras,
                    null, null, null, fecha, 1, 1, "ENVIADO");
                return true;
            }
            catch (Exception error) when (EsFalloTransporte(error))
            {
                historial?.RegistrarPendiente(id, TipoMensaje.MENSAJE_TEXTO, remitente,
                    destinatario, contenido, null, null, fecha);
                return false;
            }
        }

        public async Task<bool> EnviarImagenAsync(IConexionRed conexion, string remitente,
            string destinatario, string imagePath, string? pie = null)
        {
            long longitud = new FileInfo(imagePath).Length;
            if (longitud > CatalogoAdjuntos.MaxBytesImagenTramaUnica)
                throw new ArgumentException("la imagen supera el límite de envío directo de 12 MB");
            byte[] bytes = await File.ReadAllBytesAsync(imagePath).ConfigureAwait(false);
            if (bytes.Length == 0) throw new ArgumentException("la imagen esta vacia");
            var historial = _historialActual();
            var id = Guid.NewGuid().ToString();
            var fecha = DateTime.UtcNow.ToString("O");
            var nombre = Path.GetFileName(imagePath);
            // El texto que accompanies a la imagen viaja como pie/caption en el
            // `contenido` del MISMO MENSAJE_IMAGEN (misma burbuja), no como un
            // segundo mensaje. El servidor lo persiste tal cual y lo reenvia.
            var caption = string.IsNullOrWhiteSpace(pie) ? null : pie!.Trim();
            historial?.UpsertMensaje(id, remitente, destinatario,
                TipoMensaje.MENSAJE_IMAGEN, null, null, null, null,
                nombre, imagePath, bytes.Length, fecha, 1, 1, "PENDIENTE",
                null, null, caption);
            var mensaje = new Dictionary<string, object?>
            {
                { "id", id },
                { "tipo", TipoMensaje.MENSAJE_IMAGEN },
                { "remitente", remitente },
                { "destinatario", destinatario },
                { "contenido", caption },
                { "contenidoImagen", Convert.ToBase64String(bytes) },
                { "nombreArchivo", nombre },
                { "tamanoArchivo", bytes.Length },
                { "fechaHora", fecha }
            };
            try
            {
                var respuesta = await conexion.Pedir(mensaje).ConfigureAwait(false);
                var aceptada = ExigirAceptado(respuesta, "envio rechazado",
                    TipoMensaje.IMAGE_FILTERED_RESULT);
                historial?.UpsertMensaje(id, remitente, destinatario,
                    TipoMensaje.MENSAJE_IMAGEN, null, aceptada.HashSha256,
                    null, null, nombre, imagePath, bytes.Length, fecha, 1, 1,
                    "ENVIADO", aceptada.ArchivoId, aceptada.EtapasFiltrado, caption);
                return true;
            }
            catch (Exception error) when (EsFalloTransporte(error))
            {
                // El pendiente lleva el pie en `contenido` y los bytes en payload.
                historial?.RegistrarPendiente(id, TipoMensaje.MENSAJE_IMAGEN, remitente,
                    destinatario, caption, nombre, bytes, fecha);
                return false;
            }
            catch
            {
                historial?.MarcarEstado(id, "ERROR");
                throw;
            }
        }

        public async Task<bool> EnviarArchivoAsync(IConexionRed conexion, string remitente,
            string destinatario, string rutaArchivo, IProgress<double>? progreso = null,
            CancellationToken cancelacion = default, string? pie = null)
        {
            if (string.IsNullOrEmpty(rutaArchivo) || !File.Exists(rutaArchivo))
                throw new ArgumentException("archivo no encontrado");
            var info = new FileInfo(rutaArchivo);
            long tamano = info.Length;
            if (tamano == 0) throw new ArgumentException("el archivo esta vacio");
            if (tamano > IFachadaCliente.MaxBytesArchivo) throw new ArgumentException(
                $"el archivo supera el maximo de {IFachadaCliente.MaxBytesArchivo} bytes");
            await using var contenido = new FileStream(rutaArchivo, FileMode.Open, FileAccess.Read,
                FileShare.Read, ParteArchivoBytes, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var historial = _historialActual();
            var id = Guid.NewGuid().ToString();
            var fecha = DateTime.UtcNow.ToString("O");
            var nombre = Path.GetFileName(rutaArchivo);
            // Igual que en imagen: el texto que acompaña al archivo es su pie y
            // viaja en la misma burbuja, no en un segundo mensaje.
            var caption = string.IsNullOrWhiteSpace(pie) ? null : pie!.Trim();
            historial?.UpsertMensaje(id, remitente, destinatario,
                TipoMensaje.MENSAJE_ARCHIVO, null, null, null, null,
                nombre, rutaArchivo, tamano, fecha, 1, 1, "PENDIENTE",
                null, null, caption);
            try
            {
                await EnviarFragmentosConStreamAsync(conexion, remitente, destinatario, nombre,
                    CatalogoAdjuntos.Mime(rutaArchivo), contenido, id, fecha, tamano,
                    progreso, cancelacion, caption).ConfigureAwait(false);
                historial?.UpsertMensaje(id, remitente, destinatario,
                    TipoMensaje.MENSAJE_ARCHIVO, null, null, null, null,
                    nombre, rutaArchivo, tamano, fecha, 1, 1, "ENVIADO",
                    null, null, caption);
                return true;
            }
            catch (OperationCanceledException)
            {
                // La transferencia incompleta no se reintenta automáticamente.
                historial?.MarcarEstado(id, "ERROR");
                throw;
            }
            catch (Exception error) when (EsFalloTransporte(error))
            {
                try
                {
                    byte[] payload = await File.ReadAllBytesAsync(rutaArchivo).ConfigureAwait(false);
                    historial?.RegistrarPendiente(id, TipoMensaje.MENSAJE_ARCHIVO, remitente,
                        destinatario, caption, nombre, payload, fecha);
                }
                catch
                {
                    historial?.MarcarEstado(id, "ERROR");
                    throw;
                }
                return false;
            }
            catch
            {
                historial?.MarcarEstado(id, "ERROR");
                throw;
            }
        }

        public Task<bool> EnviarArchivoAsync(IConexionRed conexion, string remitente,
            string destinatario, AdjuntoPendiente adjunto, IProgress<double>? progreso,
            CancellationToken cancelacion) =>
            EnviarArchivoAsync(conexion, remitente, destinatario, adjunto.Ruta,
                progreso, cancelacion);

        /// <summary>Reutiliza el mismo protocolo para reenviar archivos pendientes.</summary>
        internal Task EnviarFragmentosAsync(IConexionRed conexion, string remitente,
            string destinatario, string nombre, string mime, byte[] bytes, string id,
            string fecha, IProgress<double>? progreso, CancellationToken cancelacion,
            string? pie = null) =>
            EnviarFragmentosPrivadoAsync(conexion, remitente, destinatario, nombre,
                mime, bytes, id, fecha, progreso, cancelacion, pie);

        internal static string MimePorExtension(string ruta) =>
            CatalogoAdjuntos.Mime(ruta);

        internal static async Task EnviarFragmentosConStreamAsync(IConexionRed conexion,
            string remitente, string destinatario, string nombre, string mime, Stream contenido,
            string id, string fecha, long tamano, IProgress<double>? progreso,
            CancellationToken cancelacion, string? pie = null)
        {
            int totalPartes = (int)((tamano + ParteArchivoBytes - 1) / ParteArchivoBytes);
            TimeSpan esperaArchivo = TimeSpan.FromSeconds(45);
            var inicio = await conexion.Pedir(new Dictionary<string, object?>
            {
                { "id", id },
                { "tipo", TipoMensaje.ARCHIVO_INICIO },
                { "remitente", remitente },
                { "destinatario", destinatario },
                { "contenido", pie },
                { "nombreArchivo", nombre },
                { "mime", mime },
                { "tamanoArchivo", tamano },
                { "totalPartes", totalPartes },
                { "fechaHora", fecha }
            }, esperaArchivo).ConfigureAwait(false);
            ExigirAceptado(inicio, "envio rechazado", TipoMensaje.ACK);

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[ParteArchivoBytes];
            for (int i = 0; i < totalPartes; i++)
            {
                cancelacion.ThrowIfCancellationRequested();
                int requeridos = (int)Math.Min(ParteArchivoBytes, tamano - (long)i * ParteArchivoBytes);
                int leidos = 0;
                while (leidos < requeridos)
                {
                    int n = await contenido.ReadAsync(buffer.AsMemory(leidos, requeridos - leidos),
                        cancelacion).ConfigureAwait(false);
                    if (n == 0) throw new IOException("El archivo cambió durante el envío.");
                    leidos += n;
                }

                hash.AppendData(buffer, 0, requeridos);
                string base64 = Convert.ToBase64String(buffer, 0, requeridos);
                var parte = await conexion.Pedir(new Dictionary<string, object?>
                {
                    { "id", id },
                    { "tipo", TipoMensaje.ARCHIVO_PARTE },
                    { "remitente", remitente },
                    { "archivoId", id },
                    { "indiceParte", i },
                    { "contenidoImagen", base64 },
                    { "fechaHora", fecha }
                }, esperaArchivo).ConfigureAwait(false);
                ExigirAceptado(parte, "envio rechazado", TipoMensaje.ACK);
                progreso?.Report((double)(i + 1) / totalPartes);
            }

            string digest = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            var fin = await conexion.Pedir(new Dictionary<string, object?>
            {
                { "id", id },
                { "tipo", TipoMensaje.ARCHIVO_FIN },
                { "remitente", remitente },
                { "archivoId", id },
                { "hashSha256", digest },
                { "fechaHora", fecha }
            }, esperaArchivo).ConfigureAwait(false);
            ExigirAceptado(fin, "envio rechazado", TipoMensaje.ACK);
        }

        private static async Task EnviarFragmentosPrivadoAsync(IConexionRed conexion,
            string remitente, string destinatario, string nombre, string mime,
            byte[] bytes, string id, string fecha, IProgress<double>? progreso,
            CancellationToken cancelacion, string? pie = null)
        {
            int totalPartes = (bytes.Length + ParteArchivoBytes - 1) / ParteArchivoBytes;
            TimeSpan esperaArchivo = TimeSpan.FromSeconds(45);
            var inicio = await conexion.Pedir(new Dictionary<string, object?>
            {
                { "id", id },
                { "tipo", TipoMensaje.ARCHIVO_INICIO },
                { "remitente", remitente },
                { "destinatario", destinatario },
                // Pie/caption del archivo: viaja en el `contenido` del INICIO
                // (los servidores que no lo conocen lo ignoran).
                { "contenido", pie },
                { "nombreArchivo", nombre },
                { "mime", mime },
                { "tamanoArchivo", (long)bytes.Length },
                { "totalPartes", totalPartes },
                { "fechaHora", fecha }
            }, esperaArchivo).ConfigureAwait(false);
            ExigirAceptado(inicio, "envio rechazado", TipoMensaje.ACK);
            for (int i = 0; i < totalPartes; i++)
            {
                cancelacion.ThrowIfCancellationRequested();
                int desde = i * ParteArchivoBytes;
                int hasta = Math.Min(desde + ParteArchivoBytes, bytes.Length);
                string base64 = Convert.ToBase64String(bytes, desde, hasta - desde);
                var parte = await conexion.Pedir(new Dictionary<string, object?>
                {
                    { "id", id },
                    { "tipo", TipoMensaje.ARCHIVO_PARTE },
                    { "remitente", remitente },
                    { "archivoId", id },
                    { "indiceParte", i },
                    { "contenidoImagen", base64 },
                    { "fechaHora", fecha }
                }, esperaArchivo).ConfigureAwait(false);
                ExigirAceptado(parte, "envio rechazado", TipoMensaje.ACK);
                progreso?.Report((double)(i + 1) / totalPartes);
            }

            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var fin = await conexion.Pedir(new Dictionary<string, object?>
            {
                { "id", id },
                { "tipo", TipoMensaje.ARCHIVO_FIN },
                { "remitente", remitente },
                { "archivoId", id },
                { "hashSha256", hash },
                { "fechaHora", fecha }
            }, esperaArchivo).ConfigureAwait(false);
            ExigirAceptado(fin, "envio rechazado", TipoMensaje.ACK);
        }

        private static RespuestaRed ExigirAceptado(
            Dictionary<string, JsonElement> respuesta, string defecto, string tipoEsperado)
        {
            respuesta.TryGetValue("tipo", out var tipoElemento);
            string tipo = tipoElemento.ValueKind == JsonValueKind.String
                ? tipoElemento.GetString()! : "";
            bool exito = !respuesta.TryGetValue("exito", out var exitoElemento)
                || exitoElemento.ValueKind != JsonValueKind.False;
            if (tipo != tipoEsperado || !exito)
            {
                string motivo = respuesta.TryGetValue("mensajeError", out var errorElemento)
                    && errorElemento.ValueKind == JsonValueKind.String
                    ? errorElemento.GetString()! : defecto;
                throw new InvalidOperationException(motivo);
            }
            return MapeadorRespuestaRed.Mapear(respuesta);
        }

        private static bool EsFalloTransporte(Exception error) =>
            error is TimeoutException || error is SocketException || error is IOException;
    }
}
