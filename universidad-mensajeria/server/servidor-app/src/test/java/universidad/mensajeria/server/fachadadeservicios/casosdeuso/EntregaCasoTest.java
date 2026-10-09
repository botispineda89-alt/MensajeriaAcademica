package universidad.mensajeria.server.fachadadeservicios.casosdeuso;

import org.junit.jupiter.api.Test;
import universidad.mensajeria.common.interno.IdSesion;
import universidad.mensajeria.common.tipos.Mensaje;
import universidad.mensajeria.common.tipos.TipoMensaje;
import universidad.mensajeria.protocolo.InterfazProtocoloComunicacion;
import universidad.mensajeria.server.conexionesclientes.InterfazConexionesClientes;

import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.doAnswer;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

/**
 * Sync multi-PC: la entrega llega al destinatario y como eco a las OTRAS
 * sesiones del remitente, nunca a la sesión origen (ella correlaciona su
 * ACK/IMAGE_FILTERED_RESULT por el mismo id).
 */
class EntregaCasoTest {

    private record Envio(IdSesion sesion, Mensaje mensaje) { }

    private static final class ProtocoloFalso {
        final List<Envio> enviados = new ArrayList<>();
        final InterfazProtocoloComunicacion instancia = mock(
                InterfazProtocoloComunicacion.class);

        ProtocoloFalso() throws Exception {
            doAnswer(inv -> {
                enviados.add(new Envio(inv.getArgument(0), inv.getArgument(1)));
                return null;
            }).when(instancia).enviar(any(), any());
        }
    }

    @Test
    void entregaAlcanzaAlDestinoYAlEcoDeLaOtraSesionDelRemitente() throws Exception {
        IdSesion origen = new IdSesion("s1", "A001", "10.0.0.1");
        IdSesion otraDeA = new IdSesion("s2", "A001", "10.0.0.2");
        IdSesion destino = new IdSesion("s3", "B002", "10.0.0.3");
        InterfazConexionesClientes conexiones = mock(InterfazConexionesClientes.class);
        LinkedHashSet<IdSesion> sesionesA = new LinkedHashSet<>();
        sesionesA.add(origen);
        sesionesA.add(otraDeA);
        when(conexiones.sesionesDe("A001")).thenReturn(sesionesA);
        when(conexiones.sesionesDe("B002")).thenReturn(Set.of(destino));
        ProtocoloFalso protocolo = new ProtocoloFalso();
        EntregaCaso entrega = new EntregaCaso(protocolo.instancia, conexiones);

        Mensaje msg = Mensaje.builder().tipo(TipoMensaje.MENSAJE_TEXTO).id("m1")
                .remitente("A001").destinatario("B002").contenido("hola").build();
        entrega.entregar("B002", msg, origen);

        // Destino + eco a la otra sesión (el acuse va a todas las del remitente).
        List<String> entregas = protocolo.enviados.stream()
                .filter(e -> e.mensaje().tipo() == TipoMensaje.MENSAJE_TEXTO)
                .map(e -> e.sesion().id()).toList();
        assertEquals(List.of("s3", "s2"), entregas);
        List<String> acuses = protocolo.enviados.stream()
                .filter(e -> e.mensaje().tipo() == TipoMensaje.MENSAJE_ENTREGADO)
                .map(e -> e.sesion().id()).toList();
        assertEquals(Set.of("s1", "s2"), new java.util.HashSet<>(acuses));
    }

    @Test
    void sinOtraSesionNoHayEcoPeroSiAcuse() throws Exception {
        IdSesion origen = new IdSesion("s1", "A001", "10.0.0.1");
        InterfazConexionesClientes conexiones = mock(InterfazConexionesClientes.class);
        when(conexiones.sesionesDe("A001")).thenReturn(Set.of(origen));
        when(conexiones.sesionesDe("B002")).thenReturn(Set.of());
        ProtocoloFalso protocolo = new ProtocoloFalso();
        EntregaCaso entrega = new EntregaCaso(protocolo.instancia, conexiones);

        Mensaje msg = Mensaje.builder().tipo(TipoMensaje.MENSAJE_TEXTO).id("m2")
                .remitente("A001").destinatario("B002").contenido("hola").build();
        entrega.entregar("B002", msg, origen);

        List<String> entregas = protocolo.enviados.stream()
                .filter(e -> e.mensaje().tipo() == TipoMensaje.MENSAJE_TEXTO)
                .map(e -> e.sesion().id()).toList();
        assertTrue(entregas.isEmpty());
        assertEquals(1, protocolo.enviados.size());
        assertEquals(TipoMensaje.MENSAJE_ENTREGADO,
                protocolo.enviados.get(0).mensaje().tipo());
    }
}
