# Despliegue del cliente Python

Cliente de escritorio PySide6/QFluentWidgets. Esta carpeta contiene la aplicación Python y su
copia de `store/schema-local.sql`; no necesita el monorepo ni acceso a la base MySQL del servidor.

## Requisitos e instalación

Windows 10/11 de 64 bits y Python 3.13 recomendado (es el runtime de validación; el mínimo
declarado en `pyproject.toml` es 3.11). Las versiones de PySide6, QFluentWidgets y Pillow están
fijadas en `requirements.txt`.

Desde la carpeta `client-python`:

```powershell
py -3.13 -m venv .venv
.\.venv\Scripts\python.exe -m pip install --upgrade pip
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
.\.venv\Scripts\python.exe main.py
```

Inicia siempre desde la carpeta de la aplicación: `client.properties` se busca relativo al
directorio de trabajo. Instala la carpeta bajo un directorio donde el usuario tenga permisos de
escritura; las imágenes recibidas se almacenan en la subcarpeta `imagenes`.

## Preparar y copiar la carpeta

La forma oficial es el script `distribuir.cmd`, ejecutado desde `client-python`:

```powershell
cmd distribuir.cmd
```

Genera `dist\MensajeriaPython\` sin `.venv`, cachés, pruebas, imágenes recibidas (`imagenes/`) ni
bases SQLite. **Sí incluye los recursos gráficos estáticos de la interfaz**, en particular
`ui\fondos\fondo_chat_claro.jpg`, `ui\fondos\fondo_chat_oscuro.jpg` y los cinco iconos por tipo de
archivo de `ui\iconos\` (`FORMATOS-02`). Verifica el contenido mínimo
(`main.py`, `requirements.txt`, `client.properties`, `store\schema-local.sql`, fachada, UI y los cinco
iconos), compila
la copia con `compileall` para detectar errores de sintaxis y deja un `version.txt` con la fecha de
generación.

Si prefieres hacerlo a mano, copia el contenido de `client-python` excluyendo el entorno virtual,
las cachés y las pruebas. No copies `.venv`: contiene rutas y binarios ligados al equipo donde se
creó.

```powershell
$dist = "$env:USERPROFILE\Desktop\MensajeriaPython"
robocopy . $dist /E /XD .venv __pycache__ tests .pytest_cache imagenes dist /XF *.db *.db-shm *.db-wal *.pyc
```

`robocopy` puede devolver códigos `1` a `7` cuando la copia terminó correctamente. En el destino,
repite los pasos de instalación del entorno virtual y ejecuta `main.py`.

## Configuración

Edita `client.properties` en la carpeta del cliente:

```properties
host=192.168.1.20
puerto=5000
db.url=sqlite:///~/mensajeria_cliente.db
idioma=es
tema=oscuro
tls.habilitado=false
tls.puerto=5001
tls.confianza=
```

Reemplaza `192.168.1.20` por la IP LAN o el nombre DNS del servidor. `localhost` solo sirve cuando
Python y el servidor están en el mismo computador. `puerto` debe coincidir con `red.puerto` del
servidor. El cliente necesita salida TCP a ese puerto; no requiere puertos entrantes ni acceso a
MySQL.

**TLS opcional** (`transversal/tls.py`): con `tls.habilitado=true` el cliente conecta al puerto
`tls.puerto` (5001 por defecto) con TLS ≥ 1.2 y verificación de certificado y hostname siempre
activas. `tls.confianza` apunta a un bundle PEM con la CA propia; vacío, se usan las CA del
sistema. La validación es fail-fast antes de abrir el socket: un valor de `tls.habilitado` que no
sea `true`/`false`, puertos plano y TLS iguales, o una ruta de confianza indicada pero ausente
producen un error accionable en lugar de una conexión caída.

SQLite vive en el perfil del usuario y se deriva una ruta por código de cuenta. El primer acceso
migra de manera aditiva el archivo legado compartido si existe. No borres ese archivo antes de
iniciar por primera vez. `store/schema-local.sql` está incluido para que la migración y el primer
arranque también funcionen cuando solo se distribuye esta carpeta.

## Funcionalidad

- Acceso con código y contraseña (sin registro); la ventana muestra servidor
  y puerto con opción de recordarlos. Tema claro/oscuro persistente y aplicado
  también a los controles (campos, combo y botón cambian con el modo).
- El panel lateral es una lista de conversaciones estilo WhatsApp: tarjeta con
  punto de presencia, nombre, preview del último mensaje, hora relativa e
  insignia de no leídos; lo más nuevo arriba y la presencia no reordena (los
  contactos sin conversación van al final). Filtro Todos/Conectados/Con no
  leídos y buscador. La lista se pinta al instante desde la caché SQLite y la
  red la refresca por detrás; el directorio se escribe en lote (una sola
  transacción). El historial remoto se fusiona sin duplicar burbujas ya
  recibidas en vivo. Las fechas se guardan en UTC con sufijo `Z` (el mismo
  formato "O" de .NET) y al abrir la base se normaliza a ese formato todo lo
  heredado: como la SQLite por cuenta se comparte con el cliente .NET y la
  columna se ordena como TEXTO, guardar hora local sin zona sacaba los
  mensajes propios recién enviados de la primera página del historial.
- Enviar admite texto, imágenes y archivos de hasta 50 MB. Las imágenes viajan
  en una sola trama hasta 12 MB (la misma regla del cliente .NET); una imagen
  mayor se rechaza con aviso claro antes de tocar la red, porque su base64 ya
  no cabería en la trama de 16 MiB que fija el servidor. Los archivos usan
  el protocolo fragmentado (`ARCHIVO_INICIO/PARTE/FIN`); el `FIN` recibe `ACK`
  del servidor. Se pueden seleccionar o arrastrar hasta 12 adjuntos por lote,
  quitar elementos individualmente y ver un montaje visual local cuando hay
  varias imágenes. El montaje nunca se sube: cada archivo original se envía
  individualmente y en orden.
- El texto escrito junto a un único adjunto se guarda como caption; cuando el
  lote contiene solo imágenes, se adjunta a la primera imagen. Se conserva en
  el historial local,
  incluidas las entregas pendientes y sus reintentos. WebP se transmite en
  formato original como imagen y Pillow solo lo decodifica para la vista cuando
  Qt no lo admite; guardar conserva el archivo original. Lo mismo aplica a
  AVIF, HEIC/HEIF y SVG (`FORMATOS-01`): viajan como imagen, el servidor los
  filtra vía ImageMagick y el cliente los pinta con los plugins de Pillow y
  `QSvgRenderer` (`ui/decodificador_imagen.py`).
- Las imágenes recibidas ofrecen «Guardar imagen» y se copian desde la caché
  local al destino elegido. Los avisos de descarga y envío aparecen dentro del
  chat, se cierran automáticamente y tienen un botón accesible para cerrarlos.
  Cada aviso lleva **severidad** (Error / Atención / Aviso / Listo) con su plazo,
  y se **anuncia** a los lectores de pantalla limpiando y volviendo a poner el
  texto accesible, para que un aviso repetido también se lea. Los fondos de
  conversación claro y oscuro se pintan a cover y centrados en
  el `paintEvent` de la lista — como el CSS del cliente Java — junto al tema
  global de QFluentWidgets. Pintarlos ahí (y no en la paleta del viewport) es
  lo que los hace inmunes a los stylesheets de los paneles vecinos.
- **Descargar un adjunto de más de 1 MiB** llega como secuencia
  `ARCHIVO_INICIO` + `ARCHIVO_PARTE`\* + `ARCHIVO_FIN`, no como una trama
  sola: la reensambla `fachada/servicios/colector_fragmentos.py` (la primera
  trama completaba la espera de la petición y las partes se perdían, dejando un
  archivo de 0 bytes sin error). Lo mismo con un adjunto grande recibido, que el
  servidor ya entrega sin los bytes dentro de la trama (roadmap §5.15).
- La difusión del servidor aparece como mensaje de `SERVIDOR`.
- El cierre del login pide confirmación, y cancelar mantiene abierta la ventana.
  Los ecos de mensajes propios de otras sesiones de la misma cuenta se asocian
  a la conversación indicada por `destinatario`, cuando el servidor los entrega.
- Sin red, los envíos quedan en una cola local y se reintentan solos cada 30 s
  (`INTERVALO_FLUSH_SEGUNDOS`): si la red vuelve, la cola se vacía sin que el
  usuario toque nada. El intento se para al cerrar sesión o desconectar. Un
  pendiente que nunca podrá salir (una imagen mayor que la trama única) se
  marca «! No enviado» y se saca de la cola en vez de tumbar la conexión en
  cada reintento.

## Uso y solución de problemas

- El registro por red está eliminado; solicita al administrador código y contraseña.
- Si no conecta, prueba `Test-NetConnection IP_SERVIDOR -Port 5000` y revisa firewall/IP/puerto.
- Si la UI dice que envió pero no aparece el mensaje, conserva los logs/estado local y verifica el
  acuse y la conexión; no edites SQLite para “forzar” un visto.
- Conserva juntos `main.py`, paquetes (`ui`, `fachada`, `componentes`, `store`, `transversal`,
  `puertos`), `requirements.txt`, `client.properties` y `store/schema-local.sql`.
- Para actualizar, reemplaza los archivos de aplicación pero conserva la base SQLite del usuario.

## Pruebas de desarrollo

Desde esta carpeta y con las dependencias instaladas (pytest está en el grupo `dev` de
`pyproject.toml`):

```powershell
.\.venv\Scripts\python.exe -m pip install pytest
.\.venv\Scripts\python.exe -m pytest tests
```

También funciona con unittest:

```powershell
.\.venv\Scripts\python.exe -m unittest discover -s tests
```

Ultima validación local con unittest: 294 pruebas, 5 omitidas (2026-10-13).

## Revisión visual manual pendiente

La suite automatizada verifica la carga de los fondos y estados de UI en modo
offscreen, pero no sustituye la comprobación en una ventana interactiva. Usa
[`VERIFICACION_VISUAL_CHAT_PENDIENTE.md`](VERIFICACION_VISUAL_CHAT_PENDIENTE.md)
como lista de aceptación y marca allí cada comprobación manual.
