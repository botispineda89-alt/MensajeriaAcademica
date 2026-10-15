# Guía de pruebas y aceptación

Esta guía cubre las comprobaciones compartidas del servidor y los tres clientes. Los pasos para
instalar cada proceso están en [`DESPLIEGUE.md`](../DESPLIEGUE.md) y en el README dentro de cada
carpeta de despliegue.

## Pruebas automatizadas

> Corte 2026-10-14: el usuario confirmó que los smoke manuales pasaron. No se reejecutaron en esta
> actualización documental. Los SMOKE-01 a SMOKE-11 y los criterios aplicables están marcados como
> aprobados; los detalles de SO, hora, IDs y capturas que no se registraron en el momento se señalan
> como no disponibles, sin inventar evidencia.

Desde `universidad-mensajeria/`, con Docker disponible y MySQL de pruebas en `localhost:3307`:

```powershell
docker compose up -d mysql
mvn -pl server/servidor-app,client/client-app -am test
```

Pruebas de Python, desde `client-python/`:

```powershell
.\.venv\Scripts\python.exe -m unittest discover -s tests
```

Pruebas y build WPF, desde `universidad-mensajeria/` (los tests actuales apuntan a .NET 10):

```powershell
dotnet test client-dotnet.Tests/Mensajeria.Tests.csproj
dotnet build client-dotnet/Mensajeria.csproj -c Release
```

La aceptación automatizada no sustituye la prueba manual entre procesos y computadores.

**Smoke de adjunto grande (20 MiB, servidor real).** Es el único chequeo que no usa dobles de red:
levanta dos sesiones TCP contra el servidor de verdad y compara el SHA-256 de lo que llega. Desde la
raíz del repositorio, con MySQL arriba y el servidor **escuchando** en 5000 (su consola JLine espera
`7 Iniciar`):

```powershell
.\universidad-mensajeria\client-python\.venv\Scripts\python.exe smoke\smoke_adjunto_20mb.py
```

Sale con 0 si A→B y B→A cuadran byte a byte; el resultado confirmado el 2026-10-14 también valida
A→A mediante comparación SHA-256. Ver `ESTADO_ROADMAP.md` §§1 y 5.15.

**Estado de las suites (corte 2026-10-14):** JavaFX **305/305** (49 clases, ArchUnit incluida),
Python **321 pruebas** (5 omitidas) y .NET **391/391** (incluye el test de UIX-27 del 2026-10-15). Build WPF Release 0/0.
`contrato/verificar.py` pasa **10/10** (tokens, estados, puertos, contrato, iconos, textos UI, suites JavaFX/Python/.NET y build WPF) y **sirve como puerta de calidad**. `server/utilerias` 15/15 y `codec` 7/7 (2026-10-10).
En `servidor-app`, `JpaPersistenciaAdapterTest` pasó 7/7 con la resolución de subidas duplicadas de imágenes (bug §5.7 cerrado); la suite general conserva el registro del 2026-10-08: 106 casos, 102 en verde, 4 fallos preexistentes ajenos a cambios de cliente.

## Preparación del smoke

1. Ejecuta el servidor y cada cliente en computadores separados, o valida primero en LAN con
   ventanas separadas. Asegura que el firewall permite cliente → servidor en `5000/TCP`.
2. Confirma conectividad desde cada cliente con `Test-NetConnection IP_SERVIDOR -Port 5000`.
3. Usa cuentas distintas. El alta de usuarios desde las UIs está desactivada; las cuentas de
   demostración están en el CSV de semilla del servidor.
4. Comprueba primero una dirección Java ↔ cliente remoto y luego todas las parejas de la matriz.

## Matriz de interoperabilidad

En **cada dirección** verifica mensaje recibido una sola vez, remitente/nombre correctos, burbuja
propia a la derecha y recibida a la izquierda, estado entregado/leído sobre el mensaje correcto:

| Pareja | Direcciones que se deben probar |
|---|---|
| Java ↔ Python | Java → Python; Python → Java |
| Java ↔ .NET | Java → .NET; .NET → Java |
| Python ↔ .NET | Python → .NET; .NET → Python |

Para cada dirección envía: texto; imagen sola; texto e imagen; respuesta a texto; respuesta a
imagen. Revisa que una cita llegue como texto ordinario y que un cliente no emita un segundo acuse
al recibir `MENSAJE_LEIDO` o `MENSAJE_ENTREGADO`.

## Historial, sesiones e imágenes

- Mantén dos procesos Java abiertos con cuentas distintas. Un mismo ID recibido debe quedar con la
  orientación relativa a cada cuenta; enviar en una cuenta no debe parecer un mensaje propio en la
  otra. ✅ Verificado por el usuario el 2026-10-15.
- Abre dos sesiones del mismo usuario. Cerrar una con `LOGOUT` no debe desconectar la otra; la
  presencia queda offline únicamente después de cerrar la última. Prueba explícitamente «Salir en
  todos».
