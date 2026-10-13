package universidad.mensajeria.cliente.presentacion.celdas;

/**
 * UIX-28: la aritmética del zoom del visor de imágenes.
 * <p>
 * Va aparte de {@link VisorImagenChat} a propósito: el visor es JavaFX y no se
 * puede probar sin levantar el toolkit, y esto es justo lo que estaba mal. Antes
 * el visor fijaba el lienzo en 900×720 sea cual sea la foto, así que el botón
 * "Tamaño original" ({@code zoom = 1}) no era 1:1 sino "ajustado a 900×720", y
 * encima hacía zoom sobre la miniatura de 300 px.
 * </p>
 * <p>
 * Aquí {@code zoom} es un factor sobre el <b>tamaño real en píxeles</b>: 1.0
 * significa un píxel de la foto por un píxel de pantalla.
 * </p>
 */
public final class EscalaVisor {

    /** Zoom por debajo del cual no se deja: más pequeño no aporta nada. */
    public static final double ZOOM_MIN = 0.05;

    /**
     * Zoom por encima del cual no se deja. Ampliar más allá de 1:1 no añade un
     * solo píxel de detalle: solo interpola, y el usuario creería que la foto
     * tiene más resolución de la que tiene.
     */
    public static final double ZOOM_MAX = 1.0;

    private EscalaVisor() {
    }

    /**
     * Factor con el que la imagen entra entera en la ventana.
     *
     * @param anchoAncho real de la imagen en píxeles
     * @param alto       alto real en píxeles
     * @param ventana    ancho y alto útiles de la ventana
     */
    public static double escalaEncaje(double ancho, double alto, double ventanaAncho,
            double ventanaAlto) {
        if (ancho <= 0 || alto <= 0 || ventanaAncho <= 0 || ventanaAlto <= 0) {
            return 1.0;
        }
        // Nunca por encima de 1: una foto más pequeña que la ventana se muestra
        // a tamaño real, no estirada.
        return Math.min(1.0, Math.min(ventanaAncho / ancho, ventanaAlto / alto));
    }

    /** Recorta el zoom a un rango usable. */
    public static double limitar(double zoom) {
        if (Double.isNaN(zoom) || zoom <= 0) {
            return escalaEncajeDefecto();
        }
        return Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, zoom));
    }

    private static double escalaEncajeDefecto() {
        return ZOOM_MIN;
    }

    /** Ancho en píxeles al que hay que pintar la imagen para ese zoom. */
    public static double anchoPintado(double anchoReal, double zoom) {
        return anchoReal * limitar(zoom);
    }

    /** Alto en píxeles al que hay que pintar la imagen para ese zoom. */
    public static double altoPintado(double altoReal, double zoom) {
        return altoReal * limitar(zoom);
    }

    /**
     * true si a ese zoom la imagen sigue entrando en la ventana, y por tanto el
     * {@code ScrollPane} debe estirarla para centrarla en vez de dejar barras.
     */
    public static boolean encaja(double anchoReal, double altoReal, double zoom,
            double ventanaAncho, double ventanaAlto) {
        return anchoPintado(anchoReal, zoom) <= ventanaAncho
                && altoPintado(altoReal, zoom) <= ventanaAlto;
    }
}