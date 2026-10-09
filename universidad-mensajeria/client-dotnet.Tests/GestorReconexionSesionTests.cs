using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mensajeria.Fachada.Servicios;
using Mensajeria.Transversal;
using Xunit;

namespace Mensajeria.Tests
{
    /// <summary>
    /// UIX-20: la reconexión automática que le faltaba a .NET (el cliente Java
    /// ya la tenía en <c>GestorReconexionSesion</c>). Se verifica sin sockets:
    /// el gestor solo depende de cuatro collaborators inyectados.
    /// </summary>
    public class GestorReconexionSesionTests
    {
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 2)]
        [InlineData(2, 4)]
        [InlineData(3, 8)]
        [InlineData(4, 16)]
        [InlineData(5, 30)]
        [InlineData(6, 30)]
        [InlineData(99, 30)]
        [InlineData(-3, 1)]
        public void ElBackoffEsExponencialYAcotado(int intento, int esperado) =>
            Assert.Equal(esperado, GestorReconexionSesion.EsperaSegundos(intento));

        [Fact]
        public async Task SinSesionNoSeIntentaReconectar()
        {
            var reconexiones = 0;
            using var gestor = CrearGestor((_, _) => { reconexiones++; return Task.FromResult(true); },
                null, null);

            gestor.ProgramarReconexion();
            await Task.Delay(400);

            // Sin GuardarSesion no hay credencial: programar no debe hacer nada.
            Assert.Equal(0, reconexiones);
        }

        [Fact]
        public async Task UnaCaidaRestauraLaSesionYVuelveAEnLinea()
        {
            var estados = new List<EstadoConexion>();
            string? restaurada = null;
            using var gestor = CrearGestor((codigo, clave) =>
                {
                    Assert.Equal("A001", codigo);
                    Assert.Equal("secreto", clave);
                    return Task.FromResult(true);
                },
                estados.Add,
                codigo => restaurada = codigo);
            gestor.GuardarSesion("A001", "secreto");

            gestor.ProgramarReconexion();
            await Esperar(() => restaurada != null);

            Assert.Equal("A001", restaurada);
            Assert.Equal(EstadoConexion.Conectado, Assert.Single(estados));
        }

        [Fact]
        public async Task UnServidorQueRechazaLaClaveDejaDeReintentar()
        {
            var intentos = 0;
            var estados = new List<EstadoConexion>();
            var noRecuperable = 0;
            using var gestor = CrearGestor((_, _) => { intentos++; return Task.FromResult(false); },
                estados.Add, null, () => noRecuperable++);
            gestor.GuardarSesion("A001", "secreto");

            gestor.ProgramarReconexion();
            await Esperar(() => estados.Count > 0);
            // Un par de intentos extra no deben aparecer: la sesión está cerrada.
            await Task.Delay(1500);

            Assert.Equal(1, intentos);
            Assert.Equal(EstadoConexion.SesionCerrada, Assert.Single(estados));
            Assert.Equal(1, noRecuperable);
        }

        [Fact]
        public async Task UnCierreDelServidorNoSeReintentaSolo()
        {
            var intentos = 0;
            using var gestor = CrearGestor((_, _) => { intentos++; return Task.FromResult(true); },
                null, null);
            gestor.GuardarSesion("A001", "secreto");

            // PAT-03: CLOSE_NOTICE es "vuelve a entrar", no "reintenta".
            gestor.OlvidarSesion();
            gestor.ProgramarReconexion();
            await Task.Delay(400);

            Assert.Equal(0, intentos);
        }

        [Fact]
        public async Task UnFalloDeTransporteReintentaConBackoff()
        {
            var intentos = 0;
            var estados = new List<EstadoConexion>();
            using var gestor = CrearGestor((_, _) =>
                {
                    intentos++;
                    throw new System.IO.IOException("corte simulado");
                },
                estados.Add, null);
            gestor.GuardarSesion("A001", "secreto");

            gestor.ProgramarReconexion();
            await Esperar(() => intentos >= 2);

            Assert.True(intentos >= 2, "debería reintentar tras un corte de red");
            Assert.Contains(EstadoConexion.SinRed, estados);
        }

        [Fact]
        public async Task SalirCortaLaReconcilacion()
        {
            var intentos = 0;
            using var gestor = CrearGestor((_, _) => { intentos++; return Task.FromResult(true); },
                null, null);
            gestor.GuardarSesion("A001", "secreto");

            gestor.Cerrar();
            gestor.ProgramarReconexion();
            await Task.Delay(400);

            Assert.Equal(0, intentos);
        }

        private static GestorReconexionSesion CrearGestor(
            Func<string, string, Task<bool>> reconectar,
            Action<EstadoConexion>? publicarEstado,
            Action<string>? alRestaurar,
            Action? noRecuperable = null) =>
            new GestorReconexionSesion(reconectar,
                publicarEstado ?? (_ => { }),
                alRestaurar ?? (_ => { }),
                noRecuperable ?? (() => { }));

        private static async Task Esperar(Func<bool> condicion, int ms = 6000)
        {
            var fin = DateTime.UtcNow.AddMilliseconds(ms);
            while (DateTime.UtcNow < fin && !condicion()) await Task.Delay(50);
        }
    }
}