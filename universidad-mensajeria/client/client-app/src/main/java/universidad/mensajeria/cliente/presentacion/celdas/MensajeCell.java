package universidad.mensajeria.cliente.presentacion.celdas;

import java.nio.file.Path;
import java.util.Locale;
import java.util.function.BiConsumer;
import java.util.function.Consumer;
import java.util.function.Function;
import java.util.function.Predicate;
import java.util.function.Supplier;
import javafx.geometry.Insets;
import javafx.geometry.Pos;
import javafx.scene.Cursor;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.control.ListCell;
import javafx.scene.control.Tooltip;
import javafx.scene.image.Image;
import javafx.scene.image.ImageView;
import javafx.scene.layout.HBox;
import javafx.scene.layout.Priority;
import javafx.scene.layout.VBox;
import javafx.scene.text.Text;
import javafx.scene.text.TextFlow;
import universidad.mensajeria.cliente.transversal.records.EstadoMensaje;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;
import universidad.mensajeria.cliente.transversal.utilerias.FormatoFecha;
import universidad.mensajeria.cliente.transversal.utilerias.FormatoEtapasFiltrado;
import universidad.mensajeria.cliente.transversal.utilerias.IconoArchivo;

/** Celda virtualizada del historial: pinta metadatos y solo pide la imagen visible. */
public final class MensajeCell extends ListCell<FilaChat> {

    private static final double MINI_ANCHO = 300;
    private static final double MINI_ALTO = 240;
    private static final double BURBUJA_PROPORCION = 0.65;
    private static final double BURBUJA_TOPE = 580;
    private static final double BURBUJA_SUELO = 280;
    private static final double BURBUJA_DEFECTO = 460;
    private static final double BURBUJA_MARGEN = 30;
    private static final double BURBUJA_INTERIOR_MIN = 200;
    private static final double LISTA_MIN_ANCHO = 100;
    private static final int CITA_CORTE = 300;

    private final Consumer<MensajeLocal> alVerImagen;
    private final Consumer<MensajeLocal> alReintentarImagen;
    private final Consumer<MensajeLocal> alResponder;
    private final Function<String, String> nombreVisible;
    private final Predicate<String> imagenFallida;
    /** UIX-27: los bytes llegaron pero este cliente no sabe pintarlos. */
    private final Predicate<String> noDecodificable;
    /** UIX-28: ruta del original en disco, para abrir el visor a resolución real. */
    private final Function<String, Path> originalImagen;
    private final Consumer<MensajeLocal> alDescargar;
    private final Supplier<String> consultaResaltada;
    private final BiConsumer<MensajeLocal, String> alNavegarCita;

    public MensajeCell(Consumer<MensajeLocal> alVerImagen,
                       Consumer<MensajeLocal> alReintentarImagen,
                       Consumer<MensajeLocal> alResponder,
                       Function<String, String> nombreVisible,
                       Predicate<String> imagenFallida) {
        this(alVerImagen, alReintentarImagen, alResponder, nombreVisible, imagenFallida, m -> { }, () -> "");
    }

    public MensajeCell(Consumer<MensajeLocal> alVerImagen,
                       Consumer<MensajeLocal> alReintentarImagen,
                       Consumer<MensajeLocal> alResponder,
                       Function<String, String> nombreVisible,
                       Predicate<String> imagenFallida,
                       Consumer<MensajeLocal> alDescargar) {
        this(alVerImagen, alReintentarImagen, alResponder, nombreVisible,
                imagenFallida, alDescargar, () -> "", (mensaje, cita) -> { });
    }

    public MensajeCell(Consumer<MensajeLocal> alVerImagen,
                       Consumer<MensajeLocal> alReintentarImagen,
                       Consumer<MensajeLocal> alResponder,
                       Function<String, String> nombreVisible,
                       Predicate<String> imagenFallida,
                       Consumer<MensajeLocal> alDescargar,
                       Supplier<String> consultaResaltada) {
        this(alVerImagen, alReintentarImagen, alResponder, nombreVisible, imagenFallida,
                alDescargar, consultaResaltada, (mensaje, cita) -> { });
    }

    public MensajeCell(Consumer<MensajeLocal> alVerImagen,
                       Consumer<MensajeLocal> alReintentarImagen,
                       Consumer<MensajeLocal> alResponder,
                       Function<String, String> nombreVisible,
                       Predicate<String> imagenFallida,
                       Consumer<MensajeLocal> alDescargar,
                       Supplier<String> consultaResaltada,
                       BiConsumer<MensajeLocal, String> alNavegarCita) {
        this(alVerImagen, alReintentarImagen, alResponder, nombreVisible, imagenFallida,
                alDescargar, consultaResaltada, alNavegarCita, id -> false, id -> null);
    }

