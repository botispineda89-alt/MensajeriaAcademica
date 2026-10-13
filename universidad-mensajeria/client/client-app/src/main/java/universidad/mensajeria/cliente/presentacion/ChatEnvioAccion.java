package universidad.mensajeria.cliente.presentacion;

import java.util.List;
import java.util.concurrent.Executor;
import java.util.function.BiConsumer;
import java.util.function.Consumer;
import java.util.function.Supplier;
import javafx.beans.property.BooleanProperty;
import universidad.mensajeria.cliente.presentacion.comandos.EnviarMensajeComando;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;

/**
 * PAT-VM: encapsula la acción de enviar mensaje (texto + adjuntos + respuesta)
 * con su plan de envío y el comando asíncrono. Extraído de {@link ChatController}
 * para devolverlo a adaptador FXML.
 * <p>
 * La decisión de "puedo enviar" vive en {@link InterfazChatViewModel}; aquí solo
 * se orquesta el plan, la limpieza del adjunto y el comando.
 * </p>
 */
final class ChatEnvioAccion {

    private final InterfazChatViewModel viewModel;
    private final Executor tareasFondo;
    private final CargadorAdjuntoEntradaChat adjuntosEntrada;
    private final PaginadorHistorialChat paginador;
    private final Consumer<String> estado;
    private final BiConsumer<String, TipoAviso> mostrarAviso;
    private final Supplier<String> seleccionado;
    private final BooleanProperty enviando;
    private final Runnable pedirFocoEntrada;

    ChatEnvioAccion(InterfazChatViewModel viewModel,
                    Executor tareasFondo,
                    CargadorAdjuntoEntradaChat adjuntosEntrada,
                    PaginadorHistorialChat paginador,
                    Consumer<String> estado,
                    BiConsumer<String, TipoAviso> mostrarAviso,
                    Supplier<String> seleccionado,
                    BooleanProperty enviando,
                    Runnable pedirFocoEntrada) {
        this.viewModel = viewModel;
        this.tareasFondo = tareasFondo;
        this.adjuntosEntrada = adjuntosEntrada;
        this.paginador = paginador;
        this.estado = estado;
        this.mostrarAviso = mostrarAviso;
        this.seleccionado = seleccionado;
        this.enviando = enviando;
        this.pedirFocoEntrada = pedirFocoEntrada;
    }

    /**
     * Intenta enviar el mensaje actual. Devuelve {@code true} si el envío quedó
     * en curso (el llamador debe limpiar el campo de texto), {@code false} si no
     * se podía enviar.
     */
    boolean intentarEnviar(String textoCampo) {
        String destino = seleccionado.get();
        String texto = textoCampo == null ? "" : textoCampo.trim();
        List<AdjuntoPendiente> adjuntos = viewModel.adjuntos();

        if (!InterfazChatViewModel.puedeEnviar(texto, !adjuntos.isEmpty(),
                destino != null, enviando.get())) {
            return false;
        }

        List<PlanEnvioAdjuntos.Envio> plan = PlanEnvioAdjuntos.calcular(texto, adjuntos);
        if (plan.isEmpty()) {
            return false;
        }

        MensajeLocal respuesta = viewModel.respuestaSeleccionada();
        enviando.set(true);
        // Los pendientes y la cita eran de esta operación: se limpian ya, igual
        // que antes de la extracción (el campo de texto lo limpia el llamador).
        adjuntosEntrada.limpiar();
        viewModel.cancelarRespuesta();

        new EnviarMensajeComando(viewModel, tareasFondo, destino, plan, respuesta,
                this::alEnvioTerminado, this::alEnvioFallido).ejecutar();
        return true;
    }

    private void alEnvioTerminado(boolean enRed) {
        enviando.set(false);
        if (enRed) {
            estado.accept("Enviado");
            mostrarAviso.accept("Mensaje enviado.", TipoAviso.EXITO);
        } else {
            // Sin red no es un error: se queda en la cola y se reintenta solo.
            // Por eso es INFORMACION y no ERROR.
            estado.accept("Sin red: guardado para reintentar");
            mostrarAviso.accept("Sin red: quedó en la cola y se reintentará solo.",
                    TipoAviso.INFORMACION);
        }
        paginador.cargarConversacion();
        pedirFocoEntrada.run();
    }

    private void alEnvioFallido(String detalle) {
        enviando.set(false);
        estado.accept("No se completó el envío: " + detalle);
        mostrarAviso.accept("No se completó el envío: " + detalle, TipoAviso.ERROR);
        paginador.cargarConversacion();
        pedirFocoEntrada.run();
    }
}
