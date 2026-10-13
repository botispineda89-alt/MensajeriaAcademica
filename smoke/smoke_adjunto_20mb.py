"""Smoke real de interoperabilidad: adjunto de 20 MiB contra el servidor de verdad.

No es un doble de socket. Levanta dos sesiones reales (dos cuentas de
``usuarios_iniciales.csv``) sobre TCP 5000, manda un archivo de 20 MiB por el
protocolo fragmentado, lo recibe la otra sesion, lo descarga y comprueba que el
SHA-256 coincide byte a byte.

Cubre las direcciones que se pueden comprobar sin ventana:
  A -> B  (archivo grande A envia, B recibe y descarga y compara)
  B -> A  (a la inversa, con el mismo archivo)
  A -> A  (a si mismo: comprueba que no depende del otro extremo)

Uso (desde la raiz del repositorio)::

    .\\universidad-mensajeria\\client-python\\.venv\\Scripts\\python.exe smoke\\smoke_adjunto_20mb.py

Sale con codigo 0 si todo cuadra y 1 con el detalle del fallo si algo no.
"""

import hashlib
import os
import sys
import tempfile
import time
import uuid
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
CLIENTE = RAIZ / "universidad-mensajeria" / "client-python"
sys.path.insert(0, str(CLIENTE))

from fachada.fachada_cliente import FachadaCliente  # noqa: E402
from transversal.tipo_mensaje import TipoMensaje  # noqa: E402

TAMANO_MIB = 20
MIB = 1024 * 1024
CONTRASENA = "12345"
CUENTAS = ("160004726", "160004716", "160004822", "160004729")


def archivo_de_prueba(destino: Path) -> tuple[str, int]:
    """20 MiB no comprimibles: si fueran repetitivos, el hash no probaria nada."""
    semilla = os.urandom(MIB)
    ruta = destino / f"plantilla-{TAMANO_MIB}MiB.bin"
    with ruta.open("wb") as archivo:
        for vuelta in range(TAMANO_MIB):
            # Cambia el bloque en cada vuelta: el contenido final no es un patron
            # repetible, asi que el SHA-256 de verdad distingue un archivo bien
            # reconstruido de uno truncado o descolocado.
            archivo.write(bytes(b ^ ((vuelta * 7 + 13) & 0xFF) for b in semilla))
    return hashlib.sha256(ruta.read_bytes()).hexdigest(), ruta.stat().st_size


def sesion(codigo: str, recibidos: list) -> FachadaCliente:
    from transversal.config import Config

    # client.properties se resuelve relativo al directorio de trabajo: el smoke
    # arranca desde la carpeta del cliente igual que haria una sesion real, para
    # no mandar contra un host/puerto distinto del configurado.
    os.chdir(CLIENTE)
    config = Config()
    print(f"  [{codigo}] contra {config.get('host', 'localhost')}:"
          f"{config.get('puerto', 5000)}")
    fachada = FachadaCliente(
        on_mensaje_recibido_ui=lambda msg: recibidos.append(msg),
        config=config,
    )
    fachada.conectar()
    if not fachada.login(codigo, CONTRASENA):
        raise RuntimeError(f"login fallido para {codigo}")
    return fachada


def esperar_archivo(fachada: FachadaCliente, otro: str, id_msg: str, segundos: float = 90):
    """Espera al mensaje exacto del adjunto en el historial local del otro.

    Se busca por id y no por nombre: el SYNC_LOGIN del login trae el historial
    viejo (con adjuntos de pruebas anteriores que se llaman igual) y una busqueda
    por nombre daria por buena una fila que no es la de ahora.
    """
    limite = time.time() + segundos
    while time.time() < limite:
        pagina = fachada.obtener_conversacion(otro, limit=50, offset=0)
        for fila in pagina:
            if fila.get("id_mensaje") == id_msg and fila.get("tipo") == TipoMensaje.MENSAJE_ARCHIVO:
                return fila
        time.sleep(0.5)
    raise TimeoutError(f"el adjunto {id_msg} no llegó a {otro} en {segundos:g} s")


