"""Prefijo de respuesta y separación cita/cuerpo (paridad Java/.NET).

La respuesta viaja como TEXTO ORDINARIO con el prefijo
`Respuesta a Nombre [CÓDIGO]: "…"` (invariante 9): sin vínculo estructurado al
mensaje original. Los tres clientes separan la cita del cuerpo con la misma
regla para pintarla como bloque: Java en `MensajeCell.extraerCita`, .NET en
`Transversal/FormatoCita.Separar` y aquí.

Regla (idéntica en los tres): la cita es lo anterior al PRIMER salto doble,
solo si el texto empieza por el prefijo y el corte no supera `CITA_CORTE`. El
corte existe porque un mensaje normal puede contener un salto doble cualquiera;
sin tope, cualquier texto largo que empiece por esas palabras se partiría mal.
"""

#: Lo anterior al primer salto doble solo es cita si empieza así.
PREFIJO = "Respuesta a "

#: Tope del bloque de cita (`MensajeCell.CITA_CORTE`, `FormatoCita.Corte`).
CITA_CORTE = 300


def separar(texto):
    """Devuelve `(cita_o_None, cuerpo)`. Si no hay cita, todo es cuerpo."""
    cuerpo = (texto or "").replace("\r\n", "\n")
    if not cuerpo:
        return None, ""
    corte = cuerpo.find("\n\n")
    if corte > 0 and corte < CITA_CORTE and cuerpo.startswith(PREFIJO):
        return cuerpo[:corte].strip(), cuerpo[corte + 2:].strip()
    return None, cuerpo


def extracto(cita):
    """Texto entrecomillado de la cita (`"…"`) o "" si no lo hay.

    Es lo que se busca en el historial para navegar al mensaje original: la
    cita no lleva el id, solo un extracto de 120 caracteres como mucho.
    """
    if not cita:
        return ""
    inicio = cita.find('"')
    fin = cita.rfind('"')
    if inicio < 0 or fin <= inicio:
        return ""
    return cita[inicio + 1:fin].strip()