    /**
     * UIX-27: constructor completo. {@code noDecodificable} distingue "no se pudo
     * descargar" de "se descargó pero este cliente no sabe pintarlo": son fallos
     * distintos y el texto de la burbuja tiene que decirlo.
     * <p>
     * UIX-28: {@code originalImagen} da la ruta del original en disco para que el
     * visor abra a resolución real. Si devuelve {@code null}, el visor cae a la
     * miniatura.
     * </p>
     */
    public MensajeCell(Consumer<MensajeLocal> alVerImagen,
                       Consumer<MensajeLocal> alReintentarImagen,
                       Consumer<MensajeLocal> alResponder,
                       Function<String, String> nombreVisible,
                       Predicate<String> imagenFallida,
                       Consumer<MensajeLocal> alDescargar,
                       Supplier<String> consultaResaltada,
                       BiConsumer<MensajeLocal, String> alNavegarCita,
                       Predicate<String> noDecodificable) {
        this(alVerImagen, alReintentarImagen, alResponder, nombreVisible, imagenFallida,
                alDescargar, consultaResaltada, alNavegarCita, noDecodificable, id -> null);
    }

    /** Constructor completo, con la ruta del original para el visor. */
    public MensajeCell(Consumer<MensajeLocal> alVerImagen,
                       Consumer<MensajeLocal> alReintentarImagen,
                       Consumer<MensajeLocal> alResponder,
                       Function<String, String> nombreVisible,
                       Predicate<String> imagenFallida,
                       Consumer<MensajeLocal> alDescargar,
                       Supplier<String> consultaResaltada,
                       BiConsumer<MensajeLocal, String> alNavegarCita,
                       Predicate<String> noDecodificable,
                       Function<String, Path> originalImagen) {
        this.alVerImagen = alVerImagen;
        this.alReintentarImagen = alReintentarImagen;
        this.alResponder = alResponder;
        this.nombreVisible = nombreVisible;
        this.imagenFallida = imagenFallida;
        this.noDecodificable = noDecodificable == null ? id -> false : noDecodificable;
        this.originalImagen = originalImagen == null ? id -> null : originalImagen;
        this.alDescargar = alDescargar == null ? m -> { } : alDescargar;
        this.consultaResaltada = consultaResaltada == null ? () -> "" : consultaResaltada;
        this.alNavegarCita = alNavegarCita == null ? (mensaje, cita) -> { } : alNavegarCita;
        getStyleClass().add("chat-message-cell");
    }

    @Override
    protected void updateItem(FilaChat filaChat, boolean empty) {
        super.updateItem(filaChat, empty);
        if (empty || filaChat == null) {
            setGraphic(null);
            setText(null);
            return;
        }
        if (filaChat instanceof FilaChat.DiaFila dia) {
            setGraphic(cabeceraDia(dia));
            return;
        }
        FilaChat.MensajeFila fila = (FilaChat.MensajeFila) filaChat;
        setGraphic(filaMensaje(fila.mensaje(), fila.mostrarRemitente()));
        setText(null);
    }

    private static Label cabeceraDia(FilaChat.DiaFila dia) {
        Label cabecera = new Label(dia.etiqueta());
        cabecera.getStyleClass().add("day-separator");
        cabecera.setMaxWidth(Double.MAX_VALUE);
        cabecera.setTooltip(new Tooltip(dia.claveDia()));
        return cabecera;
    }

    private HBox filaMensaje(MensajeLocal item, boolean mostrarRemitente) {
        VBox burbuja = burbuja(item, mostrarRemitente);
        HBox fila = new HBox(burbuja);
        fila.setAlignment(item.enviado() ? Pos.CENTER_RIGHT : Pos.CENTER_LEFT);
        fila.setPadding(new Insets(3, 12, 3, 12));
        HBox.setHgrow(burbuja, Priority.NEVER);
        fila.setOnMouseClicked(e -> {
            if (e.getClickCount() == 2) {
                alResponder.accept(item);
            }
        });
        return fila;
    }

    private VBox burbuja(MensajeLocal item, boolean mostrarRemitente) {
        VBox burbuja = new VBox(5);
        burbuja.getStyleClass().addAll("message-bubble", item.enviado() ? "message-out" : "message-in");
        burbuja.setPadding(new Insets(8, 10, 6, 10));
        double ancho = anchoBurbuja();
        burbuja.setMaxWidth(ancho);
        double interior = Math.max(BURBUJA_INTERIOR_MIN, ancho - BURBUJA_MARGEN);
        if (!item.enviado() && mostrarRemitente) {
            burbuja.getChildren().add(etiquetaRemitente(item));
        }
        burbuja.getChildren().add(contenido(item, interior));
        burbuja.getChildren().add(pie(item));
        return burbuja;
    }

