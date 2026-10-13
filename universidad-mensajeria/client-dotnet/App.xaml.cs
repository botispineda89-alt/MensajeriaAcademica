using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Mensajeria.Composicion;
using Mensajeria.Fachada;

namespace Mensajeria
{
    public partial class App : Application
    {
        private IFachadaCliente? _fachada;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            InstalarGuardas();
            Temas.Tema.Aplicar(Temas.Tema.Leer());
            var fachada = FabricaCliente.CrearFachada();
            _fachada = fachada;
            new Views.LoginWindow(fachada).Show();
        }

        /// <summary>
        /// Una excepcion no controlada en WPF cierra el proceso. Sin esto el
        /// usuario ve como la aplicacion desaparece sin saber por que: un fallo
        /// puntual de la vista se pierde entero. Se registra en un fichero
        /// junto al ejecutable y se avisa, pero la sesion sobrevive.
        /// </summary>
        private void InstalarGuardas()
        {
            string log = Path.Combine(AppContext.BaseDirectory, "errores-cliente.log");

            void Registrar(Exception error)
            {
                try
                {
                    File.AppendAllText(log,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{error}"
                        + Environment.NewLine + new string('-', 70) + Environment.NewLine);
                }
                catch { /* si ni el log se puede escribir, no se molesta al usuario */ }
            }

            DispatcherUnhandledException += (_, args) =>
            {
                Registrar(args.Exception);
                MessageBox.Show(
                    "Ocurrio un error inesperado y no se pudo completar la operacion."
                    + Environment.NewLine + Environment.NewLine
                    + args.Exception.Message + Environment.NewLine + Environment.NewLine
                    + $"El detalle se guardo en:{Environment.NewLine}{log}",
                    "Mensajeria", MessageBoxButton.OK, MessageBoxImage.Warning);
                // Que continue: perder la sesion por un fallo puntual es peor.
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception error) Registrar(error);
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Registrar(args.Exception);
                args.SetObserved();
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _fachada?.Desconectar();
            base.OnExit(e);
        }
    }
}
