using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Bitacora.Models;
using Bitacora.ViewModels;
using Bitacora.Services;

namespace Bitacora.Views
{
    public partial class ListadoRegistrosView : UserControl
    {
        private readonly AuthViewModel _authViewModel;
        private readonly RegistroViewModel _registroViewModel;

        public ListadoRegistrosView(AuthViewModel authViewModel, RegistroViewModel registroViewModel)
        {
            InitializeComponent();
            _authViewModel = authViewModel;
            _registroViewModel = registroViewModel;
            DataContext = _registroViewModel;
            
            if (authViewModel.CurrentUser != null)
            {
                // Cargar todos los registros para la vista general
                _ = registroViewModel.CargarRegistrosAsync();
            }
        }

        private void NuevoRegistroButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new NuevoRequerimientoWindow(_authViewModel, _registroViewModel);
            window.Owner = Window.GetWindow(this);
            window.ShowDialog();
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Registro registro)
            {
                var registroCompleto = await DatabaseService.Instance.GetRegistroByIdAsync(registro.Id) ?? registro;

                var window = new NuevoRequerimientoWindow(_authViewModel, _registroViewModel);
                window.Owner = Window.GetWindow(this);
                window.SetTitle("Editar Registro");
                window.FormView.LoadRegistro(registroCompleto);
                window.ShowDialog();
            }
        }

        private async void ResendNotificationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Registro registro)
            {
                try
                {
                    await _registroViewModel.ReenviarNotificacionAsync(registro);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al reenviar la notificación: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void EstimateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Registro registroItem)
            {
                var registroCompleto = await DatabaseService.Instance.GetRegistroByIdAsync(registroItem.Id) ?? registroItem;

                var window = new TiempoEstimadoWindow(registroCompleto.TiempoEstimado);
                window.Owner = Window.GetWindow(this);
                
                if (window.ShowDialog() == true)
                {
                    registroCompleto.TiempoEstimado = window.TotalSeconds;
                    
                    if (registroCompleto.Estado == EstadoRegistro.Pendiente && registroCompleto.TiempoEstimado > 0)
                    {
                        registroCompleto.Estado = EstadoRegistro.EnEspera;
                    }
                    
                    try
                    {
                        await _registroViewModel.ActualizarRegistroAsync(registroCompleto);
                        await _registroViewModel.ReenviarNotificacionAsync(registroCompleto);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al actualizar el registro: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void FilterPlanificar_Click(object sender, RoutedEventArgs e)
        {
            _registroViewModel.FiltrarPorEstado(EstadoRegistro.Pendiente);
        }

        private void FilterEnEspera_Click(object sender, RoutedEventArgs e)
        {
            _registroViewModel.FiltrarPorEstado(EstadoRegistro.EnEspera);
        }

        private void FilterCurso_Click(object sender, RoutedEventArgs e)
        {
            _registroViewModel.FiltrarPorEstado(EstadoRegistro.EnProceso);
        }

        private void FilterPausado_Click(object sender, RoutedEventArgs e)
        {
            _registroViewModel.FiltrarPorEstado(EstadoRegistro.Pausado);
        }

        private void FilterCompletados_Click(object sender, RoutedEventArgs e)
        {
            _registroViewModel.FiltrarPorEstado(EstadoRegistro.Cerrado);
        }

        private void FilterTodos_Click(object sender, RoutedEventArgs e)
        {
            _registroViewModel.FiltrarPorEstado(null);
        }

        private async void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Registro registro)
            {
                var registroCompleto = await DatabaseService.Instance.GetRegistroByIdAsync(registro.Id) ?? registro;

                var window = new NuevoRequerimientoWindow(_authViewModel, _registroViewModel);
                window.Owner = Window.GetWindow(this);
                window.SetTitle("Detalle del Registro");
                window.SetCompactDetailMode();
                window.FormView.LoadRegistro(registroCompleto);
                window.FormView.SetReadOnly(true);
                window.ShowDialog();
            }
        }

        private async void CerrarSesionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _registroViewModel.PausarRegistroAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al pausar el registro activo antes de cerrar sesión: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            _authViewModel.Logout();
        }

        private async void CerrarAplicacionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _registroViewModel.PausarRegistroAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al pausar el registro activo antes de cerrar la aplicación: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Application.Current.Shutdown();
        }
    }
}
