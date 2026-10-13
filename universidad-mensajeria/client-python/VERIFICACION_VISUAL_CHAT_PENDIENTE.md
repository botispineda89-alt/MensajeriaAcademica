# Verificación manual pendiente: chat Python

La suite automatizada Python pasa, pero no sustituye el smoke de la aplicación real. Completa esta
lista antes de considerar cerrada la alineación visual/funcional. Ejecuta el cliente desde
`universidad-mensajeria\client-python` para que lea el `client.properties` correcto.

## Preparación

- [X] Iniciar el servidor con una cuenta de prueba y confirmar que JavaFX, WPF y Python conectan.
- [x] Usar dos equipos o dos instancias de cliente para probar los mensajes en vivo.
- [x] Tener a mano dos o más imágenes distintas y un archivo que no sea imagen.
- [ ] Antes y después del smoke, ejecutar:

  ```powershell
  .\.venv\Scripts\python.exe -m unittest discover -s tests
  ```

## Inspección visual de la ventana Python

- [X] Abrir el login y cambiar de tema claro a oscuro y de oscuro a claro; confirmar que campos,
  combo, etiquetas y botón conservan contraste y texto legible.
- [X] Abrir una conversación y comprobar que el fondo ilustrado claro aparece en tema claro y el
  fondo oscuro aparece en tema oscuro, después de cargar historial y cambiar de conversación.
  (Verificado 2026-10-11 tras roadmap §5.10: fondo pintado a cover en ambos temas y el historial
  propio vuelve a aparecer; ver también `ui/lista_mensajes_chat.py` y `transversal/fechas.py`.)
- [X] Comprobar que el fondo ocupa el viewport de mensajes, no la lista de contactos ni el compositor,
  y que las burbujas siguen siendo legibles.
- [X] Seleccionar varias imágenes: comprobar que el montaje compuesto es solo una vista previa local
  en la bandeja y que se puede quitar cada archivo individualmente.
- [X] Enviar y recibir un archivo e imagen; comprobar miniatura/descarga y, si no hay miniatura, las
  etiquetas «Enviado archivo» o «Enviada imagen» (y las etiquetas de recibido correspondientes).
- [X] Revisar tema claro y oscuro en la bandeja de adjuntos, el montaje, las burbujas y los avisos.

## Interacciones, estados e historial

- [ ] Conectar dos cuentas distintas. Enviar un texto de Python a JavaFX o WPF y responder desde el
  otro cliente; comprobar entrega en vivo en ambas direcciones.
- [ ] Abrir un mensaje desde el receptor y confirmar que el emisor pasa de «Enviado» a «Entregado» y
  luego a «Leído» (✓✓ azul), sin cerrar ni reabrir la conversación.
- [ ] Cerrar la ventana de login: elegir **No** y confirmar que permanece abierta; volver a cerrar,
  elegir **Sí** y confirmar que la aplicación termina.
- [ ] Cambiar repetidamente de conversación A → B → A mientras llegan/responden cargas de historial.
  Confirmar que nunca se mezclan mensajes, nombres, contadores, miniaturas o scroll entre chats.
- [ ] Cargar mensajes anteriores y comprobar que la posición de lectura se conserva y que no aparecen
  duplicados al volver al chat.
- [ ] Responder a un mensaje de texto, una imagen y un archivo. Confirmar el preview en el compositor
  y que el contenido enviado mantiene el prefijo ordinario
  `Respuesta a Nombre [CÓDIGO]: "…"` (no un formato de protocolo nuevo).

## Pruebas de varias imágenes y varias sesiones

- [ ] Seleccionar dos o más imágenes. Confirmar que la bandeja muestra el montaje local, pero el
  servidor y el cliente receptor reciben cada imagen original como un mensaje/archivo independiente,
  en el orden seleccionado. No debe aparecer una imagen PNG compuesta como adjunto remoto.
- [ ] Comprobar que cada imagen conserva su propio estado de envío/entrega y que el texto escrito con
  un lote solo de imágenes queda como caption de la primera imagen.
- [ ] Iniciar la misma cuenta en PC1 con JavaFX y PC2 con Python. Enviar desde JavaFX y comprobar si
  Python recibe el eco dentro de la conversación con el destinatario correcto; repetir al revés.
  Si el eco no llega a la conexión de la otra instancia, registrar el resultado: el cliente Python
  solo puede pintar eventos que el servidor ya difunda, y este plan no autoriza cambios de backend.

## Evidencia y cierre

- [ ] Guardar capturas de login, ambos fondos, estados de lectura, respuesta citada, bandeja/montaje
  local y una conversación con varias imágenes recibidas por separado.
- [ ] Anotar sistema operativo, cliente/tema probado y resultado de cada caso que falle.
- [ ] Si falla únicamente el eco entre dos instancias de la misma cuenta, adjuntar el log/resultado
  de recepción del servidor y mantener ese punto abierto fuera del alcance Python.
- [ ] Marcar el plan como completo solo después de revisar todas las comprobaciones anteriores y
  confirmar que las 257 pruebas (5 omitidas) siguen pasando.
