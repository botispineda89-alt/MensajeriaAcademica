# Clientes JavaFX y WPF — lote de octubre 2026 (ejecutado)

> **Documento histórico.** El 2026-10-10 se retiraron de la raíz `PLAN_AMBOS.md` y `PLAN3.MD`
> porque sus fases están implementadas. Este archivo conserva **qué se decidió y qué se cambió**
> para que nadie tenga que releer los planes; **el estado vigente y los pendientes viven en
> [`ESTADO_ROADMAP.md`](../ESTADO_ROADMAP.md)**. No es normativa: si algo de aquí contradice al
> roadmap, manda el roadmap.

Alcance del lote: `universidad-mensajeria/client/` (JavaFX), `universidad-mensajeria/client-dotnet/`
(WPF) y `universidad-mensajeria/server/utilerias/` (solo el filtro de imágenes). **Python no se
tocó**; `common-protocol` solo se amplió con `caption` en el esquema canónico, y `contrato/` solo
para registrar firmas nuevas en `puertos.json`.

---

## 1. Envío múltiple de adjuntos en WPF (antes `PLAN3.MD`)

**Por qué el enunciado original era incorrecto para este repositorio.** El backend no es HTTP:
no hay `MultipartFormDataContent`, ni `HttpClient`, ni WebSocket, ni SignalR. El contrato es
`[4 bytes big-endian de longitud][JSON UTF-8]` (`PROTOCOLO.md`, `TramaCodec`), con **tope de 16 MiB
por trama**. Cuatro decisiones quedaron fijadas antes de escribir código:

| # | Decisión | Por qué |
|---|---|---|
| D1 | El envío múltiple son **N transferencias fragmentadas** sobre el `IConexionRed` existente. Cero HTTP. | Es el único transporte que habla el servidor. |
| D2 | **Secuencial**, nunca `Task.WhenAll`. | `PROTOCOLO.md` §4 exige partes secuenciales (una en vuelo) con `ACK` por parte; además `ConexionRed` serializa con un `SemaphoreSlim`, así que paralelizar solo intercalaría tramas de transferencias distintas. |
| D3 | La **cancelación del usuario no es un fallo de transporte**: se comprueba antes de que el coordinador pueda encolar nada. | `OperationCanceledException` no está en `EsFalloTransporte`; hoy no se encola por accidente, no por diseño. |
| D4 | `.webp` se ofrece en el diálogo; el envío como imagen se resuelve con transcodificación (ver §2), no con un soporte nuevo del servidor. | Decisión superada después por el trabajo B: el `.webp` se transcodifica a PNG y el servidor acabó aceptando WebP (§4). |

**Lo que se implementó en WPF:** selección múltiple (`OpenFileDialog.Multiselect`), arrastre
múltiple, bandeja reactiva deduplicada con miniatura o ícono por extensión, envío secuencial con
progreso **ponderado por bytes**, cancelación cooperativa, tope de 50 MiB por archivo (12 MiB para
imágenes directas) y aviso de lote por encima de `adjuntos.lote-max-mb`. Ante cancelación no se
registra ninguna fila en la cola offline; ante fallo de transporte se encola **solo el archivo que
falló** y los posteriores quedan sin intentar.

**Clases nuevas:** `CatalogoAdjuntos`, `PlanEnvioAdjuntos`, `MontajeImagenes`,
`TranscodificadorImagen`, `ProgresoEnvio`, `AdjuntoVM`, `AdjuntosPanelVM`, `BotonGuardar`,
`FormatoPreview`, `FormatoCita`, `EnvioMultipleAdjuntos`, `ResultadoEnvioMultiple`,
`AdjuntoPendiente`. **Deuda asumida:** JavaFX ya tiene selección/arrastre múltiple y bandeja de
hasta 5 adjuntos, pero no progreso por bytes ni cancelación; Python sigue aceptando un adjunto por
operación. Queda completar esas capacidades y probarlas allí (§5).

---

## 2. Los seis fallos de JavaFX y .NET (antes `PLAN_AMBOS.md`)

