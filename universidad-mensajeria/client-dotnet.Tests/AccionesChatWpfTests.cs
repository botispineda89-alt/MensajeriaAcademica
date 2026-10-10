using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Mensajeria.Tests
{
    public class AccionesChatWpfTests
    {
        private static string RaizMonorepo => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));

        [Fact]
        public void ComposerEnviaConEnterYReservaMayusEnterParaElSalto()
        {
            string ruta = Path.Combine(RaizMonorepo, "client-dotnet", "Views", "ChatWindow.xaml");
            var documento = XDocument.Load(ruta);
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            XNamespace ui = "http://schemas.lepo.co/wpfui/2022/xaml";
            var composer = Assert.Single(documento.Descendants(ui + "TextBox"),
                elemento => (string?)elemento.Attribute(xaml + "Name") == "txtMensaje");

            Assert.Equal("True", (string?)composer.Attribute("AcceptsReturn"));
            // UIX-20: tiene que ser PreviewKeyDown. Con KeyDown, el manejador de
            // clase del TextBox insertaba el salto de línea ANTES de que llegara
            // el manejador de instancia y Enter nunca enviaba el mensaje.
            Assert.Equal("TxtMensaje_PreviewKeyDown",
                (string?)composer.Attribute("PreviewKeyDown"));
            Assert.Null(composer.Attribute("KeyDown"));
            Assert.Contains("Enter para enviar, Mayús+Enter para salto",
                (string?)composer.Attribute("PlaceholderText"));
        }

        [Fact]
        public void ElLoginNoSeEstiraAlMaximizarComoEnJava()
        {
            // UIX-20: en Java la tarjeta es VBox(maxWidth=420) dentro de un
            // StackPane centrado. Si aquí la tarjeta no lleva MaxWidth ni
            // centrado, al maximizar el formulario ocupa toda la pantalla.
            string ruta = Path.Combine(RaizMonorepo, "client-dotnet", "Views", "LoginWindow.xaml");
            var documento = XDocument.Load(ruta);
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var ventana = Assert.Single(documento.Descendants(wpf + "Window"));

            var tarjeta = Assert.IsType<System.Xml.Linq.XElement>(
                Assert.Single(ventana.Elements(wpf + "ScrollViewer")
                    .Elements(wpf + "Grid")
                    .Elements(wpf + "Border")));
            Assert.Equal("420", (string?)tarjeta.Attribute("MaxWidth"));
            Assert.Equal("Center", (string?)tarjeta.Attribute("HorizontalAlignment"));
            Assert.Equal("Center", (string?)tarjeta.Attribute("VerticalAlignment"));
        }

        [Fact]
        public void AccionKickOtrasEsDistintaDeSalirEnTodos()
        {
            string ruta = Path.Combine(RaizMonorepo, "client-dotnet", "Views", "ChatWindow.xaml");
            var documento = XDocument.Load(ruta);
            XNamespace ui = "http://schemas.lepo.co/wpfui/2022/xaml";
            var botones = documento.Descendants(ui + "Button").ToArray();

            var kick = Assert.Single(botones, boton =>
                (string?)boton.Attribute("Content") == "KICK otras");
            var salirTodos = Assert.Single(botones, boton =>
                (string?)boton.Attribute("Content") == "Salir en todos");

            Assert.Equal("BtnKickOtras_Click", (string?)kick.Attribute("Click"));
            Assert.Equal("BtnSalirTodos_Click", (string?)salirTodos.Attribute("Click"));
        }
    }
}
