package universidad.mensajeria.cliente.presentacion;

import java.util.List;
import java.util.concurrent.Executor;
import java.util.function.BiConsumer;
import java.util.function.Consumer;
import java.util.function.Supplier;
import javafx.beans.property.BooleanProperty;
import universidad.mensajeria.cliente.presentacion.comandos.EnviarMensajeComando;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;

/**
 * PAT-VM: encapsula la acción de enviar mensaje (texto + adjuntos + respuesta)
 * con su plan de envío y el comando asíncrono. Extraído de {@link ChatController}
 * para devolverlo a adaptador FXML.
 * <p>
 * La decisión de "puedo enviar" vive en {@link InterfazChatViewModel}; aquí solo
 * se orquesta el plan, la limpieza del adjunto y el comando.
 * </p>
 */
final class ChatEnvioAccion {

    private final InterfazChatViewModel viewModel;
    private final Executor tareasFondo;
    private final CargadorAdjuntoEntradaChat adjuntosEntrada;
    private final PaginadorHistorialChat paginador;
    private final Consumer<String> estado;
    private final BiConsumer<String, TipoAviso> mostrarAviso;
    private final Supplier<String> seleccionado;
    private final BooleanProperty enviando;
    private final Runnable pedirFocoEntrada;
    /** §7 paridad: fila de progreso y botón de cancelar del lote. */
    private final BarraProgresoEnvioChat progreso;
    /** El lote en curso, para poder cancelarlo desde el botón. */
    private volatile EnviarMensajeComando envioEnCurso;

    ChatEnvioAccion(InterfazChatViewModel viewModel,
                    Executor tareasFondo,
                    CargadorAdjuntoEntradaChat adjuntosEntrada,
                    PaginadorHistorialChat paginador,
                    Consumer<String> estado,
                    BiConsumer<String, TipoAviso> mostrarAviso,
                    Supplier<String> seleccionado,
                    BooleanProperty enviando,
                    Runnable pedirFocoEntrada,
                    BarraProgresoEnvioChat progreso) {
        this.viewModel = viewModel;
        this.tareasFondo = tareasFondo;
        this.adjuntosEntrada = adjuntosEntrada;
        this.paginador = paginador;
        this.estado = estado;
        this.mostrarAviso = mostrarAviso;
        this.seleccionado = seleccionado;
        this.enviando = enviando;
        this.pedirFocoEntrada = pedirFocoEntrada;
        this.progreso = progreso;
    }

    /**
     * Intenta enviar el mensaje actual. Devuelve {@code true} si el envío quedó
     * en curso (el llamador debe limpiar el campo de texto), {@code false} si no
     * se podía enviar.
     */
    boolean intentarEnviar(String textoCampo) {
        String destino = seleccionado.get();
        String texto = textoCampo == null ? "" : textoCampo.trim();
        List<AdjuntoPendiente> adjuntos = viewModel.adjuntos();

        if (!InterfazChatViewModel.puedeEnviar(texto, !adjuntos.isEmpty(),
                destino != null, enviando.get())) {
            return false;
        }

        List<PlanEnvioAdjuntos.Envio> plan = PlanEnvioAdjuntos.calcular(texto, adjuntos);
        if (plan.isEmpty()) {
            return false;
        }

        MensajeLocal respuesta = viewModel.respuestaSeleccionada();
        enviando.set(true);
        // Los pendientes y la cita eran de esta operación: se limpian ya, igual
        // que antes de la extracción (el campo de texto lo limpia el llamador).
        adjuntosEntrada.limpiar();
        viewModel.cancelarRespuesta();

        EnviarMensajeComando comando = new EnviarMensajeComando(viewModel, tareasFondo, destino,
                plan, respuesta, this::alEnvioTerminado, this::alEnvioFallido,
                this::alAvance, this::alPulsarCancelar);
        envioEnCurso = comando;
        comando.ejecutar();
        return true;
    }

    /** §7 paridad: el botón Cancelar delega aquí; el comando consulta la bandera. */
    void cancelar() {
        EnviarMensajeComando enCurso = envioEnCurso;
        if (enCurso == null) {
            return;
        }
        if (progreso != null) {
            progreso.mostrarTexto("Cancelando…");
        }
        enCurso.cancelar();
    }

    private void alAvance(ProgresoEnvio avance) {
        if (progreso != null) {
            progreso.mostrar(avance);
        }
    }

    private void alPulsarCancelar() {
        if (progreso != null) {
            progreso.mostrarTexto("Cancelando…");
        }
    }

    /**
     * Cancelación: lo que no llegó a salir vuelve a la bandeja. Perderlo sería
     * justo lo que el botón promete evitar, así que se reinyecta por el ViewModel
     * y se avisa, igual que en Python ({@code restantes}) y .NET.
     */
    private void devolverRestantes(EnviarMensajeComando comando) {
        List<AdjuntoPendiente> pendientes = comando.restantes();
        if (pendientes.isEmpty()) {
            return;
        }
        viewModel.agregarAdjuntos(pendientes);
        mostrarAviso.accept("Envío cancelado: " + pendientes.size()
                + " adjunto(s) volvieron a la bandeja.", TipoAviso.ADVERTENCIA);
    }

    private void alEnvioTerminado(boolean enRed) {
        cerrarLote();
        if (enRed) {
            estado.accept("Enviado");
            mostrarAviso.accept("Mensaje enviado.", TipoAviso.EXITO);
        } else {
            // Sin red no es un error: se queda en la cola y se reintenta solo.
            // Por eso es INFORMACION y no ERROR.
            estado.accept("Sin red: guardado para reintentar");
            mostrarAviso.accept("Sin red: quedó en la cola y se reintentará solo.",
                    TipoAviso.INFORMACION);
        }
        paginador.cargarConversacion();
        pedirFocoEntrada.run();
    }

    private void alEnvioFallido(String detalle) {
        EnviarMensajeComando enCurso = envioEnCurso;
        cerrarLote();
        if (enCurso != null) {
            devolverRestantes(enCurso);
        }
        estado.accept("No se completó el envío: " + detalle);
        mostrarAviso.accept("No se completó el envío: " + detalle, TipoAviso.ERROR);
        paginador.cargarConversacion();
        pedirFocoEntrada.run();
    }

    private void cerrarLote() {
        enviando.set(false);
        envioEnCurso = null;
        if (progreso != null) {
            progreso.ocultar();
        }
    }
}
