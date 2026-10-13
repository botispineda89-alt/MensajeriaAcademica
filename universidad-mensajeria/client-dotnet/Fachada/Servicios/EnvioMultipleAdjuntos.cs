using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mensajeria.Componentes;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;

namespace Mensajeria.Fachada.Servicios
{
    /// <summary>
    /// Envía N transferencias independientes estrictamente en secuencia; el protocolo
    /// fragmentado admite una sola transferencia activa y cada fragmento espera su ACK.
    /// </summary>
    public sealed class EnvioMultipleAdjuntos
    {
        private readonly Func<CoordinadorMensajeriaCliente> _coordinadorActual;
        private readonly Func<string> _usuarioActual;

        public EnvioMultipleAdjuntos(
            Func<CoordinadorMensajeriaCliente> coordinadorActual,
            Func<string> usuarioActual)
        {
            _coordinadorActual = coordinadorActual ?? throw new ArgumentNullException(nameof(coordinadorActual));
            _usuarioActual = usuarioActual ?? throw new ArgumentNullException(nameof(usuarioActual));
        }

        public async Task<ResultadoEnvioMultiple> EnviarAsync(
            IConexionRed conexion,
            string destinatario,
            IReadOnlyList<AdjuntoPendiente> adjuntos,
            IProgress<ProgresoEnvio>? progreso = null,
            CancellationToken cancelacion = default,
            string? pie = null)
        {
            ArgumentNullException.ThrowIfNull(conexion);
            ArgumentNullException.ThrowIfNull(adjuntos);
            if (adjuntos.Count == 0)
                return new ResultadoEnvioMultiple(Array.Empty<ResultadoEnvioAdjunto>(), false);

            string remitente = _usuarioActual();

            // PLAN_AMBOS FASE 5: si todo lo que hay son imágenes y son 2 o más,
            // se compone un montaje y sale UNA sola trama. El resultado sigue
            // teniendo una entrada por adjunto original, que es lo que espera la
            // barra de progreso de la vista.
            if (EsGrupoUnico(adjuntos))
            {
                return await EnviarMontajeAsync(conexion, remitente, destinatario,
                    adjuntos, progreso, cancelacion, pie).ConfigureAwait(false);
            }

            var agregado = new AgregadorProgreso(adjuntos.Select(a => a.Tamano).ToArray());
            var resultados = new List<ResultadoEnvioAdjunto>(adjuntos.Count);
            bool cancelado = false;

            for (int i = 0; i < adjuntos.Count; i++)
            {
                AdjuntoPendiente actual = adjuntos[i];
                if (cancelacion.IsCancellationRequested)
                {
                    cancelado = true;
                    MarcarNoIntentados(adjuntos, resultados, i, "cancelado");
                    break;
                }

                progreso?.Report(new ProgresoEnvio(i, adjuntos.Count, actual.Nombre,
                    agregado.Acumulado(i, 0d), agregado.BytesTotales, 0d));

                if (!File.Exists(actual.Ruta))
                {
                    resultados.Add(Fallido(actual, "El archivo ya no está disponible."));
                    continue;
                }

                long tamano;
                try { tamano = new FileInfo(actual.Ruta).Length; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    resultados.Add(Fallido(actual, "No se pudo leer el archivo."));
                    continue;
                }

                if (tamano == 0)
                {
                    resultados.Add(Fallido(actual, "El archivo está vacío."));
                    continue;
                }
                if (tamano > IFachadaCliente.MaxBytesArchivo)
                {
                    resultados.Add(Fallido(actual,
                        $"Supera el máximo de {IFachadaCliente.MaxBytesArchivo / 1048576} MB."));
                    continue;
                }
                if (!CatalogoAdjuntos.CabeEnTramaUnica(actual.Ruta, tamano))
                {
                    resultados.Add(Fallido(actual,
                        "La imagen supera el límite de envío directo (12 MB)."));
                    continue;
                }

                try
                {
                    var progresoArchivo = new ProgresoAdjunto(fraccion =>
                        progreso?.Report(new ProgresoEnvio(i, adjuntos.Count,
                            actual.Nombre, agregado.Acumulado(i, fraccion),
                            agregado.BytesTotales, fraccion)));
                    bool enRed = await EnviarUnoAsync(conexion, remitente, destinatario, actual,
                        progresoArchivo, cancelacion, PieDeEste(actual, adjuntos.Count, pie))
                    .ConfigureAwait(false);

                    resultados.Add(new ResultadoEnvioAdjunto(actual.Nombre,
                        enRed ? EstadoEnvioAdjunto.Enviado : EstadoEnvioAdjunto.EnCola,
                        enRed ? null : "Sin red: guardado para reintentar."));
                    if (!enRed)
                    {
                        cancelado = true;
                        MarcarNoIntentados(adjuntos, resultados, i + 1,
                            "No se intentó: se perdió la conexión.");
                        break;
                    }

                    progreso?.Report(new ProgresoEnvio(i, adjuntos.Count, actual.Nombre,
                        agregado.Completar(i), agregado.BytesTotales, 1d));
                }
                catch (OperationCanceledException)
                {
                    cancelado = true;
                    MarcarNoIntentados(adjuntos, resultados, i, "cancelado");
                    break;
                }
                catch (Exception ex)
                {
                    resultados.Add(Fallido(actual,
                        MensajesUsuario.Accion(ex, "enviar el archivo")));
                }
            }

            return new ResultadoEnvioMultiple(resultados, cancelado);
        }

