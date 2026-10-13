using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Mensajeria.Componentes;
using Mensajeria.Store;
using Mensajeria.Fachada.Servicios;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;

namespace Mensajeria.Fachada
{
    public partial class FachadaCliente
    {
        // Lectores de la trama cruda
        // Solo code-behind: los campos, los eventos y la composicion viven
        // en FachadaCliente.cs; este parcial agrupa una sola responsabilidad.

        private static void LanzarSiError(Dictionary<string, JsonElement> resp,
            string defecto, params string[] okTipos)
        {
            resp.TryGetValue("tipo", out var t);
            string tipo = t.ValueKind == JsonValueKind.String ? t.GetString()! : "";
            bool esOk = okTipos.Length == 0 || Array.IndexOf(okTipos, tipo) >= 0;
            bool exito = !resp.TryGetValue("exito", out var e)
                || e.ValueKind != JsonValueKind.False;
            if (esOk && exito) return;
            string motivo = GetString(resp, "mensajeError") ?? defecto;
            throw new InvalidOperationException(motivo);
        }

        private static string? GetString(Dictionary<string, JsonElement> m, string clave) =>
            m.TryGetValue(clave, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static string? Texto(JsonElement m, string clave) =>
            m.TryGetProperty(clave, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static int? Numero(JsonElement m, string clave) =>
            m.TryGetProperty(clave, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : null;

        private static long? NumeroLong(JsonElement m, string clave) =>
            m.TryGetProperty(clave, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n) ? n : null;

        private static int? GetInt(Dictionary<string, JsonElement> m, string clave) =>
            m.TryGetValue(clave, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n) ? n : null;

        private static long? GetLong(Dictionary<string, JsonElement> m, string clave) =>
            m.TryGetValue(clave, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n) ? n : null;
    }
}
