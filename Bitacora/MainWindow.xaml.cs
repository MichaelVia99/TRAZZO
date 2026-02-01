using System.Windows;
using System.Windows.Input;
using Bitacora.ViewModels;
using Bitacora.Views;

namespace Bitacora
{
    public partial class MainWindow : Window
    {
        private readonly AuthViewModel _authViewModel;
        private readonly RegistroViewModel _registroViewModel;
        private Window? _currentWindow;

        public MainWindow()
        {
            InitializeComponent();
            _authViewModel = new AuthViewModel();
            _registroViewModel = new RegistroViewModel();
            
            _authViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AuthViewModel.IsAuthenticated))
                {
                    if (_authViewModel.IsAuthenticated)
                        ShowHomeWindow();
                    else
                        ShowLoginView();
                }
            };
            
            MouseDown += (s, e) => { if (e.ChangedButton == MouseButton.Left) DragMove(); };
            
            ShowLoginView();
        }

        private void ShowLoginView()
        {
            // Detener polling de notificaciones al salir/logout
            Bitacora.Services.NotificationService.Instance.StopPolling();

            MainContent.Content = new LoginView(_authViewModel);
            Show();
            Activate();
        }

        private void ShowHomeWindow()
        {
            try
            {
                _currentWindow?.Close();
                
                if (_authViewModel.CurrentUser == null)
                {
                    MessageBox.Show("Error: No se ha podido recuperar la información del usuario.", "Error de Inicio de Sesión", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShowLoginView();
                    return;
                }

                // Iniciar polling de notificaciones para el usuario autenticado
                Bitacora.Services.NotificationService.Instance.StartPolling(_authViewModel.CurrentUser.Id);

                Window homeWindow;
                if (_authViewModel.CurrentUser.RolId != 1 && _authViewModel.CurrentUser.RolId != 2)
                {
                    MessageBox.Show("Error: Rol de usuario no reconocido o sin permisos.", "Acceso Denegado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ShowLoginView();
                    return;
                }
                
                homeWindow = new HomeWindow(_authViewModel, _registroViewModel);
                
                homeWindow.Closing += async (s, e) =>
                {
                    // Si hay un registro activo, pausarlo antes de cerrar
                    if (_registroViewModel.TieneRegistroActivo)
                    {
                        e.Cancel = true; // Cancelar cierre inmediato
                        
                        // Pausar registro
                        await _registroViewModel.PausarRegistroAsync();
                        
                        // Volver a intentar cerrar (ahora TieneRegistroActivo será false)
                        homeWindow.Close();
                        return;
                    }

                    if (!_authViewModel.IsAuthenticated)
                        ShowLoginView();
                    else
                        Application.Current.Shutdown();
                };
                
                _currentWindow = homeWindow;
                homeWindow.Show();
                Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ocurrió un error inesperado al cargar la ventana principal: {ex.Message}", "Error Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
                ShowLoginView();
            }
        }
    }
}
