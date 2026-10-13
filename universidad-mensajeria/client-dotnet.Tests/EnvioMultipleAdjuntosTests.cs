using System.Security.Cryptography;
using System.Text.Json;
using Mensajeria.Componentes;
using Mensajeria.Fachada.Servicios;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;
using Xunit;

namespace Mensajeria.Tests;

public class EnvioMultipleAdjuntosTests
{
    [Fact]
    public async Task EnviaEnOrdenSecuencialCuandoHayDocumentos()
    {
        using var archivos = new ArchivosPrueba();
        string primero = archivos.Crear("uno.txt", [1, 2, 3]);
        string segundo = archivos.Crear("dos.txt", [4, 5, 6]);
        var red = new RedCaptura();
        var orquestador = CrearOrquestador(new HistorialCaptura());

        ResultadoEnvioMultiple resultado = await orquestador.EnviarAsync(red, "B002",
            [Adjunto(primero), Adjunto(segundo)]);

        Assert.True(resultado.TodoEnviado);
        Assert.Equal(new[]
        {
            TipoMensaje.ARCHIVO_INICIO, TipoMensaje.ARCHIVO_PARTE, TipoMensaje.ARCHIVO_FIN,
            TipoMensaje.ARCHIVO_INICIO, TipoMensaje.ARCHIVO_PARTE, TipoMensaje.ARCHIVO_FIN
        }, red.Tipos);
    }

    [Fact]
    public async Task UnWebpSaleComoImagenYConservaSusBytesYSuNombre()
    {
        // UIX-27: el archivo viaja TAL CUAL. Antes el cliente lo transcodificaba
        // a PNG al enviar y el receptor recibía un "foto.png" que el emisor
        // nunca había enviado. Lo que se manda es lo que hay en el disco.
        using var archivos = new ArchivosPrueba();
        string ruta = archivos.Crear("foto.webp", WebpDePrueba(40, 30));
        var red = new RedCaptura();

        ResultadoEnvioMultiple resultado = await CrearOrquestador(new HistorialCaptura())
            .EnviarAsync(red, "B002", [Adjunto(ruta)]);

        Assert.True(resultado.TodoEnviado);
        // MENSAJE_IMAGEN, no MENSAJE_ARCHIVO: el filtro lo ofrece como imagen.
        var enviado = Assert.Single(red.Mensajes);
        Assert.Equal(TipoMensaje.MENSAJE_IMAGEN, enviado["tipo"]);
        Assert.Equal("foto.webp", enviado["nombreArchivo"]);
        // Y los bytes son los del archivo, no un PNG renombrado.
        byte[] enviados = Convert.FromBase64String((string)enviado["contenidoImagen"]!);
        Assert.Equal(File.ReadAllBytes(ruta), enviados);
        // Cabecera RIFF/WEBP: lo que el servidor comprueba antes de aceptar.
        Assert.Equal(new byte[] { 0x52, 0x49, 0x46, 0x46 }, enviados.Take(4));
        Assert.Equal("WEBP",
            System.Text.Encoding.ASCII.GetString(enviados, 8, 4));
    }

    [Fact]
    public async Task UnWebpIlegibleIgualSaleComoImagenParaQueLoRechaceElServidor()
    {
        // Tres bytes que no son una imagen. El cliente no lo esconde ni lo
        // degrada a documento: lo manda como imagen y es el servidor, que
        // comprueba los magic bytes, quien lo rechaza con un motivo claro.
        using var archivos = new ArchivosPrueba();
        string ruta = archivos.Crear("roto.webp", [4, 5, 6]);
        var red = new RedCaptura();

        ResultadoEnvioMultiple resultado = await CrearOrquestador(new HistorialCaptura())
            .EnviarAsync(red, "B002", [Adjunto(ruta)]);

        Assert.True(resultado.TodoEnviado);
        Assert.Equal(new[] { TipoMensaje.MENSAJE_IMAGEN }, red.Tipos);
        Assert.Equal("roto.webp", red.Mensajes[0]["nombreArchivo"]);
    }

