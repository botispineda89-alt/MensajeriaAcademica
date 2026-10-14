# Estado y roadmap

Actualizado: **2026-10-11** (dos bugs del smoke Python cerrados, ver §5.10; el 2026-10-10: cierre del lote de clientes JavaFX/WPF, consolidación documental y
verificación fresca de las tres suites; ver §1 y §5.4–§5.7). Este es el tablero de estado; la arquitectura está en
[`PLAN.md`](PLAN.md), el plan de refactor de clientes en
[`PLAN_PATRONES_UI.md`](PLAN_PATRONES_UI.md), la evidencia de la auditoría en
[`docs/archivo/ANALISIS_TECNICO-historico.md`](docs/archivo/ANALISIS_TECNICO-historico.md), los requisitos en
[`REQUERIMIENTOS.md`](REQUERIMIENTOS.md) y las instrucciones para instalar cada proceso en
[`DESPLIEGUE.md`](DESPLIEGUE.md).

Regla de tableros: **este archivo manda hacia adelante** (qué falta);
[`docs/archivo/PLAN_MEJORAS-historico.md`](docs/archivo/PLAN_MEJORAS-historico.md) y
[`docs/archivo/PLAN_CLIENTES-2026-10-historico.md`](docs/archivo/PLAN_CLIENTES-2026-10-historico.md)
son el **diario de lo ya ejecutado** (qué se hizo y cómo se verificó). Los dos planes que dirigieron
ese trabajo —`PLAN3.MD` y `PLAN_AMBOS.md`— se retiraron de la raíz el 2026-10-10 al quedar
ejecutados: su contenido único quedó en el diario y no se vuelven a abrir.

## Cómo leer este archivo

| Estado | Significa |
|---|---|
| ✅ Hecho | Implementado + suite automatizada en verde |
| 🟡 Por verificar | Código hecho, falta smoke manual / registro en matriz |
| ⬜ Por hacer | No iniciado, con alcance y criterio de aceptación definidos abajo |
| 🟠 Propuesto | Requiere decisión de producto/protocolo antes de implementarse |
| 🔴 Riesgo | Conocido, documentado, pendiente de decisión antes de producción |

> **Resumen en 30 segundos (corte 2026-10-11).** `SEC-05`/`SEC-06`, los lotes `MUERTE-1..5`,
> los defectos `UIX-00a..00f`, varios lotes P1/P2 de UI y el aviso inline .NET `UIX-15` están
> implementados. La vista administrativa del servidor tiene ficha de usuario y JLine conserva sus
> comandos con mejor jerarquía visual. El lote de octubre en los clientes está cerrado:
> envío múltiple de adjuntos (WPF) y lotes con vista previa (Python), los seis fallos de
> JavaFX/.NET con sus rondas de depuración en pantalla, y los formatos amplios AVIF/HEIC/SVG +
> WebP soportados de punta a punta (decodificación vía ImageMagick en el servidor y visores en
> los 3 clientes). Los tres bugs del smoke de Python (historial, fondo, bucle de reconexión) y el
> crash de .NET por `x:Static` están cerrados (§5.10–§5.13). `DIAG-01..08` y `DIAG-10` tienen
> fuente/render y herramienta disponibles según el inventory de `DIAGRAMAS/README.md`; `DIAG-09`
> sigue pendiente. `contrato/verificar.py` pasa **9/9 pasos** (exit 0, 2026-10-11). Siguen
> pendientes smokes manuales, parte de `MUERTE-6`, `PAT-01` (Python/.NET), `PAT-VM`/`PAT-CMD`,
> `PAT-04`/`PAT-05`, temas `VIS-*`, el **bug del servidor de subida duplicada** (§5.7) y las
> tareas consolidadas de `PLAN_PARIDAD_CLIENTES.md` §7 (incluido el test de framing de Java
> data-driven, §5.14). En `PAT-ARQ-JAVA`, las fases 1–4 del plan están verificadas y el smoke
> visual conectado sigue pendiente.

## Índice de familias

| Familia | Qué agrupa | Cantidad | Bloquea producción |
|---|---|---|---|
| `SMOKE-*` | Aceptación manual pendiente | 9 | **Sí** |
| `SEC-*` | Seguridad | 6 | ~~`SEC-05`~~ **cerrado** |
| `MUERTE-*` | Limpieza de código muerto | 6 lotes | No |
| `UIX-*` | Bugs y mejoras de interfaz | 29 | Los 6 P0, sí |
| `PAT-*` | Patrones de diseño en clientes | 8 | No |
| `VIS-*` | Contrato visual compartido | 2 | No |
| `SEG-*` / `DIST-*` / `PERF-*` / `PROD-*` | Fases previas | 15 | — |
| `DIAG-*` | Diagramas | 10 | No |
| `DOC-*` | Documentación | 4 | No |

---

## 1. Estado actual por componente

| Componente | Estado registrado | Evidencia disponible al 2026-10-13 |
|---|---|---|
| `servidor-app` | ✅ **Ejecutado 2026-10-13** (solo `EntregaCasoPayloadGrandeTest` 4/4; suite completa sigue con el registro del 2026-10-08) | Surefire del 2026-10-08: 106 casos, 102 en verde; los 4 fallos son ajenos a los cambios de cliente (2 umbrales temporales de `CargaMixtaTest`, 2 de `MensajesTest`). La suite completa **no** se re-ejecutó en el lote del 2026-10-13; lo que sí pasó es el test nuevo de la entrega ligera (§5.15). |
| `contrato/verificar.py` | ✅ **Ejecutado 2026-10-13** | **9/9 pasos OK, exit 0**: tokens (--check), estado-mensaje, puertos, suite de contrato, iconos, suite JavaFX, suite Python, build WPF y suite .NET. Es la puerta de calidad vigente. |
| Cliente JavaFX | ✅ **Ejecutado 2026-10-13** | `mvn -pl client/client-app -am test`: **299/299** en **50 clases**, 0 fallos (paso 6 de `verificar.py`). +19 por la barra de progreso con cancelación (§5.16), los 9 colaboradores nuevos de `ChatController`/`ChatComponentes` y la regla de tamaño. |
| `server/utilerias` | ✅ **Ejecutado 2026-10-10** | Surefire: **15/15**, 0 fallos, incluidos `ImagenMagicaTest` y `ImagenWebpTest` tras el soporte WebP (§5.5). |
| `common-protocol/codec` | ✅ **Ejecutado 2026-10-10** | Surefire: **7/7**, 0 fallos. |
| Adaptadores del servidor (`comp-*`) | ✅ Último registro fechado | Surefire del 2026-10-08: 48 casos sin fallos. No re-ejecutados hoy. |
| Python | ✅ **Ejecutado 2026-10-13** | `python -m unittest discover -s tests`: **294 pruebas, 0 fallos, 5 omitidas** (paso 7 de `verificar.py`). +13 por la severidad y el anuncio del aviso inline (§5.17) y el colector de descarga fragmentada (§5.15). |
| .NET tests | ✅ **Ejecutado 2026-10-13** | `dotnet test client-dotnet.Tests/Mensajeria.Tests.csproj`: **368/368**, 0 fallos, 0 omitidos (paso 9 de `verificar.py`). +2 reglas de arquitectura: tamaño de los parciales de la vista y su reparto por responsabilidad (§5.18). |
| WPF build | ✅ **Ejecutado 2026-10-13** | `dotnet build client-dotnet/Mensajeria.csproj -c Release`: **0 errores y 0 advertencias** (paso 8 de `verificar.py`). |
| `contrato/verificar_puertos.py` | ✅ **Ejecutado 2026-10-13** | OK (exit 0, paso 3 de `verificar.py`): 23 puertos, **202 métodos**, 28 valores de `TipoMensajeCliente`, 4 textos de `EstadoConexion`. |
| Smoke real de 20 MiB | ✅ **Ejecutado 2026-10-13** | `smoke/smoke_adjunto_20mb.py` contra el servidor real: **A→B y B→A cuadran byte a byte (SHA-256)**. Encontró y documentó tres defectos (§5.15). |
| WPF / cliente Java publicados, contenedor y carga | ✅ Verificación histórica | Las pruebas de distribución, arranque, contenedor y carga corresponden a los hitos fechados más abajo; revisar su fecha/entorno antes de repetir en otro PC. |
| `server/servidor-app` | 🔴 **Bug abierto: subida duplicada** | Ver §5.7. Mandar dos veces la misma imagen reventa el `insert` (`archivos.ruta` única) y **se pierde el mensaje**. Es del servidor y sigue abierto. |

> **Interpretación de los reportes disponibles:** `CargaMixtaTest` tiene dos fallos de umbral
> temporal; sus reportes registran tramas procesadas/encoladas y ACK p95 sobre el límite. Los
> resultados históricos de rendimiento de `PERF-01/02` fueron tomados en otro entorno y no
> sustituyen esta medición. No afirmar que los umbrales están verdes; volver a medir con entorno
> controlado antes de una decisión de rendimiento.

### 1.1 Verificación manual registrada el 2026-10-05 (con la app real)

No es `SMOKE`, pero es la evidencia de que `UIX-00a` y `UIX-00f` funcionan y no solo compilan:

1. MySQL levantado, `servidor-app` arrancado (puerto 5000), cliente .NET **publicado** lanzado.
2. Login con una cuenta de `usuarios_iniciales.csv` → ventana de chat con el directorio cargado.
3. `Stop-Process` del servidor → el **banner ámbar** aparece con el texto de `EstadoConexion` y el
   `txtEstado` de la cabecera coincide. Antes de `PAT-03` no aparecía nada.
4. Al volver a levantar el servidor, `Actualizar` recupera el directorio sin que la app se caiga.
5. Login con el servidor **caído**: "Conectando…" aparece en la etiqueta **gris**, no en la caja
   roja, y el error real sale en rojo aparte.

> Ojo al reproducir: los tests E2E **dejan `activo=0`** en las cuentas de
> `src/test/resources/usuarios_iniciales.csv` de la BD de desarrollo. Si el login dice "usuario no
> registrado", ese es el motivo; se reactiva con un `UPDATE usuarios SET activo=1` y reiniciando
> el servidor (la plantilla de usuarios vive en memoria). Las cuentas de producción
> (`160004726` / `12345`) no se ven afectadas.

> Nota de entorno registrada: JDK 25.0.1 y Maven 3.9.11 están disponibles en el entorno de
> herramientas del repo. `mmdc` y Node se habilitaron el 2026-10-06; los renders vigentes y las
> carpetas legadas se detallan en `DIAGRAMAS/README.md`. `plantuml` no tiene fuentes `.puml` que
> renderizar en este proyecto.

Versiones configuradas: Java de compilación 21 (probado con JDK 25.0.1), JavaFX 21.0.6 en el
servidor y JavaFX 22.0.2 + AtlantaFX 2.1.0 en el cliente Java, Flyway 11.14.1.
El launcher del cliente habilita `--enable-native-access=javafx.graphics`. Los tests E2E usan semilla propia
(`src/test/resources/usuarios_iniciales.csv`): no dependen del CSV de producción.

### Cerrado recientemente (no reabrir sin motivo)

- **Fase 17 ✅:** tres pistas con toolkits propios y contrato TCP compartido.
- **Fase 18 ✅:** textos en `uploads/<prop>/textos/<hash>/`, genéricos por propietario, auto-refresh
  de informes, etiquetas `[INFO]/[WARN]/[ERROR]`, auditoría `LIMITE_ALCANZADO`.
- **PLAN_MEJORAS 1–9 ✅:** cita en tarjeta, solo-adjunto, descarga con filtro, crash shiboken,
  descarga .NET, login solo-lectura, confirmación de cierre, broadcast con pool acotado,
  maestro-detalle en Informes/Usuarios.
- **Ronda 2026-10-05 ✅:** duplicados Python al re-entrar, descarga Python, cita Java, NRE .NET,
  manifest JVM, CSV sombra de tests.
- **UI-01 a UI-10 + LOGIN-01/02 ✅:** revamp Telegram en los 3, modo oscuro/claro, botón `＋`
  único, doble-click responde, limpieza del login, burbuja roja unificada.
- **Tooling de UI ✅ instalado:** ver §6. No es backlog, es base ya disponible.
- **Bug de concurrencia en difusión ✅:** varias tareas de `poolFiltros` hacían `Files.move` sobre
  la misma ruta y en Windows se perdían copias. Corregido con candado por ruta en
  `AlmacenarInformacion`, cubierto por `difusionDelMismoTextoEnParaleloNoPierdeCopias`.
- **Vista administrativa del servidor ✅ (implementación 2026-10-06 + Sesión B 2026-10-07):** mejoras a Estado,
  Conectados, ficha de Usuario con páginas de 50, Mensajes con IP/contadores, detalle condicional
  de Informes, expansión de Logs, secciones de Pool, etiqueta de Difundir y confirmación de cierre
  que deja vivos TCP/JLine. Sesión B: Conectados con `Nombre Apellidos [código]` y sync
  lista↔combo, confirmación Sí/No al desconectar, Usuarios centrado, ficha Mensajes con
  paridad (filtro local, detalle, divisor, selección por id) e Informes manual sin
  auto-refresh (conserva fila). JLine reorganiza jerarquía visual. La revisión documental no ejecutó el
  smoke visual completo: `SMOKE-09` lo registra como pendiente.
