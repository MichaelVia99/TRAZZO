using BitacoraApi.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace BitacoraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CanalesController : ControllerBase
    {
        private readonly IBitacoraRepository _repository;

        public CanalesController(IBitacoraRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("activos")]
        public async Task<ActionResult<List<KeyValuePair<int, string>>>> GetCanalesActivos()
        {
            var result = await _repository.GetCanalesActivosAsync();
            return Ok(result);
        }
    }
}