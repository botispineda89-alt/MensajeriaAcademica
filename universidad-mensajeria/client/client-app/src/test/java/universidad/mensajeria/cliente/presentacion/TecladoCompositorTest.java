package universidad.mensajeria.cliente.presentacion;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import org.junit.jupiter.api.Test;

import javafx.scene.input.KeyCode;

/**
 * R2: la regla del compositor, sin toolkit.
 * <p>
 * {@code KeyCode} es un enum: no hace falta levantar JavaFX para probarlo.
 * </p>
 */
class TecladoCompositorTest {

    @Test
    void enterSolaEnvia() {
        assertTrue(TecladoCompositor.debeEnviar(KeyCode.ENTER, false, false, false));
    }

    @Test
    void shiftEnterNoEnvia() {
        assertFalse(TecladoCompositor.debeEnviar(KeyCode.ENTER, true, false, false));
        assertFalse(TecladoCompositor.debeConsumir(KeyCode.ENTER, true, false, false),
                "consumir el evento mataría el salto de línea");
    }

    @Test
    void controlYAltNoEnvian() {
        assertFalse(TecladoCompositor.debeEnviar(KeyCode.ENTER, false, true, false));
        assertFalse(TecladoCompositor.debeEnviar(KeyCode.ENTER, false, false, true));
        assertFalse(TecladoCompositor.debeEnviar(KeyCode.ENTER, true, true, true));
    }

    @Test
    void otrasTeclasNoEnvian() {
        assertFalse(TecladoCompositor.debeEnviar(KeyCode.A, false, false, false));
        assertFalse(TecladoCompositor.debeEnviar(KeyCode.TAB, false, false, false));
        assertFalse(TecladoCompositor.debeEnviar(KeyCode.ESCAPE, false, false, false));
        assertFalse(TecladoCompositor.debeEnviar(null, false, false, false));
    }
}