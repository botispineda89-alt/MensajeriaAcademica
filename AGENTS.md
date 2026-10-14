# AGENTS.md — guía de orientación rápida

> Para cualquier agente de IA o persona nueva. Objetivo: entender cómo funciona el código en
> **5 minutos**. Si con esto no basta, el índice está en §8. El estado vigente y pendientes están en
> `ESTADO_ROADMAP.md`; este archivo no registra ejecuciones ni historial.

## 1. Qué es esto, en 4 líneas

Aplicación de escritorio **cliente/servidor** para mensajería académica. Un servidor central en
**Java** y **tres clientes intercambiables** en **JavaFX**, **PySide6** y **WPF**, que se hablan por
**TCP + JSON** (no HTTP).

Contrato de red en una línea: **cabecera de 4 bytes big-endian con la longitud, seguida de JSON
UTF-8** (`universidad-mensajeria/common-protocol/PROTOCOLO.md`, `TramaCodec`).

## 2. Mapa del repositorio en 30 segundos

```
<raíz>/
├── AGENTS.md, README.md, REQUERIMIENTOS.md (QUÉ, RF-*), PLAN.md (CÓMO), ESTADO_ROADMAP.md (qué falta)
├── ANALISIS_TECNICO.md (auditoría histórica) → `docs/archivo/ANALISIS_TECNICO-historico.md`, PLAN_PATRONES_UI.md, PLAN_PARIDAD_CLIENTES.md
├── GUIA_ACEPTACION.md, DESPLIEGUE.md, INSTALACION.md
├── contrato/          ← fuente ÚNICA de lo compartido: tokens JSON + generadores y verificadores
├── DIAGRAMAS/         ← fuentes/renders canónicos (ver DIAGRAMAS/README.md); incluye legados migrados
├── docs/archivo/      ← histórico, no normativo (diarios de lo ya ejecutado; ver su README.md)
└── universidad-mensajeria/ ← EL MONOREPO (solo aquí se compila; java.version=21)
    ├── pom.xml (padre, Spring Boot 3.5.7, Flyway 11.14.1), Dockerfile, docker-compose.yml, empaquetar-todo.cmd
    ├── herramientas/preparar-entorno-servidor.ps1  (instala/verifica JDK 21 + Maven 3.9.16 + MySQL sin admin; -SoloVerificar, -IniciarMySql)
    ├── common-protocol/ dto + codec (sin Spring, sin UI; TramaCodec + schema-local.sql canónico + test-fixtures/)
    ├── server/  12 módulos: 5 int-* (puertos, sin Spring) + 5 comp-* (adaptadores) + utilerias + servidor-app
    ├── client/  client-interfaces + client-app (JavaFX 22.0.2 + AtlantaFX 2.1.0)
    ├── client-python/ (PySide6 + qfluentwidgets, SQLite)
    ├── client-dotnet/ (WPF .NET 8 + WPF-UI, SQLite)
    └── client-dotnet.Tests/ (xUnit; OJO: apunta a net10.0, la app es net8.0-windows)
```

Reactor Maven: 3 agregadores (`common-protocol`, `server`, `client`) + 16 módulos con código.

**Dato clave:** `int-*` y `comp-*` **no usan Spring**. El único módulo con Spring es `servidor-app`,
que compila **solo contra los `int-*`** (los `comp-*` son `<scope>runtime</scope>`); los adaptadores
se descubren con `ServiceLoader` vía `META-INF/services`. Los 5 `Proveedor*` no aparecen en ningún
llamador compilado porque se cargan reflectivamente: `Fabrica.montar()` (servidor-app,
`inicio/Fabrica.java:112-122`) hace `ServiceLoader.load` de los 5 SPI al arrancar — verificado el
2026-10-11 en el jar empaquetado (los 10 jars `int-*`+`comp-*` viven en `BOOT-INF/lib`) y en los
logs (`u.m.protocolo.impl.ManejadorConexion` es la clase de `comp-protocolo-clienteservidor`
procesando cada trama). `DiagramaFidelidadTest` fija que hay exactamente UN proveedor por SPI.

