package universidad.mensajeria.cliente.presentacion;

import javafx.application.Platform;
import javafx.scene.control.ComboBox;
import javafx.scene.control.SplitPane;
import javafx.scene.input.KeyCode;
import javafx.scene.input.KeyCodeCombination;
import javafx.scene.input.KeyCombination;
import javafx.util.Duration;

import java.util.function.Supplier;
import java.util.prefs.Preferences;

/**
 * Ajustes de la ventana que no son negocio: tema, atajo de búsqueda y ancho del
 * divisor.
 * <p>
 * Tres detalles de una vez porque los tres son "lo mismo que la ventana recuerda
 * entre sesiones" y ninguno pertenece al flujo de un mensaje. El ancho del
 * divisor se guarda por cuenta, igual que en Python y .NET: cada usuario tiene
 * su sidebar y compartirlo sería molestar a la mitad.
 * </p>
 */
final class ConfiguradorVistaChat {

    private final ComboBox<String> comboTema;
    private final SplitPane divisorPrincipal;
    private final InterfazNavegacionCliente navegacion;
    private final javafx.scene.Node ancla;
    private final Supplier<String> codigoActual;
    private final Runnable mostrarBusqueda;
    private final Runnable deseleccionar;

    ConfiguradorVistaChat(ComboBox<String> comboTema, SplitPane divisorPrincipal,
                          InterfazNavegacionCliente navegacion, javafx.scene.Node ancla,
                          Supplier<String> codigoActual, Runnable mostrarBusqueda,
                          Runnable deseleccionar) {
        this.comboTema = comboTema;
        this.divisorPrincipal = divisorPrincipal;
        this.navegacion = navegacion;
        this.ancla = ancla;
        this.codigoActual = codigoActual;
        this.mostrarBusqueda = mostrarBusqueda;
        this.deseleccionar = deseleccionar;
    }

    void configurar() {
        configurarTema();
        configurarAtajoBusqueda();
        configurarEscape();
        configurarDivisor();
    }

    /**
     * T1 (2026-10-14): Esc deja la ventana sin conversación.
     * <p>
     * Va como manejador de la <b>escena</b> y no como acelerador a propósito: el evento
     * sube desde el nodo con el foco, así que si el compositor tenía una cita o un adjunto
     * que cancelar (o la búsqueda estaba abierta) ya lo consumió y aquí no se llega. El
     * orden que se quiere es ese: primero se cancela, después se deselecciona.
     * </p>
     */
    private void configurarEscape() {
        if (ancla == null || deseleccionar == null) {
            return;
        }
        ancla.sceneProperty().addListener((o, anterior, escena) -> {
            if (escena != null) {
                escena.setOnKeyPressed(evento -> {
                    if (evento.getCode() == KeyCode.ESCAPE) {
                        deseleccionar.run();
                    }
                });
            }
        });
    }

    private void configurarTema() {
        comboTema.setItems(javafx.collections.FXCollections
                .observableArrayList("Oscuro", "Claro"));
        comboTema.getSelectionModel().select(
                Tema.CLARO.equals(Tema.cargar()) ? "Claro" : "Oscuro");
        comboTema.getSelectionModel().selectedItemProperty().addListener((o, a, b) -> {
            Tema.guardar("Claro".equals(b) ? Tema.CLARO : Tema.OSCURO);
            if (ancla != null && ancla.getScene() != null) {
                navegacion.aplicarTema(ancla.getScene(), false);
            }
        });
    }

    private void configurarAtajoBusqueda() {
        if (ancla == null) {
            return;
        }
        ancla.sceneProperty().addListener((o, anterior, escena) -> {
            if (escena != null) {
                escena.getAccelerators().put(
                        new KeyCodeCombination(KeyCode.F, KeyCombination.CONTROL_DOWN),
                        mostrarBusqueda::run);
            }
        });
    }

    private void configurarDivisor() {
        if (divisorPrincipal == null) {
            return;
        }
        String codigo = codigoActual.get();
        PreferenciaDivisorChat preferencia = new PreferenciaDivisorChat(
                Preferences.userNodeForPackage(ConfiguradorVistaChat.class));
        Platform.runLater(() -> {
            divisorPrincipal.setDividerPositions(preferencia.leer(codigo));
            divisorPrincipal.getDividers().get(0).positionProperty().addListener(
                    (observable, anterior, nueva) ->
                            preferencia.guardar(codigo, nueva.doubleValue()));
        });
    }
}