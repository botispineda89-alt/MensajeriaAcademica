namespace Mensajeria.Transversal
{
    /// <summary>
    /// UIX-10: ancho de burbuja relativo a la lista (65 %, tope 720, suelo 280).
    /// Réplica de `anchoBurbuja` (Java) y `ancho_burbuja` (Python).
    /// </summary>
    public static class AnchoBurbuja
    {
        public const double Tope = 720;
        public const double Suelo = 280;
        public const double Fraccion = 0.65;

        public static double Calcular(double anchoLista)
        {
            // La medida puede llegar como NaN/Infinity durante el primer pase
            // de layout o al redimensionar el ListBox. En ese momento NO se fija
            // ancho: devolver NaN deja que el contenedor limite lo disponible.
            // Antes se devolvia Tope (720, el valor MAYOR) y por eso las burbujas
            // se salian de la columna en paneles mas estrechos que 720.
            if (double.IsNaN(anchoLista) || double.IsInfinity(anchoLista)
                || anchoLista <= 100) return double.NaN;
            return System.Math.Min(Tope, System.Math.Max(Suelo, anchoLista * Fraccion));
        }
    }
}
