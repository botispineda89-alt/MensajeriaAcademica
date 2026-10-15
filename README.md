# Mensajería académica

Sistema de escritorio cliente/servidor para mensajería académica. Un servidor Java recibe conexiones TCP y procesa mensajes JSON; tres clientes independientes usan JavaFX, PySide6 y WPF. Solo el servidor accede a MySQL; los clientes mantienen su historial local en H2 o SQLite.

> Documentación revisada el **2026-10-14** contra el código y la evidencia vigente. Para el estado
> operativo, consulta [`ESTADO_ROADMAP.md`](docs/ESTADO_ROADMAP.md); para criterios manuales, consulta
> [`GUIA_ACEPTACION.md`](docs/GUIA_ACEPTACION.md). Este README es la entrada rápida y no sustituye la
> especificación vigente ni los registros históricos.

## Componentes

| Componente | Proyecto | Guía |
|---|---|---|
| Servidor Java 21+ / Spring Boot | `universidad-mensajeria/server/servidor-app` | [Operación del servidor](universidad-mensajeria/server/servidor-app/README.md) |
| Cliente JavaFX | `universidad-mensajeria/client/client-app` | [Cliente Java](universidad-mensajeria/client/README.md) |
| Cliente Python / PySide6 | `universidad-mensajeria/client-python` | [Cliente Python](universidad-mensajeria/client-python/README.md) |
| Cliente .NET / WPF | `universidad-mensajeria/client-dotnet` | [Cliente WPF](universidad-mensajeria/client-dotnet/README.md) |

El contrato de red es TCP con una cabecera big-endian de 4 bytes y un cuerpo JSON UTF-8. Los clientes remotos deben conectarse a la IP o DNS del servidor, no a `localhost`. Las distribuciones se generan por separado desde el monorepo.

## Preparación y desarrollo

- Para preparar Windows sin permisos de administrador, sigue [`INSTALACION.md`](INSTALACION.md) y [`AUTOMATIZACION_ENTORNO.md`](AUTOMATIZACION_ENTORNO.md).
- Para compilar Java, inicia MySQL de desarrollo y ejecuta los comandos de [universidad-mensajeria/README.md](universidad-mensajeria/README.md).
- Para pruebas de aceptación entre los tres clientes, usa [`GUIA_ACEPTACION.md`](docs/GUIA_ACEPTACION.md).
- Para desplegar y respaldar servicios, consulta [`DESPLIEGUE.md`](DESPLIEGUE.md).

## Documentación del proyecto

| Documento | Propósito |
|---|---|
| [`ESTADO_ROADMAP.md`](docs/ESTADO_ROADMAP.md) | Estado vigente, pendientes conocidos y evidencia con fecha. |
| [`REQUERIMIENTOS.md`](docs/REQUERIMIENTOS.md) | Requisitos vigentes y alcance funcional, reconciliados al 2026-10-14. |
| [`PLAN.md`](docs/PLAN.md) | Arquitectura, decisiones técnicas y trazabilidad de la línea base. |
| [`docs/CHECKLIST_ESTADO_DOCUMENTAL.md`](docs/CHECKLIST_ESTADO_DOCUMENTAL.md) | Sincronización documental realizada y asuntos que requieren evidencia adicional. |
| [`docs/archivo/ANALISIS_TECNICO-historico.md`](docs/archivo/ANALISIS_TECNICO-historico.md) | Auditoría histórica con ubicación de hallazgos; consultar el roadmap para saber si siguen abiertos. |
| [`docs/archivo/PLAN_PATRONES_UI-historico.md`](docs/archivo/PLAN_PATRONES_UI-historico.md) | Plan e historial de mejoras de las interfaces (cerrado el 2026-10-14). |
| [`docs/archivo/PLAN_PARIDAD_CLIENTES-historico.md`](docs/archivo/PLAN_PARIDAD_CLIENTES-historico.md) | Plan de paridad e historial de los tres clientes (cerrado el 2026-10-14). |
| [`docs/archivo/README.md`](docs/archivo/README.md) | Qué se archivó/fusionó y dónde está lo único que tenía. |
| [`DIAGRAMAS/README.md`](DIAGRAMAS/README.md) | Índice de fuentes/renderes de diagramas (incluye legados migrados). |


## Estado resumido

- `SEC-05`/`SEC-06` — autorización de comandos: cerrados y cubiertos por pruebas; el detalle del hallazgo se conserva como historial en la auditoría.
- Vista administrativa JavaFX: incluye pestaña reutilizable de ficha de usuario, métricas de mensajes, sesiones/IP disponibles, detalle de texto/imagen, y pestañas Estado, Conectados, Usuarios, Mensajes, Informes, Logs, Pool y Difundir. La consola JLine conserva los comandos e incorpora jerarquía visual.
- Clientes JavaFX y WPF: envío de varias imágenes en una sola burbuja (montaje), texto como pie de la imagen o del archivo en la misma burbuja, botón de guardar en imágenes, aviso inline con severidad y cierre automático, visor a resolución real y lista de contactos al día. JavaFX ofrece selección/arrastre múltiple y bandeja de hasta 5 adjuntos; WPF admite lotes de hasta 12 con progreso por bytes y cancelación. Python admite lotes de hasta 12 con vista previa compuesta local (cada imagen original se sube por separado). **Los tres clientes** tienen ahora barra de progreso por bytes y cancelación del envío, y lo cancelado vuelve a la bandeja.
- Imágenes WebP —y los formatos amplios AVIF/HEIC/SVG (`FORMATOS-01`)—: el servidor los acepta y los procesa (ImageMagick/`imageio-webp`); los tres clientes los envían tal cual como `MENSAJE_IMAGEN` y convierten **solo para pintar**.
- La subida duplicada de una imagen quedó cerrada e idempotente el **2026-10-14** (`ESTADO_ROADMAP.md` §5.7). La validación manual del adjunto de 20 MiB confirmó A→B, B→A y A→A byte a byte; los SMOKE-01 a SMOKE-11 se reportan aprobados por el usuario en el corte del 2026-10-14.
- Suites y contrato verificados el **2026-10-14** (`contrato/verificar.py`, **10/10 pasos, exit 0**): JavaFX **305/305**, Python **321 pruebas (5 omitidas)**, .NET **391/391** (re-verificado 2026-10-15 con el test de UIX-27; ver [`CHECKLIST_ESTADO_DOCUMENTAL.md`](docs/CHECKLIST_ESTADO_DOCUMENTAL.md)).
- La automatización de preparación de entorno instala/verifica herramientas del usuario sin permisos de administrador, documentada en [`AUTOMATIZACION_ENTORNO.md`](AUTOMATIZACION_ENTORNO.md).

## Advertencia JavaFX `sun.misc.Unsafe`

La distribución actual del servidor usa JavaFX Graphics **21.0.6**. En JDK recientes el renderizador `com.sun.marlin.OffHeapArray` puede emitir una advertencia al llamar `Unsafe.allocateMemory`; el stack trace señala el JAR de JavaFX, no una llamada de la lógica de mensajería. La guía del servidor explica el alcance y las alternativas de runtime. El aviso no significa por sí solo que el servidor haya fallado.

Consulta [`docs/archivo/README.md`](docs/archivo/README.md) para ver qué se archivó o fusionó (2026-10-07) y qué planes ya ejecutados se retiraron de la raíz (2026-10-10), y dónde está el material único preservado.
