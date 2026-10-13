using System;
using System.Collections.Generic;
using System.Linq;
using Mensajeria.Transversal.Dtos;

namespace Mensajeria.Transversal
{
    /// <summary>
    /// PLAN_AMBOS FASE 5: qué se envía, en qué orden y cuántas burbujas salen.
    /// <para>
    /// Espejo de <c>PlanEnvioAdjuntos.java</c> del cliente JavaFX, con las mismas
    /// reglas para que los dos clientes hagan lo mismo con el mismo material.
    /// </para>
    /// <para>
    /// Puro: ni WPF, ni red, ni disco. <see cref="Fachada.Servicios.EnvioMultipleAdjuntos"/>
    /// solo ejecuta el plan, así que toda la política queda testeada sin levantar
    /// una ventana.
    /// </para>
    /// <para>
    /// <b>Burbujas.</b> El protocolo manda un adjunto por trama y la UI pinta una
    /// burbuja por mensaje. Para que varias imágenes se vean como UNA sola burbuja,
    /// el cliente las compone en un montaje antes de enviar (ver <c>MontajeImagenes</c>);
    /// los documentos no se pueden componer y siguen yendo en mensajes propios.
    /// </para>
    /// </summary>
    public static class PlanEnvioAdjuntos
    {
        /// <summary>Una acción de envío pendiente.</summary>
        public abstract record Envio;

        /// <summary>Mensaje de texto suelto.</summary>
        public sealed record Texto(string Cuerpo) : Envio;

        /// <summary>Un adjunto suelto, con su caption (solo si es la única burbuja).</summary>
        public sealed record Adjunto(AdjuntoPendiente Archivo, string? Caption) : Envio;

        /// <summary>Varias imágenes que viajan como una sola burbuja.</summary>
        public sealed record Grupo(IReadOnlyList<AdjuntoPendiente> Imagenes, string? Caption) : Envio;

        /// <summary>
        /// Reglas:
        /// <list type="bullet">
        /// <item>0 adjuntos → el texto viaja solo.</item>
        /// <item>1 adjunto → el texto es su pie/caption: UN mensaje, UNA burbuja.</item>
        /// <item>2+ imágenes → <b>una sola burbuja</b> (montaje), con o sin texto.</item>
        /// <item>imágenes + documentos → la burbuja de imágenes más una por documento.</item>
        /// <item>el texto es el pie de la PRIMERA burbuja si es la única; si hay varias,
        /// va como mensaje suelto antes (un pie no puede repetirse en N).</item>
        /// </list>
        /// </summary>
        public static IReadOnlyList<Envio> Calcular(
            string? texto, IReadOnlyList<AdjuntoPendiente> adjuntos)
        {
            string cuerpo = (texto ?? "").Trim();
            IReadOnlyList<AdjuntoPendiente> lista =
                adjuntos ?? Array.Empty<AdjuntoPendiente>();

            if (lista.Count == 0)
                return cuerpo.Length == 0
                    ? Array.Empty<Envio>()
                    : new Envio[] { new Texto(cuerpo) };

            List<AdjuntoPendiente> imagenes = lista.Where(a => a.EsImagen).ToList();
            List<AdjuntoPendiente> archivos = lista.Where(a => !a.EsImagen).ToList();

            List<Envio> burbujas = new List<Envio>();
            if (imagenes.Count >= 2)
                burbujas.Add(new Grupo(imagenes, null));
            else if (imagenes.Count == 1)
                burbujas.Add(new Adjunto(imagenes[0], null));
            foreach (AdjuntoPendiente a in archivos)
                burbujas.Add(new Adjunto(a, null));

            // El texto se pega a la única burbuja; si hay varias, mensaje propio antes.
            if (burbujas.Count == 1 && cuerpo.Length > 0)
            {
                burbujas[0] = ConCaption(burbujas[0], cuerpo);
                return burbujas;
            }
            if (cuerpo.Length > 0)
                burbujas.Insert(0, new Texto(cuerpo));
            return burbujas;
        }

        /// <summary>true si no hay nada que enviar (el botón debe seguir deshabilitado).</summary>
        public static bool Vacio(string? texto, IReadOnlyList<AdjuntoPendiente> adjuntos) =>
            Calcular(texto, adjuntos).Count == 0;

        /// <summary>
        /// true si el texto escrito debe ir pegado como pie de los adjuntos en vez
        /// de mandarse como mensaje aparte: cuando los adjuntos forman una sola
        /// burbuja. Vive aquí, y no en la fachada, para que la vista no necesite
        /// conocer la fachada concreta (PAT-00).
        /// </summary>
        public static bool TextoVaComoPie(IReadOnlyList<AdjuntoPendiente> adjuntos) =>
            Calcular(null, adjuntos).Count == 1;

        private static Envio ConCaption(Envio envio, string caption) => envio switch
        {
            Grupo g => new Grupo(g.Imagenes, caption),
            Adjunto a => new Adjunto(a.Archivo, caption),
            Texto t => t,
            _ => envio
        };
    }
}