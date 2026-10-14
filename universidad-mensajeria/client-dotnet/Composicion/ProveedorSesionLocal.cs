using System;
using Mensajeria.Componentes;
using Mensajeria.Store;

namespace Mensajeria.Composicion
{
    /// <summary>
    /// La sesion local de una cuenta: que archivo se abre y que adaptadores hay
    /// sobre el. <see cref="Fachada.FachadaCliente"/> la recibe montada en vez de
    /// hacer <c>new LocalStore</c>, <c>new HistorialLocal</c> y
    /// <c>new CacheUsuarios</c> dentro de si misma (PAT-01 en .NET).
    /// </summary>
    public readonly record struct SesionLocal(
        LocalStore? Store, IHistorialLocal? Historial, ICacheUsuarios? Cache)
    {
        public static SesionLocal Vacia =>
            new(null, null, null);
    }

    /// <summary>
    /// Composition root del almacenamiento local. Resolve una base por cuenta
    /// (<c>&lt;base&gt;_&lt;cuenta&gt;.db</c>, la misma convencion que Python),
    /// migra el legado de forma idempotente y conmuta historial y cache.
    /// </summary>
    /// <remarks>
    /// Dos procesos con la misma cuenta comparten archivo (WAL); cuentas distintas
    /// nunca comparten filas. Al cambiar de cuenta se cierra el store anterior:
    /// dejarlo abierto dejaba un pool de SQLite vivo y el archivo bloqueado para
    /// el otro cliente.
    /// </remarks>
    public sealed class ProveedorSesionLocal : IDisposable
    {
        private readonly Func<string, LocalStore> _abrirStore;
        private LocalStore? _store;
        private IHistorialLocal? _historial;
        private ICacheUsuarios? _cache;

        /// <summary>
        /// Sin argumentos a proposito: lo crea el composition root
        /// (<see cref="FabricaCliente"/>) y lo inyecta en la fachada, que solo lo
        /// usa. La ruta de la base la pasa quien la pide, porque se lee de
        /// <c>client.properties</c> y la fachada es quien la carga.
        /// </summary>
        public ProveedorSesionLocal(Func<string, LocalStore>? abrirStore = null)
        {
            _abrirStore = abrirStore ?? (ruta => new LocalStore(ruta));
        }

        public SesionLocal Actual { get; private set; } = SesionLocal.Vacia;

        /// <summary>
        /// Sesión inicial, antes de saber con qué cuenta se entra: abre la base
        /// tal cual (<c>client.properties</c>) para que el login pueda usarla.
        /// En cuanto entra una cuenta, <see cref="ParaCuenta"/> la sustituye.
        /// </summary>
        public SesionLocal Inicial(string dbUrl, string dbPath)
        {
            Cerrar();
            return Abrir(dbPath);
        }

        /// <summary>
        /// Abre (o reutiliza) la sesion de <paramref name="codigo"/>. Es
        /// idempotente por ruta: volver a pedir la misma cuenta no reabre nada.
        /// </summary>
        public SesionLocal ParaCuenta(string dbUrl, string codigo)
        {
            string porCuenta = LocalStore.RutaParaCuenta(dbUrl, codigo);
            if (_store != null
                && string.Equals(_store.DbPath, porCuenta, StringComparison.OrdinalIgnoreCase))
            {
                return Actual;
            }

            LocalStore.MigrarLegadoACuenta(LocalStore.RutaLegada(dbUrl), porCuenta, codigo);
            Cerrar();
            return Abrir(porCuenta);
        }

        private SesionLocal Abrir(string dbPath)
        {
            LocalStore store = _abrirStore(dbPath);
            _store = store;
            _historial = new HistorialLocal(store);
            _cache = new CacheUsuarios(store);
            Actual = new SesionLocal(store, _historial, _cache);
            return Actual;
        }

        private void Cerrar()
        {
            _store?.Dispose();
            _store = null;
            _historial = null;
            _cache = null;
            Actual = SesionLocal.Vacia;
        }

        public void Dispose() => Cerrar();
    }
}