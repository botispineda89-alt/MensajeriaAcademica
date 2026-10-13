"""UIX-03: formato de fecha unificado y relativo en los 3 clientes.

Réplica exacta de `FormatoFecha.java` y `FormatoFecha.cs`. Antes la burbuja
pintaba la cadena cruda (`2026-01-01 10:00:00` o `...T10:00:00` mezclados) y un
mensaje de ayer parecía de hoy. Contrato compartido (VIS-02):
"Hoy 10:00", "Ayer 10:00", "02-01 10:00", "02-01-2025 10:00".

Acepta `yyyy-MM-ddTHH:mm:ss` (red), `yyyy-MM-dd HH:mm:ss` (SQLite) y cadenas
con zona horaria: la base SQLite la comparten los clientes por cuenta y el
cliente .NET escribe UTC con sufijo `Z` (`DateTime.ToString("O")`).
Lo ilegible devuelve "" en vez de romper la fila.
"""
import datetime


def _sin_zona(fecha):
    """Deja el datetime naive en hora local (como `TryLeer` de .NET).

    Las filas escritas por Python/Java son naive y las del cliente .NET llegan
    aware; sin esta normalización mezclar ambas rompe con
    `TypeError: can't subtract offset-naive and offset-aware datetimes`.
    """
    if fecha.tzinfo is None:
        return fecha
    return fecha.astimezone().replace(tzinfo=None)


def _leer(fecha_envio):
    if not fecha_envio or not str(fecha_envio).strip():
        return None
    texto = str(fecha_envio).strip()
    # fromisoformat acepta 'T' o espacio, microsegundos y zona horaria.
    # El reemplazo de la 'Z' final es seguro en Python moderno y necesario
    # en 3.9/3.10, donde fromisoformat no entendía el sufijo UTC.
    if texto.endswith("Z"):
        texto = texto[:-1] + "+00:00"
    try:
        return _sin_zona(datetime.datetime.fromisoformat(texto))
    except ValueError:
        pass
    for formato in ("%Y-%m-%dT%H:%M:%S", "%Y-%m-%dT%H:%M", "%Y-%m-%d"):
        try:
            return _sin_zona(datetime.datetime.strptime(texto[:19], formato))
        except ValueError:
            continue
    return None


def a_utc(fecha_envio):
    """Normaliza una fecha a ISO-8601 UTC con sufijo ``Z`` (UIX-26).

    Réplica de ``FormatoFecha.AUtc`` del cliente .NET. ``fecha_envio`` es
    TEXT y las consultas ordenan por ella COMO TEXTO: el servidor manda hora
    local sin zona y .NET guarda UTC con ``Z``, así que escribir aquí hora
    local hacía que, en la base compartida por cuenta, los mensajes propios
    recientes quedaran ordenados DETRÁS de filas .NET más viejas y
    desaparecieran de la primera página del historial. Sin zona se interpreta
    hora local (el servidor y el cliente están en la misma máquina en este
    despliegue). Lo ilegible se devuelve tal cual para no perder la fila.
    """
    if fecha_envio is None:
        return ""
    if isinstance(fecha_envio, datetime.datetime):
        leida = fecha_envio
    else:
        texto = str(fecha_envio).strip()
        if not texto:
            return ""
        if texto.endswith("Z"):
            # Ya está en UTC con Z (filas de .NET): no se cicla por datetime
            # otra vez — Python solo guarda 6 decimales y mutilaría el
            # séptimo que el formato "O" de .NET sí lleva.
            return texto
        leida = _leer(texto)
        if leida is None:
            return texto
    if leida.tzinfo is None:
        leida = leida.astimezone()
    utc = leida.astimezone(datetime.timezone.utc)
    # El "O" de .NET: 7 decimales + Z (`DateTime.ToString("O")`), el mismo
    # formato byte a byte de las filas que ya escribe el cliente .NET.
    return (utc.strftime("%Y-%m-%dT%H:%M:%S")
            + f".{utc.microsecond:07d}Z")


def ahora_utc():
    """Fecha de envío de las filas que este cliente guarda: UTC con ``Z``."""
    return a_utc(datetime.datetime.now())


def hora_corta(fecha_envio):
    """Hora corta para el pie de la burbuja."""
    fecha = _leer(fecha_envio)
    if fecha is None:
        return ""
    hoy = datetime.date.today()
    dia = fecha.date()
    hora = fecha.strftime("%H:%M")
    if dia == hoy:
        return hora
    if dia.year == hoy.year:
        return fecha.strftime("%d-%m") + " " + hora
    return fecha.strftime("%d-%m-%Y") + " " + hora


def etiqueta_dia(fecha_envio):
    """Etiqueta para el separador de día (UIX-04)."""
    fecha = _leer(fecha_envio)
    if fecha is None:
        return ""
    hoy = datetime.date.today()
    dia = fecha.date()
    if dia == hoy:
        return "Hoy"
    if dia == hoy - datetime.timedelta(days=1):
        return "Ayer"
    if dia.year == hoy.year:
        return fecha.strftime("%d-%m")
    return fecha.strftime("%d-%m-%Y")


def clave_dia(fecha_envio):
    """Clave de agrupación por día (UIX-04): "" si ilegible."""
    fecha = _leer(fecha_envio)
    return "" if fecha is None else fecha.date().isoformat()


VENTANA_RACHA_MINUTOS = 5


def agrupar(anterior, actual):
    """UIX-05: ¿el mensaje actual va en la racha del anterior (sin remitente)?

    Mismo contrato que `FilasChat.agrupar` (Java) y `DebeMostrarEncabezado`
    (.NET, negado): mismo `origen`, mismo día y menos de 5 minutos.
    `anterior` y `actual` son las filas crudas (`origen`, `fecha_envio`).
    Con fecha ilegible no se agrupa: mostrar el remitente es lo seguro.
    """
    if not anterior or not actual:
        return False
    if (anterior.get("origen") or "") != (actual.get("origen") or ""):
        return False
    fa = _leer(anterior.get("fecha_envio"))
    fb = _leer(actual.get("fecha_envio"))
    if fa is None or fb is None:
        return False
    if fa.date() != fb.date():
        return False
    return abs((fb - fa).total_seconds()) < VENTANA_RACHA_MINUTOS * 60
