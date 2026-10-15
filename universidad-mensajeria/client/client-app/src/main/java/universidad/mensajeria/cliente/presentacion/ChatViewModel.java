package universidad.mensajeria.cliente.presentacion;
import universidad.mensajeria.cliente.transversal.excepciones.ExcepcionCliente;

import javafx.beans.property.BooleanProperty;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.SimpleBooleanProperty;
import javafx.beans.property.SimpleObjectProperty;
import javafx.collections.FXCollections;
import javafx.collections.ObservableList;
import universidad.mensajeria.cliente.fachada.InterfazArchivosConversacion;
import universidad.mensajeria.cliente.fachada.InterfazChatCliente;
import universidad.mensajeria.cliente.fachada.InterfazEnvioArchivoCancelable;
import universidad.mensajeria.cliente.transversal.excepciones.EnvioArchivoCanceladoException;
import universidad.mensajeria.cliente.transversal.EstadoConexion;
import universidad.mensajeria.cliente.transversal.records.EventoCliente;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;
import universidad.mensajeria.cliente.transversal.records.PaginaHistorialRemota;
import universidad.mensajeria.cliente.transversal.records.RecienteChat;
import universidad.mensajeria.cliente.transversal.records.UsuarioLocal;
import universidad.mensajeria.cliente.transversal.utilerias.FormatoRespuesta;
import universidad.mensajeria.cliente.transversal.utilerias.FormatoUsuario;

import java.io.IOException;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.concurrent.Executor;
import java.util.function.Consumer;
import java.util.function.BooleanSupplier;

/**
 * ViewModel del Chat (Fase 3 del Plan de Arquitectura):
 * Encapsula el estado observable de la vista, la lógica de presentación,
 * filtros y coordinación de casos de uso sin acceso directo a socket ni persistencia.
 */
public class ChatViewModel implements InterfazChatViewModel {

    private final InterfazChatCliente fachada;
    private final Executor tareasFondo;

    private final BooleanProperty enviando = new SimpleBooleanProperty(false);
    private final BooleanProperty adjuntoDisponible = new SimpleBooleanProperty(false);
    private final BooleanProperty hayDestino = new SimpleBooleanProperty(false);
    private final ObjectProperty<EstadoConexion> estadoConexion =
            new SimpleObjectProperty<>(EstadoConexion.SIN_RED);
    private final ObjectProperty<MensajeLocal> respuestaSeleccionada =
            new SimpleObjectProperty<>(null);
    private final Consumer<EstadoConexion> alCambiarEstado = estadoConexion::set;

    private final Map<String, UsuarioLocal> directorio = new HashMap<>();
    private final ObservableList<RecienteChat> recientes = FXCollections.observableArrayList();

    /** R3: los adjuntos pendientes, en el orden en que se enviarán. */
    private final List<AdjuntoPendiente> adjuntos = new ArrayList<>();
    private int offsetActual;

    public ChatViewModel(InterfazChatCliente fachada, Executor tareasFondo) {
        this.fachada = fachada;
        this.tareasFondo = tareasFondo;
        if (fachada != null) {
            this.estadoConexion.set(fachada.estadoConexion());
            fachada.alCambiarEstado(alCambiarEstado);
        }
    }

    public InterfazChatCliente fachada() {
        return fachada;
    }

    public Executor tareasFondo() {
        return tareasFondo;
    }

    public String codigoActual() {
        return fachada != null ? fachada.codigoActual() : null;
    }

    public BooleanProperty enviandoProperty() {
        return enviando;
    }

    public BooleanProperty adjuntoDisponibleProperty() {
        return adjuntoDisponible;
    }

    public BooleanProperty hayDestinoProperty() {
        return hayDestino;
    }

    public ObjectProperty<EstadoConexion> estadoConexionProperty() {
        return estadoConexion;
    }

    public ObjectProperty<MensajeLocal> respuestaSeleccionadaProperty() {
        return respuestaSeleccionada;
    }

    public Map<String, UsuarioLocal> directorio() {
        return directorio;
    }

    public ObservableList<RecienteChat> recientes() {
        return recientes;
    }

    public byte[] adjuntoBytes() {
        return adjuntos.isEmpty() ? null : adjuntos.get(0).bytes();
    }

    public String adjuntoNombre() {
        return adjuntos.isEmpty() ? null : adjuntos.get(0).nombre();
    }

    public String adjuntoMime() {
        return adjuntos.isEmpty() ? null : adjuntos.get(0).mime();
    }

    public int offsetActual() {
        return offsetActual;
    }

    public void setOffsetActual(int offset) {
        this.offsetActual = Math.max(0, offset);
    }

    public void prepararAdjunto(byte[] bytes, String nombre, String mime) {
        limpiarAdjuntos();
        if (bytes != null && bytes.length > 0) {
            adjuntos.add(new AdjuntoPendiente(nombre, mime, bytes));
        }
        adjuntoDisponible.set(!adjuntos.isEmpty());
    }

    public void limpiarAdjunto() {
        limpiarAdjuntos();
    }

