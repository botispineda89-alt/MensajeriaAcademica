"""Entrega en el hilo de UI de resultados de tareas asíncronas.

PAT-VM: el reparto por tipo de tarea es una **tabla**, no una cadena de
``if/elif``. Antes eran dos (una para el error y otra para el éxito) de unas
dieciocho ramas cada una: añadir un tipo significaba tocar las dos, y olvidar
una rama no daba ningún aviso, solo un resultado que no se pintaba. Con la
tabla, un tipo sin manejador se ve en el test y los manejadores son métodos
pequeños con nombre.

Las dos tablas son de clase y se construyen con los nombres de los métodos, así
que funcionan igual en la ventana y en cualquier doble de prueba.
"""

from pathlib import Path

from PySide6.QtGui import QPixmap

from transversal.textos import (
    OTRAS_SESIONES_ESTADO,
    SIN_RED,
)
from ui.estado_aviso_inline import TipoAviso


class ProcesadorTareasChat:
    """Reparte el resultado de cada tarea al manejador de su tipo."""

    #: tipo de tarea -> método que recibe (resultado, error).
    MANEJADORES = {
        "imagen": "_entregar_imagen",
        "envio": "_entregar_envio",
        "descarga": "_entregar_descarga",
        "directorio": "_entregar_directorio",
        "historial": "_entregar_historial",
        "historial_remoto": "_entregar_historial_remoto",
        "pagina_anterior": "_entregar_pagina_anterior",
        "salir_todos": "_entregar_salir_todos",
        "kick_otras": "_entregar_kick_otras",
        "marcar_leidos": "_entregar_marcar_leidos",
    }

    def _procesar_tarea(self, tipo, resultado, error):
        if self._cerrando:
            return
        manejador = self.MANEJADORES.get(tipo)
        if manejador is None:
            # Un tipo sin manejador es un fallo de desarrollo, no del usuario:
            # dejarlo pasar en silencio hace perder el resultado sin rastro.
            raise KeyError(f"tarea sin manejador en el chat: {tipo!r}")
        getattr(self, manejador)(resultado, error)

    # ------------------------------------------------------------------- error
    def _entregar_imagen(self, resultado, error):
        if error is None:
            mensaje_id, imagen, ruta = resultado
            # UIX-09: resultado de una carga cancelada: se descarta sin tocar
            # la UI (ya es otro chat) y se limpia la marca.
            self._cargas_imagen.pop(mensaje_id, None)
            if mensaje_id in self._imagen_canceladas:
                self._imagen_canceladas.discard(mensaje_id)
                return
            mensaje = self._mensaje_por_id(mensaje_id)
            if mensaje is not None:
                self._delegado_mensajes.guardar_pixmap(
                    mensaje_id, QPixmap.fromImage(imagen))
                mensaje["image_state"] = Path(ruta).name
                mensaje["image_loaded"] = True
                mensaje["image_loading"] = False
                mensaje["image_error"] = ""
                self._modelo_mensajes.refrescar(mensaje_id)
            return

        mid = getattr(error, "mensaje_id", "")
        self._cargas_imagen.pop(mid, None)
        # UIX-09: error de una carga cancelada: no se ofrece reintentar
        # algo que el usuario ya dejó atrás.
        if mid in self._imagen_canceladas:
            self._imagen_canceladas.discard(mid)
            return
        row = self._row_por_id(mid)
        if row is not None:
            row["image_loading"] = False
            row["image_error"] = str(error)
            self._modelo_mensajes.refrescar(mid)

    def _entregar_envio(self, resultado, error):
        if error is not None:
            self.modelo_vista.enviando = False
            self._aviso_red(f"No se pudo enviar: {error}")
            return

        usuario, _, ids_enviados, avisos, estados, restantes = resultado
        self.modelo_vista.enviando = False
        self._ocultar_progreso_envio()
        if any(estado is False for estado in estados.values()):
            self.lbl_estado_red.setText(SIN_RED)
        for mid in ids_enviados:
            mensaje = self._mensaje_por_id(mid)
            if mensaje is not None and mensaje.get("raw_state") == "PENDIENTE":
                if estados.get(mid) is True:
                    mensaje["state"] = self._estado_visible("ENVIADO")
                    mensaje["raw_state"] = "ENVIADO"
                elif estados.get(mid) is None:
                    mensaje["state"] = self._estado_visible("ERROR")
                    mensaje["raw_state"] = "ERROR"
                mensaje["image_loading"] = False
                self._modelo_mensajes.refrescar(mid)
        self._cargar_imagenes_visibles()
        if restantes:
            # Cancelación: los adjuntos no enviados vuelven a la bandeja
            # para reenviarlos; el texto del pie vuelve al compositor.
            self.bandeja_adjuntos.agregar(
                [r["ruta"] for r in restantes if r.get("ruta")])
            texto_devuelto = next(
                (r.get("caption") for r in restantes if r.get("caption")), None)
            if texto_devuelto:
                self.entry_msg.setPlainText(texto_devuelto)
            self._aviso_red(
                f"Envío cancelado: {len(restantes)} adjuntos volvieron a la bandeja.",
                TipoAviso.ADVERTENCIA)
        elif avisos:
            # La lista mezcla fallos duros con "quedó en cola para reintentar":
            # si hay alguno duro, el aviso sube a error; si no, es informativo.
            hay_fallo = any("No se pudo" in aviso for aviso in avisos)
            self._aviso_red("\n".join(avisos),
                            TipoAviso.ERROR if hay_fallo else TipoAviso.INFORMACION)
        self._solicitar_historial(usuario, 0)
        # Mi envío también es "último mensaje": la tarjeta sube arriba.
        self._programar_refresco_recientes()

    def _entregar_descarga(self, resultado, error):
        if error is not None:
            mid = getattr(error, "mensaje_id", "")
            mensaje = self._mensaje_por_id(mid)
            if mensaje is not None:
                mensaje["descargando"] = False
                self._modelo_mensajes.refrescar(mid)
            self._aviso_red(f"No se pudo descargar el archivo: {error}")
            return

        mensaje_id, ruta_guardada = resultado
        mensaje = self._mensaje_por_id(mensaje_id)
        if mensaje is not None:
            mensaje["descargando"] = False
            mensaje["descargado_ruta"] = ruta_guardada
            self._modelo_mensajes.refrescar(mensaje_id)
        self.aviso_inline.mostrar(f"Archivo guardado en: {ruta_guardada}",
                                  TipoAviso.EXITO)

    def _entregar_historial(self, resultado, error):
        if error is not None:
            self.lbl_estado_red.setText("No se pudo cargar el historial local")
            self._cargando_historial = False
            self._actualizar_vacio_chat()
            return

        usuario, filas, offset, total, gen = resultado
        if usuario != self.usuario_seleccionado or gen != self._gen_historial:
            return
        if usuario == self.usuario_seleccionado:
            self._pintar_historial(filas, prepend=offset > 0)
            self._offset = offset
            hay_local = offset + 50 < total
            # UIX-06: si hay un salto pendiente (búsqueda), se reintenta
            # ahora que hay más mensajes cargados.
            pendiente = self.modelo_vista.consumir_salto()
            if pendiente is not None:
                pendiente, intentos = pendiente
                self._saltar_a(pendiente, intentos)
            hay_remoto = self._pagina_remota.get(usuario, 0) + 1 < self._total_remoto.get(usuario, 0)
            self.btn_anteriores.setVisible(hay_local or hay_remoto)
            self._cargar_imagenes_visibles()

    def _entregar_historial_remoto(self, resultado, error):
        if error is not None:
            self.lbl_presencia.setText("Mostrando historial local")
            return

        usuario, pagina, total_paginas, older, gen = resultado
        if usuario != self.usuario_seleccionado or gen != self._gen_historial:
            return
        self._pagina_remota[usuario] = pagina
        self._total_remoto[usuario] = total_paginas
        if self.usuario_seleccionado == usuario:
            self._solicitar_historial(usuario, self._offset if older else 0)

    def _entregar_directorio(self, resultado, error):
        if error is not None:
            self._aviso_red(f"No se pudo cargar el directorio: {error}",
                            TipoAviso.ERROR)
            return

        gen, usuarios, no_leidos, error_red = resultado
        if gen != self._gen_directorio:
            # Refresh obsoleto (el usuario cambio de chat o filtro
            # mientras la red respondia): no pisa la seleccion viva.
            return
        propio = next((u for u in usuarios if u.get("codigo") == self.codigo), None)
        if propio is not None:
            nombre_propio = " ".join(filter(None, [propio.get("nombres"),
                                                    propio.get("apellidos")])).strip()
            identidad_propia = (f"{nombre_propio} [{self.codigo}]"
                                if nombre_propio else self.codigo)
            self.lbl_sesion.setText(f"Mensajería\nSesión activa · {identidad_propia}")
        self._directorio = [u for u in usuarios if u.get("codigo") != self.codigo]
        self._no_leidos = dict(no_leidos or {})
        # Tarjetas: recientes por fecha con preview (de caché local; la
        # tarea ya la actualizó por red). Sin `recientes()` en la fachada,
        # `_filtrar_usuarios` usa el directorio plano (dobles de prueba).
        recientes = self._recientes_de_fachada()
        if recientes is not None:
            self._recientes = recientes
            self._no_leidos = {r.otro: r.no_leidos for r in recientes}
        else:
            self._recientes = None
        # Restaura la seleccion VIVA (no la capturada al pedir): si el
        # usuario cambio de chat o escribio un filtro mientras tanto,
        # un refresh obsoleto jamas lo mueve de conversacion.
        self._huella_pintada = None
        self._filtrar_usuarios(seleccionado=self.usuario_seleccionado)
        if self._recientes is not None:
            self._huella_pintada = self._huella_recientes(self._recientes)
        self._restaurar_ultima_conversacion()
        online = sum(bool(u.get("conectado")) for u in self._directorio)
        sin_red = error_red is not None
        self.lbl_offline.setVisible(sin_red)
        if sin_red:
            self.lbl_estado_red.setText("Sin conexión: directorio local · cola activa")
            self.lbl_estado_red.setStyleSheet("color: #B45309; font-size: 11px; font-weight: 700;")
        else:
            self.lbl_estado_red.setText(f"{online} contactos en línea")
            self.lbl_estado_red.setStyleSheet("color: #61717F; font-size: 11px;")

    def _entregar_pagina_anterior(self, resultado, error):
        if error is not None:
            return

        usuario, destino, pagina = resultado
        if usuario != self.usuario_seleccionado:
            return
        if destino == "local":
            self._solicitar_historial(usuario, pagina)
        elif destino == "remoto":
            self._solicitar_historial_remoto(usuario, pagina, True)

    def _entregar_salir_todos(self, resultado, error):
        if error is not None:
            self._aviso_red(f"No se pudo cerrar todas las sesiones: {error}",
                            TipoAviso.ERROR)
            return

        self._cerrando = True
        self.fachada.on_mensaje_recibido_ui = None
        self.fachada.on_cierre_ui = None
        self.fachada.on_presencia_ui = None
        self.fachada.on_desconexion_ui = None
        self.fachada.desconectar()
        self.on_logout()
        self.close()

    def _entregar_kick_otras(self, resultado, error):
        if error is not None:
            self._aviso_red(f"No se pudo expulsar: {error}", TipoAviso.ERROR)
            return

        self.lbl_estado_red.setText(OTRAS_SESIONES_ESTADO)

    def _entregar_marcar_leidos(self, resultado, error):
        """Marcar como leídos no pinta nada: la insignia la baja el directorio.

        Antes esta tarea caía al final de la cadena sin hacer nada; con la tabla
        hace falta el manejador explícito, o el reparto la rechazaría. Un fallo
        aquí tampoco molesta al usuario: el próximo refresco del directorio
        vuelve a pedir los no leídos y lo corrige.
        """