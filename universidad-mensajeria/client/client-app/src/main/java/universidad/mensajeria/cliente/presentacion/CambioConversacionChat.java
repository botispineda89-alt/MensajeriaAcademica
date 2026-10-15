package universidad.mensajeria.cliente.presentacion;

import javafx.scene.control.Label;
import universidad.mensajeria.cliente.transversal.records.RecienteChat;

import java.util.prefs.Preferences;

/**
 * Lo que hay que hacer cuando el usuario elige otra conversación.
 * <p>
 * Son cinco pasos y ninguno es trivial por sí solo: cancelar la cita en curso,
 * soltar los adjuntos que eran del chat anterior, recordar la última conversación
 * por cuenta, recargar la primera página y marcar lo leído. Encadenarlos en el
 * controlador era lo que lo mantenía largo; aquí quedan con su porqué al lado.
 * </p>
 */
final class CambioConversacionChat {

    private static final String PREF = "ultimoChat_";

    private final Label etiquetaChat;
    private final Label etiquetaPresencia;
    private final InterfazChatViewModel viewModel;
    private final InterfazGestorAdjuntos gestorAdjuntos;
    private final PaginadorHistorialChat paginador;
    private final BusquedaHistorialChat busqueda;
    private final ConfiguradorCeldasChat configuradorCeldas;
    private final CargadorAdjuntoEntradaChat adjuntosEntrada;
    private final java.util.function.Function<String, String> nombreVisible;
    private final java.util.concurrent.Executor tareasFondo;
    private final Runnable alMarcarLeidos;

    CambioConversacionChat(Label etiquetaChat, Label etiquetaPresencia,
                           InterfazChatViewModel viewModel,
                           InterfazGestorAdjuntos gestorAdjuntos,
                           PaginadorHistorialChat paginador,
                           BusquedaHistorialChat busqueda,
                           ConfiguradorCeldasChat configuradorCeldas,
                           CargadorAdjuntoEntradaChat adjuntosEntrada,
                           java.util.function.Function<String, String> nombreVisible,
                           java.util.concurrent.Executor tareasFondo,
                           Runnable alMarcarLeidos) {
        this.etiquetaChat = etiquetaChat;
        this.etiquetaPresencia = etiquetaPresencia;
        this.viewModel = viewModel;
        this.gestorAdjuntos = gestorAdjuntos;
        this.paginador = paginador;
        this.busqueda = busqueda;
        this.configuradorCeldas = configuradorCeldas;
        this.adjuntosEntrada = adjuntosEntrada;
        this.nombreVisible = nombreVisible;
        this.tareasFondo = tareasFondo;
        this.alMarcarLeidos = alMarcarLeidos;
    }

    void aplicar(RecienteChat actual) {
        if (configuradorCeldas != null) {
            configuradorCeldas.cancelarNavegacionCita();
        }
        // R3: los adjuntos pendientes eran del chat anterior. Sin esto, un archivo
        // elegido para Joan se enviaría a Sofia sin que nadie lo pidiera.
        adjuntosEntrada.limpiar();
        // T1 (2026-10-14): la preferencia se sigue guardando (es el último chat, un dato
        // del usuario) aunque ya nadie la lea para reabrir la conversación al arrancar:
        // el chat arranca sin selección a propósito.
        Preferences.userNodeForPackage(CambioConversacionChat.class)
                .put(PREF + viewModel.codigoActual(), actual.otro());
        viewModel.setOffsetActual(0);
        gestorAdjuntos.cambiarConversacion();
        busqueda.cerrar();
        etiquetaChat.setText(nombreVisible.apply(actual.otro()));
        etiquetaPresencia.setText(actual.conectado() ? "en línea" : "desconectado");
        paginador.cargarConversacion();
        viewModel.marcarLeidosEnFondo(actual.otro());
        if (alMarcarLeidos != null) {
            alMarcarLeidos.run();
        }
        if (tareasFondo != null) {
            tareasFondo.execute(() -> {
                try {
                    universidad.mensajeria.cliente.transversal.records.PaginaHistorialRemota remota =
                            viewModel.traerHistorial(actual.otro(), 0);
                    if (gestorAdjuntos != null) {
                        gestorAdjuntos.registrarPaginaRemota(actual.otro(), 1, remota.totalPaginas());
                    }
                    javafx.application.Platform.runLater(() -> paginador.recargarSiAlDia(actual.otro()));
                } catch (Exception e) {
                    // Si el servidor no responde o está offline, el historial local ya está cargado
                }
            });
        }
    }
}