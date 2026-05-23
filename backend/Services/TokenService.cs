using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using backend.Models;
using Microsoft.IdentityModel.Tokens;

namespace backend.Services;

public class TokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreateToken(User user)
    {
        var jwtKey = _configuration["Jwt:Key"];
        var jwtIssuer = _configuration["Jwt:Issuer"];
        var jwtAudience = _configuration["Jwt:Audience"];

        if (string.IsNullOrWhiteSpace(jwtKey) || IsWeakJwtKey(jwtKey))
        {
            throw new InvalidOperationException(
                "Configuracion JWT invalida: 'Jwt:Key' es obligatoria, debe tener al menos 32 caracteres y no puede usar valores de ejemplo."
            );
        }

        if (string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
        {
            throw new InvalidOperationException(
                "Configuracion JWT invalida: 'Jwt:Issuer' y 'Jwt:Audience' son obligatorios."
            );
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256
        );

        var configuredExpires = _configuration["Jwt:ExpiresInMinutes"];
        var expiresInMinutes = 60;

        if (!int.TryParse(configuredExpires, out expiresInMinutes) ||
            expiresInMinutes <= 0 ||
            expiresInMinutes > 1440)
        {
            expiresInMinutes = 60;
        }

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool IsWeakJwtKey(string key)
    {
        var trimmed = key.Trim();

        return trimmed.Length < 32
            || trimmed.Contains("CAMBIAR_EN_PRODUCCION", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("ESTA_CLAVE_DEBE_SER_LARGA_Y_SEGURA", StringComparison.OrdinalIgnoreCase);
    }
}