## 3. Los 4 procesos y cómo se hablan

| Proceso | Entry point | UI / BD local |
|---|---|---|
| Servidor | `server/servidor-app/.../server/ServerApplication.java` | JLine + JavaFX 21.0.6 (admin) / MySQL 8.4 (Postgres 16 / Oracle opcionales) |
| Cliente JavaFX | `client/client-app/.../cliente/ClienteMain.java` | JavaFX 22.0.2 + AtlantaFX 2.1.0 (FXML+CSS) / H2 por cuenta |
| Cliente Python | `client-python/main.py` | PySide6 + PySide6-Fluent-Widgets / SQLite por cuenta |
| Cliente WPF | `client-dotnet/App.xaml.cs` | WPF .NET 8 + WPF-UI + SkiaSharp (imágenes) / SQLite por cuenta |

Decodificación de imágenes: **`imageio-webp` 3.12.0 (BSD-3)** en `server/utilerias` **y** en el cliente
JavaFX, y **`SkiaSharp` 4.153.1 (MIT)** en WPF. JavaFX y WPF **no reconvierten al enviar**: mandan el
`.webp` tal cual (el servidor lo acepta) y convierten **solo para pintar**. Python manda el `.webp`
como archivo genérico; es la única vía sin cerrar (`ESTADO_ROADMAP.md` §5.6, `SEG-02`).

Para lo que ni el JDK ni ese plugin abren (**AVIF, HEIC/HEIF, SVG**) el servidor llama a
**ImageMagick 7 vía `im4java`** (`DecodificadorMagick`, dentro de `Imagenes.leer`) y convierte a PNG
antes de la tubería. Es un requisito del **servidor**, no de los clientes: ver `INSTALACION.md` §1.1.

```
JavaFX ─┐
Python ─┼── TCP/JSON ──▶ IP_SERVIDOR:5000 (TLS: 5001) ──▶ Servidor ──▶ MySQL
WPF   ──┘                                                      ├── uploads/<nombre [código]>/<hash>/
                                                               └── logs/
```

Puertos TCP por defecto en los 4 `client.properties`/`server.properties`: **5000 plano, 5001 TLS**.
MySQL Docker: host **`localhost:3307`** → contenedor `mysql:3306` (los tests usan el host).

Cotas vivas en `server/servidor-app/src/main/resources/server.properties` (no las adivines):
`red.limites.max-conexiones=100`, `red.limites.max-conexiones-por-usuario=3`, `red.pool.max=100`,
`red.upload-dir=uploads`, `red.sesion.timeout-segundos=600`. La de 3 sesiones por usuario es la que
gobierna `KICK` y la presencia (invariante 8 de §5).

**Las dos reglas que más se violan por ignorancia:**

1. **`localhost` no sirve como dirección de servidor** en un cliente que está en otro PC.
2. **Los clientes NUNCA tocan la base del servidor.** Solo el servidor → MySQL.

## 4. Build y test: los comandos exactos

```powershell
Set-Location universidad-mensajeria
docker compose up -d mysql          # OBLIGATORIO: los tests de servidor fallan sin MySQL

mvn -pl server/servidor-app,client/client-app -am test    # servidor + JavaFX
mvn -pl client/client-app -am test                        # solo JavaFX
```

El `-am` (**also-make**) no es opcional: compila los módulos hermanos (`common-protocol`,
`client-interfaces`). Foco en un solo test:

```powershell
mvn -pl server/servidor-app -am test -Dtest=NombreTest -Dsurefire.failIfNoSpecifiedTests=false
mvn -pl client/client-app -am test -Dtest=NombreTest -Dsurefire.failIfNoSpecifiedTests=false
Set-Location client-python; .\.venv\Scripts\python.exe -m pytest tests/test_modulo.py -k filtro
dotnet test client-dotnet.Tests\Mensajeria.Tests.csproj --filter "FullyQualifiedName~Nombre"
```