| # | Fallo | Cliente | Qué se hizo |
|---|---|---|---|
| 1 | El aviso inline de éxito se quedaba pegado para siempre | .NET | `EstadoAvisoInline` con plazos por tipo y `DispatcherTimer` |
| 2 | Un `.webp` no se comportaba como imagen en ninguna de las tres capas | ambos | `TranscodificadorImagen` (Java y .NET) + fin del bucle de recarga infinita |
| 3 | No se podían descargar las imágenes | ambos | El botón de guardar se pinta también en el bloque de imagen |
| 4 | El visor de JavaFX abría la miniatura de 300 px y su "1:1" no era 1:1 | JavaFX | Original en disco + `EscalaVisor` (zoom real, tope 1:1) |
| 5 | Varias imágenes producían N burbujas en .NET | .NET | `PlanEnvioAdjuntos` + `MontajeImagenes` (rejilla 1×1 / 2×1 / 3×1 / 2×2 / 3×2 / 3×3) |
| 6 | La lista de contactos de .NET no se mantenía al día | .NET | Refresco agrupado (400 ms, *trailing edge*) que actualiza no leídos + preview + orden en una pasada |

**La decisión que condiciona el punto 2:** ni JavaFX (`javafx.scene.image.Image`) ni WPF
(`BitmapImage`) decodifican WebP, y el `ImageIO` del JDK tampoco. La primera solución fue
**transcodificar en el cliente** (WebP → PNG) en vez de tocar el servidor; **esa parte quedó
superada después** (§4): hoy el `.webp` viaja tal cual y la conversión es **solo para pintar**. Las
dependencias que hizo falta sí se mantienen, porque el servidor y los clientes siguen necesitando un
lector WebP:

| Cliente | Paquete | Licencia | Motivo |
|---|---|---|---|
| JavaFX | `com.twelvemonkeys.imageio:imageio-webp:3.12.0` | BSD-3 | Lector WebP por SPI de ImageIO (solo lector: no escribe WebP). |
| .NET | `SkiaSharp` + `SkiaSharp.NativeAssets.Win32` 4.153.1 | MIT | **`SixLabors.ImageSharp` se descartó**: la 4.x exige licencia comercial de pago (rompe el build) y la 3.x arrastra avisos NU1902/NU1903 en cada compilación. Skia además resuelve el montaje de imágenes sin WPF, que es lo que lo hace verificable desde la suite (`net10.0`). |

Consecuencias que quedaron anotadas: convertir a PNG **agranda** el archivo (un WebP de 300 KB puede
salir 2 MB); sin *sniffing* de magic bytes, la conversión decide por extensión; y enviar un `.webp`
corrupto ya **no** degrada a documento: se manda y el servidor lo rechaza con un motivo claro.

`contrato/puertos.json` se registró con **11 firmas nuevas** (el invariante 13 de `AGENTS.md` §5 lo
exige) sin tocar semántica de protocolo.

---

## 3. Lo que destapó la prueba en pantalla (rondas 2 a 4)

Las suites estaban en verde y aun así el usuario veía fallos. Cuatro cosas de la ronda 2:

1. **El aviso de error que no se quitaba era una decisión equivocada.** La regla original era "un
   error que desaparece es un error que nadie lee"; en pantalla tapaba el chat para siempre. **Todos
   los avisos se van solos**, con plazos por tipo (4 s / 6 s / 8 s / **12 s** para el error).
2. **JavaFX no tenía aviso inline.** Se añadió con el mismo comportamiento (`EstadoAvisoInline`,
   `TipoAviso`, `Timeline`), cableado al envío, al fallo de envío y a la pérdida/recuperación de red.
3. **El preview de .NET sacaba "Tú: " de la columna `enviado`**, que está mal en 17 filas reales
   (las escribe el sincronizador de `SYNC_LOGIN`), así que los mensajes **recibidos** se anunciaban
   como propios. Ahora sale de `origen = yo`, igual que JavaFX.
4. **El agrupado del refresco descartaba en vez de fusionar.** El segundo mensaje de una ráfaga caía
   dentro de la ventana de 400 ms y no programaba nada; como la consulta va en segundo plano, ese
   preview no llegaba a pintarse. Ahora hay *trailing edge*, y `MENSAJE_ARCHIVO` entró en el filtro.

**Ronda 4 — los dos bugs de raíz que el usuario estaba viendo:**

1. **"Siempre sale Tú:"** no era el prefijo, era **la fecha**. En la misma tabla convivían dos
   formatos de `fecha_envio`: lo que sale de este cliente, **UTC con `Z`** (`2026-10-11T03:14:51Z`),
   y lo que llega del servidor, **hora local sin zona** (`2026-10-10T22:14:51`). La columna es
   `TEXT` y las consultas ordenan por ella **como texto**: `2026-10-10…` < `2026-10-11…` siempre, así
   que todo lo que llegaba quedaba por detrás de lo que se había enviado. Arreglo:
   `FormatoFecha.AUtc` normaliza al guardar y `LocalStore.NormalizarFechasAUtc` **migra las filas ya
   guardadas** al arrancar (578 filas convertidas en la base real). JavaFX y Python escriben hora
   local sin zona, que también es coherente consigo mismo: lo que no vale es mezclar los dos.
