package universidad.mensajeria.server.fachadadeservicios.casosdeuso;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import universidad.mensajeria.common.interno.EventoEtapa;
import universidad.mensajeria.common.interno.IdSesion;
import universidad.mensajeria.common.interno.ResultadoMensajeDTO;
import universidad.mensajeria.common.tipos.Mensaje;
import universidad.mensajeria.common.tipos.TipoMensaje;
import universidad.mensajeria.server.conexionesclientes.InterfazConexionesClientes;

import java.time.LocalDateTime;
import java.util.List;

/**
 * Entrega TCP y acuses del procesamiento (E10 hacia la red via protocolo).
 * Movido verbatim desde FachadaDeServicios para que la Fachada solo
 * coordine: entrega a sesiones vivas (+acuse al remitente), presencia y
 * respuestas ACK/IMAGEN/ERROR del camino de procesamiento.
 */
public final class EntregaCaso {

    private static final Logger LOG = LoggerFactory.getLogger(EntregaCaso.class);

    private final universidad.mensajeria.protocolo.InterfazProtocoloComunicacion protocolo;
    private final InterfazConexionesClientes conexiones;

    public EntregaCaso(universidad.mensajeria.protocolo.InterfazProtocoloComunicacion protocolo,
                       InterfazConexionesClientes conexiones) {
        this.protocolo = protocolo;
        this.conexiones = conexiones;
    }

    public void entregar(String codigo, Mensaje entrega) {
        entregar(codigo, entrega, null);
    }

    /**
     * Entrega al destinatario y eco a las OTRAS sesiones del remitente (sync
     * multi-PC: PC1 = PC2 = PC15). Se excluye la sesión origen porque ella ya
     * correlaciona su ACK/IMAGE_FILTERED_RESULT por el mismo id: si le llegara
     * el eco con igual id, lo tomaría por respuesta y fallaría el envío.
     * El eco usa el mismo id (upsert idempotente en el cliente).
     */
    public void entregar(String codigo, Mensaje entrega, IdSesion excepto) {
        boolean alMenosUna = false;
        for (IdSesion s : conexiones.sesionesDe(codigo)) {
            try {
                protocolo.enviar(s, entrega);
                alMenosUna = true;
            } catch (Exception e) {
                // Un adjunto grande no cabe en UNA trama (16 MiB): los bytes van
                // en base64 dentro del mismo frame que los metadatos. Antes esto
                // mataba la sesion del destinatario y el adjunto se perdia.
                // Se reintenta con la version ligera (sin contenido) y el
                // cliente lo baja con DESCARGAR_ARCHIVO, que ya hacen los tres.
                if (excedeTrama(e) && enviarLigero(s, entrega)) {
                    alMenosUna = true;
                    continue;
                }
                LOG.warn("No se pudo entregar a {}: {}", codigo, e.getMessage());
                conexiones.remover(codigo, s);
            }
        }
        ecoRemitente(codigo, entrega, excepto);
        // Acuse de entrega al remitente (aunque esté offline queda persistido).
        if (entrega.remitente() != null && entrega.id() != null) {
            Mensaje acuse = Mensaje.builder()
                    .tipo(TipoMensaje.MENSAJE_ENTREGADO)
                    .fechaHora(LocalDateTime.now().toString())
                    .remitente(codigo)
                    .destinatario(entrega.remitente())
                    .contenido(entrega.id())
                    .build();
            for (IdSesion s : conexiones.sesionesDe(entrega.remitente())) {
                try {
                    protocolo.enviar(s, acuse);
                } catch (Exception e) {
                    LOG.debug("No se pudo acusar entrega a {}: {}",
                            entrega.remitente(), e.getMessage());
                }
            }
            if (!alMenosUna) {
                LOG.debug("Entrega offline a {} (id {}): igual se acusa", codigo, entrega.id());
            }
        }
    }

    /** El codec rechaza el frame antes de escribir nada: se puede reintentar. */
    private boolean excedeTrama(Exception fallo) {
        String motivo = fallo.getMessage();
        return motivo != null && motivo.contains("excede el maximo");
    }

    /** Reintento sin los bytes: el receptor conserva metadatos y archivoId. */
    private boolean enviarLigero(IdSesion sesion, Mensaje entrega) {
        if (entrega.contenidoImagen() == null || entrega.contenidoImagen().isBlank()) {
            return false;
        }
        try {
            protocolo.enviar(sesion, sinContenido(entrega));
            return true;
        } catch (Exception e) {
            LOG.debug("Tampoco cupo la version ligera de {}: {}",
                    entrega.id(), e.getMessage());
            return false;
        }
    }

    /**
     * Copia del mismo mensaje sin los bytes en linea. Se conserva {@code contenido}
     * porque en un adjunto es el pie (caption), no el archivo; lo pesado vive solo
     * en {@code contenidoImagen}.
     */
    static Mensaje sinContenido(Mensaje m) {
        return new Mensaje(m.tipo(), m.id(), m.fechaHora(), m.codigo(), m.contrasena(),
                m.nombres(), m.apellidos(), m.programa(), m.remitente(), m.destinatario(),
                m.contenido(), m.hashSha256(), m.numCaracteres(), m.numPalabras(),
                m.etapasFiltrado(), m.nombreArchivo(), m.tamanoArchivo(), m.archivoId(),
                m.mime(), null, m.pagina(), m.totalPaginas(), m.exito(), m.mensajeError(),
                m.ipRemitente(), m.ipDestinatario(), m.usuariosConectados(), m.totalPartes(),
                m.indiceParte(), m.tipoOriginal());
    }

