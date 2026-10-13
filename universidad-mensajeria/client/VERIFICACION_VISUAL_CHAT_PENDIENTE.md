# Verificación manual pendiente: chat JavaFX

Cierre del smoke visual que `PLAN_ARQUITECTURA_CLIENTE_JAVA.md` (archivado en
`docs/archivo/PLAN_ARQUITECTURA_CLIENTE_JAVA-historico.md`) dejó pendiente. Ejecuta el cliente
desde `universidad-mensajeria/` (el `client.properties` se resuelve relativo al directorio de
trabajo o a la carpeta del ejecutable):

```powershell
mvn -pl client/client-app javafx:run
```

o con el empaquetado de `client/dist-client.cmd`.

## Preparación

- [ ] Servidor arriba (TCP 5000) y MySQL accesible; dos cuentas de prueba (`usuarios_iniciales.csv`).
- [ ] Dos instancias (dos sesiones de la misma cuenta o dos cuentas) para probar entrega en vivo.

## Inspección visual de login y tema

- [ ] Login: campos, combo de servidor/puerto y botón con contraste en **tema oscuro y claro**
  (los 4 CSS vienen de `contrato/generar_tokens.py`; si un color no coincide, regenerar, no editar).
- [ ] Cerrar la ventana de login pide confirmación; cancelar la mantiene abierta.

## Chat: fondo, burbujas y estados

- [ ] El **fondo ilustrado** (`fondos/fondo_chat_oscuro.jpg` / `fondo_chat_claro.jpg`) aparece detrás
  de las burbujas en cada tema y tras cargar historial y cambiar de conversación (los CSS lo aplican
  con `cover`; las celdas van transparentes).
- [ ] Burbujas propia/ajena a la derecha/izquierda, separador de día («Hoy», «Ayer»), racha sin
  remitente repetido y ticks `✓ Enviado`, `✓✓ Entregado`, `✓✓` azul **Leído**, `! No enviado`.
- [ ] El preview del sidebar usa `origen = yo` (no pinta «Tú:» en lo recibido) y «Sin mensajes aún»
  cuando corresponde.

## Adjuntos e imágenes (FORMATOS-01/02)

- [ ] Enviar texto con pie (caption), imagen con pie en la **misma burbuja**, y archivo: la burbuja
  muestra **icono del tipo a 48 DIP** (pdf/xlsx/csv/json/docs de `resources/iconos/`) **sin** el
  `📎` duplicado; extensiones sin catálogo conservan el `📎`.
- [ ] Selección/arrastre múltiple con bandeja (tope 5) y montaje: 2+ imágenes componen el montaje y
  viajan como **una sola burbuja**; si el montaje falla, cada imagen sale por su cuenta.
- [ ] Imágenes > 12 MB se **rechazan con el mensaje del límite** («la imagen supera el límite de
  envío directo de 12 MB»); archivos hasta 50 MB viajan fragmentados.
- [ ] **WebP** se envía tal cual y se pinta (imageio-webp); **AVIF/HEIC/SVG** se envían como imagen,
  el servidor los filtra, y el cliente los pinta con jsvg/ImageMagick — sin ImageMagick instalado
  muestran «Formato no soportado por este cliente» con su botón de guardar.
- [ ] El **visor** abre el original a resolución real con zoom (no la miniatura); en AVIF/HEIC/SVG
  abre el PNG convertido.

## Interacciones y estados de sesión

- [ ] Responder citando (doble clic, o flechas con `NavegadorCitaChat`); el prefijo viaja como
  `Respuesta a Nombre [CÓDIGO]: "…"`.
- [ ] `Enter` envía y `Shift+Enter` salta de línea (`TecladoCompositor`).
- [ ] Cargar mensajes anteriores conserva el punto de lectura; «↓ Nuevos» sin arrastre.
- [ ] Caída del servidor: banner y reconexión automática sin duplicar mensajes; **KICK otras** cierra
  las otras sesiones; «Salir» y «Salir en todos» vuelven al login.
- [ ] Los avisos inline (`EstadoAvisoInline`) se retiran solos y se pueden cerrar.

## Evidencia

- [ ] Capturas de login (2 temas), chat con fondo (2 temas), visor, adjunto con icono y estados.
- [ ] Anotar cliente/tema/resultado de cada caso que falle y abrir registro en `ESTADO_ROADMAP.md`.
