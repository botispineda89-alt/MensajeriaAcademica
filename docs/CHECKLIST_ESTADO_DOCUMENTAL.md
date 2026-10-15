# Checklist de estado documental

Revisión de los documentos principales contra el estado conocido al **2026-10-14**. El estado
operativo vigente se mantiene en [`ESTADO_ROADMAP.md`](ESTADO_ROADMAP.md). No se reejecutaron los
smoke ni las suites: se respeta la confirmación del usuario y se citan los registros ya existentes.

## Trabajo completado en esta actualización

- [x] `REQUERIMIENTOS.md`: se reconciliaron formatos de imagen (incluidos AVIF/HEIC/HEIF/SVG),
  persistencia local por cliente (H2 para JavaFX, SQLite para Python/.NET) y cierre de `RF-S60`.
- [x] `PLAN.md`: se identificó la tabla de trazabilidad como línea base histórica; se actualizaron
  estados de las fases 10, 15, 16 y 17, el backlog de fase 11 y los enlaces a auditorías archivadas.
- [x] `GUIA_ACEPTACION.md`: se marcaron SMOKE-01 a SMOKE-11 como aprobados conforme a la
  confirmación del usuario; se actualizó A→A, el registro del resultado y el total .NET al corte
  fechado de §1 del roadmap. No se inventaron hora, SO, IDs ni capturas.
- [x] `ESTADO_ROADMAP.md`: se retiró la contradicción que mantenía los smokes como pendientes,
  incluida la tabla de verificación manual y los hitos 5.9/5.27.
- [x] `README.md`: se actualizó el resumen y la evidencia fechada para que concuerden con el roadmap.
- [x] Se trasladaron las listas de verificación visual de JavaFX y Python a `docs/verificacion/`;
  se conservaron sus casillas pendientes, pues contienen comprobaciones complementarias cuyo detalle
  no está registrado individualmente.
- [x] Se actualizaron los enlaces de esas listas en el README de Python, el índice de archivo y el
  roadmap.

## Pendientes documentales reales

- [x] [`REQUERIMIENTOS.md`](REQUERIMIENTOS.md): decisión de producto 2026-10-15 —
  `RF-S28` y `RF-C16` quedan **fuera de alcance** (filas conservadas como registro,
  fila 23 del control de cambios) y la consola se renumera a **`RF-S61`/`RF-S62`**;
  `RF-S34`/`RF-S35` quedan solo para autorización. Referencias actualizadas en `PLAN.md`
  (trazabilidad, fase 11) y nota de numeración de §9 cerrada.
- [x] [`PLAN.md`](PLAN.md): reconciliación completa contra el roadmap el 2026-10-15 —
  residuos stale de smoke corregidos, `RF-S28`/`RF-C16` excluidos, consola a
  `RF-S61`/`RF-S62`, criterio §13-33 calificado como opcional de usuario (§16 del roadmap).
- [x] [`GUIA_ACEPTACION.md`](GUIA_ACEPTACION.md): cruce completo guía↔roadmap sin
  contradicciones; criterios de § Historial confirmados por el usuario el 2026-10-15
  y `UIX-27` hecho, probado y confirmado visualmente.
- [x] Checklists complementarios cerrados el 2026-10-15 por confirmación del usuario
  (mismo estándar que SMOKE-01–11, sin capturas anexas): JavaFX 19/19 y Python 23/23 en
  `docs/verificacion/VERIFICACION_VISUAL_CHAT_JAVAFX.md` y
  `docs/verificacion/VERIFICACION_VISUAL_CHAT_PYTHON.md`. Corrección lateral: el cierre
  de Python citaba 257 pruebas; se actualizó a 321 (5 omitidas), registro vigente.
- [x] Conteo de pruebas .NET cerrado el 2026-10-15: `dotnet test` → **391/391, 0 fallos,
  0 omitidos** (net10.0; incluye el test nuevo de UIX-27 `RefrescarTemaNotificaLosTresPincelesSinRecargar`).
  §1, §5.26, guía, README y `AGENTS.md` registran 391/391.