```powershell
# Verificación global de paridad (tokens + estado + puertos + 3 suites + build WPF): exit 0/1
.\universidad-mensajeria\client-python\.venv\Scripts\python.exe contrato\verificar.py
dotnet build client-dotnet/Mensajeria.csproj -c Release   # valida el XAML generado
```

Notas que se adivinan mal:

- `client-python/pyproject.toml` existe por `uv run` y por el MCP `pyside6.launch_app` (que exige
  ese fichero). Las dependencias de ejecución se fijan en `requirements.txt` (`pillow`, `PySide6`,
  `PySide6-Fluent-Widgets`); **`pytest` solo está en el grupo `dev` del `pyproject`**, por eso
  `python -m unittest discover -s tests` (lo que corre `verificar.py`) funciona sin instalar nada.
- Arranca **siempre cada cliente desde su propia carpeta**: `client.properties` se resuelve
  relativo al directorio de trabajo (Python) o a la carpeta del ejecutable (.NET).
- Tests WPF requieren **SDK 10** aunque la app sea `net8.0-windows`. No hay CI workflows en
  `.github/`; la verificación es local (`contrato/verificar.py`).
- **No afirmes que las suites están en verde sin fecharlo.** Última ejecución real (2026-10-14,
  tercera tanda: checklist 5.21 + VIS-02): JavaFX **299/299** (50 clases, ArchUnit incluida),
  Python **307** (5 omitidas) y .NET **386/386** en verde; build WPF Release 0/0;
  `server/utilerias` 15/15 y `codec` 7/7 (2026-10-10).
  **`contrato/verificar.py` pasó 10/10 (exit 0)** y sirve como puerta de calidad (el paso 6 es
  nuevo: `verificar_textos_ui.py`, literales de ventana/diálogo una vez por cliente).
- `servidor-app` solo ejecutó `EntregaCasoPayloadGrandeTest` (4/4) el 2026-10-13; **la suite completa
  no se re-ejecutó**: su último registro fechado es el del 2026-10-08, 106 casos, **102 en verde y
  4 fallos preexistentes** (2 umbrales temporales de `CargaMixtaTest` + 2 de `MensajesTest`), ajenos
  a los cambios de cliente.
- **Smoke real (no dobles):** `smoke/smoke_adjunto_20mb.py` levanta dos sesiones TCP contra el
  servidor de verdad y compara SHA-256. Requiere MySQL arriba y el servidor escuchando en 5000; el
  servidor **no levanta el puerto solo**: su consola JLine espera `7 Iniciar`, así que hay que
  redirigirle la entrada (`(echo 7 & ping -t 127.0.0.1 >nul) | java -jar ...`).
- Los reportes Surefire viven en `target/`, que está en `.gitignore`: en un clon o worktree recién
  hecho **no existen**, así que hay que ejecutarlos o citar el roadmap fechado, no "los reportes
  presentes".

### Distribuciones (lo que se instala en cada PC)

```powershell
Set-Location universidad-mensajeria
.\empaquetar-todo.cmd   # las 4: servidor + JavaFX + Python + `dotnet publish ... -o dist\MensajeriaWPF`
```

Equivalentes por pieza: `dist\distribuir-servidor.cmd`, `client\dist-client.cmd`,
`client-python\distribuir.cmd`. Los 2 scripts de Java usan **`mvn install`, nunca `package`**:
`dependency:copy-dependencies` resuelve los módulos hermanos desde el repo local.

## 5. Invariantes — no romper esto

Rompes algo que funciona aunque las suites sigan en verde si tocas:

