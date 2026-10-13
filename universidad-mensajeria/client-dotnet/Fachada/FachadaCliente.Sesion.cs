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
        // Login, directorio y salida de sesion
        // Solo code-behind: los campos, los eventos y la composicion viven
        // en FachadaCliente.cs; este parcial agrupa una sola responsabilidad.

        public void Desconectar()
        {
            // Primero la custodia: si no, un intento de reconexión ya programado
            // podría resucitar la sesión justo después de cerrar.
            _gestorReconexion.Cerrar();
            DetenerFlushPendientes();
            _autenticacion?.Logout();
            _conexion?.Desconectar();
            CambiarEstado(EstadoConexion.SinRed);
            _store?.Dispose();
            _store = null;
        }

        public async Task<bool> LoginAsync(string codigo, string contrasena)
        {
            if (_autenticacion == null) throw new InvalidOperationException("ConectarAsync primero");
            bool ok = await _autenticacion.LoginAsync(codigo, contrasena);
            if (ok)
            {
                CambiarEstado(EstadoConexion.Conectado);
                // La credencial pasa a custodia del gestor mientras dure la
                // sesión: es lo que permite reconectar sin volver a escribirla.
                _gestorReconexion.GuardarSesion(codigo, contrasena);
                CambiarAStorePorCuenta(codigo);
                AsegurarEnCache(codigo);
                await ReintentarPendientesAsync();
                IniciarFlushPendientes(IntervaloFlushSegundos);
            }
            return ok;
        }

        /// <summary>
        /// FASE 11.1: worker que vacia pendientes en orden cada cierto tiempo
        /// (best-effort). Cubre la reconexion silenciosa sin accion de la UI.
        /// </summary>
        public const int IntervaloFlushSegundos = 30;
        private System.Threading.Timer? _flush;
        private readonly object _flushLock = new();

        public void IniciarFlushPendientes(int segundos)
        {
            lock (_flushLock)
            {
                if (_flush != null) return;
                var periodo = TimeSpan.FromSeconds(Math.Max(1, segundos));
                _flush = new System.Threading.Timer(_ =>
                {
                    try { ReintentarPendientesAsync().GetAwaiter().GetResult(); }
                    catch { }
                }, null, periodo, periodo);
            }
        }

        public void DetenerFlushPendientes()
        {
            lock (_flushLock)
            {
                _flush?.Dispose();
                _flush = null;
            }
        }

        /// <summary>
        /// Resuelve SQLite por cuenta (&lt;base&gt;_&lt;cuenta&gt;.db, misma ruta que Python),
        /// migra el legado de forma idempotente y conmuta el historial/caché.
        /// Dos procesos con la misma cuenta comparten archivo (WAL); cuentas
        /// distintas nunca comparten filas. El legado queda como respaldo.
        /// </summary>
        private void CambiarAStorePorCuenta(string codigo)
        {
            if (_dependenciasInyectadas) return;
            try
            {
                string legada = LocalStore.RutaLegada(_dbUrl);
                string porCuenta = LocalStore.RutaParaCuenta(_dbUrl, codigo);
                if (_store != null && string.Equals(_store.DbPath, porCuenta, StringComparison.OrdinalIgnoreCase))
                    return;
                LocalStore.MigrarLegadoACuenta(legada, porCuenta, codigo);
                LocalStore store = AbrirStore(porCuenta);
                _historialProd = new HistorialLocal(store);
                _cacheProd = new CacheUsuarios(store);
            }
            catch { }
        }

        /// <summary>Conectados del servidor (array de strings, PROTOCOLO.md §3) + cache.</summary>
        public async Task<List<string>> ListarConectadosAsync()
        {
            if (_conexion == null) throw new InvalidOperationException("ConectarAsync primero");
            var msg = new Dictionary<string, object?> { { "tipo", TipoMensaje.LISTAR_CONECTADOS } };
            var resp = await _conexion.Pedir(msg).ConfigureAwait(false);

            var lista = new List<string>();
            if (resp.TryGetValue("usuariosConectados", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        lista.Add(item.GetString()!);
                    else if (item.ValueKind == JsonValueKind.Object
                        && item.TryGetProperty("codigo", out var c)
                        && c.ValueKind == JsonValueKind.String)
                        lista.Add(c.GetString()!); // tolerante, el contrato manda strings
                }
            }
            Cache?.MarcarConectados(lista);
            foreach (var codigo in lista) AsegurarEnCache(codigo);
            return lista;
        }

        public async Task<List<Dictionary<string, object>>> ListarUsuariosAsync()
        {
            if (_conexion == null) throw new InvalidOperationException("ConectarAsync primero");
            var resp = await _conexion.Pedir(new Dictionary<string, object?>
                { { "tipo", TipoMensaje.LISTAR_USUARIOS } }).ConfigureAwait(false);
            // El servidor envia el directorio en `contenido` (JSON) — ListarCaso.java,
            // igual que lee el cliente Java (FachadaCliente.sincronizarDirectorio).
            string contenido = GetString(resp, "contenido") ?? "[]";
            List<UsuarioLocal> filas;
            try { filas = _mapeadorDatos.MapearDirectorio(contenido); }
            catch { return DirectorioLocal(); }
            if (filas.Count == 0) return DirectorioLocal();
            var usuarios = new List<Dictionary<string, object>>();
            foreach (var usuario in filas)
            {
                string codigo = usuario.Codigo!;
                string nombres = usuario.Nombres ?? "";
                string apellidos = usuario.Apellidos ?? "";
                Cache?.Guardar(codigo, nombres, apellidos, usuario.Programa,
                    usuario.Conectado ? 1 : 0, usuario.FechaRegistro,
                    usuario.Actualizado ?? DateTime.UtcNow.ToString("O"));
                usuarios.Add(new Dictionary<string, object>
                {
                    ["codigo"] = codigo, ["nombres"] = nombres,
                    ["apellidos"] = apellidos, ["conectado"] = usuario.Conectado
                });
            }
            Cache?.MarcarConectados(usuarios.Where(u => (bool)u["conectado"])
                .Select(u => (string)u["codigo"]).ToList());
            return usuarios;
        }

public async Task MarcarLeidosAsync(string otroUsuario)
        {
            if (UsuarioActual == null || _conexion == null)
                throw new InvalidOperationException("inicia sesion primero");
            var pendientes = Pendientes();
            foreach (string id in Historial?.IdsNoLeidos(UsuarioActual, otroUsuario) ?? new List<string>())
            {
                string idPendiente = "lectura:" + id;
                if (pendientes.Any(p => (string?)p["id"] == idPendiente)) continue;
                Historial?.RegistrarPendiente(idPendiente, TipoMensaje.MENSAJE_LEIDO,
                    UsuarioActual, otroUsuario, id, null, null, DateTime.UtcNow.ToString("O"));
            }
            Historial?.MarcarLeidos(UsuarioActual, otroUsuario);
            await ReintentarPendientesAsync().ConfigureAwait(false);
        }

        /// <summary>UIX-02: no leídos por contacto para la insignia.</summary>
        public IReadOnlyDictionary<string, int> ContarNoLeidos()
        {
            if (UsuarioActual == null || Historial == null)
                return new Dictionary<string, int>();
            return Historial.ContarNoLeidosPorContacto(UsuarioActual);
        }

        /// <summary>UIX-06: ids con el patrón (de más nuevo a más viejo).</summary>
        public List<string> BuscarEnHistorial(string otro, string patron)
        {
            if (UsuarioActual == null || Historial == null)
                return new List<string>();
            return Historial.BuscarEnConversacion(UsuarioActual, otro, patron, 200);
        }
    }
}
