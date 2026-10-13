package universidad.mensajeria.cliente.presentacion;

import universidad.mensajeria.cliente.transversal.EstadoConexion;
import universidad.mensajeria.cliente.transversal.records.EventoCliente;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;
import universidad.mensajeria.cliente.transversal.records.RecienteChat;

import java.util.List;
import java.util.concurrent.Executor;
import java.util.function.BooleanSupplier;
import java.util.function.Consumer;
import java.util.function.Function;
import java.util.function.Supplier;
import java.util.prefs.Preferences;

/**
 * Composition root del chat: monta el grafo de colaboradores y lo apaga.
 * <p>
 * Antes vivía dentro de {@link ChatController}, que acababa siendo el lugar donde
 * se decidía todo a la vez. Aquí se ensambla una sola vez y el controlador se
 * limita a delegar.
 * </p>
 * <p>
 * El orden de montaje está escrito en un solo sitio y es significativo: el gestor
 * de adjuntos antes del paginador (que lo necesita), el compositor después de las
 * acciones (que le piden devolver el foco) y los colaboradores que deben leer al
 * compositor —citas y respuestas— se enlazan con llamadas perezosas, porque en el
 * momento de crearlos todavía no existe.
 * </p>
 */
final class ChatComponentes {

    private static final int LIMITE_PAGINA = 50;
    private static final int RESPALDO_SEGUNDOS = 30;
    private static final long DEBOUNCE_MS = 300;

    /** Lo que el controlador aporta desde fuera: identidade estable y textos. */
    record Puentes(Supplier<String> seleccionado,
                   Function<String, String> nombreVisible,
                   Consumer<String> estado,
                   Consumer<EventoCliente> alEntrar,
                   Consumer<EstadoConexion> alCambiarEstado,
                   BooleanSupplier detenido,
                   Runnable alCerrarSesion) {
    }

    private final InterfazChatViewModel viewModel;
    private final Puentes puentes;
    private final SaltoBusquedaPendiente salto = new SaltoBusquedaPendiente();

    private InterfazGestorAdjuntos gestorAdjuntos;
    private CargadorAdjuntoEntradaChat adjuntosEntrada;
    private PaginadorHistorialChat paginador;
    private BusquedaHistorialChat busqueda;
    private RefrescadorContactosChat contactos;
    private GestorSalidaSesionChat salida;
    private ChatEnvioAccion envioAccion;
    private ChatRespuestaAccion respuestaAccion;
    private PresentadorEstadoConexionChat presentadorEstado;
    private ReceptorEventosChat receptorEventos;
    private BarraProgresoEnvioChat progresoEnvio;
    private CompositorChat compositor;
    private CambioConversacionChat cambio;
    private NavegadorCitaChat navegadorCitas;
    private ConfiguradorCeldasChat configuradorCeldas;

    private ChatComponentes(InterfazChatViewModel viewModel, Puentes puentes) {
        this.viewModel = viewModel;
        this.puentes = puentes;
    }

    /** Crea el contenedor todavía vacío: el controlador debe guardar la referencia
     *  <b>antes</b> de cablear, porque los colaboradores llaman de vuelta al
     *  controlador (estado, foco, cierre) desde el propio montaje. */
    static ChatComponentes crear(InterfazChatViewModel viewModel, Puentes puentes) {
        return new ChatComponentes(viewModel, puentes);
    }

    /** Monta el grafo completo y lo deja listo para pintar. */
    void configurar(NodosChat n, InterfazNavegacionCliente navegacion,
                    InterfazFabricaAdjuntos fabricaAdjuntos, FlujoConversacion flujo) {
        construir(n, navegacion, fabricaAdjuntos, flujo);
    }

