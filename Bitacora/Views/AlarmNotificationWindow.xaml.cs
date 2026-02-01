using System;
using System.Collections.Generic;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using Bitacora.Models;

namespace Bitacora.Views
{
    public partial class AlarmNotificationWindow : Window
    {
        private static readonly List<AlarmNotificationWindow> _openWindows = new();
        private static DispatcherTimer? _globalSoundTimer;
        private readonly string _registroId;
        private readonly DateTime _arrivalTime;
        private DispatcherTimer _elapsedTimer;

        private AlarmNotificationWindow(string registroId, string tipo, string titulo, string? prioridad, EstadoRegistro estado, string? proyecto, string? empresa, string codigo, string headerTitle = "¡Nueva Asignación!")
        {
            InitializeComponent();

            _registroId = registroId;
            _arrivalTime = DateTime.Now;

            // Configure Timer for "Hace X segundos"
            _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _elapsedTimer.Tick += (s, e) => UpdateElapsedTime();
            _elapsedTimer.Start();

            // Initial text
            HeaderTitleText.Text = headerTitle;
            TitleText.Text = titulo;
            ProjectText.Text = string.IsNullOrWhiteSpace(proyecto) ? "Sin proyecto" : proyecto;
            
            if (EmpresaText != null)
            {
                EmpresaText.Text = string.IsNullOrWhiteSpace(empresa) ? "" : empresa;
                EmpresaText.Visibility = string.IsNullOrWhiteSpace(empresa) ? Visibility.Collapsed : Visibility.Visible;
            }

            ConfigureTipo(tipo, codigo);
            ConfigurePrioridad(prioridad);
            ConfigureEstado(estado);

            Loaded += AlarmNotificationWindow_Loaded;
            Closed += AlarmNotificationWindow_Closed;
        }

        private void UpdateElapsedTime()
        {
            var elapsed = DateTime.Now - _arrivalTime;
            if (elapsed.TotalSeconds > 60)
            {
                TimeText.Text = $"Hace {Math.Floor(elapsed.TotalMinutes)} min";
            }
            else
            {
                TimeText.Text = $"Hace {Math.Floor(elapsed.TotalSeconds)} seg";
            }
        }

        private void AlarmNotificationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatePosition();
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            BeginAnimation(OpacityProperty, anim);
        }

        private void UpdatePosition()
        {
            var area = SystemParameters.WorkArea;
            int index = _openWindows.IndexOf(this);
            
            if (index == -1) return;

            double margin = 10;
            double itemHeight = Height + margin;
            
            Left = area.Right - Width - 20;
            Top = area.Bottom - itemHeight * (index + 1);
        }

        public static void ShowAlarm(string registroId, string tipo, string titulo, string? prioridad, EstadoRegistro estado, string? proyecto, string? empresa, string codigo, string headerTitle = "¡Nueva Asignación!")
        {
            var window = new AlarmNotificationWindow(registroId, tipo, titulo, prioridad, estado, proyecto, empresa, codigo, headerTitle);
            _openWindows.Add(window);
            EnsureSoundPlaying();
            window.Show();
        }

        private static void EnsureSoundPlaying()
        {
            if (_globalSoundTimer == null)
            {
                // Sonido más frecuente (1 segundo) y molesto (Hand)
                _globalSoundTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _globalSoundTimer.Tick += (s, e) => 
                {
                    SystemSounds.Hand.Play();
                };
                _globalSoundTimer.Start();
                SystemSounds.Hand.Play();
            }
            else if (!_globalSoundTimer.IsEnabled)
            {
                _globalSoundTimer.Start();
            }
        }

        private void AlarmNotificationWindow_Closed(object? sender, EventArgs e)
        {
            _openWindows.Remove(this);
            _elapsedTimer.Stop();

            if (_openWindows.Count == 0)
            {
                _globalSoundTimer?.Stop();
                _globalSoundTimer = null;
            }
            
            // Re-adjust positions of remaining windows
            foreach (var win in _openWindows)
            {
                win.UpdatePosition();
            }
        }

