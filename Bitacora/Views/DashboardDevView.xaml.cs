using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Bitacora.Models;
using Bitacora.ViewModels;

namespace Bitacora.Views
{
    public partial class DashboardDevView : UserControl
    {
        public DashboardDevView(AuthViewModel authViewModel, RegistroViewModel registroViewModel)
        {
            InitializeComponent();
            
            registroViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(RegistroViewModel.Registros))
                {
                    UpdateStats(registroViewModel);
                }
                else if (e.PropertyName == nameof(RegistroViewModel.RegistroActivo))
                {
                    UpdateRegistroActivo(registroViewModel);
                }
            };
            
            UpdateStats(registroViewModel);
            UpdateRegistroActivo(registroViewModel);
        }

        private void UpdateStats(RegistroViewModel viewModel)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var registros = viewModel.Registros;
                TotalText.Text = registros.Count.ToString();
                PendientesText.Text = registros.Count(r => r.Estado == EstadoRegistro.Pendiente).ToString();
            });
        }

        private void UpdateRegistroActivo(RegistroViewModel viewModel)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (viewModel.RegistroActivo != null)
                {
                    RegistroActivoBorder.Visibility = Visibility.Visible;
                    RegistroActivoTitulo.Text = viewModel.RegistroActivo.Titulo;
                    RegistroActivoTiempo.Text = viewModel.RegistroActivo.TiempoFormateado;
                }
                else
                {
                    RegistroActivoBorder.Visibility = Visibility.Collapsed;
                }
            });
        }
    }
}
