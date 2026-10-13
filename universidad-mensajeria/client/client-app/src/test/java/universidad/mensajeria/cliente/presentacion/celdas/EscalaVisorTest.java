package universidad.mensajeria.cliente.presentacion.celdas;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * UIX-28: el zoom del visor es un factor sobre el tamaño REAL de la foto.
 * <p>
 * Antes el visor fijaba el lienzo en 900×720 sea cual sea la imagen, así que el
 * botón "Tamaño original" no era 1:1 sino "ajustado a 900×720": una foto de
 * 4000 px se veía a menos de un cuarto de su tamaño sin avisar.
 * </p>
 */
class EscalaVisorTest {

    private static final double VENTANA = 940;

    @Test
    void unaImagenGrandeArrancaEncajadaYElUnoEsDeVerdadUno() {
        // 4000x3000 en una ventana de 940x800: encaja a la anchura.
        double encaje = EscalaVisor.escalaEncaje(4000, 3000, VENTANA, 800);

        assertEquals(940d / 4000d, encaje, 1e-9);
        // Y el 1:1 pinta los 4000 px de verdad, no 900.
        assertEquals(4000d, EscalaVisor.anchoPintado(4000, 1.0), 1e-9);
        assertEquals(3000d, EscalaVisor.altoPintado(3000, 1.0), 1e-9);
    }

    @Test
    void unaImagenPequenaNoSeEstiraParaLlenarLaVentana() {
        // Una de 200x150 se ve a su tamaño, no gigante.
        assertEquals(1.0, EscalaVisor.escalaEncaje(200, 150, VENTANA, 800), 1e-9);
    }

    @Test
    void noSeAmpliaMasAllaDelTamanoReal() {
        // Ampliar un 200% solo interpola: el usuario creería que la foto tiene
        // más resolución de la que tiene.
        assertEquals(1.0, EscalaVisor.limitar(2.0), 1e-9);
        assertEquals(1.0, EscalaVisor.limitar(1.0), 1e-9);
        assertEquals(1.0, EscalaVisor.limitar(8.0), 1e-9);
    }

    @Test
    void tampocoSeReduceHastaPerderLaFoto() {
        assertEquals(EscalaVisor.ZOOM_MIN, EscalaVisor.limitar(0.001), 1e-9);
        assertEquals(EscalaVisor.ZOOM_MIN, EscalaVisor.limitar(0.0), 1e-9);
        // Un zoom sin sentido (NaN) no debe romper la escena.
        assertEquals(EscalaVisor.ZOOM_MIN, EscalaVisor.limitar(Double.NaN), 1e-9);
        assertEquals(EscalaVisor.ZOOM_MIN, EscalaVisor.limitar(-3), 1e-9);
    }

    @Test
    void elScrollPaneSoloApareceCuandoLaImagenDesborda() {
        double encaje = EscalaVisor.escalaEncaje(4000, 3000, VENTANA, 800);

        assertTrue(EscalaVisor.encaja(4000, 3000, encaje, VENTANA, 800));
        assertFalse(EscalaVisor.encaja(4000, 3000, 1.0, VENTANA, 800),
                "a 1:1 una foto de 4000 px necesita barras de desplazamiento");
    }

    @Test
    void unaFotoAPaisajeEncajaPorElAlto() {
        // 3000x4000 (vertical): manda la altura de la ventana, no la anchura.
        assertEquals(800d / 4000d, EscalaVisor.escalaEncaje(3000, 4000, VENTANA, 800), 1e-9);
    }

    @Test
    void medidasSinSentidoNoRompenNada() {
        assertEquals(1.0, EscalaVisor.escalaEncaje(0, 0, VENTANA, 800), 1e-9);
        assertEquals(1.0, EscalaVisor.escalaEncaje(100, 100, 0, 0), 1e-9);
    }
}