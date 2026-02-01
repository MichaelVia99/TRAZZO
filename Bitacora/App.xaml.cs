using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Bitacora
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            await Services.DatabaseService.Instance.InitializeDatabaseAsync();
        }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowGlobalError("Se produjo un error inesperado en la aplicación.", e.Exception);
            e.Handled = true;
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                ShowGlobalError("Se produjo un error no controlado en la aplicación.", ex);
            }
        }

        void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            ShowGlobalError("Se produjo un error en una tarea en segundo plano.", e.Exception);
            e.SetObserved();
        }

        static void ShowGlobalError(string message, Exception exception)
        {
            var fullMessage = message + Environment.NewLine + exception.Message;

            if (Current?.Dispatcher != null)
            {
                try
                {
                    Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(fullMessage, "Error inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    return;
                }
                catch
                {
                }
            }

            MessageBox.Show(fullMessage, "Error inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
