using System;
using System.Collections.Generic;
using System.Linq;

namespace Mensajeria.Transversal
{
    public readonly record struct ProgresoEnvio(
        int IndiceActual,
        int TotalArchivos,
        string NombreActual,
        long BytesEnviados,
        long BytesTotales,
        double FraccionArchivo)
    {
        public double FraccionGlobal => BytesTotales <= 0
            ? 0d : Math.Clamp((double)BytesEnviados / BytesTotales, 0d, 1d);
    }

    public sealed class AgregadorProgreso
    {
        private readonly long[] _pesos;
        private long _completados;

        public AgregadorProgreso(IReadOnlyList<long> pesos)
        {
            _pesos = [.. pesos];
            BytesTotales = _pesos.Sum();
        }

        public long BytesTotales { get; }

        public long Completar(int indice)
        {
            _completados += indice >= 0 && indice < _pesos.Length ? _pesos[indice] : 0;
            return _completados;
        }

        public long Acumulado(int indice, double fraccionArchivo) =>
            _completados + (long)(_pesos[indice] * Math.Clamp(fraccionArchivo, 0d, 1d));
    }
}
