package universidad.mensajeria.cliente.componentes.conexion;

import com.google.gson.Gson;
import com.google.gson.JsonObject;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import universidad.mensajeria.common.codec.CodecException;
import universidad.mensajeria.common.codec.TramaCodec;
import universidad.mensajeria.common.tipos.Mensaje;
import universidad.mensajeria.common.tipos.TipoMensaje;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HexFormat;
import java.util.Map;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertAll;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.fail;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Paridad de framing entre los 3 clientes, DATA-DRIVEN: los vectores y los
 * rechazos se LEEN de {@code contrato/framing.json} — igual que el test de
 * Python (por ruta) y el de .NET (enlazado en su csproj) — y no copiados a
 * mano. Un cambio del contrato se propaga solo a esta suite.
 *
 * <p>Se compara contra {@code trama_hex} byte a byte, no solo que haya ida y
 * vuelta: una ida y vuelta con el mismo bug se cancela y no detecta nada. El
 * vector {@code multibyte_acentos} es el que mata (53 caracteres, 56 bytes):
 * medir la cabecera en caracteres deja 3 bytes cortos y se pierde todo lo que
 * venga después en el stream.</p>
 *
 * <p>Cada vector declara su payload como CADENA EXACTA (Gson emite los records
 * en orden de declaración; el contrato se escribió para coincidir), así que la
 * comparación no depende del orden de claves de nadie.</p>
 */
class TramaCodecParidadTest {

    private static final HexFormat HEX = HexFormat.of();

    /** Maven ejecuta los tests con basedir en el módulo: el contrato vive en la
     * raíz del repo, tres niveles arriba (client-app → client → monorepo → raíz). */
    private static final Path CONTRATO =
            Path.of("..", "..", "..", "contrato", "framing.json");
    private static final Gson GSON = new Gson();

    /** Mensaje de prueba de cada vector, por nombre (los campos del payload). */
    private static final Map<String, Mensaje> MENSAJES_DE_PRUEBA = Map.of(
            "ack_minimo", Mensaje.de(TipoMensaje.ACK),
            "logout_minimo", Mensaje.de(TipoMensaje.LOGOUT),
            "error_minimo", Mensaje.de(TipoMensaje.ERROR),
            "login_real", Mensaje.builder().tipo(TipoMensaje.LOGIN).id("v-1")
                    .codigo("A001").contrasena("Secreta123").build(),
            "mensaje_texto", Mensaje.builder().tipo(TipoMensaje.MENSAJE_TEXTO).id("m-1")
                    .remitente("A001").destinatario("B002").contenido("Hola").build(),
            "multibyte_acentos", Mensaje.builder().tipo(TipoMensaje.MENSAJE_TEXTO)
                    .contenido("Señor café año").build());

    private static JsonObject contrato() {
        try {
            return GSON.fromJson(Files.readString(CONTRATO, StandardCharsets.UTF_8),
                    JsonObject.class);
        } catch (IOException e) {
            throw new UncheckedIOException("No se pudo leer el contrato de framing: "
                    + CONTRATO.toAbsolutePath(), e);
        }
    }

    @Test
    @DisplayName("el contrato declara 6 vectores y 5 rechazos, y este test conoce todos")
    void elContratoCoincideConLosMensajesDePrueba() {
        JsonObject contrato = contrato();
        var nombres = contrato.getAsJsonArray("vectores");
        assertEquals(6, nombres.size(), "vectores declarados");
        assertEquals(5, contrato.getAsJsonArray("rechazos").size(), "rechazos declarados");
        for (var vector : nombres) {
            String nombre = vector.getAsJsonObject().get("nombre").getAsString();
            assertNotNull(MENSAJES_DE_PRUEBA.get(nombre),
                    "vector '" + nombre + "' sin mensaje de prueba en este test");
        }
        assertEquals(MENSAJES_DE_PRUEBA.keySet(), nombresDelContrato(contrato),
                "el mapa de prueba y el contrato deben nombrar lo mismo");
    }

    @Test
    @DisplayName("cada vector se codifica byte a byte contra trama_hex")
    void todoVectorSeCodificaByteAByte() {
        for (var elemento : contrato().getAsJsonArray("vectores")) {
            JsonObject vector = elemento.getAsJsonObject();
            String nombre = vector.get("nombre").getAsString();
            Mensaje mensaje = MENSAJES_DE_PRUEBA.get(nombre);
            byte[] trama = TramaCodec.codificar(mensaje);
            assertAll(nombre,
                    () -> assertEquals(vector.get("trama_hex").getAsString(),
                            HEX.formatHex(trama), "trama byte a byte"),
                    () -> assertEquals(vector.get("longitud").getAsInt(),
                            longitudAnunciada(trama), "cabecera = bytes UTF-8"),
                    () -> assertEquals(4 + vector.get("longitud").getAsInt(), trama.length));
        }
    }

