using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Mensajeria.Fachada;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;

namespace Mensajeria.ViewModels
{
    public sealed class AdjuntosPanelVM
    {
        private readonly HashSet<string> _rutas = new(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<AdjuntoVM> Adjuntos { get; } = new();
        public bool HayAdjuntos => Adjuntos.Count > 0;
        public long TotalBytes => Adjuntos.Sum(a => a.Datos.Tamano);
        public string TextoResumen => $"{Adjuntos.Count} archivo(s) · {CatalogoAdjuntos.TamanoLegible(TotalBytes)}";
        public int ImagenesSobreLimiteTrama { get; private set; }

        public (int Aceptados, int Duplicados, int Invalidos, int DemasiadosGrandes)
            Agregar(IEnumerable<string> rutas)
        {
            int aceptados = 0, duplicados = 0, invalidos = 0, grandes = 0;
            ImagenesSobreLimiteTrama = 0;
            foreach (string ruta in rutas)
            {
                string normalizada;
                try { normalizada = Path.GetFullPath(ruta); }
                catch { invalidos++; continue; }

                if (!_rutas.Add(normalizada))
                {
                    duplicados++;
                    continue;
                }

                try
                {
                    if (!File.Exists(normalizada))
                    {
                        invalidos++;
                        _rutas.Remove(normalizada);
                        continue;
                    }

                    long tamano = new FileInfo(normalizada).Length;
                    if (tamano == 0)
                    {
                        invalidos++;
                        _rutas.Remove(normalizada);
                        continue;
                    }
                    if (tamano > IFachadaCliente.MaxBytesArchivo)
                    {
                        grandes++;
                        _rutas.Remove(normalizada);
                        continue;
                    }
                    if (!CatalogoAdjuntos.CabeEnTramaUnica(normalizada, tamano))
                    {
                        grandes++;
                        ImagenesSobreLimiteTrama++;
                        _rutas.Remove(normalizada);
                        continue;
                    }

                    var datos = new AdjuntoPendiente(normalizada, Path.GetFileName(normalizada),
                        tamano, CatalogoAdjuntos.EsImagen(normalizada), CatalogoAdjuntos.Mime(normalizada));
                    var vm = new AdjuntoVM(datos);
                    Adjuntos.Add(vm);
                    vm.CargarMiniatura();
                    aceptados++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                            or ArgumentException or NotSupportedException)
                {
                    invalidos++;
                    _rutas.Remove(normalizada);
                }
            }

            return (aceptados, duplicados, invalidos, grandes);
        }

        public void Quitar(AdjuntoVM adjunto)
        {
            if (Adjuntos.Remove(adjunto)) _rutas.Remove(adjunto.Ruta);
        }

        public void Limpiar()
        {
            Adjuntos.Clear();
            _rutas.Clear();
        }

        public IReadOnlyList<AdjuntoPendiente> Snapshot() =>
            Adjuntos.Select(a => a.Datos).ToArray();

        public AdjuntoVM? Buscar(string ruta) => Adjuntos.FirstOrDefault(a =>
            string.Equals(a.Ruta, ruta, StringComparison.OrdinalIgnoreCase));

        public AdjuntoVM? BuscarNombre(string nombre) => Adjuntos.FirstOrDefault(a =>
            string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase));

        public AdjuntoVM? BuscarIndice(int indice) =>
            indice >= 0 && indice < Adjuntos.Count ? Adjuntos[indice] : null;
    }
}