2. **"No me deja mandar imágenes y texto en la misma burbuja"** — el texto **se perdía**: el pie solo
   se pasaba en la ruta del montaje (2+ imágenes) y la imagen única llamaba a `EnviarImagenAsync`
   **sin pie**. Se reprodujo con un test que fallaba (`Expected: mira esto / Actual: null`) antes de
   arreglarlo. Hoy el pie viaja en el `contenido` de la **misma** trama `MENSAJE_IMAGEN` /
   `ARCHIVO_INICIO` y se persiste en la columna `caption`.

---

## 4. WebP en el servidor (añadido después, `server/utilerias`)

`Imagenes.esImagenValida` aceptaba solo los magic bytes de PNG/JPEG/GIF/BMP, así que una imagen
WebP enviada como `MENSAJE_IMAGEN` se rechazaba y el usuario perdía la foto. Ahora:

- `server/utilerias` depende de `imageio-webp` (mismo plugin BSD-3 que el cliente JavaFX) para que la
  **tubería** de filtros sepa decodificar, no solo la cabecera.
- `esImagenValida` reconoce el contenedor `RIFF` comprobando la firma `WEBP` en el **offset 8**:
  un `.wav` o un `.avi` empiezan igual y no son imágenes, y colarlos sería justo el agujero que la
  validación mágica cerró en su día.
- Tests: `ImagenMagicaTest` (acepta WebP, rechaza RIFF que no es WebP, rechaza RIFF demasiado corto) y
  `ImagenWebpTest` (un WebP real embebido en Base64 pasa por `Imagenes.leer`, que es lo que hace la
  tubería).

Combinado con §2: los clientes JavaFX y WPF **ya no convierten al enviar** (el `.webp` viaja tal cual
y se convierte **solo para pintar**: `GestorAdjuntosChat.precalentarParaVer` en JavaFX,
`ChatWindow.PrepararParaVer` → `*_vista.png` en WPF, conservando siempre el original para "Guardar"), y
el servidor **acepta** WebP de punta a punta. Python sigue enviando `.webp` como archivo genérico: es
lo único que queda abierto de esta vía (`ESTADO_ROADMAP.md` §5.6 y `SEG-02`).

---

## 5. Deuda que este lote dejó (no se hizo, a propósito o por tiempo)

- **Progreso por bytes y cancelación en JavaFX; multiadjunto en Python.** JavaFX ya ofrece
  multiselección/arrastre y bandeja (hasta 5 adjuntos), mientras Python aún acepta un adjunto por
  operación; solo WPF tiene progreso ponderado por bytes y cancelación cooperativa.
- **`caption` en Python.** El esquema canónico y la copia de WPF la tienen; la de Python no, y por
  eso `test_copia_python_conserva_el_esquema_canonico` **falla** (227 casos, 1 fallo, 5 omitidos).
- **Sniffing de magic bytes** en los transcodificadores: la conversión a PNG decide por extensión.
- **Agrupado de refrescos de la lista de contactos en .NET** vive en `ChatWindow.xaml.cs`, que la
  suite no compila: la regla pura sí está testeada (`FormatoPreview`), la de "llega un mensaje y la
  lista se entera" no tiene red de seguridad.
- **Insignia de no leídos con fondo y esquinas** en .NET: solo se corrigió el texto (`"3"` sin
  paréntesis) y el color al brush del tema.
- **Subida duplicada de la misma imagen (bug del servidor).** `archivos.ruta` es única: dos subidas
  del mismo contenido con el mismo propietario la violan y **el mensaje se pierde entero**. Se
  corrigió el amplificador del lado cliente (reenviar sueltas ante cualquier excepción duplicaba el
  contenido; ahora solo se reenvía si el montaje ni siquiera se compone), pero **la causa sigue
  abierta**: ver `ESTADO_ROADMAP.md` §5.7.

---

## 6. Verificación acumulada del lote

Las cifras están en `ESTADO_ROADMAP.md` §1 con la fecha de cada ejecución. La última de este lote
(2026-10-10/11) dejó **JavaFX 281/281**, **.NET 337/337**, **Python 227 con 1 fallo conocido**
(esquema distribuible sin `caption`), `contrato/verificar_puertos.py` en verde y el build WPF Release
sin advertencias. La prueba manual en pantalla la hizo el usuario; lo que destapó está en §3.