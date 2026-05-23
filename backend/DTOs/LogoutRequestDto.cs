using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public class LogoutRequestDto
{
    [Required(ErrorMessage = "El refresh token es obligatorio.")]
    public string RefreshToken { get; set; } = string.Empty;
}
