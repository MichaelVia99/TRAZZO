using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Bitacora.Models;
using Bitacora.Services;

namespace Bitacora.ViewModels;

public class RegistroViewModel : ViewModelBase
{
    private ObservableCollection<Registro> _registros = new();
    private List<Registro> _todosRegistros = new();
    private Registro? _registroActivo;
    private DispatcherTimer? _timer;
    private DateTime? _inicioTiempo;

    private int _countPlanificar;
    private int _countEnEspera;
    private int _countCurso;
    private int _countPausado;
    private int _countCompletados;
    private int _countTodos;
    private EstadoRegistro? _estadoFiltroActual;

    public RegistroViewModel()
    {
        NotificationService.Instance.OnStatusUpdate += HandleStatusUpdate;
    }

    private void HandleStatusUpdate(StatusUpdateData data)
    {
        // Actualizar en el hilo de UI
        System.Windows.Application.Current.Dispatcher.Invoke(async () =>
        {
            var registro = _todosRegistros.FirstOrDefault(r => r.Id == data.RegistroId);
            if (registro == null) return;

            registro.Estado = data.Estado;
            registro.TiempoTranscurrido = data.TiempoTranscurrido;

            if (RegistroActivo != null && RegistroActivo.Id == data.RegistroId)
            {
                if (data.Estado == EstadoRegistro.EnProceso)
                {
                    if (_timer == null || !_timer.IsEnabled)
                    {
                        _inicioTiempo = DateTime.Now;
                        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                        var regLocal = registro;
                        _timer.Tick += (s, e) =>
                        {
                            if (RegistroActivo != null && _inicioTiempo != null)
                            {
                                var tiempoAdicional = (DateTime.Now - _inicioTiempo.Value).TotalSeconds;
                                var nuevoTiempo = regLocal.TiempoTranscurrido + (int)tiempoAdicional;
                                
                                // Al ser Observable, esto actualiza la UI automáticamente
                                if (RegistroActivo.Id == regLocal.Id)
                                {
                                    RegistroActivo.TiempoTranscurrido = nuevoTiempo;
                                }
                            }
                        };
                        _timer.Start();
                    }
                }
                else
                {
                    _timer?.Stop();
                    _timer = null;
                    _inicioTiempo = null;
                }
            }

            ActualizarContadores();
            FiltrarPorEstado(EstadoFiltroActual);
        });
    }

    public ObservableCollection<Registro> Registros
    {
        get => _registros;
        set => SetProperty(ref _registros, value);
    }

    public int CountPlanificar
    {
        get => _countPlanificar;
        set => SetProperty(ref _countPlanificar, value);
    }

    public int CountEnEspera
    {
        get => _countEnEspera;
        set => SetProperty(ref _countEnEspera, value);
    }

    public int CountCurso
    {
        get => _countCurso;
        set => SetProperty(ref _countCurso, value);
    }

    public int CountPausado
    {
        get => _countPausado;
        set => SetProperty(ref _countPausado, value);
    }

    public int CountCompletados
    {
        get => _countCompletados;
        set => SetProperty(ref _countCompletados, value);
    }

    public int CountTodos
    {
        get => _countTodos;
        set => SetProperty(ref _countTodos, value);
    }

    public EstadoRegistro? EstadoFiltroActual
    {
        get => _estadoFiltroActual;
        set => SetProperty(ref _estadoFiltroActual, value);
    }

    public Registro? RegistroActivo
    {
        get => _registroActivo;
        set
        {
            if (SetProperty(ref _registroActivo, value))
            {
                OnPropertyChanged(nameof(TieneRegistroActivo));
            }
        }
    }

    public bool TieneRegistroActivo => _registroActivo != null;

    public async Task CargarRegistrosAsync()
    {
        var registros = await DatabaseService.Instance.GetAllRegistrosAsync();
        ActualizarListaRegistros(registros);
    }

    public async Task CargarRegistrosPorCreadorAsync(string creadoPor)
    {
        var registros = await DatabaseService.Instance.GetRegistrosByCreadorAsync(creadoPor);
        ActualizarListaRegistros(registros);
    }

    public async Task CargarRegistrosPorAsignadoAsync(string asignadoA)
    {
        var registros = await DatabaseService.Instance.GetRegistrosByAsignadoAsync(asignadoA);
        ActualizarListaRegistros(registros);
    }

