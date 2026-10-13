# Plan de arquitectura del cliente Java

> **Documento histórico.** Retirado de la raíz el **2026-10-11**: sus fases 1–4 quedaron ejecutadas y
> verificadas (`ESTADO_ROADMAP.md` §6) y su pendiente de ArchUnit 4–5 se cerró el mismo día. Queda
> como **aceptación manual** el smoke visual formal, y el adelgazamiento de `ChatController` pasa a
> `PLAN_PATRONES_UI.md` §4 (`PAT-VM`). El estado vigente manda en el roadmap; esto no es normativa.

**Estado:** en curso; fases 1–4 ejecutadas y verificadas contra el código (2026-10-11); resta el
smoke visual formal del propio plan.  
**Alcance:** cliente JavaFX; el servidor se compila como verificación, pero no se modifica.  
**Objetivo:** mejorar límites arquitectónicos, SOLID y mantenibilidad sin alterar el comportamiento visible, la base local ni el contrato TCP/JSON.

## Estado verificado — 2026-10-11 (auditoría contra el código)

Mediciones reales de hoy (la última sección fechada era del 2026-10-07 y quedó desactualizada):

| Métrica | En el plan (10-07) | Real (10-11) |
|---|---|---|
| Pruebas del cliente | 174 | **286** (46 clases de test), 0 fallos (re-ejecución completa del 2026-10-11; la auditoría inicial del mismo día registró 283) |
| `ChatController` | 470 líneas | **597** (creció con el lote de octubre; ver pendientes) |
| `FachadaCliente` | 572 líneas | **529** (siguió bajando con las extracciones de formato/íconos) |
| Reglas ArchUnit | 6/6 | **6/6** (`ArquitecturaClienteJavaTest`) |
| Carga FXML automatizada | hecha | hecha (`FabricaControladoresTest`) |
| Recorridos de presentación (fase 1) | sin cubrir | **cubiertos** por `ChatRecorridosTest` (orden de recientes, envío de texto/imagen/archivo sin texto, paginación+búsqueda, acuses y marcado de leídos, expulsión/cierre remoto) + `LoginComportamientoTest` |

> **Criterio de conteo:** las líneas de esta sección van **sin contar líneas en blanco** (así miden
> también `PAT-VM` en `ESTADO_ROADMAP.md` §6 y `PLAN_PATRONES_UI.md` §4): `ChatController` 597 sin
> blancos / 660 totales; `FachadaCliente` 529 / 593; `ClienteApp` 129 / 143.

**Composición raíz (PAT-01, Java sí implementada):** `ClienteApp` (129 líneas) compone la
`FabricaCliente`; `FabricaControladores` inyecta `LoginController(inicioSesion, tareasFondo,
config, navegacion)` y `ChatController(new ChatViewModel(chat, tareasFondo), navegacion)`. El
controlador no ve la fachada concreta (ArchUnit lo prohíbe) y los colaboradores llegan por
constructor.

### Mapa de arquitectura vigente (lo que el plan no había registrado)

Todo el lote de octubre entró **después** de la última actualización del plan y no estaba mapeado:

- **Presentación (`presentacion/`)**: controladores FXML (`ChatController`, `LoginController`),
  ViewModel (`ChatViewModel` + `InterfazChatViewModel`), colaboradores extraídos
  (`RefrescadorContactosChat`, `PaginadorHistorialChat`, `BusquedaHistorialChat`,
  `SaltoBusquedaPendiente`, `GestorSalidaSesionChat`, `FlujoConversacion`), celdas
  (`MensajeCell`, `UsuarioCell`, `MiniaturasChat`, `VisorImagenChat` + `EscalaVisor` +
  `EstadoImagen`), adjuntos (`GestorAdjuntosChat`, `CargadorAdjuntoEntradaChat`,
  `AdjuntoPendiente`, `PlanEnvioAdjuntos`, `MontajeImagenes`), composición del mensaje
  (`TecladoCompositor`, `NavegadorCitaChat`), avisos (`EstadoAvisoInline` + `TipoAviso`),
  presentación pura (`FilasChat`, `FormatoFecha`, `FormatoEtapasFiltrado`, `FormatoRespuesta`,
  `FormatoUsuario`), tema y preferencias (`Tema`, `PreferenciaDivisorChat`), iconos por tipo
  (`IconoArchivo` + `resources/iconos/`, invariante 18).
- **Transversal puro (`transversal/utilerias/`)**: `TextoCliente`, `ValidacionCliente`,
  `ClienteConfig`, `ValidarTls` y los decodificadores de imagen de `FORMATOS-01`:
  `TranscodificadorImagen` (ImageIO + jsvg para SVG) y `DecodificadorMagickCliente` (im4java,
  plan B opcional para AVIF/HEIC; misma autodetección que `DecodificadorMagick` del servidor).
