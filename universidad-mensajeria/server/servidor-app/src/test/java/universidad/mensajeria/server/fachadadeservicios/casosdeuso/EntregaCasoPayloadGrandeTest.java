package universidad.mensajeria.server.fachadadeservicios.casosdeuso;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import universidad.mensajeria.common.interno.IdSesion;
import universidad.mensajeria.common.tipos.Mensaje;
import universidad.mensajeria.common.tipos.TipoMensaje;
import universidad.mensajeria.protocolo.InterfazProtocoloComunicacion;
import universidad.mensajeria.server.conexionesclientes.InterfazConexionesClientes;

import java.lang.reflect.InvocationHandler;
import java.lang.reflect.Method;
import java.lang.reflect.Proxy;
import java.util.ArrayList;
import java.util.List;
import java.util.Set;

/**
 * Un adjunto grande no cabe en UNA trama (invariante 2: 16 MiB). El servidor lo
 * entrega dentro del mismo frame que los metadatos, asi que antes de este trabajo
 * la entrega fallaba y ademas <b>mataba la sesion del destinatario</b>: un
 * archivo de 20 MiB cerraba la sesion de quien lo recibia.
 * <p>
 * Aqui se comprueba el reparto: los bytes se quedan fuera, el pie se conserva (en
 * un adjunto {@code contenido} es el caption, no el archivo), el {@code archivoId}
 * sigue para que el cliente lo baje, y la sesion sobrevive.
 * </p>
 */
class EntregaCasoPayloadGrandeTest {

    private static final long TOPE_TRAMA = 16L * 1024 * 1024;
    private static final IdSesion SESION = new IdSesion("s1", "B", "127.0.0.1");

    /**
     * Doble del protocolo que falla como el codec cuando la trama no cabe.
     * <p>
     * Proxy dinamico y no una clase: la interfaz tiene mas de diez metodos y aqui
     * solo importa {@code enviar}. Implementarlos todos en el test lo que haria es
     * esbozar de mas cosas que no se estan probando.
     * </p>
     */
    private static final class ProtocoloDoble implements InvocationHandler {
        private final List<Mensaje> recibidas;

        ProtocoloDoble(List<Mensaje> recibidas) {
            this.recibidas = recibidas;
        }

        InterfazProtocoloComunicacion como() {
            return (InterfazProtocoloComunicacion) Proxy.newProxyInstance(
                    getClass().getClassLoader(),
                    new Class<?>[]{InterfazProtocoloComunicacion.class}, this);
        }

        @Override
        public Object invoke(Object proxy, Method metodo, Object[] args) {
            if ("enviar".equals(metodo.getName())) {
                Mensaje m = (Mensaje) args[1];
                int bytes = m.contenidoImagen() == null ? 0 : m.contenidoImagen().length();
                if (bytes > TOPE_TRAMA) {
                    throw new IllegalStateException("Payload de " + bytes
                            + " bytes excede el maximo de " + TOPE_TRAMA);
                }
                recibidas.add(m);
                return null;
            }
            return valorPorDefecto(metodo.getReturnType());
        }
    }

    /** Sesiones con una unica sesion viva y contador de expulsiones. */
    private static final class Sesiones implements InvocationHandler {
        int eliminadas;

        InterfazConexionesClientes como() {
            return (InterfazConexionesClientes) Proxy.newProxyInstance(
                    getClass().getClassLoader(),
                    new Class<?>[]{InterfazConexionesClientes.class}, this);
        }

        @Override
        public Object invoke(Object proxy, Method metodo, Object[] args) {
            return switch (metodo.getName()) {
                // Solo "B" tiene la sesión viva: el emisor "A" está en otra
                // máquina, así que no hay eco ni acuse que contar.
                case "sesionesDe" -> "B".equals(args[0]) ? Set.of(SESION) : Set.<IdSesion>of();
                case "remover" -> { eliminadas++; yield Boolean.TRUE; }
                case "codigosConectados" -> List.of();
                default -> valorPorDefecto(metodo.getReturnType());
            };
        }
    }

    private static Object valorPorDefecto(Class<?> tipo) {
        if (!tipo.isPrimitive()) {
            return null;
        }
        return tipo == boolean.class ? Boolean.FALSE : 0;
    }