    /**
     * R3: copia defensiva. El compositor la recorre para pintar miniaturas y el
     * comando para construir el plan; ninguno debe poder mutar el estado.
     */
    public List<AdjuntoPendiente> adjuntos() {
        return List.copyOf(adjuntos);
    }

    /**
     * R3: añade un lote de adjuntos ya leídos. Si no cabe el lote entero se
     * recorta al cupo en vez de perderlo todo, y el llamante avisa.
     *
     * @return cuántos se aceptaron
     */
    public int agregarAdjuntos(List<AdjuntoPendiente> nuevos) {
        if (nuevos == null || nuevos.isEmpty()) {
            return 0;
        }
        int libres = AdjuntoPendiente.MAXIMO_ARCHIVOS - adjuntos.size();
        if (libres <= 0) {
            return 0;
        }
        int acepta = Math.min(nuevos.size(), libres);
        adjuntos.addAll(nuevos.subList(0, acepta));
        adjuntoDisponible.set(!adjuntos.isEmpty());
        return acepta;
    }

    public void quitarAdjunto(int indice) {
        if (indice >= 0 && indice < adjuntos.size()) {
            adjuntos.remove(indice);
        }
        adjuntoDisponible.set(!adjuntos.isEmpty());
    }

    public void limpiarAdjuntos() {
        adjuntos.clear();
        adjuntoDisponible.set(false);
    }

    public void seleccionarRespuesta(MensajeLocal mensaje) {
        this.respuestaSeleccionada.set(mensaje);
    }

    public MensajeLocal respuestaSeleccionada() {
        return respuestaSeleccionada.get();
    }

    public void cancelarRespuesta() {
        this.respuestaSeleccionada.set(null);
    }

    public static boolean puedeEnviar(String texto, boolean adjunto, boolean hayDestino, boolean enviando) {
        return InterfazChatViewModel.puedeEnviar(texto, adjunto, hayDestino, enviando);
    }

    public String nombreVisible(String codigo) {
        return FormatoUsuario.visible(directorio.get(codigo), codigo);
    }

    public List<RecienteChat> filtrar(List<RecienteChat> lista, String query, String filtro) {
        String q = query == null ? "" : query.trim().toLowerCase(Locale.ROOT);
        String yo = codigoActual();
        List<RecienteChat> resultado = new ArrayList<>();
        for (RecienteChat r : lista) {
            // JV-1: la sesión no conversa consigo misma. El historial devuelve
            // `otro = yo` cuando hay filas propias (el servidor acepta A→A y el
            // dato se conserva: `recientesIncluyeChatConUnoMismo` lo fija); lo
            // que no existe es la tarjeta. Python filtra en la entrega igual.
            if (yo != null && yo.equals(r.otro())) {
                continue;
            }
            UsuarioLocal u = directorio.get(r.otro());
            String nombre = u == null ? "" : (u.nombres() + " " + u.apellidos());
            String preview = r.ultimoContenido() == null ? "" : r.ultimoContenido();
            if (!q.isEmpty() && !r.otro().toLowerCase(Locale.ROOT).contains(q)
                    && !nombre.toLowerCase(Locale.ROOT).contains(q)
                    && !preview.toLowerCase(Locale.ROOT).contains(q)) {
                continue;
            }
            if ("Conectados".equals(filtro) && !r.conectado()) {
                continue;
            }
            if ("No leídos".equals(filtro) && r.noLeidos() <= 0) {
                continue;
            }
            resultado.add(r);
        }
        return resultado;
    }

    public boolean enviarTexto(String destinatario, String texto) throws ExcepcionCliente, IOException {
        return enviarTexto(destinatario, texto, respuestaSeleccionada.get());
    }

    public boolean enviarTexto(String destinatario, String texto, MensajeLocal respuesta)
            throws ExcepcionCliente, IOException {
        if (destinatario == null || destinatario.isBlank()) {
            return false;
        }
        String cuerpo = texto;
        if (respuesta != null) {
            cuerpo = FormatoRespuesta.prefijo(respuesta, nombreVisible(respuesta.origen())) + texto;
        }
        return fachada.enviarTexto(destinatario, cuerpo);
    }

    public boolean enviarImagen(String destinatario, byte[] bytes, String nombre, String mime)
            throws ExcepcionCliente, IOException {
        return fachada.enviarImagen(destinatario, bytes, nombre, mime);
    }

    public boolean enviarImagen(String destinatario, byte[] bytes, String nombre, String mime,
                                String caption) throws ExcepcionCliente, IOException {
        return fachada.enviarImagen(destinatario, bytes, nombre, mime, caption);
    }

    public boolean enviarImagen(String destinatario, byte[] bytes, String nombre, String mime,
                                String texto, MensajeLocal respuesta)
            throws ExcepcionCliente, IOException {
        return fachada.enviarImagen(destinatario, bytes, nombre, mime,
                pieConRespuesta(texto, respuesta));
    }

    public boolean enviarArchivo(String destinatario, byte[] bytes, String nombre, String mime)
            throws ExcepcionCliente, IOException {
        return fachada.enviarArchivo(destinatario, bytes, nombre, mime);
    }

