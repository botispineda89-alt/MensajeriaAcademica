using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mensajeria.Componentes;
using Mensajeria.Composicion;
using Mensajeria.Store;
using Mensajeria.Fachada.Servicios;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;

namespace Mensajeria.Fachada
{
    public partial class FachadaCliente
    {
        // Conexion, reconexion y estado
        // Solo code-behind: los campos, los eventos y la composicion viven
        // en FachadaCliente.cs; este parcial agrupa una sola responsabilidad.

        private GestorReconexionSesion CrearGestorReconexion() =>
            new GestorReconexionSesion(
                ReconectarYReautenticarAsync,
                CambiarEstado,
                codigo => OnSesionRestaurada?.Invoke(codigo),
                DetenerFlushPendientes);

        private void CargarPropiedades()
        {
            _host = Transversal.ClienteConfig.Valor("host", Transversal.ClienteConfig.HostPorDefecto);
            _puerto = Transversal.ClienteConfig.Entero("puerto",
                    Transversal.ClienteConfig.PuertoPorDefecto, 1, 65535);
            _tlsHabilitado = ValidarTls.HabilitadoDesdeTexto(
                Transversal.ClienteConfig.Valor("tls.habilitado", "false"));
            _tlsPuerto = Transversal.ClienteConfig.Entero("tls.puerto",
                    Transversal.ClienteConfig.TlsPuertoPorDefecto, 1, 65535);
            // Vacío = validación de cadena del sistema (lo correcto por defecto).
            _tlsConfianza = Transversal.ClienteConfig.Valor("tls.confianza", "");
            _dbUrl = Transversal.ClienteConfig.Valor("db.url", Transversal.ClienteConfig.DbUrlPorDefecto);
        }

        public Task ConectarAsync() => ConectarAsync(_host, _puerto);

        public async Task ConectarAsync(string host, int puerto)
        {
            CambiarEstado(EstadoConexion.Conectando);
            try
            {
                await ConectarInternoAsync(host, puerto);
                CambiarEstado(EstadoConexion.Conectado);
            }
            catch
            {
                CambiarEstado(EstadoConexion.SinRed);
                throw;
            }
        }

        private async Task ConectarInternoAsync(string host, int puerto)
        {
            if (_dependenciasInyectadas)
            {
                await _conexion!.ConectarAsync(host, puerto);
                return;
            }
            var conexion = new ConexionRed
            {
                TlsHabilitado = _tlsHabilitado,
                TlsPuerto = _tlsPuerto,
                TlsConfianza = _tlsConfianza,
                // SEG-01: si hay PEM propio se valida contra él (nombre + cadena);
                // si no, CrearValidador devuelve null y manda la validación del sistema.
                ValidarCertificado = _tlsHabilitado
                    ? ValidarTls.CrearValidador(_tlsConfianza, host)
                    : null
            };
            await conexion.ConectarAsync(host, puerto);
            // Con la reconexión automática el socket anterior queda muerto: sin cerrarlo
            // se acumularía un hilo lector por cada intento.
            _conexion?.Desconectar();
            _conexion = conexion;
            string dbPath = _dbUrl.StartsWith("sqlite:///") ? _dbUrl["sqlite:///".Length..] : _dbUrl;
            // PAT-01: también la sesión inicial se compone fuera de la fachada.
            Adoptar(SesionLocalInicial(dbPath));
            _autenticacion = new Autenticacion(conexion);
            SuscribirRed();
        }

        /// <summary>
        /// Resuelve SQLite por cuenta y conmuta historial/caché. La composición
        /// (qué store y qué adaptadores) vive en
        /// <see cref="ProveedorSesionLocal"/>; aquí solo se le pide la sesión del
        /// código que acaba de entrar. Es idempotente por ruta: volver a pedir la
        /// misma cuenta no reabre nada.
        /// </summary>
        private void CambiarAStorePorCuenta(string codigo)
        {
            if (_dependenciasInyectadas) return;
            try
            {
                Adoptar(Proveedor().ParaCuenta(_dbUrl, codigo));
            }
            catch { }
        }

        private ProveedorSesionLocal Proveedor() =>
            _sesionLocal ?? throw new InvalidOperationException(
                "sin ProveedorSesionLocal: construye la fachada con FabricaCliente");

        private SesionLocal SesionLocalInicial(string dbPath) =>
            Proveedor().Inicial(_dbUrl, dbPath);

        /// <summary>
        /// Publica en la fachada la sesión que compuso el proveedor. Los campos
        /// <c>_historial</c>/<c>_cache</c> son de solo lectura (los del ctor con
        /// dependencias inyectadas); estos son los de producción.
        /// </summary>
        private void Adoptar(SesionLocal sesion)
        {
            _store = sesion.Store;
            _historialProd = sesion.Historial as HistorialLocal;
            _cacheProd = sesion.Cache as CacheUsuarios;
        }

