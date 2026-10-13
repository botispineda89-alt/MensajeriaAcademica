using System;
using System.Collections.Generic;
using System.IO;
using Mensajeria.Transversal.Dtos;
using SkiaSharp;

namespace Mensajeria.Transversal
{
    /// <summary>
    /// PLAN_AMBOS FASE 5: varias imágenes en UNA sola burbuja (espejo de
    /// <c>MontajeImagenes.java</c>).
    /// <para>
    /// El protocolo manda un adjunto por trama y la UI pinta una burbuja por
    /// mensaje. En vez de tocar el contrato (que también leería el servidor y el
    /// cliente Python), se compone aquí un <b>montaje</b> y se envía como una
    /// única <c>MENSAJE_IMAGEN</c>: una trama, una burbuja, y los otros clientes
    /// la ven igual sin que sepan nada de grupos.
    /// </para>
    /// <para>
    /// Va en <c>Transversal/</c> y usa Skia (MIT) en vez de WPF a propósito: los
    /// tests compilan esta carpeta desde un proyecto <c>net10.0</c> sin
    /// <c>UseWPF</c>, así que con Skia la composición se puede testear de verdad.
    /// </para>
    /// </summary>
    public static class MontajeImagenes
    {
        /// <summary>Máximo de imágenes por montaje (más allá son 2 montajes, no 1 burbuja).</summary>
        public const int MaximoImagenes = 10;

        /// <summary>Máximo de columnas de la retícula: más se sale de la pantalla.</summary>
        public const int ColumnasMax = 3;

        /// <summary>Lado de cada celda del montaje.</summary>
        public const int LadoCelda = 480;

        /// <summary>Separación entre celdas.</summary>
        public const int Hueco = 4;

        // Fondo entre celdas: el gris del chat, no un blanco que rompe el tema.
        // El MISMO valor que MontajeImagenes.FONDO en Java, para que las dos
        // rejillas se vean iguales en pantalla.
        private static readonly SKColor Fondo = new(0x2A, 0x2F, 0x3A);

        /// <summary>Montaje listo para enviar: PNG en memoria + nombre y retícula.</summary>
        public sealed record Resultado(byte[] Png, string Nombre, int Columnas, int Filas);

        /// <summary>
        /// Compone las imágenes en una sola. Falla si alguna no se puede leer: el
        /// llamador decide qué hacer (el comando reintenta enviándolas sueltas).
        /// </summary>
        /// <exception cref="InvalidOperationException">si alguna imagen no se decodifica.</exception>
        public static Resultado Componer(IReadOnlyList<AdjuntoPendiente> imagenes)
        {
            if (imagenes == null || imagenes.Count < 2)
                throw new InvalidOperationException("un montaje necesita al menos dos imágenes");
            if (imagenes.Count > MaximoImagenes)
                throw new InvalidOperationException($"máximo {MaximoImagenes} imágenes por montaje");

            int[] reticula = Distribucion(imagenes.Count);
            int columnas = reticula[0];
            int filas = reticula[1];
            int ancho = columnas * LadoCelda + (columnas - 1) * Hueco;
            int alto = filas * LadoCelda + (filas - 1) * Hueco;

            using var lienzo = new SKBitmap(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(lienzo))
            {
                canvas.Clear(Fondo);
                using var pintura = new SKPaint { IsAntialias = true };
                // muestreo lineal: sin esto una foto de 4000 px metida en una celda
                // de 480 sale con aliasing muy visible.
                var muestreo = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
                for (int i = 0; i < imagenes.Count; i++)
                {
                    // Cada celda se decodifica YA REDUCIDA y se suelta al
                    // pintarla: el pico de memoria es el del lienzo, no la suma de
                    // las fotos enteras. `SKBitmap.Decode(codec, info)` le dice al
                    // decodificador el tamaño destino, que es lo que evita subir a
                    // memoria una foto de 40 MP solo para pintar 480 px.
                    using SKBitmap celda = LeerReducida(imagenes[i].Ruta)
                        ?? throw new InvalidOperationException(
                            $"no se pudo leer la imagen {imagenes[i].Nombre}");
                    using var imagen = SKImage.FromBitmap(celda);
                    int x = (i % columnas) * (LadoCelda + Hueco);
                    int y = (i / columnas) * (LadoCelda + Hueco);
                    canvas.DrawImage(imagen,
                        new SKRect(x, y, x + LadoCelda, y + LadoCelda), muestreo, pintura);
                }
            }

            using var compuesto = SKImage.FromBitmap(lienzo);
            using SKData datos = compuesto.Encode(SKEncodedImageFormat.Png, 100);
            if (datos == null)
                throw new InvalidOperationException("no hay codificador PNG disponible");
            return new Resultado(datos.ToArray(),
                $"montaje_{imagenes.Count}_imagenes.png", columnas, filas);
        }

        /// <summary>
        /// Retícula para n imágenes: hasta 3 columnas y sin celdas huérfanas.
        /// <para>
        /// Puro y aparte para poder testear la aritmética sin decodificar nada.
        /// </para>
        /// </summary>
        /// <returns>[columnas, filas]</returns>
        public static int[] Distribucion(int n)
        {
            if (n < 1)
                throw new ArgumentOutOfRangeException(nameof(n), "n debe ser positivo");
            if (n == 1) return new[] { 1, 1 };
            // Una fila se lee mejor que un 2x2 con un hueco.
            if (n == 3) return new[] { 3, 1 };
            int columnas = Math.Min(ColumnasMax, n <= 4 ? 2 : 3);
            int filas = (n + columnas - 1) / columnas;
            return new[] { columnas, filas };
        }

        /// <summary>
        /// Lee el archivo reducido al lado de la celda, o <c>null</c> si Skia no
        /// sabe abrirlo.
        /// </summary>
        private static SKBitmap? LeerReducida(string ruta)
        {
            using var stream = File.OpenRead(ruta);
            using var codigo = SKCodec.Create(stream);
            if (codigo == null) return null;
            SKImageInfo info = codigo.Info;
            if (info.Width <= 0 || info.Height <= 0) return null;

            double escala = Math.Min(1.0,
                (double)LadoCelda / Math.Max(info.Width, info.Height));
            int ancho = Math.Max(1, (int)Math.Round(info.Width * escala));
            int alto = Math.Max(1, (int)Math.Round(info.Height * escala));
            return SKBitmap.Decode(codigo, new SKImageInfo(ancho, alto));
        }
    }
}