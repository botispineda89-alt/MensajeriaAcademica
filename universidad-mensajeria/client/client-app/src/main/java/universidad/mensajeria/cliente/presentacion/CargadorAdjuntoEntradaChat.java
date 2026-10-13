package universidad.mensajeria.cliente.presentacion;

import javafx.concurrent.Task;
import javafx.geometry.Pos;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.control.ListView;
import javafx.scene.control.Tooltip;
import javafx.scene.image.Image;
import javafx.scene.image.ImageView;
import javafx.scene.input.Dragboard;
import javafx.scene.input.TransferMode;
import javafx.scene.layout.FlowPane;
import javafx.scene.layout.StackPane;
import javafx.stage.FileChooser;
import universidad.mensajeria.cliente.presentacion.celdas.FilaChat;
import universidad.mensajeria.cliente.transversal.utilerias.IconoArchivo;
import universidad.mensajeria.cliente.transversal.utilerias.TextoCliente;

import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.concurrent.Executor;
import java.util.function.Consumer;

/**
 * Adjunto de entrada del compositor: elegir, previsualizar y quitar.
 * <p>
 * R3: elige VARIOS archivos de una vez ({@code showOpenMultipleDialog}, el
 * equivalente JavaFX de la selección múltiple), lee el lote en fondo y pinta una
 * tira de miniaturas con un botón de quitar por archivo. Un archivo que no entra
 * no tumba el resto del lote: se rechaza solo y se avisa.
 * <p>
 * No decide envíos ni conversaciones; todo lo externo llega por constructor (DIP).
 * </p>
 */
public final class CargadorAdjuntoEntradaChat {

    private static final int PREVIEW_ANCHO = 96;
    private static final int MAX_ERRORES_MOSTRADOS = 3;

    private final FlowPane listaAdjuntos;
    private final ListView<FilaChat> listaMensajes;
    private final Label etiquetaAdjunto;
    private final InterfazChatViewModel viewModel;
    private final Executor tareasFondo;
    private final Consumer<String> estado;
    private int versionAdjunto;

    public CargadorAdjuntoEntradaChat(FlowPane listaAdjuntos,
                                      ListView<FilaChat> listaMensajes,
                                      Label etiquetaAdjunto,
                                      InterfazChatViewModel viewModel,
                                      Executor tareasFondo,
                                      Consumer<String> estado) {
        this.listaAdjuntos = listaAdjuntos;
        this.listaMensajes = listaMensajes;
        this.etiquetaAdjunto = etiquetaAdjunto;
        this.viewModel = viewModel;
        this.tareasFondo = tareasFondo;
        this.estado = estado;
    }

    /** Arrastra-y-suelta sobre el historial como vía alterna de adjuntar. */
    public void configurarArrastre() {
        listaMensajes.setOnDragOver(e -> {
            Dragboard db = e.getDragboard();
            if (db.hasFiles() && !db.getFiles().isEmpty()) {
                e.acceptTransferModes(TransferMode.COPY);
            }
            e.consume();
        });
        listaMensajes.setOnDragDropped(e -> {
            Dragboard db = e.getDragboard();
            boolean ok = false;
            if (db.hasFiles() && !db.getFiles().isEmpty()) {
                // R3: se cargan TODOS los archivos soltados, no solo el primero.
                cargarArchivos(new ArrayList<>(db.getFiles()));
                ok = true;
            }
            e.setDropCompleted(ok);
            e.consume();
        });
    }

    /** R3: selector con selección múltiple. */
    public void elegir() {
        FileChooser elegidor = new FileChooser();
        elegidor.setTitle("Elegir archivos (imágenes, PDF, documento...)");
        elegidor.getExtensionFilters().addAll(
                new FileChooser.ExtensionFilter("Todos", "*.*"),
                new FileChooser.ExtensionFilter("Imágenes", "*.png", "*.jpg", "*.jpeg",
                        "*.gif", "*.bmp", "*.webp", "*.avif", "*.heic", "*.heif", "*.svg"),
                new FileChooser.ExtensionFilter("Documentos", "*.pdf", "*.txt", "*.docx", "*.xlsx", "*.pptx", "*.zip"));
        // showOpenMultipleDialog ya devuelve la selección múltiple (List<File>).
        List<File> elegido = elegidor.showOpenMultipleDialog(listaMensajes.getScene().getWindow());
        if (elegido != null && !elegido.isEmpty()) {
            cargarArchivos(elegido);
        }
    }

    public void limpiar() {
        versionAdjunto++;                     // invalida cualquier lectura en vuelo
        viewModel.limpiarAdjuntos();
        pintarAdjuntos();
    }

    /**
     * Lote de archivos elegidos o arrastrados: lee en fondo y avisa de los que no
     * entran. Costura de prueba: RNF-19.
     */
    void cargarArchivos(List<File> archivos) {
        List<File> validos = archivos.stream().filter(File::isFile).toList();
        if (validos.isEmpty()) {
            return;                             // se soltó una carpeta: no es un error
        }
        int version = ++versionAdjunto;        // un SOLO versionado por lote
        Task<List<AdjuntoPendiente>> tarea = new Task<>() {
            @Override
            protected List<AdjuntoPendiente> call() {
                List<AdjuntoPendiente> leidos = new ArrayList<>();
                List<String> rechazados = new ArrayList<>();
                for (File f : validos) {
                    try {
                        leidos.add(leer(f));
                    } catch (IOException | IllegalArgumentException fueraDeTope) {
                        rechazados.add(f.getName());   // uno malo NO tumba el lote
                    }
                }
                if (!rechazados.isEmpty()) {
                    avisarRechazados(rechazados);
                }
                return leidos;
            }
        };
        tarea.setOnSucceeded(e -> {
            if (version != versionAdjunto) {
                return;                         // el usuario eligió otra cosa mientras leía
            }
            List<AdjuntoPendiente> leidos = tarea.getValue();
            int aceptados = viewModel.agregarAdjuntos(leidos);
            if (aceptados < leidos.size()) {
                estado.accept("Solo se adjuntaron " + aceptados + " de " + leidos.size()
                        + " archivos (máximo " + AdjuntoPendiente.MAXIMO_ARCHIVOS + ").");
            }
            pintarAdjuntos();
        });
        tarea.setOnFailed(e -> estado.accept("No se pudieron leer los archivos: "
                + FormatoErrorCliente.mensaje(tarea.getException())));
        tareasFondo.execute(tarea);
    }

