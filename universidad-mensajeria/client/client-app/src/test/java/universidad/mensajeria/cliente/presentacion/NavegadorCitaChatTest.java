package universidad.mensajeria.cliente.presentacion;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.LocalDateTime;

import org.junit.jupiter.api.Test;
import universidad.mensajeria.cliente.transversal.records.EstadoMensaje;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;
import universidad.mensajeria.cliente.transversal.records.TipoMensajeCliente;

class NavegadorCitaChatTest {

    @Test
    void analizaCitaConCodigoYContenido() {
        NavegadorCitaChat.Referencia referencia = NavegadorCitaChat.parsearCita(
                "Respuesta a Ana Perez [B002]: \"Hola\"");

        assertNotNull(referencia);
        assertTrue(NavegadorCitaChat.coincide(mensaje("B002", "Hola"), referencia,
                codigo -> "Ana Perez [" + codigo + "]"));
    }

    @Test
    void citaTruncadaCoincideConLaRepresentacionDelMensajeOriginal() {
        String contenido = "a".repeat(140);
        NavegadorCitaChat.Referencia referencia = NavegadorCitaChat.parsearCita(
                "Respuesta a B002: \"" + "a".repeat(119) + "…\"");

        assertNotNull(referencia);
        assertTrue(NavegadorCitaChat.coincide(mensaje("B002", contenido), referencia,
                codigo -> codigo));
    }

    @Test
    void analizaCitaDeImagenPorNombreDeArchivo() {
        MensajeLocal imagen = new MensajeLocal("imagen-1", "B002", "A001",
                TipoMensajeCliente.MENSAJE_IMAGEN.name(), "", null,
                0, 0, "foto.png", null, 10L, LocalDateTime.now().toString(),
                false, true, EstadoMensaje.ENTREGADO.name());
        NavegadorCitaChat.Referencia referencia = NavegadorCitaChat.parsearCita(
                "Respuesta a B002: [Imagen: foto.png]");

        assertNotNull(referencia);
        assertTrue(NavegadorCitaChat.coincide(imagen, referencia, codigo -> codigo));
    }

    @Test
    void descartaCitaDeOtroRemitenteYFormatoInvalido() {
        NavegadorCitaChat.Referencia referencia = NavegadorCitaChat.parsearCita(
                "Respuesta a Ana Perez [B002]: \"Hola\"");

        assertNotNull(referencia);
        assertFalse(NavegadorCitaChat.coincide(mensaje("C003", "Hola"), referencia,
                codigo -> "Ana Perez [" + codigo + "]"));
        assertNull(NavegadorCitaChat.parsearCita("texto normal"));
    }

    private static MensajeLocal mensaje(String origen, String contenido) {
        return new MensajeLocal("id-1", origen, "A001",
                TipoMensajeCliente.MENSAJE_TEXTO.name(), contenido, null,
                contenido.length(), 1, null, null, null,
                LocalDateTime.now().toString(), false, true,
                EstadoMensaje.ENTREGADO.name());
    }
}
