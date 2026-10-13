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
    /// <summary>
    /// Unico punto de entrada de la UI (espejo de FachadaCliente Java, Fase 5):
    /// inyecta interfaces, nunca implementaciones. Enviado con red → pedir() +
    /// ACK con metadatos; sin red → historial + pendiente con reintento.
    /// </summary>
    public partial class FachadaCliente : IFachadaCliente, IReceptorMensajes
    {
        public const string DestinoBroadcast = "*";

        private IConexionRed? _conexion;
        private IAutenticacion? _autenticacion;
        private readonly IHistorialLocal? _historial;
        private readonly ICacheUsuarios? _cache;
        private readonly IMapeadorDatos _mapeadorDatos;
        private readonly CoordinadorMensajeriaCliente _coordinadorMensajeria;
        private readonly EnvioMultipleAdjuntos _envioMultipleAdjuntos;
        private readonly ReintentadorPendientesCliente _reintentadorPendientes;
        private readonly GestorReconexionSesion _gestorReconexion;

        private string _host = "localhost";
        private int _puerto = 5000;
        private bool _tlsHabilitado;
        private int _tlsPuerto = 5001;
        private string _tlsConfianza = "";
        private string _dbUrl = "sqlite:///~/mensajeria_cliente.db";
        private LocalStore? _store;
        private readonly bool _dependenciasInyectadas;

        public event Action<Dictionary<string, JsonElement>>? OnMensajeRecibido;
        public event Action<Dictionary<string, JsonElement>>? OnCierre;
        public event Action<Dictionary<string, JsonElement>>? OnPresencia;
        public event Action? OnDesconectado;
        public event Action<string>? OnSesionRestaurada;
        public string? UsuarioActual => _autenticacion?.CodigoActual;

        /// <summary>Produccion: lee client.properties (mismo formato, §5.2).</summary>
        public FachadaCliente()
        {
            _dependenciasInyectadas = false;
            _mapeadorDatos = new MapeadorDatosServidor();
            _coordinadorMensajeria = new CoordinadorMensajeriaCliente(() => Historial);
            _envioMultipleAdjuntos = new EnvioMultipleAdjuntos(
                () => _coordinadorMensajeria, () => UsuarioActual
                    ?? throw new InvalidOperationException("inicia sesion primero"));
            _reintentadorPendientes = new ReintentadorPendientesCliente(
                () => Historial, _coordinadorMensajeria);
            _gestorReconexion = CrearGestorReconexion();
            CargarPropiedades();
        }

        /// <summary>Producción: el composition root inyecta el mapeador del borde JSON.</summary>
        public FachadaCliente(IMapeadorDatos mapeadorDatos)
        {
            _dependenciasInyectadas = false;
            _mapeadorDatos = mapeadorDatos ?? throw new ArgumentNullException(nameof(mapeadorDatos));
            _coordinadorMensajeria = new CoordinadorMensajeriaCliente(() => Historial);
            _envioMultipleAdjuntos = new EnvioMultipleAdjuntos(
                () => _coordinadorMensajeria, () => UsuarioActual
                    ?? throw new InvalidOperationException("inicia sesion primero"));
            _reintentadorPendientes = new ReintentadorPendientesCliente(
                () => Historial, _coordinadorMensajeria);
            _gestorReconexion = CrearGestorReconexion();
            CargarPropiedades();
        }

        /// <summary>Tests / DI: componentes ya montados.</summary>
        public FachadaCliente(IConexionRed conexion, IAutenticacion autenticacion,
            IHistorialLocal? historial, ICacheUsuarios? cache,
            IMapeadorDatos? mapeadorDatos = null)
        {
            _dependenciasInyectadas = true;
            _conexion = conexion;
            _autenticacion = autenticacion;
            _historial = historial;
            _cache = cache;
            _mapeadorDatos = mapeadorDatos ?? new MapeadorDatosServidor();
            _coordinadorMensajeria = new CoordinadorMensajeriaCliente(() => Historial);
            _envioMultipleAdjuntos = new EnvioMultipleAdjuntos(
                () => _coordinadorMensajeria, () => UsuarioActual
                    ?? throw new InvalidOperationException("inicia sesion primero"));
            _reintentadorPendientes = new ReintentadorPendientesCliente(
                () => Historial, _coordinadorMensajeria);
            _gestorReconexion = CrearGestorReconexion();
            SuscribirRed();
        }

        /// <summary>
        /// El gestor solo existe en la fachada: decide cuándo reconectar y con
        /// qué credencial, sin que la vista tenga que enterarse.
        /// </summary>


        /// <summary>Reensambla PARTES de una descarga correlacionadas por id.</summary>
        private sealed class ColectorFragmentos
        {
            private readonly object _bloqueo = new();
            private readonly Dictionary<int, byte[]> _partes = new();
            private int _total = -1;
            private string? _hash;
            private string? _idEsperado;
            private readonly TaskCompletionSource<byte[]> _listo = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public ColectorFragmentos(string? idEsperado) { _idEsperado = idEsperado; }

            public void FijarId(string id)
            {
                lock (_bloqueo) { _idEsperado ??= id; }
            }

            public void AlPush(Dictionary<string, JsonElement> msg)
            {
                lock (_bloqueo)
                {
                    if (_idEsperado != null && GetString(msg, "id") != _idEsperado) return;
                }
                string? tipo = GetString(msg, "tipo");
                try
                {
                    if (tipo == TipoMensaje.ARCHIVO_INICIO)
                    {
                        lock (_bloqueo)
                        {
                            if (msg.TryGetValue("totalPartes", out var tp)
                                && tp.ValueKind == JsonValueKind.Number
                                && tp.TryGetInt32(out int n)) _total = n;
                        }
                    }
                    else if (tipo == TipoMensaje.ARCHIVO_PARTE)
                    {
                        if (!msg.TryGetValue("indiceParte", out var ip)
                            || ip.ValueKind != JsonValueKind.Number
                            || !ip.TryGetInt32(out int indice)) return;
                        string? b64 = GetString(msg, "contenidoImagen");
                        if (b64 == null) return;
                        byte[] bytes = Convert.FromBase64String(b64);
                        bool completa = false;
                        lock (_bloqueo)
                        {
                            _partes[indice] = bytes;
                            completa = _total > 0 && _partes.Count == _total && _hash != null;
                        }
                        if (completa) Completar();
                    }
                    else if (tipo == TipoMensaje.ARCHIVO_FIN)
                    {
                        bool completa = false;
                        lock (_bloqueo)
                        {
                            _hash = GetString(msg, "hashSha256");
                            completa = _total > 0 && _partes.Count == _total && _hash != null;
                        }
                        if (completa) Completar();
                    }
                }
                catch { }
            }

            private void Completar()
            {
                try
                {
                    List<int> orden;
                    lock (_bloqueo) { orden = new List<int>(_partes.Keys); }
                    orden.Sort();
                    using var ms = new MemoryStream();
                    foreach (int i in orden) ms.Write(_partes[i], 0, _partes[i].Length);
                    byte[] todo = ms.ToArray();
                    string hash;
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                        hash = Convert.ToHexString(sha.ComputeHash(todo)).ToLowerInvariant();
                    lock (_bloqueo)
                    {
                        if (_hash != null && !hash.Equals(_hash, StringComparison.OrdinalIgnoreCase))
                        {
                            _listo.TrySetException(new InvalidOperationException(
                                "sha256 de la descarga no coincide"));
                            return;
                        }
                    }
                    _listo.TrySetResult(todo);
                }
                catch (Exception e)
                {
                    _listo.TrySetException(e);
                }
            }

            public async Task<byte[]> EsperarAsync(TimeSpan espera)
            {
                using var cts = new CancellationTokenSource(espera);
                using (cts.Token.Register(() => _listo.TrySetException(
                    new TimeoutException("descarga fragmentada incompleta"))))
                {
                    return await _listo.Task.ConfigureAwait(false);
                }
            }
        }


    }
}
