package universidad.mensajeria.cliente.presentacion;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

/**
 * §7 paridad: el reparto del avance del lote por bytes.
 * <p>
 * Sin esto, un adjunto de 20 MB se contaría igual que uno de 1 KB y la barra
 * saltaría de golpe al final. Los pesos los pone el comando al construir el
 * agregador, así que lo que se comprueba aquí es el reparto, que es donde se
 * equivocaría uno.
 */
class AgregadorProgresoTest {

    private static final long KB = 1024L;
    private static final long MB = 1024L * 1024L;

    @Test
    @DisplayName("Suma los pesos del lote y reparte el acumulado al completar")
    void repartePorBytes() {
        AgregadorProgreso agregador = new AgregadorProgreso(List.of(3 * MB, 1 * MB));

        assertEquals(4 * MB, agregador.bytesTotales());
        assertEquals(0L, agregador.bytesCompletados());

        assertEquals(3 * MB, agregador.completar(0));
        assertEquals(3 * MB, agregador.bytesCompletados());

        assertEquals(4 * MB, agregador.completar(1));
        assertEquals(1.0d, new ProgresoEnvio(2, 2, "", 4 * MB, 4 * MB).fraccionGlobal(),
                0.0001d);
    }

    @Test
    @DisplayName("Un peso negativo o nulo no resta: se trata como cero")
    void pesosNoNegativos() {
        AgregadorProgreso agregador = new AgregadorProgreso(List.of(-5L, 2L, 0L));

        assertEquals(2L, agregador.bytesTotales());
        assertEquals(0L, agregador.completar(0));
        assertEquals(2L, agregador.completar(1));
    }

    @Test
    @DisplayName("Un índice fuera de rango suma cero en vez de romper el lote")
    void indiceFueraDeRango() {
        AgregadorProgreso agregador = new AgregadorProgreso(List.of(5L, 7L));

        assertEquals(0L, agregador.completar(9));
        assertEquals(0L, agregador.completar(-1));
        assertEquals(0L, agregador.acumulado(9, 0.5d));
        assertEquals(0L, agregador.bytesCompletados());
    }

    @Test
    @DisplayName("El acumulado con adjunto en curso interpola su fracción")
    void acumuladoConParteEnCurso() {
        AgregadorProgreso agregador = new AgregadorProgreso(List.of(100L, 100L));

        assertEquals(50L, agregador.acumulado(0, 0.5d));
        agregador.completar(0);
        // Ya cerrado el primero: el segundo va del 0 al 100.
        assertEquals(100L, agregador.acumulado(1, 0d));
        assertEquals(150L, agregador.acumulado(1, 0.5d));
        // Una fracción fuera de 0..1 no puede hacer retroceder ni adelantar la barra.
        assertEquals(200L, agregador.acumulado(1, 5d));
        assertEquals(100L, agregador.acumulado(1, -3d));
    }

    @Test
    @DisplayName("Sin bytes que medir la fracción es 0, no una división por cero")
    void loteSinBytes() {
        ProgresoEnvio soloTexto = new ProgresoEnvio(0, 1, "El texto", 0L, 0L);

        assertEquals(0d, soloTexto.fraccionGlobal(), 0.0001d);
        assertEquals(0d, new ProgresoEnvio(1, 1, "El texto", 10L, 0L).fraccionGlobal(), 0.0001d);
    }

    @Test
    @DisplayName("El texto de la barra es el mismo en los tres clientes")
    void textoDeLaBarra() {
        ProgresoEnvio avance = new ProgresoEnvio(1, 3, "notas.pdf", 2 * MB, 6 * MB);

        assertEquals("Enviando 1/3 · 2.0/6.0 MB · notas.pdf", avance.texto());
    }

    @Test
    @DisplayName("Sin nombre en curso el texto no deja el separador colgando")
    void textoSinNombre() {
        assertEquals("Enviando 2/2 · 1.0/1.0 MB",
                new ProgresoEnvio(2, 2, "  ", 1 * MB, 1 * MB).texto());
        assertEquals("Enviando 0/1 · 0.0/0.0 MB",
                new ProgresoEnvio(0, 1, null, 0L, 0L).texto());
    }

    @Test
    @DisplayName("Un lote de 20 MB se reparte sin perder bytes por el camino")
    void loteDeVeinteMegas() {
        List<Long> pesos = List.of(20L * MB, 20L * MB, 20L * MB);
        AgregadorProgreso agregador = new AgregadorProgreso(pesos);

        long acumulado = 0;
        for (int i = 0; i < pesos.size(); i++) {
            assertEquals(20L * MB, agregador.acumulado(i, 1d) - acumulado,
                    "el adjunto en curso suma su peso completo");
            acumulado = agregador.completar(i);
        }
        assertEquals(60L * MB, acumulado);
        assertTrue(acumulado == agregador.bytesTotales());
    }
}