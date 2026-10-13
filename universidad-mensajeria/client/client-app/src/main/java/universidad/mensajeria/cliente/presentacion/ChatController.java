package universidad.mensajeria.cliente.presentacion;

import java.util.List;
import java.util.concurrent.Executor;
import java.util.function.Consumer;
import javafx.fxml.FXML;
import javafx.scene.control.Button;
import javafx.scene.control.ComboBox;
import javafx.scene.control.Label;
import javafx.scene.control.ListView;
import javafx.scene.control.ProgressBar;
import javafx.scene.control.SplitPane;
import javafx.scene.control.TextArea;
import javafx.scene.control.TextField;
import javafx.scene.input.KeyEvent;
import universidad.mensajeria.cliente.presentacion.celdas.FilaChat;
import universidad.mensajeria.cliente.transversal.EstadoConexion;
import universidad.mensajeria.cliente.transversal.records.EventoCliente;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;
import universidad.mensajeria.cliente.transversal.records.RecienteChat;

/**
 * Adaptador FXML del chat: expone los nodos de {@code chat.fxml} y delega.
 * <p>
 * No decide nada. Las reglas están cada una en su clase
 * ({@link PaginadorHistorialChat}, {@link RefrescadorContactosChat},
 * {@link NavegadorCitaChat}, {@link PresentadorEstadoConexionChat},
 * {@link ReceptorEventosChat}, {@link CompositorChat}…) y el grafo se monta en
 * {@link ChatComponentes}, el composition root del chat. Por eso este archivo se
 * mantiene por debajo de las 350 líneas: si crece, lo que falta es una
 * extracción, no un método más aquí.
 * </p>
 * <p>
 * Las dos suscripciones ({@code alEntrar} y {@code alCambiarEstado}) son
 * <b>instancias estables</b>, no lambdas creadas al vuelo: hay que poder quitar
 * exactamente el mismo observer que se puso al abrir la vista.
 * </p>
 */
public final class ChatController implements FlujoConversacion {

    @FXML private Label etiquetaSesion;
    @FXML private Label etiquetaChat;
    @FXML private Label etiquetaPresencia;
    @FXML private Label etiquetaEstado;
    @FXML private Label etiquetaRespuesta;
    @FXML private Label etiquetaAdjunto;
    @FXML private Label bannerOffline;
    @FXML private Label etiquetaBuscarContador;
    /** UIX-26: aviso transitorio con título, texto y ✕ (paridad con .NET). */
    @FXML private Label avisoInlineTitulo;
    @FXML private Label avisoInlineTexto;
    /** §7 paridad: avance del lote de adjuntos y su cancelación. */
    @FXML private Label etiquetaProgreso;
    @FXML private javafx.scene.layout.HBox barraRespuesta;
    @FXML private javafx.scene.layout.HBox barraAdjunto;
    @FXML private javafx.scene.layout.HBox barraBuscar;
    @FXML private javafx.scene.layout.HBox avisoInline;
    @FXML private javafx.scene.layout.HBox barraProgresoEnvio;
    @FXML private ComboBox<String> comboFiltro;
    @FXML private ComboBox<String> comboTema;
    @FXML private TextField campoBuscar;
    @FXML private TextField campoBuscarHistorial;
    @FXML private TextArea campoEntrada;
    @FXML private ListView<RecienteChat> listaUsuarios;
    @FXML private ListView<FilaChat> listaMensajes;
    @FXML private SplitPane divisorPrincipal;
    @FXML private javafx.scene.layout.FlowPane listaAdjuntos;
    @FXML private Button botonMas;
    @FXML private Button botonNuevos;
    @FXML private Button botonEnviar;
    @FXML private Button botonCancelarEnvio;
    @FXML private ProgressBar barraProgreso;
    @FXML private javafx.scene.control.ProgressIndicator ruedaHistorial;

    private final InterfazNavegacionCliente navegacion;
    private final InterfazFabricaAdjuntos fabricaAdjuntos;
    private final InterfazChatViewModel viewModel;
    /** PAT-03: la vista se suscribe al estado; antes lo deducía sondeando. */
    private final Consumer<EventoCliente> alEntrar = this::alMensajeEntrante;
    private final Consumer<EstadoConexion> alCambiarEstado = this::pintarEstadoConexion;
    private volatile boolean controladorDetenido;
    private ChatComponentes componentes;

    public ChatController(InterfazChatViewModel viewModel, InterfazNavegacionCliente navegacion) {
        this(viewModel, navegacion, ChatController::fabricaAdjuntosPorDefecto);
    }

    public ChatController(InterfazChatViewModel viewModel, InterfazNavegacionCliente navegacion,
                          InterfazFabricaAdjuntos fabricaAdjuntos) {
        this.viewModel = viewModel;
        this.navegacion = navegacion;
        this.fabricaAdjuntos = fabricaAdjuntos != null
                ? fabricaAdjuntos : ChatController::fabricaAdjuntosPorDefecto;
    }

