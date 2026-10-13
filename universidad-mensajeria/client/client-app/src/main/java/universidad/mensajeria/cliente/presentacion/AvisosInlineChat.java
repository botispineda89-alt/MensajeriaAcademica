package universidad.mensajeria.cliente.presentacion;

import javafx.animation.KeyFrame;
import javafx.animation.Timeline;
import javafx.application.Platform;
import javafx.scene.control.Label;
import javafx.scene.layout.HBox;
import javafx.util.Duration;

/**
 * UIX-26/PAT-VM: cableado JavaFX del aviso inline del chat — pintar, cerrar y el
 * reloj que lo retira solo.
 * <p>
 * La decisión (plazos y cuándo ocultar) vive en {@link EstadoAvisoInline}, que es
 * Java puro y por tanto testeable; aquí solo se mueve el reloj y se tocan los
 * nodos. Se extrajo de {@link ChatController} para devolverlo a adaptador FXML.
 * </p>
 * <p>
 * Se usa un {@link Timeline} y no un {@code Thread.sleep}: vive en el hilo de
 * JavaFX, para solo con la ventana y no puede intentar tocar un control ya
 * liberado.
 * </p>
 */
final class AvisosInlineChat {

    private final HBox contenedor;
    private final Label titulo;
    private final Label texto;
    private final EstadoAvisoInline estado = new EstadoAvisoInline();
    private Timeline reloj;

    AvisosInlineChat(HBox contenedor, Label titulo, Label texto) {
        this.contenedor = contenedor;
        this.titulo = titulo;
        this.texto = texto;
    }

    /** Muestra un aviso que se quita solo. Llega del hilo de red: salta a JavaFX. */
    void mostrar(String mensaje, TipoAviso tipo) {
        Platform.runLater(() -> {
            if (contenedor == null) {
                return;
            }
            estado.mostrar(mensaje, tipo, System.currentTimeMillis());
            titulo.setText(tituloDe(tipo));
            texto.setText(estado.mensaje());
            contenedor.setVisible(true);
            contenedor.setManaged(true);
            arrancarReloj();
        });
    }

    /** Quita el aviso ya: por aquí pasan el ✕ y el plazo vencido. */
    void limpiar() {
        estado.limpiar();
        pararReloj();
        if (contenedor != null) {
            contenedor.setVisible(false);
            contenedor.setManaged(false);
        }
    }

    private void arrancarReloj() {
        pararReloj();
        if (!estado.debeOcultarseAutomaticamente()) {
            return;
        }
        reloj = new Timeline(new KeyFrame(Duration.millis(estado.duracionMs()), e -> limpiar()));
        reloj.play();
    }

    private void pararReloj() {
        if (reloj != null) {
            reloj.stop();
            reloj = null;
        }
    }

    private static String tituloDe(TipoAviso tipo) {
        return switch (tipo) {
            case ERROR -> "Error";
            case ADVERTENCIA -> "Atención";
            case EXITO -> "Listo";
            case INFORMACION -> "Aviso";
        };
    }
}