| # | Invariante | Por qué |
|---|---|---|
| 1 | **Protocolo neutro de lenguaje.** Serialización **Gson**, nunca nativa de Java | `RNF-04`; los 3 clientes reimplementan el framing |
| 2 | **Tope de trama: 16 MiB** (`TramaCodec.TAMANO_MAXIMO_TRAMA`). Archivos **fragmentados** (`ARCHIVO_INICIO` → N×`ARCHIVO_PARTE` → `ARCHIVO_FIN`) | Un adjunto grande no cabe en una trama |
| 3 | **`servidor-app` compila SOLO contra `int-*`.** Los `comp-*` son `runtime` | Romperlo rompe la inversión de dependencias |
| 4 | **Pool de hilos CONCRETOS con token bucket**, no `ExecutorService` (`ManejadorConexion extends Thread`) | Object Pool explícito (`RF-S43..S48`) |
| 5 | **Sin registro por red.** Alta = editar `usuarios_iniciales.csv`; el intento por red responde `ERROR` | `RF-S27` |
| 6 | **Filtros de imagen SECUENCIALES, cada uno genera y guarda una imagen nueva** (5 derivadas) | `RF-S16`, `RF-S17`; no es `if/else` |
| 7 | **Login incrementa "accesos"; presencia = sesión viva.** No son lo mismo | Mezclarlos falsea informes 2–4 |
| 8 | **`LOGOUT` cierra una sesión; `KICK` cierra las OTRAS del mismo código**; presencia off al cerrar la última | `RF-S31`; lo prueba `SMOKE-03` |
| 9 | **Respuesta = TEXTO ORDINARIO** con prefijo `Respuesta a Nombre [CÓDIGO]: "…"` | Sin vínculo estructurado; cambiarlo rompe interoperabilidad |
| 10 | **BD local aislada por cuenta, sin FKs.** `historial_local.origen/destino` son `TEXT` con el código | El esquema local **no tiene claves foráneas** |
| 11 | **Paleta generada, no editada.** 4 CSS + 2 `.xaml` + `ui/tema_tokens.py` salen de `contrato/generar_tokens.py` | Se edita `contrato/tokens-diseno.json` y se regenera (`--check` en CI local) |
| 12 | **Estados en `contrato/estado-mensaje.json`; comprobar, no asumir.** `PENDIENTE→ENVIADO→ENTREGADO→LEIDO` + `ERROR` terminal, en 5 copias / 3 lenguajes / 2 motores | Corre `contrato/verificar_estado_mensaje.py` si tocas una copia |
| 13 | **Puertos en `contrato/puertos.json`: 23, no 12** (hoy 202 métodos). 9 viven en `client-app`/`presentacion` | Añadir un método sin añadirlo al JSON rompe `contrato/verificar_puertos.py` |
| 14 | **Los datos de contrato tienen UNA sola copia y los tests de los 3 la enlazan**: `schema-local.sql` (canónico en `common-protocol/dto/src/main/resources/`), `contrato/framing.json`, `common-protocol/test-fixtures/client-properties-fixture.properties` | Una copia a mano hace que el store de un cliente ejecute un esquema que el otro no, y nada falla hasta producción. **Hoy Python viola esto**: su copia no tiene `caption` y su propio test lo detecta (`ESTADO_ROADMAP.md` §5.8) |
| 15 | **`fecha_envio` es TEXT y se ordena COMO TEXTO.** Un cliente usa **un solo formato**, no dos | Python y .NET escriben **UTC con `Z`** y normalizan las filas viejas al arrancar (Python: `transversal/fechas.a_utc` + migración `store/database.py::_normalizar_fechas_utc`; .NET: `FormatoFecha.AUtc` + `LocalStore.NormalizarFechasAUtc`); JavaFX sigue con hora local ISO sin zona porque su H2 es por cliente y no comparte archivo. La mezcla Python-local/.NET-Z sacó lo propio recién enviado de la página 0 del historial (roadmap §5.10, 2026-10-11) |
| 16 | **El pie de un adjunto viaja en el `contenido` de la MISMA trama** (`MENSAJE_IMAGEN` / `ARCHIVO_INICIO`) y se guarda en la columna `caption` | Un pie en un segundo mensaje es otra burbuja, y el texto se perdía al enviar una sola imagen sin montaje |
| 17 | **Formatos de imagen: PNG/JPEG/GIF/BMP, WebP, AVIF, HEIC/HEIF y SVG.** WebP por su firma en el offset 8 (no cualquier `RIFF`), AVIF/HEIC por `ftyp` + marca (no cualquier `ftyp`), SVG por el primer elemento real del documento. AVIF/HEIC/SVG los decodifica ImageMagick, no el JDK. Los 3 clientes los clasifican como imagen y los mandan como `MENSAJE_IMAGEN`; **no reconvierten al enviar** y para pintar llevan el original a PNG (Python: Pillow + QtSvg; .NET: Magick.NET; Java: jsvg + ImageMagick opcional vía im4java) | Un `.wav`/`.avi` también empiezan por `RIFF` y un `.mp4` por `ftyp`; y renombrar el archivo al enviar hacía que quien lo recibiera recibiera otro |
| 18 | **El icono por tipo de archivo es el MISMO dibujo en los 3 clientes** (`iconos/{pdf,xlsx,csv,json,docs}.png` en los recursos de JavaFX, `ui/iconos/` de Python y `Iconos/` de WPF; el catálogo extensión→icono vive en `IconoArchivo.java`, `transversal/iconos_archivo.py` y `Transversal/IconosArchivo.cs`) y el MISMO tamaño: 48 DIP en burbuja y bandeja, estilo tarjeta de archivo de Telegram Web. Cuando el tipo tiene icono propio, el `📎` genérico del texto **se quita** (doble icono); solo queda para extensiones sin catálogo | Cada distribuidor es independiente y ninguno lee los recursos del otro: cambiar un PNG o una extensión en un solo cliente deja el mismo PDF distinto según quién lo mire, y ninguna suite se entera. `contrato/verificar_iconos.py` compara las 3 copias byte a byte y los 3 catálogos. Trampa WPF: esos lados los consume el XAML con `x:Static` sobre `Width`/`Height`; declarados `int`, WPF no convierte y revienta con `InvalidCastException (Int32→Double)` en `MinMax..ctor` — van como `double` (IconosArchivo.cs) |
| 19 | **La entrega de un adjunto NO lleva los bytes dentro de la trama.** El servidor manda el `MENSAJE_ARCHIVO` con los bytes en base64 y, si no cabe en 16 MiB (todo lo de más de ~12 MiB), reintenta con la **versión ligera**: mismo mensaje sin `contenidoImagen`, conservando `archivoId`, `nombreArchivo`, `tamanoArchivo`, `hashSha256` y el pie. El receptor baja con `DESCARGAR_ARCHIVO` | Sin el reintento, el codec rechaza la trama y el servidor **expulsa la sesión del destinatario**: un archivo de 20 MiB cerraba la sesión de quien lo recibía. Un cliente no puede asumir que los bytes vienen en línea (`PROTOCOLO.md` §5, roadmap §5.15) |
| 20 | **Toda descarga tiene que saber reensamblar.** El servidor responde con una trama única si el archivo cabe y con `ARCHIVO_INICIO` + `ARCHIVO_PARTE`\* + `ARCHIVO_FIN` si pasa de 1 MiB; la primera trama completa la espera de la petición y las partes llegan **fuera** de ella. Cada cliente necesita su colector: `ColectorFragmentos` (.NET), el recolector de `GestorAdjuntosChat` (JavaFX) y `fachada/servicios/colector_fragmentos.py` (Python) | Sin colector se escribe un archivo de **0 bytes sin ningún error**: la peor forma de fallar, porque el usuario cree que tiene el archivo (roadmap §5.15) |
| 21 | **La costura de inyección de Python es el símbolo del módulo, no el parámetro.** `test_ui.py` sustituye `ui.chat_window.PreferenciasChat` con unas preferencias de `QSettings` temporal; por eso ese import sigue aunque la ventana ya no construya nada con él, y `construir_contexto_chat` acepta una `preferencias` ya construida | Quitar el import «por limpieza» deja la ventana sin forma de probarse con dobles y hace que el «último chat guardado» de una prueba se filtre a la siguiente: falla sin error de código, solo con datos cruzados (roadmap §5.19) |
| 22 | **El estado de conversación tiene un solo dueño en cada cliente:** `ui/chat_view_model.py` (Python, `QObject`), `ViewModels/ChatViewModel.cs` (.NET, `INotifyPropertyChanged`) y el `ChatViewModel` de JavaFX. Las ventanas lo exponen como **accesos**, no como copias | Una copia puede quedar desincronizada y el síntoma es un botón de enviar que se habilita sin destinatario, o un offset de la conversación anterior aplicado a la nueva. Lo bloquean `test_arquitectura.py` y `ArquitecturaTests.ElEstadoDeConversacionNoViveEnLaVentana` |

