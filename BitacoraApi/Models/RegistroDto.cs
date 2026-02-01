using System;
using System.Collections.Generic;

namespace BitacoraApi.Models
{
    public class RegistroDto
    {
        public string Id { get; set; } = string.Empty;
        public int Numero { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string CreadoPor { get; set; } = string.Empty;
        public string? AsignadoA { get; set; }
        public DateTime FechaCreacion { get; set; }
        public DateTime? FechaAsignacion { get; set; }
        public DateTime? FechaCierre { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int TiempoTranscurrido { get; set; }
        public int TiempoEstimado { get; set; }
        public string? Proyecto { get; set; }
        public string? Empresa { get; set; }
        public string? Prioridad { get; set; }
        public string? Adjuntos { get; set; }
        public string? NombreAsignado { get; set; }
        
        // Nuevos campos para recursividad
        public string? ParentId { get; set; }
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public List<RegistroDto> SubRegistros { get; set; } = new List<RegistroDto>();

        // Deprecated but kept for compatibility if needed, or removed? User said "ya no va haber tabla con req pcincipal y tabla para tareas"
        // public List<TareaRegistroDto> Tareas { get; set; } = new List<TareaRegistroDto>();
        
        public int TaskCountFromDb { get; set; }
        public int TotalPesoAdjuntosKb { get; set; }
        public List<string>? AdjuntosEliminados { get; set; }
        public List<string>? AdjuntosTareasEliminados { get; set; }
    }
}