- **Cierre de parciales 2026-10-08 ✅:** RF-S36 (bloque multilínea en consola con rutas de
  derivadas en IMAGEN, `BloqueMensajeTest` 4/4) y RF-S53 (IP viva del destinatario en tabla
  y detalle, con `No disponible` si offline). Las 5 lecturas de `casosdeuso/` pasan por E10
  (`InterfazMensajes` como pass-through, 0 flechas y 0 métodos netos nuevos) con candado
  `casosNuncaTocanAlmacen` en `DiagramaFidelidadTest` (7/7). Suite `servidor-app`: 106 casos,
  102 en verde; los 4 fallos son preexistentes y ajenos (2 umbrales `CargaMixtaTest`, 2
  `MensajesTest` verificados sin estos cambios).
- **Deuda SOLID/Clean Code saldada 2026-10-08 ✅:** `EntregaCaso` (entrega, presencia, acuses)
  y `DifusionCaso` (abanico admin/usuario) extraídos verbatim de `FachadaDeServicios`
  (901→650 líneas); despacho de cola por tabla `EnumMap` en vez de `switch`; pool de entrega
  creado por `Fabrica` e inyectado; `DetalleMensaje` extraído a clase propia
  (VistaEscritorio 1699→1418); fallback 50 MB unificado en
  `LimitesDTO.TAMANO_MAXIMO_POR_DEFECTO`. Diagrama intacto (0 flechas, 0 métodos netos
  nuevos) y suite en verde salvo los 4 preexistentes.

---

<a id="seguridad-bloqueante"></a>

## 2. Hallazgo histórico de seguridad — cerrado (`SEC-05`/`SEC-06`)

### Hallazgo original: `LISTAR_USUARIOS` respondía sin autenticación · **P0 cerrado**

> Este apartado documenta el estado encontrado en la auditoría, antes del arreglo. **No describe
> el comportamiento actual.** La implementación, pruebas y fecha de cierre se anotan al final de
> esta sección y en §9.

**El hallazgo.** `LISTAR_USUARIOS` es uno de los 3 comandos que **no aplican la guarda de
autenticación**. Los otros 12 sí lo hacen (`Comandos.exigirAutenticacion`). El despachador no pone
puerta global, así que la responsabilidad es de cada comando — y basta con que uno se olvide.

**Consecuencia.** Cualquiera que abra un socket TCP contra `5000` y envíe una trama
`LISTAR_USUARIOS` — **sin código ni contraseña** — recibe el directorio completo de la comunidad
académica: código, nombres, apellidos y programa.

**Cadena verificada extremo a extremo:**

| Eslabón | Archivo:línea |
|---|---|
| El despachador no filtra por sesión | `server/comp-protocolo-clienteservidor/.../impl/Despachador.java:55-62` |
| La guarda existe y funciona | `.../commands/Comandos.java:14-20` |
| 12 de 15 comandos la llaman | `.../commands/TextoCommand.java:23` y los otros 11 |
| **`ListarUsuariosCommand` no la llama** y ni siquiera guarda `respuestas` | `.../commands/ListarUsuariosCommand.java:14-20` |
| El caso de uso tampoco valida | `server/servidor-app/.../casosdeuso/ListarCaso.java:45-62` |

**Alcance.** `LISTAR_CONECTADOS` y `LOGOUT` comparten el hueco (menor gravedad: `LOGOUT` solo
cierra la sesión anónima del atacante, `LISTAR_CONECTADOS` solo revela quién está en línea). Lo que
se cierra es **el patrón**, no los 3 síntomas.

**Por qué ningún test lo detecta.** Las 3 clases reciben `respuestas` en el constructor y
`FabricaDelDespachador` lo pasa, así que el cableado *parece* completo. Un test que solo compruebe
"responde bien a un usuario autenticado" pasa igual.

- [x] **`SEC-05` — Cerrar el hueco.** ✅ Hecho 2026-10-05.
  1. `Comandos.exigirAutenticacion(...)` añadida a `ListarUsuariosCommand`, `ListarCommand` y
     `LogoutCommand` (era lo único que faltaba: los otros 12 ya la tenían).
  2. **Fallo cerrado en el SPI:** `CommandHandler.requiereAutenticacion()` vale **`true` por
     defecto** y solo `LoginCommand` lo sobreescribe a `false`. Un comando nuevo nace protegido:
     si olvida la guarda, el test lo delata porque su tipo sigue marcando `true`.
  3. **Test parametrizado** `sesionAnonimaNoEntraEnNingunComandoProtegido`: recorre los **14**
     tipos que el despachador tiene registrados y exige `ERROR "autenticate primero con LOGIN"`
     **y ninguna llamada de negocio**. Los datos salen de `despachador.comandos()`, no de una
     lista escrita a mano, así que un comando nuevo entra solo en el test.
  4. `soloLosTiposDeclaradosPublicosQuedanSinGuarda` fija el conjunto abierto en `{LOGIN}`: abrir
     otro obliga a tocar `TIPOS_PUBLICOS` y a fallar mientras no se haga.
  5. `conSesionAutenticadaLogoutYListarSiguenDelegando` evita que la guarda cierre el camino
     autenticado por error.
  6. E2E: `MensajeriaE2ETest.tramaNoAutenticadaSeRechaza` (socket crudo, 5 tramas de negocio sin
     LOGIN → `ERROR`, `contenido` y `usuariosConectados` nulos, y LOGIN posterior entra bien).
- [x] **`SEC-06` — Cerrar el surrogate de `SEC-05`.** ✅ Hecho 2026-10-05. Elegida la opción (b),
  pero **automatizada en vez de manual**: el checklist de PR lo remplace `requiereAutenticacion()`
  con default `true` más los 2 tests que lo vigilan. El parámetro `respuestas` de los 3 comandos
  dejó de ser "no usado" (ahora es el que responde el `ERROR`), así que `MUERT-13` desaparece solo.

> **Verificado con el bug reintroducido.** Se revirtió la guarda de `ListarUsuariosCommand` a
> propósito y el test falló nombrando el comando y la fuga: `expected [error:...] but was
> [error:..., usuarios]`. El test tiene dientes; no es decorativo.

---

## 3. Pendiente de verificación (código hecho, falta humano)

Esto **NO** es código por escribir. Es el smoke que las suites automáticas no cubren.

| ID | Tarea | Estado | Dónde registrar |
|---|---|---|---|
| SMOKE-01 | Matriz 6 direcciones Java↔Python↔.NET: texto, imagen sola, texto+imagen, respuesta a texto, respuesta a imagen; cita llega como texto ordinario; sin doble acuse ante `MENSAJE_LEIDO/ENTREGADO` | 🟡 Por verificar | `GUIA_ACEPTACION.md` § Matriz |
| SMOKE-02 | Dos procesos Java con cuentas distintas: orientación propia/ajena correcta por cuenta | 🟡 Por verificar | Misma matriz, § Historial |
| SMOKE-03 | Dos sesiones del mismo usuario: `LOGOUT` cierra una, presencia offline solo al cerrar la última; probar «Salir en todos» | 🟡 Por verificar | Misma matriz, § Historial |
| SMOKE-04 | Imagen nueva: `uploads/Nombre Apellido [CODIGO]/<hash>/` con original + 5 derivadas; aparece en otro cliente incluso tras reinicio; carga diferida + reintento | 🟡 Por verificar | Misma matriz, § Historial |
| SMOKE-05 | Smoke cambios 2026-10-04 (7 casos) | 🟡 Por verificar | `GUIA_ACEPTACION.md` § Smoke 2026-10-04 |
| **SMOKE-06** | **Matriz manual de transición de conexión × 4 estados × 3 clientes** (`Conectando`/`Conectado`/`SinRed`/`SesionCerrada`), con textos y tiempos medidos. `PAT-03` está implementado; falta la matriz manual completa. | 🟡 Por verificar | `GUIA_ACEPTACION.md` § Estados de conexión |
| **SMOKE-07** | **Capturas lado a lado de la misma conversación** en los 3 clientes, tema oscuro y claro; comparar después de implementar `VIS-01` | ⬜ Por hacer; captura de línea base si se ejecuta antes | `DIAGRAMAS/mockups/` |
| **SMOKE-08** | **Comprobación de contrastes WCAG AA** de los tokens de tema claro en los 3 clientes | ⬜ Por hacer | `PLAN_PATRONES_UI.md` §7.4 |
| **SMOKE-09** | **Vista administrativa del servidor**: estados vacíos/activos, doble clic y ficha paginada, IP y contadores, detalle de informe, espacio de Logs/Pool, cierre X sin parar TCP/JLine y recorrido JLine | 🟡 Por verificar | `GUIA_ACEPTACION.md` § Consola administrativa |

Criterio para dar por aceptada la paridad: SMOKE-01 a SMOKE-09 en verde con SO/versión/commit
anotados. Hasta entonces nada de lo de §5 se considera «aceptado».

---

## 4. Limpieza de código muerto

38 items, agrupados en 6 lotes. Detalle con `archivo:línea` y conteo de referencias en
[`docs/archivo/ANALISIS_TECNICO-historico.md`](docs/archivo/ANALISIS_TECNICO-historico.md) §2.

**Reglas de borrado** (están en [`PLAN_PATRONES_UI.md`](PLAN_PATRONES_UI.md) §2.1 y son la parte
importante de esta sección):

- **B1** — 0 referencias fuera de la definición **y** sin anotación de framework/reflexión/SPI.
- **B2** — **nunca borrar por búsqueda de texto.** `.split-pane`, `.list-cell`, `.combo-box` son
  clases integradas de JavaFX; `login.fxml:15 fx:controller=` es lo que hace viva
  `LoginController`; `ServiceLoader` hace vivos los 5 `Proveedor*`; el escaneo de JUnit hace vivos
  los 49 tests. Todos tienen 0 referencias y viven.
- **B3** — una clase `@Configuration`/`@Component` nunca es muerta.
- **B4** — un enum solo se borra si se comprueban antes las filas históricas de producción.
- **B5** — *dead feature* ≠ código muerto: `difundir()` sin botón es decisión de producto.
- **B6** — lo que se borre va con test.

| ID | Lote | Contenido | Estado | Aceptación |
|---|---|---|---|---|
| MUERTE-1 | Java: 13 símbolos + 3 parámetros `respuestas` + 12 imports | `MUERT-01..13`. Incluye `SEC-05`/`SEC-06` | ✅ Hecho | `mvn test` verde; el E2E de login sigue pasando |
| MUERTE-2 | .NET: 9 pinceles, 3 constantes, 1 propiedad, 1 variable, 12 `using` | `MUERT-25..30` | ✅ Hecho | `dotnet test` + `build -c Release`; comparación visual de ambos temas contra el mockup |
| MUERTE-3 | Python: 6 símbolos + imports + campos de test | `MUERT-16..24` | ✅ Hecho (sin `MUERT-23`) | `pytest tests` verde. **`_tareas_imagen` es un leak**: su borrado es corrección |
| MUERTE-4 | CSS y XAML muertos | `MUERT-31..38` | ✅ Hecho (sin `MUERT-37`) | **Medio riesgo**: arrancar la app y confirmar que cada control conserva su estilo antes de borrar (regla B2) |
| MUERTE-5 | Deduplicación | 3 parsers de `client.properties` → 1; `fallbackSchema` muerto; 2 migradores de esquema duplicados; `_leer_modo`/`_guardar_modo` unificados; tabla MIME unificada | ✅ Hecho (sin tabla MIME) | Tests existentes + test nuevo de equivalencia de `client.properties` en los 3 |
| MUERTE-6 | Decisiones de producto, no limpieza | `difundir()` sin botón en Python y .NET; clave `idioma=` sin uso; dependencia `pillow` sin imports; `ui/registro_window.py` | 🟠 Propuesto | Ver §9 |

### 4.1 Lo que estos lotes arreglan de paso

- **`MUERT-33`:** el título del sidebar salía del color equivocado en tema claro (`#1F3A5F` en vez
  del `#25343b` pretendido, por una segunda regla `.sidebar-title` que ganaba). Corregido.
- **`MUERT-28`:** un `_ = cuentaExistia;` que anulaba el aviso del compilador y escondía los
  `MUERT` reales. Desaparecido con la variable.
- **`MUERT-19`:** leak de memoria cerrado (`_tareas_imagen` crecía sin límite).
- **`MUERT-37`:** handler quehalbía de un botón inexistente (`BtnAdjuntarArchivo_Click` sin `Click=`).
- **Login .NET vs fachada:** `CargarServidor` leía `client.properties` del **directorio de
  trabajo** y la fachada de la **carpeta del ejecutable**, y solo leía `host`/`puerto`. Ahora los
  tres consumidores pasan por un `ClienteConfig` con una sola ruta. Comprobado en la app
  distribuida: editando `client.properties` el login muestra el host y el puerto nuevos.
