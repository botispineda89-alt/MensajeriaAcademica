# Despliegue del cliente .NET / WPF

Cliente de escritorio Windows, objetivo `net8.0-windows`, WPF UI y SQLite. En ejecución no requiere
el monorepo, Maven, Python ni conexión directa a MySQL.

## Publicación para un equipo Windows x64

Desde la raíz `universidad-mensajeria/`, en una máquina de compilación Windows con .NET SDK 8 o
posterior:

```powershell
dotnet publish client-dotnet/Mensajeria.csproj `
  -c Release -r win-x64 --self-contained true `
  -o client-dotnet/bin/publish-win-x64
```

La primera publicación restaura paquetes NuGet y el runtime Windows. Copia la carpeta completa
`client-dotnet/bin/publish-win-x64` al PC destino; incluye `Mensajeria.exe`, runtime, dependencias,
`client.properties`, `Store/schema-local.sql` y este README. No copies solo el `.exe` ni `bin/Debug`.

Dos paquetes sostienen el trabajo con imágenes: **`SkiaSharp` + `SkiaSharp.NativeAssets.Win32`
4.153.1 (MIT)**, que convierte y compone las imágenes sin depender de WPF —por eso la lógica es
testeable desde la suite—. Se descartó `SixLabors.ImageSharp`: la 4.x exige licencia comercial y la
3.x arrastra avisos NU1902/NU1903 en cada compilación. Verifica que `libSkiaSharp.dll` está en la
publicación.

La auditoría NuGet actual de tests advierte gravedad alta en la dependencia transitiva
`SQLitePCLRaw.lib.e_sqlite3 2.1.6` (vía `Microsoft.Data.Sqlite 8.0.10`, fijado a propósito).
Decisión vigente: se mantiene la versión fijada y la alerta queda como riesgo conocido a
revisar antes de un despliegue de producción; no se actualiza sin probar la suite completa.

En el destino, inicia `Mensajeria.exe`. Al ser self-contained no requiere instalar .NET Desktop
Runtime. Para publicar en ARM64 o una plataforma distinta, sustituye el Runtime Identifier (`-r`)
y valida WPF en esa plataforma; WPF es exclusivamente Windows.

## Desarrollo local

Desde la raíz del proyecto:

```powershell
dotnet run --project client-dotnet/Mensajeria.csproj
```

El proyecto de UI compila contra `net8.0-windows`. La suite actual de tests apunta a `net10.0`, por
lo que ejecutarla requiere .NET SDK 10:

```powershell
dotnet test client-dotnet.Tests/Mensajeria.Tests.csproj
dotnet build client-dotnet/Mensajeria.csproj -c Release
```

## Configuración y red

En publicación, `client.properties` queda junto al ejecutable. Modifica ese archivo antes de
iniciar:

```properties
host=192.168.1.20
puerto=5000
db.url=sqlite:///~/mensajeria_cliente.db
db.user=
db.pass=
idioma=es
adjuntos.lote-max-mb=200
```

`adjuntos.lote-max-mb` (10–1000 MiB; defecto 200) solo muestra una advertencia cuando un lote
supera esa suma; el tope duro de 50 MiB se aplica a cada archivo.

Configura IP LAN/DNS del servidor y el puerto TCP `red.puerto`; `localhost` solo vale en una
instalación local. Permite salida TCP del cliente hacia el servidor. No abras un puerto de entrada
ni configures MySQL en este equipo.

La base SQLite es local al perfil del usuario, con sufijo por cuenta; instancias del mismo usuario
en el mismo PC pueden compartirla. La migración del archivo legacy es aditiva. Las imágenes
descargadas se guardan bajo `%LOCALAPPDATA%\Mensajeria\imagenes`; no elimines esa carpeta si deseas
conservar archivos locales.

## Operación

- El alta de usuarios se realiza en el servidor; la UI no ofrece registro.
- El acceso muestra servidor y puerto (de `client.properties`).
- El directorio trae todos los usuarios como `Nombre Apellido [CÓDIGO]`,
  con filtro Todos/Conectados y buscador. La conversación va en orden
  cronológico (los nuevos abajo) y cada burbuja muestra la fecha abajo
  (`aaaa-MM-dd HH:mm`) junto a Responder.
- Se pueden seleccionar varios adjuntos a la vez o arrastrarlos sobre la redacción o la lista de
  mensajes. Se muestran en una bandeja donde se pueden quitar individualmente o en conjunto;
  los envíos fragmentados son secuenciales, muestran progreso por bytes y permiten cancelación.
  El límite por archivo es 50 MiB; las imágenes enviadas directamente se limitan a 12 MiB.
- El texto escrito junto a una imagen o a un archivo viaja como **pie de la misma burbuja**, no como un
  segundo mensaje. Varias imágenes se componen en un montaje y salen como **una sola burbuja**.
- Los adjuntos se pintan con el **icono de su tipo** (`FORMATOS-02`): PDF, hoja de cálculo, CSV, JSON o
  documento, tomado de `Iconos/` junto al ejecutable y con el mismo dibujo en los tres clientes
  (`contrato/verificar_iconos.py`). En la burbuja también aparece el **nombre del archivo**, que antes
  no salía. La extensión sin dibujo se queda con el icono de texto de siempre.
- Las imágenes, incluidos los `.webp`, se envían **tal cual** (el servidor las acepta y les aplica los
  cinco filtros). El cliente solo genera una copia PNG para pintarlas; "Guardar" entrega siempre el
  archivo original. Si un `.webp` llega ilegible, el servidor lo rechaza con un motivo claro: el
  cliente no lo degrada a documento. Lo mismo aplica a AVIF, HEIC/HEIF y SVG (`FORMATOS-01`):
  viajan como imagen, el servidor los filtra vía ImageMagick y la vista los pinta con Magick.NET.
- Si no hay conexión, prueba `Test-NetConnection IP_SERVIDOR -Port 5000` antes de reinstalar.
- Los avisos/errores recuperables se muestran en un banner inline que se puede cerrar y **se retira
  solo** a los 4-12 s según el tipo; el cierre remoto aparece en el banner del login. Se conservan
  tres confirmaciones modales: salir del login, cerrar la sesión del chat y cerrar las demás sesiones.
- Actualiza el directorio publicado conservando `client.properties`, SQLite del perfil e imágenes.
- Enviar texto, imagen o ambos no implica que la otra persona ya lo recibió: confirma los estados
  de entrega/lectura en la conversación.