    [Fact]
    public async Task VariasImagenesSalenEnUnaSolaTrama()
    {
        // PLAN_AMBOS FASE 5: tres imágenes = una burbuja, no tres mensajes.
        using var archivos = new ArchivosPrueba();
        var rutas = new[]
        {
            Adjunto(archivos.Crear("a.png", PngDePrueba(20, 10))),
            Adjunto(archivos.Crear("b.png", PngDePrueba(20, 10))),
            Adjunto(archivos.Crear("c.png", PngDePrueba(20, 10)))
        };
        var red = new RedCaptura();

        ResultadoEnvioMultiple resultado = await CrearOrquestador(new HistorialCaptura())
            .EnviarAsync(red, "B002", rutas, pie: "las tres");

        Assert.True(resultado.TodoEnviado);
        // Un resultado por adjunto original (la barra de progreso no se descuadra)
        // pero UNA sola trama por la red.
        Assert.Equal(3, resultado.Adjuntos.Count);
        Assert.Equal(new[] { TipoMensaje.MENSAJE_IMAGEN }, red.Tipos);
        var enviado = Assert.Single(red.Mensajes);
        Assert.Equal("las tres", enviado["contenido"]);
        Assert.Equal("montaje_3_imagenes.png", enviado["nombreArchivo"]);
    }

    [Fact]
    public async Task UnDocumentoRompeElGrupoYLasImagenesVanSueltas()
    {
        using var archivos = new ArchivosPrueba();
        var rutas = new[]
        {
            Adjunto(archivos.Crear("a.png", PngDePrueba(20, 10))),
            Adjunto(archivos.Crear("b.png", PngDePrueba(20, 10))),
            Adjunto(archivos.Crear("doc.pdf", [1, 2, 3]))
        };
        var red = new RedCaptura();

        ResultadoEnvioMultiple resultado = await CrearOrquestador(new HistorialCaptura())
            .EnviarAsync(red, "B002", rutas);

        Assert.True(resultado.TodoEnviado);
        Assert.Equal(new[]
        {
            TipoMensaje.MENSAJE_IMAGEN, TipoMensaje.MENSAJE_IMAGEN,
            TipoMensaje.ARCHIVO_INICIO, TipoMensaje.ARCHIVO_PARTE, TipoMensaje.ARCHIVO_FIN
        }, red.Tipos);
    }

    [Fact]
    public async Task SiElMontajeFallaLasImagenesSeMandanSueltas()
    {
        // El montaje es una comodidad: si una imagen está corrupta no se pierde
        // el envío, se avisa con un Trace y cada una va por su cuenta.
        using var archivos = new ArchivosPrueba();
        var rotas = new[]
        {
            Adjunto(archivos.Crear("a.png", PngDePrueba(20, 10))),
            Adjunto(archivos.Crear("rota.png", [9, 9, 9]))
        };
        var red = new RedCaptura();

        ResultadoEnvioMultiple resultado = await CrearOrquestador(new HistorialCaptura())
            .EnviarAsync(red, "B002", rotas);

        Assert.Equal(2, resultado.Adjuntos.Count);
        // Dos tramas sueltas, no una de montaje.
        Assert.Equal(new[] { TipoMensaje.MENSAJE_IMAGEN, TipoMensaje.MENSAJE_IMAGEN },
            red.Tipos);
        Assert.DoesNotContain(red.Mensajes,
            m => (string)m["nombreArchivo"]! == "montaje_2_imagenes.png");
    }

    [Fact]
    public async Task UnaSolaImagenConTextoSaleEnLaMismaBurbuja()
    {
        using var archivos = new ArchivosPrueba();
        var rutas = new[] { Adjunto(archivos.Crear("a.png", PngDePrueba(20, 10))) };
        var red = new RedCaptura();

        ResultadoEnvioMultiple resultado = await CrearOrquestador(new HistorialCaptura())
            .EnviarAsync(red, "B002", rutas, pie: "mira esto");

        Assert.True(resultado.TodoEnviado);
        // UNA sola trama, y el texto va en `contenido`: no en un segundo mensaje.
        var enviado = Assert.Single(red.Mensajes);
        Assert.Equal(TipoMensaje.MENSAJE_IMAGEN, enviado["tipo"]);
        Assert.Equal("mira esto", enviado["contenido"]);
        Assert.Equal("a.png", enviado["nombreArchivo"]);
    }

    [Fact]
    public async Task ElTextoSeQuedaSinEnviarSiElAdjuntoFalla()
    {
        // Con el pie pegado al adjunto, un adjunto que no se puede enviar se
        // llevaría el texto por delante: el usuario escribía, añadía la imagen,
        // pulsaba Enviar y el texto desaparecía sin más.
        var inexistente = new AdjuntoPendiente(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "no-existe.png"),
            "no-existe.png", 100, true, "image/png");
        var red = new RedCaptura();