    private void ActualizarListaRegistros(List<Registro> registros)
    {
        _todosRegistros = registros;
        ActualizarContadores();
        FiltrarPorEstado(EstadoFiltroActual);

        // Auto-resume: Si al cargar hay un registro en proceso pero no lo estamos trakeando (ej. reinicio),
        // lo activamos localmente para que funcione la pausa y el cronómetro.
        if (RegistroActivo == null)
        {
            var registroEnProceso = _todosRegistros.FirstOrDefault(r => r.Estado == EstadoRegistro.EnProceso);
            if (registroEnProceso != null)
            {
                RegistroActivo = registroEnProceso;
                _inicioTiempo = DateTime.Now; 
                
                _timer?.Stop();
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                
                // Capturar estado base para el cronómetro
                var tiempoBase = registroEnProceso.TiempoTranscurrido;
                var registroId = registroEnProceso.Id;

                _timer.Tick += (s, e) =>
                {
                    if (RegistroActivo != null && _inicioTiempo != null && RegistroActivo.Id == registroId)
                    {
                        var regUI = _todosRegistros.FirstOrDefault(r => r.Id == registroId);
                        if (regUI != null)
                        {
                            var tiempoTotal = tiempoBase + (int)(DateTime.Now - _inicioTiempo.Value).TotalSeconds;
                            regUI.TiempoTranscurrido = tiempoTotal;
                            
                            if (RegistroActivo != regUI)
                                RegistroActivo.TiempoTranscurrido = tiempoTotal;
                        }
                    }
                };
                _timer.Start();

                // Registrar en socket por seguridad
                _ = NotificationService.Instance.RegistrarTareaActivaAsync(registroEnProceso.Id);
            }
        }
    }

    private void ActualizarContadores()
    {
        CountPlanificar = _todosRegistros.Count(r => r.Estado == EstadoRegistro.Pendiente);
        CountEnEspera = _todosRegistros.Count(r => r.Estado == EstadoRegistro.EnEspera);
        CountCurso = _todosRegistros.Count(r => r.Estado == EstadoRegistro.EnProceso);
        CountPausado = _todosRegistros.Count(r => r.Estado == EstadoRegistro.Pausado);
        CountCompletados = _todosRegistros.Count(r => r.Estado == EstadoRegistro.Cerrado);
        CountTodos = _todosRegistros.Count;
    }

    public void FiltrarPorEstado(EstadoRegistro? estado)
    {
        EstadoFiltroActual = estado;
        if (estado == null)
        {
            Registros = new ObservableCollection<Registro>(_todosRegistros);
        }
        else
        {
            Registros = new ObservableCollection<Registro>(_todosRegistros.Where(r => r.Estado == estado));
        }
    }

    public async Task<string> CrearRegistroAsync(Registro registro)
    {
        var id = await DatabaseService.Instance.InsertRegistroAsync(registro);
        await CargarRegistrosAsync();

        // La notificación de asignación se maneja en RegistroFormView
        // para que se muestre solo cuando corresponda

        return id;
    }

    public async Task ActualizarRegistroAsync(Registro registro)
    {
        await DatabaseService.Instance.UpdateRegistroAsync(registro);
        await CargarRegistrosAsync();
    }

    public async Task AsignarRegistroAsync(string registroId, string desarrolladorId)
    {
        var registro = await DatabaseService.Instance.GetRegistroByIdAsync(registroId);
        if (registro != null)
        {
            registro.AsignadoA = desarrolladorId;
            registro.FechaAsignacion = DateTime.Now;
            registro.Estado = EstadoRegistro.Pendiente;

            await DatabaseService.Instance.UpdateRegistroAsync(registro);
            await CargarRegistrosAsync();

            NotificationService.Instance.ShowAssignmentNotification(
                registro.Id,
                registro.TipoTexto,
                registro.Titulo,
                registro.Prioridad,
                registro.Estado,
                registro.Proyecto,
                registro.Empresa,
                registro.CodigoFull
            );

            _ = NotificationService.Instance.SendAssignmentAsync(
                desarrolladorId,
                registro.Id,
                registro.TipoTexto,
                registro.Titulo,
                registro.Prioridad,
                registro.Estado,
                registro.Proyecto,
                registro.Empresa,
                registro.CodigoFull
            );
        }
    }

