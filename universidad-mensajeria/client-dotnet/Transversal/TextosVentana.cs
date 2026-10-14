namespace Mensajeria.Transversal
{
    /// <summary>
    /// Literales de ventana y diálogo en un solo sitio (VIS-02).
    /// </summary>
    /// <remarks>
    /// Fuente de verdad: <c>contrato/textos-ui.json</c> (que a su vez sale de
    /// <c>DIAGRAMAS/textos-ui.md</c>). <c>contrato/verificar_textos_ui.py</c>
    /// comprueba que este módulo contiene el texto exacto del contrato y que
    /// ningún otro fuente lo repite inline (los tests sí pueden fijarlos).
    /// Las vistas los referencian con <c>x:Static</c> en el XAML; lo que ya
    /// estaba centralizado (<see cref="FormatoPreview"/>,
    /// <see cref="MensajesUsuario"/>) se deja donde está y el contrato lo
    /// apunta allí.
    /// </remarks>
    public static class TextosVentana
    {
        /// <summary>Título de la ventana de login.</summary>
        public const string TituloLogin = "Mensajería Académica | Iniciar sesión";

        /// <summary>Título de la ventana de chat.</summary>
        public const string TituloChat = "Mensajería Académica | Chat";

        /// <summary>Título del visor de imagen.</summary>
        public const string TituloVisor = "Imagen";

        /// <summary>Título de los diálogos de salida.</summary>
        public const string ConfirmarSalida = "Confirmar salida";

        /// <summary>Confirmación de volver al inicio.</summary>
        public const string CerrarSesionPregunta =
            "¿Deseas cerrar esta sesión y volver al inicio?";

        /// <summary>Confirmación de cerrar el login.</summary>
        public const string CerrarProgramaPregunta = "¿Deseas cerrar el programa?";

        /// <summary>Confirmación de cerrar todas las sesiones.</summary>
        public const string SalirTodosPregunta =
            "¿Cerrar todas las sesiones de esta cuenta?";

        /// <summary>Aviso tras expulsar otras sesiones.</summary>
        public const string OtrasSesionesEstado = "Se solicitó cerrar tus otras sesiones";

        /// <summary>Aviso de envío sin red (queda en cola).</summary>
        public const string SinRed = "Sin red: guardado para reintentar";

        /// <summary>Aviso cuando la cita no está en el historial.</summary>
        public const string CitaNoDisponible =
            "El mensaje citado no está disponible en este historial.";

        /// <summary>Cabecera sin conversación.</summary>
        public const string SeleccionaConversacion = "Selecciona una conversación";

        /// <summary>Fila de contacto sin mensajes.</summary>
        public const string SinMensajesFila = "Sin mensajes aún";

        /// <summary>Placeholder de conversación vacía (guía UIX-13).</summary>
        public const string VacioChat = "Sin mensajes aún. Escribe el primero.";

        /// <summary>Botón de paginación del historial.</summary>
        public const string CargarAnteriores = "↑ Cargar mensajes anteriores";
    }
}
