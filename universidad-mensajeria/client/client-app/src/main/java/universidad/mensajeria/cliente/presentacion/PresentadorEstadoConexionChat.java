package universidad.mensajeria.cliente.presentacion;

import javafx.application.Platform;
import javafx.scene.control.Label;
import universidad.mensajeria.cliente.transversal.EstadoConexion;

import java.util.function.BooleanSupplier;
import java.util.function.Consumer;

/**
 * PAT-03/UIX-00c: pinta el estado de conexión que publica la fachada.
 * <p>
 * Sale de {@link ChatController} porque mezcla tres cosas que no son suyas: el
 * salto al hilo de JavaFX (el aviso llega por el hilo de red), la política del
 * banner (qué estados lo dejan visible) y el "se recuperó la conexión", que es un
 * evento de negocio y no un simple color.
 * </p>
 * <p>
 * El banner ámbar aparece en cuanto cae el socket, sin esperar al sondeo de 30 s;
 * el aviso inline, en cambio, es el "te pasó esto" puntual y se retira solo. Son
 * dos cosas distintas y por eso viven separadas.
 * </p>
 */
final class PresentadorEstadoConexionChat {

    private final Label etiquetaEstado;
    private final Label bannerOffline;
    private final InterfazChatViewModel viewModel;
    private final AvisosInlineChat avisos;
    /** Qué hacer cuando vuelve la conexión: refrescar contactos y la vista visible. */
    private final Runnable alRestaurar;
    /** Qué hacer cuando el servidor cierra la sesión (KICK, logout remoto). */
    private final Runnable alCerrarSesion;
    private final BooleanSupplier detenido;

    private volatile EstadoConexion ultimoEstado = EstadoConexion.SIN_RED;

    PresentadorEstadoConexionChat(Label etiquetaEstado, Label bannerOffline,
                                 InterfazChatViewModel viewModel, AvisosInlineChat avisos,
                                 Runnable alRestaurar, Runnable alCerrarSesion,
                                 BooleanSupplier detenido) {
        this.etiquetaEstado = etiquetaEstado;
        this.bannerOffline = bannerOffline;
        this.viewModel = viewModel;
        this.avisos = avisos;
        this.alRestaurar = alRestaurar;
        this.alCerrarSesion = alCerrarSesion;
        this.detenido = detenido;
    }

    /** Llega en el hilo de red: el salto a JavaFX es obligatorio. */
    void pintar(EstadoConexion estado) {
        boolean restaurada = ultimoEstado == EstadoConexion.SIN_RED
                && estado == EstadoConexion.CONECTADO;
        ultimoEstado = estado;
        Platform.runLater(() -> {
            if (estado == EstadoConexion.SESION_CERRADA && !detenido.getAsBoolean()) {
                alCerrarSesion.run();
                return;
            }
            pintarBanner(estado);
            if (etiquetaEstado != null && !estado.equals(EstadoConexion.CONECTADO)) {
                etiquetaEstado.setText(estado.texto());
            }
            avisarCambio(restaurada, estado);
        });
    }

    /** Refleja la conexión actual sin esperar un evento (arranque, logout). */
    void refrescar() {
        pintarBanner(viewModel.conectado() ? EstadoConexion.CONECTADO : EstadoConexion.SIN_RED);
    }

    private void pintarBanner(EstadoConexion estado) {
        if (bannerOffline == null) {
            return;
        }
        bannerOffline.setVisible(estado.muestraBanner());
        bannerOffline.setManaged(estado.muestraBanner());
        if (estado.muestraBanner()) {
            bannerOffline.setText(estado.texto());
        }
    }

    /** UIX-26: perder la conexión también se avisa en el aviso inline, que se retira solo. */
    private void avisarCambio(boolean restaurada, EstadoConexion estado) {
        if (estado == EstadoConexion.SIN_RED && !detenido.getAsBoolean()) {
            mostrarAviso(estado.texto(), TipoAviso.ADVERTENCIA);
        } else if (restaurada && !detenido.getAsBoolean()) {
            mostrarAviso("Conexión recuperada.", TipoAviso.EXITO);
            alRestaurar.run();
        }
    }

    /**
     * Aviso transitorio con título, texto y ✕. El cableado JavaFX vive en
     * {@link AvisosInlineChat} y la decisión de plazo en {@link EstadoAvisoInline}.
     */
    void mostrarAviso(String texto, TipoAviso tipo) {
        if (avisos != null) {
            avisos.mostrar(texto, tipo);
        }
    }

    void cerrarAviso() {
        if (avisos != null) {
            avisos.limpiar();
        }
    }

    /** Actualización del texto de estado que hace la vista al cambiar de chat. */
    Consumer<String> escritorEstado() {
        return texto -> {
            if (etiquetaEstado != null) {
                etiquetaEstado.setText(texto);
            }
            refrescar();
        };
    }
}