- Envía una imagen nueva y comprueba en el servidor `uploads/Nombre Apellido [CODIGO]/<hash>/` el
  original y las cinco derivadas. En el otro cliente la imagen aparece en la conversación, también
  después de reiniciar; la carga es diferida, no bloquea la UI, y un fallo permite reintento.
- Cierra y vuelve a abrir cada cliente. Verifica historial, nombre/código, metadatos de imagen y
  acuses. No borres las bases legacy antes de comprobar la migración por cuenta. ✅ Verificado por el usuario el 2026-10-15.
- Desconecta temporalmente un cliente, vuelve a conectarlo y comprueba reintento sin duplicados.
- Comprueba que el botón de registro no aparece en ninguna UI.

## Informes y almacenamiento del servidor

- Inicia sesión y consulta `informe conexiones`: el conteo aumenta por login exitoso, no por enviar,
  listar o leer mensajes; las sesiones vivas cambian al abrir/cerrar sesiones.
- En informes 2–4 se muestra `Nombre Apellido [CODIGO]`; el filtro por código sigue funcionando.
- Inspecciona filas, rutas y auditoría con DBeaver siguiendo la sección de la [guía del servidor](../universidad-mensajeria/server/servidor-app/README.md#inspección-con-dbeaver).
- La presencia es estado vivo en memoria del servidor; no concluyas que una persona está online
  basándote en una fila histórica de MySQL.

## Registro del resultado

Anota sistema operativo y versiones, commit/versión desplegada, IP y puerto (sin contraseñas),
pareja/dirección, caso probado, resultado, hora e ID del mensaje. Si falla, conserva el log
pertinente de cliente y servidor y revisa el mismo ID y tipo de trama en ambos extremos.

### Smoke visual del 2026-10-14 (matriz 6/6)

El usuario abrió los 3 clientes y probó las 6 direcciones (Java ↔ Python, Java ↔ .NET,
Python ↔ .NET, en ambos sentidos): **todo llegó bien**. Con este smoke se encontraron 3 defectos,
ya corregidos (roadmap §5.21: visto azul en Python, auto-chat en Java, cita como bloque en
Python). Confirmado por el usuario el 2026-10-14; no se registraron IDs de mensaje, hora exacta ni
capturas anexas.

---

## Checklist exhaustivo de aceptación manual (para marcar durante el Smoke)

Esta sección conserva los criterios y resultados del smoke manual. El usuario confirmó el 2026-10-14
que los SMOKE-01 a SMOKE-11 pasaron; las marcas `[x]` registran esa aceptación y no implican que
esta actualización haya vuelto a ejecutar pruebas.

### [x] SMOKE-01: Matriz de interoperabilidad en 6 direcciones (Java ↔ Python ↔ .NET)
- **Alcance:** Probar las 6 parejas (Java → Python, Python → Java, Java → .NET, .NET → Java, Python → .NET, .NET → Python).
- **Qué DEBE salir / ocurrir:**
  - [X] **Envío de texto:** El mensaje llega inmediatamente; en el emisor la burbuja se ubica a la **derecha** con color primario/morado; en el receptor la burbuja se ubica a la **izquierda** con fondo neutro.
  - [X] **Nombre y código:** En el receptor aparece arriba de la burbuja el formato canónico `Nombre Apellido [CODIGO]`.
  - [X] **Acuses de recibo:** El mensaje enviado progresa de `ENVIADO` (un tick gris) → `ENTREGADO` (doble tick gris) → `LEIDO` (doble tick azul/destacado al enfocar o abrir el chat en el receptor).
  - [X] **Envío de imagen sola:** La imagen se previsualiza nítida en la burbuja del receptor y permite abrir visor / hacer clic.
  - [X] **Texto e imagen (misma burbuja con caption):** El texto viaja como pie de foto y se muestra como **una sola burbuja** (imagen arriba, texto abajo), NO como dos mensajes separados.
  - [X] **Respuesta a texto (cita):** Al pulsar "Responder" o doble clic, aparece la tarjeta de cita en el compositor y la burbuja original se resalta levemente en dorado (`#FFD166`) durante 1.5 s. Al enviar, el mensaje llega como texto ordinario con prefijo `Respuesta a Nombre [CODIGO]: "..."` o bloque estilizado de cita sin alterar el protocolo.
  - [X] **Respuesta a imagen:** La tarjeta de cita referencia la imagen y llega vinculada correctamente.
- **Qué NO DEBE salir / ocurrir:**
  - [X] **NO** debe emitirse un segundo acuse de recibo al recibir un evento `MENSAJE_LEIDO` o `MENSAJE_ENTREGADO` (no generar bucles de acuses).
  - [X] **NO** debe aparecer la burbuja propia alineada a la izquierda ni en color gris.
  - [X] **NO** deben llegar mensajes duplicados bajo ninguna circunstancia.
  - [X] **NO** debe romperse ni alterarse el texto citado.

### [x] SMOKE-02: Dos procesos con cuentas distintas en el mismo PC
- **Alcance:** Levantar dos clientes del mismo toolkit (ej. dos instancias JavaFX o dos Python) con dos usuarios diferentes de `usuarios_iniciales.csv`.
- **Qué DEBE salir / ocurrir:**
  - [X] Cada proceso muestra en el sidebar y en el título de la ventana la identidad de su propia cuenta: `Mensajería Académica | Nombre Apellido [CODIGO]`.
  - [X] Un mensaje enviado desde la Cuenta A se ve a la derecha en la ventana A y a la izquierda en la ventana B.
  - [X] El archivo de historial local de cada cuenta (`historial_local` o SQLite `mensajeria_cliente_<CUENTA>.db`) se mantiene completamente aislado sin colisiones.
- **Qué NO DEBE salir / ocurrir:**
  - [X] Un mensaje enviado por A **NO** debe aparecer con `Tú:` ni con estilo propio en la ventana de B.
  - [X] Las conversaciones abiertas de una cuenta **NO** deben filtrarse ni interferir con la otra instancia.

### [x] SMOKE-03: Múltiples sesiones del mismo usuario y Presencia
- **Alcance:** Abrir dos sesiones simultáneas con el mismo código de usuario (ej. una en JavaFX y otra en .NET o dos instancias).
- **Qué DEBE salir / ocurrir:**
  - [X] Ambas sesiones pueden enviar y recibir mensajes correctamente.
  - [X] Al pulsar «Cerrar sesión» (`LOGOUT`) en la Sesión 1, solo la Sesión 1 vuelve a la pantalla de login.
  - [X] La Sesión 2 sigue conectada, activa y funcionando normalmente.
  - [X] La presencia del usuario para terceros en el directorio sigue mostrándose como **Online** mientras la Sesión 2 siga viva.
  - [X] Al cerrar la Sesión 2 (la última), el estado del usuario pasa a **Offline** en el directorio de los demás clientes.
  - [X] Al accionar «Salir en todos» (`KICK` / `Cerrar sesiones`), la otra sesión recibe la notificación de expulsión y es redirigida al login con el aviso correspondiente.
- **Qué NO DEBE salir / ocurrir:**
  - [X] Cerrar una sesión **NO** debe desconectar las demás sesiones del mismo usuario.
  - [X] El estado del usuario en el directorio **NO** debe pasar a Offline si aún le queda al menos una sesión activa.

### [x] SMOKE-04: Gestión de imágenes y derivadas en servidor
- **Alcance:** Enviar una imagen PNG o JPG y comprobar almacenamiento en servidor y visualización en clientes.
- **Qué DEBE salir / ocurrir:**
  - [X] En el servidor, se crea la carpeta física `uploads/Nombre Apellido [CODIGO]/<hash_sha256>/` conteniendo el archivo original más las 5 imágenes derivadas de filtros secuenciales (grises, sepia, etc.).
  - [X] En la consola JLine del servidor se imprime el bloque multilínea informativo con el hash SHA-256 y las rutas de las 5 derivadas generadas.
  - [X] En el cliente receptor, la imagen carga de forma diferida en segundo plano sin congelar la interfaz de usuario.
  - [X] Al pulsar el botón de guardar en la burbuja de imagen, se descarga el archivo **original** (mismo hash, nombre y tamaño original).
  - [X] Al hacer clic para ampliar, el visor abre la imagen nítida en resolución 1:1 real con controles de zoom.
  - [X] Al reenviar la misma imagen inmediatamente, el servidor la procesa con idempotencia sin error de duplicados.
- **Qué NO DEBE salir / ocurrir:**
  - [X] **NO** debe bloquearse el hilo de la interfaz gráfica durante la decodificación o descarga de imágenes.
  - [X] **NO** debe arrojarse error de clave duplicada en `archivos.ruta` en el servidor al enviar la misma imagen.
  - [X] **NO** debe guardarse una versión reducida de thumbnail cuando el usuario solicita "Guardar imagen".

### [x] SMOKE-05: Cambios funcionales (Cita, Solo-adjunto, Login solo-lectura, Difusión)
- **Alcance:** Comprobar las funciones añadidas en todos los clientes.
- **Qué DEBE salir / ocurrir:**
  - [X] **Cita en tarjeta:** Al seleccionar responder, se muestra la tarjeta de previsualización con botón `✕`. La tecla `Esc` cancela la cita sin borrar el texto que el usuario haya escrito en la caja. Al enviar, la tarjeta de cita desaparece automáticamente.
  - [X] **Enviar solo adjunto:** Es posible adjuntar únicamente una imagen o un archivo (dejando la caja de texto vacía); el botón Enviar se habilita y el archivo llega íntegro al destinatario.
  - [X] **Límite de tamaño:** Al intentar adjuntar un archivo mayor al tope permitido (>50 MB), la aplicación muestra un aviso inline de advertencia y **deshabilita** el botón Enviar.
  - [X] **Login solo-lectura:** Los campos de servidor y puerto en la pantalla de login están protegidos contra edición accidental y reflejan los valores configurados en `client.properties`.
  - [X] **Confirmación de cierre:** Al intentar cerrar la ventana de login en .NET o cerrar la ventana de chat, se solicita confirmación al usuario antes de salir.
- **Qué NO DEBE salir / ocurrir:**
  - [X] El botón `✕` de la tarjeta de cita **NO** debe borrar el texto que ya estaba redactado en el compositor.
  - [X] El botón Enviar **NO** debe habilitarse si la caja de texto está vacía y tampoco hay adjunto seleccionado.

### [x] SMOKE-06: Transición de 4 estados de conexión en los 3 clientes (`PAT-03`)
- **Alcance:** Comprobar la respuesta visual de los 3 clientes ante caídas y restauraciones de red.
- **Qué DEBE salir / ocurrir:**
  - [X] **→ `Conectando`:** Al iniciar login con servidor iniciando o reconectando, aparece indicador neutro de "Conectando...".
  - [X] **→ `Conectado`:** Tras login exitoso, el banner ámbar desaparece de inmediato y se carga el directorio de contactos.
  - [X] **→ `SinRed`:** Al detener abruptamente el servidor, los clientes reaccionan mostrando el banner de advertencia ámbar ("Sin conexión con el servidor") sin cerrarse abruptamente.
  - [X] **`SinRed` → `Conectando` → `Conectado`:** Al volver a encender el servidor, los clientes reconectan automáticamente con backoff exponencial y vacían las colas de mensajes pendientes sin duplicar nada. El banner de desconexión desaparece al recuperar `Conectado`, aunque el drenado de la cola continúe.
  - [X] **→ `SesionCerrada`:** Al pulsar "Cerrar sesión" voluntariamente o recibir expulsión remota, el cliente muestra el aviso con el motivo del cierre y regresa a la pantalla de login.
- **Qué NO DEBE salir / ocurrir:**
  - [X] **NO** debe colgarse ni crashear la aplicación al perder la conexión TCP.
  - [X] Un cierre de sesión voluntario (`LOGOUT`) **NO** debe mostrar falsamente el banner de "Error de red / Sin conexión".
  - [X] La reconexión automática **NO** debe re-enviar múltiples veces los mensajes ya entregados.

### [x] SMOKE-07 y SMOKE-08: Capturas lado a lado y contraste WCAG AA en temas Claro y Oscuro
- **Alcance:** Inspeccionar visualmente la estética en tema claro y oscuro en los tres clientes.
- **Qué DEBE salir / ocurrir:**
  - [X] **Modo Claro (Python, JavaFX, .NET):** El texto del cuerpo de los mensajes y de las citas se muestra en color negro nítido (`#000000`), garantizando contraste superior a 16:1 (cumple holgadamente WCAG AA/AAA).
  - [X] **Modo Oscuro:** Los textos son de color claro de alto contraste sobre fondo morado oscuro/superficie oscura, sin fatiga visual.
  - [X] **Placeholders y fechas:** Fechas, metadatos y textos de ayuda cumplen ratio de contraste ≥ 4.5:1.
  - [X] **Cambio de tema en caliente:** Al alternar entre tema Claro y Oscuro con 50+ mensajes en pantalla, el cambio es instantáneo sin recargar datos de red ni parpadear.
- **Qué NO DEBE salir / ocurrir:**
  - [X] En tema claro en Python **NO** debe aparecer texto blanco ni gris claro sobre fondo claro (texto perfectamente legible).
  - [X] El cambio de tema **NO** debe reiniciar la posición de scroll ni perder la conversación abierta.

### [x] SMOKE-09: Consola administrativa y servidor JLine + JavaFX
- **Alcance:** Comprobar la ventana administrativa de JavaFX del servidor y su consola JLine.
- **Qué DEBE salir / ocurrir:**
  - [X] **Pestaña Estado:** Los indicadores métricos (CPU, memoria, hilos, conexiones) se presentan de forma vertical y responsiva; al redimensionar la ventana el diseño se adapta sin solapamientos.
  - [X] **Pestaña Conectados:** Muestra usuarios activos con formato `Nombre Apellidos [CODIGO]`. Al hacer clic en un usuario se sincroniza la selección. El botón «Desconectar usuario» abre diálogo de confirmación Sí/No. Con `No` no realiza ninguna acción.
  - [X] **Pestaña Usuarios:** Tabla centrada y paginada (50 por página). Doble clic sobre un usuario abre su ficha con resumen, conexiones, historial y archivos.
  - [X] **Pestaña Mensajes:** Permite filtrar por usuario y tipo. Muestra contadores de caracteres, palabras, hash e IP históricas capturadas al enviar para remitente y destinatario. La IP guardada no cambia cuando una sesión se desconecta; «No disponible» solo corresponde a una IP que no se pudo registrar en el momento del envío.
  - [X] **Difusión del Servidor:** Al escribir un mensaje de difusión y pulsar «Enviar difusión», el mensaje se emite a todos los clientes conectados y **la barra de texto de difusión se limpia automáticamente**.
  - [X] **Cierre con `✕`:** Al pulsar la X de la ventana administrativa, un diálogo pregunta si desea cerrar la vista administrativa; al confirmar, **la vista se cierra pero el servidor TCP y la consola JLine continúan funcionando**.
  - [X] **Consola JLine:** El comando `7 Iniciar` levanta el puerto; los comandos `usuarios`, `conectados`, `informe`, `logs` responden con formato ordenado y jerárquico.
- **Qué NO DEBE salir / ocurrir:**
  - [X] Pulsar la X de la ventana del servidor **NO** debe matar el proceso servidor ni desconectar a los clientes conectados.
  - [X] Enviar una difusión **NO** debe dejar el texto anterior pegado en el campo de entrada.
  - [X] Las letras y encabezados de la vista de difusión **NO** deben quedar en blanco sobre fondo claro ni volverse invisibles.

### [x] SMOKE-10: Salto al mensaje citado estilo Telegram Web y Búsqueda en historial
- **Alcance:** Verificar la navegación interactiva a mensajes respondidos en JavaFX, Python y .NET.
- **Qué DEBE salir / ocurrir:**
  - [X] Al hacer clic sobre el bloque/tarjeta de la cita (incluso en su zona vacía/transparente), la lista de mensajes hace scroll suave o salto y posiciona el mensaje original citado **centrado en pantalla** (`PositionAtCenter`).
  - [X] El mensaje original citado se resalta visualmente de inmediato con un marco/borde dorado (`#FFD166`), el cual se desvanece o retira automáticamente tras 1.5 segundos. Al iniciar una respuesta con «Responder», también se resalta la burbuja original sin cambiar el scroll.
  - [X] Si el mensaje citado estaba en una página anterior del historial, el cliente lo localiza y salta a él sin recargar datos innecesarios.
  - [X] Al pulsar `Ctrl+F`, se despliega la barra de búsqueda en el chat; al escribir con debounce resalta las coincidencias y permite avanzar (`Enter`) y retroceder (`Shift+Enter`).
  - [X] La tecla `Esc` cierra la barra de búsqueda; una segunda pulsación de `Esc` deselecciona la conversación activa y regresa al estado inicial sin selección.
- **Qué NO DEBE salir / ocurrir:**
  - [X] El clic en la cita **NO** debe fallar silenciosamente ni dejar al usuario en la misma posición de scroll.
  - [X] El resaltado dorado **NO** debe quedar permanente en el mensaje (debe retirarse tras 1.5 s).
  - [X] La búsqueda en el historial **NO** debe congelar la UI al tipear términos de búsqueda.

### [x] SMOKE-11: Transferencia de adjuntos grandes (20 MiB) y fragmentación
- **Alcance:** Transferir un archivo grande (ej. 20 MiB) mediante el script `smoke_adjunto_20mb.py` o directamente entre dos clientes.
- **Qué DEBE salir / ocurrir:**
  - [X] El archivo se fragmenta en bloques secuenciales de 1 MiB (`ARCHIVO_INICIO` → N × `ARCHIVO_PARTE` → `ARCHIVO_FIN`).
  - [X] En cada cliente, la operación de 20 MiB termina sin mostrar error si el servidor aceptó y guardó el archivo; el ACK final llega antes del timeout de transferencia.
  - [X] .NET y Python muestran el avance por partes; JavaFX muestra el avance ponderado al terminar cada adjunto (no por fragmento).
  - [X] Al finalizar, el hash SHA-256 del archivo recibido en el destino coincide exactamente byte a byte con el archivo emisor.
  - [X] Si el usuario pulsa «Cancelar envío» en mitad de la transferencia, el destinatario no recibe el archivo, el mensaje cancelado no queda en la cola offline y el servidor elimina el temporal; el archivo no enviado vuelve a la bandeja.
- **Qué NO DEBE salir / ocurrir:**
  - [X] **NO** debe desconectarse la sesión ni mostrarse un falso error por superar el tamaño de trama o agotar un timeout corto (no debe emitirse `Frame size exceeds maximum` para los bloques válidos de 1 MiB).
  - [X] La descarga **NO** debe crear un archivo de 0 bytes ni corromper el contenido.

---

### Registro del resultado del Smoke Manual por el evaluador
| Smoke ID | Evaluador | Fecha / Hora | Sistema Operativo | Resultado (Aprobado / Con Observaciones) | Notas / Hallazgos |
|---|---|---|---|---|---|
| SMOKE-01 | Usuario | 2026-10-14 (hora no registrada) | No registrado | Aprobado | Confirmación directa; sin IDs/capturas anexas |
| SMOKE-02 | Usuario | 2026-10-14 (hora no registrada) | No registrado | Aprobado | Confirmación directa |
| SMOKE-03 | Usuario | 2026-10-14 (hora no registrada) | No registrado | Aprobado | Confirmación directa |
| SMOKE-04 | Usuario | 2026-10-14 (hora no registrada) | No registrado | Aprobado | Confirmación directa |
| SMOKE-05 | Usuario | 2026-10-14 (hora no registrada) | No registrado | Aprobado | Confirmación directa |
| SMOKE-06 | Usuario | 2026-10-14 (hora no registrada) | No registrado | Aprobado | Confirmación directa |
| SMOKE-07 | Usuario | 2026-10-14 (hora no registrada) | No registrado | Aprobado | Confirmación directa; archivos de captura no anexados |
| SMOKE-08 | Usuario | 2026-10-14 (hora no registrada) | No registrado | Aprobado | Confirmación directa |
| SMOKE-09 | Usuario | 2026-10-14 (hora no registrada) | No registrado | Aprobado | Confirmación directa |
| SMOKE-10 | Usuario | 2026-10-14 (hora no registrada) | No registrado | Aprobado | Confirmación directa |
| SMOKE-11 | Usuario | 2026-10-14 (hora no registrada) | No registrado | Aprobado | A→B, B→A y A→A comparados por SHA-256 |

---

## Smoke de los cambios 2026-10-04 (cita, login, descarga, difusión)

- **Cita en tarjeta (3 clientes):** responder muestra la cita en tarjeta
  con ✕ y el campo queda limpio; Esc cancela cita y adjunto sin borrar el
  texto; el mensaje enviado lleva la cita como prefijo. Cancela con ✕ y
  verifica que el envío sale sin cita.
- **Enviar solo adjunto:** en cada cliente, adjunta solo imagen y luego
  solo archivo (sin texto) y verifica que Enviar habilita y llega.
  Prueba también adjunto mayor a 50 MB (debe avisar, no habilitar).
- **Login solo-lectura:** en los 3 clientes, servidor/puerto no editables
  y reflejan `client.properties`; en .NET, cerrar el login pregunta
  "¿Deseas cerrar el programa?".
- **Descarga con filtro (escritorio):** aplica Grises/Sepia/etc.,
  previsualiza y descarga: el archivo debe llamarse
  `<nombre>-<Filtro>.png` y diferir del original. El detalle muestra
  Nombre[codigo] (y `SERVIDOR (sistema)` para broadcasts).
- **Maestro-detalle (escritorio):** en Informes tipo mensajes, seleccionar
  fila muestra el mismo detalle que la pestaña Mensajes; en Usuarios,
  click muestra ficha + "Cerrar sesiones" + "Ver mensajes" (salta a
  Mensajes con el filtro puesto).
- **Difusión con progreso:** difunde desde consola y desde escritorio;
  verifica avance `Difusión N/total` y resumen final con fallos; con un
  cliente lento, el resto recibe sin bloqueo.
- **Reintento offline Python:** sin red, envía imagen/archivo (queda en
  pendientes), reconecta y verifica entrega sin duplicados (claves de
  contrato `nombreArchivo/tamanoArchivo/indiceParte/contenidoImagen/
  hashSha256/archivoId` en el reintento).

---

## Smoke del lote de clientes 2026-10-10/11 (JavaFX y WPF)

Lo que las suites no cubren: el orden del historial, el pie en la misma burbuja y la lista de
conversaciones se comportan distinto en pantalla que en la base. El detalle de qué se corrigió está en
[`docs/archivo/PLAN_CLIENTES-2026-10-historico.md`](archivo/PLAN_CLIENTES-2026-10-historico.md).

- **Pie en la misma burbuja:** con una sola imagen + texto, el mensaje debe llegar como **una sola
  burbuja** con el texto debajo, no como dos mensajes. Repite con un archivo (no imagen) + texto.
- **Montaje:** 2, 3, 4 y 5 imágenes en un envío → **una sola burbuja** con la rejilla
  (2×1 / 3×1 / 2×2 / 3×2) y el texto como pie. Más de 10 imágenes se rechaza.
- **Orden del historial y "Tú:"** (el fallo era de fecha, no del prefijo): con el chat de A abierto,
  que B escriba **después** de tu último envío. El mensaje de B debe verse **debajo** y su preview en
  la lista **sin** `Tú: `. Repite en el cliente .NET con una base ya existente (arranca para que
  normalice las fechas antiguas).
- **Guardar imagen:** el botón de guardar aparece en imágenes y en archivos, y entrega el **original**
  (mismo nombre y tamaño que el que se envió), no la copia de visualización.
- **Visor:** una imagen grande se abre nítida; "Tamaño original" es 1:1 real y el zoom no interpola
  por encima de la resolución.
- **WebP:** envía un `.webp` desde JavaFX y desde WPF → llega con **el mismo nombre**, con las etapas
  del filtro, y se puede ampliar. Con el servidor viejo el `.webp` sería rechazado; si el log dice
  `Error procesando MENSAJE_IMAGEN`, revisa que `server/utilerias` lleve `imageio-webp`.
- **Avisos:** un "Listo" desaparece solo en ~4 s y un error a los ~12 s; ninguno tapa el chat de forma
  permanente, y el ✕ sigue cerrándolos antes.
- **Lista de contactos:** llega un mensaje de un contacto **distinto** al abierto → sube su insignia y
  cambia su preview sin abrir su conversación; con el filtro "Con no leídos" activo aparece solo.
- **Envío múltiple (solo WPF):** 10 archivos (mezclando imágenes y PDF) en un envío → bandeja,
  progreso por bytes, cancelación a mitad sin dejar pendientes, y "Se enviaron N de M" si algo falla.
  JavaFX admite multiselección/arrastre y una bandeja de hasta 5 adjuntos, pero aún no tiene
  progreso por bytes ni cancelación; Python acepta un adjunto por operación.

---

## Comprobaciones de seguridad (nuevas, 2026-10-05)

### `SEC-05` — tramas sin autenticación

Esta sección conserva un procedimiento de bajo nivel para reproducir el hallazgo histórico y
confirmar el comportamiento actual si se necesita una comprobación manual. `SEC-05` está cerrado;
la protección principal está cubierta por pruebas parametrizadas y E2E descritas en el
[`roadmap`](ESTADO_ROADMAP.md#seguridad-bloqueante).

1. Levanta el servidor. Abre un socket TCP crudo contra `IP_SERVIDOR:5000` **sin autenticar**.
   Puedes usar PowerShell:

   ```powershell
   $c = New-Object System.Net.Sockets.TcpClient("IP_SERVIDOR", 5000)
   $s = $c.GetStream()
   $trama = '{"tipo":"LISTAR_USUARIOS","id":"probe-1"}'
   $len = [BitConverter]::GetBytes([Text.Encoding]::UTF8.GetByteCount($trama))
   [array]::Reverse($len)
   $s.Write($len, 0, 4)
   $b = [Text.Encoding]::UTF8.GetBytes($trama)
   $s.Write($b, 0, $b.Length)
   $s.Flush()
   ```

2. Lee la respuesta. **Comportamiento correcto:** una trama `ERROR` con el motivo
   `autenticate primero con LOGIN`, y **ningún** dato de usuario.
3. Comportamiento anterior al arreglo: llegaba `LISTAR_USUARIOS_RESPUESTA` con datos del directorio.
   El comportamiento vigente debe cumplir el paso 2.
4. Repite con `LISTAR_CONECTADOS` y `LOGOUT`. Las 3 deben devolver `ERROR`.
5. Repite los 4 pasos con **TLS** en `5001` para confirmar que el cifrado no cambia el resultado.

### Después de cerrarlo

La prueba automática que lo fija es un test **parametrizado** que itera **todos** los tipos
registrados en `Despachador` y exige `ERROR` con sesión anónima. Si añades un comando nuevo y no lo
registras en esa prueba, el hueco vuelve: por eso la prueba debe recorrer el registro, no una lista
escrita a mano.

---

## Estados de conexión (`SMOKE-06`; `PAT-03` implementado)

Matriz de 4 estados × 3 clientes. Los 4 estados son `Conectando`, `Conectado`, `SinRed` y
`SesionCerrada`.

| Transición | Cómo se provoca | Qué se espera en los 3 clientes | Anotar |
|---|---|---|---|
| → `Conectando` | Login con el servidor apagado y luego encendiéndolo | Indicador de carga neutro mientras conecta; el error de transporte, si llega, se muestra separado. La corrección de .NET está en `UIX-00f`. | |
| → `Conectado` | Login correcto | El banner ámbar desaparece; el directorio carga | |
| → `SinRed` | Matar el proceso del servidor con el cliente abierto | Los 3 clientes reciben el estado observable y muestran el aviso/banner de desconexión. Medir la latencia real; no asumir el límite de 2 s a partir de la ida/vuelta de una herramienta remota. | |
| → `SesionCerrada` | `Salir`, o `Salir en todos` desde otra sesión, o esperar 10 min | Aviso con el **motivo** y vuelta al login. En WPF, el cierre remoto se comunica en el banner del login. | |
| `SinRed` → `Conectando` | Reencender el servidor | Comprobar reconexión y reintento de cola sin duplicados; anotar si el usuario debe accionar una actualización según el cliente. | |

`PAT-03` está implementado en Java, Python y .NET. El usuario confirmó el SMOKE-06 completo el
2026-10-14. No quedaron registrados SO, textos observados por transición ni tiempos medidos; no se
inventan esos datos y el caso se considera aprobado conforme a la confirmación recibida.

---

## Capturas lado a lado (`SMOKE-07` y `SMOKE-08`)

`VIS-01` está implementado (la paleta única se genera desde `contrato/tokens-diseno.json`). El
usuario confirmó SMOKE-07/08 el 2026-10-14; no hay archivos de captura adjuntos a este registro.

1. Con la **misma conversación** en los 3 clientes, captura el mismo intervalo en tema **oscuro**
   y en tema **claro**.
2. Compara contra `DIAGRAMAS/mockups/mockup-chat-telegram-{java,python,dotnet}.png`.
3. Los colores se comparan contra la paleta declarada en los recursos actuales, no contra valores
   anteriores de la auditoría. Anota los valores leídos en los 3 clientes y clasifica diferencias
   por tema/toolkit.
4. Contrastes a medir con el inspector del navegador de toolkit o una herramienta de contraste:
   `.chat-list-preview` en tema claro, el placeholder del campo de búsqueda, y la fecha de la
   burbuja. Todos deben llegar a **4.5:1**.
5. Anota los hex finales de los 2 tokens que divergían, y la ratio de los 4 contrastes.

---

## Búsqueda en el historial (`UIX-06`)

La búsqueda (`Ctrl+F`) y el debounce están implementados en los tres clientes; esta comprobación
verifica el recorrido de usuario y la paridad con datos comunes.

1. `Ctrl+F` en la conversación abierta.
2. Busca un texto que esté en un mensaje **cargado hace varias páginas** (scrollea hacia atrás).
3. Debe encontrarlo sin abrir el historial entero.
4. La búsqueda debe aplicar debounce y mostrar el resultado sin bloquear la interfaz.
5. Repetir en los 3 clientes con el mismo texto: los resultados deben ser los mismos.

---

## Cambio de tema y reconstrucción de la lista (`UIX-26`, `UIX-27`, `UIX-29`)

1. Cambia de oscuro a claro **con 100+ mensajes** en pantalla en cada cliente.
2. El cambio debe ser fluido y **no** recargar la red ni releer las imágenes desde el disco.
3. En Python, el arranque debe usar el tema oscuro por defecto (`UIX-29` ya está implementado) y
   respetar una configuración explícita si se admite en `client.properties`.
4. Comprueba que los iconos de adjunto en modo oscuro usan colores legibles (`UIX-26` hecho
   según `ESTADO_ROADMAP.md` §5.3; verificar visualmente en la bandeja).
5. Cambio de tema en .NET sin recarga de datos (`UIX-27` hecho y confirmado visualmente
   el 2026-10-15: `RefrescarTema()` repinta sin reconstruir la lista).

## Avisos inline del cliente WPF (`UIX-15`)

1. Provoca avisos/errores recuperables de envío, descarga, adjunto y cierre de otras sesiones.
2. Comprueba que cada aviso aparece en el banner del chat con severidad, se puede cerrar y un aviso
   posterior reemplaza el anterior sin diálogos en cascada.
3. Simula un cierre remoto: el motivo debe aparecer en el banner ámbar del login.
4. Verifica las tres confirmaciones que se conservan: cerrar LoginWindow, salir del chat y cerrar
   todas las sesiones.
5. La cobertura xUnit de `EstadoAvisoInline` está en
   `universidad-mensajeria/client-dotnet.Tests/EstadoAvisoInlineTests.cs`. Desde el 2026-10-10 **todos**
   los avisos se retiran solos (4 s éxito, 6 s información, 8 s advertencia, 12 s error): comprueba que
   el error desaparece solo y que el ✕ sigue cerrándolo antes. La última ejecución registrada de la
   suite completa es **366/366** (2026-10-11).

## Consola administrativa del servidor (Sesión B 2026-10-07; cierre 2026-10-08)

1. En JavaFX, confirma que Estado presenta cada indicador verticalmente y que el texto no se
   solapa al redimensionar la ventana.
2. En Conectados, revisa el estado vacío «No hay usuarios activos» y, con sesiones abiertas, que
   lista y desplegable muestran `Nombre Apellidos [código]`; clic en la lista cambia el
   desplegable y viceversa. «Desconectar usuario» pide confirmación Sí/No con el nombre
   visible; con `No` no se cierra nada.
3. En Usuarios, confirma la tabla centrada con márgenes al redimensionar; abre un usuario con
   doble clic; la ficha debe mostrar resumen, conexiones, mensajes y archivos, con paginación
   de 50 registros y contacto principal. «Desconectar usuario» también confirma.
4. En la ficha, pestaña Mensajes: filtro por tipo (Todos/TEXTO/IMAGEN/ARCHIVO/BROADCAST),
   tabla + detalle con preview/descarga igual que en Mensajes, divisor movible; al cambiar de
   página conserva la selección por id.
5. En Mensajes, escribe un código y selecciónalo del desplegable; confirma caracteres, palabras,
   IP históricas del remitente y destinatario capturadas al enviar; desconecta después cualquiera
   de las cuentas y confirma que los valores no cambian. «No disponible» solo aplica si no había
   una sesión destinataria cuya IP pudiera registrarse al enviar. Envía una imagen y confirma en la consola JLine el bloque multilínea con hash y
   las 5 rutas derivadas; en texto confirma hash y conteos.
6. En Informes, verifica que el detalle de mensaje/imagen solo está visible para el informe de
   mensajes; pulsa `Actualizar` y confirma que no hay auto-refresh que mueva scroll, selección
   o detalle (el texto de ayuda lo indica); revisa la expansión de Logs, la presentación de
   Pool y el botón «Enviar difusión».
7. Cierra la ventana administrativa con X: debe preguntar si se cierra esa vista; confirmar no
   debe detener el servicio TCP ni JLine.
8. En JLine, recorre títulos, comandos, tablas y eventos; confirma que se conservaron los comandos.
