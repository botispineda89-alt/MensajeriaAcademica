package universidad.mensajeria.cliente.presentacion;

import javafx.application.Platform;
import javafx.concurrent.Task;
import universidad.mensajeria.cliente.transversal.records.EventoCliente;
import universidad.mensajeria.cliente.transversal.records.PaginaHistorialRemota;

import java.util.concurrent.Executor;
import java.util.function.BooleanSupplier;
import java.util.function.Consumer;
import java.util.function.Supplier;

/**
 * Traduce los eventos del modelo a pintadas de la vista.
 * <p>
 * Sale de {@link ChatController} porque elifancho tres decisiones que no son
 * cableado: <b>quién</b> provoca el refresco de contactos (un acuse obliga a
 * repreguntar presencia), <b>qué</b> se marca como leído (solo la conversación
 * abierta y solo lo que no es acuse) y <b>cuándo</b> se repinta la conversación
 * visible (solo si ya estaba al día, para no arrastrar el scroll de quien está
 * leyendo historial viejo).
 * </p>
 */
final class ReceptorEventosChat {

    private final InterfazChatViewModel viewModel;
    private final Executor tareasFondo;
    private final PaginadorHistorialChat paginador;
    private final RefrescadorContactosChat contactos;
    private final InterfazGestorAdjuntos gestorAdjuntos;
    private final Supplier<String> seleccionado;
    private final BooleanSupplier detenido;
    private final Consumer<String> estado;
    private final Runnable alCerrarSesion;

    ReceptorEventosChat(InterfazChatViewModel viewModel, Executor tareasFondo,
                        PaginadorHistorialChat paginador,
                        RefrescadorContactosChat contactos,
                        InterfazGestorAdjuntos gestorAdjuntos,
                        Supplier<String> seleccionado, BooleanSupplier detenido,
                        Consumer<String> estado, Runnable alCerrarSesion) {
        this.viewModel = viewModel;
        this.tareasFondo = tareasFondo;
        this.paginador = paginador;
        this.contactos = contactos;
        this.gestorAdjuntos = gestorAdjuntos;
        this.seleccionado = seleccionado;
        this.detenido = detenido;
        this.estado = estado;
        this.alCerrarSesion = alCerrarSesion;
    }

    /** Llega del hilo de red; todo el trabajo de UI se salta a JavaFX. */
    void recibir(EventoCliente evento) {
        if (evento.tipo() == EventoCliente.Tipo.PRESENCIA) {
            Platform.runLater(contactos::actualizarFondo);
            return;
        }
        Platform.runLater(() -> {
            if (evento.tipo() == EventoCliente.Tipo.SESION_CERRADA) {
                alCerrarSesion.run();
                return;
            }
            String afectado = evento.remitente() != null
                    ? evento.remitente() : evento.destinatario();
            // Un acuse del servidor es la única señal fiable de que el otro
            // usuario sigue ahí: obliga a repreguntar presencia, no a esperar.
            if (evento.esAcuse()) {
                contactos.actualizarFondoForzado();
            } else {
                contactos.actualizarFondo();
            }
            // UIX-16: si se lee historial viejo, lo nuevo no arrastra el scroll:
            // se avisa con el botón y se baja al pulsarlo.
            paginador.notificarEntrante(afectado, evento.esAcuse());
            String sel = seleccionado.get();
            if (sel != null && sel.equals(afectado) && !evento.esAcuse()) {
                viewModel.marcarLeidosEnFondo(sel);
                // Sync multi-PC: con la conversación al día (offset 0) lo entrante
                // —incluido el eco de lo enviado desde otro PC— se pinta solo.
                paginador.recargarSiAlDia(sel);
            }
        });
    }

    /**
     * Sync multi-PC al reconectar: el SYNC_LOGIN solo trae pendientes entrantes;
     * lo enviado desde otro PC mientras este estaba offline se iguala trayendo
     * la página remota 0 (upsert idempotente) y repintando la visible.
     */
    void refrescarVisibleTrasReconexion() {
        String sel = seleccionado.get();
        if (sel == null || tareasFondo == null) {
            return;
        }
        Task<Void> tarea = new Task<>() {
            @Override
            protected Void call() throws Exception {
                try {
                    PaginaHistorialRemota remota = viewModel.traerHistorial(sel, 0);
                    if (gestorAdjuntos != null && remota != null) {
                        gestorAdjuntos.registrarPaginaRemota(sel, 1, remota.totalPaginas());
                    }
                    viewModel.setOffsetActual(0);
                } catch (Exception e) {
                    estado.accept("Historial no actualizado: " + e.getMessage());
                }
                return null;
            }
        };
        tarea.setOnSucceeded(e -> {
            // Si el usuario cambió de chat mientras volvía la red, esta repintada
            // pertenece a otra conversación y no se hace.
            if (!detenido.getAsBoolean() && sel.equals(seleccionado.get())) {
                paginador.cargarConversacion();
            }
        });
        tareasFondo.execute(tarea);
    }
}