    public async Task IniciarRegistroAsync(string registroId)
    {
        var registro = await DatabaseService.Instance.GetRegistroByIdAsync(registroId);
        if (registro != null && registro.Estado != EstadoRegistro.Cerrado)
        {
            // 1. Pausar registro activo actual (si existe)
            if (RegistroActivo != null && _inicioTiempo != null)
            {
                // Optimización: No recargar todo, solo pausar
                await PausarRegistroAsync(recargar: false);
            }

            // 2. SEGURIDAD ADICIONAL (Optimizado): Buscar otros "EnProceso" y pausarlos en paralelo
            var otrosEnProceso = _todosRegistros.Where(r => r.Estado == EstadoRegistro.EnProceso && r.Id != registroId).ToList();
            if (otrosEnProceso.Any())
            {
                var pauseTasks = otrosEnProceso.Select(async otro =>
                {
                    otro.Estado = EstadoRegistro.Pausado;
                    // Actualizar BD
                    await DatabaseService.Instance.UpdateRegistroAsync(otro);
                    
                    // Notificar Socket (Fire and forget seguro)
                    _ = NotificationService.Instance.DesregistrarTareaActivaAsync(otro.Id);
                    
                    if (!string.IsNullOrEmpty(otro.AsignadoA))
                        _ = NotificationService.Instance.SendStatusUpdateAsync(otro.AsignadoA, otro.Id, EstadoRegistro.Pausado, otro.TiempoTranscurrido);
                    
                    if (!string.IsNullOrEmpty(otro.CreadoPor))
                        _ = NotificationService.Instance.SendStatusUpdateAsync(otro.CreadoPor, otro.Id, EstadoRegistro.Pausado, otro.TiempoTranscurrido);
                });

                await Task.WhenAll(pauseTasks);
            }

            // 3. Activar nuevo registro
            RegistroActivo = registro;
            _inicioTiempo = DateTime.Now;

            registro.Estado = EstadoRegistro.EnProceso;
            
            // Actualizar BD del nuevo registro
            await DatabaseService.Instance.UpdateRegistroAsync(registro);
            
            // 4. Actualizar UI Local (Sin CargarRegistrosAsync)
            var regEnLista = _todosRegistros.FirstOrDefault(r => r.Id == registroId);
            if (regEnLista != null)
            {
                regEnLista.Estado = EstadoRegistro.EnProceso;
                RegistroActivo = regEnLista; // Enlazar al de la lista
            }
            
            ActualizarContadores();
            FiltrarPorEstado(EstadoFiltroActual);

            // 5. Iniciar Timer
            _timer?.Stop();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            
            var registroLocal = RegistroActivo;
            var tiempoBase = registroLocal.TiempoTranscurrido;
            
            _timer.Tick += (s, e) =>
            {
                if (RegistroActivo != null && _inicioTiempo != null && RegistroActivo.Id == registroId)
                {
                    var tiempoTotal = tiempoBase + (int)(DateTime.Now - _inicioTiempo.Value).TotalSeconds;
                    
                    // Actualizar propiedad observable
                    RegistroActivo.TiempoTranscurrido = tiempoTotal;
                    
                    // Sincronizar si la referencia es distinta (ej. filtrado)
                    if (regEnLista != null && regEnLista != RegistroActivo)
                    {
                        regEnLista.TiempoTranscurrido = tiempoTotal;
                    }
                }
            };
            _timer.Start();

            // 6. Notificaciones (Fire and forget)
            NotificationService.Instance.ShowRegistroStartedNotification(registro.Titulo);
            
            _ = NotificationService.Instance.RegistrarTareaActivaAsync(registro.Id);

            if (!string.IsNullOrEmpty(registro.AsignadoA))
                _ = NotificationService.Instance.SendStatusUpdateAsync(registro.AsignadoA, registro.Id, EstadoRegistro.EnProceso, registro.TiempoTranscurrido);
            
            if (!string.IsNullOrEmpty(registro.CreadoPor))
                _ = NotificationService.Instance.SendStatusUpdateAsync(registro.CreadoPor, registro.Id, EstadoRegistro.EnProceso, registro.TiempoTranscurrido);
        }
    }

    // Sobrecarga para soportar optimización
    public async Task PausarRegistroAsync() => await PausarRegistroAsync(true);

