using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Bitacora.ViewModels;
using Bitacora.Services;

namespace Bitacora.Views
{
    public partial class HomeWindow : Window
    {
        private readonly AuthViewModel _authViewModel;
        private readonly RegistroViewModel _registroViewModel;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("user32.dll")]
        private static extern int SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint LWA_ALPHA = 0x2;
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_ENABLE_HOSTBACKDROP = 5,
            ACCENT_INVALID_STATE = 6
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_TRANSIENTWINDOW = 3;

        public HomeWindow(AuthViewModel authViewModel, RegistroViewModel registroViewModel)
        {
            InitializeComponent();
            _authViewModel = authViewModel;
            _registroViewModel = registroViewModel;
            
            try
            {
                var homeView = new HomeView(_authViewModel, _registroViewModel);
                ContentGrid.Children.Add(homeView);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al inicializar la vista principal: {ex.Message}", "Error de Inicialización", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    DragMove();
            };
            
            _authViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AuthViewModel.IsAuthenticated) && !_authViewModel.IsAuthenticated)
                {
                    Close();
                }
            };

            Bitacora.Services.NotificationService.Instance.OnShowCustomToast += ShowCustomToast;
            Bitacora.Services.NotificationService.Instance.OnShowAlarmNotification += ShowAlarmNotification;

            Closed += (s, e) =>
            {
                Bitacora.Services.NotificationService.Instance.OnShowCustomToast -= ShowCustomToast;
                Bitacora.Services.NotificationService.Instance.OnShowAlarmNotification -= ShowAlarmNotification;
            };
            
            Loaded += HomeDevWindow_Loaded;
            ContentRendered += HomeDevWindow_ContentRendered;
            SourceInitialized += HomeDevWindow_SourceInitialized;
        }

        private void ShowCustomToast(string title, string message, string type)
        {
            Dispatcher.Invoke(() =>
            {
                Bitacora.Controls.ToastNotificationControl? toast = null;
                toast = new Bitacora.Controls.ToastNotificationControl(title, message, type, () =>
                {
                    if (toast != null)
                        NotificationContainer.Children.Remove(toast);
                });

                NotificationContainer.Children.Add(toast);
            });
        }

        private void ShowAlarmNotification(AssignmentAlarmData data)
        {
            Dispatcher.Invoke(() =>
            {
                AlarmNotificationWindow.ShowAlarm(
                    data.RegistroId,
                    data.Tipo,
                    data.Titulo,
                    data.Prioridad,
                    data.Estado,
                    data.Proyecto,
                    data.Empresa,
                    data.Codigo,
                    data.HeaderTitle
                );
            });
        }

        private void HomeDevWindow_SourceInitialized(object? sender, EventArgs e)
        {
            ApplyTransparencyAndBlur();
        }

        private void HomeDevWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTransparencyAndBlur();
        }

        private void HomeDevWindow_ContentRendered(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplyTransparencyAndBlur();
            }), DispatcherPriority.Loaded);
            
            // Re-aplicar después de un delay adicional usando Task
            Task.Delay(200).ContinueWith(_ =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyTransparencyAndBlur();
                }), DispatcherPriority.Render);
            });
        }

        private void ApplyTransparencyAndBlur()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero)
                {
                    Dispatcher.BeginInvoke(new Action(() => ApplyTransparencyAndBlur()), DispatcherPriority.Loaded);
                    return;
                }
                
                int backdropType = 0;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

                var accent = new AccentPolicy
                {
                    AccentState = AccentState.ACCENT_DISABLED,
                    AccentFlags = 0,
                    GradientColor = 0,
                    AnimationId = 0
                };

                var accentStructSize = Marshal.SizeOf(accent);
                var accentPtr = Marshal.AllocHGlobal(accentStructSize);
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    SizeOfData = accentStructSize,
                    Data = accentPtr
                };

                SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(accentPtr);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error aplicando transparencia: {ex.Message}");
            }
        }
    }
}
