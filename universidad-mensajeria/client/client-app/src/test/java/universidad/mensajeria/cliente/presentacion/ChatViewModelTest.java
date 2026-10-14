package universidad.mensajeria.cliente.presentacion;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import universidad.mensajeria.cliente.fachada.InterfazChatCliente;
import universidad.mensajeria.cliente.transversal.EstadoConexion;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;
import universidad.mensajeria.cliente.transversal.records.RecienteChat;
import universidad.mensajeria.cliente.transversal.records.UsuarioLocal;

import java.lang.reflect.Proxy;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.atomic.AtomicInteger;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Pruebas unitarias para ChatViewModel (Fase 3):
 * Valida el estado observable, filtros y coordinación sin requerir JavaFX UI ni red real.
 */
class ChatViewModelTest {

    private ChatViewModel viewModel;
    private final List<String> textosEnviados = new ArrayList<>();
    private final AtomicInteger observadoresEstado = new AtomicInteger();
    private String ultimoDestinatario;

    @BeforeEach
    void setup() {
        textosEnviados.clear();
        observadoresEstado.set(0);
        InterfazChatCliente mockChat = (InterfazChatCliente) Proxy.newProxyInstance(
                getClass().getClassLoader(),
                new Class<?>[]{InterfazChatCliente.class},
                (proxy, method, args) -> {
                    String nombre = method.getName();
                    if ("estadoConexion".equals(nombre)) {
                        return EstadoConexion.CONECTADO;
                    }
                    if ("alCambiarEstado".equals(nombre)) {
                        observadoresEstado.incrementAndGet();
                        return null;
                    }
                    if ("quitarObservadorEstado".equals(nombre)) {
                        observadoresEstado.decrementAndGet();
                        return null;
                    }
                    if ("codigoActual".equals(nombre)) {
                        return "160004726";
                    }
                    if ("enviarTexto".equals(nombre)) {
                        ultimoDestinatario = (String) args[0];
                        textosEnviados.add((String) args[1]);
                        return true;
                    }
                    return null;
                });

        viewModel = new ChatViewModel(mockChat, Runnable::run);
    }

    @Test
    void estadoInicialEsValido() {
        assertNotNull(viewModel);
        assertEquals("160004726", viewModel.codigoActual());
        assertFalse(viewModel.enviandoProperty().get());
        assertFalse(viewModel.adjuntoDisponibleProperty().get());
        assertFalse(viewModel.hayDestinoProperty().get());
        assertEquals(EstadoConexion.CONECTADO, viewModel.estadoConexionProperty().get());
        assertNull(viewModel.respuestaSeleccionadaProperty().get());
    }

    @Test
    void detenerEliminaLaSuscripcionDeEstado() {
        assertEquals(1, observadoresEstado.get());

        viewModel.detener();

        assertEquals(0, observadoresEstado.get());
    }

    @Test
    void prepararYLimpiarAdjuntoActualizaPropiedades() {
        byte[] datos = "datos adjunto".getBytes(StandardCharsets.UTF_8);
        viewModel.prepararAdjunto(datos, "archivo.txt", "text/plain");

        assertTrue(viewModel.adjuntoDisponibleProperty().get());
        assertEquals("archivo.txt", viewModel.adjuntoNombre());
        assertEquals("text/plain", viewModel.adjuntoMime());
        assertEquals(datos, viewModel.adjuntoBytes());

        viewModel.limpiarAdjunto();
        assertFalse(viewModel.adjuntoDisponibleProperty().get());
        assertNull(viewModel.adjuntoNombre());
        assertNull(viewModel.adjuntoBytes());
    }

    @Test
    void r3LosAdjuntosSeAcumulanYSeQuitanUnoAUno() {
        List<AdjuntoPendiente> lista = List.of(
                new AdjuntoPendiente("a.png", null, new byte[]{1}),
                new AdjuntoPendiente("b.pdf", null, new byte[]{2}));

        assertEquals(2, viewModel.agregarAdjuntos(lista));
        assertEquals(2, viewModel.adjuntos().size());
        assertTrue(viewModel.adjuntoDisponibleProperty().get());

        viewModel.quitarAdjunto(0);
        assertEquals(1, viewModel.adjuntos().size());
        assertEquals("b.pdf", viewModel.adjuntos().get(0).nombre());

        viewModel.limpiarAdjuntos();
        assertTrue(viewModel.adjuntos().isEmpty());
        assertFalse(viewModel.adjuntoDisponibleProperty().get());
    }

    @Test
    void r3SeRecortaAlCupoSinPerderLoQueCabe() {
        List<AdjuntoPendiente> lote = new ArrayList<>();
        for (int i = 0; i < AdjuntoPendiente.MAXIMO_ARCHIVOS + 3; i++) {
            lote.add(new AdjuntoPendiente("f" + i + ".png", null, new byte[]{1}));
        }

        int aceptados = viewModel.agregarAdjuntos(lote);

        assertEquals(AdjuntoPendiente.MAXIMO_ARCHIVOS, aceptados);
        assertEquals(AdjuntoPendiente.MAXIMO_ARCHIVOS, viewModel.adjuntos().size());
        assertEquals(0, viewModel.agregarAdjuntos(lote), "ya está lleno: no entra ni uno más");
    }