## 6. Trampas verificadas en el código

### 6.1 Parece muerto y NO lo es — no borrar sin comprobar framework/reflexión

`LoginController` (0 refs Java; lo instancia `login.fxml` con `fx:controller=`), `ServerApplication` /
`PasswordConfig` / `FlywayMigrationAdapter` / `JpaPersistenciaAdapter` / `DataSourceAdapter`
(anotaciones Spring), los 5 `Proveedor*` (ServiceLoader), clases de estilo JavaFX en CSS
(`.split-pane`, `.list-cell`, `.combo-box`), `FabricaDelDespachador.NULA` (contrato SPI),
`FiltroMain` (CLI usada por su test). Reglas B1–B6 en `PLAN_PATRONES_UI.md` §2.1.

### 6.2 Lo que SÍ es código muerto

Foto con conteo en `docs/archivo/ANALISIS_TECNICO-historico.md` §2 (38 items al 2026-10-05; parte ya borrada):
`sesionesInfo()`, `solicitarHistorialServidor()`, columna `ipDestinatario` (siempre NULL),
`_guardar_propio()`, pinceles `Telegram*Brush`. **Verifica que el símbolo aún exista** antes de
actuar.

### 6.3 `SEC-05`/`SEC-06` están cerrados

Autenticación obligatoria por defecto; solo `LOGIN` público. Detalle vigente en
`ESTADO_ROADMAP.md` §9; la auditoría conserva el historial.

