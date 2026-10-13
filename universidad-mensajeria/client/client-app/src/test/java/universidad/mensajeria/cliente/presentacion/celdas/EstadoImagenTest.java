package universidad.mensajeria.cliente.presentacion.celdas;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * UIX-27: una imagen que este cliente no sabe pintar (un {@code .webp}) se
 * quedaba en "Cargando imagen…" indefinidamente. La celda pedía la imagen, el
 * decode fallaba, la celda volvía a pedirla… y así hasta que se cerraba el
 * chat. Estos tests fijan las tres salidas posibles.
 */
class EstadoImagenTest {

    @Test
    void mientrasNoSeSepaNadaSePideYSeAnunciaQueCarga() {
        MensajeCell.EstadoImagen estado = MensajeCell.estadoImagen(false, false);

        assertEquals("Cargando imagen…", estado.texto());
        assertTrue(estado.pedirImagen());
    }

    @Test
    void siNoSeDescargaNoSeVuelveAPedirYOfreceReintento() {
        MensajeCell.EstadoImagen estado = MensajeCell.estadoImagen(true, false);

        assertEquals("No se pudo cargar · reintentar", estado.texto());
        assertFalse(estado.pedirImagen(),
                "ya se sabe que falló: pedirla sola otra vez no lo arregla, "
                        + "hace falta el clic del usuario");
    }

    @Test
    void siElFormatoNoSePuedePintarLoDiceYNoReintenta() {
        MensajeCell.EstadoImagen estado = MensajeCell.estadoImagen(false, true);

        assertEquals("Formato no soportado por este cliente", estado.texto());
        assertFalse(estado.pedirImagen(),
                "este es el bucle: los bytes llegaron bien, solo que no se pintan");
    }

    @Test
    void loIlegibleGanaAFalloDeDescargaPorqueElTextoEsMasVeraz() {
        // Puede pasar que la descarga falle y además el mensaje fuera un .webp:
        // "no se pudo cargar" sería mentira si los bytes sí llegaron en otro
        // intento, así que manda el motivo real.
        MensajeCell.EstadoImagen estado = MensajeCell.estadoImagen(true, true);

        assertEquals("Formato no soportado por este cliente", estado.texto());
        assertFalse(estado.pedirImagen());
    }
}