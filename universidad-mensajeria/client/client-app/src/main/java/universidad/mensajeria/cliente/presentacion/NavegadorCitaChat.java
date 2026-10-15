package universidad.mensajeria.cliente.presentacion;

import javafx.animation.KeyFrame;
import javafx.animation.KeyValue;
import javafx.animation.Timeline;
import javafx.application.Platform;
import javafx.concurrent.Task;
import javafx.scene.Node;
import javafx.scene.control.ListCell;
import javafx.scene.control.ListView;
import javafx.scene.control.ProgressIndicator;
import javafx.scene.effect.BlurType;
import javafx.scene.effect.DropShadow;
import javafx.scene.paint.Color;
import javafx.util.Duration;
import universidad.mensajeria.cliente.presentacion.celdas.FilaChat;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;
import universidad.mensajeria.cliente.transversal.records.PaginaHistorialRemota;
import universidad.mensajeria.cliente.transversal.excepciones.ExcepcionCliente;
import universidad.mensajeria.cliente.transversal.utilerias.FormatoRespuesta;

import java.io.IOException;
import java.util.List;
import java.util.concurrent.Executor;
import java.util.concurrent.atomic.AtomicLong;
import java.util.function.Consumer;
import java.util.function.Function;
import java.util.function.Supplier;
import universidad.mensajeria.cliente.transversal.utilerias.TextosVentana;

/**
 * Resuelve y navega a la cita visible de un mensaje sin ampliar el protocolo.
 */
public final class NavegadorCitaChat {

    private static final int LIMITE_PAGINA = 50;
    private static final String INICIO_CITA = "Respuesta a ";
    private static final String PREFIJO_REFERENCIA = "Respuesta a citado: ";

    private final ListView<FilaChat> listaMensajes;
    private final InterfazChatViewModel viewModel;
    private final InterfazGestorAdjuntos gestorAdjuntos;
    private final PaginadorHistorialChat paginador;
    private final ProgressIndicator ruedaHistorial;
    private final Executor tareasFondo;
    private final Supplier<String> seleccionado;
    private final Function<String, String> nombreVisible;
    private final Consumer<String> estado;
    private final AtomicLong version = new AtomicLong();
    private Task<Ubicacion> tareaActual;

    public NavegadorCitaChat(ListView<FilaChat> listaMensajes,
                             InterfazChatViewModel viewModel,
                             InterfazGestorAdjuntos gestorAdjuntos,
                             PaginadorHistorialChat paginador,
                             ProgressIndicator ruedaHistorial,
                             Executor tareasFondo,
                             Supplier<String> seleccionado,
                             Function<String, String> nombreVisible,
                             Consumer<String> estado) {
        this.listaMensajes = listaMensajes;
        this.viewModel = viewModel;
        this.gestorAdjuntos = gestorAdjuntos;
        this.paginador = paginador;
        this.ruedaHistorial = ruedaHistorial;
        this.tareasFondo = tareasFondo;
        this.seleccionado = seleccionado;
        this.nombreVisible = nombreVisible;
        this.estado = estado;
    }

    public void navegar(String cita) {
        Referencia referencia = parsearCita(cita);
        String conversacion = seleccionado.get();
        if (referencia == null || conversacion == null) {
            estado.accept("No se pudo identificar el mensaje citado.");
            return;
        }
        cancelar();
        long solicitud = version.get();
        int indice = indiceMensajeCargado(referencia);
        if (indice >= 0) {
            mostrarIndice(indice);
            return;
        }

        estado.accept("Buscando mensaje citado en el historial…");
        ruedaHistorial.setVisible(true);
        ruedaHistorial.setManaged(true);
        Task<Ubicacion> tarea = new Task<>() {
            @Override
            protected Ubicacion call() throws Exception {
                return resolver(conversacion, referencia, solicitud);
            }
        };
        tarea.setOnSucceeded(e -> {
            if (solicitud != version.get() || !conversacion.equals(seleccionado.get())) {
                return;
            }
            ocultarCarga();
            Ubicacion destino = tarea.getValue();
            if (destino == null) {
                paginador.cargarConversacion(
                        () -> estado.accept(TextosVentana.CITA_NO_DISPONIBLE));
                return;
            }
            viewModel.setOffsetActual((destino.rango() / LIMITE_PAGINA) * LIMITE_PAGINA);
            paginador.cargarConversacion(() -> {
                int encontrado = indicePorId(destino.mensaje().idMensaje());
                if (encontrado < 0) {
                    estado.accept("No se pudo mostrar el mensaje citado.");
                } else {
                    mostrarIndice(encontrado);
                }
            });
        });
        tarea.setOnFailed(e -> {
            if (solicitud != version.get()) {
                return;
            }
            ocultarCarga();
            estado.accept("No se pudo buscar el mensaje citado: "
                    + FormatoErrorCliente.mensaje(tarea.getException()));
        });
        tarea.setOnCancelled(e -> {
            if (solicitud == version.get()) {
                ocultarCarga();
            }
        });
        tareaActual = tarea;
        tareasFondo.execute(tarea);
    }

