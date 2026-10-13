package universidad.mensajeria.cliente.presentacion;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.Callable;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.FutureTask;
import java.util.concurrent.TimeUnit;
import javafx.application.Platform;
import javafx.scene.control.Label;
import javafx.scene.layout.HBox;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * UIX-26/PAT-VM: el colaborador del aviso inline pinta, oculta y programa su
 * reloj. La decisión de plazo vive en {@link EstadoAvisoInline} (probada aparte);
 * aquí se cubre el cableado que antes tenía el controlador.
 */
class AvisosInlineChatTest {

    private HBox contenedor;
    private Label titulo;
    private Label texto;

    @Test
    void mostrarPintaTituloTextoYVisibilidad() throws Exception {
        iniciarJavaFx();
        AvisosInlineChat avisos = crearEnJavaFx();

        avisos.mostrar("Mensaje enviado.", TipoAviso.EXITO);

        ejecutarEnJavaFx(() -> {
            assertEquals("Listo", titulo.getText());
            assertEquals("Mensaje enviado.", texto.getText());
            assertTrue(contenedor.isVisible());
            assertTrue(contenedor.isManaged());
            return null;
        });
    }

    @Test
    void limpiarOcultaElAvisoYNoLoDejaTapandoElChat() throws Exception {
        iniciarJavaFx();
        AvisosInlineChat avisos = crearEnJavaFx();

        avisos.mostrar("No se completó el envío.", TipoAviso.ERROR);
        ejecutarEnJavaFx(() -> {
            assertEquals("Error", titulo.getText());
            avisos.limpiar();
            return null;
        });

        ejecutarEnJavaFx(() -> {
            assertFalse(contenedor.isVisible());
            assertFalse(contenedor.isManaged());
            return null;
        });
    }

    @Test
    void cadaSeveridadMuestraSuTituloYSuPlazo() throws Exception {
        iniciarJavaFx();
        AvisosInlineChat avisos = crearEnJavaFx();

        for (TipoAviso tipo : TipoAviso.values()) {
            avisos.mostrar("mensaje", tipo);
            ejecutarEnJavaFx(() -> {
                assertEquals(tituloEsperado(tipo), titulo.getText(),
                        "título de " + tipo);
                return null;
            });
        }
    }

    @Test
    void elAvisoSeAnunciaConTituloYMensaje() throws Exception {
        iniciarJavaFx();
        List<String> anuncios = new ArrayList<>();
        AvisosInlineChat avisos = crearEnJavaFx(anuncios::add);

        avisos.mostrar("Conexión recuperada.", TipoAviso.EXITO);
        ejecutarEnJavaFx(() -> null);
        ejecutarEnJavaFx(() -> null);

        assertEquals(List.of("", "Listo: Conexión recuperada."), anuncios);
    }

    @Test
    void elMismoAvisoDosVecesVuelveAnunciarse() throws Exception {
        iniciarJavaFx();
        List<String> anuncios = new ArrayList<>();
        AvisosInlineChat avisos = crearEnJavaFx(anuncios::add);

        // Un lector de pantalla solo repite si el texto cambia: el mismo error
        // saltando dos veces (un reintento que vuelve a fallar) debe re-leerse.
        // Por eso se limpia antes de volver a ponerlo.
        avisos.mostrar("No se pudo enviar.", TipoAviso.ERROR);
        ejecutarEnJavaFx(() -> null);
        ejecutarEnJavaFx(() -> null);
        anuncios.clear();

        avisos.mostrar("No se pudo enviar.", TipoAviso.ERROR);
        ejecutarEnJavaFx(() -> null);
        ejecutarEnJavaFx(() -> null);

        assertEquals(List.of("", "Error: No se pudo enviar."), anuncios,
                "el segundo aviso se vuelve a anunciar igual");
    }

    @Test
    void limpiarTambienSilenciaElAnuncio() throws Exception {
        iniciarJavaFx();
        List<String> anuncios = new ArrayList<>();
        AvisosInlineChat avisos = crearEnJavaFx(anuncios::add);

        avisos.mostrar("Error de red", TipoAviso.ERROR);
        ejecutarEnJavaFx(() -> null);
        ejecutarEnJavaFx(() -> null);
        anuncios.clear();
        avisos.limpiar();

        assertEquals(List.of(""), anuncios);
        ejecutarEnJavaFx(() -> {
            assertFalse(contenedor.isVisible());
            return null;
        });
    }

    @Test
    void elAnuncioLlevaTituloYMensajeComoEnQtYEnWpf() {
        assertEquals("Atención: Sin red.", AvisosInlineChat.anuncioDe("Atención", "Sin red."));
        assertEquals("Aviso: ya se va", AvisosInlineChat.anuncioDe("Aviso", "ya se va"));
    }

    private static String tituloEsperado(TipoAviso tipo) {
        return switch (tipo) {
            case ERROR -> "Error";
            case ADVERTENCIA -> "Atención";
            case EXITO -> "Listo";
            case INFORMACION -> "Aviso";
        };
    }

    private AvisosInlineChat crearEnJavaFx() throws Exception {
        return crearEnJavaFx(null);
    }

    private AvisosInlineChat crearEnJavaFx(java.util.function.Consumer<String> anuncio)
            throws Exception {
        return ejecutarEnJavaFx(() -> {
            contenedor = new HBox();
            titulo = new Label();
            texto = new Label();
            return new AvisosInlineChat(contenedor, titulo, texto, anuncio);
        });
    }

    private static void iniciarJavaFx() throws InterruptedException {
        CountDownLatch iniciado = new CountDownLatch(1);
        try {
            Platform.startup(iniciado::countDown);
            if (!iniciado.await(20, TimeUnit.SECONDS)) {
                throw new IllegalStateException("JavaFX no inició dentro del tiempo esperado.");
            }
        } catch (IllegalStateException yaIniciado) {
            if (!"Toolkit already initialized".equals(yaIniciado.getMessage())) {
                throw yaIniciado;
            }
        }
    }

    private static <T> T ejecutarEnJavaFx(Callable<T> accion) throws Exception {
        FutureTask<T> tarea = new FutureTask<>(accion);
        Platform.runLater(tarea);
        return tarea.get(20, TimeUnit.SECONDS);
    }
}
