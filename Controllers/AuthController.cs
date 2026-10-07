using Microsoft.AspNetCore.Mvc;
using RiderIntercom.Dtos;
using RiderIntercom.Models;
using RiderIntercom.Services;
using System.Security.Cryptography;
using System.Text;

namespace RiderIntercom.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthRepository _repo;
        private readonly JwtService _jwt;
        private readonly EmailService _email;

        public AuthController(AuthRepository repo, JwtService jwt, EmailService email)
        {
            _repo = repo;
            _jwt = jwt;
            _email = email;
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

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            var user = await _repo.GetByEmail(dto.Email);

            // Always return the same response so the endpoint does not reveal
            // whether an email address exists in the system.
            if (user == null)
            {
                return Ok(new
                {
                    message = "If an account exists for this email, a password reset link has been sent."
                });
            }

            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var tokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))
            ).ToLowerInvariant();

            await _repo.CreatePasswordResetToken(
                user.Id,
                tokenHash,
                DateTime.UtcNow.AddMinutes(30)
            );

            var frontendBaseUrl = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["FrontendBaseUrl"]
                ?.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                return StatusCode(500, new { message = "Password reset is not configured." });
            }

            var resetUrl = $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

            await _email.SendPasswordResetEmailAsync(user.Email, user.Name, resetUrl);

            return Ok(new
            {
                message = "If an account exists for this email, a password reset link has been sent."
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            var tokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(dto.Token))
            ).ToLowerInvariant();

            var resetToken = await _repo.GetValidPasswordResetToken(tokenHash);

            if (resetToken == null)
            {
                return BadRequest(new { message = "This password reset link is invalid or has expired." });
            }

            await _repo.ResetPassword(resetToken.Id, resetToken.UserId, PasswordHelper.Hash(dto.NewPassword));

            return Ok(new { message = "Password reset successful. You can now log in." });
        }
    }
}
