using System.Windows;
using System.Windows.Controls;
using Bitacora.ViewModels;

namespace Bitacora.Views
{
    public partial class HomeView : UserControl
    {
        private readonly AuthViewModel _authViewModel;
        private readonly RegistroViewModel _registroViewModel;
        private string _tabActivo = "Asignaciones";

        public HomeView(AuthViewModel authViewModel, RegistroViewModel registroViewModel)
        {
            InitializeComponent();
            _authViewModel = authViewModel;
            _registroViewModel = registroViewModel;
            
            ConfigurarPorRol();
            ActualizarTabActivo();
        }

        private void ActualizarTabActivo()
        {
            DashboardTabButton.Tag = _tabActivo == "Dashboard" ? "Active" : "Inactive";
            SolicitudesTabButton.Tag = _tabActivo == "Solicitudes" ? "Active" : "Inactive";
            AsignacionesTabButton.Tag = _tabActivo == "Asignaciones" ? "Active" : "Inactive";
        }

        private void DashboardTabButton_Click(object sender, RoutedEventArgs e)
        {
            _tabActivo = "Dashboard";
            ActualizarTabActivo();
            
            if (_authViewModel.CurrentUser?.RolId == 1)
            {
                ContentArea.Content = new DashboardGestorView(_registroViewModel);
            }
            else
            {
                ContentArea.Content = new DashboardDevView(_authViewModel, _registroViewModel);
            }
        }

        private void SolicitudesTabButton_Click(object sender, RoutedEventArgs e)
        {
            _tabActivo = "Solicitudes";
            ActualizarTabActivo();
            ContentArea.Content = new ListadoRegistrosView(_authViewModel, _registroViewModel);
        }

        private void AsignacionesTabButton_Click(object sender, RoutedEventArgs e)
        {
            _tabActivo = "Asignaciones";
            ActualizarTabActivo();
            ContentArea.Content = new AsignacionesView(_authViewModel, _registroViewModel);
        }

        private void ConfigurarPorRol()
        {
            var rolId = _authViewModel.CurrentUser?.RolId;

            if (rolId == 1)
            {
                TitleTextBlock.Text = "Panel de Gestor";
                SubtitleTextBlock.Text = "Gestión y Monitoreo de Incidencias";

                DashboardTabButton.Visibility = Visibility.Visible;
                SolicitudesTabButton.Visibility = Visibility.Visible;
                AsignacionesTabButton.Visibility = Visibility.Visible;

                _tabActivo = "Solicitudes";
                ContentArea.Content = new ListadoRegistrosView(_authViewModel, _registroViewModel);
            }
            else
            {
                TitleTextBlock.Text = "Panel de Desarrollador";
                SubtitleTextBlock.Text = "Trabaja tus requerimientos e incidentes asignados";

                DashboardTabButton.Visibility = Visibility.Visible;
                SolicitudesTabButton.Visibility = Visibility.Collapsed;
                AsignacionesTabButton.Visibility = Visibility.Visible;

                _tabActivo = "Asignaciones";
                ContentArea.Content = new AsignacionesView(_authViewModel, _registroViewModel);
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
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
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error al pausar el registro activo antes de cerrar la aplicación: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Application.Current.Shutdown();
        }
    }
}
