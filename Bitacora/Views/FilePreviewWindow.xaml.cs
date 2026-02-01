using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Bitacora.Views
{
    public partial class FilePreviewWindow : Window
    {
        private readonly string _filePath;

        public FilePreviewWindow(string filePath, string title)
        {
            InitializeComponent();
            _filePath = filePath;
            Title = title;
            
            Loaded += FilePreviewWindow_Loaded;
        }

        private async void FilePreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;

                var extension = Path.GetExtension(_filePath).ToLowerInvariant();
                var isRemote = IsHttpUrl(_filePath);

                if (IsImageExtension(extension))
                {
                    if (isRemote)
                    {
                        OpenExternal_Click(sender, new RoutedEventArgs());
                        Close();
                    }
                    else
                    {
                        LoadImage();
                    }
                }
                else if (extension == ".pdf")
                {
                    OpenExternal_Click(sender, new RoutedEventArgs());
                    Close();
                }
                else
                {
                    // Fallback for other files (should generally be handled before opening window, but just in case)
                    OpenExternal_Click(sender, new RoutedEventArgs());
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el archivo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private bool IsImageExtension(string ext)
        {
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".webp";
        }

        private static bool IsHttpUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return false;

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private void LoadImage()
        {
            ImageContainer.Visibility = Visibility.Visible;
            PdfContainer.Visibility = Visibility.Collapsed;

            // Load bitmap without locking the file
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(_filePath);
            bitmap.EndInit();

            PreviewImage.Source = bitmap;
            ZoomSlider.Value = 1.0;
        }

        private async System.Threading.Tasks.Task LoadPdf()
        {
            ImageContainer.Visibility = Visibility.Collapsed;
            PdfContainer.Visibility = Visibility.Visible;

            // Initialize WebView2
            await PdfWebView.EnsureCoreWebView2Async();
            PdfWebView.CoreWebView2.Navigate(_filePath);
        }

        #region Zoom Logic for Image

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ImageScaleTransform != null)
            {
                ImageScaleTransform.ScaleX = e.NewValue;
                ImageScaleTransform.ScaleY = e.NewValue;
                
                if (ZoomPercentageText != null)
                    ZoomPercentageText.Text = $"{(int)(e.NewValue * 100)}%";
            }
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (ZoomSlider.Value < ZoomSlider.Maximum)
                ZoomSlider.Value += 0.25;
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (ZoomSlider.Value > ZoomSlider.Minimum)
                ZoomSlider.Value -= 0.25;
        }

        private void FitToScreen_Click(object sender, RoutedEventArgs e)
        {
            ZoomSlider.Value = 1.0;
            // Optional: Implement true "Fit to Screen" logic by comparing image size to viewport size
            // For now, reset to 100% is standard behavior for "Reset"
        }

        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Delta > 0)
                    ZoomIn_Click(sender, e);
                else
                    ZoomOut_Click(sender, e);

                e.Handled = true;
            }
        }

        #endregion

        private void OpenExternal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(_filePath)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir el archivo externamente: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