        /// <summary>
        /// true si lo adjunto es todo imágenes y hay 2 o más: el caso en el que
        /// el plan decide una sola burbuja. Un documento no se puede componer, así
        /// que en cuanto hay uno, cada adjunto va por su cuenta.
        /// </summary>
        private static bool EsGrupoUnico(IReadOnlyList<AdjuntoPendiente> adjuntos) =>
            adjuntos.Count >= 2 && adjuntos.All(a => a.EsImagen);

        /// <summary>
        /// PLAN_AMBOS FASE 5: compone las imágenes y las manda como una sola
        /// trama. Si el montaje falla NO se pierde el envío: se avisa y cada
        /// imagen se manda por su cuenta, que es lo de siempre.
        /// </summary>
        private async Task<ResultadoEnvioMultiple> EnviarMontajeAsync(
            IConexionRed conexion, string remitente, string destinatario,
            IReadOnlyList<AdjuntoPendiente> adjuntos, IProgress<ProgresoEnvio>? progreso,
            CancellationToken cancelacion, string? pie)
        {
            var resultados = new List<ResultadoEnvioAdjunto>(adjuntos.Count);
            long total = adjuntos.Sum(a => a.Tamano);
            for (int i = 0; i < adjuntos.Count; i++)
            {
                progreso?.Report(new ProgresoEnvio(i, adjuntos.Count, adjuntos[i].Nombre,
                    0L, total, 0d));
            }

            if (cancelacion.IsCancellationRequested)
            {
                foreach (AdjuntoPendiente a in adjuntos)
                    resultados.Add(new ResultadoEnvioAdjunto(a.Nombre,
                        EstadoEnvioAdjunto.NoIntentado, "cancelado"));
                return new ResultadoEnvioMultiple(resultados, true);
            }

            // Carpeta propia por envío: el nombre del archivo es el que ve el destinatario
            // (EnviarImagenAsync lo saca de la ruta), así que tiene que ser
            // exactamente el del montaje y no "montaje_3_<guid>.png".
            string carpeta = Path.Combine(Path.GetTempPath(),
                "mensajeria-montaje-" + Guid.NewGuid().ToString("N"));
            string montaje = Path.Combine(carpeta, "montaje.png");
            MontajeImagenes.Resultado compuesto;
            try
            {
                // Fuera del hilo de la UI: componer y codificar un PNG grande es
                // trabajo de CPU, no de dispatcher.
                compuesto = await Task.Run(() => MontajeImagenes.Componer(adjuntos))
                    .ConfigureAwait(false);
            }
            catch (Exception noSeCompone)
            {
                // AQUÍ SÍ se puede reenviar suelto: no salió un solo byte por la
                // red. El montaje es una comodidad, no un requisito.
                Trace.TraceWarning("No se pudieron agrupar las imágenes: {0}",
                    noSeCompone.Message);
                return await EnviarSueltosAsync(conexion, remitente, destinatario,
                    adjuntos, progreso, cancelacion, total, pie).ConfigureAwait(false);
            }

            try
            {
                Directory.CreateDirectory(carpeta);
                await File.WriteAllBytesAsync(montaje, compuesto.Png, cancelacion)
                    .ConfigureAwait(false);
                string definitiva = Path.Combine(carpeta, compuesto.Nombre);
                File.Move(montaje, definitiva);

                bool enRed = await _coordinadorActual().EnviarImagenAsync(
                    conexion, remitente, destinatario, definitiva, pie).ConfigureAwait(false);

                long bytes = adjuntos.Sum(a => a.Tamano);
                for (int i = 0; i < adjuntos.Count; i++)
                {
                    resultados.Add(new ResultadoEnvioAdjunto(adjuntos[i].Nombre,
                        enRed ? EstadoEnvioAdjunto.Enviado : EstadoEnvioAdjunto.EnCola,
                        enRed ? null : "Sin red: guardado para reintentar."));
                    if (enRed)
                        progreso?.Report(new ProgresoEnvio(i, adjuntos.Count,
                            adjuntos[i].Nombre, bytes, bytes, 1d));
                }
                return new ResultadoEnvioMultiple(resultados, !enRed);
            }
            catch (OperationCanceledException)
            {
                foreach (AdjuntoPendiente a in adjuntos)
                    resultados.Add(new ResultadoEnvioAdjunto(a.Nombre,
                        EstadoEnvioAdjunto.NoIntentado, "cancelado"));
                return new ResultadoEnvioMultiple(resultados, true);
            }
            catch (Exception falloAlEnviar)
            {
                // NADA de reenviar aquí. A partir de la llamada a la red el
                // resultado es AMBIGUO: el servidor puede haber guardado el
                // montaje y lo que se perdió fue solo el ACK (pasa cuando el
                // servidor descarta tramas por límite de frecuencia). Reenviar
                // entonces duplicaba el contenido en el servidor, que reventaba
                // con "Duplicate entry ... for key 'archivos.ruta'" y perdía el
                // mensaje entero. Se informa y se deja el reintento para el
                // reintentador de la cola, que sí sabe si llegó a salir algo.
                Trace.TraceWarning("No se pudo enviar el montaje: {0}",
                    falloAlEnviar.Message);
                foreach (AdjuntoPendiente a in adjuntos)
                    resultados.Add(new ResultadoEnvioAdjunto(a.Nombre,
                        EstadoEnvioAdjunto.EnCola, "No se pudo enviar: se reintentará."));
                return new ResultadoEnvioMultiple(resultados, false);
            }
            finally
            {
                // Carpeta temporal propia: se borra entera, archivos y todo.
                try { if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true); }
                catch (IOException) { /* temporal, se limpia solo */ }
            }
        }

