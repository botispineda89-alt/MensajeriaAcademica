package universidad.mensajeria.cliente.transversal.utilerias;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import org.junit.jupiter.api.Test;

/**
 * R1: el preview de la fila de recientes y la lista de extensiones de imagen.
 * <p>
 * Son funciones puras justamente para poder testearlas sin el toolkit de
 * JavaFX: {@code UsuarioCell} solo las delega.
 * </p>
 */
class TextoClientePreviewTest {

    @Test
    void sinContenidoPoneElAviso() {
        assertEquals("Sin mensajes aún", TextoCliente.previoConversacion(null, false));
        assertEquals("Sin mensajes aún", TextoCliente.previoConversacion("", false));
        assertEquals("Sin mensajes aún", TextoCliente.previoConversacion("   ", false));
        // Tampoco lleva prefijo: no hay mensaje propio, hay nada.
        assertEquals("Sin mensajes aún", TextoCliente.previoConversacion(null, true));
    }

    @Test
    void loMioLlevaPrefijoYLoDeOtroNo() {
        assertEquals("Tú: hola", TextoCliente.previoConversacion("hola", true));
        assertEquals("hola", TextoCliente.previoConversacion("hola", false));
        assertEquals("Tú: ", TextoCliente.PREFIYO_PROPIO);
    }

    @Test
    void colapsaSaltosYEspacios() {
        assertEquals("hola mundo", TextoCliente.previoConversacion("  hola\n  mundo ", false));
        assertEquals("Tú: hola mundo", TextoCliente.previoConversacion("hola\nmundo", true));
    }

    @Test
    void truncaElMensajeAntesDeAnteponerElPrefijo() {
        String largo = "a".repeat(80);

        String salida = TextoCliente.previoConversacion(largo, false);

        assertEquals(TextoCliente.CORTE_PREVIEW, salida.length());
        assertTrue(salida.endsWith("…"));
        // El prefijo no cuenta en el recorte: paridad con UsuarioVM de .NET.
        assertEquals("Tú: " + salida, TextoCliente.previoConversacion(largo, true));
    }

    @Test
    void bmpYWebpSonImagenComoEnElSelector() {
        assertTrue(TextoCliente.esImagenNombre("foto.bmp"));
        assertTrue(TextoCliente.esImagenNombre("FOTO.WEBP"));
        assertTrue(TextoCliente.esImagenNombre("foto.png"));
        assertFalse(TextoCliente.esImagenNombre("informe.pdf"));
        assertFalse(TextoCliente.esImagenNombre("sin_extension"));
        assertFalse(TextoCliente.esImagenNombre(null));
    }
}