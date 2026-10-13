package universidad.mensajeria.cliente.presentacion;

import javafx.application.Platform;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.control.ProgressBar;
import javafx.scene.layout.HBox;

/**
 * §7 paridad: la fila de progreso del lote de adjuntos, con su botón de cancelar.
 * <p>
 * Espejo de la fila que monta Python ({@code fila_progreso} en
 * {@code constructor_vista_chat.py}) y de {@code panelProgresoEnvio} en WPF:
 * texto {@code "Enviando i/n · X,0/Y,0 MB · nombre"}, barra por bytes y un
 * "Cancelar envío" que solo se habilita mientras queda algo por mandar.
 * </p>
 * <p>
 * Como en los otros dos, <b>cancelar no deshace</b> lo ya enviado: el comando
 * deja de intentar los siguientes y la vista los devuelve a la bandeja.
 * </p>
 */
final class BarraProgresoEnvioChat {

    private final HBox fila;
    private final ProgressBar barra;
    private final Label etiqueta;
    private final Button botonCancelar;

    BarraProgresoEnvioChat(HBox fila, ProgressBar barra, Label etiqueta, Button botonCancelar) {
        this.fila = fila;
        this.barra = barra;
        this.etiqueta = etiqueta;
        this.botonCancelar = botonCancelar;
        ocultar();
    }

    /** Conecta el ✕/Cancelar. Se hace una vez, al montar la vista. */
    void configurar(Runnable alCancelar) {
        if (botonCancelar != null) {
            botonCancelar.setOnAction(evento -> alCancelar.run());
        }
    }

    /** Pinta el avance que llega del hilo de envío. */
    void mostrar(ProgresoEnvio avance) {
        Platform.runLater(() -> {
            if (fila == null) {
                return;
            }
            etiqueta.setText(avance.texto());
            barra.setProgress(avance.fraccionGlobal());
            // Sin barra que medir (lote solo de texto) la fila se apaga al acabar.
            visible(avance.indiceActual() < avance.totalArchivos()
                    && (avance.bytesTotales() > 0 || avance.indiceActual() > 0));
            botonCancelar.setDisable(false);
        });
    }

    /** Texto suelto: "Cancelando…" y demás estados del botón. */
    void mostrarTexto(String texto) {
        Platform.runLater(() -> {
            if (etiqueta == null) {
                return;
            }
            etiqueta.setText(texto);
            visible(true);
        });
    }

    /** Cierra la fila y rehabilita el botón para el siguiente lote. */
    void ocultar() {
        if (etiqueta != null) {
            etiqueta.setText("");
        }
        if (barra != null) {
            barra.setProgress(0d);
        }
        visible(false);
        if (botonCancelar != null) {
            botonCancelar.setDisable(false);
        }
    }

    private void visible(boolean valor) {
        if (fila != null) {
            fila.setVisible(valor);
            fila.setManaged(valor);
        }
    }
}