    /** Un solo archivo (costura de los tests y del drag antiguo). */
    void cargarArchivo(File archivo) {
        if (archivo == null) {
            return;
        }
        cargarArchivos(List.of(archivo));
    }

    /**
     * Lee un archivo del lote tal cual, sin tocar sus bytes.
     * <p>
     * UIX-27: aquí antes se convertía el {@code .webp} a PNG al elegirlo, y eso
     * significaba que al otro le llegaba un {@code .png} en vez del {@code .webp}
     * que se había enviado. El archivo tiene que viajar tal cual: quien lo
     * recibe se descarga exactamente lo que se mandó. Si el cliente no sabe
     * pintarlo, la conversión se hace al recibir, solo para la vista.
     * </p>
     */
    private static AdjuntoPendiente leer(File f) throws IOException {
        byte[] datos = Files.readAllBytes(f.toPath());
        String nombre = f.getName();
        return new AdjuntoPendiente(nombre, TextoCliente.mimeDe(nombre), datos);
    }

    private void avisarRechazados(List<String> rechazados) {
        String detalle = rechazados.stream().limit(MAX_ERRORES_MOSTRADOS)
                .reduce((a, b) -> a + ", " + b).orElse("");
        estado.accept("No se pudieron adjuntar " + rechazados.size() + " archivo(s): "
                + detalle + (rechazados.size() > MAX_ERRORES_MOSTRADOS ? "…" : "")
                + ". Máximo 50 MiB por archivo.");
    }

    /**
     * Redibuja la tira de miniaturas y el resumen. Corre en el hilo de JavaFX:
     * se llama desde los manejadores y desde {@code onSucceeded} del Task.
     * <p>
     * Solo reutiliza clases de estilo existentes: los CSS del cliente se generan
     * desde el contrato de diseño y añadir una clase aquí tocaría los 3 clientes.
     * </p>
     */
    private void pintarAdjuntos() {
        if (listaAdjuntos == null) {
            return;
        }
        listaAdjuntos.getChildren().clear();
        List<AdjuntoPendiente> lista = viewModel.adjuntos();
        for (int i = 0; i < lista.size(); i++) {
            listaAdjuntos.getChildren().add(miniatura(lista.get(i), i));
        }
        boolean hay = !lista.isEmpty();
        listaAdjuntos.setVisible(hay);
        listaAdjuntos.setManaged(hay);
        etiquetaAdjunto.setText(resumen(lista));
    }

    private StackPane miniatura(AdjuntoPendiente a, int indice) {
        StackPane celda = new StackPane();
        if (a.esImagen()) {
            ImageView min = new ImageView(new Image(new java.io.ByteArrayInputStream(a.bytes()),
                    PREVIEW_ANCHO, 0, true, true));
            min.getStyleClass().add("attachment-name");
            celda.getChildren().add(min);
        } else {
            // El icono del tipo (PDF, hoja de calculo...) si lo hay; el 📎 queda
            // para la extension que el catalogo no conoce.
            Image tipo = IconoArchivo.imagen(a.nombre(), IconoArchivo.ANCHO_BANDEJA);
            if (tipo != null) {
                ImageView vista = new ImageView(tipo);
                vista.setPreserveRatio(true);
                celda.getChildren().add(vista);
            } else {
                Label icono = new Label("📎");
                icono.getStyleClass().add("attachment-name");
                celda.getChildren().add(icono);
            }
        }
        Button quitar = new Button("✕");
        quitar.getStyleClass().add("icon-button");
        quitar.setAccessibleText("Quitar " + a.nombre());
        final int posicion = indice;
        quitar.setOnAction(e -> {
            viewModel.quitarAdjunto(posicion);
            pintarAdjuntos();
        });
        StackPane.setAlignment(quitar, Pos.TOP_RIGHT);
        celda.getChildren().add(quitar);
        Tooltip.install(celda, new Tooltip(a.nombre() + " · " + a.tamanoLegible()));
        return celda;
    }

    private static String resumen(List<AdjuntoPendiente> lista) {
        if (lista.isEmpty()) {
            return "";
        }
        if (lista.size() == 1) {
            return lista.get(0).nombre();
        }
        long total = 0;
        int imagenes = 0;
        for (AdjuntoPendiente a : lista) {
            total += a.bytes().length;
            if (a.esImagen()) {
                imagenes++;
            }
        }
        // R3: si hay 2+ imágenes se enviarán como UNA sola burbuja. Decirlo aquí
        // evita que el usuario espere dos mensajes y vea uno.
        String agrupado = imagenes >= 2
                ? (imagenes + (lista.size() - imagenes > 0 ? " imágenes + " + (lista.size() - imagenes) + " archivos" : " imágenes"))
                        + " → 1 mensaje · "
                : "";
        return agrupado + lista.size() + " archivos · " + AdjuntoPendiente.legible(total);
    }
}