package universidad.mensajeria.cliente.presentacion;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.io.IOException;
import java.lang.reflect.Proxy;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Base64;
import java.util.List;
import java.util.concurrent.Callable;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executor;
import java.util.concurrent.FutureTask;
import java.util.concurrent.TimeUnit;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import javafx.application.Platform;
import javafx.scene.control.Label;
import javafx.scene.control.ListView;
import javafx.scene.layout.FlowPane;
import universidad.mensajeria.cliente.presentacion.celdas.FilaChat;
import universidad.mensajeria.cliente.transversal.utilerias.TextoCliente;

/**
 * R3: el adjunto de entrada elige VARIOS archivos, los lee en fondo y pinta una
 * tira de miniaturas. Un archivo que no entra no tumba el resto del lote.
 */
class AdjuntoEntradaChatTest {

    private FlowPane tira;
    private ListView<FilaChat> lista;
    private Label etiqueta;
    private List<String> estados;
    private List<AdjuntoPendiente> agregados;
    private final Executor fondo = Runnable::run;

    @BeforeEach
    void montar() throws Exception {
        iniciarJavaFx();
        ejecutarEnJavaFx(() -> {
            tira = new FlowPane();
            lista = new ListView<>();
            etiqueta = new Label();
            return null;
        });
        estados = new ArrayList<>();
        agregados = new ArrayList<>();
    }

    @Test
    void cargarArchivoPreparaElAdjunto() throws Exception {
        Path tmp = Files.createTempFile("adjunto", ".txt");
        Files.writeString(tmp, "contenido");

        ejecutarEnJavaFx(() -> {
            cargador().cargarArchivo(tmp.toFile());
            return null;
        });
        vaciarColaFx();

        assertEquals(1, agregados.size());
        assertEquals(tmp.getFileName().toString(), agregados.get(0).nombre());
        assertEquals("contenido", new String(agregados.get(0).bytes()));
        Files.deleteIfExists(tmp);
    }

    @Test
    void unWebpSeAdjuntaTalCualSinConvertirloAPng() throws Exception {
        // UIX-27: el archivo tiene que viajar tal cual. Antes, al elegirlo, se
        // transcodificaba a PNG y el receptor recibía un "foto.png" que nadie
        // había enviado. El nombre, el mime y los bytes se respetan.
        Path temporal = Files.createTempFile("foto", ".webp");
        try {
            byte[] original = Base64.getDecoder().decode(
                    "UklGRhwAAABXRUJQVlA4TA8AAAAvHcAEAAcQ0f/+ByKi/wEA");
            Files.write(temporal, original);
            Path webp = temporal;

            ejecutarEnJavaFx(() -> {
                cargador().cargarArchivo(webp.toFile());
                return null;
            });
            vaciarColaFx();

            assertEquals(1, agregados.size());
            AdjuntoPendiente adjunto = agregados.get(0);
            assertTrue(adjunto.nombre().endsWith(".webp"),
                    "el nombre conserva la extensión original, no .png");
            assertEquals("image/webp", adjunto.mime());
            assertArrayEquals(original, adjunto.bytes(),
                    "los bytes son los del archivo, no un PNG renombrado");
            assertTrue(adjunto.esImagen(),
                    "y sigue siendo una imagen: viaja como MENSAJE_IMAGEN");
        } catch (Exception fallo) {
            throw new AssertionError(fallo);
        } finally {
            try {
                Files.deleteIfExists(temporal);
            } catch (IOException ignorado) {
                // temporal
            }
        }
    }

    @Test
    void cargaVariosArchivosDeUnaVez() throws Exception {
        Path a = Files.createTempFile("uno", ".png");
        Path b = Files.createTempFile("dos", ".pdf");
        Files.write(a, new byte[]{1, 2, 3});
        Files.write(b, new byte[]{4, 5, 6});

        ejecutarEnJavaFx(() -> {
            cargador().cargarArchivos(List.of(a.toFile(), b.toFile()));
            return null;
        });
        vaciarColaFx();

        assertEquals(2, agregados.size(), "los dos archivos deben entrar");
        assertTrue(agregados.get(0).esImagen(), ".png se reconoce como imagen");
        assertTrue(agregados.get(1).nombre().endsWith(".pdf"));
        assertTrue(etiqueta.getText().startsWith("2 archivos"), "el resumen cuenta los dos");
        Files.deleteIfExists(a);
        Files.deleteIfExists(b);
    }

