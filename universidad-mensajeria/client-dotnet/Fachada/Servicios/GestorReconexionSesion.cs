using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Mensajeria.Transversal;

namespace Mensajeria.Fachada.Servicios
{
    /// <summary>
    /// Custodia temporal de las credenciales y restauración automática de la
    /// sesión (espejo de <c>GestorReconexionSesion.java</c>, Fase 11).
    ///
    /// POR QUÉ existe en .NET: el cliente WPF no tenía nada equivalente. Una
    /// sola caída de transporte (corte de red, NAT que tira el socket o el
    /// cierre por inactividad del servidor, <c>red.sesion.timeout-segundos</c>)
    /// dejaba la sesión muerta para siempre: el banner ámbar se encendía y el
    /// usuario tenía que cerrar y volver a abrir el programa, perdiendo el
    /// hilo. Java y Python reconectan solos; .NET no. Esta clase es el hueco de
    /// paridad que se estaba pagando en producción.
    ///
    /// Reglas de custodia (idénticas a Java):
    /// <list type="bullet">
    ///   <item>La contraseña solo vive aquí, mientras la sesión está abierta.</item>
    ///   <item>Un CLOSE_NOTICE (sesión cerrada) o un cierre voluntario la borran:
    ///         eso no se reconecta solo, se vuelve a entrar.</item>
    ///   <item>Una caída de transporte sí se reintenta con backoff acotado.</item>
    /// </list>
    /// </summary>
    public sealed class GestorReconexionSesion : IDisposable
    {
        /// <summary>Backoff exponencial acotado, el mismo que el cliente Java.</summary>
        private static readonly int[] EsperaPorIntento = { 1, 2, 4, 8, 16, 30 };

        private readonly Func<string, string, Task<bool>> _reconectar;
        private readonly Action<EstadoConexion> _publicarEstado;
        private readonly Action<string> _alRestaurarSesion;
        private readonly Action _sesionNoRecuperable;
        private readonly object _cerrojo = new();

        private Timer? _reintento;
        private long _generacionSesion;
        private string? _codigoSesion;
        private string? _claveSesion;
        private bool _cierreIntencional = true;
        private bool _detenido;
        private int _intentos;
        private int _enCurso;

        public GestorReconexionSesion(
            Func<string, string, Task<bool>> reconectar,
            Action<EstadoConexion> publicarEstado,
            Action<string> alRestaurarSesion,
            Action sesionNoRecuperable)
        {
            _reconectar = reconectar ?? throw new ArgumentNullException(nameof(reconectar));
            _publicarEstado = publicarEstado ?? throw new ArgumentNullException(nameof(publicarEstado));
            _alRestaurarSesion = alRestaurarSesion
                ?? throw new ArgumentNullException(nameof(alRestaurarSesion));
            _sesionNoRecuperable = sesionNoRecuperable
                ?? throw new ArgumentNullException(nameof(sesionNoRecuperable));
        }

        /// <summary>Sesión abierta: la credencial queda en custodia para reconectar.</summary>
        public void GuardarSesion(string codigo, string contrasena)
        {
            lock (_cerrojo)
            {
                _generacionSesion++;
                _codigoSesion = codigo;
                _claveSesion = contrasena;
                _cierreIntencional = false;
                _intentos = 0;
                CancelarReintento();
            }
        }

        /// <summary>Cierre del servidor o salida del usuario: no hay nada que reintentar.</summary>
        public void OlvidarSesion()
        {
            lock (_cerrojo)
            {
                _cierreIntencional = true;
                _generacionSesion++;
                _codigoSesion = null;
                _claveSesion = null;
                _intentos = 0;
                CancelarReintento();
            }
        }

        /// <summary>
        /// Programa el siguiente intento si hay sesión abierta y no hay uno en
        /// vuelo. Es idempotente: varias caídas seguidas no acumulan intentos.
        /// </summary>
        public void ProgramarReconexion()
        {
            int demora;
            lock (_cerrojo)
            {
                if (_detenido || _cierreIntencional
                    || _codigoSesion == null || _claveSesion == null
                    || _reintento != null) return;
                // Con un intento en marcha, programar otro solaparía dos
                // reconexiones sobre el mismo socket. El reintento del catch
                // libera el flag antes de volver a llamar aquí.
                if (Volatile.Read(ref _enCurso) != 0) return;
                demora = EsperaSegundos(_intentos++);
                _reintento = new Timer(_ => IntentarReconexion(), null,
                    TimeSpan.FromSeconds(demora), Timeout.InfiniteTimeSpan);
            }
            Trace.TraceInformation("Reintento de conexión programado en {0} s", demora);
        }

        /// <summary>Sesión cerrada a propósito: olvida la credencial y para el ciclo.</summary>
        public void Cerrar() => OlvidarSesion();

        public void Dispose()
        {
            lock (_cerrojo)
            {
                _detenido = true;
                _cierreIntencional = true;
                _codigoSesion = null;
                _claveSesion = null;
                CancelarReintento();
            }
        }

        /// <summary>
        /// Segundos de espera del intento <paramref name="intento"/>, saturados
        /// al último valor. Puro y estático para poderlo verificar sin sockets.
        /// </summary>
        public static int EsperaSegundos(int intento) =>
            EsperaPorIntento[Math.Clamp(intento, 0, EsperaPorIntento.Length - 1)];

        private async void IntentarReconexion()
        {
            string codigo;
            string clave;
            long generacion;
            lock (_cerrojo)
            {
                _reintento?.Dispose();
                _reintento = null;
                if (_detenido || _cierreIntencional
                    || _codigoSesion == null || _claveSesion == null) return;
                if (Interlocked.CompareExchange(ref _enCurso, 1, 0) != 0) return;
                codigo = _codigoSesion;
                clave = _claveSesion!;
                generacion = _generacionSesion;
            }
            try
            {
                if (!await _reconectar(codigo, clave).ConfigureAwait(false))
                {
                    // El servidor rechazó la reautenticación: hay que entrar a mano.
                    lock (_cerrojo) { _generacionSesion++; }
                    _sesionNoRecuperable();
                    _publicarEstado(EstadoConexion.SesionCerrada);
                    return;
                }
                if (Cancelada(generacion)) return;
                lock (_cerrojo) { _intentos = 0; }
                _publicarEstado(EstadoConexion.Conectado);
                _alRestaurarSesion(codigo);
                Trace.TraceInformation("Conexión y sesión restauradas automáticamente.");
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Falló el intento de reconexión automática: {0}", ex.Message);
                if (!Cancelada(generacion)) _publicarEstado(EstadoConexion.SinRed);
                // Se libera el flag antes de reprogramar: ProgramarReconexion
                // ignora las llamadas mientras hay un intento en marcha.
                Interlocked.Exchange(ref _enCurso, 0);
                ProgramarReconexion();
                return;
            }
            finally
            {
                Interlocked.Exchange(ref _enCurso, 0);
            }
        }

        /// <summary>La generación cambió: el intento actual ya no vale.</summary>
        private bool Cancelada(long generacion)
        {
            lock (_cerrojo)
            {
                return _detenido || _cierreIntencional || _generacionSesion != generacion;
            }
        }

        private void CancelarReintento()
        {
            _reintento?.Dispose();
            _reintento = null;
        }
    }
}