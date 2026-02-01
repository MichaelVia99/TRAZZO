using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Text;
using Bitacora.Models;

namespace Bitacora.Services;

public record AssignmentAlarmData(string RegistroId, string Tipo, string Titulo, string? Prioridad, EstadoRegistro Estado, string? Proyecto, string? Empresa, string Codigo, string HeaderTitle = "¡Nueva Asignación!");
public record StatusUpdateData(string RegistroId, EstadoRegistro Estado, int TiempoTranscurrido, string? FromUserId);

public class NotificationService
{
    private static NotificationService? _instance;
    private static readonly object _lock = new object();
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoopTask;
    private string? _currentUserId;
    private string? _currentActiveRegistroId;
    private readonly Uri _serverUri;

    // Constructor privado: configura la URL del servidor de sockets
    // Por defecto usa ws://localhost:4000, pero se puede ajustar leyendo configuración si es necesario.
    private NotificationService()
    {
        _serverUri = new Uri(Environment.GetEnvironmentVariable("BITACORA_SOCKET_URL") ?? "ws://localhost:4000");
    }

    public static NotificationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new NotificationService();
                    }
                }
            }
            return _instance;
        }
    }

    // Inicia la conexión WebSocket para recibir notificaciones en tiempo real
    public void StartPolling(string userId)
    {
        _currentUserId = userId;
        _ = ConnectAndListenAsync();
    }

    // Detiene la conexión WebSocket y libera recursos
    public void StopPolling()
    {
        _currentUserId = null;

        DisconnectWebSocket();
    }

    private void DisconnectWebSocket()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
        }

        _cts = null;

        if (_webSocket != null)
        {
            if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
            {
                try
                {
                    _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Logout", CancellationToken.None).Wait(1000);
                }
                catch
                {
                }
            }

            _webSocket.Dispose();
            _webSocket = null;
        }
    }

    // Establece la conexión y lanza el bucle de recepción de mensajes
    private async Task ConnectAndListenAsync()
    {
        if (string.IsNullOrEmpty(_currentUserId))
            return;

        try
        {
            // Cancelar cualquier conexión previa sin limpiar el userId actual
            DisconnectWebSocket();

            _cts = new CancellationTokenSource();
            _webSocket = new ClientWebSocket();

            // Conectarse al servidor de sockets
            await _webSocket.ConnectAsync(_serverUri, _cts.Token);

            // Enviar mensaje de registro con el userId actual
            var registerPayload = $"{{\"type\":\"register\",\"userId\":\"{_currentUserId}\"}}";
            var registerBytes = Encoding.UTF8.GetBytes(registerPayload);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(registerBytes),
                WebSocketMessageType.Text,
                true,
                _cts.Token
            );

            // Re-registrar tarea activa si existe (para recuperar estado tras reconexión)
            if (!string.IsNullOrEmpty(_currentActiveRegistroId))
            {
                var activePayload = new
                {
                    type = "register_task",
                    registroId = _currentActiveRegistroId,
                    userId = _currentUserId
                };
                var activeJson = System.Text.Json.JsonSerializer.Serialize(activePayload);
                var activeBytes = Encoding.UTF8.GetBytes(activeJson);
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(activeBytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token
                );
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Tarea activa re-registrada tras conexión: {_currentActiveRegistroId}");
            }

            // Lanzar bucle de recepción en segundo plano
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_cts!.Token));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error conectando al servidor de sockets: {ex.Message}");
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_webSocket == null)
            return;

        var buffer = new byte[4096];

        while (!cancellationToken.IsCancellationRequested &&
               _webSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken
                );

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server", cancellationToken);
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                    continue;

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                HandleIncomingMessage(message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ReceiveLoop de sockets: {ex.Message}");
                try
                {
                    await Task.Delay(5000, cancellationToken);
                }
                catch
                {
                }
            }
        }
    }

    private void HandleIncomingMessage(string json)
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var message = System.Text.Json.JsonSerializer.Deserialize<SocketMessage>(json, options);
            if (message == null || string.IsNullOrEmpty(message.Type))
                return;

            if (message.Type == "assignment" &&
                !string.IsNullOrEmpty(message.RegistroId) &&
                !string.IsNullOrEmpty(message.Titulo))
            {
                var estado = ParseEstado(message.Estado);

                ShowAssignmentNotification(
                    message.RegistroId,
                    message.Tipo ?? string.Empty,
                    message.Titulo,
                    message.Prioridad,
                    estado,
                    message.Proyecto,
                    message.Empresa,
                    message.Codigo ?? "0000",
                    message.HeaderTitle ?? "¡Nueva Asignación!"
                );
            }
            else if (message.Type == "status_update" &&
                     !string.IsNullOrEmpty(message.RegistroId))
            {
                var estado = ParseEstado(message.Estado);
                OnStatusUpdate?.Invoke(new StatusUpdateData(
                    message.RegistroId,
                    estado,
                    message.TiempoTranscurrido,
                    message.FromUserId
                ));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error procesando mensaje de sockets: {ex.Message}");
        }
    }

    public async Task SendAssignmentAsync(string toUserId, string registroId, string tipo, string titulo, string? prioridad, EstadoRegistro estado, string? proyecto, string? empresa, string codigo, string headerTitle = "¡Nueva Asignación!")
    {
        try
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                System.Diagnostics.Debug.WriteLine("No hay conexión WebSocket activa para enviar la asignación. Intentando reconectar...");

                if (!string.IsNullOrEmpty(_currentUserId))
                {
                    try
                    {
                        await ConnectAndListenAsync();
                    }
                    catch (Exception exReconnect)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error al intentar reconectar WebSocket: {exReconnect.Message}");
                    }
                }

                if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                {
                    System.Diagnostics.Debug.WriteLine("No se pudo establecer conexión WebSocket para enviar la asignación.");
                    return;
                }
            }

            var payload = new
            {
                type = "assignment",
                toUserId,
                registroId,
                tipo,
                titulo,
                prioridad,
                estado = estado.ToString(),
                proyecto,
                empresa,
                codigo,
                headerTitle
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                _cts?.Token ?? CancellationToken.None
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error enviando asignación por sockets: {ex.Message}");
        }
    }

    public async Task SendStatusUpdateAsync(string toUserId, string registroId, EstadoRegistro estado, int tiempoTranscurrido)
    {
        try
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                 // Intentar reconectar si es necesario, similar a SendAssignmentAsync
                 if (!string.IsNullOrEmpty(_currentUserId))
                 {
                     try { await ConnectAndListenAsync(); } catch { }
                 }
            }

            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                var payload = new
                {
                    type = "status_update",
                    toUserId,
                    registroId,
                    estado = estado.ToString(),
                    tiempoTranscurrido
                };

                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts?.Token ?? CancellationToken.None
                );
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error enviando actualización de estado: {ex.Message}");
        }
    }

    public async Task RegistrarTareaActivaAsync(string registroId)
    {
        _currentActiveRegistroId = registroId;
        try
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                 // Intentar reconectar si es necesario
                 if (!string.IsNullOrEmpty(_currentUserId))
                 {
                     System.Diagnostics.Debug.WriteLine("[NotificationService] Socket desconectado al registrar tarea. Reconectando...");
                     try { await ConnectAndListenAsync(); } catch { }
                 }
            }

            // Optimización: Menos reintentos y más rápidos (3 x 300ms = ~1s max)
            // Si no conecta rápido, probablemente no conectará ahora.
            int retries = 0;
            while (retries < 3)
            {
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                    break;
                
                await Task.Delay(300);
                retries++;
            }

            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                var payload = new
                {
                    type = "register_task",
                    registroId,
                    userId = _currentUserId
                };

                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts?.Token ?? CancellationToken.None
                );
                
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Tarea activa registrada: {registroId}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[NotificationService] Fallo al registrar tarea: Socket no conectó.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error registrando tarea activa: {ex.Message}");
        }
    }

    public async Task DesregistrarTareaActivaAsync(string registroId)
    {
        if (_currentActiveRegistroId == registroId)
            _currentActiveRegistroId = null;

        try
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open)
            {
                 // Intentar reconectar si es necesario
                 if (!string.IsNullOrEmpty(_currentUserId))
                 {
                     System.Diagnostics.Debug.WriteLine("[NotificationService] Socket desconectado al desregistrar tarea. Reconectando...");
                     try { await ConnectAndListenAsync(); } catch { }
                 }
            }

            // Optimización: Si es para desregistrar, intentamos rápido.
            int retries = 0;
            while (retries < 3)
            {
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                    break;
                
                await Task.Delay(300);
                retries++;
            }

            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                var payload = new
                {
                    type = "unregister_task",
                    registroId
                };

                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var bytes = Encoding.UTF8.GetBytes(json);

                await _webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    _cts?.Token ?? CancellationToken.None
                );
                
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Tarea activa desregistrada: {registroId}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error desregistrando tarea activa: {ex.Message}");
        }
    }

    public event Action<string, string, string>? OnShowCustomToast;
    public event Action<AssignmentAlarmData>? OnShowAlarmNotification;
    public event Action<StatusUpdateData>? OnStatusUpdate;

    public void ShowNotification(string title, string body, string type = "Info", int id = 0)
    {
        try
        {
            OnShowCustomToast?.Invoke(title, body, type);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al mostrar notificación: {ex.Message}");
        }
    }

    public void ShowAssignmentNotification(string registroId, string tipo, string titulo, string? prioridad, EstadoRegistro estado, string? proyecto, string? empresa, string codigo, string headerTitle = "¡Nueva Asignación!")
    {
        var tipoDisplay = string.IsNullOrWhiteSpace(tipo) ? "Registro" : tipo.Trim();
        var prioridadDisplay = string.IsNullOrWhiteSpace(prioridad) ? "Sin prioridad" : prioridad.Trim();
        var estadoDisplay = GetEstadoTexto(estado);
        var proyectoDisplay = string.IsNullOrWhiteSpace(proyecto) ? "Sin proyecto" : proyecto.Trim();

        ShowNotification(
            $"📌 {headerTitle} · {tipoDisplay}",
            $"{titulo}\nPrioridad: {prioridadDisplay} · Estado: {estadoDisplay}\nProyecto: {proyectoDisplay}",
            "Assignment",
            registroId.GetHashCode()
        );

        try
        {
            OnShowAlarmNotification?.Invoke(new AssignmentAlarmData(
                registroId,
                tipoDisplay,
                titulo,
                prioridad,
                estado,
                proyecto,
                empresa,
                codigo,
                headerTitle
            ));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al mostrar alarma de asignación: {ex.Message}");
        }
    }

    public void ShowRegistroCreatedNotification(string tipo, string titulo)
    {
        ShowNotification(
            $"📝 Registro creado · {tipo}",
            $"Se creó: {titulo}",
            "Success",
            1000
        );
    }

    public void ShowRegistroStartedNotification(string titulo)
    {
        ShowNotification(
            "▶ Registro iniciado",
            $"Has iniciado el trabajo en: {titulo}",
            "Info",
            2000
        );
    }

    public void ShowRegistroPausedNotification(string titulo)
    {
        ShowNotification(
            "⏸ Registro pausado",
            $"Has pausado el trabajo en: {titulo}",
            "Warning",
            3000
        );
    }

    public void ShowRegistroClosedNotification(string titulo)
    {
        ShowNotification(
            "✓ Registro cerrado",
            $"Has cerrado el registro: {titulo}",
            "Success",
            4000
        );
    }

    private sealed class SocketMessage
    {
        public string? Type { get; set; }
        public string? RegistroId { get; set; }
        public string? Tipo { get; set; }
        public string? Titulo { get; set; }
        public string? Prioridad { get; set; }
        public string? Estado { get; set; }
        public string? Proyecto { get; set; }
        public string? Empresa { get; set; }
        public string? Codigo { get; set; }
        public string? HeaderTitle { get; set; }
        public string? FromUserId { get; set; }
        public int TiempoTranscurrido { get; set; }
    }

    private static EstadoRegistro ParseEstado(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return EstadoRegistro.Pendiente;

        var value = raw.Trim();

        if (Enum.TryParse<EstadoRegistro>(value.Replace(" ", string.Empty), true, out var estadoEnum))
            return estadoEnum;

        if (value.Equals("Por Planificar", StringComparison.OrdinalIgnoreCase))
            return EstadoRegistro.Pendiente;
        if (value.Equals("En Espera", StringComparison.OrdinalIgnoreCase))
            return EstadoRegistro.EnEspera;
        if (value.Equals("En Curso", StringComparison.OrdinalIgnoreCase))
            return EstadoRegistro.EnProceso;
        if (value.Equals("En Pausa", StringComparison.OrdinalIgnoreCase))
            return EstadoRegistro.Pausado;
        if (value.Equals("Completado", StringComparison.OrdinalIgnoreCase))
            return EstadoRegistro.Cerrado;

        return EstadoRegistro.Pendiente;
    }

    private static string GetEstadoTexto(EstadoRegistro estado)
    {
        return estado switch
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