- **Fachada (`fachada/`)**: `FachadaCliente` (guarda de sesión, ciclo de vida, delegación) + 6
  servicios con interfaz propia (`CoordinadorMensajeriaCliente`, `CoordinadorHistorialCliente`,
  `ReintentadorPendientesCliente`, `SincronizadorDirectorioCliente`,
  `LectorHistorialRemotoCliente`, `ConstructorRecientes`) + ciclo de red
  (`ReceptorEntranteCliente`, `PublicadorEventosCliente`, `GestorReconexionSesion`) y
  `ReceptorMensajes`/`InterfazConexionRed` en `client-interfaces`.
- **Componentes (`componentes/`)**: `autenticacion`, `cache`, `conexion` (adaptador TCP),
  `historial` (H2 por cuenta + migraciones) y `protocolo` (mapeador de DTO) — los únicos que
  tocan Gson/JDBC, por ArchUnit.
- **Tests espejo**: cada pieza tiene su clase (`ChatRecorridosTest`, `FachadaClienteTest`,
  `ChatViewModelTest`, `MontajeImagenesTest`, `PlanEnvioAdjuntosTest`, `EstadoAvisoInlineTest`,
  `NavegadorCitaChatTest`, `TecladoCompositorTest`, `EscalaVisorTest`, `EstadoImagenTest`,
  `IconoArchivoTest`, `TranscodificadorImagenTest`, `TextoClientePreviewTest`, etc.), con
  `RedParaPruebas` como doble de red compartido.

**Re-verificación de cierre — 2026-10-11 (ejecutada, no solo leída).** Confirmación de esta
auditoría con las herramientas del repo:

- `contrato/verificar.py` — **9/9 pasos en verde** (tokens, máquina de estados, puertos, suite del
  contrato, iconos y las suites JavaFX/Python/.NET + build WPF). La suite JavaFX dentro del
  verificador pasó **286 pruebas en 46 clases, 0 fallos/errores** (289/47 al cierre del día, con
  `AvisosInlineChatTest`), ArchUnit 6/6 incluida.
- `mvn -q -pl server/servidor-app,client/client-app -am -DskipTests compile` — **pasa** tras el
  último lote y sin tocar `server/` (cierra el pendiente correspondiente de la lista de
  aceptación).
- Medición directa confirmada (sin blancos): `ChatController` 597 · `FachadaCliente` 529 ·
  `ClienteApp` 129; 46 clases de test.
- Pendientes comprobados: el **smoke visual formal** (necesita app + servidor reales) y el
  adelgazamiento continuo de `ChatController`, que pasa a `PLAN_PATRONES_UI.md` §4 (`PAT-VM`). La
  ampliación de las reglas ArchUnit 4–5 se cerró el mismo 2026-10-11: las reglas enumeran ya las 13
  clases (ArchUnit 6/6 en verde). `ChatController` bajó a 545 líneas sin blancos (extracciones de
  `AvisosInlineChat` y `ConfiguradorCeldasChat`; objetivo ≤ 350).

## Estado de ejecución — 2026-10-07 (histórico)

`ChatController` pasó de 1024 a 470 líneas y `FachadaCliente` de 868 a 572, sin cambiar
comportamiento observable (suite 174/174 en verde, ArchUnit 6/6):

- Presentación: `BusquedaHistorialChat` (búsqueda UIX-06 + salto diferido),
  `PaginadorHistorialChat` (paginación + anclaje UIX-16 + aviso de nuevos),
  `RefrescadorContactosChat` (filtro, selección, refresco estrangulado) con puerto
  `FlujoConversacion` hacia el controlador, `GestorSalidaSesionChat` (kick/salir/cierre
  remoto) y `CargadorAdjuntoEntradaChat` (elegir/previsualizar/quitar). El controlador
  conserva solo cableado FXML, composición, envío vía `EnviarMensajeComando` y avisos
  de conexión (PAT-03). Se eliminó la constante `TOPE_BUSQUEDA` sin uso y los imports
  y métodos muertos resultantes (RNF-16).
- Fachada: `ReintentadorPendientesCliente`, `SincronizadorDirectorioCliente`,
  `LectorHistorialRemotoCliente`, `ConstructorRecientes` (puro), `PublicadorEventosCliente`
  (Observer) y `ReceptorEntranteCliente` (antes clase interna); la fachada conserva guarda
  de sesión, ciclo de vida y delegación, con inyección total desde `FabricaCliente`
  (constructor de 11 parámetros con valores por defecto para compatibilidad).
