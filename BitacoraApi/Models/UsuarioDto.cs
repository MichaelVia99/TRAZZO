namespace BitacoraApi.Models
{
    public class UsuarioDto
    {
        public string Id { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public long RolId { get; set; }
        public string Rol { get; set; } = string.Empty;
    }
}
