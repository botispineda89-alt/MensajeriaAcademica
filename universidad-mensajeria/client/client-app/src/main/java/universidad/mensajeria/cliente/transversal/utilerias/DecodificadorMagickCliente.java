package universidad.mensajeria.cliente.transversal.utilerias;

import org.im4java.core.IM4JavaException;
import org.im4java.core.IMOperation;
import org.im4java.core.ImageCommand;

import javax.imageio.ImageIO;
import java.awt.image.BufferedImage;
import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * FORMATOS-01: plan B de decodificacion del CLIENTE, igual que
 * {@code DecodificadorMagick} del servidor: ImageMagick a traves de im4java
 * (proceso externo, sin JNI) para AVIF, HEIC/HEIF y cualquier formato que la
 * pila del cliente no abra.
 * <p>
 * No es la via principal: {@code TranscodificadorImagen} intenta primero
 * ImageIO (WebP/AVIF/HEIC por SPI) y jsvg (SVG); solo cae aqui cuando ninguna
 * de las dos puede. Si ImageMagick no esta instalado en el PC del cliente, la
 * conversion devuelve IOException y la burbuja muestra el placeholder de
 * siempre: el mensaje sigue siendo imagen y el servidor ya lo filtro.
 * </p>
 * <p>
 * El ejecutable se autodetecta igual que en el servidor: propiedad de sistema
 * {@value #PROPIEDAD_EJECUTABLE}, variable de entorno {@value #VARIABLE_ENTORNO},
 * PATH e instalaciones tipicas de Windows.
 * </p>
 */
public final class DecodificadorMagickCliente {

    /** Propiedad de sistema para forzar la ruta del ejecutable de ImageMagick. */
    public static final String PROPIEDAD_EJECUTABLE = "mensajeria.imagen.magick";

    /** Variable de entorno equivalente a {@link #PROPIEDAD_EJECUTABLE}. */
    public static final String VARIABLE_ENTORNO = "MAGICK_EXECUTABLE";

    private static final List<String> NOMBRES = List.of("magick", "magick.exe");

    private static final String PREFIJO_TEMPORAL = "mensajeria-cliente-magick-";

    private static volatile String ejecutable;
    private static volatile Boolean disponible;

    private DecodificadorMagickCliente() {
    }

    /** Ruta o nombre del ejecutable, con cache. Misma resolucion que el servidor. */
    public static String rutaEjecutable() {
        String cache = ejecutable;
        if (cache != null) {
            return cache;
        }
        String resuelta = resolverEjecutable();
        ejecutable = resuelta;
        return resuelta;
    }

    /** true si el ejecutable responde a {@code -version}. Se memoriza. */
    public static synchronized boolean disponible() {
        Boolean cache = disponible;
        if (cache != null) {
            return cache;
        }
        boolean ok;
        try {
            Process proceso = new ProcessBuilder(rutaEjecutable(), "-version")
                    .redirectErrorStream(true)
                    .start();
            try (InputStream salida = proceso.getInputStream()) {
                salida.readAllBytes();
            }
            ok = proceso.waitFor() == 0;
        } catch (IOException e) {
            ok = false;
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            ok = false;
        }
        disponible = ok;
        return ok;
    }

    /**
     * Convierte los bytes a PNG con ImageMagick. Igual que en el servidor, el
     * paso va por un PNG temporal y se limpia siempre.
     *
     * @throws IOException si ImageMagick no esta disponible o no puede abrir
     *                     los bytes.
     */
    public static BufferedImage leer(byte[] origen) throws IOException {
        Path temporal = Files.createTempFile(PREFIJO_TEMPORAL, ".in");
        Path destino = Files.createTempFile(PREFIJO_TEMPORAL, ".png");
        try {
            Files.write(temporal, origen);
            ejecutar(temporal, destino);
            try (InputStream entrada = Files.newInputStream(destino)) {
                BufferedImage imagen = ImageIO.read(entrada);
                if (imagen == null) {
                    throw new IOException("ImageMagick no produjo una imagen legible");
                }
                return imagen;
            }
        } finally {
            Files.deleteIfExists(temporal);
            Files.deleteIfExists(destino);
            limpiarHermanos(destino);
        }
    }

    private static void ejecutar(Path origen, Path destino) throws IOException {
        // Los dos detalles de im4java 1.4.0 son los mismos que en el servidor:
        // ImageCommand (lista vacia de comandos) y el nombre pelado + searchPath
        // para rutas con espacios.
        Path ejecutable = Path.of(rutaEjecutable());
        Path carpeta = ejecutable.getParent();
        String nombre = carpeta == null || ejecutable.getFileName() == null
                ? rutaEjecutable() : sinExtension(ejecutable.getFileName().toString());
        ImageCommand comando = new ImageCommand(nombre);
        if (carpeta != null) {
            comando.setSearchPath(carpeta.toString());
        }
        IMOperation operacion = new IMOperation();
        // El selector [0] importa: un HEIC animado escribiria destino-0.png.
        operacion.addImage(origen.toAbsolutePath() + "[0]");
        operacion.addImage(destino.toAbsolutePath().toString());
        try {
            comando.run(operacion);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new IOException("decodificacion interrumpida", e);
        } catch (IOException | IM4JavaException e) {
            throw new IOException("ImageMagick no pudo decodificar la imagen", e);
        }
        if (!Files.isRegularFile(destino)) {
            throw new IOException("ImageMagick no genero el PNG temporal");
        }
    }

    private static String resolverEjecutable() {
        String configurada = System.getProperty(PROPIEDAD_EJECUTABLE);
        if (configurada != null && !configurada.isBlank()) {
            return configurada.trim();
        }
        String entorno = System.getenv(VARIABLE_ENTORNO);
        if (entorno != null && !entorno.isBlank()) {
            return entorno.trim();
        }
        for (String nombre : NOMBRES) {
            String enPath = buscarEnPath(nombre);
            if (enPath != null) {
                return enPath;
            }
        }
        String enInstalacion = buscarEnInstalacionesWindows();
        if (enInstalacion != null) {
            return enInstalacion;
        }
        return "magick";
    }

    private static String buscarEnPath(String nombre) {
        String path = System.getenv("PATH");
        if (path == null) {
            return null;
        }
        for (String directorio : path.split(File.pathSeparator)) {
            if (directorio.isBlank()) {
                continue;
            }
            File candidato = new File(directorio, nombre);
            if (candidato.isFile()) {
                return candidato.getAbsolutePath();
            }
        }
        return null;
    }

    /**
     * El instalador oficial de Windows no expone variable de entorno: deja
     * {@code %ProgramFiles%\ImageMagick-7.1.2-Q16-HDRI}. Igual que el servidor.
     */
    private static String buscarEnInstalacionesWindows() {
        String base = System.getenv("ProgramFiles");
        if (base == null) {
            return null;
        }
        Path raiz = Path.of(base);
        if (!Files.isDirectory(raiz)) {
            return null;
        }
        List<Path> candidatos = new ArrayList<>();
        for (String prefijo : List.of("IMAGEMAGICK_HOME", "MAGICK_HOME")) {
            String home = System.getenv(prefijo);
            if (home != null && !home.isBlank()) {
                candidatos.add(Path.of(home.trim(), "magick.exe"));
            }
        }
        try (DirectoryStream<Path> directorios = Files.newDirectoryStream(raiz, "ImageMagick*")) {
            for (Path directorio : directorios) {
                candidatos.add(directorio.resolve("magick.exe"));
            }
        } catch (IOException e) {
            return null;
        }
        for (Path candidato : candidatos) {
            if (Files.isRegularFile(candidato)) {
                return candidato.toAbsolutePath().toString();
            }
        }
        return null;
    }

    private static String sinExtension(String nombre) {
        return nombre.toLowerCase(Locale.ROOT).endsWith(".exe")
                ? nombre.substring(0, nombre.length() - 4)
                : nombre;
    }

    /** Si el selector [0] no bastara, ImageMagick deja {@code destino-N.png}. */
    private static void limpiarHermanos(Path destino) {
        Path carpeta = destino.getParent();
        if (carpeta == null) {
            return;
        }
        String nombre = destino.getFileName().toString();
        String prefijo = nombre.endsWith(".png")
                ? nombre.substring(0, nombre.length() - 4)
                : nombre;
        try (DirectoryStream<Path> hermanos = Files.newDirectoryStream(carpeta,
                prefijo + "-*")) {
            for (Path hermano : hermanos) {
                Files.deleteIfExists(hermano);
            }
        } catch (IOException e) {
            // La limpieza del temporal no debe tumbar una decodificacion correcta.
        }
    }
}
