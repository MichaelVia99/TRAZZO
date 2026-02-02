using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Bitacora.Models;

public enum TipoRegistro
{
    Requerimiento,
    Incidente
}

public enum EstadoRegistro
{
    Pendiente,    // Por Planificar
    EnEspera,     // Con tiempo estimado, listo para iniciar
    EnProceso,    // En Curso
    Pausado,      // En Pausa
    Cerrado       // Completado
}

public class Registro : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private int _tiempoTranscurrido;
    private EstadoRegistro _estado = EstadoRegistro.Pendiente;

    public string Id { get; set; } = string.Empty;
    public int Numero { get; set; } // Secuencial por tipo
    public TipoRegistro Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string CreadoPor { get; set; } = string.Empty;
    public string? AsignadoA { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaAsignacion { get; set; }
    public DateTime? FechaCierre { get; set; }
    
    public EstadoRegistro Estado
    {
        get => _estado;
        set
        {
            if (SetProperty(ref _estado, value))
            {
                OnPropertyChanged(nameof(EstadoTexto));
                OnPropertyChanged(nameof(EstadoAbreviado));
                OnPropertyChanged(nameof(ColorHex));
                OnPropertyChanged(nameof(ColorEstadoBrush));
                OnPropertyChanged(nameof(IconoEstado));
                OnPropertyChanged(nameof(ColorEstado));
            }
        }
    }

    public int TiempoTranscurrido
    {
        get => _tiempoTranscurrido;
        set
        {
            if (SetProperty(ref _tiempoTranscurrido, value))
            {
                OnPropertyChanged(nameof(TiempoFormateado));
                OnPropertyChanged(nameof(TiempoComparativo));
                OnPropertyChanged(nameof(TiempoRestante));
                OnPropertyChanged(nameof(TiempoRestanteFormateado));
            }
        }
    }

    private int _tiempoEstimado;
    public int TiempoEstimado
    {
        get => _tiempoEstimado;
        set
        {
            if (SetProperty(ref _tiempoEstimado, value))
            {
                OnPropertyChanged(nameof(TiempoComparativo));
                OnPropertyChanged(nameof(TiempoRestante));
                OnPropertyChanged(nameof(TiempoRestanteFormateado));
            }
        }
    }
    public string? Proyecto { get; set; }
    public string? Empresa { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Prioridad { get; set; }
    public string? Adjuntos { get; set; } // Rutas separadas por punto y coma
    public string? NombreAsignado { get; set; }

    public List<TareaRegistro> Tareas { get; set; } = new List<TareaRegistro>();
    
    // Propiedad auxiliar para cargar conteo desde DB sin cargar todas las tareas
    public int TaskCountFromDb { get; set; }
        public int TotalPesoAdjuntosKb { get; set; }
        public List<string> AdjuntosEliminados { get; set; } = new List<string>();
        public List<string> AdjuntosTareasEliminados { get; set; } = new List<string>();

    public int TotalTareas => Tareas?.Count > 0 ? Tareas.Count : TaskCountFromDb;
    public int TareasCompletadas => Tareas?.Count(t => t.Completada) ?? 0;
    public bool TieneTareas => TotalTareas > 0;

    public string AsignadoAUpper => NombreAsignado?? AsignadoA?? string.Empty;

    public string TipoTexto => Tipo == TipoRegistro.Requerimiento ? "Requerimiento" : "Incidente";

    public string CodigoTipo
    {
        get => Tipo == TipoRegistro.Requerimiento ? "R" : "I";
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var v = value.Trim().ToUpperInvariant();
            if (v == "REQ" || v == "R")
            {
                Tipo = TipoRegistro.Requerimiento;
            }
            else if (v == "INC" || v == "I")
            {
                Tipo = TipoRegistro.Incidente;
            }
        }
    }

    public string CodigoFull => $"{Numero:D4}";

    public string IdNumero 
    {
        get 
        {
            return Numero > 0 ? Numero.ToString("D4") : "0000";
        }
    }

    public string IconoTipo
    {
        get
        {
            return Tipo == TipoRegistro.Requerimiento 
                ? "\uE8F1" // Library/Book for Requirement (Modern)
                : "\uE783"; // ErrorBadge for Incident
        }
    }

    public string ColorTipoHex
    {
        get
        {
            return Tipo == TipoRegistro.Requerimiento 
                ? "#009688" // Teal
                : "#C2185B"; // Magenta
        }
    }

    public SolidColorBrush ColorTipoBrush
    {
        get
        {
            try
            {
                return (SolidColorBrush)(new BrushConverter().ConvertFrom(ColorTipoHex) ?? Brushes.Gray);
            }
            catch
            {
                return Brushes.Gray;
            }
        }
    }

    public string EstadoTexto
    {
        get
        {
            return Estado switch
            {
                EstadoRegistro.Pendiente => "Por Planificar",
                EstadoRegistro.EnEspera => "En Espera",
                EstadoRegistro.EnProceso => "En Curso",
                EstadoRegistro.Pausado => "En Pausa",
                EstadoRegistro.Cerrado => "Completado",
                _ => "Desconocido"
            };
        }
    }

    public string EstadoAbreviado
    {
        get
        {
            return Estado switch
            {
                EstadoRegistro.Pendiente => "PP",
                EstadoRegistro.EnEspera => "EE",
                EstadoRegistro.EnProceso => "EC",
                EstadoRegistro.Pausado => "PA",
                EstadoRegistro.Cerrado => "C",
                _ => "??"
            };
        }
    }

    public string ColorHex
    {
        get
        {
            return Estado switch
            {
                EstadoRegistro.Pendiente => "#7E57C2",
                EstadoRegistro.EnEspera => "#FBC02D",
                EstadoRegistro.EnProceso => "#00BCD4",
                EstadoRegistro.Pausado => "#607D8B",
                EstadoRegistro.Cerrado => "#4CAF50",
                _ => "#9E9E9E"
            };
        }
    }

    public SolidColorBrush ColorEstadoBrush
    {
        get
        {
            try
            {
                return (SolidColorBrush)(new BrushConverter().ConvertFrom(ColorHex) ?? Brushes.Gray);
            }
            catch
            {
                return Brushes.Gray;
            }
        }
    }

    public string IconoEstado
    {
        get
        {
            return Estado switch
            {
                EstadoRegistro.Cerrado => "\uE73E", // Checkmark
                EstadoRegistro.EnProceso => "\uE916", // Stopwatch
                EstadoRegistro.Pausado => "\uE769", // Pause
                EstadoRegistro.EnEspera => "\uE823", // Clock
                _ => "\uE916" 
            };
        }
    }

    public SolidColorBrush ColorEstado
    {
        get
        {
            return ColorEstadoBrush;
        }
    }

    public string TiempoFormateado
    {
        get
        {
            var horas = TiempoTranscurrido / 3600;
            var minutos = (TiempoTranscurrido % 3600) / 60;
            return $"{horas:D2}:{minutos:D2}";
        }
    }

    public string TiempoComparativo
    {
        get
        {
            var tHoras = TiempoTranscurrido / 3600;
            var tMinutos = (TiempoTranscurrido % 3600) / 60;
            
            var eHoras = TiempoEstimado / 3600;
            var eMinutos = (TiempoEstimado % 3600) / 60;

            if (TiempoEstimado > 0)
            {
                return $"{tHoras:D2}:{tMinutos:D2} / {eHoras:D2}:{eMinutos:D2}";
            }
            return $"{tHoras:D2}:{tMinutos:D2}";
        }
    }

    public int TiempoRestante => Math.Max(0, TiempoEstimado - TiempoTranscurrido);
    
    public string TiempoRestanteFormateado
    {
        get
        {
            var tr = TiempoRestante;
            var horas = tr / 3600;
            var minutos = (tr % 3600) / 60;
            return $"{horas:D2}:{minutos:D2}";
        }
    }

    public string IconoPrioridad
    {
        get
        {
            var p = (Prioridad ?? "").Trim().ToUpperInvariant();
            if (p.Contains("ALT") || p.Contains("HIGH") || p.Contains("URG")) return "\uE814"; // Important
            if (p.Contains("MED")) return "\uE736"; // Medium/Normal
            if (p.Contains("BAJ") || p.Contains("LOW")) return "\uE96D"; // Down
            return "\uE7ba"; // Warning
        }
    }

    public SolidColorBrush ColorPrioridadBrush
    {
        get
        {
            var p = (Prioridad ?? "").Trim().ToUpperInvariant();
            if (p.Contains("ALT") || p.Contains("HIGH") || p.Contains("URG")) return Brushes.Red;
            if (p.Contains("MED")) return Brushes.Orange;
            if (p.Contains("BAJ") || p.Contains("LOW")) return Brushes.Green;
            return Brushes.Gray;
        }
    }

    public int PrioridadNivel
        {
            get
            {
                var p = (Prioridad ?? "").Trim().ToUpperInvariant();
                if (p.Contains("CRIT") || p.Contains("CRI")) return 4;
                if (p.Contains("ALT") || p.Contains("HIGH") || p.Contains("URG")) return 3;
                if (p.Contains("NORM") || p.Contains("MED")) return 2;
                if (p.Contains("MEN") || p.Contains("LOW") || p.Contains("BAJ")) return 1;
                return 0;
            }
        }
}

