@echo off
rem DIST-02: carpeta del servidor central (Windows x64).
rem Salida: dist/MensajeriaServidor/ (servidor.jar + start-server.cmd + config/ + uploads/ + logs/ + README.md + version.txt)
rem Uso: server\distribuir-servidor.cmd   (se ejecuta desde la raiz universidad-mensajeria)
setlocal EnableDelayedExpansion

cd /d "%~dp0\.."
if not exist "pom.xml" (
    echo [ERROR] Falta pom.xml: ejecutar desde universidad-mensajeria.
    exit /b 1
)

set DIST=dist\MensajeriaServidor
set JAR=servidor-app-1.0.0-SNAPSHOT.jar

rem `install` y no `package`: los modulos hermanos (dto, codec, int-*, comp-*)
rem se resuelven desde el repo local, y `package` no los deja alli.
echo [1/5] Compilando servidor...
call mvn -pl server/servidor-app -am install -DskipTests
if errorlevel 1 (
    echo [ERROR] Fallo la compilacion del servidor.
    exit /b 1
)

echo [2/5] Copiando JAR...
if exist "%DIST%" rmdir /s /q "%DIST%"
mkdir "%DIST%\config" "%DIST%\uploads" "%DIST%\logs" 2>nul
copy /y "server\servidor-app\target\%JAR%" "%DIST%\servidor.jar" >nul
if errorlevel 1 (
    echo [ERROR] No se hallo %JAR%.
    exit /b 1
)

echo [3/5] Copiando configuracion editable...
copy /y "server\servidor-app\src\main\resources\server.properties" "%DIST%\config\server.properties" >nul
copy /y "server\servidor-app\src\main\resources\usuarios_iniciales.csv" "%DIST%\config\usuarios_iniciales.csv" >nul

echo [4/5] Generando lanzador y README...
> "%DIST%\start-server.cmd" echo @echo off
>> "%DIST%\start-server.cmd" echo rem Servidor de mensajeria academica: consola JLine interactiva (opcion 7 = Iniciar).
>> "%DIST%\start-server.cmd" echo rem Edita config\server.properties (puerto, limites) y config\usuarios_iniciales.csv (altas).
>> "%DIST%\start-server.cmd" echo rem Clave MySQL por entorno: set DB_MYSQL_PASS=...  Otra URL: application.properties junto al JAR.
>> "%DIST%\start-server.cmd" echo setlocal
>> "%DIST%\start-server.cmd" echo cd /d "%%~dp0"
>> "%DIST%\start-server.cmd" echo where java ^>nul 2^>^&1
>> "%DIST%\start-server.cmd" echo if errorlevel 1 ^(echo [ERROR] Falta Java 21+ en el PATH. ^& exit /b 1^)
>> "%DIST%\start-server.cmd" echo set SPRING_CONFIG_ADDITIONAL_LOCATION=file:config/server.properties
>> "%DIST%\start-server.cmd" echo java -jar servidor.jar --app.seed=file:config/usuarios_iniciales.csv

> "%DIST%\README.md" echo # Servidor de mensajeria academica 1.0.0
>> "%DIST%\README.md" echo.
>> "%DIST%\README.md" echo ## Requisitos en este PC
>> "%DIST%\README.md" echo - JRE 21 o superior en el PATH.
>> "%DIST%\README.md" echo - MySQL 8 accesible (Docker o instalado). Por defecto: localhost:3307.
>> "%DIST%\README.md" echo - ImageMagick 7 para AVIF, HEIC y SVG (sin el, esos formatos se rechazan).
>> "%DIST%\README.md" echo - Abrir TCP 5000 entrante solo a la LAN de clientes. No abrir MySQL a la LAN.
>> "%DIST%\README.md" echo.
>> "%DIST%\README.md" echo ## Configurar
>> "%DIST%\README.md" echo - `config\server.properties`: `red.puerto`, limites, `red.upload-dir`.
>> "%DIST%\README.md" echo - `config\usuarios_iniciales.csv`: altas y bajas (es la unica via; no hay registro por red).
>> "%DIST%\README.md" echo - Clave MySQL: variable `DB_MYSQL_PASS`. Otra URL/motor: `application.properties` junto al JAR.
>> "%DIST%\README.md" echo.
>> "%DIST%\README.md" echo ## Arrancar
>> "%DIST%\README.md" echo - `start-server.cmd` (menu: opcion 7 Iniciar). Flyway migra solo.
>> "%DIST%\README.md" echo.
>> "%DIST%\README.md" echo ## Respaldo
>> "%DIST%\README.md" echo - Respaldar juntos MySQL y `uploads\` (ver DESPLIEGUE.md del monorepo).
> "%DIST%\version.txt" echo MensajeriaServidor 1.0.0 %DATE% %TIME%

echo [5/5] Verificando distribucion...
dir /b "%DIST%" | findstr /c:"servidor.jar" /c:"start-server.cmd" /c:"README.md" >nul
if errorlevel 1 (
    echo [ERROR] Faltan archivos en %DIST%.
    exit /b 1
)
if not exist "%DIST%\config\server.properties" (
    echo [ERROR] Falta %DIST%\config\server.properties.
    exit /b 1
)
if not exist "%DIST%\config\usuarios_iniciales.csv" (
    echo [ERROR] Falta %DIST%\config\usuarios_iniciales.csv.
    exit /b 1
)

echo [OK] Distribucion lista en %DIST% (edita config\ con la IP/puerto LAN y DB_MYSQL_PASS).
exit /b 0
