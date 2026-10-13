using System;
using System.Collections.Generic;
using System.Text.Json;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;

namespace Mensajeria.Componentes
{
    /// <summary>Adaptador JSON para los campos de directorio e historial del servidor.</summary>
    public sealed class MapeadorDatosServidor : IMapeadorDatos
    {
        public List<UsuarioLocal> MapearDirectorio(string json)
        {
            using var documento = JsonDocument.Parse(json);
            var usuarios = new List<UsuarioLocal>();
            if (documento.RootElement.ValueKind != JsonValueKind.Array) return usuarios;

            foreach (var fila in documento.RootElement.EnumerateArray())
            {
                if (fila.ValueKind != JsonValueKind.Object) continue;
                string? codigo = Texto(fila, "codigo");
                if (string.IsNullOrWhiteSpace(codigo)) continue;
                usuarios.Add(new UsuarioLocal(
                    codigo,
                    Texto(fila, "nombres"),
                    Texto(fila, "apellidos"),
                    Texto(fila, "programa"),
                    Booleano(fila, "conectado"),
                    Texto(fila, "fechaRegistro"),
                    Texto(fila, "actualizado")));
            }
            return usuarios;
        }

        public List<MensajeLocal> MapearPaginaHistorial(string json, string codigoSesion)
        {
            if (string.IsNullOrWhiteSpace(codigoSesion))
                throw new ArgumentException("la sesión necesita un código", nameof(codigoSesion));
            using var documento = JsonDocument.Parse(json);
            var mensajes = new List<MensajeLocal>();
            if (documento.RootElement.ValueKind != JsonValueKind.Array) return mensajes;

            foreach (var fila in documento.RootElement.EnumerateArray())
            {
                if (fila.ValueKind != JsonValueKind.Object) continue;
                string? id = Texto(fila, "id");
                string? origen = Texto(fila, "remitente");
                string? destino = Texto(fila, "destinatario");
                string? tipoCrudo = Texto(fila, "tipo");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(origen)
                    || string.IsNullOrWhiteSpace(destino) || string.IsNullOrWhiteSpace(tipoCrudo))
                    continue;

                string tipo = NormalizarTipoMensaje(tipoCrudo);
                string? nombre = Texto(fila, "nombreArchivo");
                bool propio = string.Equals(origen, codigoSesion, StringComparison.Ordinal);
                // La pagina remota nunca trae bytes: en un adjunto el `contenido`
                // del servidor es el pie/caption y los bytes van aparte. Se
                // reetiqueta igual que en el cliente Java.
                bool esAdjunto = tipo == TipoMensaje.MENSAJE_IMAGEN
                    || tipo == TipoMensaje.MENSAJE_ARCHIVO;
                string? pie = esAdjunto ? NormalizarPie(Texto(fila, "contenido")) : null;
                string? contenido = esAdjunto ? null : Texto(fila, "contenido");
                mensajes.Add(new MensajeLocal(
                    id, origen, destino, tipo, contenido,
                    Texto(fila, "hashSha256"), Entero(fila, "numCaracteres"),
                    Entero(fila, "numPalabras"), nombre, null,
                    EnteroLargo(fila, "tamanoArchivo"),
                    Texto(fila, "fechaEnvio") ?? DateTime.UtcNow.ToString("O"),
                    propio, false, propio ? "ENVIADO" : "ENTREGADO",
                    Texto(fila, "archivoId"), Texto(fila, "etapasFiltrado"), pie));
            }
            return mensajes;
        }

        /// <summary>Pie en blanco se normaliza a null (espejo de normalizarPie Java).</summary>
        private static string? NormalizarPie(string? pie) =>
            string.IsNullOrWhiteSpace(pie) ? null : pie;

        public string NormalizarTipoMensaje(string tipo)
        {
            // El SERVIDOR guarda los adjuntos y los textos con el nombre corto
            // ("TEXTO", "IMAGEN", "ARCHIVO") porque es lo que espera el enum
            // TipoContenido de la entidad JPA, mientras que el contrato del
            // cliente usa el prefijo ("MENSAJE_TEXTO"...). Sin este diccionario
            // la pagina de historial remoto revienta con ArgumentException y el
            // chat se queda vacio con "No se pudo sincronizar el historial".
            // Mismo diccionario que normalizarTipoMensaje del cliente Java.
            switch (tipo)
            {
                case "TEXTO" or "MENSAJE_TEXTO": return TipoMensaje.MENSAJE_TEXTO;
                case "IMAGEN" or "MENSAJE_IMAGEN": return TipoMensaje.MENSAJE_IMAGEN;
                case "ARCHIVO" or "MENSAJE_ARCHIVO": return TipoMensaje.MENSAJE_ARCHIVO;
            }
            // Un tipo que no es del contrato se rechaza: nunca se "aproxima" a
            // otro, porque un mensaje guardado como si fuera de otro tipo
            // mostraría bytes donde va texto.
            if (Enum.TryParse(tipo, ignoreCase: false, out TipoMensajeCliente tipoEnum)
                && string.Equals(Enum.GetName(tipoEnum), tipo, StringComparison.Ordinal))
                return tipo;
            throw new ArgumentException($"tipo de mensaje no reconocido: {tipo}", nameof(tipo));
        }

        private static string? Texto(JsonElement fila, string nombre) =>
            fila.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.String
                ? valor.GetString() : null;

        private static bool Booleano(JsonElement fila, string nombre) =>
            fila.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.True;

        private static int? Entero(JsonElement fila, string nombre) =>
            fila.TryGetProperty(nombre, out var valor) && valor.TryGetInt32(out int resultado)
                ? resultado : null;

        private static long? EnteroLargo(JsonElement fila, string nombre) =>
            fila.TryGetProperty(nombre, out var valor) && valor.TryGetInt64(out long resultado)
                ? resultado : null;
    }
}
