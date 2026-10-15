package universidad.mensajeria.cliente.presentacion;

import javafx.application.Platform;
import java.util.function.Consumer;
import java.util.function.Function;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;
import universidad.mensajeria.cliente.transversal.utilerias.FormatoRespuesta;
import universidad.mensajeria.cliente.transversal.utilerias.TextoCliente;

/**
 * PAT-VM: encapsula el manejo de la respuesta/cita a un mensaje.
 * Extraído de {@link ChatController}: compone la cita visible, la publica en la
 * etiqueta y devuelve el foco al compositor.
 */
final class ChatRespuestaAccion {

    private final InterfazChatViewModel viewModel;
    private final Function<String, String> nombreVisible;
    private final Consumer<String> actualizarEtiquetaRespuesta;
    private final Consumer<MensajeLocal> resaltarRespuesta;
    private final Runnable pedirFocoEntrada;

    ChatRespuestaAccion(InterfazChatViewModel viewModel,
                        Function<String, String> nombreVisible,
                        Consumer<String> actualizarEtiquetaRespuesta,
                        Consumer<MensajeLocal> resaltarRespuesta,
                        Runnable pedirFocoEntrada) {
        this.viewModel = viewModel;
        this.nombreVisible = nombreVisible;
        this.actualizarEtiquetaRespuesta = actualizarEtiquetaRespuesta;
        this.resaltarRespuesta = resaltarRespuesta;
        this.pedirFocoEntrada = pedirFocoEntrada;
    }

    /** Inicia una respuesta al mensaje dado y muestra la cita en la barra. */
    void responderA(MensajeLocal mensaje) {
        viewModel.seleccionarRespuesta(mensaje);
        resaltarRespuesta.accept(mensaje);
        String quote = TextoCliente.truncarCita(
                FormatoRespuesta.prefijo(mensaje, nombreVisible.apply(mensaje.origen())));
        actualizarEtiquetaRespuesta.accept(quote);
        Platform.runLater(pedirFocoEntrada);
    }

    /** Cancela la respuesta en curso. */
    void cancelar() {
        viewModel.cancelarRespuesta();
    }
}
