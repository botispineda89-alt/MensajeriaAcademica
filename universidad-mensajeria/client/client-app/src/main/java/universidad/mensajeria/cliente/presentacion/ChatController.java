package universidad.mensajeria.cliente.presentacion;

import java.util.List;
import java.util.Map;
import java.util.concurrent.Executor;
import java.util.function.Consumer;
import java.util.prefs.Preferences;
import javafx.animation.KeyFrame;
import javafx.animation.Timeline;
import javafx.application.Platform;
import javafx.beans.binding.Bindings;
import javafx.beans.property.BooleanProperty;
import javafx.collections.FXCollections;
import javafx.collections.ObservableList;
import javafx.fxml.FXML;
import javafx.scene.control.Button;
import javafx.scene.control.ComboBox;
import javafx.scene.control.Label;
import javafx.scene.control.ListView;
import javafx.scene.control.SplitPane;
import javafx.scene.control.TextArea;
import javafx.scene.control.TextField;
import javafx.scene.input.KeyCode;
import javafx.scene.input.KeyCodeCombination;
import javafx.scene.input.KeyCombination;
import javafx.scene.input.KeyEvent;
import javafx.util.Duration;
import universidad.mensajeria.cliente.presentacion.celdas.MensajeCell;
import universidad.mensajeria.cliente.presentacion.celdas.UsuarioCell;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;
import universidad.mensajeria.cliente.transversal.records.RecienteChat;
import universidad.mensajeria.cliente.transversal.EstadoConexion;
import universidad.mensajeria.cliente.presentacion.celdas.FilaChat;
import universidad.mensajeria.cliente.transversal.records.UsuarioLocal;
import universidad.mensajeria.cliente.transversal.records.EventoCliente;

/** Presentación JavaFX: no accede directamente a red ni a persistencia. */
public final class ChatController implements FlujoConversacion {

    private static final int LIMITE_PAGINA = 50;
    private static final int RESPALDO_SEGUNDOS = 30;
    private static final long DEBOUNCE_MS = 300;

    @FXML private Label etiquetaSesion;
    @FXML private Label etiquetaChat;
    @FXML private Label etiquetaPresencia;
    @FXML private Label etiquetaEstado;
    @FXML private Label etiquetaRespuesta;
    @FXML private Label etiquetaAdjunto;
    @FXML private javafx.scene.layout.HBox barraRespuesta;
    @FXML private javafx.scene.layout.HBox barraAdjunto;
    @FXML private ComboBox<String> comboFiltro;
    @FXML private ComboBox<String> comboTema;
    @FXML private TextField campoBuscar;
    @FXML private javafx.scene.layout.HBox barraBuscar;
    @FXML private TextField campoBuscarHistorial;
    @FXML private Label etiquetaBuscarContador;
    @FXML private TextArea campoEntrada;
    @FXML private ListView<RecienteChat> listaUsuarios;
    @FXML private ListView<FilaChat> listaMensajes;
    @FXML private SplitPane divisorPrincipal;
    @FXML private javafx.scene.layout.FlowPane listaAdjuntos;
    @FXML private Button botonMas;
    @FXML private Button botonNuevos;
    @FXML private javafx.scene.control.ProgressIndicator ruedaHistorial;
    @FXML private Button botonEnviar;
    @FXML private Label bannerOffline;
    /** UIX-26: aviso transitorio con título, texto y ✕ (paridad con .NET). */
    @FXML private javafx.scene.layout.HBox avisoInline;
    @FXML private Label avisoInlineTitulo;
    @FXML private Label avisoInlineTexto;
    /** UIX-26/PAT-VM: el cableado del aviso vive en su propio colaborador. */
    private AvisosInlineChat avisos;

