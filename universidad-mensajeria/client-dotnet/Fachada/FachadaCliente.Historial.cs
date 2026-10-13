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
        // Lectura del historial local y remoto
        // Solo code-behind: los campos, los eventos y la composicion viven
        // en FachadaCliente.cs; este parcial agrupa una sola responsabilidad.

        public string? ContenidoImagenLocal(string idMensaje) => Historial?.ContenidoPorId(idMensaje);

        /// <summary>Historial en orden cronologico (los mas nuevos abajo, como Python).
        /// Ordena por instante real y no por texto: los formatos mezclados
        /// (UTC con Z vs hora local) rompen el ORDER BY alfabetico.</summary>
        public List<Dictionary<string, object>> CargarHistorial(string otroUsuario, int offset = 0)
        {
            var yo = UsuarioActual;
            if (yo == null) throw new InvalidOperationException("inicia sesion primero");
            var pagina = Historial?.PaginaConversacion(yo, otroUsuario, 50, offset)
                ?? new List<Dictionary<string, object>>();
            pagina.Sort((a, b) => CompararFecha(
                a.TryGetValue("fechaEnvio", out var fa) ? fa?.ToString() : null,
                b.TryGetValue("fechaEnvio", out var fb) ? fb?.ToString() : null));
            return pagina;
        }

        private static int CompararFecha(string? x, string? y) =>
            ParseRobusto(x).CompareTo(ParseRobusto(y));

        private static DateTimeOffset ParseRobusto(string? s) =>
            !string.IsNullOrWhiteSpace(s) && DateTimeOffset.TryParse(s, out var dto)
                ? dto.ToUniversalTime() : DateTimeOffset.MaxValue;

        public async Task<int> TraerHistorialAsync(string otroUsuario, int pagina)
        {
            if (_conexion == null || UsuarioActual == null)
                throw new InvalidOperationException("inicia sesion primero");
            var resp = await _conexion.Pedir(new Dictionary<string, object?>
            {
                { "tipo", TipoMensaje.HISTORIAL_REQ }, { "remitente", UsuarioActual },
                { "destinatario", otroUsuario }, { "pagina", pagina }
            }).ConfigureAwait(false);
            LanzarSiError(resp, "historial rechazado", TipoMensaje.HISTORIAL_PAGE);
            string json = GetString(resp, "contenido") ?? "[]";
            foreach (var mensaje in _mapeadorDatos.MapearPaginaHistorial(json, UsuarioActual!))
            {
                Historial?.UpsertMensaje(mensaje.IdMensaje!, mensaje.Origen!,
                    mensaje.Destino!, mensaje.Tipo!, mensaje.Contenido,
                    mensaje.HashSha256, mensaje.NumCaracteres, mensaje.NumPalabras,
                    mensaje.NombreArchivo, mensaje.RutaArchivo, mensaje.TamanoArchivo,
                    mensaje.FechaEnvio!, mensaje.Enviado ? 1 : 0,
                    mensaje.Descargado ? 1 : 0, mensaje.Estado!, mensaje.ArchivoId,
                    mensaje.EtapasFiltrado);
            }
            return GetInt(resp, "totalPaginas") ?? 0;
        }

        public int ContarConversacion(string otro) =>
            UsuarioActual == null ? 0 : (Historial?.ContarConversacion(UsuarioActual, otro) ?? 0);

        public IReadOnlyDictionary<string, string> UltimosMensajesPorContacto() =>
            UsuarioActual == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : (Historial?.UltimosMensajesPorContacto(UsuarioActual)
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        /// <summary>UIX-24: ultimo mensaje de cada contacto, para la lista lateral.</summary>
        public IReadOnlyDictionary<string, (string Texto, bool Propio)> UltimoMensajePorContacto() =>
            UsuarioActual == null || Historial == null
                ? new Dictionary<string, (string, bool)>(StringComparer.OrdinalIgnoreCase)
                : Historial.UltimoMensajePorContacto(UsuarioActual)
                    .ToDictionary(par => par.Key, par => (par.Value.Texto, par.Value.Propio),
                        StringComparer.OrdinalIgnoreCase);

        public List<Dictionary<string, object>> DirectorioLocal() =>
            Cache?.Listar() ?? new List<Dictionary<string, object>>();

        public List<Dictionary<string, object?>> Pendientes() =>
            Historial?.ListarPendientes() ?? new List<Dictionary<string, object?>>();

        public Task<int> ReintentarPendientesAsync() =>
            _reintentadorPendientes.ReintentarPendientesAsync(_conexion, UsuarioActual);
    }
}
