package universidad.mensajeria.cliente.presentacion;

import javafx.scene.input.KeyCode;

/**
 * R2: qué hace el compositor con cada tecla.
 * <p>
 * Puro a propósito: el manejador {@code alTeclaEntrada} es privado y vive en un
 * controlador JavaFX, así que sin esto nadie puede testear la regla.
 * </p>
 * <ul>
 *   <li>{@code Enter} sin modificadores → ENVÍA.</li>
 *   <li>{@code Shift+Enter} → NO envía y NO se consume: el {@code TextArea}
 *       inserta su salto de línea. Dejar pasar el evento es justo lo que hace
 *       que el "\n" aparezca; consumirlo lo borraría.</li>
 *   <li>Cualquier otra tecla → no envía, no consume.</li>
 * </ul>
 */
public final class TecladoCompositor {

    private TecladoCompositor() {
    }

    /** true si la tecla debe lanzar el envío. */
    public static boolean debeEnviar(KeyCode codigo, boolean shift, boolean control, boolean alt) {
        return codigo == KeyCode.ENTER && !shift && !control && !alt;
    }

    /**
     * true si la tecla debe consumirse sin enviar.
     * <p>
     * Solo se consume lo que se envía: consumir {@code Shift+Enter} mataría el
     * salto de línea. Punto de extensión si algún día hace falta otra tecla
     * capturada (p. ej. enviar con {@code Ctrl+Enter}).
     * </p>
     */
    public static boolean debeConsumir(KeyCode codigo, boolean shift, boolean control, boolean alt) {
        return debeEnviar(codigo, shift, control, alt);
    }
}