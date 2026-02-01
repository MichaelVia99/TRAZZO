using System.Linq;
using System.Windows.Controls;
using Bitacora.Models;
using Bitacora.ViewModels;

namespace Bitacora.Views
{
    public partial class DashboardGestorView : UserControl
    {
        public DashboardGestorView(RegistroViewModel registroViewModel)
        {
            InitializeComponent();
            
            registroViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(RegistroViewModel.Registros))
                {
                    UpdateStats(registroViewModel);
                }
            };
            
            UpdateStats(registroViewModel);
        }

        private void UpdateStats(RegistroViewModel viewModel)
        {
            var registros = viewModel.Registros;
            TotalText.Text = registros.Count.ToString();
            PendientesText.Text = registros.Count(r => r.Estado == EstadoRegistro.Pendiente).ToString();
            EnProcesoText.Text = registros.Count(r => r.Estado == EstadoRegistro.EnProceso).ToString();
            CerradosText.Text = registros.Count(r => r.Estado == EstadoRegistro.Cerrado).ToString();
        }
    }
}