    @Test
    void r3LaListaDevuelveCopiaDefensiva() {
        viewModel.agregarAdjuntos(List.of(new AdjuntoPendiente("a.png", null, new byte[]{1})));

        java.util.List<AdjuntoPendiente> copia = viewModel.adjuntos();
        org.junit.jupiter.api.Assertions.assertThrows(UnsupportedOperationException.class,
                () -> copia.add(new AdjuntoPendiente("b.png", null, new byte[]{2})),
                "el compositor no debe poder mutar el estado del ViewModel");
    }

    @Test
    void seleccionDeRespuestaYCancelacion() {
        MensajeLocal msg = new MensajeLocal("m1", "160004716", "160004726", "MENSAJE_TEXTO",
                "hola alex", null, 9, 2, null, null, null,
                "2026-01-01T10:00:00", false, true, "ENTREGADO", null);

        viewModel.seleccionarRespuesta(msg);
        assertEquals(msg, viewModel.respuestaSeleccionadaProperty().get());

        viewModel.cancelarRespuesta();
        assertNull(viewModel.respuestaSeleccionadaProperty().get());
    }

    @Test
    void puedeEnviarValidaCombinaciones() {
        assertFalse(ChatViewModel.puedeEnviar("", false, false, false));
        assertFalse(ChatViewModel.puedeEnviar("hola", false, false, false));
        assertFalse(ChatViewModel.puedeEnviar("hola", false, true, true), "No si está enviando");
        assertTrue(ChatViewModel.puedeEnviar("hola", false, true, false));
        assertTrue(ChatViewModel.puedeEnviar("", true, true, false), "Válido si hay imagen aunque texto esté vacío");
    }

    @Test
    void filtrarContactosAplicaBusquedaYFiltros() {
        viewModel.directorio().put("A01", new UsuarioLocal("A01", "Alex", "Pineda", "Sistemas", true, null, null));
        viewModel.directorio().put("B02", new UsuarioLocal("B02", "Joan", "Garcia", "Sistemas", false, null, null));

        List<RecienteChat> lista = List.of(
                new RecienteChat("A01", "hola", "MENSAJE_TEXTO", "2026-01-01T10:00:00", 0, true, false),
                new RecienteChat("B02", "buenas", "MENSAJE_TEXTO", "2026-01-01T10:05:00", 2, false, true)
        );

        // Búsqueda por nombre
        List<RecienteChat> porNombre = viewModel.filtrar(lista, "Alex", "Todos");
        assertEquals(1, porNombre.size());
        assertEquals("A01", porNombre.get(0).otro());

        // Filtro por conectados
        List<RecienteChat> conectados = viewModel.filtrar(lista, "", "Conectados");
        assertEquals(1, conectados.size());
        assertEquals("A01", conectados.get(0).otro());

        // Filtro por no leídos
        List<RecienteChat> noLeidos = viewModel.filtrar(lista, "", "No leídos");
        assertEquals(1, noLeidos.size());
        assertEquals("B02", noLeidos.get(0).otro());
    }

    @Test
    void filtrarExcluyeLaSesionPropiaAunqueTengaFilaEnElHistorial() {
        // JV-1: el servidor acepta A→A y el dato se conserva, pero la tarjeta
        // propia no se pinta (la sesión era "160004726" en el doble).
        List<RecienteChat> lista = List.of(
                new RecienteChat("160004726", "nota para mí", "MENSAJE_TEXTO",
                        "2026-01-01T10:00:00", 0, false, true),
                new RecienteChat("B02", "buenas", "MENSAJE_TEXTO",
                        "2026-01-01T10:05:00", 2, false, true)
        );

        List<RecienteChat> resultado = viewModel.filtrar(lista, "", "Todos");

        assertEquals(1, resultado.size());
        assertEquals("B02", resultado.get(0).otro());
    }

    @Test
    void enviarTextoConRespuestaFormateaEncabezado() throws Exception {
        MensajeLocal original = new MensajeLocal("m1", "160004716", "160004726", "MENSAJE_TEXTO",
                "hola alex", null, 9, 2, null, null, null,
                "2026-01-01T10:00:00", false, true, "ENTREGADO", null);
        viewModel.seleccionarRespuesta(original);

        boolean ok = viewModel.enviarTexto("160004716", "respondiendo");
        assertTrue(ok);
        assertEquals("160004716", ultimoDestinatario);
        assertEquals(1, textosEnviados.size());
        assertTrue(textosEnviados.get(0).contains("Respuesta a"));
        assertTrue(textosEnviados.get(0).contains("respondiendo"));
    }
}