        /// <summary>
        /// Reenvía los adjuntos uno a uno. Solo se usa cuando NO ha salido nada
        /// por la red (montaje que no se pudo componer).
        /// </summary>
        private async Task<ResultadoEnvioMultiple> EnviarSueltosAsync(
            IConexionRed conexion, string remitente, string destinatario,
            IReadOnlyList<AdjuntoPendiente> adjuntos, IProgress<ProgresoEnvio>? progreso,
            CancellationToken cancelacion, long total, string? pie = null)
        {
            var resultados = new List<ResultadoEnvioAdjunto>(adjuntos.Count);
            for (int i = 0; i < adjuntos.Count; i++)
            {
                AdjuntoPendiente a = adjuntos[i];
                var porArchivo = new ProgresoAdjunto(fraccion =>
                    progreso?.Report(new ProgresoEnvio(i, adjuntos.Count, a.Nombre,
                        (long)(total * fraccion), total, fraccion)));
                // El pie se queda con la primera imagen: aunque el montaje haya
                // fallado y ahora salgan N burbujas, el texto no puede desaparecer.
                bool enRed = await EnviarUnoAsync(conexion, remitente, destinatario,
                    a, porArchivo, cancelacion, i == 0 ? pie : null).ConfigureAwait(false);
                resultados.Add(new ResultadoEnvioAdjunto(a.Nombre,
                    enRed ? EstadoEnvioAdjunto.Enviado : EstadoEnvioAdjunto.EnCola,
                    enRed ? null : "Sin red: guardado para reintentar."));
                if (!enRed)
                {
                    for (int j = i + 1; j < adjuntos.Count; j++)
                        resultados.Add(new ResultadoEnvioAdjunto(adjuntos[j].Nombre,
                            EstadoEnvioAdjunto.NoIntentado,
                            "No se intentó: se perdió la conexión."));
                    break;
                }
            }
            return new ResultadoEnvioMultiple(resultados, false);
        }

