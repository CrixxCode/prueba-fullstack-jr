using System.Security.Cryptography;
using System.Text;

namespace backend.Services;

public sealed class RefreshTokenService
{
    private readonly IConfiguration _configuration;

    public RefreshTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public RefreshTokenResult CreateToken()
    {
        var plainToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = ComputeHash(plainToken);
        var expiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenLifetimeDays());

        return new RefreshTokenResult(
            Token: plainToken,
            TokenHash: tokenHash,
            ExpiresAt: expiresAt
        );
    }

    public string ComputeHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private int GetRefreshTokenLifetimeDays()
    {
        var configuredDays = _configuration["Jwt:RefreshTokenExpiresInDays"];

        if (!int.TryParse(configuredDays, out var days) || days <= 0 || days > 90)
        {
            return 7;
        }

        return days;
    }
}

public sealed record RefreshTokenResult(
    string Token,
    string TokenHash,
    DateTime ExpiresAt
);
