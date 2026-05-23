using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public class UpdateUserDto
{
    [EmailAddress(ErrorMessage = "El email no tiene un formato valido.")]
    [MaxLength(256, ErrorMessage = "El email no puede superar los 256 caracteres.")]
    public string? Email { get; set; }

    [MaxLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string? Name { get; set; }

    [MinLength(8, ErrorMessage = "La contrasena debe tener minimo 8 caracteres.")]
    public string? Password { get; set; }

    [MaxLength(20, ErrorMessage = "El rol no puede superar los 20 caracteres.")]
    public string? Role { get; set; }

    public bool? IsActive { get; set; }
}
