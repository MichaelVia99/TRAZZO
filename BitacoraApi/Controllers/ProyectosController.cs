using BitacoraApi.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace BitacoraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProyectosController : ControllerBase
    {
        private readonly IBitacoraRepository _repository;

        public ProyectosController(IBitacoraRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("activos")]
        public async Task<ActionResult<List<string>>> GetProyectosActivos()
        {
            var result = await _repository.GetProyectosActivosAsync();
            return Ok(result);
        }
    }
}
