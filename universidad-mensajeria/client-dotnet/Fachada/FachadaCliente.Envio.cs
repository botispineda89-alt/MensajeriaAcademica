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
        // Envio de mensajes y adjuntos
        // Solo code-behind: los campos, los eventos y la composicion viven
        // en FachadaCliente.cs; este parcial agrupa una sola responsabilidad.

        public async Task ExpulsarOtrasSesionesAsync()
        {
            if (_conexion == null || UsuarioActual == null)
                throw new InvalidOperationException("inicia sesion primero");
            var resp = await _conexion.Pedir(new Dictionary<string, object?>
            {
                { "tipo", TipoMensaje.KICK }, { "codigo", UsuarioActual }
            }).ConfigureAwait(false);
            LanzarSiError(resp, "no se pudieron cerrar las otras sesiones", TipoMensaje.ACK);
        }

        /// <summary>True si salio por red (ACK); False si quedo en pendientes.
        /// Persiste PENDIENTE antes de la red con el ID estable, para que un acuse
        /// rápido (ENTREGADO/LEIDO) que llegue durante la espera correlacione por
        /// el mismo ID y no se pierda: el Upsert posterior nunca retrocede estado.
        /// </summary>
        public async Task<bool> EnviarTextoAsync(string destinatario, string contenido)
        {
            ExigirSesion(destinatario, contenido);
            RechazarDestinoPropio(destinatario);
            var (conexion, yo) = SesionEnvio();
            return await _coordinadorMensajeria.EnviarTextoAsync(
                conexion, yo, destinatario, contenido).ConfigureAwait(false);
        }

        public async Task<bool> EnviarImagenAsync(string destinatario, string imagePath,
            string? pie = null)
        {
            if (string.IsNullOrEmpty(destinatario)) throw new ArgumentException("elige un destinatario");
            var (conexion, yo) = SesionEnvio();
            RechazarDestinoPropio(destinatario);
            return await _coordinadorMensajeria.EnviarImagenAsync(
                conexion, yo, destinatario, imagePath, pie).ConfigureAwait(false);
        }

        public const long MaxBytesArchivo = IFachadaCliente.MaxBytesArchivo;
        public const int ParteArchivoBytes = CoordinadorMensajeriaCliente.ParteArchivoBytes;

        /// <summary>
        /// Envía un archivo genérico fragmentado (tope propio 50 MB, PROTOCOLO.md §3/§5):
        /// INICIO + PARTES base64 ≤1 MiB secuenciales + FIN con sha256. Persiste
        /// PENDIENTE con el ID estable antes de la red y reutiliza el mismo ID
        /// en reintentos (el servidor reinicia el buffer ante el mismo id).
        /// </summary>
        public async Task<bool> EnviarArchivoAsync(string destinatario, string rutaArchivo,
            IProgress<double>? progreso = null, CancellationToken cancelacion = default,
            string? pie = null)
        {
            if (string.IsNullOrEmpty(destinatario)) throw new ArgumentException("elige un destinatario");
            var (conexion, yo) = SesionEnvio();
            RechazarDestinoPropio(destinatario);
            return await _coordinadorMensajeria.EnviarArchivoAsync(
                conexion, yo, destinatario, rutaArchivo, progreso, cancelacion, pie)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Envía un lote de adjuntos. <paramref name="pie"/> es el texto que el
        /// usuario escribió: si todo lo adjunto cabe en UNA sola burbuja (un
        /// adjunto suelto, o varias imágenes montadas) el texto va como pie de
        /// esa burbuja; si hay varias burbujas, el texto se manda antes por
        /// separado. Mismas reglas que el cliente JavaFX.
        /// </summary>
        public async Task<ResultadoEnvioMultiple> EnviarAdjuntosAsync(string destinatario,
            IReadOnlyList<AdjuntoPendiente> adjuntos, IProgress<ProgresoEnvio>? progreso = null,
            CancellationToken cancelacion = default, string? pie = null)
        {
            if (string.IsNullOrWhiteSpace(destinatario))
                throw new ArgumentException("elige un destinatario");
            RechazarDestinoPropio(destinatario);
            var (conexion, _) = SesionEnvio();
            return await _envioMultipleAdjuntos.EnviarAsync(conexion, destinatario,
                adjuntos, progreso, cancelacion, pie).ConfigureAwait(false);
        }

        /// <summary>
        /// DESCARGAR_ARCHIVO bajo demanda: respuesta unica para archivos chicos y
        /// secuencia INICIO+PARTES+FIN reensamblada para archivos grandes (&gt;1 MiB).
        /// </summary>
        public async Task<string> DescargarArchivoAsync(string idMensaje, string archivoId, string rutaDestino)
        {
            if (_conexion == null) throw new InvalidOperationException("ConectarAsync primero");
            var colector = new ColectorFragmentos(null);
            void AlPush(Dictionary<string, JsonElement> msg) => colector.AlPush(msg);
            _conexion.OnMensajeRecibido += AlPush;
            try
            {
                var resp = await _conexion.Pedir(new Dictionary<string, object?>
                {
                    { "tipo", TipoMensaje.DESCARGAR_ARCHIVO },
                    { "archivoId", archivoId },
                    { "fechaHora", DateTime.UtcNow.ToString("O") }
                });
                if ((GetString(resp, "tipo") == TipoMensaje.ARCHIVO_INICIO))
                {
                    string? idT = GetString(resp, "id");
                    if (idT == null) throw new InvalidOperationException("descarga sin id");
                    colector.FijarId(idT);
                    colector.AlPush(resp);
                    byte[] bytes = await colector.EsperarAsync(TimeSpan.FromMinutes(5))
                        .ConfigureAwait(false);
                    await File.WriteAllBytesAsync(rutaDestino, bytes).ConfigureAwait(false);
                    Historial?.MarcarDescargado(idMensaje, rutaDestino);
                    return rutaDestino;
                }
                LanzarSiError(resp, "descarga rechazada", TipoMensaje.DESCARGAR_ARCHIVO_RESPUESTA);
                string b64 = GetString(resp, "contenidoImagen") ?? "";
                await File.WriteAllBytesAsync(rutaDestino, Convert.FromBase64String(b64))
                    .ConfigureAwait(false);
                Historial?.MarcarDescargado(idMensaje, rutaDestino);
                return rutaDestino;
            }
            finally
            {
                _conexion.OnMensajeRecibido -= AlPush;
            }
        }
        private void ExigirSesion(string destinatario, string contenido)
        {
            if (_conexion == null || UsuarioActual == null)
                throw new InvalidOperationException("inicia sesion primero");
            if (string.IsNullOrWhiteSpace(destinatario))
                throw new ArgumentException("elige un destinatario");
            if (string.IsNullOrWhiteSpace(contenido))
                throw new ArgumentException("el contenido no puede estar vacio");
        }

        private void RechazarDestinoPropio(string destinatario)
        {
            if (UsuarioActual != null && destinatario == UsuarioActual)
                throw new ArgumentException("elige un destinatario distinto a tu cuenta");
        }

        /// <summary>Foto de la sesion para operaciones largas: si el servidor
        /// cierra la sesion a mitad del envio se sigue con estos valores en vez
        /// de NullReferenceException.</summary>
        private (IConexionRed Conexion, string Usuario) SesionEnvio()
        {
            var conexion = _conexion;
            var usuario = UsuarioActual;
            if (conexion == null || usuario == null)
                throw new InvalidOperationException("sesión cerrada, vuelve a iniciar sesión");
            return (conexion, usuario);
        }

        private void AsegurarEnCache(string codigo)
        {
            if (Cache?.PorCodigo(codigo) == null)
            {
                string ahora = DateTime.UtcNow.ToString("O");
                Cache?.Guardar(codigo, codigo, "", null, 0, ahora, ahora);
            }
        }
    }
}
