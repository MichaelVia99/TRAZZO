using BitacoraApi.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace BitacoraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmpresasController : ControllerBase
    {
        private readonly IBitacoraRepository _repository;

        public EmpresasController(IBitacoraRepository repository)
        {
            _repository = repository;
        }

        [HttpGet("activas")]
        public async Task<ActionResult<List<string>>> GetEmpresasActivas()
        {
            var result = await _repository.GetEmpresasActivasAsync();
            return Ok(result);
        }
    }
}

