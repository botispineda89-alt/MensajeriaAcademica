package universidad.mensajeria.cliente.presentacion;

import java.util.List;

/**
 * §7 paridad: reparte el avance del lote por bytes, para que un adjunto de 20 MB
 * no se esconda detrás de cinco archivos de un kilo.
 * <p>
 * Réplica de {@code Transversal/ProgresoEnvio.cs} ({@code AgregadorProgreso}) de
 * .NET. Java puro, sin JavaFX, para que el reparto se pruebe sin toolkit.
 * </p>
 */
public final class AgregadorProgreso {

    private final long[] pesos;
    private final long total;
    private long completados;

    public AgregadorProgreso(List<Long> pesos) {
        this.pesos = new long[pesos.size()];
        long suma = 0;
        for (int i = 0; i < pesos.size(); i++) {
            this.pesos[i] = Math.max(0L, pesos.get(i));
            suma += this.pesos[i];
        }
        this.total = suma;
    }

    /** Bytes que suman los adjuntos del lote. */
    public long bytesTotales() {
        return total;
    }

    /** Bytes que suman los adjuntos ya cerrados. */
    public long bytesCompletados() {
        return completados;
    }

    /**
     * Cierra el adjunto {@code indice} y devuelve el acumulado.
     * Un índice fuera de rango suma 0: el lote sigue vivo, que es distinto de
     * fallar.
     */
    public long completar(int indice) {
        completados += indice >= 0 && indice < pesos.length ? pesos[indice] : 0;
        return completados;
    }

    /** Bytes del lote teniendo en cuenta el avance parcial del adjunto en curso. */
    public long acumulado(int indice, double fraccionArchivo) {
        long parcial = indice >= 0 && indice < pesos.length
                ? (long) (pesos[indice] * Math.clamp(fraccionArchivo, 0d, 1d))
                : 0L;
        return completados + parcial;
    }
}