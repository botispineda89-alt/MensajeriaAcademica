package universidad.mensajeria.cliente.presentacion.celdas;

import javafx.beans.property.DoubleProperty;
import javafx.beans.property.SimpleDoubleProperty;
import javafx.geometry.Insets;
import javafx.geometry.Pos;
import javafx.scene.Scene;
import javafx.scene.control.Button;
import javafx.scene.control.Label;
import javafx.scene.control.ScrollPane;
import javafx.scene.image.Image;
import javafx.scene.image.ImageView;
import javafx.scene.input.KeyCode;
import javafx.scene.layout.BorderPane;
import javafx.scene.layout.HBox;
import javafx.scene.layout.StackPane;
import javafx.scene.layout.VBox;
import javafx.stage.Stage;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;
import universidad.mensajeria.cliente.transversal.utilerias.TranscodificadorImagen;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import universidad.mensajeria.cliente.transversal.utilerias.TextosVentana;

/**
 * UIX-28: visor de imagen grande con zoom y pan, a resolución real.
 * <p>
 * El cambio importante: se abre el <b>original</b> desde disco, no la miniatura
 * de 300 px. Antes el visor hacía zoom sobre la miniatura, así que por muy que
 * se subiera el factor el resultado era un cuadro borroso, y encima el botón
 * "Tamaño original" no era 1:1 sino "ajustado a 900×720" porque el lienzo
 * tenía ese tamaño fijo. La aritmética está en {@link EscalaVisor}, que sí se
 * puede testear sin levantar JavaFX.
 * </p>
 */
public final class VisorImagenChat {

    private static final double VENTANA_ANCHO = 940;
    private static final double VENTANA_ALTO = 800;
    private static final double PASO_ZOOM = 1.25;
    private static final double PASO_RUEDA = 1.1;

    /**
     * A partir de este tamaño de archivo el original se carga reducido, con
     * {@link #LADO_MAXIMO_CARGADO} de lado largo. Una foto de 40 MP ocupa ~160 MB
     * descomprimidos y abrirla entera en el visor no compensa; aun así son 13×
     * más píxeles que la miniatura de antes, y el aviso se lo dice al usuario.
     */
    private static final long BYTES_PARA_ORIGINAL_COMPLETO = 6L * 1024 * 1024;

    /** Lado largo al que se recorta el original si el archivo es enorme. */
    static final double LADO_MAXIMO_CARGADO = 4096;

    private VisorImagenChat() {
    }

    /**
     * Abre el original desde disco (camino normal), con la miniatura como red de
     * seguridad.
     * <p>
     * La miniatura no es opcional: si el original no se pudo escribir en disco
     * (disco lleno, permisos) el clic tiene que abrir <i>algo</i>. Sin este
     * parámetro, el mensaje con miniatura pero sin original se quedaba
     * haciendo nada al hacer clic, que es peor que abrirla borrosa.
     * </p>
     *
     * @param original ruta del archivo ya descargado, o {@code null} si no se
     *                  conseguiu guardarlo
     * @param miniatura lo que hay pintado en la burbuja, para el respaldo
     */
    public static void abrir(MensajeLocal item, Path original, Image miniatura) {
        if (original != null) {
            Image imagen = cargarOriginal(original);
            if (imagen == null && item.nombreArchivo() != null
                    && TranscodificadorImagen.necesitaConversion(item.nombreArchivo())) {
                // webp/avif/heic/svg: JavaFX no abre el original; mismo camino
                // que la burbuja (PNG solo para la vista), pero a resolución
                // completa para que el zoom no muestre la miniatura borrosa.
                imagen = cargarConvertido(original, item.nombreArchivo());
            }
            if (imagen != null) {
                abrirEscena(item, imagen, esReducido(original), false);
                return;
            }
        }
        // No hay original legible: se abre la miniatura y se avisa de que lo que
        // se ve está reducido, para que el usuario no lo tome por la foto entera.
        if (miniatura != null && !miniatura.isError()) {
            abrirEscena(item, miniatura, true, true);
        }
    }

    /** Abre solo la miniatura: es lo único que hay si el original no está. */
    public static void abrir(MensajeLocal item, Image miniatura) {
        abrirEscena(item, miniatura, true, true);
    }

    /** true si este archivo se abriría reducido por tamaño. */
    static boolean esReducido(Path archivo) {
        try {
            return Files.size(archivo) > BYTES_PARA_ORIGINAL_COMPLETO;
        } catch (IOException noSePuedeMedir) {
            return false;
        }
    }

    private static Image cargarOriginal(Path archivo) {
        Image imagen = esReducido(archivo)
                ? new Image(archivo.toUri().toString(), LADO_MAXIMO_CARGADO,
                        LADO_MAXIMO_CARGADO, true, false)
                : new Image(archivo.toUri().toString());
        return imagen.isError() ? null : imagen;
    }

    /** Lleva el original a PNG (solo para la vista) y lo carga a resolución completa. */
    private static Image cargarConvertido(Path archivo, String nombreArchivo) {
        try {
            byte[] png = TranscodificadorImagen.aPng(Files.readAllBytes(archivo),
                    nombreArchivo);
            Image imagen = new Image(new java.io.ByteArrayInputStream(png));
            return imagen.isError() ? null : imagen;
        } catch (IOException noSePudo) {
            return null;
        }
    }