- **Tests .NET contra el esquema real:** el DDL incrustado en `LocalStore.cs` no era "inalcanzable"
  — lo ejecutaban **todos** los tests de store, porque el proyecto de tests nunca copiaba el
  `.sql`. La app usaba un esquema y los tests otro. Ahora `Mensajeria.Tests.csproj` enlaza el
  `schema-local.sql` de `client-dotnet` y `LocalStore` falla con un mensaje claro si falta, en vez
  de crear un esquema divergente en silencio.
- El Javadoc obsoleto de `LoginConteoDTO.java` que afirmaba un `GROUP BY` sobre `LOGIN/CONECTADO`
  cuando la query solo agrupa por `LOGIN`.

### 4.2 Lo que **no** se borró, y por qué (reglas B2 y B5)

| Item | Por qué se conserva |
|---|---|
| `MUERT-23` (4 métodos de los dobles de prueba: 2× `enviar_async`, `enviar_imagen`, `difundir`) | No son código muerto: son **implementaciones de la interfaz que la vista sí llama**. `fachada_cliente.py:66,506` invocan `conexion.enviar_async()` y `chat_window.py:1008` invoca `fachada.enviar_imagen()`. Que ningún test de hoyercises ese camino es irrelevante: borrar el método haría que el doble mintiera sobre su contrato y dejaría un `AttributeError` latente. |
| `MUERT-37` (`bannerOffline` del XAML) | Es el **elemento que `UIX-00a` tiene que cablear** (§5.1): su aceptación es "el banner ámbar aparece en los 3 en ≤ 2 s". Borrarlo elimina el objetivo del P0. Es una función rota, no código muerto. |
| Tabla MIME de los 3 clientes (parte de `MUERTE-5`) | No es duplicación dentro de un módulo: Java tiene un `switch` propio y Python/.NET usan el `mimetypes`/`ContentType` de su plataforma. Unificarla **cambia el tipo de trama que se envía** (Java no clasifica `.bmp` como imagen y Python/.NET sí), o sea que es una decisión de producto de paridad que pertenece a `SEG-02`/`VIS-02`, no a un lote de limpieza. Se dejó intacta y **documentada** la deriva. |
| `MUERT-14` (`NULA`), `MUERT-15` (`FiltroMain`) | Son `P` (viven por contrato SPI / CLI documentada), no `C`. No entran en limpieza. |

---

## 5. Bugs y mejoras de interfaz

Plan por fases en [`PLAN_PATRONES_UI.md`](PLAN_PATRONES_UI.md). Evidencia completa en
[`ANALISIS_TECNICO.md`](ANALISIS_TECNICO.md) §3.

### 5.1 Fase A — 6 bugs P0 (funciones rotas, no estilo) — ✅ aplicada 2026-10-05

Ninguno requirió refactor. Cada uno es un cambio pequeño y verificable por separado.

| ID | Cliente | Qué estaba roto | Qué se hizo | Aceptación | Estado |
|---|---|---|---|---|---|
| **UIX-00a** | .NET | **La pérdida de conexión es invisible.** `OnDesconectado` existía en el transporte y en la fachada, y `bannerOffline` en el XAML, pero **ninguna vista se suscribe** y nadie cambia el `Visibility` | `PAT-03`: `ChatWindow` se suscribe a `IFachadaCliente.OnEstadoConexion` y hace `bannerOffline.Visibility = Visible` | Matar el servidor con el cliente abierto → el banner ámbar aparece | ✅ Hecho y **verificado en la app real** |
| **UIX-00b** | Python | **"Reintentar imagen" nunca reintenta**: el `_ejecutar("imagen", cargar)` estaba **después** del `raise`, dentro de `def cargar` | `_ejecutar(...)` se lanza fuera de `cargar` | Romper red → reintentar → la imagen aparece sin recargar | ✅ Hecho |
| **UIX-00c** | Java | `alError()` y `alDesconexion()` **estaban vacíos**: eran un comentario | `PAT-03`: los dos avanzan el estado; `alError` registra el motivo y `alDesconexion` pasa a `SIN_RED` | El servidor cae durante un envío → el usuario ve un mensaje | ✅ Hecho |
| **UIX-00d** | Java | El fallo de persistencia **se traga** (`catch (Exception e) { }`) | Se registra el tipo de trama y el motivo en el log | BD local en solo lectura → el mensaje aparece y el log lo dice | ✅ Hecho |
| **UIX-00e** | Python | **`login()` corre en el hilo de la GUI**: bloquea hasta 15 s | Hilo `login-fondo` + vuelta al hilo de la GUI en `_terminar_login`, con guarda de doble pulsación | Servidor inalcanzable → la ventana responde | ✅ Hecho |
| **UIX-00f** | .NET | **"Conectando…" se pinta dentro de la caja roja de error** | Nueva etiqueta `txtCargando` en gris neutro; la caja roja solo se abre para errores | Con latencia artificial: "Conectando…" en gris neutro | ✅ Hecho y **verificado en la app real** |

> **Por qué `UIX-00a` y `UIX-00c` se resolvieron con `PAT-03` y no a mano.** El plan decía que
> arreglarlos "a mano" en .NET sería una cuarta reimplementación de la misma idea. Se hizo el
> cimiento (`PAT-03`) y los dos fellos se cerraron de paso.
>
> **Verificación real, no solo unit tests.** Con el servidor MySQL levantado, la app .NET
> **publicada** y Netwright: login correcto → `Stop-Process` del servidor → el banner ámbar
> aparece con el texto `Sin conexión — mensajes en cola local, se reintentan solos`, y el
> `txtEstado` de la cabecera coincide. Antes no aparecía **nada**. El criterio de "≤ 2 s" **no se
> puede afirmar con esta medición**: el tiempo medido (12 s) es casi todo ida y vuelta de las
> herramientas, no la reacción de la app. Lo que sí queda demostrado es que **no hay sondeo**:
> el estado lo empuja el evento de muerte del socket. El tiempo exacto es trabajo de `SMOKE-06`.
>
> **Un defecto de layout que solo apareció al hacerlo visible.** El banner estaba en un
> `DockPanel` sin `DockPanel.Dock`, así que competía por el espacio sobrante con el `ListView` de
> contactos y lo aplastaba. Fijado a `Dock="Top"`; comprobado en pantalla.

> `PAT-03` es la base de la matriz de transición de conexión de `SMOKE-06`, que sigue pendiente
> porque exige cronometrar los 4 estados × 3 clientes a mano.

### 5.2 Fase G — Mejoras por pantalla (P1/P2)

Lote A: UIX-02 (no leídos), UIX-03 (fecha relativa), UIX-04 (separadores), UIX-05 (agrupación). Contrato: mismo autor + mismo día + <5 min agrupa; Hoy/Ayer; insignia (N). Suites entonces: JavaFX 75, Python 83, .NET 55.

Lote B: UIX-06 (buscar Ctrl+F) + UIX-07 (debounce 300 ms). Contrato: LIKE insensible con literales escapados, 200 ids de más nuevo a más viejo.

Lote C: UIX-08 (visor zoom/pan) + UIX-09 (cancelar carga). Atajos: rueda zoom, arrastrar pan, +,-,0,F, Esc. Suites entonces: JavaFX 79, Python 91, .NET 60.

Lote D: UIX-10 (burbuja relativa 65%), UIX-11 (splitter .NET persistido), UIX-12 (Shift+Enter en Py/.NET), UIX-13 (vacíos), UIX-14 (cargando), UIX-16 (anclaje + botón de nuevos). Suites: JavaFX 79, Python 92, .NET 61. Verificado en app real (.NET publicada + Netwright): vacío inicial visible y layout de 3 columnas intacto.

Lote G (agente 2, sin solape): UIX-17/18/19/24 + DIAG-07/08. UIX-28 bloqueado (ver §5.3).

Lote E: UIX-01 (virtualizar lista Python: `QListView` + `MensajeListModel`/`MensajeDelegate` en `ui/lista_virtual.py`, `_MessageRow` eliminado; contrato visual idéntico — burbuja 65%/tope 580, rachas, separadores, resaltado, estados, descarga/reintentar/ampliar por coordenadas; doble-click = responder, click-derecho = copiar cuerpo). Suites: Python 100 (92 + 8 nuevos `test_lista_virtual.py`). Verificado en app real (MCP): burbujas, racha sin remitente, separador "Ayer", resaltado y tema claro.

Lote F: UIX-15 (.NET): nueve avisos/errores recuperables migrados a un banner inline accesible, con nivel visual y cierre manual; el aviso remoto de sesión cerrada aparece en el banner del login. Se mantienen las tres confirmaciones modales de salida. Añadidos tests xUnit para los estados visible/oculto, reemplazo y limpieza del aviso. Verificación: `dotnet test` 71/71; `dotnet build -c Release` sin warnings ni errores.

| ID | Asunto | Prioridad | Clientes | Estado |
|---|---|---|---|---|
| UIX-01 | **Virtualizar la lista de mensajes** | **P0** | Python | Hecho (Lote E) |
| UIX-15 | Avisos y errores recuperables inline; conservar confirmaciones | **P0** | .NET | Hecho (Lote F) |
| UIX-13 | Estados vacíos (conversación vacía, búsqueda sin resultados, sin contactos) | P1 | los 3 | Hecho (Lote D) |
| UIX-05 | Agrupar mensajes consecutivos (ocultar remitente en una racha) | P1 | los 3 | Hecho (Lote A) |
| UIX-04 | Separadores de día | P1 | los 3 | Hecho (Lote A) |
| UIX-03 | Formato de fecha unificado y relativo | P1 | los 3 | Hecho (Lote A) |
| UIX-02 | Distintivo de no leídos en los 3 + filtro "No leídos" | P1 | los 3 | Hecho (Lote A) |
| UIX-06 | **Buscar dentro del historial** con `Ctrl+F` | P1 | los 3 | Hecho (Lote B) |
| UIX-07 | Debounce en la búsqueda | P1 | los 3 | Hecho (Lote B) |
| UIX-08 | Zoom y pan en imagen (rueda / `+` / `-` / `F` / `0` / `Esc`) | P1 | los 3 | Hecho (Lote C) |
| UIX-09 | Cancelar la carga de imagen al salir de vista | P1 | los 3 | Hecho (Lote C) |
| UIX-17 | Corregir 4 contrastes que fallan WCAG AA en tema claro | P1 | Java, Python | Hecho (Lote G) |
| UIX-18 | Diferenciar visualmente "Enviado" y "Entregado" | P1 | .NET | Hecho (Lote G) |
| UIX-19 | Nombre accesible en botones de solo icono | P1 | Python, .NET | ✅ Hecho en ambos clientes |
| UIX-24 | Foco inicial en el campo de código del login | P1 | .NET | Hecho (Lote G) |
| UIX-14 | Indicador de carga del historial | P2 | los 3 | Hecho (Lote D) |
| UIX-16 | Anclaje de scroll al cargar anteriores | P2 | los 3 | Hecho (Lote D) |
| UIX-10 | Ancho de burbuja relativo al ancho de ventana | P2 | los 3 | Hecho (Lote D) |
| UIX-11 | Sidebar redimensionable en .NET | P2 | .NET | Hecho (Lote D) |
| UIX-12 | `Shift+Enter` = salto de línea | P2 | Python, .NET | Hecho (Lote D) |
| UIX-21 | Cuerpo del mensaje seleccionable y copiable | P2 | Java, .NET | Por hacer |
| UIX-22 | Mnemónicos / accelerator keys | P2 | los 3 | Por hacer |
| UIX-23 | Escala de tipografía / alta resolución | P2 | los 3 | Por hacer |
| UIX-15b | Login redimensionable | P2 | Java, .NET | Por hacer |
| — | Tooltip de por qué servidor/puerto no son editables | P2 | los 3 | Por hacer |
| — | Cuarto estado en el login: "usuario no registrado" | P2 | los 3 | Por hacer |

### 5.3 Errores de tematización (P1)

| ID | Bug | Estado |
|---|---|---|
| UIX-25 | `.sidebar-title` duplicado y contradictorio (`MUERT-33`) | ✅ Hecho en `MUERTE-4`; estilo de tema claro corregido |
| UIX-26 | Python adjunta QSS de **tema claro** en modo oscuro para los iconos de adjunto | ✅ Hecho: la bandeja reconstruye iconos con los tokens del tema actual |
| UIX-27 | Cambiar de tema **reconstruye toda la lista** de mensajes en .NET | ⬜ Por hacer |
| UIX-28 | .NET usa `StaticResource` pero **reasigna por reflexión** (el propio comentario lo admite) | 🔴 Bloqueado (Lote G): quitarlo exige añadir `AppSurfaceBrush`/`AppTextBrush` a `Themes/*.xaml` (fuera de alcance UIX-18) y tipar el `ThemesDictionary` de WPF-UI leyendo `App.xaml` (fuera de la lista permitida). Ver informe Lote G |
| UIX-29 | Python arranca en tema claro mientras `tema.py` dice oscuro por defecto | ✅ Hecho (Lote G): `main.py` arranca en `Theme.DARK` con acento `#7B61B8` (canónico oscuro) |

### 5.4 Envío múltiple de imágenes y archivos en WPF — ✅ código y pruebas, falta smoke manual

