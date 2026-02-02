using BitacoraApi.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace BitacoraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SociosController : ControllerBase
    {
        private readonly IBitacoraRepository _repository;

        public SociosController(IBitacoraRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("activos")]
        public async Task<ActionResult<List<KeyValuePair<string, string>>>> GetSociosActivos()
        {
            var result = await _repository.GetSociosActivosAsync();
            return Ok(result);
        }
    }
}