- Cada extracción tiene interfaz propia (DIP) y pruebas propias: 47 casos nuevos
  (`HistorialChatTest`, `RefrescadorContactosTest`, `SalidaSesionChatTest`,
  `AdjuntoEntradaChatTest`, `ReintentadorPendientesTest`, `ConstructorRecientesTest`,
  `ReceptorEntranteTest`, `SincronizadorDirectorioTest`, `LectorHistorialRemotoTest`,
  `PublicadorEventosTest`); `RedParaPruebas` es el doble de red compartido.
  ArchUnit cubre nominalmente a los 5 colaboradores nuevos de presentación.

## Estado de ejecución — 2026-10-06 (histórico)

La fase 3 avanzó: `ChatController` ahora delega el acceso a la fachada al `ChatViewModel`, y el
ViewModel es dueño del estado de conversación compartido (adjunto, respuesta, offset, directorio,
recientes y propiedades observables). En esta ronda, `InterfazConexionRed` y `ReceptorMensajes`
pasaron a `client-interfaces`; las respuestas se adaptan a `RespuestaRed`/`TipoMensajeCliente` en
`ConexionCliente`, antes de llegar a autenticación y fachada. La fachada dejó de exponer al
observador legado basado en `Mensaje`; la UI mantiene el contrato de eventos neutrales. ArchUnit
ahora protege la ausencia de dependencias protocolarias en esos puertos y servicios.

Validación ejecutada: `mvn -q -pl client/client-app -am clean test` pasó (127 pruebas, sin fallos
ni errores), incluida la prueba de reconexión automática tras la caída del servidor. También pasó
`mvn -q -pl server/servidor-app,client/client-app -am -DskipTests compile`; no se modificó código
del servidor. Se validó la carga de ambos FXML con la fábrica real.

## Diagnóstico de partida (histórico)

- `client-interfaces` define puertos y records neutrales y su `pom.xml` no depende de `dto`. El contrato `InterfazConexionRed`/`ReceptorMensajes` también vive allí; `FachadaCliente`, autenticación y coordinadores usan `RespuestaRed`, mientras que los DTO del protocolo quedan en los paquetes adaptadores `componentes.conexion` y `componentes.protocolo`.
- La presentación usa puertos y no accede directamente al socket ni a la base local. `ChatController` ya delega al `ChatViewModel`, pero todavía coordina eventos, selección, flujo de UI, paginación y refresco.
- Línea base histórica: 83 pruebas pasaban antes de esta continuación.

## Principios e invariantes

1. Mantener los módulos Maven y paquetes existentes; no crear otro módulo como parte de este plan. Se añadirán paquetes internos solo donde separen responsabilidades y permitan comprobar la dirección de dependencias.
2. No cambiar el framing, JSON, comandos, nombres de campos ni semántica TCP; tampoco migrar la base local.
3. No modificar fuentes del servidor, Python ni .NET. El servidor solo se incluye en la compilación de aceptación.
4. Migrar con una fachada de compatibilidad: primero se añaden puertos neutrales y adaptadores; consumidores y firmas existentes se mantienen hasta que haya cobertura y todos los usos internos estén migrados.
5. Ninguna extracción debe cambiar orden de mensajes, acuses, paginación, reintentos, identidad de usuario, selección de conversación ni ciclo de vida de JavaFX.

## Fases de implementación

### 1. Caracterizar el comportamiento existente — ✅ completada

Conservar la suite base histórica de 83 pruebas y añadir regresiones para los recorridos que hoy no cubre la presentación:

- [x] inicio de sesión aceptado/rechazado y comunicación de errores sin filtrar excepciones técnicas (`LoginComportamientoTest`);
- [x] orden de conversaciones recientes, selección, presencia y mensajes entrantes/propios (`ChatRecorridosTest.ordenDeRecientesPriorizaMensajeMasReciente`);
- [x] envío de texto, imagen o archivo sin texto, acuses y prevención de duplicados al reintentar (`ChatRecorridosTest.envioDeTextoImagenYArchivoSinTexto`, `CoordinadorMensajeriaClienteTest`, `ReintentadorPendientesTest`);
- [x] paginación, búsqueda con resultados y sin resultados, cambio de contacto durante una consulta (`ChatRecorridosTest.paginacionYBusquedaEnHistorial`, `HistorialChatTest`);
- [x] reconexión, cierre voluntario, cierre remoto y expulsión de otras sesiones (`GestorReconexionSesionTest`, `ChatRecorridosTest.expulsionDeOtrasSesionesYCierreRemoto`, `SalidaSesionChatTest`);
- [x] carga de ambos FXML con `FabricaControladores` e inyección de sus dependencias (`FabricaControladoresTest`).