    public void cancelar() {
        version.incrementAndGet();
        if (tareaActual != null) {
            tareaActual.cancel(true);
            tareaActual = null;
        }
        ocultarCarga();
    }

    private Ubicacion resolver(String conversacion, Referencia referencia, long solicitud)
            throws ExcepcionCliente, IOException, InterruptedException {
        int total = viewModel.contarConversacion(conversacion);
        Ubicacion encontrada = buscarEnRango(conversacion, referencia, 0, total, solicitud);
        if (encontrada != null) {
            return encontrada;
        }

        int pagina = gestorAdjuntos.paginaRemota(conversacion);
        Integer totalPaginas = gestorAdjuntos.totalPaginasRemotas(conversacion);
        while (totalPaginas == null || pagina < totalPaginas) {
            comprobarSolicitud(solicitud);
            int totalAnterior = total;
            PaginaHistorialRemota recibida = viewModel.traerHistorial(conversacion, pagina);
            gestorAdjuntos.registrarPaginaRemota(conversacion, pagina + 1,
                    recibida.totalPaginas());
            totalPaginas = recibida.totalPaginas();
            total = viewModel.contarConversacion(conversacion);
            if (total > totalAnterior) {
                encontrada = buscarEnRango(conversacion, referencia,
                        Math.max(0, totalAnterior - LIMITE_PAGINA), total, solicitud);
                if (encontrada != null) {
                    return encontrada;
                }
            }
            pagina++;
        }
        return null;
    }

    private Ubicacion buscarEnRango(String conversacion, Referencia referencia,
                                    int desde, int hasta, long solicitud)
            throws ExcepcionCliente, IOException, InterruptedException {
        for (int offset = (desde / LIMITE_PAGINA) * LIMITE_PAGINA;
             offset < hasta; offset += LIMITE_PAGINA) {
            comprobarSolicitud(solicitud);
            List<MensajeLocal> pagina = viewModel.cargarConversacion(
                    conversacion, LIMITE_PAGINA, offset);
            for (int i = 0; i < pagina.size(); i++) {
                int rango = offset + pagina.size() - 1 - i;
                MensajeLocal mensaje = pagina.get(i);
                if (rango >= desde && rango < hasta && coincide(mensaje, referencia, nombreVisible)) {
                    return new Ubicacion(mensaje, rango);
                }
            }
        }
        return null;
    }

    private void comprobarSolicitud(long solicitud) throws InterruptedException {
        if (Thread.currentThread().isInterrupted() || solicitud != version.get()) {
            throw new InterruptedException("La búsqueda de la cita fue cancelada.");
        }
    }

    private int indiceMensajeCargado(Referencia referencia) {
        for (int i = 0; i < listaMensajes.getItems().size(); i++) {
            FilaChat fila = listaMensajes.getItems().get(i);
            if (fila instanceof FilaChat.MensajeFila mensaje
                    && coincide(mensaje.mensaje(), referencia, nombreVisible)) {
                return i;
            }
        }
        return -1;
    }

    private int indicePorId(String id) {
        for (int i = 0; i < listaMensajes.getItems().size(); i++) {
            FilaChat fila = listaMensajes.getItems().get(i);
            if (fila instanceof FilaChat.MensajeFila mensaje
                    && id.equals(mensaje.mensaje().idMensaje())) {
                return i;
            }
        }
        return -1;
    }

    private void mostrarIndice(int indice) {
        if (indice < 0 || indice >= listaMensajes.getItems().size()) {
            return;
        }
        FilaChat fila = listaMensajes.getItems().get(indice);
        if (!(fila instanceof FilaChat.MensajeFila mensaje)) {
            return;
        }
        enfocar(mensaje.mensaje().idMensaje(), 2);
    }

