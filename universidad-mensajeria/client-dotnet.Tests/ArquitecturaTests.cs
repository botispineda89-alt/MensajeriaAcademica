using System;
using System.IO;
using Mensajeria.Componentes;
using Mensajeria.Fachada;
using Mensajeria.Transversal.Dtos;
using Xunit;

namespace Mensajeria.Tests
{
    /// <summary>Reglas estructurales de la Fase 2.5 para el cliente WPF.</summary>
    public class ArquitecturaTests
    {
        [Fact]
        public void PuertosNoDependenDePersistenciaNiDeLaInterfazWpf()
        {
            string[] archivos =
            {
                "Componentes/IAutenticacion.cs",
                "Componentes/IConexionRed.cs",
                "Componentes/IHistorialLocal.cs",
                "Componentes/ICacheUsuarios.cs",
                "Componentes/IMapeadorDatos.cs",
                "Componentes/IReceptorMensajes.cs",
                "Fachada/IFachadaCliente.cs"
            };
            string[] dependenciasProhibidas =
            {
                "Microsoft.Data.Sqlite", "SqliteConnection", "Mensajeria.Store",
                "System.Windows", "TcpClient", "NetworkStream", "TramaCodec",
                "new LocalStore", "new HistorialLocal", "new CacheUsuarios",
                "new ConexionRed", "new Autenticacion"
            };

            foreach (string archivo in archivos)
            {
                string fuente = LeerFuenteCliente(archivo);
                foreach (string dependencia in dependenciasProhibidas)
                    Assert.DoesNotContain(dependencia, fuente, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void PresentacionNoAbreSocketNiAccedeALaBaseLocal()
        {
            // Todos los .cs de Views, no solo los *.xaml.cs: el code-behind se
            // repartió en parciales (ChatWindow.Estado.cs, ChatWindow.Envio.cs…)
            // y las reglas tienen que mirar los parciales también.
            string carpeta = Path.Combine(RutaCliente(), "Views");
            string[] archivos = Directory.GetFiles(carpeta, "*.cs");
            Assert.NotEmpty(archivos);
            string[] dependenciasProhibidas =
            {
                "Microsoft.Data.Sqlite", "SqliteConnection", "Mensajeria.Store",
                "LocalStore", "HistorialLocal", "CacheUsuarios", "TcpClient",
                "NetworkStream", "System.Net.Sockets", "TramaCodec"
            };

            foreach (string archivo in archivos)
            {
                string fuente = File.ReadAllText(archivo);
                foreach (string dependencia in dependenciasProhibidas)
                    Assert.DoesNotContain(dependencia, fuente, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void FachadaAceptaDtoClienteEnLaFronteraTipadaDeRed()
        {
            Type fachada = typeof(FachadaCliente);
            Assert.Contains(typeof(IReceptorMensajes), fachada.GetInterfaces());
            Assert.NotNull(fachada.GetMethod(nameof(IReceptorMensajes.AlRecibir),
                new[] { typeof(RespuestaRed) }));
            Assert.NotNull(fachada.GetMethod(nameof(IReceptorMensajes.AlCierre),
                new[] { typeof(RespuestaRed) }));
            Assert.Equal("Mensajeria.Transversal.Dtos",
                typeof(RespuestaRed).Namespace);

            string fuente = LeerFuenteCliente("Fachada/FachadaCliente.cs");
            Assert.DoesNotContain("Mensajeria.CommonProtocol", fuente, StringComparison.Ordinal);
            Assert.DoesNotContain("universidad.mensajeria.common", fuente, StringComparison.Ordinal);
        }

        [Fact]
        public void VistasUsanElPuertoYNoLaFachadaConcreta()
        {
            string chat = LeerFuenteCliente("Views/ChatWindow.xaml.cs");
            string login = LeerFuenteCliente("Views/LoginWindow.xaml.cs");

            Assert.Contains("private readonly IFachadaCliente _fachada", chat, StringComparison.Ordinal);
            Assert.Contains("ChatWindow(IFachadaCliente fachada", chat, StringComparison.Ordinal);
            Assert.Contains("private readonly IFachadaCliente _fachada", login, StringComparison.Ordinal);
            Assert.Contains("LoginWindow(IFachadaCliente fachada", login, StringComparison.Ordinal);

            string[] archivos = Directory.GetFiles(
                Path.Combine(RutaCliente(), "Views"), "*.cs");
            foreach (string archivo in archivos)
            {
                string fuente = File.ReadAllText(archivo);
                Assert.DoesNotMatch(@"\bnew\s+FachadaCliente\s*\(", fuente);
                Assert.DoesNotMatch(@"\b(?:private|protected|public|internal)\s+(?:readonly\s+)?FachadaCliente\b", fuente);
                Assert.DoesNotMatch(@"\bFachadaCliente\.", fuente);
            }
        }

        [Fact]
        public void NingunArchivoDeLaVistaSupera400Lineas()
        {
            // Fase 2.5, regla 6 (equivalente a la de Python y el objetivo de
            // Java): el code-behind del chat se repartió en parciales por
            // responsabilidad, así que ningún archivo tiene que explicarse solo.
            // FachadaCliente.cs sigue fuera de este regla: su descomposición es
            // lo pendiente de la Fase 2 en .NET (ESTADO_ROADMAP.md).
            string carpeta = Path.Combine(RutaCliente(), "Views");
            string[] archivos = Directory.GetFiles(carpeta, "*.cs");
            Assert.NotEmpty(archivos);

            foreach (string archivo in archivos)
            {
                int lineas = File.ReadAllLines(archivo).Length;
                Assert.True(lineas <= 400,
                    $"{Path.GetFileName(archivo)} tiene {lineas} líneas (máximo 400)");
            }
        }

        [Fact]
        public void LaFachadaNoComponeElAlmacenamientoLocal()
        {
            // PAT-01 en .NET: la fachada decide qué usar, no qué construir. El
            // store por cuenta, el historial y la caché los monta
            // ProveedorSesionLocal (el composition root). Solo la conexión de red
            // sigue naciendo aquí, porque su ciclo de vida es el de la fachada.
            string[] prohibidas =
            {
                "new HistorialLocal(", "new CacheUsuarios(", "new ProveedorSesionLocal("
            };

            foreach (string parcial in Directory.GetFiles(
                Path.Combine(RutaCliente(), "Fachada"), "FachadaCliente*.cs"))
            {
                string fuente = File.ReadAllText(parcial);
                foreach (string construccion in prohibidas)
                    Assert.DoesNotContain(construccion, fuente, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void ElAlmacenamientoLocalSeComponeEnElCompositionRoot()
        {
            string proveedor = LeerFuenteCliente("Composicion/ProveedorSesionLocal.cs");
            Assert.Contains("RutaParaCuenta", proveedor, StringComparison.Ordinal);
            Assert.Contains("MigrarLegadoACuenta", proveedor, StringComparison.Ordinal);
            Assert.Contains("new HistorialLocal(", proveedor, StringComparison.Ordinal);
            Assert.Contains("new CacheUsuarios(", proveedor, StringComparison.Ordinal);

            string fabrica = LeerFuenteCliente("Composicion/FabricaCliente.cs");
            Assert.Contains("namespace Mensajeria.Composicion", fabrica,
                StringComparison.Ordinal);
        }

        [Fact]
        public void LaVistaDelChatEstaDivididaPorResponsabilidad()
        {
            // El code-behind del chat no puede volver a crecer como un bloque:
            // si falta comportamiento, se reparte en otro parcial.
            string carpeta = Path.Combine(RutaCliente(), "Views");
            string[] parciales = Directory.GetFiles(carpeta, "ChatWindow.*.cs");
            string[] responsabilidades =
            {
                "ChatWindow.Estado.cs", "ChatWindow.Sesion.cs", "ChatWindow.Eventos.cs",
                "ChatWindow.Vista.cs", "ChatWindow.Contactos.cs", "ChatWindow.Historial.cs",
                "ChatWindow.Navegacion.cs", "ChatWindow.Busqueda.cs",
                "ChatWindow.Adjuntos.cs", "ChatWindow.Envio.cs"
            };

            foreach (string esperado in responsabilidades)
            {
                string ruta = Path.Combine(carpeta, esperado);
                Assert.True(File.Exists(ruta), $"falta el parcial {esperado}");
                string fuente = File.ReadAllText(ruta);
                Assert.Contains("partial class ChatWindow", fuente, StringComparison.Ordinal);
            }

            Assert.True(parciales.Length >= responsabilidades.Length,
                $"se esperaban al menos {responsabilidades.Length} parciales y hay {parciales.Length}");
        }

        private static string LeerFuenteCliente(string rutaRelativa) =>
            File.ReadAllText(Path.Combine(RutaCliente(), rutaRelativa.Replace('/', Path.DirectorySeparatorChar)));

        private static string RutaCliente()
        {
            for (DirectoryInfo? carpeta = new(AppContext.BaseDirectory);
                 carpeta != null; carpeta = carpeta.Parent)
            {
                string directa = Path.Combine(carpeta.FullName, "client-dotnet");
                if (Directory.Exists(directa)
                    && Directory.Exists(Path.Combine(carpeta.FullName, "client-dotnet.Tests")))
                    return directa;

                string anidada = Path.Combine(carpeta.FullName,
                    "universidad-mensajeria", "client-dotnet");
                if (Directory.Exists(anidada)) return anidada;
            }

            throw new DirectoryNotFoundException(
                "No se encontró universidad-mensajeria/client-dotnet desde el directorio de pruebas.");
        }
    }
}