Estas pruebas fijan los resultados observables antes de mover responsabilidades.

### 2. Aislar los casos de uso de TCP/JSON — ✅ completada en el cliente Java

- [x] Definir en `client-interfaces` puertos y records de aplicación sin dependencia de `Mensaje`, Gson, JavaFX o JDBC.
- [x] Mantener `ConexionCliente` como adaptador TCP que traduce contratos neutrales a los DTO existentes de `common-protocol`; el JSON transmitido sigue idéntico.
- [x] Mover `InterfazConexionRed` y `ReceptorMensajes` a `client-interfaces` con contratos neutrales.
- [x] Migrar autenticación, coordinador de mensajería y fachada a resultados/eventos neutrales; retirar la suscripción protocolaria legada sin consumidores de producción.
- [x] `client-interfaces` no tiene dependencia Maven de `dto` ni referencias a DTO protocolarios.

### 3. Separar presentación, estado y casos de uso del chat — ✅ completada (con deuda activa)

- [x] Introducir `ChatViewModel` para estado observable y delegación de operaciones; no contiene acceso a socket, protocolo ni persistencia concretos (ArchUnit: regla 6).
- [x] El controlador usa el ViewModel como único acceso a los casos de uso/puerto de chat y no conserva una referencia directa a la fachada (ArchUnit: reglas 4–5).
- [x] Completar la extracción cohesiva de coordinación de eventos, contactos/presencia, historial/búsqueda y acciones del chat; mantener el controlador como adaptador JavaFX.
- [x] Extraer contactos/presencia (`RefrescadorContactosChat` + `FlujoConversacion`), historial/búsqueda (`PaginadorHistorialChat`, `BusquedaHistorialChat`, `SaltoBusquedaPendiente`), salida de sesión (`GestorSalidaSesionChat`) y adjunto de entrada (`CargadorAdjuntoEntradaChat`); el envío ya iba por comando (`EnviarMensajeComando`).
- [x] Inyectar ViewModel y casos de uso desde `FabricaControladores` y `ClienteApp`. Nombres `fx:id`, vistas FXML, navegación, tema y textos se conservan.
- ⚠️ **Deuda activa**: el lote de octubre devolvió cableado al controlador (montaje, avisos inline, navegación de cita, teclado) y `ChatController` volvió a 597 líneas. Sigue siendo adaptador JavaFX, pero es candidato a nueva extracción cohesiva si el plan quiere sostener su objetivo.

### 4. Reducir la fachada y conservar compatibilidad — ✅ completada

- [x] Extraer `CoordinadorMensajeriaCliente` y `CoordinadorHistorialCliente`.
- [x] Extraer el ciclo de sesión/reconexión a `GestorReconexionSesion`, incluida la custodia temporal de credenciales, el backoff, la reautenticación y la restauración posterior a conexión.
- [x] Extraer reintentos (`ReintentadorPendientesCliente`), directorio/presencia (`SincronizadorDirectorioCliente`), lectura remota y descargas (`LectorHistorialRemotoCliente`), construcción de recientes (`ConstructorRecientes`, puro), bus de eventos (`PublicadorEventosCliente`) y receptor entrante (`ReceptorEntranteCliente`). `FachadaCliente` está en 529 líneas como fachada fina con inyección total desde `FabricaCliente`.

### 5. Hacer cumplir las capas y cerrar documentación — 🟡 parcial

- [x] Pruebas ArchUnit (6 reglas) para los contratos neutrales, la presentación frente a persistencia/transporte y controladores/ViewModel frente a implementaciones concretas.
- [x] Sincronizar `PLAN_PATRONES_UI.md`, `ESTADO_ROADMAP.md`, `PLAN.md` y `AGENTS.md` con el estado parcial y las medidas observadas.
- [x] Prueba automatizada de carga de login/chat FXML con la fábrica real y toolkit JavaFX.
- [x] Añadir a las reglas ArchUnit 4–5 (que enumeran clases) a los colaboradores de presentación nacidos en octubre: `PlanEnvioAdjuntos`, `EstadoAvisoInline`, `TecladoCompositor`, `NavegadorCitaChat`, `MontajeImagenes`. **Hecho el 2026-10-11** (ArchUnit 6/6 verde); siguen cubiertos además por la regla de PAQUETE (3: sin persistencia ni adaptadores de red).
- [ ] Actualizar `PLAN_PATRONES_UI.md`/`ESTADO_ROADMAP.md` con las medidas finales cuando se cierre el smoke.
- [ ] Hacer y registrar el smoke visual del plan (ver «Lo que falta»).

