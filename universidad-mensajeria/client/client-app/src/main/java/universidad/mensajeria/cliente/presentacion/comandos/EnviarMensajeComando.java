package universidad.mensajeria.cliente.presentacion.comandos;

import universidad.mensajeria.cliente.presentacion.AdjuntoPendiente;
import universidad.mensajeria.cliente.presentacion.FormatoErrorCliente;
import universidad.mensajeria.cliente.presentacion.InterfazChatViewModel;
import universidad.mensajeria.cliente.presentacion.MontajeImagenes;
import universidad.mensajeria.cliente.presentacion.PlanEnvioAdjuntos.Adjunto;
import universidad.mensajeria.cliente.presentacion.PlanEnvioAdjuntos.Envio;
import universidad.mensajeria.cliente.presentacion.PlanEnvioAdjuntos.Grupo;
import universidad.mensajeria.cliente.presentacion.PlanEnvioAdjuntos.Texto;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.Executor;
import java.util.function.Consumer;
import javafx.concurrent.Task;

/**
 * R3: ejecuta un plan de envíos EN SERIE.
 * <p>
 * En serie, nunca en paralelo: el socket es único y multiplexado por {@code id},
 * el servidor tiene un pool con token bucket por conexión y en el chat del
 * destinatario el orden importa. Uno que falla no impide a los demás: se recoge
 * el motivo y se reporta al final.
 * <p>
 * Con un solo adjunto se comporta exactamente como antes: UN mensaje con el texto
 * como pie. Con varios, el texto viaja como mensaje propio y los adjuntos sueltos.
 * </p>
 */
public final class EnviarMensajeComando implements ComandoChat {

    private final InterfazChatViewModel viewModel;
    private final Executor fondo;
    private final String destino;
    private final List<Envio> plan;
    private final MensajeLocal respuesta;
    private final Consumer<Boolean> alTerminar;
    private final Consumer<String> alError;
    /** §7 paridad: progreso del lote y cancelación (como ProgresoEnvio de .NET). */
    private final Consumer<String> alProgreso;
    private final Runnable alCancelar;
    /** Fallos del lote, para el aviso final. Vive fuera de la Task a propósito. */
    private final List<String> fallos = new ArrayList<>();
    /** Cancelación consultada entre envíos del lote; volátil para el hilo de fondo. */
    private volatile boolean cancelado;

    public EnviarMensajeComando(InterfazChatViewModel viewModel, Executor fondo,
                                String destino, List<Envio> plan, MensajeLocal respuesta,
                                Consumer<Boolean> alTerminar, Consumer<String> alError) {
        this(viewModel, fondo, destino, plan, respuesta, alTerminar, alError, null, null);
    }

    public EnviarMensajeComando(InterfazChatViewModel viewModel, Executor fondo,
                                String destino, List<Envio> plan, MensajeLocal respuesta,
                                Consumer<Boolean> alTerminar, Consumer<String> alError,
                                Consumer<String> alProgreso, Runnable alCancelar) {
        this.viewModel = viewModel;
        this.fondo = fondo;
        this.destino = destino;
        this.plan = List.copyOf(plan);
        this.respuesta = respuesta;
        this.alTerminar = alTerminar;
        this.alError = alError;
        this.alProgreso = alProgreso;
        this.alCancelar = alCancelar;
    }

    /** §7 paridad: la UI marca la cancelación; el lote la consulta entre envíos. */
    public void cancelar() {
        cancelado = true;
        if (alCancelar != null) {
            alCancelar.run();
        }
    }

