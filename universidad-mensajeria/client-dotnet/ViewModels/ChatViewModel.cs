using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Mensajeria.ViewModels
{
    /// <summary>
    /// Estado de la conversación: con quién se habla, qué hay escrito, si se
    /// está enviando y dónde se está de la paginación. Espejo de
    /// <c>ui/chat_view_model.py</c> del cliente Python y del
    /// <c>ChatViewModel</c> de JavaFX.
    /// </summary>
    /// <remarks>
    /// Antes esto eran veinte campos privados de <c>ChatWindow</c> repartidos
    /// por doce parciales, y «¿puedo enviar?» era una línea en la ventana que
    /// alguien tenía que acordarse de llamar. Aquí el estado es un sitio y el
    /// predicado se deriva de él.
    /// <para>
    /// A propósito **no** depende de WPF (ni de <c>System.Windows.Media</c> como
    /// los demás VMs de la carpeta): así <c>client-dotnet.Tests</c> lo compila y
    /// lo prueba. Por eso el <c>csproj</c> de los tests lo incluye fichero a
    /// fichero en vez de con el glob de la carpeta.
    /// </para>
    /// </remarks>
    public sealed class ChatViewModel : INotifyPropertyChanged
    {
        private string? _usuario;
        private string _texto = "";
        private bool _enviando;
        private bool _hayAdjuntos;
        private bool _cargando;
        private string _cita = "";
        private int _offset;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Cuenta con la que se conversa (null si ninguna).</summary>
        public string? Usuario
        {
            get => _usuario;
            set
            {
                if (_usuario == value) return;
                _usuario = value;
                // Cambiar de conversación reinicia la paginación: el offset
                // belonged a la anterior y aplicarlo a la nueva saltaba
                // mensajes del medio.
                _offset = 0;
                OnPropertyChanged(nameof(Usuario));
                OnPropertyChanged(nameof(Offset));
                OnPropertyChanged(nameof(PuedeEnviar));
            }
        }

        /// <summary>Texto del compositor (sin el prefijo de la cita).</summary>
        public string Texto
        {
            get => _texto;
            set
            {
                if (_texto == value) return;
                _texto = value;
                OnPropertyChanged(nameof(Texto));
                OnPropertyChanged(nameof(PuedeEnviar));
            }
        }

        /// <summary>Hay un envío en curso: el botón espera.</summary>
        public bool Enviando
        {
            get => _enviando;
            set
            {
                if (_enviando == value) return;
                _enviando = value;
                OnPropertyChanged(nameof(Enviando));
                OnPropertyChanged(nameof(PuedeEnviar));
            }
        }

        /// <summary>La bandeja de adjuntos tiene algo pendiente.</summary>
        public bool HayAdjuntos
        {
            get => _hayAdjuntos;
            set
            {
                if (_hayAdjuntos == value) return;
                _hayAdjuntos = value;
                OnPropertyChanged(nameof(HayAdjuntos));
                OnPropertyChanged(nameof(PuedeEnviar));
            }
        }

        /// <summary>Se está cargando historial (spinner o botón de anteriores).</summary>
        public bool Cargando
        {
            get => _cargando;
            set
            {
                if (_cargando == value) return;
                _cargando = value;
                OnPropertyChanged(nameof(Cargando));
            }
        }

        /// <summary>Prefijo completo de la respuesta en curso ("" si no hay).</summary>
        public string Cita
        {
            get => _cita;
            set
            {
                if (_cita == value) return;
                _cita = value;
                OnPropertyChanged(nameof(Cita));
            }
        }

        /// <summary>Mensajes locales ya cargados de esta conversación.</summary>
        public int Offset
        {
            get => _offset;
            set
            {
                if (_offset == value) return;
                _offset = value;
                OnPropertyChanged(nameof(Offset));
            }
        }

        /// <summary>
        /// El único sitio que decide si el botón de enviar se habilita.
        /// </summary>
        /// <remarks>
        /// Es un método y no una propiedad a propósito: depende de tres datos a
        /// la vez, y en WPF eso sería una propiedad que reevalúa para un botón
        /// que solo cambia de enabled.
        /// </remarks>
        public bool PuedeEnviar =>
            !_enviando && !string.IsNullOrEmpty(_usuario)
                && (!string.IsNullOrWhiteSpace(_texto) || _hayAdjuntos);

        private void OnPropertyChanged([CallerMemberName] string? propiedad = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
    }
}
