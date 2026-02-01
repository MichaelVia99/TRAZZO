namespace Bitacora.Models;

public class Usuario
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public long RolId { get; set; } // 1 = Gestor, 2 = Dev
    public string Rol { get; set; } = string.Empty;
    public string? Password { get; set; }
}