### 6.4 Los 3 clientes no son espejos exactos — replicar la lógica en los 3

JavaFX tiene ViewModel parcial y frontera por constructor (`FabricaControladores`); Python **parchea**
la fachada con `setattr` y no tiene interfaz de historial; .NET inyecta `IFachadaCliente`; solo
Python valida TLS fail-fast (`transversal/tls.py`). Python partió su chat en componentes
(`constructor_vista_chat`, `paginador_historial_chat`, …); .NET concentra el chat en
`ChatWindow.xaml.cs`. Si tocas negocio, **replícala en los 3** o rompes la matriz de 6 direcciones
(`GUIA_ACEPTACION.md`).

**Donde hoy SÍ divergen (deuda conocida, no la reintroduzcas):**

| Capacidad | JavaFX | Python | .NET |
|---|---|---|---|
| Varias imágenes en una burbuja (montaje) | ✅ `PlanEnvioAdjuntos` + `MontajeImagenes` | ⬜ | ✅ `PlanEnvioAdjuntos` + `MontajeImagenes` (Skia) |
| Texto como pie del adjunto en la misma burbuja (`caption`) | ✅ | ⬜ ni columna en su `schema-local.sql` | ✅ |
| Selección/arrastre **múltiple** y bandeja de adjuntos | ✅ (`showOpenMultipleDialog`, tope 5) | ⬜ un adjunto por operación | ✅ (tope 12 por lote + `adjuntos.lote-max-mb`) |
| Progreso por bytes y **cancelar** el envío en curso | ⬜ | ⬜ | ✅ `ProgresoEnvio` + `Cancelar envío` |
| Botón de guardar en imágenes | ✅ `MensajeCell` | ⬜ | ✅ `BotonGuardar` |
| Aviso inline con cierre automático | ✅ `EstadoAvisoInline` | ⬜ | ✅ `EstadoAvisoInline` |
| `.webp` como imagen | ✅ (se manda tal cual) | ⬜ como archivo | ✅ (se manda tal cual) |
| `Shift+Enter` = salto de línea | ✅ `TecladoCompositor` | ✅ | ✅ |

