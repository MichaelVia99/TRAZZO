using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Bitacora.Controls;

public partial class ToastNotificationControl : UserControl
{
    private DispatcherTimer? _autoCloseTimer;
    private Action _onClose;

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public SolidColorBrush TypeColor { get; set; } = Brushes.Gray;

    public ToastNotificationControl(string title, string message, string type, Action onClose)
    {
        InitializeComponent();
        
        Title = title;
        Message = message;
        _onClose = onClose;

        ConfigureType(type);
        DataContext = this;

        Loaded += ToastNotificationControl_Loaded;
    }

    private void ConfigureType(string type)
    {
        switch (type.ToLower())
        {
            case "success":
                Icon = "\uE73E"; // Checkmark
                TypeColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                break;
            case "info":
                Icon = "\uE946"; // Info
                TypeColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3"));
                break;
            case "warning":
                Icon = "\uE7BA"; // Warning
                TypeColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107"));
                break;
            case "error":
                Icon = "\uE783"; // Error
                TypeColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                break;
            case "assignment":
                Icon = "\uE77B"; // User
                TypeColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C27B0"));
                break;
            default:
                Icon = "\uE946";
                TypeColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#607D8B"));
                break;
        }
    }

    private void ToastNotificationControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Animate In
        var sb = (Storyboard)Resources["SlideInStoryboard"];
        sb.Begin();

        // Auto Close Timer
        _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _autoCloseTimer.Tick += (s, args) => CloseToast();
        _autoCloseTimer.Start();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseToast();
    }

    private void CloseToast()
    {
        if (_autoCloseTimer != null)
        {
            _autoCloseTimer.Stop();
            _autoCloseTimer = null;
        }

        var sb = (Storyboard)Resources["SlideOutStoryboard"];
        sb.Completed += (s, e) => _onClose?.Invoke();
        sb.Begin();
    }
}