    public async Task PausarRegistroAsync(bool recargar)
    {
        if (RegistroActivo != null && _inicioTiempo != null)
        {
            var tiempoAdicional = (DateTime.Now - _inicioTiempo.Value).TotalSeconds;
            var nuevoTiempo = RegistroActivo.TiempoTranscurrido + (int)tiempoAdicional;

            var titulo = RegistroActivo.Titulo;
            var regId = RegistroActivo.Id;
            var asignadoA = RegistroActivo.AsignadoA;
            var creadoPor = RegistroActivo.CreadoPor;

            // Actualizar objeto local
            RegistroActivo.TiempoTranscurrido = nuevoTiempo;
            RegistroActivo.Estado = EstadoRegistro.Pausado;
            
            // Actualizar en lista principal también
            var regEnLista = _todosRegistros.FirstOrDefault(r => r.Id == regId);
            if (regEnLista != null)
            {
                regEnLista.TiempoTranscurrido = nuevoTiempo;
                regEnLista.Estado = EstadoRegistro.Pausado;
            }

            // Actualizar BD
            await DatabaseService.Instance.UpdateRegistroAsync(RegistroActivo);

            // Notificaciones
            NotificationService.Instance.ShowRegistroPausedNotification(titulo);
            _ = NotificationService.Instance.DesregistrarTareaActivaAsync(regId);
            
            if (!string.IsNullOrEmpty(asignadoA))
                _ = NotificationService.Instance.SendStatusUpdateAsync(asignadoA, regId, EstadoRegistro.Pausado, nuevoTiempo);
            
            if (!string.IsNullOrEmpty(creadoPor))
                _ = NotificationService.Instance.SendStatusUpdateAsync(creadoPor, regId, EstadoRegistro.Pausado, nuevoTiempo);

            // Limpieza local
            _timer?.Stop();
            _timer = null;
            _inicioTiempo = null;
            RegistroActivo = null;

            if (recargar)
            {
                ActualizarContadores();
                FiltrarPorEstado(EstadoFiltroActual);
            }
        }
    }

    public async Task CerrarRegistroAsync(string registroId)
    {
        var registro = await DatabaseService.Instance.GetRegistroByIdAsync(registroId);
        if (registro != null)
        {
            int tiempoFinal = registro.TiempoTranscurrido;
            var titulo = registro.Titulo;

            if (RegistroActivo?.Id == registroId && _inicioTiempo != null)
            {
                tiempoFinal += (int)(DateTime.Now - _inicioTiempo.Value).TotalSeconds;
                _timer?.Stop();
                _timer = null;
                _inicioTiempo = null;
                RegistroActivo = null;
            }

            registro.Estado = EstadoRegistro.Cerrado;
            registro.FechaCierre = DateTime.Now;
            registro.TiempoTranscurrido = tiempoFinal;

            await DatabaseService.Instance.UpdateRegistroAsync(registro);
            await CargarRegistrosAsync();
            
            // Mostrar notificación de cierre
            NotificationService.Instance.ShowRegistroClosedNotification(titulo);

            if (!string.IsNullOrEmpty(registro.AsignadoA))
                await NotificationService.Instance.SendStatusUpdateAsync(registro.AsignadoA, registro.Id, EstadoRegistro.Cerrado, registro.TiempoTranscurrido);
            
            if (!string.IsNullOrEmpty(registro.CreadoPor))
                await NotificationService.Instance.SendStatusUpdateAsync(registro.CreadoPor, registro.Id, EstadoRegistro.Cerrado, registro.TiempoTranscurrido);

            // Desregistrar tarea activa del SocketServer (por si acaso, aunque al cerrarlo ya no estaría "En Proceso" y el server no actualizaría, pero es buena práctica limpiar)
            await NotificationService.Instance.DesregistrarTareaActivaAsync(registro.Id);
        }
    }

    public async Task ReenviarNotificacionAsync(Registro registro)
    {
        if (string.IsNullOrEmpty(registro.AsignadoA))
            return;

        NotificationService.Instance.ShowAssignmentNotification(
            registro.Id,
            registro.TipoTexto,
            registro.Titulo,
            registro.Prioridad,
            registro.Estado,
            registro.Proyecto,
            registro.Empresa,
            registro.CodigoFull,
            "Recordatorio de Asignación"
        );

        await NotificationService.Instance.SendAssignmentAsync(
            registro.AsignadoA,
            registro.Id,
            registro.TipoTexto,
            registro.Titulo,
            registro.Prioridad,
            registro.Estado,
            registro.Proyecto,
            registro.Empresa,
            registro.CodigoFull,
            "Recordatorio de Asignación"
        );

        NotificationService.Instance.ShowNotification(
            "Notificación reenviada",
            $"Se ha reenviado la notificación de asignación a {registro.NombreAsignado ?? registro.AsignadoA}",
            "Success"
        );
    }
}

