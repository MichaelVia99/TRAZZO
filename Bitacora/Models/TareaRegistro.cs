using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.IO;

namespace Bitacora.Models;

public class TareaRegistro : INotifyPropertyChanged
{
    private string _descripcion = string.Empty;
    private bool _completada;
    private string? _archivosAdjuntos;
    private ObservableCollection<AttachmentItem> _archivosList = new();
    public List<string> AdjuntosEliminados { get; set; } = new();
    private bool _hasError;
    private int _tiempoEstimado;
    private int _tiempoTranscurrido;
    private DateTime? _fechaInicio = DateTime.Today;
    private DateTime? _fechaFin = DateTime.Today;
    private ObservableCollection<TareaRegistro> _subtareas = new();
    private bool _isSubtask;
    private int _nivel;
    private bool _isRemovable = true;
    private bool _isSorting;
    private bool _isExpanded = true;
    private bool _isLastChild;
    private bool _isFirstChild;
    private TareaRegistro? _parent;

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public TareaRegistro()
    {
        _archivosList.CollectionChanged += (s, e) =>
        {
            var newString = _archivosList.Count > 0 ? string.Join(";", _archivosList.Select(a => a.FilePath)) : null;
            if (_archivosAdjuntos != newString)
            {
                _archivosAdjuntos = newString;
                OnPropertyChanged(nameof(ArchivosAdjuntos));
            }
        };

        _subtareas.CollectionChanged += Subtareas_CollectionChanged;
    }

