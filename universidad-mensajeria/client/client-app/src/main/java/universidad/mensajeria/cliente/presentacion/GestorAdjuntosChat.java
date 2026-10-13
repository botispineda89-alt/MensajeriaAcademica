package universidad.mensajeria.cliente.presentacion;

import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.util.Base64;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.Executor;
import java.util.function.Consumer;
import java.util.function.Supplier;
import java.util.logging.Logger;
import javafx.concurrent.Task;
import javafx.scene.control.ListView;
import javafx.stage.FileChooser;
import universidad.mensajeria.cliente.fachada.InterfazArchivosConversacion;
import universidad.mensajeria.cliente.presentacion.celdas.FilaChat;
import universidad.mensajeria.cliente.presentacion.celdas.MensajeCell;
import universidad.mensajeria.cliente.transversal.records.MensajeLocal;
import universidad.mensajeria.cliente.transversal.records.PaginaHistorialRemota;
import universidad.mensajeria.cliente.transversal.utilerias.TranscodificadorImagen;

/** Carga, reintenta, cancela y descarga adjuntos de la conversación visible. */
public final class GestorAdjuntosChat implements InterfazGestorAdjuntos {

    private static final String METADATOS_PENDIENTES = "metadatos";
    private static final Logger LOG = Logger.getLogger(GestorAdjuntosChat.class.getName());

    private final InterfazArchivosConversacion archivos;
    private final Executor tareasFondo;
    private final ListView<FilaChat> listaMensajes;
    private final Supplier<String> conversacionActual;
    private final Consumer<String> mostrarEstado;
    private final Map<String, Integer> paginaPorMensaje = new ConcurrentHashMap<>();
    private final Map<String, Integer> siguientePaginaRemota = new ConcurrentHashMap<>();
    private final Map<String, Integer> totalPaginasRemotas = new ConcurrentHashMap<>();
    private final Set<String> imagenesFallidas = ConcurrentHashMap.newKeySet();
    /** UIX-27: los bytes llegaron pero este cliente no sabe pintarlos (.webp). */
    private final Set<String> imagenesIlegibles = ConcurrentHashMap.newKeySet();
    private final Map<String, Task<?>> cargasImagen = new ConcurrentHashMap<>();
    private final Map<String, Object> candadosMetadata = new ConcurrentHashMap<>();
    /**
     * UIX-28: el ORIGINAL de cada imagen, en disco. Antes el visor recibía la
     * miniatura de 300 px y hacía zoom sobre ella, así que cualquierAmpliación
     * salía borrosa; con el archivo entero el 1:1 es de verdad.
     */
    private final Map<String, Path> originalesImagen = new ConcurrentHashMap<>();

    /** Carpeta donde se guardan los originales: %LOCALAPPDATA%\Mensajeria\imagenes. */
    static Path carpetaOriginales() {
        String base = System.getProperty("os.name", "").toLowerCase(Locale.ROOT)
                .contains("win")
                ? System.getenv("LOCALAPPDATA")
                : System.getProperty("user.home");
        if (base == null || base.isBlank()) {
            base = System.getProperty("java.io.tmpdir");
        }
        return Path.of(base, "Mensajeria", "imagenes");
    }

    GestorAdjuntosChat(InterfazArchivosConversacion archivos, Executor tareasFondo,
                       ListView<FilaChat> listaMensajes,
                       Supplier<String> conversacionActual,
                       Consumer<String> mostrarEstado) {
        this.archivos = archivos;
        this.tareasFondo = tareasFondo;
        this.listaMensajes = listaMensajes;
        this.conversacionActual = conversacionActual;
        this.mostrarEstado = mostrarEstado;
    }

    @Override
    public boolean imagenFallida(String idMensaje) {
        return imagenesFallidas.contains(idMensaje) || imagenesIlegibles.contains(idMensaje);
    }

    /** UIX-27: se descargó pero no se puede pintar (formato no soportado). */
    public boolean imagenIlegible(String idMensaje) {
        return imagenesIlegibles.contains(idMensaje);
    }

