package universidad.mensajeria.cliente.componentes.conexion;

import universidad.mensajeria.common.tipos.Mensaje;
import universidad.mensajeria.common.tipos.TipoMensaje;

import java.io.ByteArrayOutputStream;
import java.security.MessageDigest;
import java.util.ArrayList;
import java.util.Base64;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

/**
 * Reensambla la descarga fragmentada de un archivo grande (> 1 MiB).
 * Espejo de ColectorFragmentos en .NET y colector_fragmentos.py en Python
 * (Invariante 20 de AGENTS.md).
 */
public final class ColectorFragmentos {

    private final String idEsperado;
    private final Map<Integer, byte[]> partes = new ConcurrentHashMap<>();
    private final CompletableFuture<byte[]> resultado = new CompletableFuture<>();
    private volatile int totalPartes = -1;
    private volatile String hashEsperado;
    private volatile String nombreArchivo;
    private volatile String mime;

    public ColectorFragmentos(String idEsperado) {
        this.idEsperado = idEsperado;
    }

    public String idEsperado() {
        return idEsperado;
    }

    public String nombreArchivo() {
        return nombreArchivo;
    }

    public String mime() {
        return mime;
    }

    public boolean corresponde(Mensaje trama) {
        if (trama == null) {
            return false;
        }
        String idTrama = trama.archivoId() != null ? trama.archivoId() : trama.id();
        return idEsperado != null && idEsperado.equals(idTrama);
    }

    public void alimentar(Mensaje trama) {
        if (!corresponde(trama)) {
            return;
        }
        TipoMensaje tipo = trama.tipo();
        if (tipo == TipoMensaje.ARCHIVO_INICIO) {
            if (trama.totalPartes() != null) {
                this.totalPartes = trama.totalPartes();
            }
            if (trama.nombreArchivo() != null) {
                this.nombreArchivo = trama.nombreArchivo();
            }
            if (trama.mime() != null) {
                this.mime = trama.mime();
            }
        } else if (tipo == TipoMensaje.ARCHIVO_PARTE) {
            Integer indice = trama.indiceParte();
            String b64 = trama.contenidoImagen();
            if (indice != null && b64 != null) {
                try {
                    byte[] bytes = Base64.getDecoder().decode(b64);
                    partes.put(indice, bytes);
                    verificarCompletitud();
                } catch (IllegalArgumentException e) {
                    resultado.completeExceptionally(new IllegalStateException("parte " + indice + " base64 invalido", e));
                }
            }
        } else if (tipo == TipoMensaje.ARCHIVO_FIN) {
            this.hashEsperado = trama.hashSha256();
            verificarCompletitud();
        } else if (tipo == TipoMensaje.ERROR) {
            String motivo = trama.mensajeError() != null ? trama.mensajeError() : "error del servidor en descarga";
            resultado.completeExceptionally(new IllegalStateException(motivo));
        }
    }

    private synchronized void verificarCompletitud() {
        if (totalPartes > 0 && partes.size() == totalPartes && hashEsperado != null) {
            if (resultado.isDone()) {
                return;
            }
            try {
                List<Integer> indices = new ArrayList<>(partes.keySet());
                Collections.sort(indices);
                ByteArrayOutputStream buffer = new ByteArrayOutputStream();
                for (int i : indices) {
                    byte[] bloque = partes.get(i);
                    if (bloque != null) {
                        buffer.write(bloque);
                    }
                }
                byte[] bytesCompletos = buffer.toByteArray();
                String hashCalculado = sha256(bytesCompletos);
                if (!hashCalculado.equalsIgnoreCase(hashEsperado)) {
                    resultado.completeExceptionally(new IllegalStateException(
                            "sha256 de la descarga no coincide: esperado=" + hashEsperado + ", calculado=" + hashCalculado));
                    return;
                }
                resultado.complete(bytesCompletos);
            } catch (Exception e) {
                resultado.completeExceptionally(e);
            }
        }
    }

    public byte[] esperar(long tiempo, TimeUnit unidad) throws Exception {
        try {
            return resultado.get(tiempo, unidad);
        } catch (TimeoutException e) {
            throw new TimeoutException("descarga fragmentada no completada a tiempo (" + partes.size() + "/" + totalPartes + " partes recibidas)");
        }
    }

    private static String sha256(byte[] bytes) throws Exception {
        MessageDigest digest = MessageDigest.getInstance("SHA-256");
        byte[] hash = digest.digest(bytes);
        StringBuilder hex = new StringBuilder(hash.length * 2);
        for (byte b : hash) {
            hex.append(Character.forDigit((b >> 4) & 0xF, 16));
            hex.append(Character.forDigit(b & 0xF, 16));
        }
        return hex.toString();
    }
}
