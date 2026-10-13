package universidad.mensajeria.cliente.presentacion;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;

import org.junit.jupiter.api.Test;

import universidad.mensajeria.cliente.presentacion.PlanEnvioAdjuntos.Adjunto;
import universidad.mensajeria.cliente.presentacion.PlanEnvioAdjuntos.Envio;
import universidad.mensajeria.cliente.presentacion.PlanEnvioAdjuntos.Grupo;
import universidad.mensajeria.cliente.presentacion.PlanEnvioAdjuntos.Texto;

/**
 * R3: el plan de envíos se calcula sin JavaFX, sin red y sin disco.
 * <p>
 * Aquí vive la política que antes estaba escondida en el comando: cuántos
 * mensajes salen, en qué orden y cuál lleva el texto como pie.
 * </p>
 */
class PlanEnvioAdjuntosTest {

    private static AdjuntoPendiente archivo(String nombre) {
        return new AdjuntoPendiente(nombre, null, new byte[]{1, 2, 3});
    }

    @Test
    void sinAdjuntosElTextoViajaSolo() {
        List<Envio> plan = PlanEnvioAdjuntos.calcular("hola", List.of());

        assertEquals(1, plan.size());
        assertInstanceOf(Texto.class, plan.get(0));
        assertEquals("hola", ((Texto) plan.get(0)).cuerpo());
    }

    @Test
    void unAdjuntoSeComeElTextoComoCaption() {
        List<Envio> plan = PlanEnvioAdjuntos.calcular("mirar esto", List.of(archivo("a.png")));

        assertEquals(1, plan.size(), "un adjunto + texto = UN mensaje, UNA burbuja");
        Adjunto adj = assertInstanceOf(Adjunto.class, plan.get(0));
        assertEquals("mirar esto", adj.caption());
        assertTrue(adj.archivo().esImagen());
    }

    @Test
    void unAdjuntoSinTextoVaSinCaption() {
        List<Envio> plan = PlanEnvioAdjuntos.calcular("   ", List.of(archivo("a.png")));

        assertEquals(1, plan.size());
        assertNull(((Adjunto) plan.get(0)).caption());
    }

    @Test
    void variosAdjuntosSacanElTextoAdelante() {
        List<Envio> plan = PlanEnvioAdjuntos.calcular("dos cosas",
                List.of(archivo("a.png"), archivo("b.pdf")));

        assertEquals(3, plan.size());
        assertInstanceOf(Texto.class, plan.get(0));
        assertEquals("a.png", ((Adjunto) plan.get(1)).archivo().nombre());
        assertTrue(((Adjunto) plan.get(1)).archivo().esImagen());
        assertNull(((Adjunto) plan.get(1)).caption(), "el caption es de un solo mensaje");
        assertEquals("b.pdf", ((Adjunto) plan.get(2)).archivo().nombre());
        assertFalse(((Adjunto) plan.get(2)).archivo().esImagen());
        assertNull(((Adjunto) plan.get(2)).caption());
    }

    @Test
    void variosAdjuntosSinTextoNoInventanMensajes() {
        List<Envio> plan = PlanEnvioAdjuntos.calcular("  ", List.of(archivo("a.png"), archivo("b.pdf")));

        assertEquals(2, plan.size());
    }

    @Test
    void vacioNoDevuelveNada() {
        assertTrue(PlanEnvioAdjuntos.vacio("", List.of()));
        assertTrue(PlanEnvioAdjuntos.vacio("  ", List.of()));
        assertTrue(PlanEnvioAdjuntos.vacio(null, null));
        assertFalse(PlanEnvioAdjuntos.vacio("hola", List.of()));
        assertFalse(PlanEnvioAdjuntos.vacio("", List.of(archivo("a.png"))));
    }

    @Test
    void elOrdenDeLosAdjuntosSeRespeta() {
        List<Envio> plan = PlanEnvioAdjuntos.calcular(null,
                List.of(archivo("1.png"), archivo("2.png"), archivo("3.png")));

        assertEquals(1, plan.size(), "tres imágenes = UNA sola burbuja");
        Grupo grupo = assertInstanceOf(Grupo.class, plan.get(0));
        assertEquals("1.png", grupo.imagenes().get(0).nombre());
        assertEquals("3.png", grupo.imagenes().get(2).nombre());
    }

    @Test
    void r3VariasImagenesConTextoVanEnUnaSolaBurbuja() {
        List<Envio> plan = PlanEnvioAdjuntos.calcular("mira esto",
                List.of(archivo("a.png"), archivo("b.png"), archivo("c.png")));

        assertEquals(1, plan.size(), "una sola trama, una sola burbuja");
        Grupo grupo = assertInstanceOf(Grupo.class, plan.get(0));
        assertEquals("mira esto", grupo.caption(), "el texto es el pie de la burbuja");
    }

    @Test
    void r3VariasImagenesSinTextoVanEnUnaSolaBurbuja() {
        List<Envio> plan = PlanEnvioAdjuntos.calcular("  ", List.of(archivo("a.png"), archivo("b.jpg")));

        assertEquals(1, plan.size());
        Grupo grupo = assertInstanceOf(Grupo.class, plan.get(0));
        assertNull(grupo.caption());
        assertEquals(2, grupo.imagenes().size());
    }

    @Test
    void r3UnaSolaImagenNoSeAgrupa() {
        List<Envio> plan = PlanEnvioAdjuntos.calcular("pie", List.of(archivo("a.png")));

        assertEquals(1, plan.size());
        Adjunto adj = assertInstanceOf(Adjunto.class, plan.get(0));
        assertEquals("pie", adj.caption());
    }

    @Test
    void r3ImagenesYDocumentosDanUnaBurbujaDeImagenesYUnaPorDocumento() {
        List<Envio> plan = PlanEnvioAdjuntos.calcular("dos cosas",
                List.of(archivo("a.png"), archivo("b.png"), archivo("informe.pdf")));

        assertEquals(3, plan.size(), "texto + grupo + un documento");
        assertInstanceOf(Texto.class, plan.get(0));
        assertInstanceOf(Grupo.class, plan.get(1));
        assertEquals("informe.pdf", ((Adjunto) plan.get(2)).archivo().nombre());
    }

    @Test
    void r3DosImagenesYUnDocumentoSinTexto() {
        List<Envio> plan = PlanEnvioAdjuntos.calcular(null,
                List.of(archivo("a.png"), archivo("b.png"), archivo("informe.pdf")));

        assertEquals(2, plan.size());
        assertInstanceOf(Grupo.class, plan.get(0));
        assertInstanceOf(Adjunto.class, plan.get(1));
    }

    @Test
    void elAdjuntoRechazaLoInvalido() {
        assertThrows(IllegalArgumentException.class,
                () -> new AdjuntoPendiente("vacio.png", null, new byte[0]));
        assertThrows(IllegalArgumentException.class,
                () -> new AdjuntoPendiente(" ", null, new byte[]{1}));
        assertThrows(IllegalArgumentException.class, () -> new AdjuntoPendiente(
                "gigante.bin", null, new byte[(int) AdjuntoPendiente.TOPE_BYTES + 1]));
    }

    @Test
    void elMimeSeDeduceDelNombreSiNoViene() {
        assertEquals("image/png", archivo("a.png").mime());
        assertEquals("application/pdf", archivo("b.pdf").mime());
    }
}