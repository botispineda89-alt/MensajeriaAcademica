package universidad.mensajeria.cliente.presentacion;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.lang.reflect.Proxy;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.Callable;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.Executor;
import java.util.concurrent.FutureTask;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.function.Consumer;

import javafx.application.Platform;
import universidad.mensajeria.cliente.presentacion.PlanEnvioAdjuntos.Adjunto;
import universidad.mensajeria.cliente.presentacion.PlanEnvioAdjuntos.Envio;
import universidad.mensajeria.cliente.presentacion.PlanEnvioAdjuntos.Texto;
import universidad.mensajeria.cliente.presentacion.comandos.EnviarMensajeComando;

/**
 * §7 paridad: cancelar un lote no pierde adjuntos.
 * <p>
 * El botón dice "Cancelar envío"; si lo no enviado se quedara en el limbo, el
 * usuario perdería archivos que ya tenía preparados, y eso es peor que no poder
 * cancelar. Lo que se comprueba aquí es que lo enviado <b>sale</b>, lo cancelado
 * <b>vuelve</b> en {@code restantes()} y el aviso final lo dice.
 * </p>
 * <p>
 * El executor es {@code Platform::runLater} a propósito: un
 * {@code javafx.concurrent.Task} solo se ejecuta si el executor lo despacha al
 * hilo de JavaFX, y con un executor directo {@code call()} no se invoca en
 * absoluto. Por eso las pruebas vacían la cola de JavaFX antes de afirmar.
 * </p>
 */
class EnvioLoteCancelacionTest {

    private final Executor fondo = Platform::runLater;
    private final List<String> enviados = new ArrayList<>();
    private final List<ProgresoEnvio> avances = new ArrayList<>();
    private final List<String> errores = new ArrayList<>();
    private final AtomicBoolean enRed = new AtomicBoolean(true);

    @BeforeEach
    void red() throws Exception {
        iniciarJavaFx();
        enRed.set(true);
    }

    @Test
    @DisplayName("Cancelar deja en «restantes» lo que no llegó a salir y no lo envía")
    void cancelarDevuelveLoNoEnviado() throws Exception {
        List<Envio> plan = List.of(
                texto("hola"),
                archivo("uno.txt", 1_024),
                archivo("dos.txt", 2_048),
                archivo("tres.txt", 4_096));

        // El botón cancela el comando en vuelo. Aquí se pulsa en el instante
        // exacto en que un usuario lo haría: recién cerrado el primer adjunto.
        AtomicBoolean cancelado = new AtomicBoolean();
        EnviarMensajeComando[] enVuelo = new EnviarMensajeComando[1];
        InterfazChatViewModel vista = vista(alEnviar -> {
            if (alEnviar.equals("archivo:uno.txt")) {
                cancelado.set(true);
                enVuelo[0].cancelar();
            }
        });
        enVuelo[0] = comando(vista, plan);

        enVuelo[0].ejecutar();
        vaciarColaFx();

        assertTrue(cancelado.get(), "el lote se canceló a mitad");
        assertEquals(List.of("texto:hola", "archivo:uno.txt"), enviados,
                "solo sale lo que se alcanzó a intentar");
        assertEquals(List.of("dos.txt", "tres.txt"),
                enVuelo[0].restantes().stream().map(AdjuntoPendiente::nombre).toList());
        assertEquals(1, errores.size(), "el aviso final avisa del envío parcial");
        assertTrue(errores.get(0).contains("cancelado por el usuario"), errores.get(0));
        // 2 de 4: los dos que no salieron. Los otros dos sí se enviaron.
        assertTrue(errores.get(0).contains("2 de 4"), errores.get(0));
    }

    @Test
    @DisplayName("Un lote que se completa entero no deja restos")
    void loteCompletoNoDejaRestos() throws Exception {
        EnviarMensajeComando comando = comando(vista(null), List.of(
                archivo("a.txt", 10), archivo("b.txt", 20)));

        comando.ejecutar();
        vaciarColaFx();

        assertTrue(comando.restantes().isEmpty());
        assertTrue(errores.isEmpty(), "sin errores: " + errores);
        assertEquals(List.of("archivo:a.txt", "archivo:b.txt"), enviados);
    }

