using BitacoraApi.Models;

namespace BitacoraApi.Repositories
{
    public interface IBitacoraRepository
    {
        Task<UsuarioDto?> LoginAsync(string email, string password);
        Task<UsuarioDto?> GetUsuarioByEmailAsync(string email);
        Task<UsuarioDto?> GetUsuarioByIdAsync(string id);
        Task<List<UsuarioDto>> GetDesarrolladoresAsync();
        Task<List<UsuarioDto>> GetUsuariosActivosAsync();
        Task<List<string>> GetProyectosActivosAsync();
        Task<List<string>> GetEmpresasActivasAsync();
        Task<List<KeyValuePair<int, string>>> GetCanalesActivosAsync();
        Task<List<KeyValuePair<string, string>>> GetSociosActivosAsync();
        Task<List<RegistroDto>> GetRegistrosAsync(string? creadoPor, string? asignadoA);
        Task<RegistroDto?> GetRegistroByIdAsync(string id);
        Task<string> InsertRegistroAsync(RegistroDto registro);
        Task UpdateRegistroAsync(RegistroDto registro);
        Task AddEvidenciaAsync(long bitacoraId, long? tareaId, string rutaLocal, string rutaMiniaturaLocal, string nombreArchivo, int pesoKb);
        Task<List<(string RutaArchivo, string RutaMiniatura)>> SoftDeleteEvidenciasGeneralesAsync(long bitacoraId, IEnumerable<string> nombresArchivos);
        Task<List<(string RutaArchivo, string RutaMiniatura)>> SoftDeleteEvidenciasTareasAsync(long bitacoraId, IEnumerable<string> nombresArchivos);
    }
}
