package universidad.mensajeria.cliente.presentacion;

import javafx.scene.control.ListCell;
import javafx.scene.control.ListView;
import universidad.mensajeria.cliente.presentacion.celdas.FilaChat;
import universidad.mensajeria.cliente.presentacion.celdas.MensajeCell;
import universidad.mensajeria.cliente.presentacion.celdas.UsuarioCell;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;
import universidad.mensajeria.cliente.transversal.records.RecienteChat;

import java.util.function.Consumer;
import java.util.function.Function;
import java.util.function.Supplier;

/**
 * Configura la fábrica de celdas de la lista de mensajes y la navegación de cita.
 * <p>
 * Extraído de {@link ChatController} para devolverlo a adaptador FXML. La navegación
 * de cita propiamente dicha vive en {@link NavegadorCitaChat}; aquí solo se cablea
 * su uso en la celda y su ciclo de vida (cancelar al cambiar de chat o al detener).
 * </p>
 */
final class ConfiguradorCeldasChat {

    private final ListView<FilaChat> listaMensajes;
    private final ListView<RecienteChat> listaUsuarios;
    private final NavegadorCitaChat navegadorCitas;
    private final InterfazGestorAdjuntos gestorAdjuntos;
    private final BusquedaHistorialChat busqueda;
    private final Function<String, String> nombreVisible;
    private final Consumer<MensajeLocal> alResponder;
    private final Supplier<String> seleccionado;

    ConfiguradorCeldasChat(ListView<FilaChat> listaMensajes,
                           ListView<RecienteChat> listaUsuarios,
                           NavegadorCitaChat navegadorCitas,
                           InterfazGestorAdjuntos gestorAdjuntos,
                           BusquedaHistorialChat busqueda,
                           Function<String, String> nombreVisible,
                           Consumer<MensajeLocal> alResponder,
                           Supplier<String> seleccionado) {
        this.listaMensajes = listaMensajes;
        this.listaUsuarios = listaUsuarios;
        this.navegadorCitas = navegadorCitas;
        this.gestorAdjuntos = gestorAdjuntos;
        this.busqueda = busqueda;
        this.nombreVisible = nombreVisible;
        this.alResponder = alResponder;
        this.seleccionado = seleccionado;
    }

    /** Configura ambas listas y la navegación de cita en la celda de mensaje. */
    void configurar() {
        listaUsuarios.setCellFactory(v -> new UsuarioCell(nombreVisible));
        listaMensajes.setCellFactory(v -> new MensajeCell(
                gestorAdjuntos::cargarImagenVisible,
                gestorAdjuntos::reintentarImagen,
                alResponder,
                nombreVisible,
                gestorAdjuntos::imagenFallida,
                gestorAdjuntos::descargarArchivo,
                busqueda::patronActual,
                (mensaje, cita) -> navegadorCitas.navegar(cita),
                gestorAdjuntos::imagenIlegible,
                gestorAdjuntos::originalDe));
        listaMensajes.widthProperty().addListener((o, a, b) -> listaMensajes.refresh());
        listaMensajes.setPlaceholder(placeholder("Sin mensajes aún. Escribe el primero."));
        listaUsuarios.setPlaceholder(placeholder("Sin contactos todavía."));
    }

    /** Cancela cualquier búsqueda de cita en curso (cambio de conversación). */
    void cancelarNavegacionCita() {
        if (navegadorCitas != null) {
            navegadorCitas.cancelar();
        }
    }

    /** Detiene completamente el configurador (cierre de la vista). */
    void detener() {
        cancelarNavegacionCita();
    }

    private static javafx.scene.control.Label placeholder(String texto) {
        javafx.scene.control.Label aviso = new javafx.scene.control.Label(texto);
        aviso.getStyleClass().add("empty-placeholder");
        return aviso;
    }
}