        /// <summary>
        /// El pie se pega al adjunto SOLO si ese adjunto es la única burbuja del
        /// lote. Con dos o más va como mensaje suelto antes, porque un pie no se
        /// puede repetir en N burbujas.
        /// <para>
        /// Antes el pie solo se usaba en la ruta del montaje y se ignoraba aquí:
        /// una imagen con texto salía sin texto y el usuario veía desaparecer lo
        /// que había escrito. Lo fija
        /// <c>UnaSolaImagenConTextoSaleEnLaMismaBurbuja</c>.
        /// </para>
        /// </summary>
        private static string? PieDeEste(AdjuntoPendiente adjunto, int total, string? pie) =>
            total == 1 ? pie : null;

        private async Task<bool> EnviarUnoAsync(
            IConexionRed conexion, string remitente, string destinatario,
            AdjuntoPendiente adjunto, IProgress<double>? progreso,
            CancellationToken cancelacion, string? pie = null)
        {
            CoordinadorMensajeriaCliente coordinador = _coordinadorActual();
            if (adjunto.EsImagen)
            {
                cancelacion.ThrowIfCancellationRequested();
                // UIX-27: el archivo viaja TAL CUAL. Un .webp se manda como
                // .webp, con su nombre y su bytes: el receptor tiene que
                // recibir el archivo que el otro envió, no una copia
                // renegociada. La conversión a PNG es solo para pintarlo, y
                // ocurre al recibir (ver ChatWindow.PrepararParaVer).
                bool enviada = await coordinador.EnviarImagenAsync(conexion, remitente,
                    destinatario, adjunto.Ruta, pie).ConfigureAwait(false);
                progreso?.Report(1d);
                return enviada;
            }

            return await EnviarArchivoAsync(coordinador, conexion, remitente,
                destinatario, adjunto, progreso, cancelacion, pie).ConfigureAwait(false);
        }

        private static Task<bool> EnviarArchivoAsync(CoordinadorMensajeriaCliente coordinador,
            IConexionRed conexion, string remitente, string destinatario,
            AdjuntoPendiente adjunto, IProgress<double>? progreso,
            CancellationToken cancelacion, string? pie = null) =>
            coordinador.EnviarArchivoAsync(conexion, remitente, destinatario,
                adjunto.Ruta, progreso, cancelacion, pie);

        private static ResultadoEnvioAdjunto Fallido(AdjuntoPendiente adjunto, string motivo) =>
            new(adjunto.Nombre, EstadoEnvioAdjunto.Fallido, motivo);

        private static void MarcarNoIntentados(
            IReadOnlyList<AdjuntoPendiente> adjuntos,
            ICollection<ResultadoEnvioAdjunto> resultados,
            int desde,
            string motivo)
        {
            for (int j = desde; j < adjuntos.Count; j++)
                resultados.Add(new ResultadoEnvioAdjunto(adjuntos[j].Nombre,
                    EstadoEnvioAdjunto.NoIntentado, motivo));
        }

        private sealed class ProgresoAdjunto(Action<double> reportar) : IProgress<double>
        {
            public void Report(double valor) => reportar(valor);
        }
    }
}