    private void construir(NodosChat n, InterfazNavegacionCliente navegacion,
                           InterfazFabricaAdjuntos fabricaAdjuntos, FlujoConversacion flujo) {
        Executor tareas = viewModel.tareasFondo();
        progresoEnvio = new BarraProgresoEnvioChat(n.barraProgresoEnvio(), n.barraProgreso(),
                n.etiquetaProgreso(), n.botonCancelarEnvio());

        AvisosInlineChat avisos = new AvisosInlineChat(n.avisoInline(), n.avisoInlineTitulo(),
                n.avisoInlineTexto());
        presentadorEstado = new PresentadorEstadoConexionChat(n.etiquetaEstado(),
                n.bannerOffline(), viewModel, avisos, this::alRestaurarConexion,
                puentes.alCerrarSesion(), puentes.detenido());
        presentadorEstado.refrescar();

        gestorAdjuntos = fabricaAdjuntos.crear(viewModel, tareas, n.listaMensajes(),
                puentes.seleccionado(), puentes.estado());
        adjuntosEntrada = new CargadorAdjuntoEntradaChat(n.listaAdjuntos(), n.listaMensajes(),
                n.etiquetaAdjunto(), viewModel, tareas, puentes.estado());

        salida = new GestorSalidaSesionChat(viewModel, tareas, navegacion, puentes.estado(),
                this::detener, puentes.detenido());
        busqueda = new BusquedaHistorialChat(n.listaMensajes(), n.botonMas(), n.barraBuscar(),
                n.campoBuscarHistorial(), n.etiquetaBuscarContador(), viewModel, tareas,
                puentes.seleccionado(), puentes.estado(), this::alCargarMas, salto, DEBOUNCE_MS);
        busqueda.configurar();
        paginador = new PaginadorHistorialChat(n.listaMensajes(), n.botonMas(), n.botonNuevos(),
                n.ruedaHistorial(), gestorAdjuntos, viewModel, tareas, puentes.seleccionado(),
                puentes.estado(), salto, busqueda::saltarA, LIMITE_PAGINA);
        navegadorCitas = new NavegadorCitaChat(n.listaMensajes(), viewModel, gestorAdjuntos,
                paginador, n.ruedaHistorial(), tareas, puentes.seleccionado(),
                puentes.nombreVisible(), puentes.estado());
        configuradorCeldas = new ConfiguradorCeldasChat(n.listaMensajes(), n.listaUsuarios(),
                navegadorCitas, gestorAdjuntos, busqueda, puentes.nombreVisible(),
                mensaje -> compositor.alResponder(mensaje), puentes.seleccionado());
        configuradorCeldas.configurar();

        contactos = new RefrescadorContactosChat(n.etiquetaSesion(), n.etiquetaChat(),
                n.etiquetaPresencia(), n.comboFiltro(), n.campoBuscar(), n.listaUsuarios(),
                Preferences.userNodeForPackage(ChatComponentes.class), viewModel, tareas,
                viewModel.directorio(), viewModel.recientes(), viewModel.hayDestinoProperty(),
                puentes.seleccionado(), puentes.nombreVisible(), puentes.estado(), flujo,
                DEBOUNCE_MS, LIMITE_PAGINA, RefrescadorContactosChat.respaldoTimeline(
                        javafx.util.Duration.seconds(RESPALDO_SEGUNDOS)));

        envioAccion = new ChatEnvioAccion(viewModel, tareas, adjuntosEntrada, paginador,
                puentes.estado(), presentadorEstado::mostrarAviso, puentes.seleccionado(),
                viewModel.enviandoProperty(), this::pedirFocoEntrada, progresoEnvio);
        progresoEnvio.configurar(envioAccion::cancelar);
        respuestaAccion = new ChatRespuestaAccion(viewModel, puentes.nombreVisible(),
                cita -> compositor.mostrarCita(cita), this::pedirFocoEntrada);

        receptorEventos = new ReceptorEventosChat(viewModel, tareas, paginador, contactos,
                gestorAdjuntos, puentes.seleccionado(), puentes.detenido(), puentes.estado(),
                puentes.alCerrarSesion());

        compositor = new CompositorChat(n.campoEntrada(), n.botonEnviar(), n.etiquetaRespuesta(),
                n.barraRespuesta(), n.barraAdjunto(), n.etiquetaAdjunto(),
                viewModel.enviandoProperty(), viewModel.adjuntoDisponibleProperty(),
                viewModel.hayDestinoProperty(), viewModel.respuestaSeleccionadaProperty(),
                envioAccion, respuestaAccion, adjuntosEntrada, respuestaAccion::responderA);
        compositor.configurarBindings();

        cambio = new CambioConversacionChat(n.etiquetaChat(), n.etiquetaPresencia(), viewModel,
                gestorAdjuntos, paginador, busqueda, configuradorCeldas, adjuntosEntrada,
                puentes.nombreVisible());

        new ConfiguradorVistaChat(n.comboTema(), n.divisorPrincipal(), navegacion,
                n.listaMensajes(), viewModel::codigoActual, busqueda::mostrar).configurar();

        adjuntosEntrada.configurarArrastre();
        contactos.configurar();
    }