    private static Image cargar(Path archivo) {
        Image imagen = new Image(archivo.toUri().toString());
        return imagen.isError() ? null : imagen;
    }

    private static void abrirEscena(MensajeLocal item, Image imagen, boolean avisoReducido,
            boolean avisoMiniatura) {
        double anchoReal = imagen.getWidth();
        double altoReal = imagen.getHeight();
        double encaje = EscalaVisor.escalaEncaje(anchoReal, altoReal,
                VENTANA_ANCHO, VENTANA_ALTO);
        // Arranca encajada, que es como se espera al abrir una foto.
        DoubleProperty zoom = new SimpleDoubleProperty(encaje);

        ImageView grande = new ImageView(imagen);
        grande.setPreserveRatio(true);
        ScrollPane scroll = nuevoLienzo(grande, zoom, anchoReal, altoReal, encaje);

        Label rotulo = new Label(Math.round(anchoReal) + " × " + Math.round(altoReal));
        rotulo.getStyleClass().add("image-placeholder");
        VBox centro = new VBox(scroll, rotulo);
        String aviso = avisoMiniatura
                ? "No se encontró el original, así que se abre la miniatura: ampliar "
                        + "esto no añade detalle."
                : avisoReducido
                ? "La imagen es muy grande: se abre reducida para no cargar el "
                        + "original entero en memoria."
                : "";
        if (!aviso.isEmpty()) {
            Label cartel = new Label(aviso);
            cartel.getStyleClass().add("image-placeholder");
            cartel.setWrapText(true);
            centro.getChildren().add(0, cartel);
        }

        BorderPane raiz = new BorderPane();
        raiz.setCenter(centro);
        raiz.setBottom(barraZoom(zoom, encaje));
        Scene escena = new Scene(raiz, VENTANA_ANCHO, VENTANA_ALTO);
        atajosTeclado(escena, zoom, encaje);
        Stage ventana = new Stage();
        ventana.setTitle(item.nombreArchivo() == null ? TextosVentana.TITULO_VISOR : item.nombreArchivo());
        ventana.setScene(escena);
        ventana.show();
    }

    private static ScrollPane nuevoLienzo(ImageView grande, DoubleProperty zoom,
            double anchoReal, double altoReal, double encaje) {
        StackPane lienzo = new StackPane(grande);
        ScrollPane scroll = new ScrollPane(lienzo);
        scroll.setPannable(true);
        zoom.addListener((o, a, b) ->
            aplicarZoom(grande, scroll, b.doubleValue(), anchoReal, altoReal, encaje));
        scroll.setOnScroll(e -> {
            zoom.set(zoom.get() * (e.getDeltaY() > 0 ? PASO_RUEDA : 1 / PASO_RUEDA));
            e.consume();
        });
        aplicarZoom(grande, scroll, zoom.get(), anchoReal, altoReal, encaje);
        return scroll;
    }

    private static void aplicarZoom(ImageView grande, ScrollPane scroll, double valor,
            double anchoReal, double altoReal, double encaje) {
        double z = EscalaVisor.limitar(valor);
        grande.setFitWidth(EscalaVisor.anchoPintado(anchoReal, z));
        grande.setFitHeight(EscalaVisor.altoPintado(altoReal, z));
        // Barras de desplazamiento solo cuando la imagen ya no cabe.
        boolean cabe = z <= encaje;
        scroll.setFitToWidth(cabe);
        scroll.setFitToHeight(cabe);
    }

    private static HBox barraZoom(DoubleProperty zoom, double encaje) {
        Button mas = new Button("+");
        mas.setOnAction(e -> zoom.set(zoom.get() * PASO_ZOOM));
        mas.setAccessibleText("Acercar");
        Button menos = new Button("−");
        menos.setOnAction(e -> zoom.set(zoom.get() / PASO_ZOOM));
        menos.setAccessibleText("Alejar");
        Button original = new Button("1:1");
        original.setOnAction(e -> zoom.set(1.0));
        original.setAccessibleText("Tamaño original, un píxel de la foto por uno de pantalla");
        Button encajar = new Button("⤢");
        encajar.setOnAction(e -> zoom.set(encaje));
        encajar.setAccessibleText("Ajustar a la ventana");
        HBox barra = new HBox(6, menos, encajar, original, mas);
        barra.setAlignment(Pos.CENTER);
        barra.setPadding(new Insets(6));
        return barra;
    }

    private static void atajosTeclado(Scene escena, DoubleProperty zoom, double encaje) {
        escena.setOnKeyPressed(e -> {
            KeyCode codigo = e.getCode();
            if (codigo == KeyCode.ADD || codigo == KeyCode.PLUS || codigo == KeyCode.EQUALS) {
                zoom.set(zoom.get() * PASO_ZOOM);
            } else if (codigo == KeyCode.SUBTRACT || codigo == KeyCode.MINUS) {
                zoom.set(zoom.get() / PASO_ZOOM);
            } else if (codigo == KeyCode.DIGIT1 || codigo == KeyCode.NUMPAD1) {
                zoom.set(1.0);                                   // tamaño real
            } else if (codigo == KeyCode.DIGIT0 || codigo == KeyCode.NUMPAD0
                    || codigo == KeyCode.F) {
                zoom.set(encaje);
            } else if (codigo == KeyCode.ESCAPE) {
                ((Stage) escena.getWindow()).close();
            } else {
                return;
            }
            e.consume();
        });
    }
}