        ResultadoEnvioMultiple resultado = await CrearOrquestador(new HistorialCaptura())
            .EnviarAsync(red, "B002", new[] { inexistente }, pie: "mira esto");

        Assert.False(resultado.TodoEnviado);
        Assert.Equal(EstadoEnvioAdjunto.Fallido, resultado.Adjuntos[0].Estado);
    }

    [Fact]
    public async Task ArchivoGrandeSeFragmentaEnPartesDeUnMiBConHashDelStream()
    {
        using var archivos = new ArchivosPrueba();
        byte[] contenido = new byte[CoordinadorMensajeriaCliente.ParteArchivoBytes + 17];
        Random.Shared.NextBytes(contenido);
        string ruta = archivos.Crear("grande.zip", contenido);
        var red = new RedCaptura();

        ResultadoEnvioMultiple resultado = await CrearOrquestador(new HistorialCaptura())
            .EnviarAsync(red, "B002", [Adjunto(ruta)]);

        Assert.True(resultado.TodoEnviado);
        Assert.Equal(new[]
        {
            TipoMensaje.ARCHIVO_INICIO, TipoMensaje.ARCHIVO_PARTE,
            TipoMensaje.ARCHIVO_PARTE, TipoMensaje.ARCHIVO_FIN
        }, red.Tipos);
        var partes = red.Mensajes.Where(m => (string)m["tipo"]! == TipoMensaje.ARCHIVO_PARTE)
            .ToArray();
        Assert.Collection(partes,
            parte => Assert.Equal(CoordinadorMensajeriaCliente.ParteArchivoBytes,
                Convert.FromBase64String((string)parte["contenidoImagen"]!).Length),
            parte => Assert.Equal(17,
                Convert.FromBase64String((string)parte["contenidoImagen"]!).Length));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(contenido)).ToLowerInvariant(),
            red.Mensajes.Last()["hashSha256"]);
    }

    [Fact]
    public async Task FalloDeTransporteEncolaSoloElPrimeroYNoIntentaElResto()
    {
        using var archivos = new ArchivosPrueba();
        string uno = archivos.Crear("uno.txt", [1]);
        string dos = archivos.Crear("dos.txt", [2]);
        string tres = archivos.Crear("tres.txt", [3]);
        var historial = new HistorialCaptura();
        var red = new RedCaptura { FallarCon = new IOException("sin red") };
        var orquestador = CrearOrquestador(historial);

        ResultadoEnvioMultiple resultado = await orquestador.EnviarAsync(red, "B002",
            [Adjunto(uno), Adjunto(dos), Adjunto(tres)]);

        Assert.Equal(1, resultado.EnCola);
        Assert.Equal(2, resultado.NoIntentados);
        Assert.Single(historial.Pendientes);
        Assert.Single(red.Tipos);
        Assert.Equal(EstadoEnvioAdjunto.EnCola, resultado.Adjuntos[0].Estado);
        Assert.All(resultado.Adjuntos.Skip(1),
            a => Assert.Equal(EstadoEnvioAdjunto.NoIntentado, a.Estado));
    }

    [Fact]
    public async Task CancelarNoRegistraPendienteYMarcaElActualYLosSiguientesNoIntentados()
    {
        using var archivos = new ArchivosPrueba();
        string uno = archivos.Crear("uno.txt", [1, 2]);
        string dos = archivos.Crear("dos.txt", [3, 4]);
        var historial = new HistorialCaptura();
        var red = new RedCaptura { CancelarEnParte = true };
        var orquestador = CrearOrquestador(historial);

        ResultadoEnvioMultiple resultado = await orquestador.EnviarAsync(red, "B002",
            [Adjunto(uno), Adjunto(dos)]);

        Assert.True(resultado.Cancelado);
        Assert.Equal(2, resultado.NoIntentados);
        Assert.Empty(historial.Pendientes);
        Assert.Contains(historial.Estados.Values, estado => estado == "ERROR");
        Assert.Single(red.Tipos, tipo => tipo == TipoMensaje.ARCHIVO_INICIO);
        Assert.DoesNotContain(TipoMensaje.ARCHIVO_FIN, red.Tipos);
    }

    [Fact]
    public async Task ArchivoInexistenteFallaYSeContinuaConElSiguiente()
    {
        using var archivos = new ArchivosPrueba();
        string existente = archivos.Crear("valido.txt", [1]);
        string inexistente = Path.Combine(archivos.Directorio, "ausente.txt");
        var red = new RedCaptura();
        var resultado = await CrearOrquestador(new HistorialCaptura()).EnviarAsync(red, "B002",
            [Adjunto(inexistente), Adjunto(existente)]);

        Assert.Equal(EstadoEnvioAdjunto.Fallido, resultado.Adjuntos[0].Estado);
        Assert.Equal(EstadoEnvioAdjunto.Enviado, resultado.Adjuntos[1].Estado);
        Assert.Equal(3, red.Tipos.Count);
    }

    [Fact]
    public async Task RechazoDelServidorNoEncolaYContinuaConElSiguiente()
    {
        using var archivos = new ArchivosPrueba();
        string rechazado = archivos.Crear("rechazado.txt", [1]);
        string valido = archivos.Crear("valido.txt", [2]);
        var historial = new HistorialCaptura();
        var red = new RedCaptura { RechazarPrimeraSolicitud = true };

        ResultadoEnvioMultiple resultado = await CrearOrquestador(historial).EnviarAsync(red,
            "B002", [Adjunto(rechazado), Adjunto(valido)]);

        Assert.Equal(EstadoEnvioAdjunto.Fallido, resultado.Adjuntos[0].Estado);
        Assert.Equal(EstadoEnvioAdjunto.Enviado, resultado.Adjuntos[1].Estado);
        Assert.Empty(historial.Pendientes);
        Assert.Contains(historial.Estados.Values, estado => estado == "ERROR");
        Assert.Contains(historial.Estados.Values, estado => estado == "ENVIADO");
    }

    [Fact]
    public void ProgresoGlobalSePonderaPorBytes()
    {
        var progreso = new AgregadorProgreso([9L, 1L]);
        progreso.Completar(0);
        Assert.Equal(0.9, (double)progreso.Acumulado(1, 0) / progreso.BytesTotales, 3);
    }

    [Fact]
    public void ResumenCuentaCadaEstado()
    {
        var resultado = new ResultadoEnvioMultiple(
        [
            new("a", EstadoEnvioAdjunto.Enviado),
            new("b", EstadoEnvioAdjunto.EnCola),
            new("c", EstadoEnvioAdjunto.Fallido),
            new("d", EstadoEnvioAdjunto.NoIntentado)
        ], false);

        Assert.Equal((4, 1, 1, 1, 1),
            (resultado.Total, resultado.Enviados, resultado.EnCola,
                resultado.Fallidos, resultado.NoIntentados));
    }

    private static EnvioMultipleAdjuntos CrearOrquestador(HistorialCaptura historial)
    {
        var coordinador = new CoordinadorMensajeriaCliente(() => historial);
        return new EnvioMultipleAdjuntos(() => coordinador, () => "A001");
    }

    private static AdjuntoPendiente Adjunto(string ruta)
    {
        var info = new FileInfo(ruta);
        return new AdjuntoPendiente(ruta, info.Name, info.Exists ? info.Length : 1,
            CatalogoAdjuntos.EsImagen(ruta), CatalogoAdjuntos.Mime(ruta));
    }

    private static byte[] PngDePrueba(int ancho, int alto)
    {
        using var bitmap = new SkiaSharp.SKBitmap(ancho, alto);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap)) canvas.Clear(SkiaSharp.SKColors.Red);
        using var imagen = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        return datos.ToArray();
    }

    private static byte[] WebpDePrueba(int ancho, int alto)
    {
        using var bitmap = new SkiaSharp.SKBitmap(ancho, alto);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap)) canvas.Clear(SkiaSharp.SKColors.Teal);
        using var imagen = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SkiaSharp.SKEncodedImageFormat.Webp, 90);
        return datos.ToArray();
    }

    private sealed class ArchivosPrueba : IDisposable
    {
        public string Directorio { get; } = Path.Combine(Directory.GetCurrentDirectory(),
            "adjuntos-test-" + Guid.NewGuid().ToString("N"));

        public string Crear(string nombre, byte[] contenido)
        {
            Directory.CreateDirectory(Directorio);
            string ruta = Path.Combine(Directorio, nombre);
            File.WriteAllBytes(ruta, contenido);
            return ruta;
        }

        public void Dispose()
        {
            if (Directory.Exists(Directorio)) Directory.Delete(Directorio, recursive: true);
        }
    }

    private sealed class HistorialCaptura : IHistorialLocal
    {
        public List<string> Pendientes { get; } = new();
        public Dictionary<string, string> Estados { get; } = new();

        public void UpsertMensaje(string id, string origen, string destino, string tipo,
            string? contenido, string? hash, int? numCaracteres, int? numPalabras,
            string? nombreArchivo, string? rutaArchivo, long? tamanoArchivo, string fechaEnvio,
            int enviado, int descargado, string estado = "ENVIADO", string? archivoId = null,
            string? etapasFiltrado = null, string? caption = null) => Estados[id] = estado;

        public void MarcarEstado(string idMensaje, string estado) => Estados[idMensaje] = estado;
        public void RegistrarPendiente(string id, string tipo, string origen, string destino,
            string? contenido, string? nombreArchivo, byte[]? payload, string fechaCreado) =>
            Pendientes.Add(id);
        public List<Dictionary<string, object>> PaginaConversacion(string origen, string destino, int limit, int offset) => new();
        public IReadOnlyDictionary<string, string> UltimosMensajesPorContacto(string yo) => new Dictionary<string, string>();
        public List<string> BuscarEnConversacion(string origen, string destino, string patron, int limite) => new();
        public IReadOnlyDictionary<string, PreviewUltimoMensaje> UltimoMensajePorContacto(string yo) => new Dictionary<string, PreviewUltimoMensaje>();
        public string? ContenidoPorId(string idMensaje) => null;
        public int ContarConversacion(string origen, string destino) => 0;
        public void MarcarDescargado(string idMensaje, string rutaArchivo) { }
        public void ActualizarArchivoId(string idMensaje, string archivoId) { }
        public List<string> IdsNoLeidos(string yo, string otro) => new();
        public IReadOnlyDictionary<string, int> ContarNoLeidosPorContacto(string yo) => new Dictionary<string, int>();
        public void MarcarLeidos(string yo, string otro) { }
        public List<Dictionary<string, object?>> ListarPendientes() => new();
        public void IncrementarIntento(string id, string ultimoError) { }
        public void EliminarPendiente(string id) { }
    }

    private sealed class RedCaptura : IConexionRed
    {
        public List<string> Tipos { get; } = new();
        public List<Dictionary<string, object?>> Mensajes { get; } = new();
        public Exception? FallarCon { get; init; }
        public bool CancelarEnParte { get; init; }
        public bool RechazarPrimeraSolicitud { get; init; }
        private bool _rechazoEmitido;
        public event Action<Dictionary<string, JsonElement>>? OnMensajeRecibido { add { } remove { } }
        public event Action<Dictionary<string, JsonElement>>? OnCierre { add { } remove { } }
        public event Action? OnDesconectado { add { } remove { } }
        public Task ConectarAsync(string host, int puerto) => Task.CompletedTask;
        public void Desconectar() { }
        public void Enviar(Dictionary<string, object?> msg) => throw new NotSupportedException();
        public Task<Dictionary<string, JsonElement>> Pedir(Dictionary<string, object?> msg) =>
            Pedir(msg, TimeSpan.FromSeconds(1));

        public Task<Dictionary<string, JsonElement>> Pedir(
            Dictionary<string, object?> msg, TimeSpan timeout)
        {
            Mensajes.Add(new Dictionary<string, object?>(msg));
            string tipo = (string)msg["tipo"]!;
            Tipos.Add(tipo);
            if (CancelarEnParte && tipo == TipoMensaje.ARCHIVO_PARTE)
                throw new OperationCanceledException();
            if (FallarCon != null) throw FallarCon;
            string tipoRespuesta = tipo == TipoMensaje.MENSAJE_IMAGEN
                ? TipoMensaje.IMAGE_FILTERED_RESULT : TipoMensaje.ACK;
            bool exito = true;
            if (RechazarPrimeraSolicitud && !_rechazoEmitido)
            {
                _rechazoEmitido = true;
                exito = false;
            }
            using JsonDocument documento = JsonDocument.Parse(JsonSerializer.Serialize(
                new Dictionary<string, object?>
                {
                    ["tipo"] = tipoRespuesta,
                    ["exito"] = exito,
                    ["mensajeError"] = exito ? null : "rechazado"
                }));
            return Task.FromResult(documento.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone()));
        }
    }
}
