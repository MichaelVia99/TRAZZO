using BitacoraApi.Models;
using BitacoraApi.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace BitacoraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly IBitacoraRepository _repository;

        public UsuariosController(IBitacoraRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("desarrolladores")]
        public async Task<ActionResult<List<UsuarioDto>>> GetDesarrolladores()
        {
            var result = await _repository.GetDesarrolladoresAsync();
            return Ok(result);
        }

        [HttpGet("activos")]
        public async Task<ActionResult<List<UsuarioDto>>> GetUsuariosActivos()
        {
            var result = await _repository.GetUsuariosActivosAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UsuarioDto>> GetUsuarioById(string id)
        {
            var usuario = await _repository.GetUsuarioByIdAsync(id);
            if (usuario == null)
            {
                return NotFound();
            }
            return Ok(usuario);
        }
    }
}