    private Label etiquetaRemitente(MensajeLocal item) {
        Label remitente = new Label(nombreVisible.apply(item.origen()));
        remitente.getStyleClass().add("message-sender");
        remitente.setTooltip(new Tooltip(remitente.getText()));
        return remitente;
    }

    private javafx.scene.Node contenido(MensajeLocal item, double interior) {
        return switch (item.tipo() == null ? "" : item.tipo()) {
            case "MENSAJE_IMAGEN" -> bloqueImagen(item, interior);
            case "MENSAJE_ARCHIVO" -> bloqueArchivo(item, interior);
            default -> bloqueTexto(item, interior);
        };
    }

    /**
     * UIX-28: botón de guardar de una IMAGEN. Va arriba del bloque, antes de la
     * miniatura, y dice qué guarda: "⬇ Guardar" a secas no dice si lo que sale
     * es una foto o un PDF. Paridad con el cliente .NET.
     */
    private Button botonGuardarImagen(MensajeLocal item) {
        Button guardar = new Button("⬇ Guardar imagen");
        guardar.getStyleClass().add("reply-button");
        guardar.setTooltip(new Tooltip("Guardar la imagen en este equipo"));
        guardar.setAccessibleText("Guardar la imagen");
        guardar.setOnAction(e -> alDescargar.accept(item));
        return guardar;
    }

    /**
     * UIX-28: botón de guardar de un ARCHIVO. Hermano de {@link #botonGuardarImagen}
     * y por eso lleva otro texto: son dos, no uno.
     */
    private Button botonGuardarArchivo(MensajeLocal item) {
        Button guardar = new Button("⬇ Guardar archivo");
        guardar.getStyleClass().add("reply-button");
        guardar.setTooltip(new Tooltip("Guardar el archivo en este equipo"));
        guardar.setAccessibleText("Guardar el archivo");
        guardar.setOnAction(e -> alDescargar.accept(item));
        return guardar;
    }

    private VBox bloqueImagen(MensajeLocal item, double interior) {
        VBox caja = new VBox(5);
        caja.getChildren().add(botonGuardarImagen(item));
        String detalle = item.nombreArchivo() == null ? "Imagen" : item.nombreArchivo();
        Label titulo = new Label(detalle);
        titulo.getStyleClass().add("image-caption");
        caja.getChildren().add(titulo);
        Image miniatura = MiniaturasChat.obtener(item.idMensaje());
        if (miniatura != null) {
            caja.getChildren().add(vistaMiniatura(item, miniatura));
        } else {
            caja.getChildren().add(placeholderImagen(item));
        }
        String resumenEtapas = FormatoEtapasFiltrado.resumir(item.etapasFiltrado());
        if (!resumenEtapas.isBlank()) {
            Label etapas = new Label(resumenEtapas);
            etapas.getStyleClass().add("image-caption");
            etapas.setWrapText(true);
            caja.getChildren().add(etapas);
        }
        pintarPie(caja, item, interior);
        return caja;
    }

    private ImageView vistaMiniatura(MensajeLocal item, Image miniatura) {
        ImageView vista = new ImageView(miniatura);
        vista.setFitWidth(MINI_ANCHO);
        vista.setFitHeight(MINI_ALTO);
        vista.setPreserveRatio(true);
        vista.setSmooth(true);
        vista.setOnMouseClicked(e -> {
            if (e.getClickCount() >= 2) {
                alResponder.accept(item);
                e.consume();
            } else {
                // UIX-28: se abre el ORIGINAL, no la miniatura. Ampliar 300 px da
                // un cuadro borroso por muy que se suba el factor. La miniatura
                // se pasa igual: si el original no se pudo dejar en disco, el
                // clic tiene que abrir algo.
                VisorImagenChat.abrir(item, originalImagen.apply(item.idMensaje()), miniatura);
            }
        });
        return vista;
    }

