using System.Windows;
using System.Windows.Controls;
using Bitacora.ViewModels;

namespace Bitacora.Views
{
    public partial class LoginView : UserControl
    {
        private readonly AuthViewModel _authViewModel;

        public LoginView(AuthViewModel authViewModel)
        {
            InitializeComponent();
            _authViewModel = authViewModel;
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;
            LoginButton.IsEnabled = false;

            var usuario = UsuarioTextBox.Text.Trim();
            var password = PasswordBox.Password;

            var success = await _authViewModel.LoginAsync(usuario, password);

            if (!success)
            {
                ErrorTextBlock.Text = "Credenciales incorrectas";
                ErrorTextBlock.Visibility = Visibility.Visible;
            }

            LoginButton.IsEnabled = true;
            PasswordBox.Password = "";
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