    @Override
    public void ejecutar() {
        Task<Boolean> tarea = new Task<>() {
            @Override
            protected Boolean call() {
                boolean todosEnRed = true;
                for (Envio envio : plan) {
                    if (cancelado) {
                        fallos.add(nombreDe(envio) + ": cancelado por el usuario");
                        continue;
                    }
                    informarProgreso(envio);
                    try {
                        boolean enRed = switch (envio) {
                            case Texto t -> viewModel.enviarTexto(destino, t.cuerpo(), respuesta);
                            case Adjunto a -> enviarAdjunto(a);
                            // R3: las imágenes van compuestas en UNA sola trama,
                            // así el otro cliente ve una sola burbuja.
                            case Grupo g -> enviarGrupo(g);
                        };
                        if (!enRed) {
                            // false = quedó en cola local: no es un error duro, se reintenta.
                            todosEnRed = false;
                            fallos.add(nombreDe(envio) + " quedó en cola para reintentar");
                        }
                    } catch (Exception fallo) {
                        todosEnRed = false;
                        fallos.add(nombreDe(envio) + ": " + FormatoErrorCliente.mensaje(fallo));
                    }
                }
                return todosEnRed;
            }
        };
        tarea.setOnSucceeded(e -> {
            if (fallos.isEmpty()) {
                alTerminar.accept(tarea.getValue());
            } else {
                // Un envío parcial NO es un fallo: se avisa y se repinta igual.
                alError.accept("No se/enviaron " + fallos.size() + " de " + plan.size()
                        + ": " + String.join("; ", fallos));
            }
        });
        tarea.setOnFailed(e -> alError.accept(tarea.getException() == null
                ? "envío fallido" : tarea.getException().getMessage()));
        fondo.execute(tarea);
    }

    /** §7 paridad: avisa al hilo de UI cuánto va del lote. */
    private void informarProgreso(Envio envio) {
        if (alProgreso == null) {
            return;
        }
        long bytes = envio instanceof Adjunto a ? a.archivo().bytes().length : 0L;
        String nombre = nombreDe(envio);
        javafx.application.Platform.runLater(() -> alProgreso.accept(
                nombre + " (" + (bytes / 1024) + " KB)"));
    }

    private boolean enviarAdjunto(Adjunto a) throws Exception {
        AdjuntoPendiente p = a.archivo();
        return p.esImagen()
                ? viewModel.enviarImagen(destino, p.bytes(), p.nombre(), p.mime(),
                        a.caption(), respuesta)
                : viewModel.enviarArchivo(destino, p.bytes(), p.nombre(), p.mime(),
                        a.caption(), respuesta);
    }

    /**
     * R3: compone las imágenes y las manda como una sola trama.
     * <p>
     * Solo se manda suelta si el montaje <b>no se pudo componer</b>, que es un
     * fallo local del que sabemos que no salió un byte por la red.
     * </p>
     * <p>
     * Si lo que falla es el envío, NO se reenvía: a partir de la red el
     * resultado es ambiguo (el servidor puede haber guardado el montaje y
     * haberse perdido solo el ACK; pasa cuando el servidor descarta tramas por
     * límite de frecuencia). Reenviar entonces duplicaba el contenido y el
     * servidor reventaba con {@code Duplicate entry ... for key 'archivos.ruta'},
     * con lo que se perdía el mensaje entero.
     * </p>
     */
    private boolean enviarGrupo(Grupo g) throws Exception {
        MontajeImagenes.Resultado montaje;
        try {
            montaje = MontajeImagenes.componer(g.imagenes());
        } catch (Exception noSePudoComponer) {
            fallos.add("No se pudieron agrupar las imágenes ("
                    + FormatoErrorCliente.mensaje(noSePudoComponer)
                    + "); se envían por separado");
            boolean todas = true;
            for (AdjuntoPendiente a : g.imagenes()) {
                todas &= enviarAdjunto(new Adjunto(a, null));
            }
            return todas;
        }
        try {
            return viewModel.enviarImagen(destino, montaje.png(), montaje.nombre(),
                    "image/png", g.caption(), respuesta);
        } catch (Exception falloDeRed) {
            fallos.add("Las " + g.imagenes().size() + " imágenes quedaron en cola"
                    + " para reintentar: " + FormatoErrorCliente.mensaje(falloDeRed));
            return false;
        }
    }

    private static String nombreDe(Envio envio) {
        return switch (envio) {
            case Texto t -> "El texto";
            case Grupo g -> "Las " + g.imagenes().size() + " imágenes";
            case Adjunto a -> a.archivo().nombre();
        };
    }
}