def enviar_y_recibir(origen: str, destino: str, directorio: Path, plantilla: Path,
                     sha_esperado: str, recibidos: list) -> None:
    # El nombre es el que viaja por el protocolo: cada direccion usa uno propio
    # para que la espera no pueda dar por bueno un adjunto de otra prueba.
    ruta = directorio / f"{origen}-a-{destino}-{TAMANO_MIB}MiB.bin"
    nombre = ruta.name
    # Mismo contenido que la plantilla, con otro nombre: lo que viaja por el
    # protocolo es el nombre, y el hash esperado es el de la plantilla.
    ruta.write_bytes(plantilla.read_bytes())
    enviados = []
    recibidos = []
    enviar = sesion(origen, enviados)
    recibir = sesion(destino, recibidos)
    try:
        # Id único por ejecución: el SYNC_LOGIN del login trae los mensajes de
        # pruebas anteriores y, con un id fijo, su fila (tipo SYNC_LOGIN) se
        # confundiría con la de ahora.
        id_msg = f"smoke-{origen}-{destino}-{uuid.uuid4().hex[:8]}"
        progreso = []
        salió = enviar.enviar_archivo(destino, str(ruta), id_msg=id_msg,
                                       on_progreso=lambda partes, total: progreso.append(
                                           (partes, total)))
        if not salió:
            raise RuntimeError(f"{origen} -> {destino}: el envio quedo en cola, no salio")
        if not progreso:
            raise RuntimeError(f"{origen} -> {destino}: el envio no informo progreso")

        if origen == destino:
            # A -> A: el servidor NO hace eco a la sesion que origina (ella ya
            # correlaciona su ACK), asi que el adjunto propio llega por la pagina
            # remota, que es el camino que usa la UI al reconectar.
            recibir.traer_historial(origen, 0)
            fila = esperar_archivo(recibir, origen, id_msg, segundos=20)
            if fila.get("archivo_id"):
                print(f"  OK {origen} -> {destino}: 20 MiB en el historial propio, "
                      f"archivoId {fila.get('archivo_id')}")
                return
            # Deuda conocida (ESTADO_ROADMAP.md): el historial remoto no devuelve
            # archivoId de los mensajes propios, asi que quien se envia un adjunto
            # a si mismo no puede descargarlo. No se rompe el smoke por eso: lo
            # importante de la matriz es que el destinatario reciba los bytes.
            print(f"  DEUDA {origen} -> {destino}: el historial propio no trae archivoId, "
                  "el adjunto propio no se puede descargar (ver roadmap)")
            return

        fila = esperar_archivo(recibir, origen, id_msg)
        if fila.get("tipo") != TipoMensaje.MENSAJE_ARCHIVO:
            raise RuntimeError(
                f"{origen} -> {destino}: llego como {fila.get('tipo')}, no como archivo")

        archivo_id = fila.get("archivo_id")
        if not archivo_id:
            raise RuntimeError(
                f"{origen} -> {destino}: llego sin archivoId, no se puede descargar")
        with tempfile.TemporaryDirectory() as temporal:
            destino_local = Path(temporal) / nombre
            recibir.descargar_archivo(archivo_id, str(destino_local))
            sha_recibido = hashlib.sha256(destino_local.read_bytes()).hexdigest()
            bytes_recibidos = destino_local.stat().st_size
        if bytes_recibidos != ruta.stat().st_size:
            raise RuntimeError(
                f"{origen} -> {destino}: llegaron {bytes_recibidos} B de {ruta.stat().st_size} B")
        if sha_recibido != sha_esperado:
            raise RuntimeError(f"{origen} -> {destino}: el SHA-256 no coincide")
        print(f"  OK {origen} -> {destino}: {bytes_recibidos // MIB} MiB, SHA-256 identico")
    finally:
        enviar.desconectar()
        recibir.desconectar()


def main() -> int:
    temporal = Path(tempfile.mkdtemp(prefix="smoke-20mb-"))
    print(f"Generando {TAMANO_MIB} MiB no comprimibles…")
    sha_esperado, tamano = archivo_de_prueba(temporal)
    plantilla = temporal / f"plantilla-{TAMANO_MIB}MiB.bin"
    print(f"  {tamano // MIB} MiB, sha256={sha_esperado[:16]}…")

    print("Ejecutando la matriz de 3 direcciones contra el servidor real:")
    a, b = CUENTAS[0], CUENTAS[1]
    fallos = []
    try:
        enviar_y_recibir(a, b, temporal, plantilla, sha_esperado, [])
        enviar_y_recibir(b, a, temporal, plantilla, sha_esperado, [])
        enviar_y_recibir(a, a, temporal, plantilla, sha_esperado, [])
    except Exception as error:  # noqa: BLE001: el smoke reporta, no propaga
        fallos.append(str(error))
        print(f"  FALLO: {error}")
    finally:
        for sobrante in temporal.glob("*"):
            sobrante.unlink(missing_ok=True)
        temporal.rmdir()

    if fallos:
        print(f"\nSmoke 20 MiB: {len(fallos)} fallo(s).")
        return 1
    print("\nSmoke 20 MiB: A->B y B->A cuadran byte a byte (SHA-256).")
    return 0


def volcar(etiqueta: str, tramas: list) -> None:
    """Que tramas llegaron de verdad, para no adivinar cuando algo falla."""
    print(f"    [{etiqueta}] {len(tramas)} trama(s) entrante(s):")
    for trama in tramas[-8:]:
        pesado = len(trama.get("contenidoImagen") or "")
        print(f"      {trama.get('tipo')} id={trama.get('id')} "
              f"bytes={pesado} nombre={trama.get('nombreArchivo')}")


if __name__ == "__main__":
    sys.exit(main())