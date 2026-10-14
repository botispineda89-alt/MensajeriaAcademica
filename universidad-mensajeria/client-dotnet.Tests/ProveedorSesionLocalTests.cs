using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mensajeria.Composicion;
using Mensajeria.Store;
using Xunit;

namespace Mensajeria.Tests
{
    /// <summary>
    /// PAT-01 en .NET: el almacenamiento local se compone fuera de la fachada.
    /// Antes <c>FachadaCliente</c> hacia <c>new LocalStore</c> + <c>new
    /// HistorialLocal</c> + <c>new CacheUsuarios</c> por su cuenta al entrar cada
    /// cuenta. Aquí se comprueba lo que el proveedor tiene que garantizar:
    /// una base por cuenta, idempotencia y cierre del store anterior.
    /// </summary>
    public class ProveedorSesionLocalTests : IDisposable
    {
        private readonly string _dir;

        public ProveedorSesionLocalTests()
        {
            _dir = Path.Combine(Path.GetTempPath(),
                "proveedor-sesion-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch (IOException) { }
        }

        private string DbUrl => $"sqlite:///{Path.Combine(_dir, "mensajeria.db")}";

        [Fact]
        public void CadaCuentaAbreSuPropiaBaseYLaRutaEsLaDePython()
        {
            using var proveedor = new ProveedorSesionLocal();

            SesionLocal a = proveedor.ParaCuenta(DbUrl, "160004726");
            SesionLocal b = proveedor.ParaCuenta(DbUrl, "160004716");

            Assert.NotNull(a.Store);
            Assert.NotNull(b.Store);
            Assert.NotEqual(a.Store!.DbPath, b.Store!.DbPath);

            string esperada = LocalStore.RutaParaCuenta(
                DbUrl, "160004726");
            Assert.Equal(esperada, a.Store.DbPath);
            Assert.NotNull(a.Historial);
            Assert.NotNull(a.Cache);
        }

        [Fact]
        public void PedirLaMismaCuentaDosVecesNoReabreElStore()
        {
            using var proveedor = new ProveedorSesionLocal();

            SesionLocal primera = proveedor.ParaCuenta(DbUrl, "160004726");
            SesionLocal repetida = proveedor.ParaCuenta(DbUrl, "160004726");

            Assert.Same(primera.Store, repetida.Store);
            Assert.Same(primera.Historial, repetida.Historial);
            Assert.Same(primera.Cache, repetida.Cache);
        }

        [Fact]
        public void CambiarDeCuentaSustituyeElStoreYDejaVigenteElNuevo()
        {
            using var proveedor = new ProveedorSesionLocal();

            SesionLocal primera = proveedor.ParaCuenta(DbUrl, "160004726");
            SesionLocal segunda = proveedor.ParaCuenta(DbUrl, "160004716");

            Assert.NotEqual(primera.Store!.DbPath, segunda.Store!.DbPath);
            // Solo queda viva la sesion de la cuenta nueva: si el proveedor
            // guardase las dos, el store anterior seguiria abierto.
            Assert.Same(segunda.Store, proveedor.Actual.Store);
            Assert.Same(segunda.Historial, proveedor.Actual.Historial);
            Assert.Same(segunda.Cache, proveedor.Actual.Cache);
        }

        [Fact]
        public void DisposeEsIdempotente()
        {
            var proveedor = new ProveedorSesionLocal();
            proveedor.ParaCuenta(DbUrl, "160004726");

            proveedor.Dispose();
            // Volver a cerrar no debe fallar: Desconectar y el cierre de la
            // ventana pueden coincidir.
            proveedor.Dispose();

            Assert.Null(proveedor.Actual.Store);
        }

        [Fact]
        public void SinSesionPidiccionElEstadoEsVacio()
        {
            using var proveedor = new ProveedorSesionLocal();

            Assert.Null(proveedor.Actual.Store);
            Assert.Null(proveedor.Actual.Historial);
            Assert.Null(proveedor.Actual.Cache);
            Assert.Empty(Directory.GetFiles(_dir, "*.db"));
        }
    }
}
