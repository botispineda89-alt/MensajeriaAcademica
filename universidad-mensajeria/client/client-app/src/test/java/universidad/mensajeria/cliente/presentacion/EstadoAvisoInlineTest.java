package universidad.mensajeria.cliente.presentacion;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * UIX-26: el aviso inline se cierra solo. Un aviso que se queda pegado
 * tapando la conversación es justo lo que arrastra este arreglo.
 */
class EstadoAvisoInlineTest {

    @Test
    void todosLosAvisosSeQuitanSolosCadaUnoConSuPlazo() {
        EstadoAvisoInline estado = new EstadoAvisoInline();

        for (TipoAviso tipo : TipoAviso.values()) {
            estado.mostrar("aviso", tipo, 1_000);
            assertTrue(estado.debeOcultarseAutomaticamente(),
                    "un aviso permanente acaba tapando el chat");
            assertTrue(estado.duracionMs() > 0);
            assertEquals(estado.duracionMs(), estado.milisegundosRestantes(1_000));
        }
    }

    @Test
    void elErrorTieneMasMargenQueElExito() {
        EstadoAvisoInline estado = new EstadoAvisoInline();

        estado.mostrar("Listo", TipoAviso.EXITO, 0);
        long exito = estado.duracionMs();
        estado.mostrar("No se pudo enviar: disco lleno", TipoAviso.ERROR, 0);

        assertEquals(EstadoAvisoInline.DURACION_ERROR_MS, estado.duracionMs());
        assertTrue(estado.duracionMs() >= exito * 2,
                "un error debe dar tiempo de leerse antes de desaparecer");
    }

    @Test
    void pasadoElPlazoQuedaCero() {
        EstadoAvisoInline estado = new EstadoAvisoInline();
        estado.mostrar("Listo", TipoAviso.EXITO, 1_000);

        assertTrue(estado.milisegundosRestantes(
                1_000 + EstadoAvisoInline.DURACION_EXITO_MS - 1) > 0);
        assertEquals(0, estado.milisegundosRestantes(
                1_000 + EstadoAvisoInline.DURACION_EXITO_MS));
        assertEquals(0, estado.milisegundosRestantes(
                1_000 + EstadoAvisoInline.DURACION_EXITO_MS + 1));
    }

    @Test
    void unAvisoNuevoReiniciaElPlazo() {
        EstadoAvisoInline estado = new EstadoAvisoInline();
        estado.mostrar("Primero", TipoAviso.EXITO, 1_000);

        // Llega otro a los 3 s: el nuevo tiene sus 4 s completos, no lo que
        // le quedaba al anterior.
        estado.mostrar("Segundo", TipoAviso.EXITO, 4_000);

        assertEquals("Segundo", estado.mensaje());
        assertEquals(EstadoAvisoInline.DURACION_EXITO_MS,
                estado.milisegundosRestantes(4_000));
    }

    @Test
    void limpiarDejaElEstadoEnBlancoYNoExpira() {
        EstadoAvisoInline estado = new EstadoAvisoInline();
        estado.mostrar("Listo", TipoAviso.EXITO, 1_000);

        estado.limpiar();

        assertFalse(estado.visible());
        assertEquals("", estado.mensaje());
        assertEquals(-1, estado.milisegundosRestantes(9_999));
    }

    @Test
    void unTextoVacioLimpiaEnLugarDeMostrarEnBlanco() {
        EstadoAvisoInline estado = new EstadoAvisoInline();
        estado.mostrar("Algo", TipoAviso.EXITO, 1_000);

        estado.mostrar("   ", TipoAviso.EXITO, 2_000);

        assertFalse(estado.visible());
    }
}