    private void ecoRemitente(String codigoDestino, Mensaje entrega, IdSesion excepto) {
        String remitente = entrega.remitente();
        if (remitente == null || remitente.isBlank() || remitente.equals(codigoDestino)) {
            return;
        }
        for (IdSesion s : conexiones.sesionesDe(remitente)) {
            if (excepto != null && excepto.id() != null && excepto.id().equals(s.id())) {
                continue;
            }
            try {
                protocolo.enviar(s, entrega);
            } catch (Exception e) {
                // Mismo motivo que en el destinatario: el eco de un adjunto grande
                // tampoco cabe. Se manda la version ligera; si ni esa cabe, el
                // otro PC lo synchroniza con la pagina remota 0 al reconectar.
                if (excedeTrama(e) && enviarLigero(s, entrega)) {
                    continue;
                }
                LOG.debug("No se pudo enviar eco a {}: {}", remitente, e.getMessage());
            }
        }
    }

    /** Broadcast de presencia a todas las sesiones vivas. */
    public void emitirPresencia(String codigo, boolean conectado) {
        List<String> todos = conexiones.codigosConectados();
        Mensaje aviso = Mensaje.builder()
                .tipo(TipoMensaje.PRESENCIA)
                .fechaHora(LocalDateTime.now().toString())
                .codigo(codigo)
                .contenido(conectado ? "conectado" : "desconectado")
                .usuariosConectados(todos)
                .build();
        for (String destino : todos) {
            for (IdSesion s : conexiones.sesionesDe(destino)) {
                try {
                    protocolo.enviar(s, aviso);
                } catch (Exception e) {
                    LOG.debug("No se pudo avisar presencia a {}: {}", destino, e.getMessage());
                }
            }
        }
    }

    public void responder(IdSesion origen, String idSolicitud, TipoMensaje tipo) {
        if (origen == null) {
            return;
        }
        try {
            protocolo.enviar(origen, Mensaje.builder()
                    .tipo(tipo)
                    .id(idSolicitud)
                    .fechaHora(LocalDateTime.now().toString())
                    .exito(true)
                    .build());
        } catch (Exception e) {
            LOG.warn("No se pudo responder a {}: {}", origen, e.getMessage());
        }
    }

    public void responderImagen(IdSesion origen, Mensaje trama, ResultadoMensajeDTO dto) {
        if (origen == null) {
            return;
        }
        try {
            protocolo.enviar(origen, Mensaje.builder()
                    .tipo(TipoMensaje.IMAGE_FILTERED_RESULT)
                    .id(trama.id())
                    .fechaHora(LocalDateTime.now().toString())
                    .exito(true)
                    .hashSha256(dto.hashSha256())
                    .etapasFiltrado(etapasJson(dto))
                    .archivoId(dto.archivoId() == null ? null : String.valueOf(dto.archivoId()))
                    .build());
        } catch (Exception e) {
            LOG.warn("No se pudo responder a {}: {}", origen, e.getMessage());
        }
    }

    public void responderError(IdSesion origen, Mensaje trama, Throwable fallo) {
        if (origen == null) {
            return;
        }
        Throwable causa = fallo instanceof java.util.concurrent.CompletionException
                && fallo.getCause() != null ? fallo.getCause() : fallo;
        String motivo = causa.getMessage() != null ? causa.getMessage() : "error interno";
        if (causa instanceof universidad.mensajeria.common.interno.UsuarioNoEncontradoException
                || causa instanceof IllegalArgumentException
                || causa instanceof IllegalStateException) {
            try {
                protocolo.enviar(origen, Mensaje.builder()
                        .tipo(TipoMensaje.ERROR)
                        .id(trama.id())
                        .fechaHora(LocalDateTime.now().toString())
                        .exito(false)
                        .mensajeError(motivo)
                        .build());
            } catch (Exception e) {
                LOG.warn("No se pudo avisar error a {}: {}", origen, e.getMessage());
            }
        } else {
            LOG.error("Error procesando {} de {}: {}", trama.tipo(), origen, motivo);
            try {
                protocolo.enviar(origen, Mensaje.builder()
                        .tipo(TipoMensaje.ERROR)
                        .id(trama.id())
                        .fechaHora(LocalDateTime.now().toString())
                        .exito(false)
                        .mensajeError("error interno procesando " + trama.tipo())
                        .build());
            } catch (Exception e) {
                LOG.warn("No se pudo avisar error a {}: {}", origen, e.getMessage());
            }
        }
    }

    /** Foto de IPs vivas del destinatario (null si offline): no es historica. */
    public String fotoIpDestinatario(String codigoDestinatario) {
        if (codigoDestinatario == null || codigoDestinatario.isBlank()) {
            return null;
        }
        String ips = conexiones.sesionesDe(codigoDestinatario.trim()).stream()
                .map(IdSesion::direccionIp)
                .filter(ip -> ip != null && !ip.isBlank())
                .distinct()
                .sorted()
                .collect(java.util.stream.Collectors.joining(", "));
        return ips.isBlank() ? null : ips;
    }

    public static String etapasJson(ResultadoMensajeDTO dto) {
        StringBuilder json = new StringBuilder("[");
        for (int i = 0; i < dto.traza().size(); i++) {
            EventoEtapa etapa = dto.traza().get(i);
            if (i > 0) {
                json.append(",");
            }
            json.append("{\"etapa\":\"").append(etapa.etapa())
                    .append("\",\"ms\":").append(etapa.ms())
                    .append(",\"ruta\":\"").append(etapa.rutaSalida()).append("\"}");
        }
        return json.append("]").toString();
    }
}
