using System.Windows;
using System.Windows.Input;
using Bitacora.ViewModels;

namespace Bitacora.Views
{
    public partial class NuevoRequerimientoWindow : Window
    {
        public NuevoRequerimientoWindow(AuthViewModel authViewModel, RegistroViewModel registroViewModel)
        {
            InitializeComponent();
            
            // Cargar el formulario dentro de la ventana
            FormContent.Content = new RegistroFormView(authViewModel, registroViewModel);
            
            // Habilitar arrastre de ventana
            MouseLeftButtonDown += (s, e) => DragMove();
        }

        public RegistroFormView FormView => (RegistroFormView)FormContent.Content;

        public void SetTitle(string title)
        {
            TitleTextBlock.Text = title;
        }

        public void SetCompactDetailMode()
        {
            SizeToContent = SizeToContent.Manual;
            Width = 480;
            Height = 650;
            ResizeMode = ResizeMode.NoResize;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
