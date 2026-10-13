using System;
using System.Collections.Generic;
using System.IO;

namespace Mensajeria.Transversal
{
    public enum ClaveIconoAdjunto
    {
        Imagen,
        DocumentoPdf,
        DocumentoTexto,
        DocumentoTabla,
        DocumentoMultiple,
        Comprimido,
        Generico
    }

    /// <summary>Clasifica adjuntos sin depender de WPF ni del transporte.</summary>
    public static class CatalogoAdjuntos
    {
        /// <summary>
        /// Formatos que el cliente trata como imagen: los que el servidor
        /// acepta en su tubería (Imagenes.esImagenValida). El <c>.webp</c> y
        /// los formatos amplios (<c>.avif</c>, <c>.heic</c>/<c>.heif</c>,
        /// <c>.svg</c>) viajan tal cual como MENSAJE_IMAGEN — el servidor los
        /// filtra (los amplios vía ImageMagick) — y el cliente los convierte
        /// a PNG solo para la vista (TranscodificadorImagen).
        /// </summary>
        public static readonly IReadOnlySet<string> ExtensionesImagen =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp",
                  ".avif", ".heic", ".heif", ".svg" };

        public const long MaxBytesImagenTramaUnica = 12L * 1024 * 1024;

        public const string FiltroDialogo =
            "Todos los archivos|*.*|Imágenes|*.png *.jpg *.jpeg *.gif *.bmp *.webp *.avif *.heic *.heif *.svg|"
            + "Documentos|*.pdf *.docx *.xlsx *.pptx *.txt *.zip";

        public static bool EsImagen(string ruta) =>
            ExtensionesImagen.Contains(Path.GetExtension(ruta));

        public static bool CabeEnTramaUnica(string ruta, long tamano) =>
            !EsImagen(ruta) || tamano <= MaxBytesImagenTramaUnica;

        public static string Mime(string ruta) => Path.GetExtension(ruta).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            ".heic" => "image/heic",
            ".heif" => "image/heif",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".zip" => "application/zip",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream"
        };

        public static ClaveIconoAdjunto Icono(string ruta) => Path.GetExtension(ruta).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp"
                or ".avif" or ".heic" or ".heif" or ".svg" => ClaveIconoAdjunto.Imagen,
            ".pdf" => ClaveIconoAdjunto.DocumentoPdf,
            ".docx" or ".doc" or ".rtf" or ".txt" or ".md" => ClaveIconoAdjunto.DocumentoTexto,
            ".xlsx" or ".xls" or ".csv" => ClaveIconoAdjunto.DocumentoTabla,
            ".pptx" or ".ppt" => ClaveIconoAdjunto.DocumentoMultiple,
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => ClaveIconoAdjunto.Comprimido,
            _ => ClaveIconoAdjunto.Generico
        };

        public static string TamanoLegible(long bytes)
        {
            double kb = bytes / 1024.0;
            return kb >= 1024
                ? FormattableString.Invariant($"{kb / 1024:0.0} MB")
                : FormattableString.Invariant($"{kb:0.0} KB");
        }
    }
}
