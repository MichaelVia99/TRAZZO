using BitacoraApi.Models;
using BitacoraApi.Repositories;
using Microsoft.AspNetCore.Mvc;
using BitacoraApi.Security;

namespace BitacoraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IBitacoraRepository _repository;

        public AuthController(IBitacoraRepository repository)
        {
            _repository = repository;
        }

        [HttpPost("login")]
        public async Task<ActionResult<UsuarioDto>> Login([FromBody] LoginRequest request)
        {
            var user = await _repository.LoginAsync(request.Email, request.Password);
            if (user == null)
            {
                return Unauthorized();
            }
            return Ok(user);
        }

        [HttpPost("hash")]
        public ActionResult<string> Hash([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest();
            }

            var hashed = PasswordHasher.Hash(request.Password);
            return Ok(hashed);
        }
    }
}