    private final Executor tareasFondo;
    private final InterfazNavegacionCliente navegacion;
    private final InterfazFabricaAdjuntos fabricaAdjuntos;
    private InterfazGestorAdjuntos gestorAdjuntos;
    private final BooleanProperty enviando;
    private final BooleanProperty adjuntoDisponible;
    private final BooleanProperty hayDestino;
    private final Map<String, UsuarioLocal> directorio;
    private final Consumer<EventoCliente> alEntrar = this::alMensajeEntrante;
    /** PAT-03: la vista se suscribe al estado; antes lo deducía sondeando. */
    private final Consumer<EstadoConexion> alCambiarEstado = this::pintarEstadoConexion;
    private final SaltoBusquedaPendiente salto = new SaltoBusquedaPendiente();
    private PaginadorHistorialChat paginador;
    private NavegadorCitaChat navegadorCitas;
    private ConfiguradorCeldasChat configuradorCeldas;
    private BusquedaHistorialChat busqueda;
    private RefrescadorContactosChat contactos;
    private GestorSalidaSesionChat salida;
    private CargadorAdjuntoEntradaChat adjuntosEntrada;
    private ChatEnvioAccion envioAccion;
    private ChatRespuestaAccion respuestaAccion;
    private volatile boolean controladorDetenido;
    private volatile EstadoConexion ultimoEstadoConexion = EstadoConexion.SIN_RED;
    private final ObservableList<RecienteChat> todos;

    private final InterfazChatViewModel viewModel;

    public ChatController(InterfazChatViewModel viewModel, InterfazNavegacionCliente navegacion) {
        this(viewModel, navegacion, ChatController::fabricaAdjuntosPorDefecto);
    }

    public ChatController(InterfazChatViewModel viewModel, InterfazNavegacionCliente navegacion,
                          InterfazFabricaAdjuntos fabricaAdjuntos) {
        this.viewModel = viewModel;
        this.tareasFondo = viewModel != null ? viewModel.tareasFondo() : null;
        this.navegacion = navegacion;
        this.fabricaAdjuntos = fabricaAdjuntos != null
                ? fabricaAdjuntos : ChatController::fabricaAdjuntosPorDefecto;
        this.enviando = viewModel.enviandoProperty();
        this.adjuntoDisponible = viewModel.adjuntoDisponibleProperty();
        this.hayDestino = viewModel.hayDestinoProperty();
        this.directorio = viewModel.directorio();
        this.todos = viewModel.recientes();
    }

    /** Compatibilidad con la fábrica: el composition root crea el gestor real. */
    private static InterfazGestorAdjuntos fabricaAdjuntosPorDefecto(
            InterfazChatViewModel viewModel, Executor fondo,
            javafx.scene.control.ListView<FilaChat> lista,
            java.util.function.Supplier<String> conversacion,
            java.util.function.Consumer<String> estado) {
        return new GestorAdjuntosChat(viewModel, fondo, lista, conversacion, estado);
    }

    public InterfazChatViewModel viewModel() {
        return viewModel;
    }

    @FXML
    private void initialize() {
        avisos = new AvisosInlineChat(avisoInline, avisoInlineTitulo, avisoInlineTexto);
        crearGestorAdjuntos();
        crearHistorial();
        crearContactos();
        configurarTema();
        configurarAtajos();
        configurarBindings();
        suscribirModelo();
        configurarDragDrop();
        configurarDivisorPrincipal();
        actualizarBannerOffline();
        contactos.configurar();

        // Colaboradores de acciones de UI (PAT-VM)
        envioAccion = new ChatEnvioAccion(viewModel, tareasFondo, adjuntosEntrada, paginador,
                this::estado, this::mostrarAvisoInline, this::seleccionado, enviando, this::pedirFocoEntrada);
        respuestaAccion = new ChatRespuestaAccion(viewModel, this::nombreVisible,
                this::actualizarEtiquetaRespuesta, this::pedirFocoEntrada);
    }

    private void actualizarEtiquetaRespuesta(String quote) {
        etiquetaRespuesta.setText(quote);
    }