        /// <summary>
        /// Rehace socket y sesión tras una caída (lo llama el gestor de
        /// reconexión, nunca la UI). Devuelve false si el servidor ya no acepta
        /// esas credenciales: en ese caso hay que volver a entrar a mano.
        /// </summary>
        private async Task<bool> ReconectarYReautenticarAsync(string codigo, string contrasena)
        {
            Interlocked.Exchange(ref _reconectando, 1);
            try
            {
                await ConectarInternoAsync(_host, _puerto).ConfigureAwait(false);
                var autenticacion = _autenticacion
                    ?? throw new InvalidOperationException("no hay autenticacion disponible");
                if (!await autenticacion.LoginAsync(codigo, contrasena).ConfigureAwait(false))
                {
                    _conexion?.Desconectar();
                    return false;
                }
                CambiarAStorePorCuenta(codigo);
                await ReintentarPendientesAsync().ConfigureAwait(false);
                IniciarFlushPendientes(IntervaloFlushSegundos);
                return true;
            }
            finally
            {
                Interlocked.Exchange(ref _reconectando, 0);
            }
        }

        // Backing para la ruta de produccion (la de tests viene por ctor).
        private HistorialLocal? _historialProd;
        private CacheUsuarios? _cacheProd;
        private IHistorialLocal? Historial => _historial ?? _historialProd;
        private ICacheUsuarios? Cache => _cache ?? _cacheProd;

        private void SuscribirRed()
        {
            if (_conexion == null) return;
            _conexion.Suscribir(this);
        }
        public void AlRecibir(RespuestaRed mensaje) =>
            AlRecibir(MapeadorRespuestaRed.ACrudo(mensaje));

        public void AlCierre(RespuestaRed aviso)
        {
            // PAT-03: CLOSE_NOTICE cierra la sesión, sin convertirlo en una
            // caída recuperable de transporte. Por eso además se olvida la
            // credencial en custodia: un KICK o un cierre del servidor no se
            // reintenta solo, se vuelve a entrar (espejo de Java).
            _gestorReconexion.OlvidarSesion();
            DetenerFlushPendientes();
            CambiarEstado(EstadoConexion.SesionCerrada);
            OnCierre?.Invoke(MapeadorRespuestaRed.ACrudo(aviso));
        }

        public void AlError(string motivo) =>
            Trace.TraceError("Error al recibir mensaje de red: {0}", motivo);

        public void AlDesconexion()
        {
            OnDesconectado?.Invoke();
            // UIX-20: al reconectar se cierra el socket anterior para no dejar
            // un hilo lector colgado, y ese cierre vuelve a avisar por aquí.
            // Tomarlo en serio programaría un segundo intento que tumbaría el
            // socket que acabamos de levantar.
            if (Volatile.Read(ref _reconectando) != 0) return;
            // Un cierre voluntario no degrada una sesión que ya fue cerrada.
            if (EstadoConexion == EstadoConexion.SesionCerrada) return;
            CambiarEstado(EstadoConexion.SinRed);
            // ESTO es lo que faltaba: sin programar el reintento, la primera
            // caída dejaba la sesión muerta y el usuario tenía que reiniciar
            // el programa a mano (el banner decía "se reintentan solos" y no
            // se reintentaba nada).
            _gestorReconexion.ProgramarReconexion();
        }

        // ------------------------------------------------------- PAT-03: estado

        /// <summary>1 mientras el gestor está rehaciendo socket y sesión.</summary>
        private int _reconectando;

        // El estado lo escriben tres hilos distintos (UI, lector TCP y el
        // temporizador de reconexión): se serializa para que una caída no
        // pise al "Conectado" de un login que terminó en ese instante.
        private readonly object _cerrojoEstado = new();
        private EstadoConexion _estado = EstadoConexion.SinRed;

        /// <summary>Estado actual de la conexión (antes nadie lo consultaba).</summary>
        public EstadoConexion EstadoConexion
        {
            get { lock (_cerrojoEstado) return _estado; }
        }

        /// <summary>La vista se suscribe aquí para pintar el banner (UIX-00a).</summary>
        public event Action<EstadoConexion>? OnEstadoConexion;

        private void CambiarEstado(EstadoConexion nuevo)
        {
            lock (_cerrojoEstado)
            {
                if (nuevo == _estado) return;
                _estado = nuevo;
            }
            OnEstadoConexion?.Invoke(nuevo);
        }
    }
}
