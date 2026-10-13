package universidad.mensajeria.cliente.presentacion;

import javafx.scene.control.Button;
import javafx.scene.control.ComboBox;
import javafx.scene.control.Label;
import javafx.scene.control.ListView;
import javafx.scene.control.ProgressBar;
import javafx.scene.control.SplitPane;
import javafx.scene.control.TextArea;
import javafx.scene.control.TextField;
import javafx.scene.layout.FlowPane;
import javafx.scene.layout.HBox;
import universidad.mensajeria.cliente.presentacion.celdas.FilaChat;
import universidad.mensajeria.cliente.transversal.records.RecienteChat;

/**
 * Los nodos que {@code chat.fxml} inyectó en el controlador.
 * <p>
 * Un record y no una lista de 31 parámetros en el constructor de
 * {@link ChatComponentes}: el grafo del chat se monta una vez, pero que esa montaje
 * viva en un archivo aparte es justamente lo que deja
 * {@link ChatController} como puro adaptador FXML. El record no añade indirección
 * —no hay comportamiento dentro— solo evita repetir la lista de nodos en cada
 * sitio que la necesita.
 * </p>
 */
record NodosChat(
        Label etiquetaSesion,
        Label etiquetaChat,
        Label etiquetaPresencia,
        Label etiquetaEstado,
        Label etiquetaRespuesta,
        Label etiquetaAdjunto,
        Label bannerOffline,
        Label etiquetaBuscarContador,
        Label avisoInlineTitulo,
        Label avisoInlineTexto,
        Label etiquetaProgreso,
        HBox barraRespuesta,
        HBox barraAdjunto,
        HBox barraBuscar,
        HBox avisoInline,
        HBox barraProgresoEnvio,
        ComboBox<String> comboFiltro,
        ComboBox<String> comboTema,
        TextField campoBuscar,
        TextField campoBuscarHistorial,
        TextArea campoEntrada,
        ListView<RecienteChat> listaUsuarios,
        ListView<FilaChat> listaMensajes,
        SplitPane divisorPrincipal,
        FlowPane listaAdjuntos,
        Button botonMas,
        Button botonNuevos,
        Button botonEnviar,
        Button botonCancelarEnvio,
        ProgressBar barraProgreso,
        javafx.scene.control.ProgressIndicator ruedaHistorial) {
}