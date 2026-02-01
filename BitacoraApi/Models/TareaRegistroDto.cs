using System.Collections.Generic;

namespace BitacoraApi.Models
{
    public class TareaRegistroDto
    {
        public string Id { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public bool Completada { get; set; }
        public string? ArchivosAdjuntos { get; set; }
        public bool HasError { get; set; }
        public List<string> ArchivosList { get; set; } = new List<string>();
        public List<string>? AdjuntosEliminados { get; set; }
    }
}
