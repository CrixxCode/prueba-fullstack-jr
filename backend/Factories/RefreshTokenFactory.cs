using backend.Models;

namespace backend.Factories;

public sealed class RefreshTokenFactory : IRefreshTokenFactory
{
    public RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        DateTime createdAtUtc
    )
    {
        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAtUtc,
            CreatedAt = createdAtUtc
        };
    }
}

