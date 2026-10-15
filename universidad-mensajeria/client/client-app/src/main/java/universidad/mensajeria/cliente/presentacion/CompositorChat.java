package universidad.mensajeria.cliente.presentacion;

import javafx.application.Platform;
import javafx.beans.binding.Bindings;
import javafx.beans.property.BooleanProperty;
import javafx.beans.property.ObjectProperty;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.control.TextArea;
import javafx.scene.input.KeyCode;
import javafx.scene.input.KeyEvent;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;

import java.util.function.Consumer;

/**
 * El compositor: campo de escritura, botón de enviar, respuesta citada y barra de
 * adjuntos, con sus bindings y sus atajos de teclado.
 * <p>
 * Sale de {@link ChatController} porque es un bloque autónomo de la vista con su
 * propia lógica (habilitar el botón, consumir la tecla, devolver el foco) que no
 * depende del chat ni de la red. El controlador se queda con lo que de verdad
 * coordina: qué enviar y a quién.
 * </p>
 */
final class CompositorChat {

    private final TextArea campoEntrada;
    private final Button botonEnviar;
    private final Label etiquetaRespuesta;
    private final javafx.scene.layout.HBox barraRespuesta;
    private final javafx.scene.layout.HBox barraAdjunto;
    private final Label etiquetaAdjunto;
    private final BooleanProperty enviando;
    private final BooleanProperty adjuntoDisponible;
    private final BooleanProperty hayDestino;
    private final ObjectProperty<MensajeLocal> respuestaSeleccionada;
    private final ChatEnvioAccion envio;
    private final ChatRespuestaAccion respuesta;
    private final CargadorAdjuntoEntradaChat adjuntosEntrada;
    private final Consumer<MensajeLocal> alResponderCelda;

    CompositorChat(TextArea campoEntrada, Button botonEnviar, Label etiquetaRespuesta,
                   javafx.scene.layout.HBox barraRespuesta,
                   javafx.scene.layout.HBox barraAdjunto, Label etiquetaAdjunto,
                   BooleanProperty enviando, BooleanProperty adjuntoDisponible,
                   BooleanProperty hayDestino, ObjectProperty<MensajeLocal> respuestaSeleccionada,
                   ChatEnvioAccion envio, ChatRespuestaAccion respuesta,
                   CargadorAdjuntoEntradaChat adjuntosEntrada,
                   Consumer<MensajeLocal> alResponderCelda) {
        this.campoEntrada = campoEntrada;
        this.botonEnviar = botonEnviar;
        this.etiquetaRespuesta = etiquetaRespuesta;
        this.barraRespuesta = barraRespuesta;
        this.barraAdjunto = barraAdjunto;
        this.etiquetaAdjunto = etiquetaAdjunto;
        this.enviando = enviando;
        this.adjuntoDisponible = adjuntoDisponible;
        this.hayDestino = hayDestino;
        this.respuestaSeleccionada = respuestaSeleccionada;
        this.envio = envio;
        this.respuesta = respuesta;
        this.adjuntosEntrada = adjuntosEntrada;
        this.alResponderCelda = alResponderCelda;
    }

    /**
     * Botón, barra de respuesta y barra de adjuntos: los tres dependen del estado
     * del ViewModel, y la tira de miniaturas no (su contenido depende de cuántos
     * adjuntos haya, así que la pinta {@link CargadorAdjuntoEntradaChat}).
     */
    void configurarBindings() {
        botonEnviar.disableProperty().bind(Bindings.createBooleanBinding(
                () -> !ChatController.puedeEnviar(campoEntrada.getText(), adjuntoDisponible.get(),
                        hayDestino.get(), enviando.get()),
                campoEntrada.textProperty(), enviando, adjuntoDisponible, hayDestino));
        barraRespuesta.visibleProperty().bind(respuestaSeleccionada.isNotNull());
        barraRespuesta.managedProperty().bind(barraRespuesta.visibleProperty());
        barraAdjunto.visibleProperty().bind(adjuntoDisponible);
        barraAdjunto.managedProperty().bind(barraAdjunto.visibleProperty());
        etiquetaRespuesta.visibleProperty().bind(respuestaSeleccionada.isNotNull());
        etiquetaAdjunto.visibleProperty().bind(adjuntoDisponible);
        etiquetaAdjunto.managedProperty().bind(etiquetaAdjunto.visibleProperty());
    }

    /** Enter envía; Shift+Enter inserta el salto de línea del {@link TextArea}. */
    void alTecla(KeyEvent evento) {
        if (evento.getCode() == KeyCode.ESCAPE) {
            // T1 (2026-10-14): Esc cancela lo que haya en el compositor y solo entonces se
            // come la tecla. Antes la consumía siempre, así que la escena —que es quien
            // deselecciona la conversación— nunca la veía cuando se escribía en el campo.
            boolean habiaCita = respuestaSeleccionada.get() != null;
            boolean habiaAdjunto = adjuntoDisponible.get();
            if (habiaCita) {
                respuesta.cancelar();
            }
            if (habiaAdjunto) {
                adjuntosEntrada.limpiar();
            }
            if (habiaCita || habiaAdjunto) {
                evento.consume();
            }
            return;
        }
        // R2: Shift+Enter NO entra en esta rama y su evento no se consume: eso es
        // lo que deja que el TextArea inserte su "\n". El "doble envío" del
        // defaultButton del botón no es un bug: alEnviar pone `enviando` de forma
        // síncrona y la segunda llamada es un no-op.
        if (TecladoCompositor.debeEnviar(evento.getCode(), evento.isShiftDown(),
                evento.isControlDown(), evento.isAltDown())
                && botonEnviar != null && !botonEnviar.isDisabled()) {
            evento.consume();
            alEnviar();
        }
    }

    void alEnviar() {
        if (envio != null && envio.intentarEnviar(campoEntrada.getText())) {
            campoEntrada.clear();
        }
    }

    void alElegirImagen() {
        adjuntosEntrada.elegir();
        pedirFocoEntrada();
    }

    void alQuitarImagen() {
        adjuntosEntrada.limpiar();
        pedirFocoEntrada();
    }

    void alResponder(MensajeLocal mensaje) {
        alResponderCelda.accept(mensaje);
    }

    void alCancelarRespuesta() {
        respuesta.cancelar();
    }

    void alCancelarEnvio() {
        if (envio != null) {
            envio.cancelar();
        }
    }

    void mostrarCita(String quote) {
        etiquetaRespuesta.setText(quote);
    }

    /**
     * Tras enviar (texto, imagen sola o con pie) o quitar el adjunto, el cursor
     * vuelve a la línea de escritura: no hay que clickear el campo para seguir.
     */
    void pedirFocoEntrada() {
        Platform.runLater(() -> {
            if (campoEntrada != null) {
                campoEntrada.requestFocus();
            }
        });
    }
}