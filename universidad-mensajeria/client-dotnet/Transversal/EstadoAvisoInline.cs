using System;

namespace Mensajeria.Transversal
{
    /// <summary>Clasifica los avisos visibles dentro de la conversación.</summary>
    public enum TipoAvisoInline
    {
        Error,
        Advertencia,
        Informacion,
        Exito
    }

    /// <summary>
    /// Estado simple del banner inline del chat. Mantiene la transición visible/oculto
    /// fuera del code-behind para que el comportamiento pueda probarse sin abrir WPF.
    /// <para>
    /// UIX-26: además decide <b>cuándo se cierra solo</b>. Un aviso de éxito que
    /// se queda pegado tapando la conversación es ruido; un aviso de error que
    /// desaparece solo es un error que nadie llega a leer, así que los errores y
    /// los avisos de atención nunca se ocultan solos.
    /// </para>
    /// </summary>
    public sealed class EstadoAvisoInline
    {
        /// <summary>Cuánto dura un "Listo". Corto: informa y no estorba.</summary>
        public const int DURACION_EXITO_MS = 4000;

        /// <summary>Cuánto dura un aviso informativo.</summary>
        public const int DURACION_INFORMACION_MS = 6000;

        /// <summary>Cuánto dura una advertencia antes de quitarse sola.</summary>
        public const int DURACION_ADVERTENCIA_MS = 8000;

        /// <summary>
        /// Cuánto dura un error. Más que los demás porque hay que leerlo, pero
        /// <b>tampoco se queda pegado</b>: un error que tapa el chat para siempre
        /// acabaiseries Robinson; el detalle queda en el log y el ✕ sigue ahí
        /// para quien quiera ampliarlo.
        /// </summary>
        public const int DURACION_ERROR_MS = 12000;

        public string Mensaje { get; private set; } = "";
        public TipoAvisoInline Tipo { get; private set; } = TipoAvisoInline.Informacion;
        public bool Visible { get; private set; }

        /// <summary>
        /// Instante (monotónico) en que se mostró el aviso actual. Lo escribe
        /// <see cref="Mostrar(string?, TipoAvisoInline)"/>; la vista lo lee para
        /// decidir si ya toca cerrarlo.
        /// </summary>
        public long MostradoEn { get; private set; }

        public void Mostrar(string? mensaje, TipoAvisoInline tipo) =>
            Mostrar(mensaje, tipo, Environment.TickCount64);

        /// <summary>
        /// Igual que <see cref="Mostrar(string?, TipoAvisoInline)"/> pero con el
        /// reloj inyectado, para que los tests no dependan del tiempo real.
        /// </summary>
        public void Mostrar(string? mensaje, TipoAvisoInline tipo, long ahora)
        {
            if (string.IsNullOrWhiteSpace(mensaje))
            {
                Limpiar();
                return;
            }

            Mensaje = mensaje.Trim();
            Tipo = tipo;
            Visible = true;
            MostradoEn = ahora;
        }

        public void Limpiar()
        {
            Mensaje = "";
            Tipo = TipoAvisoInline.Informacion;
            Visible = false;
            MostradoEn = 0;
        }

        /// <summary>
        /// true si este aviso se cierra solo. <b>Todos</b>, errores incluidos:
        /// tapando la conversación, un aviso permanente es peor que uno que se
        /// va. Cada tipo tiene su plazo, para que el error dé tiempo de leerse.
        /// </summary>
        public bool DebeOcultarseAutomaticamente => true;

        /// <summary>Plazo de este aviso, en milisegundos.</summary>
        public int DuracionMs => Tipo switch
        {
            TipoAvisoInline.Exito => DURACION_EXITO_MS,
            TipoAvisoInline.Informacion => DURACION_INFORMACION_MS,
            TipoAvisoInline.Advertencia => DURACION_ADVERTENCIA_MS,
            _ => DURACION_ERROR_MS
        };

        /// <summary>
        /// Milisegundos que faltan para cerrarlo, o <c>-1</c> si ya no hay nada
        /// que cerrar. La vista solo tiene que mirar el signo y el cero.
        /// </summary>
        public long MilisegundosRestantes(long ahora)
        {
            if (!Visible || !DebeOcultarseAutomaticamente)
            {
                return -1;
            }
            long restante = DuracionMs - (ahora - MostradoEn);
            return restante < 0 ? 0 : restante;
        }
    }
}