- [ ] [`ESTADO_ROADMAP.md`](ESTADO_ROADMAP.md): suite general 2026-10-15 en máquina de desarrollo —
  **116 casos, 115 en verde**. Resta un fallo de umbral en `CargaMixtaTest` (p95 5540–6095 ms > 5000 ms;
  entorno, no saturación). El servidor de producción irá en máquina muy superior: **repetir la
  medición allí**; si el p95 < 5000 ms, verde sin tocar código.

## Criterio de ubicación (traslado 2026-10-15 por decisión de producto)

- `docs/REQUERIMIENTOS.md`, `docs/PLAN.md`, `docs/GUIA_ACEPTACION.md`, `docs/ESTADO_ROADMAP.md`
  y `docs/AGENTS.md`: movidos desde la raíz con `git mv` y enlaces reescritos
  (verificación final: **0 enlaces rotos** en todo el repo con checker propio).
- Producción 2026-10-15: creado `universidad-mensajeria/server/distribuir-servidor.cmd`
  (faltaba: `empaquetar-todo.cmd` lo invocaba en `dist\`, ignorado por git) y reubicados
  sus 6 llamadores; `empaquetar-todo.cmd` 4/4 en verde con las 4 carpetas en `dist/`.
- Documentos históricos archivados y checklists de verificación trasladados están en `docs/`.
- Requisitos, plan y guía de aceptación viven en `docs/` tras el traslado 2026-10-15.
- `docs/ESTADO_ROADMAP.md` es el tablero vivo, no un documento terminado.
- Las especificaciones de protocolo y persistencia que son parte del contrato de `common-protocol`
  permanecen junto al módulo que las define.

## Verificación 2026-10-15

Se recorrieron los 6 pendientes contra la evidencia citada. Correcciones aplicadas:

- `GUIA_ACEPTACION.md` § cambio de tema: `UIX-26` figuraba como pendiente en la guía
  mientras `ESTADO_ROADMAP.md` §5.3 lo registra ✅ Hecho; se alineó la guía con el
  roadmap. `UIX-27` sigue ⬜ Por hacer en ambos. Cruce completo guía↔roadmap
  (SMOKE-01–11, suites, SEC-05, PAT-03, VIS-01, UIX-06/07/15/29/00f): sin más
  contradicciones.
- `PLAN.md`: reconciliación de fases contra el roadmap. Dos residuos stale corregidos
  con evidencia: el "pendiente de smoke en seis direcciones" (`PLAN.md:1118-1119`) y el
  criterio 30 (§13) contradecían la Fase 16 ✅ y SMOKE-01 aprobado 2026-10-14; la parte
  de reinicio/dos procesos Java queda como "requiere verificación" (criterio en
  `GUIA_ACEPTACION.md` § Historial sin aprobación registrada) en backlog 11.
  `RF-S28`/`RF-C16` como Extra ⏳ se conservan: son extras reales sin entrada en el
  roadmap, no un error de redacción.
- Conteo .NET **cerrado**: `dotnet test` 2026-10-15 → 391/391 (con el test de UIX-27).
- **UIX-27 implementado** en WPF (`MensajeVM.RefrescarTema()` + `ChatWindow.Vista.cs`: el
  cambio de tema repinta pinceles sin recargar el historial) con test en
  `FondoChatWpfTests` (verificación por fuente: el proyecto de tests es net10.0 sin
  UseWPF y no compila tipos WPF, igual que `ArquitecturaTests`). Falta la
  confirmación visual con la app corriendo.
- Roadmap §6 reconciliado con §15: `PAT-VM` (220/246+11 parciales, suites en verde),
  `PAT-CMD` (resuelto sin `ICommand`), `PAT-04` (`IInterfazHistorialLocal` + test) y
  `PAT-05` (fail-fast en los 3 + matriz) pasan a ✅ con evidencia verificada en código;
  §12.5 conserva solo `PAT-01` como refactor abierto.
- `PLAN.md` §13-33 calificado como opcional de usuario (remite a roadmap §16).
- `GUIA_ACEPTACION.md` § Historial: dos procesos Java y cierre/reapertura verificados
  por el usuario el 2026-10-15.

Pendiente único (de entorno, no documental):

- Servidor general 115/116: el umbral p95 de `CargaMixtaTest` no pasa en esta máquina
  (5540–6095 ms vs 5000 ms; pool ocioso 3/100). `UIX-27` hecho, probado (391/391) y
  confirmado visualmente el 2026-10-15.