    private Label placeholderImagen(MensajeLocal item) {
        // UIX-27: antes, una imagen que se descargaba pero no se sabía pintar
        // se quedaba en "Cargando imagen…" para siempre: el placeholder volvía a
        // pedirla, el decode fallaba otra vez y el bucle no terminaba nunca.
        EstadoImagen estado = estadoImagen(
                imagenFallida.test(item.idMensaje()),
                noDecodificable.test(item.idMensaje()));
        Label espera = new Label(estado.texto());
        espera.getStyleClass().add("image-placeholder");
        espera.setOnMouseClicked(e -> {
            if (e.getClickCount() >= 2) {
                alResponder.accept(item);
                e.consume();
            } else {
                alReintentarImagen.accept(item);
            }
        });
        espera.setTooltip(new Tooltip("Cargar o reintentar imagen"));
        if (estado.pedirImagen()) {
            alVerImagen.accept(item);
        }
        return espera;
    }

    /**
     * UIX-27: qué pintar cuando aún no hay miniatura y si hay que volver a
     * pedir la imagen.
     * <p>
     * Vive fuera de la celda a propósito: la celda necesita JavaFX, y esta es
     * justo la decisión que provocaba el fallo (pedir una imagen que ya sabemos
     * que no se puede pintar). Sin este método el bucle era invisible para los
     * tests.
     *
     * @param falloDescarga no se pudieron descargar los bytes
     * @param ilegible      los bytes llegaron pero este cliente no los decodifica
     */
    public static EstadoImagen estadoImagen(boolean falloDescarga, boolean ilegible) {
        if (ilegible) {
            // No reintentable: descargar otra vez daría el mismo resultado.
            return new EstadoImagen("Formato no soportado por este cliente", false);
        }
        if (falloDescarga) {
            return new EstadoImagen("No se pudo cargar · reintentar", false);
        }
        return new EstadoImagen("Cargando imagen…", true);
    }

    /**
     * @param texto       lo que se ve en la burbuja
     * @param pedirImagen si hay que lanzar la descarga (solo mientras no se sabe
     *                    el resultado); es lo que cortaba el bucle infinito
     */
    public record EstadoImagen(String texto, boolean pedirImagen) {
    }

    private VBox bloqueArchivo(MensajeLocal item, double interior) {
        VBox caja = new VBox(5);
        // UIX-28: el botón va ARRIBA, antes del nombre del archivo.
        caja.getChildren().add(botonGuardarArchivo(item));
        String nombre = item.nombreArchivo() == null ? "Archivo" : item.nombreArchivo();
        String tam = item.tamanoArchivo() != null ? " (" + item.tamanoArchivo() + " bytes)" : "";
        // El icono del tipo va como grafico del propio texto (no en un HBox
        // aparte) para no tocar el ajuste de linea de la burbuja.
        Image icono = IconoArchivo.imagen(nombre, IconoArchivo.ANCHO_BURBUJA);
        // FORMATOS-02: si la extension tiene icono propio, el 📎 genérico
        // sobra (doble icono); solo queda para lo que no está en el catálogo.
        Label titulo = new Label((icono == null ? "📎 " : "") + nombre + tam);
        titulo.getStyleClass().add("image-caption");
        titulo.setWrapText(true);
        if (icono != null) {
            ImageView vista = new ImageView(icono);
            vista.setPreserveRatio(true);
            titulo.setGraphic(vista);
            titulo.setGraphicTextGap(6);
        }
        caja.getChildren().add(titulo);
        pintarPie(caja, item, interior);
        return caja;
    }

    /**
     * Pie/caption bajo la imagen o el archivo, en la misma burbuja (estilo
     * WhatsApp/Telegram web). Reusa cita y resaltado del texto normal.
     */
    private void pintarPie(VBox caja, MensajeLocal item, double interior) {
        if (!item.tieneCaption()) {
            return;
        }
        pintarTexto(caja, item.caption(), interior, item);
    }

    private VBox bloqueTexto(MensajeLocal item, double interior) {
        VBox caja = new VBox(2);
        String crudo = item.contenido() == null ? "" : item.contenido();
        pintarTexto(caja, crudo, interior, item);
        return caja;
    }

    /** Cita + cuerpo con resaltado de búsqueda (texto y pies comparten formato). */
    private void pintarTexto(VBox caja, String crudo, double interior, MensajeLocal item) {
        String cita = extraerCita(crudo);
        String cuerpo = cita == null ? crudo : crudo.substring(crudo.indexOf("\n\n") + 2);
        if (cita != null) {
            Label quote = new Label(cita);
            quote.setWrapText(true);
            quote.setMaxWidth(interior);
            quote.getStyleClass().add("message-quote");
            quote.setCursor(Cursor.HAND);
            quote.setTooltip(new Tooltip("Ir al mensaje original"));
            quote.setOnMouseClicked(e -> {
                alNavegarCita.accept(item, cita);
                e.consume();
            });
            caja.getChildren().add(quote);
        }
        String consulta = consultaResaltada.get();
        if (consulta != null && !consulta.isBlank()
                && cuerpo.toLowerCase(Locale.ROOT).contains(consulta.toLowerCase(Locale.ROOT))) {
            caja.getChildren().add(textoResaltado(cuerpo, consulta.trim(), interior));
        } else {
            Label texto = new Label(cuerpo);
            texto.setWrapText(true);
            texto.setMaxWidth(interior);
            texto.getStyleClass().add("message-text");
            caja.getChildren().add(texto);
        }
    }