### 6.5 Detalles que rompen suposiciones

- Estados `PENDIENTE→ENVIADO→ENTREGADO→LEIDO` + `ERROR` como `CASE` SQL **idéntico** en los 3
  clientes; canónico en `DIAGRAMAS/estados/st-mensaje.mmd`.
- Python y .NET resuelven la BD local con la **misma convención de nombre**
  (`mensajeria_cliente_<CUENTA>.db`, un archivo por cuenta desde un archivo legado), así que dos
  instancias del mismo usuario en el mismo PC **comparten el SQLite**. El servidor nunca envía zona
  (`LocalDateTime.now()`), así que **cada cliente escribe un único formato** y lo normaliza al leer:
  **Python y .NET guardan UTC con `Z`** (Python: `transversal/fechas.a_utc` y la migración de una
  pasada `store/database.py::_normalizar_fechas_utc`; .NET: `FormatoFecha.AUtc` +
  `LocalStore.NormalizarFechasAUtc`); **JavaFX usa hora local ISO sin zona** porque su H2 es por
  cliente y no comparte archivo. Leer/escribir ambos formatos sí es obligatorio (invariante 15);
  **escribir los dos a la vez, no**: con la SQLite compartida, la mezcla local/Z dejó fuera de la
  página 0 los mensajes propios recién enviados (roadmap §5.10, 2026-10-11).
- **El preview de la lista de contactos** lo decide `origen = yo`, no la columna `enviado`: el
  sincronizador de `SYNC_LOGIN` deja filas con `enviado=1` que en realidad son ajenas, y eso pintaba
  "Tú: " en mensajes recibidos. Vacío → "Sin mensajes aún" en los 3.
- Tests E2E usan **semilla propia** (`src/test/resources/usuarios_iniciales.csv`) y **dejan
  `activo=0`** en esa BD; el arranque normal reactiva. Si el login dice "usuario no registrado",
  reactiva con `UPDATE usuarios SET activo=1`.
- `Usuarios` = `ConcurrentHashMap` **en memoria**; la tabla `usuarios` la persiste `servidor-app`.
- `Servicios` trata límite `0` como **sin límite**; el pool usa token bucket, no semáforo.
- `ProtocoloComunicacion.enviar` declara `throws IOException` frente a `throws Exception` de la
  interfaz: *narrowing* legítimo.
- **Un `javafx.concurrent.Task` solo se ejecuta si el executor lo despacha al hilo de JavaFX**
  (`executor = Platform::runLater`). Con un executor directo (`Runnable::run`) `call()` no llega a
  invocarse y la operación no ocurre **sin que salte ningún error**: los tests de
  `EnviarMensajeComando` vacían la cola de JavaFX antes de afirmar.
- **Al repartir un code-behind en parciales hay que revisar los `.csproj` que lo enumeran uno a uno.**
  `client-dotnet.Tests.csproj` nombraba `Fachada\FachadaCliente.cs`; al dividirlo en parciales
  compilaba el núcleo sin ellos y los tests veían una fachada que no implementaba la interfaz, sin
  avisar de nada raro. Ahora usa `Fachada\FachadaCliente*.cs`.
- **Un lector de pantalla no vuelve a leer el mismo texto.** Por eso el aviso inline limpia el texto
  accesible y lo vuelve a poner en el siguiente pulso (`AvisosInlineChat` en JavaFX,
  `_anunciar` en Python): si no, el mismo error saltando dos veces seguidas se queda mudo.

## 7. Cómo trabajar aquí