    /**
     * UIX-27: deja la miniatura lista para PINTAR. El archivo se guarda tal cual
     * (es el que envió el otro y el que se descarga con "Guardar"); si a este
     * cliente no le sabe abrir —un {@code .webp}— se hace una copia en PNG
     * <b>solo para la vista</b>, sin tocar el original.
     *
     * @return false si ni siquiera con la copia se pudo pintar
     */
    private boolean precalentarParaVer(MensajeLocal item, byte[] bytes) {
        if (MensajeCell.precalentar(item.idMensaje(),
                Base64.getEncoder().encodeToString(bytes))) {
            return true;
        }
        String nombre = item.nombreArchivo();
        if (nombre == null || !TranscodificadorImagen.necesitaConversion(nombre)) {
            return false;
        }
        try {
            byte[] paraVer = TranscodificadorImagen.aPng(bytes, nombre);
            return MensajeCell.precalentar(item.idMensaje(),
                    Base64.getEncoder().encodeToString(paraVer));
        } catch (IOException noSePudo) {
            LOG.fine(() -> "no se pudo convertir para mostrar " + nombre + ": "
                    + noSePudo.getMessage());
            return false;
        }
    }

    /**
     * UIX-28: ruta del original ya descargado, o {@code null} si todavía no se
     * ha descargado. El visor lo abre para poder hacer zoom de verdad.
     */
    public Path originalDe(String idMensaje) {
        return originalesImagen.get(idMensaje);
    }

    /**
     * Escribe el original en disco la primera vez. Best-effort: si el disco falla
     * el chat sigue funcionando con la miniatura, que es lo que hay siempre.
     */
    private void guardarOriginal(MensajeLocal item, byte[] bytes) {
        String id = item.idMensaje();
        if (originalesImagen.containsKey(id)) {
            return;
        }
        try {
            Path carpeta = carpetaOriginales();
            Files.createDirectories(carpeta);
            Path destino = carpeta.resolve(id + "_" + sufijo(item.nombreArchivo()));
            // Escribe primero en temporal y luego mueve: si el proceso muere a
            // medias no queda un "original" a medio escribir que luego se pinta
            // como si fuera entero.
            Path temporal = Files.createTempFile(carpeta, id, ".tmp");
            Files.write(temporal, bytes);
            Files.move(temporal, destino, StandardCopyOption.REPLACE_EXISTING);
            originalesImagen.put(id, destino);
        } catch (IOException | RuntimeException noSePudoGuardar) {
            LOG.fine(() -> "no se pudo guardar el original " + id + ": "
                    + noSePudoGuardar.getMessage());
        }
    }

    /** La extensión del original, para que JavaFX elija el decodificador bien. */
    private static String sufijo(String nombreArchivo) {
        if (nombreArchivo == null) {
            return "img";
        }
        int punto = nombreArchivo.lastIndexOf('.');
        String sufijo = punto < 0 ? "" : nombreArchivo.substring(punto + 1).toLowerCase(Locale.ROOT);
        return sufijo.matches("[a-z0-9]{1,5}") ? sufijo : "img";
    }

    @Override
    public int paginaRemota(String conversacion) {
        return siguientePaginaRemota.getOrDefault(conversacion, 0);
    }

    @Override
    public Integer totalPaginasRemotas(String conversacion) {
        return totalPaginasRemotas.get(conversacion);
    }

    @Override
    public void registrarPaginaRemota(String conversacion, int siguiente, int total) {
        siguientePaginaRemota.put(conversacion, siguiente);
        totalPaginasRemotas.put(conversacion, total);
    }

    @Override
    public boolean hayMasHistorialRemoto(String conversacion) {
        Integer total = totalPaginasRemotas(conversacion);
        return total != null && paginaRemota(conversacion) < total;
    }

    @Override
    public void registrarPaginaDeMensaje(String idMensaje, int pagina) {
        paginaPorMensaje.put(idMensaje, pagina);
    }

    @Override
    public void cambiarConversacion() {
        cancelarCargas();
        paginaPorMensaje.clear();
    }

    @Override
    public void detener() {
        cancelarCargas();
    }