    /** Conserva el ancho del sidebar aislado por cuenta, como los otros clientes. */
    private void configurarDivisorPrincipal() {
        if (divisorPrincipal == null) {
            return;
        }
        String codigo = viewModel.codigoActual();
        PreferenciaDivisorChat preferencia = new PreferenciaDivisorChat(
                Preferences.userNodeForPackage(ChatController.class));
        Platform.runLater(() -> {
            divisorPrincipal.setDividerPositions(preferencia.leer(codigo));
            divisorPrincipal.getDividers().get(0).positionProperty().addListener(
                    (observable, anterior, nueva) -> preferencia.guardar(codigo, nueva.doubleValue()));
        });
    }

    private void crearGestorAdjuntos() {
        gestorAdjuntos = fabricaAdjuntos.crear(viewModel, tareasFondo, listaMensajes,
                this::seleccionado, this::estado);
        adjuntosEntrada = new CargadorAdjuntoEntradaChat(listaAdjuntos, listaMensajes,
                etiquetaAdjunto, viewModel, tareasFondo, this::estado);
    }

    /** Contactos y refresco: el colaborador configura filtros, selección y respaldo. */
    private void crearContactos() {
        RefrescadorContactosChat.TimelineRespaldo respaldo =
                new RefrescadorContactosChat.TimelineRespaldo() {
                    private Timeline linea;

                    @Override
                    public void iniciar(Runnable refresco) {
                        linea = new Timeline(
                                new KeyFrame(Duration.seconds(RESPALDO_SEGUNDOS),
                                        e -> refresco.run()));
                        linea.setCycleCount(Timeline.INDEFINITE);
                        linea.play();
                    }

                    @Override
                    public void detener() {
                        if (linea != null) {
                            linea.stop();
                        }
                    }
                };
        contactos = new RefrescadorContactosChat(etiquetaSesion, etiquetaChat,
                etiquetaPresencia, comboFiltro, campoBuscar, listaUsuarios,
                Preferences.userNodeForPackage(ChatController.class), viewModel, tareasFondo,
                directorio, todos, hayDestino, this::seleccionado, this::nombreVisible,
                this::estado, this, DEBOUNCE_MS, LIMITE_PAGINA, respaldo);
    }

    /** Paginación y búsqueda del historial: colaboradores con su propio estado. */
    private void crearHistorial() {
        salida = new GestorSalidaSesionChat(viewModel, tareasFondo, navegacion,
                this::estado, this::detener, () -> controladorDetenido);
        busqueda = new BusquedaHistorialChat(listaMensajes, botonMas, barraBuscar,
                campoBuscarHistorial, etiquetaBuscarContador, viewModel, tareasFondo,
                this::seleccionado, this::estado, this::alCargarMas, salto, DEBOUNCE_MS);
        busqueda.configurar();
        paginador = new PaginadorHistorialChat(listaMensajes, botonMas, botonNuevos,
                ruedaHistorial, gestorAdjuntos, viewModel, tareasFondo, this::seleccionado,
                this::estado, salto, busqueda::saltarA, LIMITE_PAGINA);
        navegadorCitas = new NavegadorCitaChat(listaMensajes, viewModel, gestorAdjuntos,
                paginador, ruedaHistorial, tareasFondo, this::seleccionado,
                this::nombreVisible, this::estado);
        configuradorCeldas = new ConfiguradorCeldasChat(listaMensajes, listaUsuarios,
                navegadorCitas, gestorAdjuntos, busqueda, this::nombreVisible,
                this::alResponder, this::seleccionado);
        configuradorCeldas.configurar();
    }

    

    private void configurarTema() {
        comboTema.setItems(FXCollections.observableArrayList("Oscuro", "Claro"));
        comboTema.getSelectionModel().select(
                Tema.CLARO.equals(Tema.cargar()) ? "Claro" : "Oscuro");
        comboTema.getSelectionModel().selectedItemProperty().addListener((o, a, b) -> {
            Tema.guardar("Claro".equals(b) ? Tema.CLARO : Tema.OSCURO);
            if (listaMensajes.getScene() != null) {
                navegacion.aplicarTema(listaMensajes.getScene(), false);
            }
        });
    }

    private void configurarAtajos() {
        listaMensajes.sceneProperty().addListener((o, anterior, escena) -> {
            if (escena != null) {
                escena.getAccelerators().put(
                        new KeyCodeCombination(KeyCode.F, KeyCombination.CONTROL_DOWN),
                        busqueda::mostrar);
            }
        });
    }