    public boolean enviarArchivo(String destinatario, byte[] bytes, String nombre, String mime,
                                 String caption) throws ExcepcionCliente, IOException {
        return fachada.enviarArchivo(destinatario, bytes, nombre, mime, caption);
    }

    public boolean enviarArchivo(String destinatario, byte[] bytes, String nombre, String mime,
                                 String texto, MensajeLocal respuesta)
            throws ExcepcionCliente, IOException {
        return fachada.enviarArchivo(destinatario, bytes, nombre, mime,
                pieConRespuesta(texto, respuesta));
    }

    @Override
    public boolean enviarArchivo(String destinatario, byte[] bytes, String nombre, String mime,
                                 String caption, BooleanSupplier cancelado)
            throws ExcepcionCliente, IOException {
        if (fachada instanceof InterfazEnvioArchivoCancelable cancelable) {
            return cancelable.enviarArchivo(destinatario, bytes, nombre, mime,
                    caption, cancelado);
        }
        if (cancelado != null && cancelado.getAsBoolean()) {
            throw new EnvioArchivoCanceladoException();
        }
        return fachada.enviarArchivo(destinatario, bytes, nombre, mime, caption);
    }

    private String pieConRespuesta(String texto, MensajeLocal respuesta) {
        String base = texto == null ? "" : texto.trim();
        if (respuesta == null) {
            return base;
        }
        return FormatoRespuesta.prefijo(respuesta, nombreVisible(respuesta.origen())) + base;
    }

    public boolean enviarAdjunto(String destinatario, boolean esImagen) throws ExcepcionCliente, IOException {
        // R3: con varios adjuntos el envío va por lotes (PlanEnvioAdjuntos); este
        // método sigue sirviendo al primero para las llamadas de un solo archivo.
        if (destinatario == null || adjuntos.isEmpty()) {
            return false;
        }
        AdjuntoPendiente primero = adjuntos.get(0);
        if (esImagen) {
            return fachada.enviarImagen(destinatario, primero.bytes(), primero.nombre(), primero.mime());
        } else {
            return fachada.enviarArchivo(destinatario, primero.bytes(), primero.nombre(), primero.mime());
        }
    }

    public List<MensajeLocal> cargarConversacion(String otro, int limite, int offset) throws ExcepcionCliente, IOException {
        return fachada.conversacion(otro, limite, offset);
    }

    public int contarConversacion(String otro) throws ExcepcionCliente, IOException {
        return fachada.contarConversacion(otro);
    }

    public List<String> buscarHistorial(String otro, String patron) throws ExcepcionCliente, IOException {
        return fachada.buscarEnHistorial(otro, patron);
    }

    public void marcarLeidos(String otro) throws ExcepcionCliente, IOException {
        fachada.marcarLeidos(otro);
    }

    public void suscribirEventos(Consumer<EventoCliente> observador) {
        fachada.suscribirEventos(observador);
    }

    public void sincronizarDirectorio() throws ExcepcionCliente, IOException {
        fachada.sincronizarDirectorio();
    }

    public void refrescarPresencia() throws ExcepcionCliente, IOException {
        fachada.refrescarPresencia();
    }

    public List<UsuarioLocal> directorioLocal() throws ExcepcionCliente, IOException {
        return fachada.directorioLocal();
    }

    public List<RecienteChat> cargarRecientes() throws ExcepcionCliente, IOException {
        return fachada.recientes();
    }

    @Override
    public byte[] descargarImagen(String idMensaje) throws ExcepcionCliente, IOException {
        return fachada.descargarImagen(idMensaje);
    }

    @Override
    public PaginaHistorialRemota traerHistorial(String otro, int pagina) throws ExcepcionCliente, IOException {
        return fachada.traerHistorial(otro, pagina);
    }

    @Override
    public byte[] descargarContenidoArchivo(String archivoId) throws ExcepcionCliente, IOException {
        return fachada.descargarContenidoArchivo(archivoId);
    }

    public int cantidadObservadoresEventos() {
        return fachada.cantidadObservadoresEventos();
    }

    public boolean conectado() {
        return fachada.conectado();
    }

    public void desuscribirEventos(Consumer<EventoCliente> observador) {
        fachada.desuscribirEventos(observador);
    }

    public void alCambiarEstado(Consumer<EstadoConexion> observador) {
        fachada.alCambiarEstado(observador);
    }

    public void quitarObservadorEstado(Consumer<EstadoConexion> observador) {
        fachada.quitarObservadorEstado(observador);
    }

    public void detener() {
        if (fachada != null) {
            fachada.quitarObservadorEstado(alCambiarEstado);
        }
    }

    public void marcarLeidosEnFondo(String otro) {
        fachada.marcarLeidosFondo(otro);
    }

    public void desconectar() {
        fachada.desconectar();
    }

    public void logout() throws ExcepcionCliente, IOException {
        fachada.logout();
    }

    public void expulsarOtrasSesiones() throws ExcepcionCliente, IOException {
        fachada.expulsarOtrasSesiones();
    }

    public void cerrarSesionRemota() {
        fachada.cerrarSesionRemota();
    }
}