    /** Compatibilidad con la fábrica: el composition root crea el gestor real. */
    private static InterfazGestorAdjuntos fabricaAdjuntosPorDefecto(
            InterfazChatViewModel viewModel, Executor fondo,
            ListView<FilaChat> lista,
            java.util.function.Supplier<String> conversacion,
            java.util.function.Consumer<String> estado) {
        return new GestorAdjuntosChat(viewModel, fondo, lista, conversacion, estado);
    }

    public InterfazChatViewModel viewModel() {
        return viewModel;
    }

    @FXML
    private void initialize() {
        // La referencia se guarda antes de cablear: los colaboradores llaman de
        // vuelta (estado, foco, cierre) durante el propio montaje.
        componentes = ChatComponentes.crear(viewModel, puentes());
        componentes.configurar(nodos(), navegacion, fabricaAdjuntos, this);
        viewModel.desuscribirEventos(alEntrar);
        viewModel.suscribirEventos(alEntrar);
        viewModel.alCambiarEstado(alCambiarEstado);
    }

    private NodosChat nodos() {
        return new NodosChat(etiquetaSesion, etiquetaChat, etiquetaPresencia, etiquetaEstado,
                etiquetaRespuesta, etiquetaAdjunto, bannerOffline, etiquetaBuscarContador,
                avisoInlineTitulo, avisoInlineTexto, etiquetaProgreso, barraRespuesta,
                barraAdjunto, barraBuscar, avisoInline, barraProgresoEnvio, comboFiltro,
                comboTema, campoBuscar, campoBuscarHistorial, campoEntrada, listaUsuarios,
                listaMensajes, divisorPrincipal, listaAdjuntos, botonMas, botonNuevos,
                botonEnviar, botonCancelarEnvio, barraProgreso, ruedaHistorial);
    }

    private ChatComponentes.Puentes puentes() {
        return new ChatComponentes.Puentes(this::seleccionado, this::nombreVisible,
                this::estado, alEntrar, alCambiarEstado, () -> controladorDetenido,
                this::manejarCierreSesion);
    }

    /** FlujoConversacion: la selección prepara estado y carga mensajes. */
    @Override
    public void alSeleccionar(RecienteChat actual) {
        if (componentes != null) {
            componentes.alSeleccionar(actual);
        }
    }

    /** FlujoConversacion: la página recién cargada va a la lista de mensajes. */
    @Override
    public void pintar(String conversacion, List<MensajeLocal> pagina, int total, int offset) {
        componentes.pintarPagina(conversacion, pagina, total, offset);
    }

    public void detener() {
        controladorDetenido = true;
        if (componentes != null) {
            componentes.detener();
        }
    }

    public static boolean puedeEnviar(String texto, boolean imagen, boolean hayDestino,
                                       boolean enviando) {
        return InterfazChatViewModel.puedeEnviar(texto, imagen, hayDestino, enviando);
    }

    private String seleccionado() {
        RecienteChat r = listaUsuarios.getSelectionModel().getSelectedItem();
        return r == null ? null : r.otro();
    }

    public String nombreVisible(String codigo) {
        return viewModel.nombreVisible(codigo);
    }

    @FXML private void alEnviar() { componentes.alEnviar(); }

    /** §7 paridad: el botón del lote cancela; lo no enviado vuelve a la bandeja. */
    @FXML private void alCancelarEnvio() { componentes.alCancelarEnvio(); }

    @FXML private void alTeclaEntrada(KeyEvent evento) { componentes.alTeclaEntrada(evento); }

    @FXML private void alElegirImagen() { componentes.alElegirImagen(); }

    @FXML private void alQuitarImagen() { componentes.alQuitarImagen(); }

    @FXML private void alCancelarRespuesta() { componentes.alCancelarRespuesta(); }

    /** Actualización manual del directorio (botón ↻): delega por SRP. */
    @FXML private void alActualizar() { componentes.alActualizar(); }

    @FXML private void alCargarMas() { componentes.alCargarMas(); }

    /** UIX-16: baja a lo nuevo sin perder el punto de lectura anterior. */
    @FXML private void alIrANuevos() { componentes.alIrANuevos(); }

    @FXML private void alBuscarSiguiente() { componentes.alBuscarSiguiente(); }

    @FXML private void alBuscarAnterior() { componentes.alBuscarAnterior(); }

    @FXML private void alCerrarBuscar() { componentes.alCerrarBuscar(); }

    @FXML private void alKick() { componentes.alKick(); }

    @FXML private void alSalir() { componentes.alSalir(); }

    @FXML private void alSalirTodos() { componentes.alSalirTodos(); }

    void salirAlInicio() { componentes.salirAlInicio(); }

    /** UIX-26: aviso que se quita solo; el plazo lo decide EstadoAvisoInline. */
    void mostrarAvisoInline(String texto, TipoAviso tipo) {
        componentes.mostrarAviso(texto, tipo);
    }

    @FXML void alCerrarAvisoInline() { componentes.cerrarAviso(); }

    private void alMensajeEntrante(EventoCliente evento) { componentes.recibir(evento); }

    private void pintarEstadoConexion(EstadoConexion estado) { componentes.pintarEstado(estado); }

    private void manejarCierreSesion() { componentes.alCerrarSesion(); }

    private void estado(String texto) { componentes.escribirEstado(texto); }
}