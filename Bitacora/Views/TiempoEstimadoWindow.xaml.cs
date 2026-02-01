using System;
using System.Windows;
using System.Windows.Input;

namespace Bitacora.Views
{
    public partial class TiempoEstimadoWindow : Window
    {
        public int TotalSeconds { get; private set; }

        public TiempoEstimadoWindow(int currentSeconds = 0)
        {
            InitializeComponent();
            
            int horas = currentSeconds / 3600;
            int minutos = (currentSeconds % 3600) / 60;
            
            HorasTextBox.Text = horas.ToString();
            MinutosTextBox.Text = minutos.ToString();
            
            HorasTextBox.Focus();
            HorasTextBox.SelectAll();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Guardar_Click(object sender, RoutedEventArgs e)
        {
            int.TryParse(HorasTextBox.Text, out int horas);
            int.TryParse(MinutosTextBox.Text, out int minutos);
            
            TotalSeconds = (horas * 3600) + (minutos * 60);

            if (TotalSeconds < 300)
            {
                MessageBox.Show("El tiempo mínimo de planificación debe ser de 5 minutos.", "Tiempo Insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            DialogResult = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void BtnIncreaseHours_Click(object sender, RoutedEventArgs e)
        {
            int.TryParse(HorasTextBox.Text, out int horas);
            horas++;
            HorasTextBox.Text = horas.ToString();
        }

        private void BtnDecreaseHours_Click(object sender, RoutedEventArgs e)
        {
            int.TryParse(HorasTextBox.Text, out int horas);
            if (horas > 0)
            {
                horas--;
                HorasTextBox.Text = horas.ToString();
            }
        }

        private void BtnIncreaseMinutes_Click(object sender, RoutedEventArgs e)
        {
            int.TryParse(MinutosTextBox.Text, out int minutos);
            minutos += 5;
            if (minutos >= 60)
            {
                minutos -= 60;
                BtnIncreaseHours_Click(sender, e);
            }
            MinutosTextBox.Text = minutos.ToString();
        }

        private void BtnDecreaseMinutes_Click(object sender, RoutedEventArgs e)
        {
            int.TryParse(MinutosTextBox.Text, out int minutos);
            minutos -= 5;
            if (minutos < 0)
            {
                minutos = 55;
                BtnDecreaseHours_Click(sender, e);
            }
            MinutosTextBox.Text = minutos.ToString();
        }
    }
}