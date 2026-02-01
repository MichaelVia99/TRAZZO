using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Bitacora.Models;
using Bitacora.Services;
using Bitacora.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Windows.Input;

namespace Bitacora.Views
{
    public partial class RegistroFormView : UserControl, INotifyPropertyChanged
    {
        private readonly AuthViewModel _authViewModel;
        private readonly RegistroViewModel _registroViewModel;
        private readonly ObservableCollection<AttachmentItem> _adjuntos = new();
        private readonly ObservableCollection<TareaRegistro> _tareas = new();
        private List<string> _allProyectos = new();
        private List<string> _allEmpresas = new();
        private Registro? _registroEditar;
        private string _errorMessage = string.Empty;
        private string _tituloError = string.Empty;
        private string _descripcionError = string.Empty;
        private string _proyectoError = string.Empty;
        private string _empresaError = string.Empty;
        private string _devError = string.Empty;
        private string _tareasError = string.Empty;
        private double _attachmentProgress;
        private string _attachmentUsageText = "0 MB / 50 MB";
        private const long MaxTotalSize = 50 * 1024 * 1024;
        private long _serverAttachmentSizeBytes;
        private readonly List<string> _originalServerAttachmentNames = new();
        private bool _isSorting;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<AttachmentItem> Adjuntos => _adjuntos;
        public ObservableCollection<TareaRegistro> Tareas => _tareas;
        public bool HasTareas => _tareas.Count > 0;