    private void enfocar(String id, int intentos) {
        int indice = indicePorId(id);
        if (indice < 0) {
            return;
        }
        listaMensajes.scrollTo(indice);
        listaMensajes.getSelectionModel().clearAndSelect(indice);
        resaltarVisible(id, intentos);
    }

    /** Resalta la burbuja respondida sin cambiar el scroll ni la selección del chat. */
    void resaltarAlResponder(MensajeLocal mensaje) {
        if (mensaje == null || mensaje.idMensaje() == null) {
            return;
        }
        resaltarVisible(mensaje.idMensaje(), 2);
    }

    private void resaltarVisible(String id, int intentos) {
        Platform.runLater(() -> {
            Node burbuja = listaMensajes.lookupAll(".list-cell").stream()
                    .filter(ListCell.class::isInstance)
                    .map(nodo -> (ListCell<?>) nodo)
                    .filter(celda -> !celda.isEmpty()
                            && celda.getItem() instanceof FilaChat.MensajeFila filaMensaje
                            && id.equals(filaMensaje.mensaje().idMensaje()))
                    .map(celda -> celda.lookup(".message-bubble"))
                    .filter(nodo -> nodo != null)
                    .findFirst()
                    .orElse(null);
            if (burbuja != null) {
                resaltar(burbuja);
            } else if (intentos > 0) {
                resaltarVisible(id, intentos - 1);
            }
        });
    }

    private void resaltar(Node burbuja) {
        DropShadow resplandor = new DropShadow(BlurType.GAUSSIAN,
                Color.web("#FFD166"), 18, 0.78, 0, 0);
        javafx.scene.effect.Effect efectoAnterior = burbuja.getEffect();
        burbuja.setEffect(resplandor);
        Timeline animacion = new Timeline(
                new KeyFrame(Duration.ZERO, new KeyValue(resplandor.radiusProperty(), 4)),
                new KeyFrame(Duration.millis(250), new KeyValue(resplandor.radiusProperty(), 21)),
                new KeyFrame(Duration.millis(1_500), new KeyValue(resplandor.radiusProperty(), 3)));
        animacion.setOnFinished(e -> {
            if (burbuja.getEffect() == resplandor) {
                burbuja.setEffect(efectoAnterior);
            }
        });
        animacion.play();
    }

    private void ocultarCarga() {
        ruedaHistorial.setVisible(false);
        ruedaHistorial.setManaged(false);
    }

    static Referencia parsearCita(String cita) {
        if (cita == null || !cita.startsWith(INICIO_CITA)) {
            return null;
        }
        int separador = cita.indexOf(": ", INICIO_CITA.length());
        if (separador < 0) {
            return null;
        }
        String identidad = cita.substring(INICIO_CITA.length(), separador).trim();
        String codigo = null;
        int abreCorchete = identidad.lastIndexOf('[');
        if (identidad.endsWith("]") && abreCorchete >= 0) {
            codigo = identidad.substring(abreCorchete + 1, identidad.length() - 1).trim();
        }
        String representacion = cita.substring(separador + 2);
        if (identidad.isBlank() || representacion.isBlank()) {
            return null;
        }
        return new Referencia(identidad, codigo, representacion);
    }

    static boolean coincide(MensajeLocal mensaje, Referencia referencia,
                            Function<String, String> nombreVisible) {
        if (mensaje == null || referencia == null || mensaje.origen() == null) {
            return false;
        }
        boolean mismoRemitente = referencia.codigo() != null
                ? referencia.codigo().equals(mensaje.origen())
                : referencia.identidad().equals(mensaje.origen())
                || referencia.identidad().equals(nombreVisible.apply(mensaje.origen()));
        if (!mismoRemitente) {
            return false;
        }
        String prefijo = FormatoRespuesta.prefijo(mensaje, "citado");
        if (!prefijo.startsWith(PREFIJO_REFERENCIA)) {
            return false;
        }
        String representacion = prefijo.substring(PREFIJO_REFERENCIA.length());
        if (representacion.endsWith("\n\n")) {
            representacion = representacion.substring(0, representacion.length() - 2);
        }
        return referencia.representacion().equals(representacion);
    }

    record Referencia(String identidad, String codigo, String representacion) {
    }

    private record Ubicacion(MensajeLocal mensaje, int rango) {
    }
}