    @Test
    @DisplayName("cada vector se codifica igual por el stream y se lee igual")
    void idaYVueltaPorElStream() throws IOException {
        for (var elemento : contrato().getAsJsonArray("vectores")) {
            JsonObject vector = elemento.getAsJsonObject();
            Mensaje mensaje = MENSAJES_DE_PRUEBA.get(vector.get("nombre").getAsString());
            ByteArrayOutputStream salida = new ByteArrayOutputStream();
            TramaCodec.escribir(salida, mensaje);

            assertAll(vector.get("nombre").getAsString(),
                    () -> assertEquals(vector.get("trama_hex").getAsString(),
                            HEX.formatHex(salida.toByteArray())),
                    () -> {
                        Mensaje leido = TramaCodec.leer(
                                new ByteArrayInputStream(salida.toByteArray()));
                        assertEquals(mensaje, leido, "ida y vuelta fiel");
                    });
        }
    }

    @Test
    @DisplayName("multibyte_acentos: 53 caracteres, 56 bytes; el bug de len(cadena) se canta")
    void elBugDeCaracteresProduciriaUnaCabeceraCorta() {
        JsonObject multibyte = vector("multibyte_acentos");
        String payload = multibyte.get("payload").getAsString();
        assertEquals(multibyte.get("caracteres").getAsInt(), payload.length());
        assertEquals(multibyte.get("longitud").getAsInt(),
                payload.getBytes(StandardCharsets.UTF_8).length);

        // Control negativo explicito: la trama con la cabecera del bug (por
        // caracteres) NO es la del contrato, aunque el payload sí coincide.
        byte[] conBug = java.nio.ByteBuffer
                .allocate(4 + payload.getBytes(StandardCharsets.UTF_8).length)
                .putInt(payload.length())
                .put(payload.getBytes(StandardCharsets.UTF_8))
                .array();
        assertNotEquals(multibyte.get("trama_hex").getAsString(), HEX.formatHex(conBug));
        assertEquals(multibyte.get("trama_hex").getAsString().substring(8),
                HEX.formatHex(conBug).substring(8), "solo cambia la cabecera");
    }

    @Test
    @DisplayName("los 5 rechazos del contrato lanzan CodecException")
    void todoRechazoDelContratoSeRechaza() throws IOException {
        for (var elemento : contrato().getAsJsonArray("rechazos")) {
            JsonObject caso = elemento.getAsJsonObject();
            assertTrue(caso.get("debe_fallar").getAsBoolean());
            byte[] trama = HEX.parseHex(caso.get("trama_hex").getAsString());
            String motivo = caso.get("motivo").getAsString();
            assertThrows(CodecException.class, () -> TramaCodec.decodificar(trama), motivo);
            // Por el stream el rechazo puede llegar como codec o como corte del
            // stream (una trama incompleta es un EOF antes de que el parser
            // opine): el contrato pide que se rechace, no el tipo exacto.
            try {
                TramaCodec.leer(new ByteArrayInputStream(trama));
                fail("debe fallar por el stream: " + motivo);
            } catch (CodecException | IOException esperada) {
                // rechazo esperado
            }
        }
    }

    @Test
    @DisplayName("el tope de trama es el que declara el contrato")
    void elTopeDeTramaEsElDelContrato() {
        assertEquals(contrato().getAsJsonObject("reglas").get("tope_trama").getAsInt(),
                TramaCodec.TAMANO_MAXIMO_TRAMA);
    }

    private static JsonObject vector(String nombre) {
        for (var elemento : contrato().getAsJsonArray("vectores")) {
            JsonObject vector = elemento.getAsJsonObject();
            if (nombre.equals(vector.get("nombre").getAsString())) {
                return vector;
            }
        }
        throw new IllegalStateException("vector no encontrado: " + nombre);
    }

    private static Set<String> nombresDelContrato(JsonObject contrato) {
        var nombres = new java.util.HashSet<String>();
        contrato.getAsJsonArray("vectores").forEach(
                v -> nombres.add(v.getAsJsonObject().get("nombre").getAsString()));
        return nombres;
    }

    /** Longitud que anuncian los 4 primeros bytes, en big-endian. */
    private static int longitudAnunciada(byte[] trama) {
        return ((trama[0] & 0xFF) << 24) | ((trama[1] & 0xFF) << 16)
                | ((trama[2] & 0xFF) << 8) | (trama[3] & 0xFF);
    }
}