**Implementado 2026-10-10.** Selección múltiple de archivos y arrastre múltiple, bandeja
deduplicada con miniaturas/íconos, límite de 12 MiB para imágenes directas, transferencias
fragmentadas por streams **secuenciales**, progreso ponderado por bytes, cancelación cooperativa y
prevención del cierre a mitad del lote. Ante cancelación no se registra ninguna fila en la cola
offline; ante fallo de transporte se encola solo el archivo que falló y los posteriores quedan sin
intentar. JavaFX ya ofrece selección/arrastre múltiple y una bandeja de hasta 5 adjuntos, pero aún
no tiene progreso por bytes ni cancelación. Python ya permite lotes de hasta 12 adjuntos por
selección o arrastre, eliminación individual, grupos de imágenes con caption y WebP como imagen;
todavía no ofrece progreso por bytes ni cancelación. La deuda restante es completar esas capacidades, según
[`PLAN_PARIDAD_CLIENTES.md`](PLAN_PARIDAD_CLIENTES.md).

**Cuatro decisiones de diseño que no son negociables** (el enunciado original —HTTP multipart,
`Task.WhenAll`— era incorrecto para este repositorio):

| # | Decisión | Por qué |
|---|---|---|
| D1 | N transferencias fragmentadas sobre el `IConexionRed` existente. **Cero HTTP** | El servidor no expone HTTP; el tope de trama son 16 MiB |
| D2 | **Secuencial**, nunca `Task.WhenAll` | `PROTOCOLO.md` §4 exige partes secuenciales con `ACK` por parte, y `ConexionRed` ya serializa con un `SemaphoreSlim` |
| D3 | La cancelación del usuario **no** es un fallo de transporte | `OperationCanceledException` no está en `EsFalloTransporte`: hoy no se encola por accidente, no por diseño |
| D4 | El `.webp` no se degrada a documento | Quedó **superada** por §5.6: el WebP se acepta de punta a punta |

Verificación: pruebas focalizadas **20/20**; suite .NET completa hoy **337/337**; build WPF Release
**0 advertencias / 0 errores**. La aceptación visual con los diez archivos, caída de red y
cancelación real **sigue pendiente** de ejecutarse manualmente.

### 5.5 Los seis fallos de los clientes JavaFX y .NET — ✅ código y pruebas; smoke parcial

Implementados: aviso inline de .NET que se cierra solo (con plazos por tipo); aviso inline **nuevo en
JavaFX**; `.webp` tratado como imagen en los dos clientes; botón de guardar en imágenes en ambos; el
visor de JavaFX abre el original desde disco con 1:1 de verdad (`EscalaVisor`); varias imágenes en
una sola burbuja en ambos (`PlanEnvioAdjuntos` + `MontajeImagenes`); lista de contactos de .NET al día
(no leídos + preview + orden) y preview con `"Tú: "` según `origen = yo`.

**Dependencias nuevas** (las dos nacieron del `.webp`):

| Cliente | Paquete | Licencia | Por qué |
|---|---|---|---|
| JavaFX | `com.twelvemonkeys.imageio:imageio-webp:3.12.0` | BSD-3 | Lector WebP por SPI de ImageIO (solo lector: no escribe WebP). |
| .NET | `SkiaSharp` + `SkiaSharp.NativeAssets.Win32` 4.153.1 | MIT | Descartada `SixLabors.ImageSharp`: la 4.x **exige licencia comercial** (rompe el build) y la 3.x arrastra NU1902/NU1903 en cada compilación. Skia además resuelve el montaje de imágenes sin WPF, que es lo que lo hace verificable desde la suite (`net10.0`). |

`contrato/puertos.json` registró **11 firmas nuevas** —el invariante 13 de `AGENTS.md` §5 lo exige—
sin tocar semántica de protocolo. Hoy son **23 puertos y 202 métodos**, y `verificar_puertos.py` da
exit 0.

**Lo que destapó la prueba en pantalla y se corrigió** (todo con test): el aviso de error que nunca
se quitaba (ahora **todos** los avisos se van solos, 12 s el error); el aviso inline que faltaba en
JavaFX; el preview que ponía "Tú: " a mensajes **recibidos**; y el agrupado del refresco, que
**descartaba** en vez de fusionar. Ese último destapó dos bugs de raíz:

1. **"Siempre sale Tú:"** era la **fecha**, no el prefijo. `fecha_envio` es `TEXT` y las consultas
   ordenan por ella **como texto**: el cliente escribía UTC con `Z` (`2026-10-11T03:14:51Z`) y el
   servidor mandaba hora local sin zona (`2026-10-10T22:14:51`), así que **todo lo que llegaba
   quedaba detrás de lo que salía**. `FormatoFecha.AUtc` normaliza al guardar y
   `LocalStore.NormalizarFechasAUtc` migra las filas ya guardadas (578 en la base real).
2. **"No me deja mandar imagen y texto en la misma burbuja"**: el pie se pasaba solo en la ruta del
   montaje y la imagen única lo perdía. El pie viaja hoy en el `contenido` de la **misma** trama y se
   persiste en la columna `caption`.

**Deuda que queda de este lote:**

- El agrupado de refrescos de la lista de contactos sigue en `ChatWindow.xaml.cs`, que los tests no
  compilan: la regla pura sí se testea (`FormatoPreview`), la de "llega un mensaje y la lista se
  entera" no tiene red de seguridad.
- El paso "insignia con fondo y esquinas" quedó sin hacer; solo cambió el texto (`"3"` sin
  paréntesis) y el color al brush del tema.
- Sin *sniffing* de magic bytes en los transcodificadores: la conversión decide por extensión.

El detalle de las cuatro rondas está en
[`docs/archivo/PLAN_CLIENTES-2026-10-historico.md`](docs/archivo/PLAN_CLIENTES-2026-10-historico.md)
§3. Los dos planes que dirigieron este trabajo (`PLAN3.MD` y `PLAN_AMBOS.md`) **ya no existen**: se
retiraron de la raíz al quedar ejecutados.

### 5.6 WebP de punta a punta — ✅ código y pruebas, **toca `server/` con visto bueno del usuario**

Consecuencia de §5.5: convertir a PNG **al elegir** hacía que quien recibía el archivo recibiera otro
(mandabas `foto.webp`, llegaba `foto.png`). Decisión del usuario: *"los webp llegan al servidor como
WebP, la conversión es solo para la vista"*.

Antes no era posible sin tocar el servidor: `Mensajes.procesarImagen` rechaza por magic bytes y
`server/utilerias` no tenía lector WebP.

| Dónde | Cambio |
|---|---|
| `server/utilerias/pom.xml` | `imageio-webp` 3.12.0 (BSD-3) |
| `Imagenes.esImagenValida` | acepta `RIFF` con firma `WEBP` en el **offset 8** (un WAV/AVI con RIFF se sigue rechazando) |
| `ImagenMagicaTest` | el rechazo de WebP pasa a aceptación + RIFF-no-WebP y RIFF-corto como rechazos |
| `ImagenWebpTest` (nuevo) | un WebP real atraviesa `Imagenes.leer`, que es lo que hace la tubería de filtros |
| JavaFX `CargadorAdjuntoEntradaChat` | deja de convertir al elegir |
| JavaFX `GestorAdjuntosChat.precalentarParaVer` | convierte **solo para pintar**; el original se guarda y es el que da "Guardar" |
| .NET `EnviarUnoAsync` | manda `adjunto.Ruta` tal cual, sin renombrar ni convertir |
| .NET `ChatWindow.PrepararParaVer` | copia `*_vista.png` al lado; `vm.RutaArchivo` sigue siendo el original |

Un `.webp` ilegible se manda como `MENSAJE_IMAGEN` y lo rechaza el servidor con un motivo claro; el
cliente no lo degrada a documento. Python también envía el WebP original como `MENSAJE_IMAGEN` y
usa Pillow solo para decodificarlo para la vista cuando Qt no lo admite; guardar la imagen conserva
el original.

### 5.7 Subida duplicada de la misma imagen — 🔴 **BUG DEL SERVIDOR, abierto**

**Encontrado 2026-10-10** leyendo `server/logs/server.log` (22:14:49-51) mientras se depuraba el
.envp de imágenes. El usuario subió **la misma imagen 3 veces seguidas** (hilos `filtros-13/14/15`,
mismo hash `2b16d8d8…`) y las 2ª y 3ª reventaron:

```
ERROR … Error procesando MENSAJE_IMAGEN … [insert into archivos (fecha_subida,hash_sha256,…)]
       Duplicate entry 'Joan Garcia [160004716]/2b16d8d8…' for key 'archivos.ruta'
```

`archivos.ruta` es única y dos subidas del mismo contenido con el mismo propietario la violan, así que
**el mensaje se pierde entero**: el receptor ve "No se pudo cargar · reintentar". Justo antes,
`[red-cliente-2] Limite de frecuencia excedido de 127.0.0.1 (max 10/s): trama descartada` ×12 — el
servidor tiró tramas y el cliente, sin enterarse de que perdió el ACK, reenvió.

**Lo que sí se corrigió en los clientes** (parte amplificadora, no la causa): el `catch` del montaje
de .NET y `EnviarMensajeComando.enviarGrupo` de JavaFX reenvían las imágenes sueltas ante
**cualquier** excepción, incluida la de red. Como el resultado de un envío es ambiguo, eso duplicaba
el contenido. Ahora solo se reenvía cuando el montaje **ni siquiera se compone**; si falla el envío,
se encola para el reintentador.

**Lo que queda:** que `archivos.ruta` admita la resubida (o que el servicio reintente el `insert`
con `archivoId` ya conocido). Es `server/` y **no depende de ninguna otra tarea**: es el bug abierto
que más rápido se puede cerrar.

### 5.8 Deriva del `schema-local.sql` distribuible de Python — ✅ cerrada 2026-10-11

Se añadió `caption` a la copia distribuible y a la migración aditiva de SQLite; el DAO lo conserva
en upserts, historial remoto, mensajes entrantes, envíos y reintentos. La prueba compara la copia
Python con el DDL canónico. La suite Python pasó (248 pruebas, 5 omitidas) el 2026-10-10 y
`contrato/verificar.py` terminó con exit 0, incluidas las comprobaciones Java y .NET.

### 5.9 Chat Python: acuses, ecos de sesión, historial y adjuntos — ✅ automatizado; 🟡 inspección visual pendiente

El 2026-10-11 se añadieron pruebas para aplicar acuses `MENSAJE_LEIDO` por ID, descartar callbacks
de historial de selecciones anteriores, confirmar el cierre del login y renderizar el fondo en la
ventana completa bajo ambos temas. El montaje de varias imágenes queda en la bandeja como vista
previa local; `EnviarMensajeComando` transmite secuencialmente cada ruta original con su propio ID,
nunca el PNG del montaje. Los mensajes de archivo e imagen incluyen etiquetas de fallback legibles.

Los ecos de mensajes propios se asocian por el campo `destinatario` y se guardan en la conversación
real, pero solo pueden mostrarse en otras instancias si el servidor ya entrega esos eventos. No se
modificó el servidor ni el protocolo. Falta que el usuario complete el smoke visual e interoperable
de `client-python/VERIFICACION_VISUAL_CHAT_PENDIENTE.md`.

### 5.10 Dos bugs del smoke Python: historial que «no se guardaba» y fondo plano — ✅ cerrados 2026-10-11

Reportados por el usuario con la app real durante la verificación visual (los mensajes enviados
desde Python desaparecían al reabrir la conversación y el fondo ilustrado no se veía nunca). Ambos
arreglados, con pruebas nuevas, y confirmados en vivo por el usuario:

1. **El historial propio salía de la página 0.** Los mensajes SÍ se guardaban en SQLite (la base
   por cuenta del smoke tiene cientos de filas), pero `obtener_conversacion` no los devolvía: esa
   base la comparten Python y .NET (AGENTS §6.5), .NET escribe `fecha_envio` en UTC con `Z` y
   Python escribía hora local sin zona. La columna es TEXT y se ordena como tal, así que filas
   .NET de la noche anterior quedaban «más nuevas» que lo propio recién enviado y los 50 primeros
   puestos se llenaban solo con filas `Z` — exactamente el bug que .NET ya había resuelto para sí
   (UIX-26). Réplica del mismo arreglo en Python: `transversal/fechas.a_utc` (formato "O" de .NET,
   7 decimales + `Z`; lo que ya trae `Z` pasa intacto) como único formato de escritura en
   `guardar_mensaje`, `guardar_pendiente`, receptor entrante, lector de historial remoto,
   coordinador de envíos y burbuja optimista, más la migración de una pasada
   `Database._normalizar_fechas_utc` al abrir la base (idempotente). Java no necesita este arreglo:
   su H2 es por cliente y no comparte archivo.
2. **`fondo_chat_claro/oscuro` no se pintaban nunca.** El pincel de textura del viewport
   (`QPalette.Base`) lo anula cualquier stylesheet de un ancestro (`_panel_chat.setStyleSheet` en
   el chat real); la prueba unitaria no tenía ese ancestro y por eso pasaba en offscreen. Ahora el
   fondo se pinta en el `paintEvent` de la lista sobre el viewport, a cover y centrado — el
   equivalente del CSS del cliente Java (`.message-list { -fx-background-image: url(...);
   -fx-background-size: cover; }`) — y la regresión nueva monta la lista dentro de un panel con
   QSS. Nota Qt que costó una iteración: un widget-viewport propio NO sirve, porque
   `QAbstractScrollArea` consume el Paint del viewport y no invoca el `paintEvent` de ese widget;
   el sitio correcto es el `paintEvent` de la propia lista.

