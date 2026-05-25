using backend.Models;

namespace backend.Factories;

public interface IRefreshTokenFactory
{
    RefreshToken Create(
        Guid userId,
        string tokenHash,
        DateTime expiresAtUtc,
        DateTime createdAtUtc
    );
}

