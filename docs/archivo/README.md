# Archivo histórico

Documentos conservados solo por trazabilidad. No son normativa vigente.

| Archivo | Qué era | Fuente de verdad actual |
|---|---|---|
| `REQ-original.md` | Brief original del enunciado | `../../REQUERIMIENTOS.md` |
| `PLAN_MEJORAS-historico.md` | Diario de ejecución 2026-10-04/05 | `../../ESTADO_ROADMAP.md` |
| `PLAN_CLIENTES-2026-10-historico.md` | Diario del lote de clientes JavaFX/WPF de 2026-10-10/11 (envío múltiple en WPF, seis fallos de JavaFX y .NET, WebP) | `../../ESTADO_ROADMAP.md` |
| `PLAN_ARQUITECTURA_CLIENTE_JAVA-historico.md` | Plan y auditoría de arquitectura del cliente Java (fases 1–4 verificadas el 2026-10-11: puertos, ViewModel, fachada fina, ArchUnit; framing data-driven §5.14) | `../../ESTADO_ROADMAP.md` §6 (`PAT-ARQ-JAVA`) y `PLAN_PATRONES_UI-historico.md` §4 |
| `PLAN_PATRONES_UI-historico.md` | Plan de patrones UI 2026-10-06/14 (fases A–G cerradas, §9.1 6/6; incluye reglas de borrado B1–B6 y tabla de tamaños §4.2) | `../../ESTADO_ROADMAP.md` §5.16–5.24 y §6 |
| `PLAN_PARIDAD_CLIENTES-historico.md` | Plan de paridad total de los 3 clientes (fases 0–3 cerradas el 2026-10-14, verificación 10/10 en verde; incluye catálogo de puertos, formatos de imagen WebP/AVIF/HEIC/SVG, captions, envíos fragmentados y citas estilo Telegram Web) | `../../ESTADO_ROADMAP.md` §1 y §15, `../GUIA_ACEPTACION.md` |
| `ANALISIS_TECNICO-historico.md` | Auditoría técnica inicial (2026-10-05/06): seguridad, código muerto, bugs UI, patrones, contrato visual, diagramas. Hallazgos volcados a `ESTADO_ROADMAP.md`, `PLAN_PATRONES_UI-historico.md`, `PLAN_PARIDAD_CLIENTES-historico.md` | `../../ESTADO_ROADMAP.md` (tabla §2–§6), `../GUIA_ACEPTACION.md` |

Checklists de verificación visual complementarios (su estado y limitaciones están en cada archivo):

- [`../verificacion/VERIFICACION_VISUAL_CHAT_PYTHON.md`](../verificacion/VERIFICACION_VISUAL_CHAT_PYTHON.md)
- [`../verificacion/VERIFICACION_VISUAL_CHAT_JAVAFX.md`](../verificacion/VERIFICACION_VISUAL_CHAT_JAVAFX.md)

Retirados de la raíz el **2026-10-10**, ya ejecutados; su contenido único está en
`PLAN_CLIENTES-2026-10-historico.md` y su estado vigente en `../../ESTADO_ROADMAP.md` §5:

- `PLAN3.MD` — envío múltiple de imágenes y archivos en WPF (4 decisiones de diseño, 11 clases
  nuevas, deuda de paridad asumida).
- `PLAN_AMBOS.md` — seis errores de los clientes JavaFX y .NET, con las cuatro correcciones que
  destapó la prueba en pantalla y los dos bugs de raíz de la ronda 4.

Eliminados el 2026-10-07 por fusión, con su contenido único preservado:

- `INSTRUCCIONES_4_PCS.md` → fusionada en `../../DESPLIEGUE.md` § Reparto en 4 PCs
  (matriz fija PC1–PC4, pasos por PC sin rutas absolutas, prueba cruzada).
  Se corrigieron sus defectos: ruta personal `c:\Users\AlexP\Downloads\...`,
  `<--` dentro de bloques `.properties`, firewall sin `RemoteAddress LocalSubnet`,
  `docker compose` sin carpeta base y `pip` en vez de `python -m pip`.
- `REVISION_DOCUMENTAL.md` → su tabla fuente-de-verdad vive en `../../README.md`;
  su plan de diagramas vive en `../../DIAGRAMAS/README.md` §3.
- `BUSQUEDA_DOCUMENTACION.md` → nota de trabajo interna del 2026-10-06; sus
  correcciones se aplicaron y la nota se retiró.
- `ComponentesServidor.png` → huérfano sin referencias; lo cubre
  `../../DIAGRAMAS/clases/`.