Suite: **266 pruebas, 0 fallos, 5 omitidas** (2026-10-11), incluidas la regresión de orden
(página 0 con semilla mixta local/Z) y la del fondo bajo un panel con stylesheet.

### 5.11 Bucle de reconexión al reintentar una imagen gigante — ✅ cerrado 2026-10-11

Cada login el servidor veía `Trama invalida (Longitud de payload invalida: 47225041)` y cortaba la
sesión, así que el cliente Python entraba en bucle de reconexión. La aritmética del propio log
delató la causa: un **pendiente de imagen de 35,4 MB** (`road-car-forest-top-view.jpg`, 24
intentos) se reenviaba como UNA trama de 47.225.041 bytes — base64 de 35 MB + JSON — y el tope de
trama del servidor es 16 MiB (invariante 2): rechazo + cierre. El cliente carecía de las dos
defensas que sí documentan `PROTOCOLO.md` y ya tenía el cliente .NET:

1. `enviar_imagen` valida el tope de imagen en trama única **antes** de abrir el socket:
   `IMAGEN_MAX_BYTES = 12 MiB`, misma regla y mensaje que
   `CatalogoAdjuntos.MaxBytesImagenTramaUnica` («la imagen supera el límite de envío directo
   de 12 MB»). Una imagen mayor debe viajar como ARCHIVO fragmentado, no como MENSAJE_IMAGEN.
2. El **reintentador** ya no reintenta lo irreintentable: un pendiente de imagen mayor al tope se
   marca `ERROR` (aparece «! No enviado») y sale de la cola en el mismo pase, sin tocar el socket,
   y el resto de la cola sigue flusheando. Sin esto, el pendiente era una píldora venenosa que
   mataba la conexión en cada login para siempre.
3. Defensa sistemática: `transversal/protocolo.empaquetar` levanta `ValueError` si el payload
   supera el tope de 16 MiB — nadie puede volver a construir una trama que el servidor rechazará
   cerrando la sesión.

Verificado con la app real y la base del smoke: tras el login, `pendientes_envio` quedó en 0, los
textos atascados («AAA», «3R», «AAAA») pasaron a ENTREGADO y la imagen venenosa quedó en `ERROR`.
Suite: **269 pruebas, 0 fallos, 5 omitidas**.

### 5.12 FORMATOS-01: AVIF/HEIC/SVG como imagen de punta a punta + vista de mensajes DESC — ✅ cerrado 2026-10-11

Tres piezas pedidas juntas por el usuario tras probar un `.avif` que viajaba como archivo:

1. **El servidor ya validaba y filtraba los formatos amplios** (`Imagenes.esImagenValida` acepta
   AVIF/HEIC por `ftyp`+marca y SVG por primer elemento; `DecodificadorMagick` los abre con
   ImageMagick). Lo que faltaba era la CLASIFICACIÓN en los clientes: sus conjuntos de extensiones
   solo tenían png/jpg/jpeg/gif/bmp/webp, así que un `.avif` salía por `MENSAJE_ARCHIVO` y nunca
   tocaba los filtros. Ahora los tres mandan `MENSAJE_IMAGEN` (invariante 17): Java
   `TextoCliente.EXT_IMAGEN` + filtro del selector; .NET `CatalogoAdjuntos.ExtensionesImagen` +
   `FiltroDialogo` + MIME/icono; Python `gestor_adjuntos_chat._EXTENSIONES_IMAGEN` +
   `bandeja_adjuntos_chat.EXTENSIONES_IMAGEN` + filtro del diálogo.
2. **Visualización en los 3 clientes**, siempre convirtiendo a PNG SOLO para pintar (el original
   viaja y se guarda tal cual):
   - **Python**: `ui/decodificador_imagen.py` (nuevo) — Pillow con `pillow-avif-plugin`/`pillow-heif`
     para AVIF/HEIC y `QSvgRenderer` (viene con PySide6, sin dependencias nuevas) para SVG. Lo usan
     el cargador de la conversación y el icono de la bandeja.
   - **.NET**: `TranscodificadorImagen.ConvertirAPng` añade el relevo a **Magick.NET** cuando Skia
     no puede (AVIF/HEIC/SVG) y `ExtensionesNecesitanConversion` crece a los 5 formatos; el visor
     y las burbujas ya consumían ese camino.
   - **Java**: `TranscodificadorImagen` rasteriza SVG con **jsvg** (un jar puro, Apache-2) y añade
     el plan B del servidor — **ImageMagick vía im4java** (`DecodificadorMagickCliente`, con la
     misma autodetección y tolerancia a ausencia) — para AVIF/HEIC, porque no existe lector ImageIO
     de esos formatos compatible con Java 21 (NightMonkeys exige Java 22 o `--enable-preview`; sus
     jars 0.6.3/1.x lo comprueban y se desactivan). El visor (`VisorImagenChat.abrir`) también
     recae al PNG convertido para no abrir la miniatura borrosa. Sin ImageMagick en el PC, la
     burbuja muestra «Formato no soportado por este cliente» y el mensaje sigue siendo imagen con
     su botón de guardar. El montaje (Java y .NET) ya tenía relevo: si no se puede componer, cada
     imagen sale por su cuenta.
3. **Vista de mensajes del servidor, del más reciente al más viejo**: `MensajeRepository.historico`
   (pestaña «Mensajes») y `Servicios.armarInformeMensajes` (Informe 3, consola y pestaña Informes)
   pasan de `ASC` a `DESC, id DESC`; el informe se retitula. La ficha de usuario ya era DESC.

Pruebas nuevas: Java `TranscodificadorImagenTest` (AVIF real embebido vía ImageMagick con
`assumeTrue`, SVG con jsvg), .NET `CatalogoAdjuntosTests`/`TranscodificadorImagenTests` (AVIF real
vía Magick.NET, SVG), Python `test_cargador_adjunto_entrada_chat` (AVIF/HEIC/SVG generados al
vuelo, original intacto). Suites: Java **283**, .NET **343**, Python **270**, `comp-servicios` **8**
— todas en verde el 2026-10-11.

### 5.13 Iconos de archivo al estilo Telegram Web + crash WPF por `x:Static` con `int` — ✅ cerrado 2026-10-11

Dos cosas sobre los iconos de `FORMATOS-02` (invariante 18):

1. **Tamaño y doble icono**: los lados eran 18/20 px en burbuja — apenas se veían — y el texto
   de la burbuja de archivo seguía trayendo el `📎` genérico junto al icono nuevo. Ahora los tres
   clientes pintan **48 DIP** en burbuja y bandeja (estilo tarjeta de archivo de Telegram Web) y
   el `📎` **solo queda para las extensiones sin dibujo en el catálogo**: Java quita el prefijo en
   `MensajeCell.bloqueArchivo`, Python centraliza la regla en `iconos_archivo.prefijo()` y la
   aplican los tres constructores del texto (historial, mensaje en vivo y burbuja optimista).
2. **Crash del cliente .NET** (`Unable to cast object of type 'System.Int32' to type
   'System.Double'`, en cascada de MessageBoxes y caída a los ~15 s): el XAML consumía
   `Width="{x:Static transversal:IconosArchivo.LadoIconoBurbuja}"` con la constante declarada
   `const int`. El loader BAML no convierte el `int` encajonado a la DP `double` y
   `FrameworkElement.MinMax..ctor` revienta al medir cada burbuja. Solución: los dos lados
   (`LadoIconoBurbuja`/`LadoIconoBandeja`) son `const double` en `IconosArchivo.cs`.
   Diagnóstico por el stack del propio log del cliente (`errores-cliente.log` →
   `MinMax..ctor`, sin frames de la app) y confirmado con el valor del log del servidor del
   smoke previo. Dependencia Python asociada: `pillow-heif 1.8.0` exige `pillow>=11.1.0`, así
   que `requirements.txt` sube a `pillow==11.3.0` (el `ResolutionImpossible` venía de fijar
   10.4.0).

Suites tras el lote: Java **286**, .NET **366**, Python **278** — todas en verde el 2026-10-11;
build Release de WPF sin errores (valida el XAML con las constantes nuevas).

### 5.14 Test de framing de Java data-driven — ✅ cerrado 2026-10-11

`TramaCodecParidadTest` (client-app) tenía los 6 vectores + 5 rechazos de `contrato/framing.json`
**copiados como literales hex**: Python los lee por ruta (`test_framing.py`) y .NET los **enlaza**
desde su csproj, pero un cambio del contrato exigía editar Java a mano y ningún verificador comparaba
esa copia. **Cerrado**: el test ahora **lee el JSON** (con Gson desde `../../contrato/framing.json`,
la misma ruta relativa que usará cualquier clon) y recorre el contrato: los 6 vectores byte a byte
contra `trama_hex` + ida y vuelta por stream + control negativo del bug de caracteres, y los 5
rechazos en `decodificar` y por el stream (aceptando `CodecException` o el corte de stream que la
trama incompleta produce). 6 pruebas; los 15 literales hex salieron del código. La paridad de framing
ya es mecánica en los 3 clientes.

### 5.15 Smoke real de 20 MiB: tres defectos de entrega y descarga — ✅ dos cerrados, 🔴 uno abierto

La casilla de §7 del plan de paridad marcaba la matriz de 6 direcciones como hecha, pero la evidencia
solo era "implementado y probado contra dobles". El smoke real (`smoke/smoke_adjunto_20mb.py`, dos
sesiones TCP contra el servidor de verdad, 20 MiB **no comprimibles** y comparación de SHA-256)
destapó tres cosas:

**a) El servidor mataba la sesión de quien recibía un adjunto grande — cerrado.**
`EntregaCaso.entregar` mandaba el `MENSAJE_ARCHIVO` entero en una trama, con los bytes en base64
dentro de `contenidoImagen`. Un archivo de 20 MiB son 27 962 406 bytes de payload y la trama vive de
16 MiB: el codec lo rechazaba y el servidor **expulsaba la sesión del destinatario** (log:
`No se pudo entregar a 160004716: Payload de 27962406 bytes excede el maximo de 16777216` +
`Sesion eliminada`). El mismo fallo pasaba con el eco multi-PC, que se perdía en silencio.
Ahora, si la trama no cabe, se reintenta con la **versión ligera** (mismo mensaje sin
`contenidoImagen`, conservando `archivoId`, `nombreArchivo`, `tamanoArchivo`, `hashSha256` y el pie)
y **solo se expulsa la sesión si tampoco cabe esa**. El receptor baja los bytes con
`DESCARGAR_ARCHIVO`. Pruebas: `EntregaCasoPayloadGrandeTest` (4 casos). El `PROTOCOLO.md` §5
documenta la regla.

**b) Python escribía un archivo de 0 bytes al descargar cualquier cosa de más de 1 MiB — cerrado.**
`descargar_archivo` solo entendía la trama única `DESCARGAR_ARCHIVO_RESPUESTA`. El servidor, para
más de 1 MiB, responde `ARCHIVO_INICIO` + `ARCHIVO_PARTE`\* + `ARCHIVO_FIN`: la primera trama
completaba la espera de la petición y las partes llegaban fuera, así que se escribía un archivo vacío
**sin ningún error** (Java y .NET ya tenían colector). Añadido `ColectorFragmentos`
(`fachada/servicios/colector_fragmentos.py`) y el enroscado por id en `ConexionRed`, más un fallo
explícito si el servidor devuelve el archivo vacío. 7 pruebas nuevas
(`test_colector_fragmentos.py`).

**c) El historial remoto no devuelve `archivoId` de los mensajes propios — 🔴 abierto.** En A→A el
servidor no hace eco a la sesión que originó el envío (ella ya correlaciona su ACK, por diseño), así
que el adjunto propio llega por la página remota; ahí la fila se queda sin `archivoId` y **quien se
envía un archivo a sí mismo no puede descargarlo**. El smoke lo reporta como `DEUDA` y no falla por
ello. Pendiente: revisar `HistorialCaso`/`AutenticarCaso` para los mensajes propios y la fusión del
cliente (`buscar_equivalente`). No es una regresión de este lote.

### 5.16 JavaFX: progreso por bytes y cancelación en la UI — ✅ cerrado 2026-10-13

El comando ya aceptaba `alProgreso`/`cancelar()`, pero no había ni barra ni botón: la capacidad
figuraba como ausente en la matriz de paridad. Ahora hay `barraProgresoEnvio` en `chat.fxml`
(`ProgressBar` + etiqueta + "Cancelar envío"), `BarraProgresoEnvioChat` como cableado JavaFX,
`ProgresoEnvio`/`AgregadorProgreso` como modelo por bytes (espejo de `Transversal/ProgresoEnvio.cs`) y
`EnviarMensajeComando.restantes()` para que **lo cancelado vuelva a la bandeja** en vez de perderse.
Granularidad documentada: .NET y Python reciben un aviso por cada parte del fragmentado; Java informa
al cerrar cada adjunto, porque exponer ese callback tocaría `client-interfaces` y el contrato de
puertos. Pruebas: `AgregadorProgresoTest` (8) y `EnvioLoteCancelacionTest` (5).

