@echo off
REM ============================================================
REM distribuir-todos.cmd — las 4 distribuciones en un solo paso.
REM Fase 6 de PLAN_PARIDAD_CLIENTES.md: mismos assets en los 3
REM clientes (fondos, iconos, schema-local.sql, client.properties).
REM Ejecutar desde universidad-mensajeria/
REM ============================================================
setlocal
echo === [1/4] Servidor (Spring Boot fat jar) ===
call dist\distribuir-servidor.cmd || goto :fallo

echo === [2/4] Cliente JavaFX ===
call client\dist-client.cmd || goto :fallo

echo === [3/4] Cliente Python ===
call client-python\distribuir.cmd || goto :fallo

echo === [4/4] Cliente WPF (dotnet publish) ===
dotnet publish client-dotnet\Mensajeria.csproj -c Release -o dist\MensajeriaWPF || goto :fallo

echo.
echo === Las 4 distribuciones están en dist\ ===
exit /b 0

:fallo
echo.
echo ERROR: falló una de las distribuciones. Revisar la salida de arriba.
exit /b 1
