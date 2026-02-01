using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Bitacora.Models;
using Bitacora.ViewModels;

using Bitacora.Services;

namespace Bitacora.Views
{
    public partial class AsignacionesView : UserControl
    {
        private readonly AuthViewModel _authViewModel;
        private readonly RegistroViewModel _registroViewModel;

        public AsignacionesView(AuthViewModel authViewModel, RegistroViewModel registroViewModel)
        {
            InitializeComponent();
            _authViewModel = authViewModel;
            _registroViewModel = registroViewModel;
            
            if (_authViewModel.CurrentUser != null)
            {
                _ = registroViewModel.CargarRegistrosPorAsignadoAsync(_authViewModel.CurrentUser.Id);
            }
            
            registroViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(RegistroViewModel.Registros))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RefrescarAsignaciones();
                    });
                }
                else if (e.PropertyName == nameof(RegistroViewModel.RegistroActivo))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RefrescarAsignaciones();
                    });
                }
            };
            
            RefrescarAsignaciones();
        }

        private void RefrescarAsignaciones()
        {
            // 1. En Curso
            // 2. Todos los demás (excepto Por Planificar) ordenados por Prioridad
            // 3. Por Planificar ordenados por Prioridad

            var listaOrdenada = _registroViewModel.Registros
                .OrderBy(r => 
                {
                    if (r.Estado == EstadoRegistro.EnProceso) return 0;
                    if (r.Estado != EstadoRegistro.Pendiente) return 1;
                    return 2; // Pendiente (Por Planificar) al final
                })
                .ThenByDescending(r => r.PrioridadNivel)
                .ThenBy(r => r.TiempoRestante)
                .ThenBy(r => r.FechaCreacion)
                .ToList();

            AsignacionesItemsControl.ItemsSource = listaOrdenada;
        }

        private async void EditarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Registro registro)
            {
                try 
                {
                    var registroCompleto = await DatabaseService.Instance.GetRegistroByIdAsync(registro.Id) ?? registro;

                    var window = new NuevoRequerimientoWindow(_authViewModel, _registroViewModel);
                    window.Owner = Window.GetWindow(this);
                    window.SetTitle("Detalle de la Asignación");
                    window.SetCompactDetailMode();
                    window.FormView.LoadRegistro(registroCompleto);
                    window.FormView.SetReadOnly(true);
                    window.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al cargar los detalles: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void IniciarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Registro registro)
            {
                try
                {
                    await _registroViewModel.IniciarRegistroAsync(registro.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al iniciar el registro: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void PausarButton_Click(object sender, RoutedEventArgs e)
        {
            if (_registroViewModel.RegistroActivo != null)
            {
                try
                {
                    await _registroViewModel.PausarRegistroAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al pausar el registro: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("No hay ningún registro activo para pausar.", "Información", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void CerrarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Registro registro)
            {
                var result = MessageBox.Show(
                    $"¿Está seguro de que desea cerrar el registro \"{registro.Titulo}\"?",
                    "Cerrar Registro",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _registroViewModel.CerrarRegistroAsync(registro.Id);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error al cerrar el registro: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}