**Trampa documentada:** un `javafx.concurrent.Task` **solo se ejecuta si el executor lo despacha al
hilo de JavaFX**. Con un executor directo, `call()` no llega a invocarse y el envío no ocurre sin que
salte ningún error; los tests de `EnviarMensajeComando` usan `Platform::runLater` y vacían la cola.

### 5.17 Aviso inline con severidad y anuncio dinámico en JavaFX y Python — ✅ cerrado 2026-10-13

Python tenía un rótulo plano de 5 s sin severidad ni título; JavaFX tenía severidad pero no anunciaba
los cambios. Ahora los tres clasifican igual (Error / Atención / Aviso / Listo, con los mismos plazos
4/6/8/12 s) y los tres **anuncian**: WPF con `LiveSetting="Polite"`, Qt con
`setAccessibleDescription` y JavaFX reescribiendo el texto accesible del contenedor. El truco es el
mismo en Qt y JavaFX y está comentado en ambos: un lector de pantalla no vuelve a leer lo mismo, así
que el texto se limpia y se vuelve a poner, o el mismo error saltando dos veces se quedaría mudo.
Pruebas: `AvisosInlineChatTest` (+5 en JavaFX) y `test_estado_aviso_inline.py` (7 en Python).

### 5.18 .NET Fase 2: code-behind repartido en parciales — ✅ cerrado 2026-10-13

`ChatWindow.xaml.cs` (1 973 líneas) y `FachadaCliente.cs` (960) eran los dos archivos que impedían
cerrar el objetivo de < 400 líneas. Repartidos por responsabilidad, sin tocar una línea de lógica:

| Antes | Después | Parciales |
|---|---|---|
| `ChatWindow.xaml.cs` (1 973) | **191** | Estado, Sesion, Eventos, Vista, Contactos, Historial, Scroll, Navegacion, Busqueda, Adjuntos, Envio |
| `FachadaCliente.cs` (960) | **218** | Red, Sesion, Envio, Historial, Mensajes, Soporte |

El mayor parcial se queda en 309 líneas. Dos reglas nuevas en `ArquitecturaTests.cs` lo impiden
regresar: ningún `.cs` de `Views/` supera 400 líneas, y los parciales del chat existen todos. La
división de `FachadaCliente` obligó también a corregir el `.csproj` de los tests, que enumeraba un
solo `FachadaCliente.cs`: compilaba el núcleo sin sus parciales y la fachada "desaparecía" en
silencio de los tests.

### 5.19 Segunda tanda del plan de patrones: composición y estado en Python y .NET — ✅ 2026-10-13

Ejecutados `PAT-01` (composición), `PAT-VM` (estado) y la tabla de reparto de Python. Ninguno era una
avería: eran sitios donde el mismo dato vivía en dos sitios y nadie lo notaba.

| Ítem | Antes | Después |
|---|---|---|
| Python: piezas de la vista | `ChatWindow.__init__` construía estado de búsqueda, hub de señales, pool y preferencias | `ui/composicion_chat.py` las construye; `main.py` es la raíz y se las pasa (`ContextoChat`) |
| .NET: almacenamiento local | `FachadaCliente` hacía `new LocalStore` + `new HistorialLocal` + `new CacheUsuarios`, **también antes del login** | `Composicion/ProveedorSesionLocal` los compone y `FabricaCliente` lo inyecta |
| Python: estado de conversación | `usuario_seleccionado`, `_offset`, `_cargando_historial`, `_respuesta_mensaje` y `_enviando` de la ventana; el botón se habilitaba con un `if` en 3 sitios | `ui/chat_view_model.py` (`QObject`, 5 señales, `Property`, predicado único `puede_enviar()`); la ventana expone 4 nombres como **accesos** |
| .NET: estado de conversación | 5 campos privados de `ChatWindow` repartidos en 12 parciales; `IsEnabled` escrito a mano | `ViewModels/ChatViewModel.cs` (`INotifyPropertyChanged`, `PuedeEnviar`); la ventana los expone como accesos |
| Python: reparto de tareas | dos cadenas `if/elif` de ~18 ramas; un tipo sin pintar no daba ningún aviso | `ProcesadorTareasChat.MANEJADORES` (10 manejadores con nombre) y `KeyError` ante un tipo sin manejador |

Lo que encontró el trabajo, y no se buscaba:

- **`marcar_leidos` no tenía manejador.** Caía al final de la cadena sin hacer nada; con la tabla
  habría sido un `KeyError` en el hilo de UI. Ahora tiene manejador explícito que no pinta nada (la
  insignia la baja el directorio) y `test_toda_tarea_lanzada_tiene_manejador` recorre los
  `_ejecutar("tipo", …)` del paquete `ui/` y comprueba los dos sentidos: que no falte ninguno y que
  no sobre ninguno.
- **La costura de los tests de Python era `ui.chat_window.PreferenciasChat`.** `test_ui.py` sustituye
  ese símbolo por unas preferencias con `QSettings` temporal, a propósito, para que el «último chat
  guardado» de una prueba no se filtre a la siguiente. Al construir el contexto en otro módulo, ese
  parche dejó de aplicarse y dos pruebas de no-leídos fallaron. Por eso el import sigue en
  `chat_window.py` y el contexto acepta una `preferencias` ya construida.
- **El offset se reinicia al cambiar de destinatario**, ahora en el ViewModel. Antes dependía de que
  cada parcial se acordara de hacerlo.
- La regla que prohíbe framework en el ViewModel pasó de «no importar `PySide6`» a «no importar
  `QtWidgets`/`QtGui`»: el límite real es no **pintar** ni **leer disco** desde el estado, y `QtCore`
  solo da señales y propiedades.
- `ProveedorSesionLocal.Dispose()` no vaciaba `Actual` (lo detectó su propio test) y `Dispose` no era
  idempotente.

Verificación de esta tanda: JavaFX 298/298 (50 clases), Python 297 (5 omitidas), .NET 386/386, build
WPF Release 0 errores/0 avisos, `contrato/verificar.py` 9/9 con exit 0.

### 5.20 `PAT-04` y `PAT-05`: lo que se hizo y lo que se rechaza, con motivo — 2026-10-13

- **`PAT-04` a medias, a propósito.** Hecho: el SQL del upsert de `historial_local` sale del cuerpo
  del método a `_GUARDAR_MENSAJE_SQL`, y el de .NET a la constante `INSERTAR_MENSAJE`, que es lo que
  la fila del plan señalaba (`inline`) y lo que ya hace `HistorialLocal.java`.
  **Rechazado**: convertirlos al `DELETE + INSERT` de Java. El de Java solo conserva el `estado` y
  **pisa el resto de columnas**, así que un mensaje que vuelve a llegar sin `archivo_id` ni
  `hash_sha256` borraría lo ya guardado; Python y .NET usan `COALESCE` y `MAX(descargado)` para
  exactamente eso. Traducirlos tal cual sería una regresión silenciosa en el hottest path y en el
  contrato de estados (invariante 12). Y no hay dialecto del que liberarse: por invariante 10 ambos
  son SQLite.
- **`PAT-05` con la premisa desfasada.** El fail-fast de TLS **ya era coherente en los tres**:
  `ValidarTls.java`, `ValidarTls.cs` y `transversal/tls.py` leen `tls.confianza`, fallan antes del
  handshake con un error accionable y usan las CA del sistema cuando no hay almacén. Queda solo la
  costura `Transporte { Pedir, Enviar }`, que es refactor sin ningún defecto detrás.
- **`PAT-CMD` (fase E) auditado y descartado.** Ninguno de los tres clientes usa comandos: Java
  declara el manejador en el FXML por nombre, Python encadena señales a métodos y .NET usa
  `Click`/`TextChanged`. Lo que difiere entre ellos es dónde vive el cableado, no el patrón.

Pendiente de estos tres ítems, si alguien lo quiere: la interfaz `HistorialLocal` en Python (hoy la
vista la salta y habla con `fachada.historial`) y la costura `Transporte` en los tres. Ninguna de las
dos es una avería.

### 5.21 Checklist de cierre: 3 defectos del smoke visual + resto de patrones — ⬜ abierto 2026-10-14

El usuario ya hizo el smoke visual con las 3 apps abiertas y reportó 3 defectos. Esta es la lista
canónica de lo que falta; nada de aquí está implementado todavía.

**A — Defectos del smoke (los 3 con causa localizada en el código).**

- [ ] **PY-1 · El "visto" (LEÍDO) no se pinta de azul en Python.** Causa probable:
  `ui/celdas/mensaje_delegate.py:268-270` pide `"Le" in raw_state`, pero el estado real es `"LEIDO"`
  (todo mayúsculas) y la comparación es sensible a mayúsculas: nunca se cumple y siempre pinta
  `visto_otro`. Los tokens sí existen (`tick.read` → `visto_leido`). Cierre: comparar contra
  `"LEIDO"` exacto y comprobar a ojo que el `✓✓ Leído` propio sale azul en los 2 temas y el
  `✓✓ Entregado` sigue gris.
- [ ] **JV-1 · La sesión propia sale en la lista de chats (Java).** Python ya filtra
  (`procesador_tareas_chat.py:201`, `refrescador_contactos_chat.py:81` con `!= self.codigo`); Java no:
  `SincronizadorDirectorioCliente.sincronizarDirectorio()` guarda el directorio tal cual, con la
  sesión dentro. Cierre: filtrar en la causa raíz (sincronizador o vista del directorio, no en cada
  llamada) y comprobar que la propia cuenta no sale en la lista ni se puede abrir auto-chat, sin
  romper `asegurarEnCache` ni el conteo de conectados.
- [ ] **PY-2 · Las respuestas no se ven como en .NET.** .NET separa cita/cuerpo con
  `FormatoCita.Separar` y pinta la cita como bloque (fondo `CitaBgBrush`, borde izquierdo grueso
  `3,1,1,1`, clicable → `NavegarACitaAsync`); Python pinta la burbuja con el texto crudo
  `Respuesta a …`. Cierre: portar la misma regla de separación (cita = lo anterior al primer salto
  doble, igual que `MensajeCell.extraerCita` en Java) y el mismo bloque citado clicable con
  navegación al mensaje original, incluyendo el caso de la cita con saltos dentro.

**B — Resto de `PLAN_PATRONES_UI.md` (su §9.1).**

- [ ] **Registrar el smoke visual ya hecho (condición 2).** Falta la evidencia escrita: fecha, las 6
  direcciones probadas y cuentas usadas, en `GUIA_ACEPTACION.md`. Sin eso la casilla no se marca.
- [ ] **`VIS-02` de ventana/diálogo (condición 3).** Único trabajo real pendiente del plan:
  centralizar los literales de ventana y diálogo. (La fila "G (cola)" del plan dice `[x]` pero su
  texto admite que esto falta; corregirla al cerrar.)
- [ ] **Regla B2 (condición 5).** Si el smoke ya arrancó JavaFX y miró los controles tocados, se
  cierra con el registro; es control de proceso, no verificable por script.
- [ ] **Decidir los dos restos opcionales** (ninguno es avería): interfaz `HistorialLocal` en Python
  y costura `Transporte { Pedir, Enviar }` en los 3. O se hacen o se dejan documentados como
  descartados.
- [ ] **`UIX-*` restantes del roadmap §5** (pulido visual por pantalla, fuera del núcleo de patrones).

---

## 6. Patrones de diseño en los clientes

Detalle, riesgos y criterios medibles en [`PLAN_PATRONES_UI.md`](PLAN_PATRONES_UI.md) §3–6.

