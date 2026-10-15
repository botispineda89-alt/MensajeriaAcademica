"""Literales de ventana y diálogo en un solo sitio (VIS-02).

Fuente de verdad: `contrato/textos-ui.json` (que a su vez sale de
`DIAGRAMAS/textos-ui.md`). `contrato/verificar_textos_ui.py` comprueba que este
módulo contiene el texto exacto del contrato y que ningún otro fuente lo repite
inline. Un diálogo que solo existe aquí (p. ej. el aviso al adjuntar sin
destino) se conserva como está: centralizar no es unificar conductas.
"""

#: Título de la ventana de login.
TITULO_LOGIN = "Mensajería Académica | Iniciar sesión"

#: Título de la ventana de chat: `{identidad}` = `Nombre Apellido [CÓDIGO]` de la
#: cuenta abierta (P4, 2026-10-14). Antes era el código a secas.
TITULO_CHAT = "Mensajería Académica | {identidad}"

#: Título del visor de imagen.
TITULO_VISOR = "Imagen"

#: Título de los diálogos de salida.
CONFIRMAR_SALIDA = "Confirmar salida"

#: Confirmación de volver al inicio.
CERRAR_SESION_PREGUNTA = "¿Deseas cerrar esta sesión y volver al inicio?"

#: Confirmación de cerrar el login.
CERRAR_PROGRAMA_PREGUNTA = "¿Deseas cerrar el programa?"

#: Confirmación de cerrar todas las sesiones.
SALIR_TODOS_PREGUNTA = "¿Cerrar todas las sesiones de esta cuenta?"

#: Aviso tras expulsar otras sesiones.
OTRAS_SESIONES_ESTADO = "Se solicitó cerrar tus otras sesiones"

#: Aviso de envío sin red (queda en cola).
SIN_RED = "Sin red: guardado para reintentar"

#: Título del diálogo de guardado.
GUARDAR_ARCHIVO = "Guardar archivo"

#: Aviso cuando la cita no está en el historial.
CITA_NO_DISPONIBLE = "El mensaje citado no está disponible en este historial."

#: Cabecera sin conversación.
SELECCIONA_CONVERSACION = "Selecciona una conversación"

#: Botón de responder de la burbuja (§5: el botón se conserva en los 3; Java lo
#: dibuja como `↩` con su texto accesible). Python solo tenía el doble clic.
BOTON_RESPONDER = "Responder"

#: Aviso al adjuntar sin destino.
SELECCIONA_CONVERSACION_PRIMERO = "Selecciona una conversación primero."

#: Título de ese aviso.
TITULO_SELECCIONAR_CONVERSACION = "Seleccionar conversación"

#: Título del error sin identificador (Java/.NET usan otro mecanismo).
DESCARGA_ERROR_TITULO = "No se pudo descargar"

#: Cuerpo del error sin identificador.
DESCARGA_SIN_ID = "El identificador del archivo no está disponible."

#: Fila de contacto sin mensajes.
SIN_MENSAJES_FILA = "Sin mensajes aún"

#: Placeholder de conversación vacía (guía UIX-13, distinto de la fila).
VACIO_CHAT = "Sin mensajes aún. Escribe el primero."

#: Botón de paginación del historial.
CARGAR_ANTERIORES = "↑ Cargar mensajes anteriores"

#: Motivo por defecto al cerrar sesión remota (difiere de Java/.NET a propósito).
SESION_CERRADA_DEFECTO = "Sesión cerrada por el servidor."