    @Override
    public void cargarImagenVisible(MensajeLocal item) {
        if (item == null || cargasImagen.containsKey(item.idMensaje())) {
            return;
        }
        // UIX-27: si ya sabemos que no se puede pintar, no se vuelve a descargar.
        // Antes el placeholder pedia la imagen, el decode fallaba y se repetia
        // el ciclo indefinidamente con "Cargando imagen…" en pantalla.
        if (imagenesIlegibles.contains(item.idMensaje())) {
            return;
        }
        String chat = conversacionActual.get();
        if (chat == null) {
            return;
        }
        int paginaMetadata = paginaPorMensaje.getOrDefault(item.idMensaje(), 0);
        Task<byte[]> tarea = new Task<>() {
            @Override
            protected byte[] call() throws Exception {
                byte[] bytes;
                try {
                    bytes = archivos.descargarImagen(item.idMensaje());
                } catch (IllegalStateException faltaId) {
                    if (!esFaltaDeMetadatos(faltaId)) {
                        throw faltaId;
                    }
                    String clave = chat + ":" + paginaMetadata;
                    Object candado = candadosMetadata.computeIfAbsent(clave, k -> new Object());
                    synchronized (candado) {
                        try {
                            bytes = archivos.descargarImagen(item.idMensaje());
                        } catch (IllegalStateException aunSinId) {
                            if (!esFaltaDeMetadatos(aunSinId)) {
                                throw aunSinId;
                            }
                            PaginaHistorialRemota remota = archivos
                                    .traerHistorial(chat, paginaMetadata);
                            siguientePaginaRemota.merge(chat, paginaMetadata + 1, Math::max);
                            totalPaginasRemotas.put(chat, remota.totalPaginas());
                            bytes = archivos.descargarImagen(item.idMensaje());
                        }
                    }
                }
                if (!precalentarParaVer(item, bytes)) {
                    imagenesIlegibles.add(item.idMensaje());
                }
                guardarOriginal(item, bytes);
                return bytes;
            }
        };
        tarea.setOnSucceeded(e -> {
            cargasImagen.remove(item.idMensaje(), tarea);
            imagenesFallidas.remove(item.idMensaje());
            // UIX-27: `imagenesIlegibles` NO se borra aquí. Se acaba de descargar
            // bien, pero eso no significa que se sepa pintar: si se limpiara, la
            // celda volvería a pedir la imagen y el ciclo de "Cargando imagen…"
            // se repetiría indefinidamente.
            listaMensajes.refresh();
        });
        tarea.setOnFailed(e -> {
            cargasImagen.remove(item.idMensaje(), tarea);
            imagenesFallidas.add(item.idMensaje());
            LOG.fine(() -> "imagen no disponible " + item.idMensaje() + ": "
                    + FormatoErrorCliente.mensaje(tarea.getException()));
            listaMensajes.refresh();
        });
        tarea.setOnCancelled(e -> cargasImagen.remove(item.idMensaje(), tarea));
        cargasImagen.put(item.idMensaje(), tarea);
        tareasFondo.execute(tarea);
    }

    @Override
    public void reintentarImagen(MensajeLocal item) {
        if (item == null) {
            return;
        }
        imagenesFallidas.remove(item.idMensaje());
        // UIX-27: el reintento manual sí limpia el "no se puede pintar": es el
        // gesto explícito del usuario para volver a intentarlo (por ejemplo tras
        // instalar el soporte de .webp en una versión nueva).
        imagenesIlegibles.remove(item.idMensaje());
        cargarImagenVisible(item);
    }

    @Override
    public void descargarArchivo(MensajeLocal item) {
        if (item == null) {
            return;
        }
        FileChooser elegidor = new FileChooser();
        elegidor.setTitle("Guardar archivo");
        elegidor.setInitialFileName(item.nombreArchivo() == null
                ? "archivo.bin" : item.nombreArchivo());
        File destino = elegidor.showSaveDialog(listaMensajes.getScene().getWindow());
        if (destino == null) {
            return;
        }
        mostrarEstado.accept("Descargando " + item.nombreArchivo() + "…");
        Task<Void> tarea = new Task<>() {
            @Override
            protected Void call() throws Exception {
                byte[] bytes;
                try {
                    bytes = archivos.descargarImagen(item.idMensaje());
                } catch (universidad.mensajeria.cliente.transversal.excepciones.ExcepcionCliente
                        | java.io.IOException noDisponibleLocalmente) {
                    bytes = archivos.descargarContenidoArchivo(item.archivoId());
                }
                Files.write(destino.toPath(), bytes);
                return null;
            }
        };
        tarea.setOnSucceeded(e -> mostrarEstado.accept(
                "Archivo guardado: " + destino.getName()));
        tarea.setOnFailed(e -> mostrarEstado.accept("No se pudo descargar: "
                + FormatoErrorCliente.mensaje(tarea.getException())));
        tareasFondo.execute(tarea);
    }

    private void cancelarCargas() {
        for (Task<?> tarea : cargasImagen.values()) {
            tarea.cancel(true);
        }
        cargasImagen.clear();
    }

    private static boolean esFaltaDeMetadatos(IllegalStateException error) {
        return error.getMessage() != null
                && error.getMessage().contains(METADATOS_PENDIENTES);
    }
}