    /** FlujoConversacion: la selección prepara estado y carga mensajes. */
    @Override
    public void alSeleccionar(RecienteChat actual) {
        if (configuradorCeldas != null) {
            configuradorCeldas.cancelarNavegacionCita();
        }
        // R3: los adjuntos pendientes eran del chat anterior. Sin esto, un archivo
        // elegido para Joan se enviaría a Sofia sin que nadie lo pidiera.
        adjuntosEntrada.limpiar();
        Preferences.userNodeForPackage(ChatController.class)
                .put("ultimoChat_" + viewModel.codigoActual(), actual.otro());
        viewModel.setOffsetActual(0);
        gestorAdjuntos.cambiarConversacion();
        busqueda.cerrar();
        etiquetaChat.setText(nombreVisible(actual.otro()));
        etiquetaPresencia.setText(actual.conectado() ? "en línea" : "desconectado");
        paginador.cargarConversacion();
        viewModel.marcarLeidosEnFondo(actual.otro());
    }

    /** FlujoConversacion: la página recién cargada va a la lista de mensajes. */
    @Override
    public void pintar(String conversacion, List<MensajeLocal> pagina, int total, int offset) {
        paginador.pintarPagina(conversacion, pagina, total, offset);
    }

    private void configurarBindings() {
        botonEnviar.disableProperty().bind(Bindings.createBooleanBinding(
                () -> !puedeEnviar(campoEntrada.getText(), adjuntoDisponible.get(),
                        hayDestino.get(), enviando.get()),
                campoEntrada.textProperty(), enviando, adjuntoDisponible, hayDestino));
        barraRespuesta.visibleProperty().bind(
                viewModel.respuestaSeleccionadaProperty().isNotNull());
        barraRespuesta.managedProperty().bind(barraRespuesta.visibleProperty());
        barraAdjunto.visibleProperty().bind(adjuntoDisponible);
        barraAdjunto.managedProperty().bind(barraAdjunto.visibleProperty());
        etiquetaRespuesta.visibleProperty().bind(
                viewModel.respuestaSeleccionadaProperty().isNotNull());
        etiquetaAdjunto.visibleProperty().bind(adjuntoDisponible);
        etiquetaAdjunto.managedProperty().bind(etiquetaAdjunto.visibleProperty());
        // R3: la tira de miniaturas se pinta/limpia desde CargadorAdjuntoEntradaChat,
        // no con un binding: su contenido depende de cuántos adjuntos haya.
    }

    private void suscribirModelo() {
        viewModel.desuscribirEventos(alEntrar);
        viewModel.suscribirEventos(alEntrar);
        viewModel.alCambiarEstado(alCambiarEstado);
    }

    public void detener() {
        controladorDetenido = true;
        if (configuradorCeldas != null) {
            configuradorCeldas.detener();
        }
        if (navegadorCitas != null) {
            navegadorCitas.cancelar();
        }
        contactos.detenerRespaldo();
        if (gestorAdjuntos != null) {
            gestorAdjuntos.detener();
        }
        viewModel.desuscribirEventos(alEntrar);
        viewModel.quitarObservadorEstado(alCambiarEstado);
        viewModel.detener();
    }

    public static boolean puedeEnviar(String texto, boolean imagen, boolean hayDestino, boolean enviando) {
        return InterfazChatViewModel.puedeEnviar(texto, imagen, hayDestino, enviando);
    }

    private String seleccionado() {
        RecienteChat r = listaUsuarios.getSelectionModel().getSelectedItem();
        return r == null ? null : r.otro();
    }

    public String nombreVisible(String codigo) {
        return viewModel.nombreVisible(codigo);
    }

    @FXML
    private void alEnviar() {
        if (envioAccion != null && envioAccion.intentarEnviar(campoEntrada.getText())) {
            campoEntrada.clear();
        }
    }

