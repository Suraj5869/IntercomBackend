using System.ComponentModel.DataAnnotations;

namespace RiderIntercom.Dtos
{
    public class ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
