using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using Mensajeria.Fachada;
using Mensajeria.Transversal;
using Mensajeria.Transversal.Dtos;
using Mensajeria.ViewModels;

namespace Mensajeria.Views
{
    public partial class ChatWindow
    {
        // Busqueda en el historial (UIX-06/UIX-07)
        // Solo code-behind: los campos y el constructor viven en ChatWindow.xaml.cs;
        // este parcial agrupa una sola responsabilidad para poder localizarlo.

        // ------------------------------------------------- UIX-06: buscar en el historial

        private void MostrarBuscar()
        {
            if (_usuarioDestino == null)
            {
                txtEstado.Text = "Elige una conversación para buscar en su historial.";
                return;
            }
            barraBuscar.Visibility = Visibility.Visible;
            txtBuscarHistorial.Focus();
            txtBuscarHistorial.SelectAll();
        }

        private void CerrarBuscar()
        {
            barraBuscar.Visibility = Visibility.Collapsed;
            txtBuscarHistorial.Clear();
            _patronBusqueda = "";
            _idsBusqueda.Clear();
            _indiceBusqueda = -1;
            _saltoPendiente = null;
            txtBuscarContador.Text = "";
            foreach (var vm in _mensajes) vm.PatronResaltado = "";
        }

        private void BtnBuscarCerrar_Click(object sender, RoutedEventArgs e) => CerrarBuscar();

        private void TxtBuscarHistorial_TextChanged(object sender, TextChangedEventArgs e)
        {
            // UIX-07: la query a SQLite espera 300 ms tras la última tecla.
            _timerBuscar.Stop();
            _timerBuscar.Start();
        }

        private void TxtBuscarHistorial_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                    BuscarAnterior();
                else
                    BuscarSiguiente();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                CerrarBuscar();
                e.Handled = true;
            }
        }

        private void BtnBuscarSiguiente_Click(object sender, RoutedEventArgs e) => BuscarSiguiente();

        private void BtnBuscarAnterior_Click(object sender, RoutedEventArgs e) => BuscarAnterior();

        private void EjecutarBusqueda()
        {
            string? codigo = _usuarioDestino;
            string patron = (txtBuscarHistorial.Text ?? "").Trim();
            if (codigo == null || patron.Length == 0)
            {
                _patronBusqueda = "";
                _idsBusqueda.Clear();
                _indiceBusqueda = -1;
                ActualizarContadorBusqueda();
                foreach (var vm in _mensajes) vm.PatronResaltado = "";
                return;
            }
            _patronBusqueda = patron;
            _idsBusqueda.Clear();
            _idsBusqueda.AddRange(_fachada.BuscarEnHistorial(codigo, patron));
            _indiceBusqueda = _idsBusqueda.Count > 0 ? 0 : -1;
            ActualizarContadorBusqueda();
            foreach (var vm in _mensajes) vm.PatronResaltado = patron;
            if (_idsBusqueda.Count > 0) SaltarA(_idsBusqueda[0], 5);
        }

        private void ActualizarContadorBusqueda()
        {
            txtBuscarContador.Text = _idsBusqueda.Count == 0
                ? (_patronBusqueda.Length == 0 ? "" : "Sin resultados")
                : $"{_indiceBusqueda + 1}/{_idsBusqueda.Count}";
        }

        private void BuscarSiguiente()
        {
            if (_idsBusqueda.Count == 0) return;
            _indiceBusqueda = (_indiceBusqueda + 1) % _idsBusqueda.Count;
            ActualizarContadorBusqueda();
            SaltarA(_idsBusqueda[_indiceBusqueda], 5);
        }

        private void BuscarAnterior()
        {
            if (_idsBusqueda.Count == 0) return;
            _indiceBusqueda = (_indiceBusqueda - 1 + _idsBusqueda.Count) % _idsBusqueda.Count;
            ActualizarContadorBusqueda();
            SaltarA(_idsBusqueda[_indiceBusqueda], 5);
        }

        /// <summary>
        /// Baja al mensaje: si está cargado, scroll + selección; si no, trae
        /// una página más y reintenta (acotado).
        /// </summary>
        private void SaltarA(string id, int intentos)
        {
            if (_usuarioDestino == null) return;
            var vm = _mensajes.FirstOrDefault(m => !m.EsCabeceraDia && m.Id == id);
            if (vm != null)
            {
                lstMensajes.ScrollIntoView(vm);
                lstMensajes.SelectedItem = vm;
                return;
            }
            if (intentos <= 0 || btnAnteriores.Visibility != Visibility.Visible)
            {
                txtBuscarContador.Text = "Fuera del historial cargado";
                return;
            }
            _saltoPendiente = id;
            _intentosSalto = intentos - 1;
            BtnAnteriores_Click(this, new RoutedEventArgs());
        }
    }
}
