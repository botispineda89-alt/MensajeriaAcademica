using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Mensajeria.Tests
{
    /// <summary>
    /// Revisión visual del 2026-10-14, cliente WPF:
    /// <list type="bullet">
    /// <item><b>N2</b>: la presencia del contacto abierto vive en la cabecera y la
    /// cuenta de contactos en línea en el sidebar. Antes las dos escribían el mismo
    /// <c>txtEstado</c>, así que el `en línea`/`desconectado` duraba hasta el
    /// siguiente refresco del directorio.</item>
    /// <item><b>T1</b>: Esc deja la ventana sin conversación y el chat arranca sin
    /// selección (murió el auto-restore del último chat).</item>
    /// </list>
    /// Igual que el resto de pruebas WPF del proyecto, el XAML se lee como XML y el
    /// code-behind como texto: el proyecto de pruebas no compila <c>Views/</c> (es
    /// net10.0 y la ventana es net8.0-windows).
    /// </summary>
    public class CabeceraYAtajosWpfTests
    {
        private static readonly XNamespace Wpf =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        private static readonly XNamespace Xaml =
            "http://schemas.microsoft.com/winfx/2006/xaml";

        [Fact]
        public void LaPresenciaDelContactoVaEnLaCabeceraYLaCuentaEnElSidebar()
        {
            var documento = XDocument.Load(Path.Combine(RutaCliente(), "Views", "ChatWindow.xaml"));
            // Las dos columnas del Grid raíz: 0 = sidebar, 2 = conversación. Se miran
            // los hijos directos y no cualquier descendiente: dentro hay más Grids
            // con sus propias columnas.
            var raiz = documento.Descendants(Wpf + "Grid").First();
            var sidebar = Assert.Single(raiz.Elements(),
                elemento => (string?)elemento.Attribute("Grid.Column") == "0");
            var conversacion = Assert.Single(raiz.Elements(),
                elemento => (string?)elemento.Attribute("Grid.Column") == "2");
            // La cabecera es el borde que lleva el título de la conversación.
            var cabecera = Assert.Single(conversacion.Descendants(Wpf + "Border"),
                borde => borde.Descendants().Any(elemento => Nombre(elemento) == "txtTitulo"));

            Assert.Contains(cabecera.Descendants(), elemento => Nombre(elemento) == "txtEstado");
            Assert.DoesNotContain(cabecera.Descendants(), elemento => Nombre(elemento) == "txtEstadoRed");
            Assert.Contains(sidebar.Descendants(), elemento => Nombre(elemento) == "txtEstadoRed");
            Assert.DoesNotContain(sidebar.Descendants(), elemento => Nombre(elemento) == "txtEstado");
        }

        [Fact]
        public void CadaEtiquetaDeEstadoSeEscribeDondeSePinta()
        {
            string contactos = Fuente("Views", "ChatWindow.Contactos.cs");
            string estado = Fuente("Views", "ChatWindow.Estado.cs");
            string ventana = Fuente("Views", "ChatWindow.xaml.cs");

            // N2: la cabecera se refresca desde el contacto abierto...
            Assert.Contains("private void RefrescarPresenciaCabecera()", contactos, StringComparison.Ordinal);
            Assert.Contains("txtEstado.Text = UsuarioDestino == null", contactos, StringComparison.Ordinal);
            Assert.Contains("txtEstadoRed.Text = $\"{_directorio.Count(u => u.Conectado)} contactos en línea\"",
                contactos, StringComparison.Ordinal);
            // ...y el estado de conexión degradado ya no la pisa (va al banner y al sidebar).
            Assert.Contains("if (txtEstadoRed != null)", estado, StringComparison.Ordinal);
            Assert.DoesNotContain("txtEstado.Text", estado, StringComparison.Ordinal);
            Assert.Contains("RefrescarPresenciaCabecera();", estado, StringComparison.Ordinal);

            // T1: Esc suelta la conversación; el manejador mira la ventana entera.
            Assert.Contains("System.Windows.Input.Key.Escape", ventana, StringComparison.Ordinal);
            Assert.Contains("DeseleccionarConversacion();", ventana, StringComparison.Ordinal);
        }

        [Fact]
        public void EscDeseleccionaSinTocarElHistorialNiLaPreferencia()
        {
            string contactos = Fuente("Views", "ChatWindow.Contactos.cs");
            Assert.Contains("lstUsuarios.SelectedItem = null;", contactos, StringComparison.Ordinal);
            Assert.Contains("txtTitulo.Text = TextosVentana.SeleccionaConversacion;", contactos, StringComparison.Ordinal);
            // La preferencia se sigue guardando (es un dato del usuario)...
            Assert.Contains("PreferenciasChat.GuardarUltimoChat(", contactos, StringComparison.Ordinal);
            // ...pero nadie la lee para reabrir la conversación al arrancar.
            foreach (string parcial in Directory.GetFiles(
                Path.Combine(RutaCliente(), "Views"), "ChatWindow*.cs"))
            {
                string fuente = File.ReadAllText(parcial);
                Assert.DoesNotContain(".ResolverSeleccion(", fuente, StringComparison.Ordinal);
                Assert.DoesNotContain(".UltimoChat(", fuente, StringComparison.Ordinal);
            }
        }

        private static string? Nombre(XElement elemento) =>
            (string?)elemento.Attribute(Xaml + "Name");

        private static string Fuente(params string[] partes)
        {
            string ruta = RutaCliente();
            foreach (string parte in partes) ruta = Path.Combine(ruta, parte);
            return File.ReadAllText(ruta);
        }

        private static string RutaCliente()
        {
            for (DirectoryInfo? carpeta = new(AppContext.BaseDirectory);
                 carpeta != null; carpeta = carpeta.Parent)
            {
                string directa = Path.Combine(carpeta.FullName, "client-dotnet");
                if (Directory.Exists(directa)) return directa;

                string anidada = Path.Combine(carpeta.FullName,
                    "universidad-mensajeria", "client-dotnet");
                if (Directory.Exists(anidada)) return anidada;
            }

            throw new DirectoryNotFoundException(
                "No se encontró universidad-mensajeria/client-dotnet desde el directorio de pruebas.");
        }
    }
}