    @FXML
    private void alTeclaEntrada(KeyEvent evento) {
        if (evento.getCode() == KeyCode.ESCAPE) {
            if (respuestaAccion != null) {
                respuestaAccion.cancelar();
            }
            adjuntosEntrada.limpiar();
            evento.consume();
            return;
        }
        // R2: Enter envía. Shift+Enter NO entra en esta rama y su evento no se
        // consume: eso es lo que deja que el TextArea inserte su "\n".
        // El "doble envío" del defaultButton del botón no es un bug: alEnviar
        // pone `enviando` de forma síncrona y la segunda llamada es un no-op.
        if (TecladoCompositor.debeEnviar(evento.getCode(), evento.isShiftDown(),
                evento.isControlDown(), evento.isAltDown())
                && botonEnviar != null && !botonEnviar.isDisabled()) {
            evento.consume();
            alEnviar();
        }
    }

    private void alResponder(MensajeLocal mensaje) {
        if (respuestaAccion != null) {
            respuestaAccion.responderA(mensaje);
        }
    }

    @FXML private void alCancelarRespuesta() {
        if (respuestaAccion != null) {
            respuestaAccion.cancelar();
        }
    }

    @FXML
    private void alElegirImagen() {
        adjuntosEntrada.elegir();
        pedirFocoEntrada();
    }

    @FXML private void alQuitarImagen() {
        adjuntosEntrada.limpiar();
        pedirFocoEntrada();
    }

    /**
     * Tras enviar (texto, imagen sola o con pie) o quitar el adjunto, el cursor
     * vuelve a la línea de escritura: no hay que clickear el campo para seguir.
     */
    private void pedirFocoEntrada() {
        javafx.application.Platform.runLater(() -> {
            if (campoEntrada != null) {
                campoEntrada.requestFocus();
            }
        });
    }

    private void configurarDragDrop() {
        adjuntosEntrada.configurarArrastre();
    }

    /** Actualización manual del directorio (botón ↻): delega por SRP. */
    @FXML
    private void alActualizar() {
        contactos.actualizar();
    }

    @FXML private void alCargarMas() { paginador.cargarMas(); }

    /** UIX-16: baja a lo nuevo sin perder el punto de lectura anterior. */
    @FXML private void alIrANuevos() { paginador.irANuevos(); }

    @FXML private void alBuscarSiguiente() { busqueda.siguiente(); }

    @FXML private void alBuscarAnterior() { busqueda.anterior(); }

    @FXML private void alCerrarBuscar() { busqueda.cerrar(); }

    private void marcarLeidosAsync(String otro) { viewModel.marcarLeidosEnFondo(otro); }

    /**
     * Sync multi-PC al reconectar: el SYNC_LOGIN solo trae pendientes entrantes;
     * lo enviado desde otro PC mientras este estaba offline se iguala trayendo
     * la página remota 0 (upsert idempotente) y repintando la visible.
     */
    private void refrescarVisibleTrasReconexion() {
        String sel = seleccionado();
        if (sel == null || tareasFondo == null) {
            return;
        }
        javafx.concurrent.Task<Void> tarea = new javafx.concurrent.Task<>() {
            @Override
            protected Void call() throws Exception {
                try {
                    universidad.mensajeria.cliente.transversal.records.PaginaHistorialRemota
                            remota = viewModel.traerHistorial(sel, 0);
                    if (gestorAdjuntos != null && remota != null) {
                        gestorAdjuntos.registrarPaginaRemota(sel, 1, remota.totalPaginas());
                    }
                    viewModel.setOffsetActual(0);
                } catch (Exception e) {
                    estado("Historial no actualizado: " + e.getMessage());
                }
                return null;
            }
        };
        tarea.setOnSucceeded(e -> {
            if (!controladorDetenido && sel.equals(seleccionado())) {
                paginador.cargarConversacion();
            }
        });
        tareasFondo.execute(tarea);
    }

