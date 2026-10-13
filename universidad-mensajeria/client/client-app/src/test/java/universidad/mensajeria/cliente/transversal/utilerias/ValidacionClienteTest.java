package universidad.mensajeria.cliente.transversal.utilerias;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertThrows;

/** Validaciones puras de entrada (mismas reglas del servidor). */
class ValidacionClienteTest {

    @Test
    void rechazaEntradasVacias() {
        assertThrows(IllegalArgumentException.class, () -> ValidacionCliente.codigo("  "));
        assertThrows(IllegalArgumentException.class, () -> ValidacionCliente.contrasena("12345"));
        assertThrows(IllegalArgumentException.class, () -> ValidacionCliente.contenido(null));
        assertThrows(IllegalArgumentException.class, () -> ValidacionCliente.destinatario(""));
        assertThrows(IllegalArgumentException.class, () -> ValidacionCliente.imagen(new byte[0]));
    }

    @Test
    void rechazaImagenMayorQueLaTramaUnica() {
        // Invariante 2: una imagen que no cabe en los 16 MiB de la trama tras
        // el base64 no debe salir jamás por el socket — el servidor cortaría
        // la conexión (el bug de la píldora venenosa, roadmap §5.11).
        IllegalArgumentException ex = assertThrows(IllegalArgumentException.class,
                () -> ValidacionCliente.imagen(
                        new byte[(int) (ValidacionCliente.MAXIMO_IMAGEN_TRAMA + 1)]));
        org.junit.jupiter.api.Assertions.assertTrue(
                ex.getMessage().contains("12 MB"));
    }
}