        private void ConfigurePrioridad(string? prioridad)
        {
            // Get color from resources based on priority name
            Brush priorityBrush = Brushes.Gray;
            string p = (prioridad ?? "").Trim();
            
            if (string.Equals(p, "Critica", StringComparison.OrdinalIgnoreCase) || string.Equals(p, "Crítica", StringComparison.OrdinalIgnoreCase))
            {
                priorityBrush = (Brush)Application.Current.Resources["PriorityCriticalBrush"];
                if (PrioridadCriticaIcon != null) PrioridadCriticaIcon.Visibility = Visibility.Visible;
            }
            else if (string.Equals(p, "Alta", StringComparison.OrdinalIgnoreCase))
            {
                priorityBrush = (Brush)Application.Current.Resources["PriorityHighBrush"];
                if (PrioridadAltaIcon != null) PrioridadAltaIcon.Visibility = Visibility.Visible;
            }
            else if (string.Equals(p, "Menor", StringComparison.OrdinalIgnoreCase) || string.Equals(p, "Baja", StringComparison.OrdinalIgnoreCase))
            {
                priorityBrush = (Brush)Application.Current.Resources["PriorityLowBrush"];
                if (PrioridadMenorIcon != null) PrioridadMenorIcon.Visibility = Visibility.Visible;
            }
            else
            {
                // Default to Normal
                priorityBrush = (Brush)Application.Current.Resources["PriorityNormalBrush"];
                if (PrioridadNormalIcon != null) PrioridadNormalIcon.Visibility = Visibility.Visible;
            }

            // Fallback if resource not found
            if (priorityBrush == null) priorityBrush = Brushes.Gray;

            // Set text and colors
            PrioridadText.Foreground = priorityBrush;
            PrioridadText.Text = string.IsNullOrWhiteSpace(prioridad) ? "Sin prioridad" : prioridad.Trim();
            LateralBar.Background = priorityBrush;
            if (PrioridadBadgeBorder != null) PrioridadBadgeBorder.Background = priorityBrush;

            // Hide all icons first (already handled by setting visibility above, but safer to reset if needed)
            // Ideally we should collapse all first, then show one. 
            // Let's refactor slightly to ensure correctness.
            if (PrioridadCriticaIcon != null) PrioridadCriticaIcon.Visibility = Visibility.Collapsed;
            if (PrioridadAltaIcon != null) PrioridadAltaIcon.Visibility = Visibility.Collapsed;
            if (PrioridadNormalIcon != null) PrioridadNormalIcon.Visibility = Visibility.Collapsed;
            if (PrioridadMenorIcon != null) PrioridadMenorIcon.Visibility = Visibility.Collapsed;

            // Re-apply visibility based on logic
             if (string.Equals(p, "Critica", StringComparison.OrdinalIgnoreCase) || string.Equals(p, "Crítica", StringComparison.OrdinalIgnoreCase))
            {
                if (PrioridadCriticaIcon != null) PrioridadCriticaIcon.Visibility = Visibility.Visible;
            }
            else if (string.Equals(p, "Alta", StringComparison.OrdinalIgnoreCase))
            {
                if (PrioridadAltaIcon != null) PrioridadAltaIcon.Visibility = Visibility.Visible;
            }
            else if (string.Equals(p, "Menor", StringComparison.OrdinalIgnoreCase) || string.Equals(p, "Baja", StringComparison.OrdinalIgnoreCase))
            {
                if (PrioridadMenorIcon != null) PrioridadMenorIcon.Visibility = Visibility.Visible;
            }
            else
            {
                if (PrioridadNormalIcon != null) PrioridadNormalIcon.Visibility = Visibility.Visible;
            }
        }

        private void ConfigureTipo(string tipo, string codigo)
        {
            var registro = new Registro { Tipo = tipo.Trim().Equals("Incidente", StringComparison.OrdinalIgnoreCase) ? TipoRegistro.Incidente : TipoRegistro.Requerimiento };
            TipoIconText.Text = registro.IconoTipo;
            TipoBadgeBorder.Background = registro.ColorTipoBrush;
            
            string tipoDisplay = string.IsNullOrWhiteSpace(tipo) ? "Registro" : tipo.Trim();
            TipoText.Text = $"{tipoDisplay}: {codigo}";
        }

        private void ConfigureEstado(EstadoRegistro estado)
        {
            var registro = new Registro { Estado = estado };
            EstadoText.Text = registro.EstadoTexto;
            EstadoBadge.Background = registro.ColorEstadoBrush;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}