    private void Subtareas_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_isSorting) return;

        OnPropertyChanged(nameof(HasChildren));

        if (e.OldItems != null)
        {
            foreach (TareaRegistro item in e.OldItems)
            {
                item.PropertyChanged -= Child_PropertyChanged;
                item.Parent = null;
            }
        }

        if (e.NewItems != null)
        {
            foreach (TareaRegistro item in e.NewItems)
            {
                item.PropertyChanged += Child_PropertyChanged;
                item.Parent = this;
                item.IsSubtask = true;
                item.Nivel = this.Nivel + 1;
            }
        }

        // Defer sorting and updates to avoid collection modification during event
        if (System.Windows.Application.Current != null)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                SortChildren();
                UpdateParentAggregates();
            }));
        }
        else
        {
            SortChildren();
            UpdateParentAggregates();
        }
    }

    private void Child_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FechaInicio))
        {
            SortChildren();
            UpdateParentAggregates();
        }
        else if (e.PropertyName == nameof(FechaFin) ||
            e.PropertyName == nameof(TiempoEstimado))
        {
            UpdateParentAggregates();
        }
    }

    private void SortChildren()
    {
        if (_isSorting) return;
        if (_subtareas.Count == 0) return;

        _isSorting = true;
        try
        {
            var sorted = _subtareas.OrderBy(t => t.FechaInicio ?? DateTime.MaxValue).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                var item = sorted[i];
                item.IsLastChild = (i == sorted.Count - 1);
                item.IsFirstChild = (i == 0);

                int oldIndex = _subtareas.IndexOf(item);
                if (oldIndex != i)
                {
                    _subtareas.Move(oldIndex, i);
                }
            }
        }
        finally
        {
            _isSorting = false;
        }
    }

    public bool HasChildren => _subtareas.Count > 0;
    private bool _isUpdatingAggregates;

    private void UpdateParentAggregates()
    {
        if (_isUpdatingAggregates) return;
        if (_subtareas.Count == 0) return;

        _isUpdatingAggregates = true;

        try
        {
            var children = _subtareas.ToList();

            // Fecha Inicio = Min Fecha Inicio de hijos
            var minStart = children.Where(c => c.FechaInicio.HasValue).Min(c => c.FechaInicio);
            if (minStart.HasValue)
            {
                FechaInicio = minStart;
            }

            // Fecha Fin = Max Fecha Fin de hijos
            var maxEnd = children.Where(c => c.FechaFin.HasValue).Max(c => c.FechaFin);
            if (maxEnd.HasValue)
            {
                FechaFin = maxEnd;
            }

            // Tiempo Estimado = Sumatoria de hijos
            var totalSeconds = children.Sum(c => c.TiempoEstimado);
            TiempoEstimado = totalSeconds;
        }
        finally
        {
            _isUpdatingAggregates = false;
        }
    }

    public string Descripcion
    {
        get => _descripcion;
        set
        {
            if (_descripcion != value)
            {
                _descripcion = value;
                OnPropertyChanged();
            }
        }
    }

    public bool Completada
    {
        get => _completada;
        set
        {
            if (_completada != value)
            {
                _completada = value;
                OnPropertyChanged();
            }
        }
    }

    public string? ArchivosAdjuntos
    {
        get => _archivosAdjuntos;
        set
        {
            if (_archivosAdjuntos != value)
            {
                _archivosAdjuntos = value;
                OnPropertyChanged();
                
                // Sincronizar lista si el cambio viene de fuera
                _archivosList.Clear();
                if (!string.IsNullOrEmpty(value))
                {
                    var files = value.Split(';', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var f in files)
                    {
                        var fileName = Path.GetFileName(f);
                        long size = 0;
                        try 
                        {
                            if (File.Exists(f))
                                size = new FileInfo(f).Length;
                        }
                        catch {}

                        _archivosList.Add(new AttachmentItem 
                        { 
                            FilePath = f,
                            OriginalPath = f,
                            FileName = fileName,
                            FileSize = size,
                            FromServer = true
                        });
                    }
                }
            }
        }
    }

    public ObservableCollection<AttachmentItem> ArchivosList => _archivosList;

    public bool HasError
    {
        get => _hasError;
        set
        {
            if (_hasError != value)
            {
                _hasError = value;
                OnPropertyChanged();
            }
        }
    }

    public int TiempoEstimado
    {
        get => _tiempoEstimado;
        set
        {
            if (_tiempoEstimado != value)
            {
                _tiempoEstimado = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HorasEstimadas));
                OnPropertyChanged(nameof(MinutosEstimados));
            }
        }
    }

    public string HorasEstimadas
    {
        get => (_tiempoEstimado / 3600).ToString();
        set
        {
            if (int.TryParse(value, out int h))
            {
                int currentMinutes = (_tiempoEstimado % 3600) / 60;
                TiempoEstimado = (h * 3600) + (currentMinutes * 60);
            }
        }
    }

    public string MinutosEstimados
    {
        get => ((_tiempoEstimado % 3600) / 60).ToString();
        set
        {
            if (int.TryParse(value, out int m))
            {
                int currentHours = _tiempoEstimado / 3600;
                TiempoEstimado = (currentHours * 3600) + (m * 60);
            }
        }
    }

    public int TiempoTranscurrido
    {
        get => _tiempoTranscurrido;
        set
        {
            if (_tiempoTranscurrido != value)
            {
                _tiempoTranscurrido = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime? FechaInicio
    {
        get => _fechaInicio;
        set
        {
            if (_fechaInicio != value)
            {
                _fechaInicio = value;
                OnPropertyChanged();
                // Si la nueva fecha de inicio es mayor que la fecha de fin, actualizar fecha de fin
                if (value.HasValue && _fechaFin.HasValue && value.Value > _fechaFin.Value)
                {
                    FechaFin = value;
                }
            }
        }
    }

    public DateTime? FechaFin
    {
        get => _fechaFin;
        set
        {
            if (_fechaFin != value)
            {
                _fechaFin = value;
                OnPropertyChanged();
                // Si la nueva fecha de fin es menor que la fecha de inicio, actualizar fecha de inicio
                if (value.HasValue && _fechaInicio.HasValue && value.Value < _fechaInicio.Value)
                {
                    FechaInicio = value;
                }
            }
        }
    }

    public ObservableCollection<TareaRegistro> Subtareas => _subtareas;

    public bool IsSubtask
    {
        get => _isSubtask;
        set
        {
            if (_isSubtask != value)
            {
                _isSubtask = value;
                OnPropertyChanged();
            }
        }
    }

    public int Nivel
    {
        get => _nivel;
        set
        {
            if (_nivel != value)
            {
                _nivel = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsRemovable
    {
        get => _isRemovable;
        set
        {
            if (_isRemovable != value)
            {
                _isRemovable = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsLastChild
    {
        get => _isLastChild;
        set
        {
            if (_isLastChild != value)
            {
                _isLastChild = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsFirstChild
    {
        get => _isFirstChild;
        set
        {
            if (_isFirstChild != value)
            {
                _isFirstChild = value;
                OnPropertyChanged();
            }
        }
    }

    public TareaRegistro? Parent
    {
        get => _parent;
        set
        {
            if (_parent != value)
            {
                _parent = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