    @Test
    @DisplayName("Un adjunto grande se entrega sin los bytes y sin matar la sesion")
    void entregaLigeraSinMatarLaSesion() {
        List<Mensaje> recibidas = new ArrayList<>();
        Sesiones sesiones = new Sesiones();

        Mensaje grande = Mensaje.builder()
                .tipo(TipoMensaje.MENSAJE_ARCHIVO)
                .id("m-20mb")
                .remitente("A")
                .destinatario("B")
                .nombreArchivo("adjunto.bin")
                .mime("application/octet-stream")
                .contenido("pie del adjunto")
                .contenidoImagen("A".repeat((int) TOPE_TRAMA + 1024))
                .archivoId("7")
                .hashSha256("abc")
                .tamanoArchivo(20L * 1024 * 1024)
                .build();

        new EntregaCaso(new ProtocoloDoble(recibidas).como(), sesiones.como())
                .entregar("B", grande, null);

        assertEquals(1, recibidas.size(), "el mensaje llego");
        Mensaje ligero = recibidas.get(0);
        assertNull(ligero.contenidoImagen(), "los bytes viajan con DESCARGAR_ARCHIVO");
        assertEquals("pie del adjunto", ligero.contenido(), "el pie NO es el archivo");
        assertEquals("adjunto.bin", ligero.nombreArchivo());
        assertEquals("7", ligero.archivoId(), "sin archivoId el cliente no puede bajarlo");
        assertEquals(20L * 1024 * 1024, ligero.tamanoArchivo());
        assertEquals(0, sesiones.eliminadas, "NO se expulsa al destinatario");
    }

    @Test
    @DisplayName("Un adjunto pequeno sigue yendo en linea como antes")
    void adjuntoPequenoNoCambia() {
        List<Mensaje> recibidas = new ArrayList<>();
        Sesiones sesiones = new Sesiones();

        Mensaje pequeno = Mensaje.builder()
                .tipo(TipoMensaje.MENSAJE_IMAGEN)
                .id("m-img")
                .remitente("A")
                .destinatario("B")
                .nombreArchivo("foto.png")
                .contenidoImagen("base64chiquita")
                .build();

        new EntregaCaso(new ProtocoloDoble(recibidas).como(), sesiones.como())
                .entregar("B", pequeno, null);

        assertEquals(1, recibidas.size());
        assertEquals("base64chiquita", recibidas.get(0).contenidoImagen(),
                "por debajo del tope no se toca nada");
        assertEquals(0, sesiones.eliminadas);
    }

    @Test
    @DisplayName("Un mensaje sin bytes no se reintenta: la entrega es la de siempre")
    void mensajeSinBytesNoSeReintenta() {
        List<Mensaje> recibidas = new ArrayList<>();

        new EntregaCaso(new ProtocoloDoble(recibidas).como(), new Sesiones().como())
                .entregar("B", Mensaje.builder().tipo(TipoMensaje.MENSAJE_TEXTO)
                        .id("t-1").remitente("A").destinatario("B").contenido("hola").build(),
                        null);

        assertEquals(1, recibidas.size());
        assertEquals("hola", recibidas.get(0).contenido());
    }

    @Test
    @DisplayName("sinContenido conserva todo menos los bytes")
    void sinContenidoConservaLosMetadatos() {
        Mensaje original = Mensaje.builder()
                .tipo(TipoMensaje.MENSAJE_ARCHIVO).id("x").fechaHora("2026-10-13T10:00:00")
                .remitente("A").destinatario("B").contenido("pie")
                .contenidoImagen("pesado").hashSha256("h").numCaracteres(3)
                .nombreArchivo("n.bin").tamanoArchivo(9L).archivoId("7").mime("m")
                .etapasFiltrado("[]").pagina(1).totalPaginas(2).exito(true)
                .ipRemitente("1.2.3.4").tipoOriginal("T").build();

        Mensaje ligero = EntregaCaso.sinContenido(original);

        assertNull(ligero.contenidoImagen());
        assertNotNull(ligero.etapasFiltrado());
        assertEquals(original.tipo(), ligero.tipo());
        assertEquals(original.archivoId(), ligero.archivoId());
        assertEquals(original.tamanoArchivo(), ligero.tamanoArchivo());
        assertEquals(original.ipRemitente(), ligero.ipRemitente());
        assertEquals(original.tipoOriginal(), ligero.tipoOriginal());
        assertNull(EntregaCaso.sinContenido(ligero).contenidoImagen());
    }
}