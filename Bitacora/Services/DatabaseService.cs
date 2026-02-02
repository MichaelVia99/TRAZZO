using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Bitacora.Dtos;
using Bitacora.Models;
using Microsoft.Extensions.Configuration;

namespace Bitacora.Services;

public class DatabaseService
{
    private static DatabaseService? _instance;
    private readonly HttpClient _httpClient;

    private DatabaseService()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        var configuration = builder.Build();

        var baseUrl = configuration["ApiSettings:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("La URL de la API (ApiSettings:BaseUrl) no está configurada en appsettings.json.");
        }
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public static DatabaseService Instance => _instance ??= new DatabaseService();

    public async Task InitializeDatabaseAsync()
    {
        try
        {
            await _httpClient.GetAsync("weatherforecast");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error conectando a API: {ex.Message}");
        }
    }

    public async Task<Usuario?> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/login", new LoginRequest { Email = email, Password = password });
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<UsuarioDto>();
                return MapToUsuario(dto);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Login error: {ex.Message}");
        }
        return null;
    }

    public async Task<Usuario?> GetUsuarioByEmailAsync(string email)
    {
        // Deprecated for login. Use LoginAsync instead.
        // Return null to force AuthViewModel update.
        return null;
    }

    public async Task<List<Usuario>> GetDesarrolladoresAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("usuarios/desarrolladores");
            if (response.IsSuccessStatusCode)
            {
                var dtos = await response.Content.ReadFromJsonAsync<List<UsuarioDto>>();
                return dtos?.Select(MapToUsuario).Where(u => u != null).Cast<Usuario>().ToList() ?? new List<Usuario>();
            }
        }
        catch { }
        return new List<Usuario>();
    }

    public async Task<List<Usuario>> GetUsuariosActivosAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("usuarios/activos");
            if (response.IsSuccessStatusCode)
            {
                var dtos = await response.Content.ReadFromJsonAsync<List<UsuarioDto>>();
                return dtos?.Select(MapToUsuario).Where(u => u != null).Cast<Usuario>().ToList() ?? new List<Usuario>();
            }
        }
        catch { }
        return new List<Usuario>();
    }

    public async Task<List<string>> GetProyectosActivosAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("proyectos/activos");
            if (response.IsSuccessStatusCode)
            {
                var nombres = await response.Content.ReadFromJsonAsync<List<string>>();
                return nombres ?? new List<string>();
            }
        }
        catch { }
        return new List<string>();
    }

    public async Task<List<string>> GetEmpresasActivasAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("empresas/activas");
            if (response.IsSuccessStatusCode)
            {
                var nombres = await response.Content.ReadFromJsonAsync<List<string>>();
                return nombres ?? new List<string>();
            }
        }
        catch { }
        return new List<string>();
    }

    public async Task<List<string>> GetCanalesActivosAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("canales/activos");
            if (response.IsSuccessStatusCode)
            {
                var nombres = await response.Content.ReadFromJsonAsync<List<string>>();
                return nombres ?? new List<string>();
            }
        }
        catch { }
        return new List<string>();
    }

    public async Task<List<string>> GetSociosActivosAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("socios/activos");
            if (response.IsSuccessStatusCode)
            {
                var nombres = await response.Content.ReadFromJsonAsync<List<string>>();
                return nombres ?? new List<string>();
            }
        }
        catch { }
        return new List<string>();
    }

    public async Task<Usuario?> GetUsuarioByIdAsync(string id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"usuarios/{id}");
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<UsuarioDto>();
                return MapToUsuario(dto);
            }
        }
        catch { }
        return null;
    }

    public async Task<string> InsertRegistroAsync(Registro registro)
    {
        var dto = MapToRegistroDto(registro);
        var response = await _httpClient.PostAsJsonAsync("registros", dto);
        if (response.IsSuccessStatusCode)
        {
            var createdDto = await response.Content.ReadFromJsonAsync<RegistroDto>();
            if (createdDto == null)
            {
                return "";
            }

            var registroServidor = MapToRegistro(createdDto);

            await UploadEvidenciasBitacoraAsync(registroServidor.Id, registro.Adjuntos);
            await UploadEvidenciasTareasAsync(registroServidor.Id, registro.Tareas, registroServidor.Tareas);

            return registroServidor.Id;
        }

        var content = await response.Content.ReadAsStringAsync();
        throw new Exception($"Error al crear registro ({response.StatusCode}): {content}");
    }

    public async Task<List<Registro>> GetAllRegistrosAsync()
    {
        return await GetRegistrosInternal(null, null);
    }

    public async Task<List<Registro>> GetRegistrosByCreadorAsync(string creadoPor)
    {
        return await GetRegistrosInternal(creadoPor, null);
    }

    public async Task<List<Registro>> GetRegistrosByAsignadoAsync(string asignadoA)
    {
        return await GetRegistrosInternal(null, asignadoA);
    }

    private async Task<List<Registro>> GetRegistrosInternal(string? creadoPor, string? asignadoA)
    {
        try
        {
            var url = "registros?";
            if (!string.IsNullOrEmpty(creadoPor)) url += $"creadoPor={creadoPor}&";
            if (!string.IsNullOrEmpty(asignadoA)) url += $"asignadoA={asignadoA}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var dtos = await response.Content.ReadFromJsonAsync<List<RegistroDto>>();
                return dtos?.Select(MapToRegistro).ToList() ?? new List<Registro>();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting registros: {ex.Message}");
        }
        return new List<Registro>();
    }

    public async Task<Registro?> GetRegistroByIdAsync(string id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"registros/{id}");
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<RegistroDto>();
                if (dto != null)
                {
                    return MapToRegistro(dto);
                }
            }
        }
        catch { }
        return null;
    }

    public async Task UpdateRegistroAsync(Registro registro)
    {
        var dto = MapToRegistroDto(registro);
        var response = await _httpClient.PutAsJsonAsync($"registros/{registro.Id}", dto);
        
        if (!response.IsSuccessStatusCode)
        {
            var contentError = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error al actualizar registro ({response.StatusCode}): {contentError}");
        }

        var recargado = await GetRegistroByIdAsync(registro.Id);
        if (recargado != null)
        {
            await UploadEvidenciasBitacoraAsync(recargado.Id, registro.Adjuntos);
            await UploadEvidenciasTareasAsync(recargado.Id, registro.Tareas, recargado.Tareas);
        }
    }

    private Usuario? MapToUsuario(UsuarioDto? dto)
    {
        if (dto == null) return null;
        return new Usuario
        {
            Id = dto.Id,
            Nombre = dto.Nombre,
            Email = dto.Email,
            RolId = dto.RolId,
            Rol = dto.Rol
        };
    }

        private Registro MapToRegistro(RegistroDto dto)
        {
            if (!Enum.TryParse<TipoRegistro>(dto.Tipo, true, out var tipo))
                tipo = TipoRegistro.Requerimiento;

            // Mapeo manual de estados si es necesario, o TryParse
            EstadoRegistro estado;
            if (!Enum.TryParse(dto.Estado.Replace(" ", ""), true, out estado))
            {
                 // Fallback para mapeo de espacios
                 if (dto.Estado.Equals("En Proceso", StringComparison.OrdinalIgnoreCase)) estado = EstadoRegistro.EnProceso;
                 else estado = EstadoRegistro.Pendiente;
            }

            return new Registro
            {
                Id = dto.Id,
                Numero = dto.Numero,
                Tipo = tipo,
                Titulo = dto.Titulo,
                Descripcion = dto.Descripcion,
                CreadoPor = dto.CreadoPor,
                AsignadoA = dto.AsignadoA,
                FechaCreacion = dto.FechaCreacion,
                FechaAsignacion = dto.FechaAsignacion,
                FechaCierre = dto.FechaCierre,
                Estado = estado,
                TiempoTranscurrido = dto.TiempoTranscurrido,
                TiempoEstimado = dto.TiempoEstimado,
                Proyecto = dto.Proyecto,
                Empresa = dto.Empresa,
                Prioridad = dto.Prioridad,
                Adjuntos = dto.Adjuntos,
                NombreAsignado = dto.NombreAsignado,
                TaskCountFromDb = dto.TaskCountFromDb,
                TotalPesoAdjuntosKb = dto.TotalPesoAdjuntosKb,
                AdjuntosEliminados = dto.AdjuntosEliminados ?? new List<string>(),
                AdjuntosTareasEliminados = dto.AdjuntosTareasEliminados ?? new List<string>(),
                Tareas = MapToTareaList(dto.SubRegistros)
            };
        }

        private List<TareaRegistro> MapToTareaList(List<RegistroDto>? dtos)
        {
            if (dtos == null) return new List<TareaRegistro>();

            return dtos.Select(t =>
            {
                var tarea = new TareaRegistro
                {
                    Id = t.Id,
                    Descripcion = t.Titulo,
                    Completada = t.Estado == "Cerrado" || t.Estado == "Finalizado",
                    ArchivosAdjuntos = t.Adjuntos,
                    AdjuntosEliminados = t.AdjuntosEliminados ?? new List<string>(),
                    TiempoEstimado = t.TiempoEstimado,
                    TiempoTranscurrido = t.TiempoTranscurrido,
                    FechaInicio = t.FechaInicio,
                    FechaFin = t.FechaFin
                };

                if (t.SubRegistros != null && t.SubRegistros.Any())
                {
                    foreach (var sub in MapToTareaList(t.SubRegistros))
                    {
                        sub.IsSubtask = true;
                        tarea.Subtareas.Add(sub);
                    }
                }

                return tarea;
            }).ToList();
        }

        private RegistroDto MapToRegistroDto(Registro modelo)
        {
            return new RegistroDto
            {
                Id = modelo.Id,
                Numero = modelo.Numero,
                Tipo = modelo.Tipo.ToString(),
                Titulo = modelo.Titulo,
                Descripcion = modelo.Descripcion,
                CreadoPor = modelo.CreadoPor,
                AsignadoA = modelo.AsignadoA,
                FechaCreacion = modelo.FechaCreacion,
                FechaAsignacion = modelo.FechaAsignacion,
                FechaCierre = modelo.FechaCierre,
                Estado = modelo.Estado.ToString(),
                TiempoTranscurrido = modelo.TiempoTranscurrido,
                TiempoEstimado = modelo.TiempoEstimado,
                Proyecto = modelo.Proyecto,
                Empresa = modelo.Empresa,
                Contacto = modelo.Contacto,
                Telefono = modelo.Telefono,
                Prioridad = modelo.Prioridad,
                Adjuntos = modelo.Adjuntos,
                TotalPesoAdjuntosKb = modelo.TotalPesoAdjuntosKb,
                AdjuntosEliminados = modelo.AdjuntosEliminados,
                AdjuntosTareasEliminados = modelo.AdjuntosTareasEliminados,
                SubRegistros = MapSubtasksToRegistroDtoList(modelo.Tareas, modelo)
            };
        }

        private List<RegistroDto> MapSubtasksToRegistroDtoList(IEnumerable<TareaRegistro> tareas, Registro parentModel)
        {
            if (tareas == null) return new List<RegistroDto>();

            return tareas.Select(t => new RegistroDto
            {
                Id = t.Id,
                Titulo = t.Descripcion,
                Estado = t.Completada ? "Cerrado" : "Pendiente",
                // Defaults for required fields
                Tipo = parentModel.Tipo.ToString(),
                Descripcion = "",
                CreadoPor = parentModel.CreadoPor,
                AsignadoA = parentModel.AsignadoA,
                TiempoEstimado = t.TiempoEstimado,
                TiempoTranscurrido = t.TiempoTranscurrido,
                Adjuntos = t.ArchivosAdjuntos,
                AdjuntosEliminados = t.AdjuntosEliminados,
                FechaInicio = t.FechaInicio,
                FechaFin = t.FechaFin,
                SubRegistros = MapSubtasksToRegistroDtoList(t.Subtareas, parentModel)
            }).ToList();
        }

    private async Task UploadEvidenciasBitacoraAsync(string bitacoraId, string? adjuntos)
    {
        if (string.IsNullOrWhiteSpace(bitacoraId))
            return;

        if (string.IsNullOrWhiteSpace(adjuntos))
            return;

        var archivos = adjuntos.Split(';', StringSplitOptions.RemoveEmptyEntries);
        if (archivos.Length == 0)
            return;

        using var form = new MultipartFormDataContent();
        var hasFile = false;

        foreach (var ruta in archivos)
        {
            if (string.IsNullOrWhiteSpace(ruta))
                continue;

            if (Uri.TryCreate(ruta, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                continue;
            }

            if (!System.IO.File.Exists(ruta))
                continue;

            var fileName = System.IO.Path.GetFileName(ruta);
            var fileStream = System.IO.File.OpenRead(ruta);
            var fileContent = new StreamContent(fileStream);
            form.Add(fileContent, "files", fileName);
            hasFile = true;
        }

        if (!hasFile)
            return;

        var response = await _httpClient.PostAsync($"evidencias/bitacora/{bitacoraId}", form);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception($"Error al subir evidencias de bitácora ({response.StatusCode}): {content}");
        }
    }

    private async Task UploadEvidenciasTareasAsync(string bitacoraId, List<TareaRegistro> tareasCliente, List<TareaRegistro> tareasServidor)
    {
        if (string.IsNullOrWhiteSpace(bitacoraId))
            return;

        if (tareasCliente == null || tareasCliente.Count == 0)
            return;

        if (tareasServidor == null || tareasServidor.Count == 0)
            return;

        var total = Math.Min(tareasCliente.Count, tareasServidor.Count);

        for (int i = 0; i < total; i++)
        {
            var tareaCliente = tareasCliente[i];
            var tareaServidor = tareasServidor[i];

            // Recursión para subtareas
            if (tareaCliente.Subtareas.Count > 0 && tareaServidor.Subtareas.Count > 0)
            {
                await UploadEvidenciasTareasAsync(bitacoraId, tareaCliente.Subtareas.ToList(), tareaServidor.Subtareas.ToList());
            }

            if (string.IsNullOrWhiteSpace(tareaServidor.Id))
                continue;

            var adjuntos = tareaCliente.ArchivosAdjuntos;
            if (string.IsNullOrWhiteSpace(adjuntos))
                continue;

            var archivos = adjuntos.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (archivos.Length == 0)
                continue;

            using var form = new MultipartFormDataContent();
            var hasFile = false;

            foreach (var ruta in archivos)
            {
                if (string.IsNullOrWhiteSpace(ruta))
                    continue;

                if (Uri.TryCreate(ruta, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    continue;
                }

                if (!System.IO.File.Exists(ruta))
                    continue;

                var fileName = System.IO.Path.GetFileName(ruta);
                var fileStream = System.IO.File.OpenRead(ruta);
                var fileContent = new StreamContent(fileStream);
                form.Add(fileContent, "files", fileName);
                hasFile = true;
            }

            if (!hasFile)
                continue;

            var response = await _httpClient.PostAsync($"evidencias/bitacora/{bitacoraId}/tarea/{tareaServidor.Id}", form);
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error al subir evidencias de tarea ({response.StatusCode}): {content}");
            }
        }
    }
}
