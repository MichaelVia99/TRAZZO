using BitacoraApi.Models;
using BitacoraApi.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Linq;

namespace BitacoraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrosController : ControllerBase
    {
        private readonly IBitacoraRepository _repository;
        private readonly IWebHostEnvironment _environment;

        public RegistrosController(IBitacoraRepository repository, IWebHostEnvironment environment)
        {
            _repository = repository;
            _environment = environment;
        }

        [HttpGet]
        public async Task<ActionResult<List<RegistroDto>>> Get([FromQuery] string? creadoPor, [FromQuery] string? asignadoA)
        {
            var registros = await _repository.GetRegistrosAsync(creadoPor, asignadoA);
            return Ok(registros);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RegistroDto>> GetById(string id)
        {
            var registro = await _repository.GetRegistroByIdAsync(id);
            if (registro == null)
            {
                return NotFound();
            }

            MapLocalPathsToPublicUrls(registro);

            return Ok(registro);
        }

        [HttpPost]
        public async Task<ActionResult<RegistroDto>> Create([FromBody] RegistroDto registro)
        {
            var id = await _repository.InsertRegistroAsync(registro);
            var creado = await _repository.GetRegistroByIdAsync(id);
            if (creado != null)
            {
                MapLocalPathsToPublicUrls(creado);
            }

            return CreatedAtAction(nameof(GetById), new { id }, creado);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] RegistroDto registro)
        {
            if (!long.TryParse(id, out var bitacoraId))
            {
                return BadRequest("Id inválido");
            }

            try
            {
                registro.Id = id;
                var archivosAEliminar = new List<(string RutaArchivo, string RutaMiniatura)>();

                if (registro.AdjuntosEliminados != null && registro.AdjuntosEliminados.Count > 0)
                {
                    var eliminadosGenerales = await _repository.SoftDeleteEvidenciasGeneralesAsync(bitacoraId, registro.AdjuntosEliminados);
                    if (eliminadosGenerales != null && eliminadosGenerales.Count > 0)
                    {
                        archivosAEliminar.AddRange(eliminadosGenerales);
                    }
                }

                if (registro.AdjuntosTareasEliminados != null && registro.AdjuntosTareasEliminados.Count > 0)
                {
                    var eliminadosTareas = await _repository.SoftDeleteEvidenciasTareasAsync(bitacoraId, registro.AdjuntosTareasEliminados);
                    if (eliminadosTareas != null && eliminadosTareas.Count > 0)
                    {
                        archivosAEliminar.AddRange(eliminadosTareas);
                    }
                }

                await _repository.UpdateRegistroAsync(registro);

                foreach (var item in archivosAEliminar)
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(item.RutaArchivo) && System.IO.File.Exists(item.RutaArchivo))
                        {
                            System.IO.File.Delete(item.RutaArchivo);
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        if (!string.IsNullOrWhiteSpace(item.RutaMiniatura) && System.IO.File.Exists(item.RutaMiniatura))
                        {
                            System.IO.File.Delete(item.RutaMiniatura);
                        }
                    }
                    catch
                    {
                    }
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        private void MapLocalPathsToPublicUrls(RegistroDto registro)
        {
            if (registro == null)
                return;

            var evidenciasRoot = Path.Combine(_environment.ContentRootPath, "Evidencias");
            evidenciasRoot = Path.GetFullPath(evidenciasRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            string ToPublicSingle(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return path;

                if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    return path;
                }

                if (!Path.IsPathRooted(path))
                {
                    return $"{Request.Scheme}://{Request.Host}{Request.PathBase}{path}";
                }

                var fullPath = Path.GetFullPath(path);
                if (!fullPath.StartsWith(evidenciasRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }

                var relative = fullPath.Substring(evidenciasRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var urlPath = "/evidencias/" + relative.Replace(Path.DirectorySeparatorChar, '/');
                return $"{Request.Scheme}://{Request.Host}{Request.PathBase}{urlPath}";
            }

            string ToPublicUrl(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return value;

                if (!value.Contains('|'))
                {
                    return ToPublicSingle(value);
                }

                var parts = value.Split('|', StringSplitOptions.None);

                // Formatos posibles:
                // 1) mini|full|size|name
                // 2) full|size|name
                // 3) mini|full|size
                // 4) full|size
                // 5) mini|full
                //
                // Regla general:
                //  - Solo los primeros dos elementos pueden ser rutas de archivo.
                //  - size y name NO son rutas y no deben pasar por ToPublicSingle.

                if (parts.Length >= 4)
                {
                    var p1 = ToPublicSingle(parts[0]);
                    var p2 = ToPublicSingle(parts[1]);
                    var rest = parts.Skip(2); // size, name y cualquier extra
                    return string.Join("|", new[] { p1, p2 }.Concat(rest));
                }

                if (parts.Length == 3)
                {
                    bool isSecondSize = int.TryParse(parts[1], out _);

                    if (isSecondSize)
                    {
                        // full|size|name
                        var p1 = ToPublicSingle(parts[0]);
                        return string.Join("|", p1, parts[1], parts[2]);
                    }
                    else
                    {
                        // mini|full|size
                        var p1 = ToPublicSingle(parts[0]);
                        var p2 = ToPublicSingle(parts[1]);
                        return string.Join("|", p1, p2, parts[2]);
                    }
                }

                if (parts.Length == 2)
                {
                    bool isSecondSize = int.TryParse(parts[1], out _);

                    if (isSecondSize)
                    {
                        // full|size
                        var p1 = ToPublicSingle(parts[0]);
                        return string.Join("|", p1, parts[1]);
                    }
                    else
                    {
                        // mini|full
                        var p1 = ToPublicSingle(parts[0]);
                        var p2 = ToPublicSingle(parts[1]);
                        return string.Join("|", p1, p2);
                    }
                }

                return ToPublicSingle(parts[0]);
            }

            if (!string.IsNullOrWhiteSpace(registro.Adjuntos))
            {
                var locals = registro.Adjuntos.Split(';', StringSplitOptions.RemoveEmptyEntries);
                var urls = locals.Select(ToPublicUrl);
                registro.Adjuntos = string.Join(";", urls);
            }

            if (registro.SubRegistros != null && registro.SubRegistros.Count > 0)
            {
                foreach (var sub in registro.SubRegistros)
                {
                    MapLocalPathsToPublicUrls(sub);
                }
            }
        }
    }
}
