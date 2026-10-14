# Guion de capturas — paridad visual (Fase 4 del plan de paridad)

12 escenarios × 2 temas × 3 clientes = 72 PNG, más los estados que solo aparecen
por acción (~90 en total). Destino: `DIAGRAMAS/capturas/<escenario>/<tema>/<cliente>.png`.

Verificado 2026-10-14: la vía de captura funciona (K1 login Python oscuro
capturado con `pyside6-mcp`; WPF sale por `netwright`; JavaFX necesita flag
`--capturar` con Monocle headless, aún sin implementar).

## Escenarios

| # | Escenario | Qué demuestra | Notas de montaje |
|---|---|---|---|
| K1 | Login vacío | tarjeta, campo código, botón | Sin escribir nada |
| K2 | Login con error `✖` | banner de error | Credencial mala |
| K3 | Login con aviso `⏳` | banner ámbar | Cierre por inactividad (o aviso pendiente) |
| K4 | Login conectando | spinner, botón deshabilitado | Servidor parado o lento |
| K5 | Chat sin contactos | estado vacío `Sin contactos todavía.` | Cuenta nueva sin directorio |
| K6 | Búsqueda sin resultados | `Sin resultados para esta búsqueda.` | Buscar texto imposible |
| K7 | Conversación corta | burbujas propia/derecha y recibida/izquierda | 3–5 mensajes, con cita |
| K8 | Conversación larga (100+ mensajes) | agrupado, cabecera de día, scroll | Semilla generada, no a mano |
| K9 | Mensaje con imagen | miniatura, caption, estados | PNG + pie en la misma burbuja |
| K10 | Mensaje con archivo | `📎`, bytes, botón `⬇ Descargar` | PDF con icono a 48 DIP |
| K11 | Cita activa | barra `Respondiendo a`, bloque citado en burbuja | Doble clic → responder |
| K12 | Banner offline | ámbar `⛔ Sin conexión…` | Matar el servidor con el chat abierto |

## Mecanismos de captura

| Cliente | Mecanismo |
|---|---|
| Python | MCP `pyside6` (`launch_app` → operar → `screenshot` → `stop_app`). Sin código nuevo. |
| .NET | MCP `netwright` (`desktop_app` launch + `desktop_screenshot`). Sin código nuevo. |
| JavaFX | Flag `--capturar=DIR` en `ClienteMain` con Monocle headless + `node.snapshot` (**sin implementar**). |

## Comparación (`contrato/comparar_capturas.py`, sin implementar)

1. Alinea por tamaño; si difiere, rojo primero (falta un selector).
2. Diferencia pixel a pixel por zona (burbuja, sidebar, composer, banner).
3. Muestrea el color donde debería estar cada token y lo compara con
   `tokens-diseno.json` (detecta tokens sin regenerar).
4. Contrastes SMOKE-07 (`.chat-list-preview` en claro, placeholder, fecha) ≥ 4.5:1.
5. Salida `DIAGRAMAS/capturas/informe.html` (3 columnas + delta por zona).

**Umbral:** ≤ 2 % de píxeles distintos por escenario y tema, 0 tokens fuera de paleta.
Lo que se salga del umbral se registra como tarea, no se explica.
