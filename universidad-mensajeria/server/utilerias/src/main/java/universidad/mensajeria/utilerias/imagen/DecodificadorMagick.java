package universidad.mensajeria.utilerias.imagen;

import org.im4java.core.IM4JavaException;
import org.im4java.core.IMOperation;
import org.im4java.core.ImageCommand;

import javax.imageio.ImageIO;
import java.awt.image.BufferedImage;
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
 * Plan B de decodificacion: ImageMagick a traves de im4java (proceso externo,
 * sin JNI). Traduce a PNG cualquier formato que el JDK no sepa abrir —AVIF,
 * HEIC/HEIF, SVG y en general todo lo que ImageMagick reconozca— para que la
 * tuberia de filtros siga trabajando con {@code BufferedImage} igual que antes.
 * <p>
 * No es la via principal: {@link Imagenes#leer} intenta primero {@code ImageIO}
 * y solo cae aqui cuando este devuelve {@code null}. PNG/JPEG/GIF/BMP (JDK) y
 * WebP ({@code imageio-webp}) nunca llegan a ejecutar un proceso externo, asi
 * que su comportamiento y su coste no cambian.
 * </p>
 * <p>
 * Por que un proceso y no una libreria Java: AVIF y HEIC necesitan
 * libavif/libheif, que en Java puro no existen sin JNI. ImageMagick ya los trae
 * y ademas cubre el resto de formatos de una vez.
 * </p>
 * <p>
 * El ejecutable se autodetecta (ver {@link #rutaEjecutable()}); se puede forzar
 * con la propiedad de sistema {@value #PROPIEDAD_EJECUTABLE} o la variable de
 * entorno {@value #VARIABLE_ENTORNO}, que es la salida de emergencia cuando el
 * PATH del servicio no incluye el directorio de ImageMagick.
 * </p>
 */
public final class DecodificadorMagick {

    /** Propiedad de sistema para forzar la ruta del ejecutable de ImageMagick. */
    public static final String PROPIEDAD_EJECUTABLE = "mensajeria.imagen.magick";

    /** Variable de entorno equivalente a {@link #PROPIEDAD_EJECUTABLE}. */
    public static final String VARIABLE_ENTORNO = "MAGICK_EXECUTABLE";

    private static final List<String> NOMBRES = List.of("magick", "magick.exe");

    private static final String PREFIJO_TEMPORAL = "mensajeria-magick-";

    /**
     * Topes de recursos para un archivo subido por un usuario (que es hostil
     * por definicion): sin ellos, un SVG o un HEIC preparados para descomprimir
     * a un lienzo gigantesco se comen la memoria del servidor.
     * <p>
     * La cota que manda es {@code area}: 512 MP dejan fuera una foto de
     * 40000x40000 (1600 MP) aunque un lado de 40000 por si solo se acepte, que
     * es lo que permite un panorama. Siguen siendo cotas holgadas —cualquier
     * camara de movil actual no llega a 200 MP— y solo afectan a los formatos
     * que entran por esta via; los que decodifica ImageIO no pasan por aqui.
     * </p>
     */
    private static final String[] LIMITES = {
        "-limit", "memory", "1GiB",
        "-limit", "map", "2GiB",
        "-limit", "disk", "4GiB",
        "-limit", "width", "40000",
        "-limit", "height", "40000",
        "-limit", "area", "512MP",
        "-limit", "time", "120",
    };

    private static volatile String ejecutable;
    private static volatile Boolean disponible;

    private DecodificadorMagick() {
    }

    /**
     * Ruta o nombre del ejecutable a invocar. Orden: propiedad de sistema,
     * variable de entorno, PATH del proceso, instalaciones tipicas de Windows y,
     * como ultimo recurso, el nombre {@code magick} para que lo resuelva el
     * sistema al arrancar el proceso.
     */
    public static String rutaEjecutable() {
        String cache = ejecutable;
        if (cache != null) {
            return cache;
        }
        String resuelta = resolverEjecutable();
        ejecutable = resuelta;
        return resuelta;
    }

    /**
     * true si el ejecutable responde a {@code -version}. Se memoriza: en
     * produccion el resultado no cambia mientras el proceso vive.
     */
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
     * Decodifica {@code origen} a un {@code BufferedImage} usando ImageMagick.
     * La conversion pasa por un PNG temporal que se borra siempre.
     *
     * @throws IOException si ImageMagick no esta disponible o no puede abrir el
     *                     archivo; el destino nunca se deja a medias.
     */
    public static BufferedImage leer(Path origen) throws IOException {
        if (origen == null || !Files.isRegularFile(origen)) {
            throw new IOException("no existe el archivo a decodificar: " + origen);
        }
        Path temporal = Files.createTempFile(PREFIJO_TEMPORAL, ".png");
        try {
            ejecutar(origen, temporal);
            try (InputStream entrada = Files.newInputStream(temporal)) {
                BufferedImage imagen = ImageIO.read(entrada);
                if (imagen == null) {
                    throw new IOException(
                            "ImageMagick no produjo una imagen legible para " + origen);
                }
                return imagen;
            }
        } finally {
            Files.deleteIfExists(temporal);
            limpiarHermanos(temporal);
        }
    }

    private static void ejecutar(Path origen, Path destino) throws IOException {
        // Dos detalles de im4java 1.4.0 obligan a construir el comando asi:
        //  - ConvertCmd fija "convert" en la lista de comandos, y setCommand
        //    ANADE en vez de reemplazar (quedaria "convert magick"). ImageCommand
        //    arranca con la lista vacia, que es lo que se necesita.
        //  - Si la ruta del ejecutable lleva espacios ("C:\Program Files\..."),
        //    la version 1.4.0 la parte mal al armar el proceso. Por eso se le da
        //    el nombre pelado y el directorio aparte, y lo resuelve su searchPath.
        Path ejecutable = Path.of(rutaEjecutable());
        Path carpeta = ejecutable.getParent();
        String nombre = carpeta == null || ejecutable.getFileName() == null
                ? rutaEjecutable() : sinExtension(ejecutable.getFileName().toString());
        ImageCommand comando = new ImageCommand(nombre);
        if (carpeta != null) {
            comando.setSearchPath(carpeta.toString());
        }
        IMOperation operacion = new IMOperation();
        operacion.addRawArgs(LIMITES);
        // El selector [0] importa: un HEIC o un WebP animado traen varias
        // imagenes y, sin el, ImageMagick escribiria destino-0.png, destino-1.png
        // y el destino pedido no existiria.
        operacion.addImage(origen.toAbsolutePath() + "[0]");
        operacion.addImage(destino.toAbsolutePath().toString());
        try {
            comando.run(operacion);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new IOException("decodificacion interrumpida: " + origen, e);
        } catch (IOException | IM4JavaException e) {
            throw new IOException("ImageMagick no pudo decodificar " + origen
                    + detalle(comando), e);
        }
        if (!Files.isRegularFile(destino)) {
            throw new IOException("ImageMagick no genero " + destino + detalle(comando));
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
        // Ultimo recurso: el nombre pelado. Lo resuelve el sistema operativo
        // heredando el PATH, que es como el servidor lo tiene en produccion.
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
     * {@code %ProgramFiles%\ImageMagick-7.1.2-Q16-HDRI}. Se busca ahi para que
     * un servicio con PATH recortado siga arrancando.
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

    /**
     * En Windows el buscador de im4java <b>añade</b> la extension: le das
     * {@code magick} y prueba {@code magick.exe}, {@code magick.cmd} y
     * {@code magick.bat}, pero no el nombre tal cual. Si se le pasara
     * {@code magick.exe} buscaria {@code magick.exe.exe} y no lo encontraria.
     */
    private static String sinExtension(String nombre) {
        return nombre.toLowerCase(Locale.ROOT).endsWith(".exe")
                ? nombre.substring(0, nombre.length() - 4)
                : nombre;
    }

    /** Primeras lineas de stderr como diagnostico, sin volcar la salida entera. */
    private static String detalle(ImageCommand comando) {
        List<String> errores = comando.getErrorText();
        if (errores == null || errores.isEmpty()) {
            return "";
        }
        List<String> recorte = new ArrayList<>();
        for (String linea : errores) {
            if (linea != null && !linea.isBlank()) {
                recorte.add(linea.trim());
            }
            if (recorte.size() == 3) {
                break;
            }
        }
        return recorte.isEmpty() ? "" : ": " + String.join(" | ", recorte);
    }

    /**
     * Si el selector [0] no bastara en algun coder, ImageMagick deja
     * {@code destino-N.png}. Se borran para no ensuciar el directorio temporal.
     */
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