    private void alMensajeEntrante(EventoCliente evento) {
        if (evento.tipo() == EventoCliente.Tipo.PRESENCIA) {
            Platform.runLater(contactos::actualizarFondo);
            return;
        }
        Platform.runLater(() -> {
            if (evento.tipo() == EventoCliente.Tipo.SESION_CERRADA) {
                manejarCierreSesion();
                return;
            }
            String afectado = evento.remitente() != null
                    ? evento.remitente() : evento.destinatario();
            String sel = seleccionado();
            if (evento.esAcuse()) {
                contactos.actualizarFondoForzado();
            } else {
                contactos.actualizarFondo();
            }
            // UIX-16: si se lee historial viejo, lo nuevo no arrastra el scroll:
            // se avisa con el botón y se baja al pulsarlo.
            paginador.notificarEntrante(afectado, evento.esAcuse());
            if (sel != null && sel.equals(afectado) && !evento.esAcuse()) {
                marcarLeidosAsync(sel);
                // Sync multi-PC: con la conversación al día (offset 0) lo entrante
                // —incluido el eco de lo enviado desde otro PC— se pinta solo.
                paginador.recargarSiAlDia(sel);
            }
        });
    }

    @FXML
    private void alKick() { salida.expulsarOtras(); }

    @FXML private void alSalir() { salida.salir(); }

    @FXML
    private void alSalirTodos() { salida.salirEnTodas(); }

    void salirAlInicio() {
        salida.salirAlInicio();
    }

    private void manejarCierreSesion() {
        salida.manejarCierreSesion();
    }

    private void estado(String texto) {
        etiquetaEstado.setText(texto);
        actualizarBannerOffline();
    }

    private void actualizarBannerOffline() {
        if (bannerOffline == null || viewModel == null) {
            return;
        }
        boolean sinRed = !viewModel.conectado();
        bannerOffline.setVisible(sinRed);
        bannerOffline.setManaged(sinRed);
    }

    /**
     * UIX-26: muestra un aviso que <b>se quita solo</b>. El cableado JavaFX vive
     * en {@link AvisosInlineChat} y la decisión de plazo en {@link EstadoAvisoInline}.
     */
    void mostrarAvisoInline(String texto, TipoAviso tipo) {
        if (avisos != null) {
            avisos.mostrar(texto, tipo);
        }
    }

    @FXML
    void alCerrarAvisoInline() {
        if (avisos != null) {
            avisos.limpiar();
        }
    }

    /**
     * PAT-03/UIX-00c: pinta el estado que publica la fachada. Llega en el hilo
     * de red, así que el salto a JavaFX es obligatorio. El banner ámbar aparece
     * en cuanto cae el socket, sin esperar al sondeo de 30 s.
     */
    private void pintarEstadoConexion(EstadoConexion estado) {
        boolean restaurada = ultimoEstadoConexion == EstadoConexion.SIN_RED
                && estado == EstadoConexion.CONECTADO;
        ultimoEstadoConexion = estado;
        Platform.runLater(() -> {
            if (estado == EstadoConexion.SESION_CERRADA && !controladorDetenido) {
                manejarCierreSesion();
                return;
            }
            if (bannerOffline != null) {
                bannerOffline.setVisible(estado.muestraBanner());
                bannerOffline.setManaged(estado.muestraBanner());
                if (estado.muestraBanner()) {
                    bannerOffline.setText(estado.texto());
                }
            }
            if (etiquetaEstado != null && !estado.equals(EstadoConexion.CONECTADO)) {
                etiquetaEstado.setText(estado.texto());
            }
            // UIX-26: perder la conexión también se avisa en el aviso inline, que
            // se retira solo. El bannerOffline de al lado se queda mientras siga
            // sin red (refleja el estado); este es el "te pasó esto" puntual.
            if (estado == EstadoConexion.SIN_RED && !controladorDetenido) {
                mostrarAvisoInline(estado.texto(), TipoAviso.ADVERTENCIA);
            } else if (restaurada && !controladorDetenido) {
                mostrarAvisoInline("Conexión recuperada.", TipoAviso.EXITO);
            }
            if (restaurada && !controladorDetenido) {
                alActualizar();
                refrescarVisibleTrasReconexion();
            }
        });
    }
}
