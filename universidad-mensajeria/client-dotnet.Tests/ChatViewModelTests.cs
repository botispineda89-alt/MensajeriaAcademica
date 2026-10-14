using System.Collections.Generic;
using Mensajeria.ViewModels;
using Xunit;

namespace Mensajeria.Tests
{
    /// <summary>
    /// PAT-VM en .NET: el estado de la conversación es de <see cref="ChatViewModel"/>
    /// y «¿puedo enviar?» se deriva de él una sola vez. Antes eran cinco campos
    /// privados de <c>ChatWindow</c> y un <c>IsEnabled</c> escrito a mano.
    /// </summary>
    public class ChatViewModelTests
    {
        private static ChatViewModel Con =>
            new() { Usuario = "160004726", Texto = "hola" };

        [Theory]
        [InlineData("160004726", "hola", false, false, true)]   // normal
        [InlineData(null, "hola", false, false, false)]          // sin destinatario
        [InlineData("160004726", "", false, false, false)]       // nada escrito
        [InlineData("160004726", "   ", false, false, false)]    // solo espacios
        [InlineData("160004726", "", false, true, true)]        // solo adjuntos
        [InlineData("160004726", "hola", true, true, false)]     // ya enviando
        public void PuedeEnviarEsLaDerivacionDelEstado(
            string? usuario, string texto, bool enviando, bool adjuntos, bool esperado)
        {
            var vm = new ChatViewModel
            {
                Usuario = usuario,
                Texto = texto,
                Enviando = enviando,
                HayAdjuntos = adjuntos
            };

            Assert.Equal(esperado, vm.PuedeEnviar);
        }

        [Fact]
        public void CambiarDeConversacionReiniciaElOffset()
        {
            var vm = new ChatViewModel { Usuario = "160004726", Offset = 120 };

            vm.Usuario = "160004716";

            // El offset era de la conversación anterior: aplicarlo a la nueva
            // se saltaba mensajes del medio.
            Assert.Equal(0, vm.Offset);
        }

        [Fact]
        public void ElTextoNoCambiadoNoAvanzaNotificacion()
        {
            var vm = Con;
            var avisos = new List<string>();
            vm.PropertyChanged += (_, e) => avisos.Add(e.PropertyName ?? "");

            vm.Texto = "hola";           // mismo valor
            vm.Usuario = "160004726";   // mismo valor

            Assert.Empty(avisos);

            vm.Texto = "hola!";

            Assert.Equal(new[] { nameof(ChatViewModel.Texto), nameof(ChatViewModel.PuedeEnviar) },
                avisos);
        }

        [Fact]
        public void UnAdjuntoAvanzaElPredicadoYLaNotificacion()
        {
            var vm = new ChatViewModel { Usuario = "160004726", Texto = "" };
            var avisos = new List<string>();
            vm.PropertyChanged += (_, e) => avisos.Add(e.PropertyName ?? "");

            Assert.False(vm.PuedeEnviar);
            vm.HayAdjuntos = true;

            Assert.True(vm.PuedeEnviar);
            Assert.Contains(nameof(ChatViewModel.PuedeEnviar), avisos);
        }

        [Fact]
        public void CambiarDeDestinatarioNotificaOffsetYPredicado()
        {
            var vm = Con;
            vm.Offset = 50;
            var avisos = new List<string>();
            vm.PropertyChanged += (_, e) => avisos.Add(e.PropertyName ?? "");

            vm.Usuario = "160004716";

            Assert.Equal(new[]
            {
                nameof(ChatViewModel.Usuario),
                nameof(ChatViewModel.Offset),
                nameof(ChatViewModel.PuedeEnviar)
            }, avisos);
        }
    }
}