| ID | Patrón | Qué resuelve | Prioridad | Estado |
|---|---|---|---|---|
| **PAT-01** | Composition root real | Java: `FabricaCliente` compone runtime/adaptadores; los puertos de login y chat y la navegación se inyectan por constructor vía `FabricaControladores`; sin localizador estático. Python conserva 6 colaboradores con defaults `or`; .NET conserva la composición pendiente en UI | P0 (base de PAT-VM) | 🟡 Parcial: Java implementado y compilado 2026-10-06; Python/.NET pendientes |
| **PAT-ARQ-JAVA** | SOLID + límites limpios en cliente Java | Puertos por consumidor para login, chat y transporte; mapper JSON inyectado; evento de aplicación para UI; ejecutor inyectado; gestores de adjuntos y reconexión; delegación del controlador al ViewModel; reglas ArchUnit | P1 | 🟡 Parcial: **289 pruebas** pasan (auditoría 2026-10-11, archivada en `docs/archivo/PLAN_ARQUITECTURA_CLIENTE_JAVA-historico.md` §«Estado verificado»), incluida reconexión automática; DTO confinados a adaptadores; reglas ArchUnit 4–5 con los 5 colaboradores nuevos; el controlador bajó a 552 líneas (extracción de avisos; sigue en `PAT-VM`) y **resta el smoke visual formal** |
| **PAT-03** | `EstadoConexion` observable (4 estados, 3 clientes) | Cerró la falta de notificación que originó `UIX-00a` y `UIX-00c`; las vistas se suscriben al estado publicado por la fachada | P0 | ✅ **Hecho 2026-10-05** (§5.1) |
| **PAT-02** | Guarda de autenticación obligatoria en comandos nuevos | El surrogate de `SEC-05`: la firma dice "puede responder errores" y el cuerpo dice lo contrario | P0 (con `SEC-05`) | ✅ **Hecho** con `SEC-05` (§2): default `true` + 2 tests |
| **PAT-VM** | MVVM: `ChatViewModel` en los 3 | JavaFX tiene ViewModel que centraliza el estado conversacional y sirve de frontera de operaciones, pero el controlador conserva coordinación JavaFX (545 líneas, bajó de 968; extracciones de `AvisosInlineChat` y `ConfiguradorCeldasChat` el 2026-10-11). Python partió el chat en 31 módulos de `ui/` (`chat_window.py` es ahora un ensamblador de 293 líneas, 214 sin blancos) y tiene `chat_view_model.py`; .NET va ⅓ (`MensajeVM` y `UsuarioVM` notifican, la ventana concentra 1 858 líneas) | P1 | 🟡 Parcial en los 3; la coordinación JavaFX y el tamaño de `ChatWindow.xaml.cs` siguen abiertos |
| **PAT-CMD** | `Command` / `ICommand` para las acciones de UI | "Puedo enviar" está implementado **3 veces** y en .NET hay **0 `ICommand`**, así que ningún botón se habilita de forma declarativa | P1 | ⬜ Por hacer |
| **PAT-04** | Adapter del almacén local | Python **no tiene interfaz**: la vista la salta y accede a `fachada.historial`. El upsert `DELETE + INSERT` portable ya está documentado y solo se usa en Java | P2 | ⬜ Por hacer |
| **PAT-05** | Adapter de transporte TCP/TLS | Sigue habiendo estrategias y formatos de almacén distintos; el fail-fast ya está cableado en los 3 clientes y se comprueba en sus suites | P2 (cierra `SEG-01`) | 🟡 Implementación validada el 2026-10-08; falta la matriz de handshake de aceptación |
| **PAT-06** | Test de equivalencia del `schema-local.sql` en .NET | `test_schema_distribuible.py:34` protege la copia Python; **la .NET no tiene equivalente** y puede divergir en silencio | P2 | ✅ Cerrado **por otra vía** (en `MUERTE-5`): `Mensajeria.Tests.csproj` enlaza el `schema-local.sql` real y los 35 tests de store lo ejecutan |

### 6.1 `PAT-03` en detalle — qué se añadió

Un enum por cliente con **los mismos 4 estados y los mismos textos**, y un punto único de
cambio. La vista ya no deduce nada: se suscribe.

| | Java | Python | .NET |
|---|---|---|---|
| Definición | `client-interfaces/.../transversal/EstadoConexion.java` | `transversal/estado_conexion.py` | `Transversal/EstadoConexion.cs` |
| Se suscribe con | `fachada.alCambiarEstado(obs)` | `fachada.on_estado_ui` | `fachada.OnEstadoConexion +=` |
| Lo publica | `FachadaCliente.cambiarEstado` (único sitio) | `FachadaCliente._cambiar_estado` | `FachadaCliente.CambiarEstado` |

Los 4 estados: `Conectando` · `Conectado` · `SinRed` · `SesionCerrada`. Los dos degradados
muestran banner.

**Dos decisiones que no son obvias y conviene no volver a discutir:**

1. **`CLOSE_NOTICE` es `SesionCerrada`, no `SinRed`.** El servidor cerró la sesión a propósito;
   ofrecer "reconectar" ahí es Offering la acción equivocada. Los 3 clientes lo distinguen, y hay
   test en los tres.
2. **Un cierre voluntario no degrada el estado.** El hilo receptor también muere al cerrar a
   propósito, así que `alDesconexion` disparaba en un logout normal. En Java se resolvió en el
   transporte con un **contador de generación** (`ConexionCliente.generacion`): el lector solo
   reporta si sigue siendo el dueño del socket. Sin eso, el banner parpadearía en el login. En
   Python y .NET se resolvió con la guarda `if estado != SesionCerrada`.

> **Efecto colateral arreglado de paso.** El `finally` del hilo lector viejo también fallaba
> **todos los pendientes** de la conexión nueva, porque `pendientes` es compartido. Ahora un
> lector obsoleto no toca nada. No se tocó esa parte a propósito: cambiar la semántica de fallo
> de peticiones pendientes es un cambio de comportamiento que `SMOKE` debe medir.

### 6.2 `PAT-01` en detalle — composición e inyección Java

| Pieza | Responsabilidad comprobable |
|---|---|
| `ClienteMain` | Solo inicia `ClienteApp`; ya no construye ni almacena servicios |
| `FabricaCliente` | Lee configuración, prepara H2 y construye transporte, DAOs, autenticación, fachada y runtime |
| `InterfazFachadaCliente` | Contrato agregado conservado por compatibilidad interna; la UI recibe puertos más pequeños |
| `FabricaControladores` | Mapea tipos FXML a constructores con puertos de login/chat, ejecutor, configuración y navegación inyectados |
| `ClienteApp` | En `init()` compone el runtime; en `stop()` lo cierra. Conserva stage/navegación como estado de instancia |
| `ClienteRuntime` | Cierra conexión, fachada y almacén local de forma idempotente |

### 6.3 `PAT-ARQ-JAVA` en detalle — contratos por consumidor

| Frontera | Responsabilidad comprobable |
|---|---|
| `InterfazInicioSesion` | Login usa solo autenticación y desconexión; la fachada agregada la implementa sin perder sus operaciones existentes |
| `InterfazChatCliente` | La vista recibe los casos de uso de conversación/presencia/sesión que utiliza, sin depender de la fachada agregada |
| `InterfazArchivosConversacion` | La presentación de adjuntos solicita historial/bytes sin depender del DTO de descarga del protocolo |
| `GestorAdjuntosChat` | Mantiene paginación remota y ciclo de carga, reintento, cancelación y descarga de medios |
| `InterfazMapeadorDatosServidor` | Puerto que traduce directorio e historial JSON a records del cliente; `MapeadorDatosServidor` concentra Gson en el adaptador |
| `InterfazEventosCliente` / `EventoCliente` | Presentación recibe presencia, cierre, acuses y mensajes como eventos propios del cliente, no como DTOs del protocolo |
| `Executor` | El contenedor de composición entrega el ejecutor acotado a los controladores; se elimina el `new Thread` ad hoc del login |

La arquitectura sigue usando los módulos y paquetes actuales. No se añadieron capas Maven ni se
alteraron el contrato TCP/JSON, el servidor o los clientes Python/.NET. En la continuación del
2026-10-06, `InterfazConexionRed` y `ReceptorMensajes` pasaron a `client-interfaces`; conexión,
autenticación y fachada intercambian resultados neutrales, y ArchUnit impide que los servicios y
puertos dependan de `common-protocol`. El DTO/codec queda en `componentes.conexion` y el mapeo JSON
en `componentes.protocolo`. El ViewModel asumió estado conversacional y el controlador dejó de
retener una referencia directa a la fachada; aun así, el controlador conserva coordinación JavaFX.
Por eso `PAT-ARQ-JAVA`/`PAT-VM` siguen parciales y no se declara Clean Architecture completa.

La compilación de `servidor-app` pasó en el mismo reactor Maven y no se modificó código de `server/`.
En la continuación del 2026-10-06, una ejecución limpia pasó 127 pruebas del cliente (0 fallos y
0 errores), incluida la carga de `login.fxml` y `chat.fxml` con la fábrica real y la reconexión
automática tras una caída. También pasó `mvn -q -pl server/servidor-app,client/client-app -am
-DskipTests compile`; no se modificó código de `server/`. Sigue pendiente un smoke visual conectado
para validar tema, adjuntos y recorridos completos de sesión/reconexión.

**Criterios medibles de `PAT-VM`** (para que no sea un subjective "quedó más limpio"):

| Métrica | Antes | Objetivo |
|---|---|---|
| `ChatController.java` / `chat_window.py` / `ChatWindow.xaml.cs` | 552 / 293 (214 sin blancos) + 31 módulos de `ui/` / 1 858 | ≤ 350 / ≤ 550 / ≤ 400 |
| Métodos privados que los tests tienen que llamar | 3 | **0** |
| Bindings reales en el cliente Java | 1 | ≥ 8 |

---

## 7. Contrato visual compartido

| ID | Tarea | Estado |
|---|---|---|
| **VIS-01** | **Fijar una única paleta por tema.** El tema oscuro coincide en los 3 (11 colores idénticos); **el claro no coincide en ninguno**: burbuja propia `#E4F3EC` / `#E1F5FE` / `#D9FDD3` y acento `#2456A6` / `#147D73` / defecto de WPF-UI. Deriva real, no diferencia de toolkit | ✅ **Cerrado 2026-10-07.** Fase 0.2 de [`PLAN_PARIDAD_CLIENTES.md`](PLAN_PARIDAD_CLIENTES.md): la paleta vive una vez en `contrato/tokens-diseno.json` y `contrato/generar_tokens.py` produce los 4 CSS, los 2 `.xaml` y los tokens de Python. El generador **falla** si un contraste baja de 4.5:1 (19 reglas, todas OK). Detectó y corrigió 4 tokens con contraste insuficiente y partió `sender.fg` en dos por el lavanda sobre la burbuja morada. Efecto colateral: `chat-claro.css` ya no puede quedarse sin selectores, porque sale de la misma plantilla que `chat.css` (le faltaban 3) |
| **VIS-02** | **Centralizar los 73 literales compartidos.** Caso grave: la **máquina de estados de mensaje** está escrita como `CASE` SQL **3 veces** con la misma precedencia — si se añade un estado y se olvida una pista, el historial de un cliente discrepa del de otro. Ojo: `Respuesta a {identidad}: "…"` es **visible en el protocolo** y no se puede cambiar sin tocar los 3 a la vez | 🟠 **Parcial 2026-10-08.** La parte grave (máquina de estados) está cerrada: `contrato/estado-mensaje.json` declara los 5 estados y sus 5 reglas de precedencia, y `contrato/verificar_estado_mensaje.py` lo contrasta con lo escrito en los 3 clientes. **Bug real encontrado:** `ERROR` no pintaba nada en Python ni en WPF (`ERROR` sí se escribía en la base y JavaFX sí lo mostraba) — corregido. Queda centralizar los literales de ventana y diálogo (~1 j) |

**Lo que NO entra:** i18n (resource files / `QTranslator` / `.resx`). Ninguno de los 3 tiene
mecanismo y el enunciado no lo pide. Lo que sí entra es centralizar literales (`VIS-02`), que es lo
que hace visible la deriva.

---

## 8. Diagramas

Fuentes Mermaid **renderizadas y revisadas** en `DIAGRAMAS/`; solo los mockups de DIAG-09 y la
reconciliación del legado siguen pendientes. Detalle y plan de consolidación en
[`DIAGRAMAS/README.md`](DIAGRAMAS/README.md).

| ID | Tarea | Estado | Aceptación |
|---|---|---|---|
| **DIAG-01** | **Casos de uso** — `casos/uc-cliente.mmd`, `uc-servidor.mmd`, `uc-sistema.mmd`. Antes **no existía ningún `usecaseDiagram`** en el repo | ✅ source | Render + revisión |
| **DIAG-02** | **Secuencia** — conexión/trama, Observer, Object Pool, tuberías. Antes solo había 3, en la carpeta vieja | ✅ source | Render + revisión |
| **DIAG-03** | **Actividad** — login, envío, archivo fragmentado, descarga, reconexión/cola. Antes no existía ningún árbol de decisión | ✅ source | Render + revisión |
| **DIAG-04** | **Estados** — mensaje, sesión, transferencia. Antes no existía ningún `stateDiagram`. `st-mensaje` pasa a ser la referencia contra la cual se comparan los 3 `CASE` SQL duplicados | ✅ source | Render + revisión |
| **DIAG-05** | **Despliegue** — 4 procesos en LAN, con la prohibición explícita de cliente → base del servidor | ✅ source | Render + revisión |
| **DIAG-06** | **Clases de Python y .NET** — no existía ninguno de los dos | ✅ source | Render + revisión |
| **DIAG-07** | **Consolidación** — migrar diagramas únicos a `DIAGRAMAS/` y revisar los legados | ✅ Fuentes/renderes canónicos migrados y catalogados en Lote G. Legados bajo `DIAGRAMAS/DIAGRAMAS_ENTREGA/` y `DIAGRAMAS/clases/01..09`; inventario y criterios en `DIAGRAMAS/README.md` §3 y `docs/archivo/README.md`. | Comparar los 10 `.mmd` legados y 5 mockups legado con sus equivalentes antes de archivarlos; registrar lo que no esté cubierto. |
| **DIAG-08** | **`render.ps1` sale siempre con código 0.** No hay forma de que CI detecte un render roto | ✅ Hecho (Lote G): contador `$script:fallos` + `exit 1`; verificado con roto temporal (exit 1) y sin rotos (exit 0). De paso, `Invoke-Nativo` evita que el stderr del shim npm aborte el bucle en PS 5.1 | Contar los `FALLO` y devolver exit code distinto de 0 |
| **DIAG-09** | **Mockups posteriores a las features**: estados de conexión/vacío, banner inline .NET y vista administrativa actual | ⬜ Por hacer; varias features existen, pero los diseños/renderes deben reflejar el código actual | Actualizar source `.excalidraw`, render y revisión visual. |
| **DIAG-10** | Toolchain Mermaid en el entorno documentado | ✅ Hecho 2026-10-06; `mmdc`/Node disponibles y `render.ps1 -Solo mermaid` se registró con exit 0 | Revalidar al cambiar de equipo/entorno. |