| Si tu tarea es… | Empieza por | Antes de editar, lee |
|---|---|---|
| Función de negocio | `REQUERIMIENTOS.md` (el `RF-*`) | `universidad-mensajeria/common-protocol/PROTOCOLO.md` si toca red |
| Servidor | `PLAN.md` §2 y §9 | `DIAGRAMAS/clases/arq-vista-general.mmd`; regla 3 de §5 |
| Trama TCP | `universidad-mensajeria/common-protocol/PROTOCOLO.md` + `commands/Comandos.java` | Nada de `comp-*` en `servidor-app` |
| Un cliente | `DIAGRAMAS/clases/clases-client-{java,dotnet,python}.mmd` | §6.4: **replicar en los 3** |
| UI | `DIAGRAMAS/textos-ui.md` + mockups | `PLAN_PATRONES_UI.md` antes de añadir |
| Arquitectura JavaFX | `docs/archivo/PLAN_ARQUITECTURA_CLIENTE_JAVA-historico.md` (fases 1–4 verificadas 2026-10-11; **archivado**; falta su smoke visual formal — no declarar Clean completa) | `ESTADO_ROADMAP.md` §6 |
| Color/medida UI | `contrato/tokens-diseno.json` y regenerar | **Nunca editar CSS/`.xaml` generados** (`contrato/README.md`) |
| Igualar clientes | `PLAN_PARIDAD_CLIENTES.md` | `contrato/verificar.py` (9 pasos; verde el 2026-10-11 — la corrección de §5.8 ya está aplicada) |
| Esquema servidor | `server/servidor-app/src/main/resources/db/migration/{mysql,postgres,oracle}/` (**los 3**) | `DIAGRAMAS/modelo-relacional/er-modelo-servidor.mmd` |
| Filtro imagen | `server/utilerias/` + `TuberiaFactory` | Regla 6 de §5; formatos en invariante 17 |
| Saber **qué se decidió y por qué** | `docs/archivo/PLAN_CLIENTES-2026-10-historico.md` y `docs/archivo/PLAN_MEJORAS-historico.md` | Son diarios, no normativa: el estado vigente manda en el roadmap |
| Documentar | `DIAGRAMAS/README.md` (regla de 3 pasos) | Un `.mmd` sin PNG **no cuenta** |

## 8. Convenciones e índice

- **Dominio en español** (nada de `manager`, `util2`); javadoc en español; **Python** `snake_case` +
  hints públicos; **C#** `PascalCase` público / `camelCase` privado.
- **Comentarios solo en contratos públicos y en el *por qué*.** Sin hardcode: `localhost:5000`,
  credenciales, rutas → `application.properties` / `server.properties` / `client.properties`.
- **Literales UI en código**, sin recursos; si añades uno, igual en los 3 clientes. Excepciones de
  dominio (`UsuarioNoEncontradoException`), no `RuntimeException` genérica.

| Pregunta | Archivo |
|---|---|
| Qué debe hacer / por qué así / qué falta | `REQUERIMIENTOS.md` / `PLAN.md` / `ESTADO_ROADMAP.md` |
| Trama exacta / esquema local | `universidad-mensajeria/common-protocol/PROTOCOLO.md` / `universidad-mensajeria/common-protocol/HISTORIAL_LOCAL.md` |
| Dónde está cada clase / textos UI | `DIAGRAMAS/` (`README.md` es el índice) / `DIAGRAMAS/textos-ui.md` |
| Paridad / paleta / ¿siguen iguales? | `PLAN_PARIDAD_CLIENTES.md` / `contrato/tokens-diseno.json` / `contrato/verificar.py` |
| Problemas ya encontrados | `docs/archivo/ANALISIS_TECNICO-historico.md` (histórico; el estado vigente manda en el roadmap) |
| Preparar PC / operar / comprobar | `INSTALACION.md` (+`AUTOMATIZACION_ENTORNO.md`) / `DESPLIEGUE.md` / `GUIA_ACEPTACION.md` |
| Servidor / JavaFX / Python / WPF | `server/servidor-app/README.md` / `client/README.md` / `client-python/README.md` / `client-dotnet/README.md` |
