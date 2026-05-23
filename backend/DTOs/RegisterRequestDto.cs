using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato valido.")]
    [MaxLength(256, ErrorMessage = "El email no puede superar los 256 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contrasena debe tener minimo 8 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Name { get; set; } = string.Empty;
}