    @Test
    @DisplayName("Sin red el lote termina en cola y no es un fallo duro")
    void sinRedQuedaEnCola() throws Exception {
        enRed.set(false);
        EnviarMensajeComando comando = comando(vista(null), List.of(archivo("a.txt", 8)));

        comando.ejecutar();
        vaciarColaFx();

        assertEquals(1, errores.size());
        assertTrue(errores.get(0).contains("quedó en cola para reintentar"), errores.get(0));
        assertTrue(comando.restantes().isEmpty(),
                "lo que quedó en cola no vuelve a la bandeja: sale solo en el reintento");
    }

    @Test
    @DisplayName("Un fallo de un adjunto no impide que salgan los demás")
    void unFalloNoParaElLote() throws Exception {
        EnviarMensajeComando comando = comando(vista(alEnviar -> {
            if (alEnviar.endsWith("b.txt")) {
                throw new IllegalStateException("red caída");
            }
        }), List.of(archivo("a.txt", 10), archivo("b.txt", 20), archivo("c.txt", 30)));

        comando.ejecutar();
        vaciarColaFx();

        assertEquals(List.of("archivo:a.txt", "archivo:b.txt", "archivo:c.txt"), enviados);
        assertEquals(1, errores.size());
        assertTrue(errores.get(0).contains("1 de 3"), errores.get(0));
        assertTrue(errores.get(0).contains("b.txt"), errores.get(0));
    }

    @Test
    @DisplayName("El progreso pesa por bytes, no por número de adjuntos")
    void progresoPonderadoPorBytes() throws Exception {
        EnviarMensajeComando comando = comando(vista(null), List.of(
                archivo("chico.txt", 1_000), archivo("grande.bin", 9_000)));

        comando.ejecutar();
        vaciarColaFx();

        assertFalse(avances.isEmpty(), "el lote informa su avance");
        ProgresoEnvio trasPrimero = avances.stream()
                .filter(p -> p.indiceActual() == 1).findFirst().orElseThrow();
        // 1 KB de 10 KB: si se contaran adjuntos en vez de bytes, esto sería 50 %.
        assertEquals(1_000L, trasPrimero.bytesEnviados());
        assertEquals(10_000L, trasPrimero.bytesTotales());
        assertEquals(0.1d, trasPrimero.fraccionGlobal(), 0.0001d);
        assertEquals("Enviando 1/2 · 0.0/0.0 MB · chico.txt", trasPrimero.texto());

        ProgresoEnvio ultimo = avances.get(avances.size() - 1);
        assertEquals(10_000L, ultimo.bytesEnviados());
        assertEquals(1.0d, ultimo.fraccionGlobal(), 0.0001d);
    }

    private EnviarMensajeComando comando(InterfazChatViewModel vista, List<Envio> plan) {
        return new EnviarMensajeComando(vista, fondo, "DEST", plan, null, enRed::set,
                errores::add, avances::add, null);
    }

    /**
     * Doble mínimo del ViewModel: registra lo enviado, avisa a {@code alEnviar} y
     * devuelve lo que marque {@link #enRed} (false = quedó en cola local).
     */
    private InterfazChatViewModel vista(Consumer<String> alEnviar) {
        return (InterfazChatViewModel) Proxy.newProxyInstance(
                getClass().getClassLoader(),
                new Class<?>[]{InterfazChatViewModel.class},
                (proxy, method, args) -> {
                    String metodo = method.getName();
                    if (metodo.equals("enviarTexto") || metodo.equals("enviarArchivo")
                            || metodo.equals("enviarImagen")) {
                        String etiqueta = metodo.equals("enviarTexto")
                                ? "texto:" + args[1] : "archivo:" + args[2];
                        enviados.add(etiqueta);
                        if (alEnviar != null) {
                            alEnviar.accept(etiqueta);
                        }
                        return enRed.get();
                    }
                    if (method.getReturnType() == boolean.class) {
                        return false;
                    }
                    return null;
                });
    }

    private static Envio texto(String cuerpo) {
        return new Texto(cuerpo);
    }

    private static Envio archivo(String nombre, int bytes) {
        return new Adjunto(new AdjuntoPendiente(nombre, "text/plain", new byte[bytes]), null);
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