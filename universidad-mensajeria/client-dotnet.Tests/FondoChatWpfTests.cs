using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Mensajeria.Tests
{
    public class FondoChatWpfTests
    {
        private static string RaizMonorepo => Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));

        [Fact]
        public void ListaTransparenteDejaVerElFondoDelPanelQueLaContiene()
        {
            string ruta = Path.Combine(RaizMonorepo, "client-dotnet", "Views", "ChatWindow.xaml");
            var documento = XDocument.Load(ruta);
            XNamespace wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            var panel = Assert.Single(documento.Descendants(wpf + "Grid"),
                elemento => (string?)elemento.Attribute(xaml + "Name") == "panelMensajes");
            var lista = Assert.Single(panel.Elements(wpf + "ListBox"),
                elemento => (string?)elemento.Attribute(xaml + "Name") == "lstMensajes");

            Assert.Equal("{DynamicResource ChatBgBrush}", (string?)panel.Attribute("Background"));
            Assert.Equal("Transparent", (string?)lista.Attribute("Background"));
        }

        [Fact]
        public void RefrescarTemaNotificaLosTresPincelesSinRecargar()
        {
            // UIX-27: MensajeVM es WPF y no compila en el proyecto de tests
            // (net10.0 sin UseWPF); se verifica por fuente, como ArquitecturaTests.
            string ruta = Path.Combine(RaizMonorepo, "client-dotnet", "ViewModels", "MensajeVM.cs");
            string fuente = File.ReadAllText(ruta);
            Assert.Contains("public void RefrescarTema()", fuente);
            Assert.Contains("nameof(Fondo)", fuente);
            Assert.Contains("nameof(ColorEstado)", fuente);
            Assert.Contains("nameof(ColorContenido)", fuente);

            string vista = File.ReadAllText(Path.Combine(
                RaizMonorepo, "client-dotnet", "Views", "ChatWindow.Vista.cs"));
            int handler = vista.IndexOf("CmbTema_SelectionChanged", StringComparison.Ordinal);
            Assert.True(handler >= 0);
            string bloque = vista.Substring(handler, vista.IndexOf("AplicarFondoChat(string", StringComparison.Ordinal) - handler);
            Assert.Contains("RefrescarTema()", bloque);
            Assert.DoesNotContain("CargarHistorial", bloque);
        }

        [Fact]
        public void EstanLosDosJpgDeTemaYSonImagenesValidas()
        {
            string carpeta = Path.Combine(RaizMonorepo, "client-dotnet", "Temas", "fondos");
            foreach (string nombre in new[] { "fondo_chat_claro.jpg", "fondo_chat_oscuro.jpg" })
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(carpeta, nombre));
                Assert.True(bytes.Length > 4);
                Assert.Equal(new byte[] { 0xFF, 0xD8 }, bytes[..2]);
                Assert.Equal(new byte[] { 0xFF, 0xD9 }, bytes[^2..]);
            }
        }
    }
}