    private static String extraerCita(String crudo) {
        int corte = crudo.indexOf("\n\n");
        if (crudo.startsWith("Respuesta a ") && corte > 0 && corte < CITA_CORTE) {
            return crudo.substring(0, corte);
        }
        return null;
    }

    private HBox pie(MensajeLocal item) {
        String hora = FormatoFecha.horaCorta(item.fechaEnvio());
        HBox pie = new HBox(7);
        pie.setAlignment(Pos.CENTER_RIGHT);
        pie.getStyleClass().add("message-footer");
        Button responder = new Button("↩");
        responder.getStyleClass().add("reply-button");
        responder.setTooltip(new Tooltip("Responder a este mensaje"));
        responder.setAccessibleText("Responder a este mensaje");
        responder.setOnAction(e -> alResponder.accept(item));
        Label lHora = new Label(hora);
        lHora.getStyleClass().add("message-time");
        String dia = FormatoFecha.etiquetaDia(item.fechaEnvio());
        if (!dia.isEmpty()) {
            lHora.setTooltip(new Tooltip(dia + " " + hora));
        }
        pie.getChildren().addAll(responder, lHora);
        if (item.enviado()) {
            pie.getChildren().add(tick(item));
        }
        return pie;
    }

    private Label tick(MensajeLocal item) {
        Label tick = new Label(EstadoMensaje.tickDe(item.estado()));
        tick.getStyleClass().addAll("message-tick",
                "LEIDO".equals(item.estado()) ? "message-read" : "message-unread");
        tick.setTooltip(new Tooltip(EstadoMensaje.detalleDe(item.estado(),
                nombreVisible.apply(item.destino()))));
        return tick;
    }

    /**
     * UIX-06: cuerpo con las coincidencias resaltadas. Parte el texto por la
     * consulta (insensible a mayúsculas) y marca esos tramos con la clase
     * `message-highlight`; el resto conserva `message-text`.
     */
    static TextFlow textoResaltado(String cuerpo, String consulta, double maxWidth) {
        TextFlow flujo = new TextFlow();
        flujo.setMaxWidth(maxWidth);
        String minusCuerpo = cuerpo.toLowerCase(Locale.ROOT);
        String minusConsulta = consulta.toLowerCase(Locale.ROOT);
        int pos = 0;
        int hallado;
        while ((hallado = minusCuerpo.indexOf(minusConsulta, pos)) >= 0) {
            if (hallado > pos) {
                flujo.getChildren().add(textoPlano(cuerpo.substring(pos, hallado)));
            }
            Text marca = new Text(cuerpo.substring(hallado, hallado + consulta.length()));
            marca.getStyleClass().add("message-highlight");
            flujo.getChildren().add(marca);
            pos = hallado + consulta.length();
        }
        if (pos < cuerpo.length()) {
            flujo.getChildren().add(textoPlano(cuerpo.substring(pos)));
        }
        return flujo;
    }

    private static Text textoPlano(String trozo) {
        Text texto = new Text(trozo);
        texto.getStyleClass().add("message-text");
        return texto;
    }

    private double anchoBurbuja() {
        try {
            javafx.scene.control.ListView<?> lista = getListView();
            if (lista != null && lista.getWidth() > LISTA_MIN_ANCHO) {
                return Math.min(BURBUJA_TOPE, Math.max(BURBUJA_SUELO, lista.getWidth() * BURBUJA_PROPORCION));
            }
        } catch (RuntimeException ignorada) {
            // Sin lista a mano se usa el ancho clásico.
        }
        return BURBUJA_DEFECTO;
    }

    static String tickDe(String estado) {
        return EstadoMensaje.tickDe(estado);
    }

    /**
     * Se llama desde tareas de fondo; la escena FX nunca decodifica Base64.
     *
     * @return true si quedó miniatura; false si los bytes llegaron pero no se
     *         pueden pintar. El gestor lo anota para no reintentar en bucle.
     */
    public static boolean precalentar(String idMensaje, String base64) {
        return MiniaturasChat.precalentar(idMensaje, base64);
    }
}
