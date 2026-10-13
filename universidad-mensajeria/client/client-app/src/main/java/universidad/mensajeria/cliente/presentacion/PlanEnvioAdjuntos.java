package universidad.mensajeria.cliente.presentacion;

import java.util.ArrayList;
import java.util.List;

/**
 * R3: qué se envía, en qué orden y cuántas burbujas salen.
 * <p>
 * Puro: ni JavaFX, ni red, ni disco. El comando solo ejecuta el plan, así que
 * toda la política queda testeada sin levantar nada.
 * </p>
 * <p>
 * <b>Burbujas.</b> El protocolo manda un adjunto por trama y la UI pinta una
 * burbuja por mensaje. Para que varias imágenes se vean como UNA sola burbuja, el
 * cliente las compone en un montaje antes de enviar (ver {@link MontajeImagenes});
 * los documentos no se pueden componer y siguen yendo en mensajes propios.
 * </p>
 * <p>
 * Reglas:
 * <ul>
 *   <li>0 adjuntos → el texto viaja solo.</li>
 *   <li>1 adjunto → el texto es su pie/caption: UN mensaje, UNA burbuja.</li>
 *   <li>2+ imágenes → <b>una sola burbuja</b> (montaje), con o sin texto.</li>
 *   <li>imágenes + documentos → la burbuja de imágenes más una por documento.</li>
 *   <li>el texto es el pie de la PRIMERA burbuja si es la única; si hay varias,
 *       va como mensaje suelto antes (un pie no puede repetirse en N).</li>
 * </ul>
 * </p>
 */
public final class PlanEnvioAdjuntos {

    /** Una acción de envío pendiente. */
    public sealed interface Envio permits Texto, Adjunto, Grupo { }

    /** Mensaje de texto suelto. */
    public record Texto(String cuerpo) implements Envio { }

    /** Un adjunto suelto, con su caption (solo si es la única burbuja). */
    public record Adjunto(AdjuntoPendiente archivo, String caption) implements Envio { }

    /** Varias imágenes que viajan como una sola burbuja. */
    public record Grupo(List<AdjuntoPendiente> imagenes, String caption) implements Envio { }

    private PlanEnvioAdjuntos() {
    }

    public static List<Envio> calcular(String texto, List<AdjuntoPendiente> adjuntos) {
        String cuerpo = texto == null ? "" : texto.trim();
        List<AdjuntoPendiente> lista = adjuntos == null ? List.of() : adjuntos;

        if (lista.isEmpty()) {
            return cuerpo.isEmpty() ? List.of() : List.of(new Texto(cuerpo));
        }

        List<AdjuntoPendiente> imagenes = new ArrayList<>();
        List<AdjuntoPendiente> archivos = new ArrayList<>();
        for (AdjuntoPendiente a : lista) {
            (a.esImagen() ? imagenes : archivos).add(a);
        }

        List<Envio> burbujas = new ArrayList<>();
        if (imagenes.size() >= 2) {
            burbujas.add(new Grupo(List.copyOf(imagenes), null));
        } else if (imagenes.size() == 1) {
            burbujas.add(new Adjunto(imagenes.get(0), null));
        }
        for (AdjuntoPendiente a : archivos) {
            burbujas.add(new Adjunto(a, null));
        }

        // El texto se pega a la única burbuja; si hay varias, mensaje propio antes.
        if (burbujas.size() == 1 && !cuerpo.isEmpty()) {
            burbujas.set(0, conCaption(burbujas.get(0), cuerpo));
            return burbujas;
        }
        if (!cuerpo.isEmpty()) {
            burbujas.add(0, new Texto(cuerpo));
        }
        return burbujas;
    }

    /** true si no hay nada que enviar (el botón debe seguir deshabilitado). */
    public static boolean vacio(String texto, List<AdjuntoPendiente> adjuntos) {
        return calcular(texto, adjuntos).isEmpty();
    }

    private static Envio conCaption(Envio envio, String caption) {
        return switch (envio) {
            case Grupo g -> new Grupo(g.imagenes(), caption);
            case Adjunto a -> new Adjunto(a.archivo(), caption);
            case Texto t -> t;
        };
    }
}