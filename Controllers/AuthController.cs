using Microsoft.AspNetCore.Mvc;
using RiderIntercom.Dtos;
using RiderIntercom.Models;
using RiderIntercom.Services;

namespace RiderIntercom.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthRepository _repo;
        private readonly JwtService _jwt;
        public AuthController(AuthRepository repo, JwtService jwt)
        {
            _repo = repo;
            _jwt = jwt;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> Signup(SignupDto dto)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Email = dto.Email,
                PasswordHash = PasswordHelper.Hash(dto.Password)
            };

            await _repo.CreateUser(user);

            return Ok(new { message = "User created" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            var user = await _repo.GetByEmail(dto.Email);

            if (user == null || !PasswordHelper.Verify(dto.Password, user.PasswordHash))
                return Unauthorized();

            var token = _jwt.GenerateToken(user.Id.ToString(), user.Name);

            return Ok(new
            {
                userId = user.Id,
                name = user.Name,
                token = token
            });
        }
    }
}