Los fuentes Mermaid canónicos tienen render en la carpeta `DIAGRAMAS/`; ver el conteo actual y las
excepciones históricas en `DIAGRAMAS/README.md` §0–§3. La falta actual de `DIAG-09` es de mockups
para nuevos estados de producto, no de instalación del renderizador Mermaid.

---

## 9. Seguridad (además de `SEC-05`)

| ID | Tarea | Estado | Nota |
|---|---|---|---|
| **SEC-05** | ~~`LISTAR_USUARIOS` sin autenticación~~ | ✅ **Cerrado** 2026-10-05 | §2. Guardas + fallo cerrado en el SPI + test parametrizado de 14 tipos + E2E |
| **SEC-06** | Guardar el surrogate de `SEC-05` | ✅ **Cerrado** 2026-10-05 | §2. Automatizado con `requiereAutenticacion()` por defecto `true` y 2 tests que fijan el conjunto abierto |
| SEG-01 | TLS y fail-fast en los tres clientes | 🟡 Implementado el 2026-10-08 | Validación temprana y almacén propio en cada cliente; falta registrar la matriz de handshake y revisar la operativa por formato (JKS en Java, PEM en Python/.NET). Se cierra junto con `PAT-05` tras aceptación |
| SEG-02 | Paridad `.webp` en validación de imagen | 🟡 **Casi cerrado (§5.6)** | El servidor ya acepta y decodifica WebP (`Imagenes.esImagenValida` + `imageio-webp`) y JavaFX/WPF lo envían como imagen sin renombrar. Falta solo **Python**, que sigue mandando `.webp` como archivo genérico, y la matriz de aceptación 3 clientes × (png/jpg/webp/webp corrupto) |
| SEG-03 | Rotación de secretos en producción | ✅ Hecho 2026-10-05 | El fail-fast se movió a `DataSourceAdapter.dataSource` porque en el `ApplicationRunner` corría **después** de que Spring creara el `DataSource`, y el síntoma era un `Access denied` que nunca nombraba la variable que faltaba |
| SEG-04 | Alerta NuGet `SQLitePCLRaw.lib.e_sqlite3 2.1.6` | 🔴 Riesgo | Versión fijada a propósito. Decidir antes de producción: actualizar o aceptar con justificación registrada |

---

## 10. Producto — requieren decisión, no se inician a medias

| ID | Tarea | Estado |
|---|---|---|
| **PROD-03** | **`difundir()` sin botón.** El servidor tiene difusión, pero es la del administrador; los clientes tienen el método y **ninguna UI**. Decidir: botón de difusión en los 3 clientes, o borrar el camino. Afecta a `MUERTE-6` | 🟠 Propuesto |
| PROD-01 | Notificación al minimizar | 🟠 Propuesto. Requiere decisión de UX + alcance por SO |
| PROD-02 | Salas temáticas | 🟠 Propuesto. Requiere modelo + enrutado + 3 UIs + cambio de protocolo |
| **PROD-04** | **`idioma=es` sin uso en los 3 clientes.** 0 lecturas de código; está en la lista blanca de `Config.save` y documentado en 2 README. Decidir: implementar i18n o quitar la clave | 🟠 Propuesto |
| **PROD-05** | **`pillow==10.4.0` sin un solo import** en el cliente Python | 🟠 Propuesto. Quitar o usar |

---

## 11. Fases ya cerradas (no reabrir sin motivo)

### DIST — distribución (P0 para entregar fuera de este PC) — ✅ 2026-10-05

- **DIST-01 ✅:** `docker-compose.prod.yml` + `.env.prod.example`, secretos por entorno,
  volúmenes de producción con nombre propio (`mensajeria-prod-*`).
- **DIST-02 ✅:** 4 instaladores nativos (`distribuir-servidor.cmd`, `dist-client.cmd`,
  `distribuir.cmd`, `dotnet publish`). Los 3 `.cmd` estaban rotos: Java usaba `package` donde
  hacía falta `install`, y buscaba los nativos JavaFX con un glob entrecomillado que nunca expande.
- **DIST-03 ✅:** checklist de actualización/backup en `DESPLIEGUE.md`, ejecutada extremo a extremo.

### PERF — rendimiento — ✅ 2026-10-05

- **PERF-01 ✅:** **95-98 tramas/s, ACK p50 ~690 ms, p95 ~1150 ms, 0 rechazos**, 100 conexiones
  vivas de 100. Base previa (50 sesiones × 2 textos, solo texto): 11,8 tramas/s.
- **PERF-02 ✅:** Hikari 10→30 y `filtros.hilos` 4→16 dan 79→96 tramas/s y p95 1524→1088 ms. El
  combo es lo que rinde; aislados dan 89 y 87.
- **PERF-03 ✅:** `poolEntrega` en difusión masiva — 99/99 destinatarios en 2-6 s con un cliente lento
  cuyo socket nunca lee.

---

## 12. Orden sugerido

1. **Cerrar el bug del servidor de subida duplicada** (§5.7, `archivos.ruta` admite la resubida).
2. **Revisión de seguridad previa a producción** — `SEC-05`/`SEC-06` ya están cerrados; atender los
   riesgos todavía abiertos de §9 (`SEG-01`, `SEG-02`, `SEG-04`) antes de publicar.
3. **`SMOKE-01` a `SMOKE-09`** — aceptación manual de lo ya construido, incluyendo la consola
   administrativa y el lote de clientes (§5.4 y §5.5). Registrar entorno y resultado en
   `GUIA_ACEPTACION.md`.
4. **`DIAG-09` y cierre de `DIAG-07`** — actualizar mockups de estados/avisos y conciliar el
   contenido de los diagramas legados; la conciliación está en `DIAGRAMAS/README.md` §3.
5. Continuar los refactors aún abiertos: `PAT-01` en Python/.NET, `PAT-VM`, `PAT-CMD`, `PAT-04`
   y `PAT-05`, según dependencias y riesgos descritos en `PLAN_PATRONES_UI.md`.
6. Completar tareas de interfaz y accesibilidad todavía abiertas (`UIX-*`, `VIS-*`) según los
   criterios vigentes de §5 y `GUIA_ACEPTACION.md`.
7. Resolver `MUERTE-6` y `PROD-*` solo cuando las decisiones de producto indicadas en §10 estén
   registradas.

---

## 13. Tooling de UI (ya instalado — no es backlog)

Herramientas presentes y verificadas en este entorno al 2026-10-06; al cambiar de computador hay
que volver a comprobar versiones y disponibilidad:

| Herramienta | Versión/fuente | Para qué | Prueba superada |
|---|---|---|---|
| skill `excalidraw-skill` | `mcp-excalidraw-server` (CLI `render` headless) | mockups `.excalidraw` + PNG | uso diario |
| skill `x9-diagrams` | repo `xonika9/agent-skills` | elegir formato, convertir, revisar | uso diario |
| skill `diagramas-profesionales` | local + referencias por toolkit | mockups, clases, ER y revamp | 9/9 checks de estilos |
| skill `pyside6-mcp` | `com55/pyside6-mcp` v4.0.11, MIT | ver/manejar la app Qt en vivo | launch→tree→find→click→stop |
| MCP `pyside6` | local `uvx pyside6-mcp@0.1.0` (uv 0.12.23) + `timeout: 60000` | 22 tools de Qt | ✅ 2026-10-06: estaba caído (`server unavailable` en el log por resolución PyPI en arranque frío + `PATH` con segmento roto `hotspot\\bin`; ambos corregidos en `opencode.jsonc`). Como `launch_app` ejecuta `uv run` (exige `pyproject.toml`), se añadió `client-python/pyproject.toml` mínimo + grupo `dev` con pytest. Verificado: `launch_app` → screenshot del login → `stop_app` |
| LSP opencode | `"lsp": true` en `opencode.jsonc` + `pyright` global por npm | diagnósticos al agente en `.java`/`.cs`/`.py` | ✅ 2026-10-06: antes `all LSPs are disabled`. `jdtls` (JDK 25) y `csharp` (.NET 10) cumplen requisitos; opencode descarga esos servidores en el primer uso (tarda minutos la primera vez) |
| Workspace VS Code | `universidad-mensajeria/.vscode/` (`settings.json`, `extensions.json`, `tasks.json`) | JDK 25 + Maven 3.9.11, intérprete al `.venv` de client-python, 5 tasks con los comandos de AGENTS.md | ✅ 2026-10-06: las 9 extensiones ya estaban instaladas; solo faltaba el workspace |
| MCP `netwright` | `dotnet tool install -g Netwright` 2.0.0, MIT | 15 tools de la app WPF | launch + screenshot del login real |
| MCP remoto `drawio` | `https://mcp.draw.io/mcp` | `search_shapes` | conectado |
| MCP remoto `qt-docs` | `https://qt-docs-mcp.qt.io/mcp` | señales/slots/props Qt | handshake + catálogo OK |
| MCP remoto `mslearn` | `https://learn.microsoft.com/api/mcp` | docs oficiales XAML/WPF | handshake + catálogo OK |

**Pendiente documental:** completar los mockups `DIAG-09` y revisar/reconciliar los ficheros fuente
legados `DIAG-07`. Las carpetas legadas se conservan; no se eliminó ningún archivo en esta revisión.
No falta instalar `mmdc`/Node en el entorno descrito. Evaluación pendiente:
reevaluar `dotnet/skills` si hace falta `msbuild` para instaladores nativos.

Nada instalado sin URL verificada ni prueba de funcionamiento. OJO técnico: no usar `{env:VAR}` en
`opencode.jsonc` con el CLI `mcp add` (lo expande y rompe el JSON); editar el archivo a mano.

---

## 14. Hechos de despliegue (resumen; detalle en `DESPLIEGUE.md`)

- Servidor: JAR Spring Boot ejecutable, Java 21, directorios persistentes `server/uploads/` y
  `server/logs/`.
- Cliente JavaFX: carpeta de distribución con el JAR, librerías Maven, nativos JavaFX, launcher y
  `client.properties`.
- Python: carpeta independiente con `requirements.txt` y esquema SQLite; cada PC crea su `.venv`.
- .NET: salida `dotnet publish`, preferentemente self-contained para Windows x64.
- Clientes → servidor: TCP configurable, predeterminado `5000`. **Solo el servidor** → base de
  datos. Los clientes nunca tocan la base del servidor.

---

## Índice documental

- [`docs/archivo/ANALISIS_TECNICO-historico.md`](docs/archivo/ANALISIS_TECNICO-historico.md) — auditoría técnica inicial (2026-10-05/06); hallazgos volcados al roadmap y planes de patrones/paridad.
- [`PLAN_PATRONES_UI.md`](PLAN_PATRONES_UI.md) — fases, orden y criterios de aceptación.
- [`PLAN_PARIDAD_CLIENTES.md`](PLAN_PARIDAD_CLIENTES.md) — qué debe ser igual en los 3 clientes.
- [`REQUERIMIENTOS.md`](REQUERIMIENTOS.md) — contrato funcional y no funcional.
- [`PLAN.md`](PLAN.md) — arquitectura y decisiones de diseño.
- [`docs/archivo/PLAN_MEJORAS-historico.md`](docs/archivo/PLAN_MEJORAS-historico.md) — diario de lo ya ejecutado (2026-10-04/05).
- [`docs/archivo/PLAN_CLIENTES-2026-10-historico.md`](docs/archivo/PLAN_CLIENTES-2026-10-historico.md) — diario del lote de clientes JavaFX/WPF (2026-10-10/11); sustituye a los ya retirados `PLAN3.MD` y `PLAN_AMBOS.md`.
- [`docs/archivo/PLAN_ARQUITECTURA_CLIENTE_JAVA-historico.md`](docs/archivo/PLAN_ARQUITECTURA_CLIENTE_JAVA-historico.md) — arquitectura del cliente Java (fases 1–4 verificadas; archivado el 2026-10-11; resta su smoke visual formal).
- [`DESPLIEGUE.md`](DESPLIEGUE.md) — mapa de despliegue.
- [`GUIA_ACEPTACION.md`](GUIA_ACEPTACION.md) — pruebas automatizadas y matriz manual.
- [`DIAGRAMAS/README.md`](DIAGRAMAS/README.md) — catálogo de diagramas.
- Guías por componente:
  [servidor](universidad-mensajeria/server/servidor-app/README.md) ·
  [JavaFX](universidad-mensajeria/client/README.md) ·
  [Python](universidad-mensajeria/client-python/README.md) ·
  [.NET/WPF](universidad-mensajeria/client-dotnet/README.md)
- Contratos: [TCP/JSON](universidad-mensajeria/common-protocol/PROTOCOLO.md) ·
  [persistencia local](universidad-mensajeria/common-protocol/HISTORIAL_LOCAL.md)
