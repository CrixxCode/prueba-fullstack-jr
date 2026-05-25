using backend.Models;

namespace backend.Repositories;

public interface IRefreshTokenRepository
{
    IQueryable<RefreshToken> Query(bool asNoTracking = false);

    Task<RefreshToken?> GetByHashAsync(
        string tokenHash,
        bool includeUser = false,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    );

    Task<int> RevokeActiveAndSetReplacementAsync(
        Guid tokenId,
        DateTime revokedAtUtc,
        string replacementTokenHash,
        CancellationToken cancellationToken = default
    );

    void Add(RefreshToken refreshToken);
}