    /** PAT-03/UIX-00c: volvió la red, se iguala el directorio y la vista visible. */
    private void alRestaurarConexion() {
        contactos.actualizar();
        receptorEventos.refrescarVisibleTrasReconexion();
    }

    private void pedirFocoEntrada() {
        if (compositor != null) {
            compositor.pedirFocoEntrada();
        }
    }

    void mostrarAviso(String texto, TipoAviso tipo) {
        presentadorEstado.mostrarAviso(texto, tipo);
    }

    void cerrarAviso() { presentadorEstado.cerrarAviso(); }

    void pintarEstado(EstadoConexion estado) { presentadorEstado.pintar(estado); }

    void escribirEstado(String texto) { presentadorEstado.escritorEstado().accept(texto); }

    void alSeleccionar(RecienteChat actual) { cambio.aplicar(actual); }

    void alEnviar() { compositor.alEnviar(); }

    void alCancelarEnvio() { compositor.alCancelarEnvio(); }

    void alTeclaEntrada(javafx.scene.input.KeyEvent evento) { compositor.alTecla(evento); }

    void alElegirImagen() { compositor.alElegirImagen(); }

    void alQuitarImagen() { compositor.alQuitarImagen(); }

    void alCancelarRespuesta() { compositor.alCancelarRespuesta(); }

    void alCargarMas() { paginador.cargarMas(); }

    void alIrANuevos() { paginador.irANuevos(); }

    void alBuscarSiguiente() { busqueda.siguiente(); }

    void alBuscarAnterior() { busqueda.anterior(); }

    void alCerrarBuscar() { busqueda.cerrar(); }

    void alActualizar() { contactos.actualizar(); }

    void alKick() { salida.expulsarOtras(); }

    void alSalir() { salida.salir(); }

    void alSalirTodos() { salida.salirEnTodas(); }

    void salirAlInicio() { salida.salirAlInicio(); }

    void pintarPagina(String conversacion, List<MensajeLocal> pagina, int total, int offset) {
        paginador.pintarPagina(conversacion, pagina, total, offset);
    }

    void recibir(EventoCliente evento) { receptorEventos.recibir(evento); }

    /** El servidor cerró la sesión (KICK, logout remoto): lo lleva el colaborador. */
    void alCerrarSesion() { salida.manejarCierreSesion(); }

    /**
     * Cierre de la vista: colaboradores, suscripciones y ViewModel. Las dos
     * suscripciones se retiran con la <b>misma</b> instancia que se creó en
     * {@code ChatController}; por eso viajan en {@link Puentes} y no se vuelven a
     * construir aquí.
     */
    void detener() {
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
        viewModel.desuscribirEventos(puentes.alEntrar());
        viewModel.quitarObservadorEstado(puentes.alCambiarEstado());
        viewModel.detener();
    }
}