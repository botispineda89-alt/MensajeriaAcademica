using System;
using Mensajeria.Componentes;
using Mensajeria.Fachada;

namespace Mensajeria.Composicion
{
    /// <summary>
    /// Composition root del cliente .NET. La UI solo recibe puertos; la
    /// fachada decide qué usar y esta clase decide qué se construye.
    /// </summary>
    public static class FabricaCliente
    {
        public static IFachadaCliente CrearFachada() =>
            CrearFachada(new ProveedorSesionLocal());

        /// <summary>
        /// PAT-01: el almacenamiento local se compone aquí y se inyecta. La
        /// fachada ya no abre bases ni construye historial ni caché.
        /// </summary>
        public static IFachadaCliente CrearFachada(ProveedorSesionLocal sesionLocal) =>
            new FachadaCliente(new MapeadorDatosServidor(),
                sesionLocal ?? throw new ArgumentNullException(nameof(sesionLocal)));
    }
}