    @Test
    void archivoGiganteSeRechazaConEstado() throws Exception {
        Path tmp = Files.createTempFile("grande", ".bin");
        try (var raf = new java.io.RandomAccessFile(tmp.toFile(), "rw")) {
            raf.setLength(TextoCliente.TOPE_ADJUNTO_BYTES + 1);
        }

        ejecutarEnJavaFx(() -> {
            cargador().cargarArchivo(tmp.toFile());
            return null;
        });
        vaciarColaFx();

        assertEquals(0, agregados.size(), "no debe entrar un archivo de más de 50 MiB");
        assertEquals(1, estados.size());
        assertTrue(estados.get(0).startsWith("No se pudieron adjuntar"),
                "avisa del rechazo: " + estados.get(0));
        Files.deleteIfExists(tmp);
    }

    @Test
    void unArchivoMaloNoTumbaElLote() throws Exception {
        Path bueno = Files.createTempFile("bueno", ".png");
        Path malo = Files.createTempFile("malo", ".bin");
        Files.write(bueno, new byte[]{1, 2, 3});
        try (var raf = new java.io.RandomAccessFile(malo.toFile(), "rw")) {
            raf.setLength(TextoCliente.TOPE_ADJUNTO_BYTES + 1);
        }

        ejecutarEnJavaFx(() -> {
            cargador().cargarArchivos(List.of(malo.toFile(), bueno.toFile()));
            return null;
        });
        vaciarColaFx();

        assertEquals(1, agregados.size(), "el bueno entra igual");
        assertEquals(bueno.getFileName().toString(), agregados.get(0).nombre());
        assertEquals(1, estados.size());
        assertTrue(estados.get(0).contains("malo"),
                "el aviso nombra el archivo rechazado: " + estados.get(0));
        Files.deleteIfExists(bueno);
        Files.deleteIfExists(malo);
    }

    @Test
    void limpiarVacíaElViewModelYLaVista() throws Exception {
        ejecutarEnJavaFx(() -> {
            cargador().limpiar();
            return null;
        });

        assertTrue(agregados.isEmpty());
        assertEquals("", etiqueta.getText());
    }

    @Test
    void soltarUnaCarpetaNoEsUnError() throws Exception {
        Path carpeta = Files.createTempDirectory("carpeta");

        ejecutarEnJavaFx(() -> {
            cargador().cargarArchivos(List.of(carpeta.toFile()));
            return null;
        });
        vaciarColaFx();

        assertTrue(agregados.isEmpty());
        assertTrue(estados.isEmpty(), "una carpeta no es un error del usuario");
        Files.deleteIfExists(carpeta);
    }

    private CargadorAdjuntoEntradaChat cargador() {
        InterfazChatViewModel vista = (InterfazChatViewModel) Proxy.newProxyInstance(
                getClass().getClassLoader(),
                new Class<?>[]{InterfazChatViewModel.class},
                (proxy, method, args) -> switch (method.getName()) {
                    case "agregarAdjuntos" -> {
                        List<AdjuntoPendiente> nuevos = (List<AdjuntoPendiente>) args[0];
                        agregados.addAll(nuevos);
                        yield nuevos.size();
                    }
                    case "adjuntos" -> List.copyOf(agregados);
                    case "limpiarAdjuntos", "limpiarAdjunto" -> { agregados.clear(); yield null; }
                    default -> null;
                });
        return new CargadorAdjuntoEntradaChat(tira, lista, etiqueta, vista,
                fondo, estados::add);
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

    private static void vaciarColaFx() throws Exception {
        ejecutarEnJavaFx(() -> null);
        ejecutarEnJavaFx(() -> null);
    }
}