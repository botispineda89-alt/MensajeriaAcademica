package universidad.mensajeria.cliente.presentacion;

import org.junit.jupiter.api.Test;
import javafx.application.Platform;
import javafx.fxml.FXMLLoader;
import javafx.scene.Node;
import javafx.scene.Parent;
import universidad.mensajeria.cliente.fachada.InterfazChatCliente;
import universidad.mensajeria.cliente.fachada.InterfazInicioSesion;
import universidad.mensajeria.cliente.transversal.config.ClienteConfig;

import java.lang.reflect.Proxy;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Properties;
import java.util.List;
import java.util.concurrent.Callable;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executor;
import java.util.concurrent.FutureTask;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Pruebas de regresión para la fábrica de controladores y la inyección de dependencias.
 */
class FabricaControladoresTest {

    private final InterfazInicioSesion inicioSesion = (InterfazInicioSesion) Proxy.newProxyInstance(
            getClass().getClassLoader(),
            new Class<?>[]{InterfazInicioSesion.class},
            (proxy, method, args) -> null);

    private final InterfazChatCliente chat = (InterfazChatCliente) Proxy.newProxyInstance(
            getClass().getClassLoader(),
            new Class<?>[]{InterfazChatCliente.class},
            (proxy, method, args) -> switch (method.getName()) {
                case "codigoActual" -> "160004726";
                case "estadoConexion" -> universidad.mensajeria.cliente.transversal.EstadoConexion.CONECTADO;
                case "conectado" -> true;
                case "directorioLocal", "sincronizarDirectorio", "recientes",
                        "refrescarPresencia", "conversacion", "buscarEnHistorial" -> List.of();
                case "contarConversacion", "cantidadObservadoresEventos" -> 0;
                default -> null;
            });

    private final Executor tareasFondo = Runnable::run;
    private final ClienteConfig config = ClienteConfig.desde(new Properties());
    private final InterfazNavegacionCliente navegacion = (InterfazNavegacionCliente) Proxy.newProxyInstance(
            getClass().getClassLoader(),
            new Class<?>[]{InterfazNavegacionCliente.class},
            (proxy, method, args) -> null);

    @Test
    void creaLoginControllerConDependencias() {
        FabricaControladores fabrica = new FabricaControladores(
                inicioSesion, chat, tareasFondo, config, navegacion);

        Object controller = fabrica.crear(LoginController.class);
        assertNotNull(controller);
        assertTrue(controller instanceof LoginController);
    }

    @Test
    void creaChatControllerConDependencias() {
        FabricaControladores fabrica = new FabricaControladores(
                inicioSesion, chat, tareasFondo, config, navegacion);

        Object controller = fabrica.crear(ChatController.class);
        assertNotNull(controller);
        assertTrue(controller instanceof ChatController);
    }

    @Test
    void claseDesconocidaLanzaIllegalArgumentException() {
        FabricaControladores fabrica = new FabricaControladores(
                inicioSesion, chat, tareasFondo, config, navegacion);

        assertThrows(IllegalArgumentException.class, () -> fabrica.crear(String.class));
    }

    @Test
    void cargaVistasConLaFabricaDeControladores() throws Exception {
        iniciarJavaFx();
        ejecutarEnJavaFx(() -> {
            FabricaControladores fabrica = new FabricaControladores(
                    inicioSesion, chat, tareasFondo, config, navegacion);
            FXMLLoader loginLoader = crearLoader("/vistas/login.fxml", fabrica);
            Parent login = loginLoader.load();
            assertNotNull(login);
            assertTrue(loginLoader.getController() instanceof LoginController);

            FXMLLoader chatLoader = crearLoader("/vistas/chat.fxml", fabrica);
            Parent chatView = chatLoader.load();
            assertNotNull(chatView);
            assertTrue(chatLoader.getController() instanceof ChatController);
            ((ChatController) chatLoader.getController()).detener();
            return null;
        });
    }

    @Test
    void losAvisosVanArribaDeLaListaDeMensajes() throws Exception {
        // J1 (2026-10-14): el banner de sin conexión y el aviso inline van bajo la
        // cabecera, no al pie: abajo competían con el compositor y el aviso se leía
        // tarde. Se comprueba sobre la vista cargada de verdad, no sobre el texto
        // del FXML, para que un `<VBox>` movido de sitio no pase inadvertido.
        iniciarJavaFx();
        ejecutarEnJavaFx(() -> {
            FXMLLoader chatLoader = crearLoader("/vistas/chat.fxml",
                    new FabricaControladores(inicioSesion, chat, tareasFondo, config, navegacion));
            Parent vista = chatLoader.load();
            // La columna derecha del divisor es la conversación; los avisos se buscan
            // por `fx:id` entre sus hijos en vez de por selector CSS.
            javafx.scene.layout.VBox conversacion = (javafx.scene.layout.VBox)
                    ((javafx.scene.control.SplitPane) vista).getItems().get(1);
            Map<String, Integer> posiciones = new LinkedHashMap<>();
            for (Node hijo : conversacion.getChildren()) {
                posiciones.putIfAbsent(hijo.getId(), posiciones.size());
            }
            int lista = posiciones.getOrDefault("listaMensajes", -1);
            int banner = posiciones.getOrDefault("bannerOffline", -1);
            int aviso = posiciones.getOrDefault("avisoInline", -1);

            assertTrue(lista >= 0, "la lista de mensajes tiene que estar en la vista");
            assertTrue(banner >= 0 && banner < lista,
                    "el banner de sin conexión va antes de la lista de mensajes");
            assertTrue(aviso >= 0 && aviso < lista,
                    "el aviso inline va antes de la lista de mensajes");
            ((ChatController) chatLoader.getController()).detener();
            return null;
        });
    }

    private static FXMLLoader crearLoader(String recurso, FabricaControladores fabrica) {
        FXMLLoader loader = new FXMLLoader(FabricaControladoresTest.class.getResource(recurso));
        loader.setControllerFactory(fabrica::crear);
        return loader;
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