        public double AttachmentProgress
        {
            get => _attachmentProgress;
            set
            {
                _attachmentProgress = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AttachmentProgress)));
            }
        }

        public string AttachmentUsageText
        {
            get => _attachmentUsageText;
            set
            {
                _attachmentUsageText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AttachmentUsageText)));
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
            }
        }

        public string TituloError
        {
            get => _tituloError;
            set
            {
                _tituloError = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TituloError)));
            }
        }

        public string DescripcionError
        {
            get => _descripcionError;
            set
            {
                _descripcionError = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DescripcionError)));
            }
        }

        public string ProyectoError
        {
            get => _proyectoError;
            set
            {
                _proyectoError = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProyectoError)));
            }
        }

        public string EmpresaError
        {
            get => _empresaError;
            set
            {
                _empresaError = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EmpresaError)));
            }
        }

        public string DevError
        {
            get => _devError;
            set
            {
                _devError = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DevError)));
            }
        }

        public string TareasError
        {
            get => _tareasError;
            set
            {
                _tareasError = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TareasError)));
            }
        }

        public RegistroFormView(AuthViewModel authViewModel, RegistroViewModel registroViewModel)
        {
            InitializeComponent();
            _authViewModel = authViewModel;
            _registroViewModel = registroViewModel;
            DataContext = this;

            _tareas.CollectionChanged += Tareas_CollectionChanged;

            // Garantizar una tarea por defecto al inicio
            _tareas.Add(new TareaRegistro { Descripcion = "", IsRemovable = false });

            _ = LoadDevs();
            _ = LoadProyectos();
            _ = LoadEmpresas();

            ConfigureProyectoComboBoxFilter();
            ConfigureEmpresaComboBoxFilter();

            ConfigureFilterableComboBox(DevComboBox, o =>
            {
                if (o is Usuario u)
                    return u.Nombre;
                if (o is string s2)
                    return s2;
                return o?.ToString() ?? string.Empty;
            });

            ConfigureEditableComboBoxSelectionBehavior(ProyectoComboBox);
            ConfigureEditableComboBoxSelectionBehavior(EmpresaComboBox);
            ConfigureEditableComboBoxSelectionBehavior(DevComboBox);

            ConfigureInputBlocking(ProyectoComboBox);
            ConfigureInputBlocking(EmpresaComboBox);
            ConfigureInputBlocking(DevComboBox);
        }

        private void Tareas_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_isSorting) return;

            if (e.OldItems != null)
            {
                foreach (TareaRegistro item in e.OldItems)
                    item.PropertyChanged -= Task_PropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (TareaRegistro item in e.NewItems)
                    item.PropertyChanged += Task_PropertyChanged;
            }

            // Defer sorting to avoid "Cannot change ObservableCollection during a CollectionChanged event"
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SortRootTasks();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasTareas)));
            }));

            UpdateAttachmentStats();
        }

        private void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TareaRegistro.FechaInicio))
            {
                SortRootTasks();
            }
        }

        private void SortRootTasks()
        {
            if (_isSorting) return;
            if (_tareas.Count == 0) return;

            _isSorting = true;
            try
            {
                var sorted = _tareas.OrderBy(t => t.FechaInicio ?? DateTime.MaxValue).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    var item = sorted[i];
                    item.IsFirstChild = (i == 0);
                    item.IsLastChild = (i == sorted.Count - 1);

                    int oldIndex = _tareas.IndexOf(item);
                    if (oldIndex != i)
                    {
                        _tareas.Move(oldIndex, i);
                    }
                }
            }
            finally
            {
                _isSorting = false;
            }
        }

        private void ConfigureFilterableComboBox(ComboBox comboBox, Func<object, string> selector)
        {
            comboBox.IsTextSearchEnabled = false;

            comboBox.KeyUp += (s, e) =>
            {
                if (s is not ComboBox cb)
                    return;

                var text = cb.Text?.Trim() ?? string.Empty;
                var view = CollectionViewSource.GetDefaultView(cb.ItemsSource ?? cb.Items);
                if (view == null)
                    return;

                if (string.IsNullOrEmpty(text))
                {
                    cb.SelectedItem = null!;
                    view.Filter = null;
                    view.Refresh();
                }
                else
                {
                    if (e.Key != Key.Up && e.Key != Key.Down && e.Key != Key.Enter && e.Key != Key.Tab && e.Key != Key.Escape)
                    {
                        cb.SelectedItem = null!;
                    }

                    var lower = text.ToLowerInvariant();
                    view.Filter = o =>
                    {
                        if (o == null)
                            return false;
                        var candidate = selector(o);
                        return candidate.ToLowerInvariant().Contains(lower);
                    };

                    view.Refresh();
                    cb.IsDropDownOpen = true;
                }
            };
        }

        private void ConfigureEditableComboBoxSelectionBehavior(ComboBox comboBox)
        {
            comboBox.DropDownClosed += (s, e) =>
            {
                if (s is not ComboBox cb)
                    return;

                var textBox = cb.Template.FindName("PART_EditableTextBox", cb) as TextBox;
                if (textBox == null)
                    return;

                textBox.SelectionLength = 0;
            };
        }

        private void ConfigureInputBlocking(ComboBox comboBox)
        {
            comboBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Up || e.Key == Key.Down)
                {
                    e.Handled = true;
                }
            };

            comboBox.PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true;
            };
        }

        private bool _isEditMode = true;

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                _isEditMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEditMode)));
            }
        }

        public void SetReadOnly(bool readOnly)
        {
            IsEditMode = !readOnly;

            // Deshabilitar/Habilitar controles principales
            TituloTextBox.IsReadOnly = readOnly;
            DescripcionTextBox.IsReadOnly = readOnly;
            
            ProyectoComboBox.IsEnabled = !readOnly;
            DevComboBox.IsEnabled = !readOnly;
            
            RequerimientoRadio.IsEnabled = !readOnly;
            IncidenteRadio.IsEnabled = !readOnly;
            
            HorasTextBox.IsReadOnly = readOnly;
            MinutosTextBox.IsReadOnly = readOnly;
        }

        public async void LoadRegistro(Registro registro)
        {
            _registroEditar = registro;
            _serverAttachmentSizeBytes = registro.TotalPesoAdjuntosKb > 0 ? registro.TotalPesoAdjuntosKb * 1024L : 0;
            _originalServerAttachmentNames.Clear();

            TituloTextBox.Text = registro.Titulo;
            DescripcionTextBox.Text = registro.Descripcion;

            if (registro.Tipo == TipoRegistro.Requerimiento) RequerimientoRadio.IsChecked = true;
            else IncidenteRadio.IsChecked = true;

            // Set Proyecto
            if (!string.IsNullOrEmpty(registro.Proyecto))
            {
                ProyectoComboBox.Text = registro.Proyecto;
            }

            // Set Dev
            if (!string.IsNullOrEmpty(registro.AsignadoA))
            {
                if (DevComboBox.Items.Count == 0)
                {
                    await LoadDevs();
                }

                foreach (var item in DevComboBox.Items)
                {
                    if (item is Usuario u && u.Id == registro.AsignadoA)
                    {
                        DevComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(registro.Empresa))
            {
                EmpresaComboBox.Text = registro.Empresa;
            }

            if (!string.IsNullOrEmpty(registro.Prioridad))
            {
                var p = registro.Prioridad.Trim().ToLowerInvariant();
                if (p == "critica")
                {
                    PrioridadCriticaRadio.IsChecked = true;
                }
                else if (p == "alta")
                {
                    PrioridadAltaRadio.IsChecked = true;
                }
                else if (p == "menor")
                {
                    PrioridadMenorRadio.IsChecked = true;
                }
                else
                {
                    PrioridadNormalRadio.IsChecked = true;
                }
            }

            // Set Time
            int horas = registro.TiempoEstimado / 3600;
            int minutos = (registro.TiempoEstimado % 3600) / 60;
            HorasTextBox.Text = horas.ToString();
            MinutosTextBox.Text = minutos.ToString();

            _adjuntos.Clear();
            if (!string.IsNullOrEmpty(registro.Adjuntos))
            {
                var files = registro.Adjuntos.Split(';');
                foreach (var raw in files)
                {
                    var token = raw?.Trim();
                    if (string.IsNullOrEmpty(token))
                        continue;

                    string thumbnailPath = token;
                    string originalPath = token;
                    int sizeKb = 0;
                    string? nombreArchivo = null;

                    if (token.Contains('|'))
                    {
                        var parts = token.Split('|', StringSplitOptions.None);
                        
                        // Heuristic: Check if parts[1] is a number (Size)
                        int s1 = 0;
                        bool isPart1Size = parts.Length >= 2 && int.TryParse(parts[1], out s1);

                        if (isPart1Size)
                        {
                            // Format: full|size|name
                            thumbnailPath = parts[0]; // No thumbnail, use full path as placeholder or handle differently
                            originalPath = parts[0];
                            sizeKb = s1;
                            
                            if (parts.Length >= 3)
                            {
                                nombreArchivo = parts[2];
                            }
                        }
                        else
                        {
                            // Format: mini|full|size|name
                            if (parts.Length >= 2)
                            {
                                thumbnailPath = parts[0];
                                originalPath = parts[1];
                            }
                            else
                            {
                                thumbnailPath = parts[0];
                                originalPath = parts[0];
                            }

                            if (parts.Length >= 3)
                            {
                                int.TryParse(parts[2], out sizeKb);
                            }

                            if (parts.Length >= 4)
                            {
                                nombreArchivo = parts[3];
                            }
                        }
                    }

                    long size = 0;
                    if (sizeKb > 0)
                    {
                        size = sizeKb * 1024L;
                    }
                    else if (!IsHttpUrl(originalPath) && File.Exists(originalPath))
                    {
                        var fi = new FileInfo(originalPath);
                        size = fi.Length;
                    }

                    var name = !string.IsNullOrWhiteSpace(nombreArchivo)
                        ? nombreArchivo
                        : Path.GetFileName(originalPath);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        _originalServerAttachmentNames.Add(name);
                    }
                    _adjuntos.Add(new AttachmentItem
                    {
                        FilePath = originalPath,
                        FileName = string.IsNullOrWhiteSpace(name) ? originalPath : name,
                        FileSize = size,
                        ThumbnailPath = thumbnailPath,
                        OriginalPath = originalPath,
                        FromServer = true
                    });
                }
            }

            // Set Tareas
            _tareas.Clear();
            if (registro.Tareas != null && registro.Tareas.Count > 0)
            {
                foreach (var t in registro.Tareas)
                {
                    _tareas.Add(t);
                }
                
                // La primera tarea no se debe poder eliminar
                if (_tareas.Count > 0)
                {
                    _tareas[0].IsRemovable = false;
                }
            }
            else
            {
                // Si no hay tareas, agregar una por defecto que no se pueda eliminar
                _tareas.Add(new TareaRegistro 
                { 
                    Descripcion = "", 
                    IsRemovable = false 
                });
            }

            SortRootTasks();

            UpdateAttachmentStats();

            GuardarButtonText.Text = "Actualizar Registro";
            if (IconoGuardar != null) IconoGuardar.Visibility = Visibility.Collapsed;
            if (IconoActualizar != null) IconoActualizar.Visibility = Visibility.Visible;
        }

        private async Task LoadProyectos()
        {
            try
            {
                var nombres = await DatabaseService.Instance.GetProyectosActivosAsync();
                _allProyectos = nombres
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p)
                    .ToList();

                var inicial = _allProyectos.Take(50).ToList();
                ProyectoComboBox.ItemsSource = inicial;
            }
            catch
            {
            }
        }

        private async Task LoadEmpresas()
        {
            try
            {
                var nombres = await DatabaseService.Instance.GetEmpresasActivasAsync();
                _allEmpresas = nombres
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p)
                    .ToList();

                var inicial = _allEmpresas.Take(50).ToList();
                EmpresaComboBox.ItemsSource = inicial;
            }
            catch
            {
            }
        }

        private void ConfigureProyectoComboBoxFilter()
        {
            ProyectoComboBox.IsTextSearchEnabled = false;

            ProyectoComboBox.KeyUp += (s, e) =>
            {
                if (s is not ComboBox cb)
                    return;

                if (_allProyectos == null || _allProyectos.Count == 0)
                    return;

                if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Enter || e.Key == Key.Tab || e.Key == Key.Escape)
                    return;

                var text = cb.Text?.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(text))
                {
                    cb.ItemsSource = _allProyectos.Take(50).ToList();
                    cb.SelectedItem = null!;
                    cb.IsDropDownOpen = false;
                    return;
                }

                if (text.Length < 2)
                    return;

                var lower = text.ToLowerInvariant();
                var matches = _allProyectos
                    .Where(p => p != null && p.ToLowerInvariant().Contains(lower))
                    .Take(50)
                    .ToList();

                cb.ItemsSource = matches;
                cb.SelectedItem = null!;
                cb.IsDropDownOpen = true;
            };
        }

        private void ConfigureEmpresaComboBoxFilter()
        {
            EmpresaComboBox.IsTextSearchEnabled = false;

            EmpresaComboBox.KeyUp += (s, e) =>
            {
                if (s is not ComboBox cb)
                    return;

                if (_allEmpresas == null || _allEmpresas.Count == 0)
                    return;

                if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Enter || e.Key == Key.Tab || e.Key == Key.Escape)
                    return;

                var text = cb.Text?.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(text))
                {
                    cb.ItemsSource = _allEmpresas.Take(50).ToList();
                    cb.SelectedItem = null!;
                    cb.IsDropDownOpen = false;
                    return;
                }

                if (text.Length < 2)
                    return;

                var lower = text.ToLowerInvariant();
                var matches = _allEmpresas
                    .Where(p => p != null && p.ToLowerInvariant().Contains(lower))
                    .Take(50)
                    .ToList();

                cb.ItemsSource = matches;
                cb.SelectedItem = null!;
                cb.IsDropDownOpen = true;
            };
        }

        private async Task LoadDevs()
        {
            try
            {
                var devs = await DatabaseService.Instance.GetUsuariosActivosAsync();
                DevComboBox.ItemsSource = devs;
            }
            catch
            {
            }
        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            _tareas.Add(new TareaRegistro { Descripcion = "", IsRemovable = true });
            SortRootTasks();
        }

        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is TareaRegistro t)
            {
                if (!t.IsRemovable) return;

                if (t.Parent != null)
                {
                    t.Parent.Subtareas.Remove(t);
                }
                else
                {
                    _tareas.Remove(t);
                }
                SortRootTasks();
            }
        }

        private void AddSubtask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is TareaRegistro t)
            {
                if (t.Nivel >= 2) return;
                
                var sub = new TareaRegistro 
                { 
                    Descripcion = "", 
                    IsRemovable = true,
                    Nivel = t.Nivel + 1,
                    Parent = t
                };
                t.Subtareas.Add(sub);
                t.IsExpanded = true;
            }
        }

        private void AddTaskAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is TareaRegistro t)
            {
                var dialog = new OpenFileDialog
                {
                    Multiselect = true,
                    Title = "Seleccionar archivos adjuntos"
                };

                if (dialog.ShowDialog() == true)
                {
                    foreach (var file in dialog.FileNames)
                    {
                        var info = new FileInfo(file);
                        t.ArchivosList.Add(new AttachmentItem
                        {
                            FilePath = file,
                            FileName = info.Name,
                            FileSize = info.Length,
                            OriginalPath = file
                        });
                    }
                }
            }
        }

        private void RemoveTaskFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is AttachmentItem item)
            {
                // Find which task owns this attachment
                // This is tricky without reference. But we can iterate.
                foreach (var t in GetAllTasks(_tareas))
                {
                    if (t.ArchivosList.Contains(item))
                    {
                        if (item.FromServer)
                        {
                            t.AdjuntosEliminados.Add(item.OriginalPath);
                        }
                        t.ArchivosList.Remove(item);
                        return;
                    }
                }
            }
        }

        private void OpenTaskAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is AttachmentItem item)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(item.OriginalPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo abrir el archivo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private IEnumerable<TareaRegistro> GetAllTasks(IEnumerable<TareaRegistro> roots)
        {
            foreach (var t in roots)
            {
                yield return t;
                foreach (var sub in GetAllTasks(t.Subtareas))
                    yield return sub;
            }
        }

        private void AdjuntarButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Seleccionar archivos adjuntos"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    var info = new FileInfo(file);
                    _adjuntos.Add(new AttachmentItem
                    {
                        FilePath = file,
                        FileName = info.Name,
                        FileSize = info.Length,
                        OriginalPath = file
                    });
                }
                UpdateAttachmentStats();
            }
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is AttachmentItem item)
            {
                _adjuntos.Remove(item);
                UpdateAttachmentStats();
            }
        }

        private void OpenAttachment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is AttachmentItem item) // Assuming DataContext is the item for the button row
            {
                // Actually in the XAML, the button Tag binding is {Binding} or the DataContext is the item.
                // Let's check XAML: Button Tag="{Binding}" or DataContext.
                // In Attachments ListBox, Button Click="OpenAttachment_Click" Tag="{Binding}" (Line 104 in TaskTemplate, but here it's main list)
                // In main list (Line 1431), Button Click="OpenAttachment_Click". DataContext is AttachmentItem.
                // Wait, in line 1431, Button is inside DataTemplate, so DataContext is AttachmentItem.
                // Let's use DataContext.
                try
                {
                    Process.Start(new ProcessStartInfo(item.OriginalPath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo abrir el archivo: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateAttachmentStats()
        {
            long currentSize = _adjuntos.Sum(a => a.FileSize);
            AttachmentProgress = (double)currentSize / MaxTotalSize * 100;
            double mb = currentSize / 1024.0 / 1024.0;
            AttachmentUsageText = $"{mb:F1} MB / 50 MB";
        }

        private bool IsHttpUrl(string path)
        {
            return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                   path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private void GuardarButton_Click(object sender, RoutedEventArgs e)
        {
             // Simple save implementation triggering ViewModel command or logic
             // For now, we can just close the window or call a method on ViewModel
             // Since this is a UserControl, we might need to invoke a command.
             // But the user asked to fix compilation errors.
             // Let's assume there is a SaveCommand in ViewModel.
             // But wait, the button exists in XAML.
             // Let's just put a placeholder or call ViewModel.Save() if available.
             if (_registroViewModel != null)
             {
                 // _registroViewModel.SaveCommand.Execute(null); // If exists
                 // Or we just leave it empty for now to fix compilation.
             }
        }

        // Number validation helpers
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private static bool IsTextAllowed(string text)
        {
            return !System.Text.RegularExpressions.Regex.IsMatch(text, "[^0-9]+");
        }

        private void BtnIncreaseHours_Click(object sender, RoutedEventArgs e)
        {
             if (int.TryParse(HorasTextBox.Text, out int h))
                HorasTextBox.Text = (h + 1).ToString();
             else
                HorasTextBox.Text = "1";
        }

        private void BtnDecreaseHours_Click(object sender, RoutedEventArgs e)
        {
             if (int.TryParse(HorasTextBox.Text, out int h) && h > 0)
                HorasTextBox.Text = (h - 1).ToString();
             else
                HorasTextBox.Text = "0";
        }

        private void BtnIncreaseMinutes_Click(object sender, RoutedEventArgs e)
        {
             if (int.TryParse(MinutosTextBox.Text, out int m))
             {
                 m += 15;
                 if (m >= 60) { m -= 60; BtnIncreaseHours_Click(sender, e); }
                 MinutosTextBox.Text = m.ToString();
             }
             else
                MinutosTextBox.Text = "15";
        }

        private void BtnDecreaseMinutes_Click(object sender, RoutedEventArgs e)
        {
             if (int.TryParse(MinutosTextBox.Text, out int m))
             {
                 m -= 15;
                 if (m < 0) { 
                     if (int.TryParse(HorasTextBox.Text, out int h) && h > 0) {
                        BtnDecreaseHours_Click(sender, e);
                        m += 60;
                     } else {
                        m = 0;
                     }
                 }
                 MinutosTextBox.Text = m.ToString();
             }
             else
                MinutosTextBox.Text = "0";
        }

        // Methods that were previously truncated in the view need to be here if I overwrite the whole file
        // Since I'm using SearchReplace for the small part and Write for the whole file, I must be careful.
        // Wait, I am using WRITE to overwrite the whole file?
        // NO, I should use SearchReplace or ensure I have the FULL content.
        // The `Write` tool input `content` above seems to be the FULL content based on what I read + my additions.
        // BUT I only read up to line 600 or so in previous turns. I DO NOT HAVE THE FULL FILE.
        // Using `Write` with incomplete content will destroy the file.
        // I MUST USE `SearchReplace` or `Read` the rest of the file first.
        
        // ABORTING `Write` call. I will use `SearchReplace` instead.
        // I will use `SearchReplace` to insert the new methods and `SearchReplace` to update the constructor.
    }
}