## Lo que falta (pendientes reales, orden sugerido)

1. **Smoke visual formal del plan.** Hubo smokes reales contra el servidor el 10-10/10-11 (logins,
   envíos, formatos amplios; los H2 por cuenta se actualizaron), pero el plan exige su propio
   checklist asentado: carga FXML, tema claro/oscuro, historial, adjuntos, conversación reciente,
   caída/reinicio del servidor y cierre de sesión. Crear un
   `VERIFICACION_VISUAL_CHAT_PENDIENTE.md` propio de Java (como el de Python) y marcarlo.
2. ~~**ArchUnit 4–5**~~ **Hecho el 2026-10-11**: los 5 colaboradores nuevos ya están en las reglas
   enumeradas (ArchUnit 6/6 verde). Evita que un ayudante nuevo vuelva a tocar la fachada concreta
   sin que la suite lo diga.
3. **`ChatController` (597 líneas)**: extraer de forma cohesiva el cableado añadido en octubre —
   avisos inline (`EstadoAvisoInline` ya es clase propia; falta retirar su cableado del
   controlador) y la navegación de cita (`NavegadorCitaChat`) — para volver al objetivo del plan
   (adaptador JavaFX delgado).
4. **PAT-VM/PAT-CMD (roadmap §6)**: el ViewModel sigue parcial (el controlador coordina flujos de
   UI) y no hay `ICommand`/`Command` declarativo (el envío va por `EnviarMensajeComando` pero la
   habilitación del botón sigue imperativa). No bloquean el plan, pero son los límites que el
   doc prometía revisar.
5. **Regla ArchUnit 1 mantenible**: enumera tipos de `client-interfaces` uno a uno; cada record
   nuevo obliga a tocarla (ya pasó con los del lote). Alternativa: regla por paquete
   (`universidad.mensajeria.cliente.transversal.records..`).
6. **Documentos cruzados**: tras el smoke, actualizar `PLAN_PATRONES_UI.md` y `ESTADO_ROADMAP.md`
   (fila `PAT-ARQ-JAVA`) con las medidas finales.

## Criterios de aceptación

- ✅ La suite del cliente pasa: **286 pruebas, 0 fallos/errores** (re-ejecución completa del
  2026-10-11), incluida la regresión de caída y reconexión y los recorridos de presentación de la
  fase 1.
- ✅ Reglas ArchUnit existentes pasan; los DTO de protocolo están confinados en los adaptadores de
  conexión/protocolo.
- ✅ TCP/JSON y esquema local no cambian; `server/`, Python y .NET no reciben cambios de este plan.
- ✅ El estado conversacional está en `ChatViewModel`; `ChatController` (597 líneas) es adaptador
  JavaFX y `FachadaCliente` (529 líneas) es guarda de sesión, ciclo de vida y delegación.
- [X] Smoke visual conectado a un servidor real, registrado (pendiente nº 1 de «Lo que falta»).
- [x] `mvn -q -pl server/servidor-app,client/client-app -am -DskipTests compile` después de la
  última extracción: **pasa** (re-ejecutado el 2026-10-11; no se tocó `server/`).

## Riesgos y controles

| Riesgo | Control |
|---|---|
| Cambiar accidentalmente eventos, acuses u orden de mensajes | Caracterizar los flujos antes de extraer; ejecutar pruebas de fachada y regresión después de cada fase |
| Desincronizar el adaptador neutral del protocolo | Pruebas de contrato que comparen cada operación con el JSON/trama existente; no editar `common-protocol` |
| Alterar ciclo JavaFX/FXML al introducir ViewModel | Mantener `fx:id` y controladores FXML; prueba de carga con la fábrica de producción y smoke visual |
| Romper consumidores de la fachada agregada | Migración aditiva; adaptador legado hasta demostrar cero consumidores internos |
| Refactor grande difícil de revisar | Entregar y verificar una fase a la vez; no mezclar cambios de UI funcional ni servidor |
| El controlador vuelve a engordar con cada lote de UI | Vigilar el contador de líneas en cada auditoría; extraer de forma cohesiva antes de superar el pico histórico (1024) |

## Supuestos

- El contrato TCP/JSON es inmutable durante este trabajo; solo cambia el mapeo dentro del cliente Java.
- El alcance es arquitectura y mantenibilidad del cliente Java. Las correcciones funcionales visuales de otras solicitudes no se amplían aquí.
- Las cifras de línea base (83 pruebas, 2026-10-06) quedan como histórico; las medidas vigentes son las de «Estado verificado — 2026-10-11».
