using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;

namespace Mensajeria.Fachada
{
    public interface IFachadaCliente
    {
        /// <summary>Tamaño máximo de un archivo que se puede enviar.</summary>
        const long MaxBytesArchivo = 52_428_800L;

        event Action<Dictionary<string, JsonElement>>? OnMensajeRecibido;
        event Action<Dictionary<string, JsonElement>>? OnCierre;
        event Action<Dictionary<string, JsonElement>>? OnPresencia;

        /// <summary>PAT-03: cambio de estado de conexión (banner offline, UIX-00a).</summary>
        event Action<EstadoConexion>? OnEstadoConexion;

        /// <summary>
        /// La sesión se restauró sola tras una caída (reconexión + reautenticación
        /// automáticas). Llega desde un hilo de fondo: la vista debe saltar al
        /// dispatcher, igual que con el resto de eventos de la fachada.
        /// </summary>
        event Action<string>? OnSesionRestaurada;

        /// <summary>PAT-03: estado actual, para pintar sin esperar al primer aviso.</summary>
        EstadoConexion EstadoConexion { get; }

        string? UsuarioActual { get; }
        Task ConectarAsync();
        void Desconectar();
        Task<bool> LoginAsync(string codigo, string contrasena);
        Task<List<string>> ListarConectadosAsync();
        Task<List<Dictionary<string, object>>> ListarUsuariosAsync();
        Task MarcarLeidosAsync(string otroUsuario);
        /// <summary>UIX-02: no leídos por contacto para la insignia.</summary>
        IReadOnlyDictionary<string, int> ContarNoLeidos();
        /// <summary>UIX-06: ids con el patrón (de más nuevo a más viejo).</summary>
        List<string> BuscarEnHistorial(string otro, string patron);
        Task ExpulsarOtrasSesionesAsync();
        List<Dictionary<string, object>> CargarHistorial(string otroUsuario, int offset = 0);
        IReadOnlyDictionary<string, string> UltimosMensajesPorContacto();
        int ContarConversacion(string otroUsuario);
        Task<int> TraerHistorialAsync(string otroUsuario, int pagina);
        /// <summary>UIX-24: ultimo mensaje de cada contacto (texto y si es propio).</summary>
        IReadOnlyDictionary<string, (string Texto, bool Propio)> UltimoMensajePorContacto();
        List<Dictionary<string, object>> DirectorioLocal();
        Task<bool> EnviarTextoAsync(string destinatario, string contenido);
        /// <summary>
        /// Envía una imagen. <paramref name="pie"/> es el texto que acompaña al
        /// archivo dentro de la MISMA burbuja (null o vacío = sin pie).
        /// </summary>
        Task<bool> EnviarImagenAsync(string destinatario, string imagePath, string? pie = null);
        Task<bool> EnviarArchivoAsync(string destinatario, string rutaArchivo,
            IProgress<double>? progreso = null, CancellationToken cancelacion = default,
            string? pie = null);
        /// <summary>
        /// Envía una lista de adjuntos secuencialmente y reporta progreso agregado por bytes.
        /// <paramref name="pie"/> es el texto escrito: si los adjuntos caben en una sola
        /// burbuja (uno suelto, o varias imágenes montadas) va pegado a esa burbuja.
        /// </summary>
        Task<ResultadoEnvioMultiple> EnviarAdjuntosAsync(string destinatario,
            IReadOnlyList<AdjuntoPendiente> adjuntos, IProgress<ProgresoEnvio>? progreso = null,
            CancellationToken cancelacion = default, string? pie = null);
        Task<string> DescargarArchivoAsync(string idMensaje, string archivoId, string rutaDestino);
        string? ContenidoImagenLocal(string idMensaje);
    }
}
