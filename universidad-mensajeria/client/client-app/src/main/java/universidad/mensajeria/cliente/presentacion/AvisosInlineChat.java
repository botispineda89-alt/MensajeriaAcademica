package universidad.mensajeria.cliente.presentacion;

import javafx.animation.KeyFrame;
import javafx.animation.Timeline;
import javafx.application.Platform;
import javafx.scene.control.Label;
import javafx.scene.layout.HBox;
import javafx.util.Duration;

import java.util.function.Consumer;

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
    /** Sega de anuncio: por defecto escribe el texto accesible del contenedor. */
    private final Consumer<String> anuncio;
    private Timeline reloj;

    AvisosInlineChat(HBox contenedor, Label titulo, Label texto) {
        this(contenedor, titulo, texto, null);
    }

    AvisosInlineChat(HBox contenedor, Label titulo, Label texto, Consumer<String> anuncio) {
        this.contenedor = contenedor;
        this.titulo = titulo;
        this.texto = texto;
        this.anuncio = anuncio;
    }

    /** Muestra un aviso que se quita solo. Llega del hilo de red: salta a JavaFX. */
    void mostrar(String mensaje, TipoAviso tipo) {
        Platform.runLater(() -> {
            if (contenedor == null) {
                return;
            }
            estado.mostrar(mensaje, tipo, System.currentTimeMillis());
            pintarSeveridad(tipo);
            String encabezado = tituloDe(tipo);
            titulo.setText(encabezado);
            texto.setText(estado.mensaje());
            contenedor.setVisible(true);
            contenedor.setManaged(true);
            anunciar(anuncioDe(encabezado, estado.mensaje()));
            arrancarReloj();
        });
    }

    /**
     * Anuncia el aviso a quien use un lector de pantalla.
     * <p>
     * JavaFX no tiene equivalente de {@code LiveSetting} de WPF: lo más cerca es
     * reescribir el texto accesible del contenedor. Y hay un detalle que solo se
     * ve al usarlo: un lector de pantalla <b>no vuelve a leer lo mismo</b>, así
     * que el mismo error saltando dos veces seguidas (un reintento que vuelve a
     * fallar) se quedaría mudo. Por eso se limpia primero y se vuelve a poner en
     * el siguiente pulso; en Qt se hace el mismo truco.
     * </p>
     */
    private void anunciar(String anuncio) {
        escribirAccesible("");
        Platform.runLater(() -> escribirAccesible(anuncio));
    }

    private void escribirAccesible(String valor) {
        if (anuncio != null) {
            anuncio.accept(valor);
        } else if (contenedor != null) {
            contenedor.setAccessibleText(valor);
        }
    }

    /** Lo que se lee en voz alta: el título da el tono sin depender del color. */
    static String anuncioDe(String titulo, String mensaje) {
        return titulo + ": " + mensaje;
    }

    /** Quita el aviso ya: por aquí pasan el ✕ y el plazo vencido. */
    void limpiar() {
        estado.limpiar();
        pararReloj();
        if (contenedor != null) {
            contenedor.setVisible(false);
            contenedor.setManaged(false);
        }
        escribirAccesible("");
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

    /**
     * T2: el color del aviso depende de su severidad, no del tipo de aviso anterior.
     * <p>
     * Antes el contenedor reutilizaba siempre {@code offline-banner} (ámbar), de modo que un
     * error se pintaba igual que una advertencia. Las cuatro clases salen de las plantillas
     * del contrato ({@code contrato/plantillas/chat.css.in}), así que los 3 clientes pintan
     * la misma severidad del mismo color.
     * </p>
     */
    private void pintarSeveridad(TipoAviso tipo) {
        if (contenedor != null) {
            contenedor.getStyleClass().setAll("aviso-inline", claseDe(tipo));
        }
    }

    /** Clase CSS de la severidad; la usa también su test. */
    static String claseDe(TipoAviso tipo) {
        return switch (tipo) {
            case ERROR -> "aviso-inline-error";
            case ADVERTENCIA -> "aviso-inline-atencion";
            case EXITO -> "aviso-inline-listo";
            case INFORMACION -> "aviso-inline-aviso";
        };
    }
}
