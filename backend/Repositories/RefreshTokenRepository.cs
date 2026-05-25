using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public IQueryable<RefreshToken> Query(bool asNoTracking = false)
    {
        return asNoTracking
            ? _context.RefreshTokens.AsNoTracking()
            : _context.RefreshTokens;
    }

    public async Task<RefreshToken?> GetByHashAsync(
        string tokenHash,
        bool includeUser = false,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    )
    {
        IQueryable<RefreshToken> query = Query(asNoTracking);

        if (includeUser)
        {
            query = query.Include(rt => rt.User);
        }

        return await query.FirstOrDefaultAsync(
            rt => rt.TokenHash == tokenHash,
            cancellationToken
        );
    }

    public Task<int> RevokeActiveAndSetReplacementAsync(
        Guid tokenId,
        DateTime revokedAtUtc,
        string replacementTokenHash,
        CancellationToken cancellationToken = default
    )
    {
        return _context.RefreshTokens
            .Where(rt =>
                rt.Id == tokenId &&
                rt.RevokedAt == null &&
                rt.ExpiresAt > revokedAtUtc
            )
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(rt => rt.RevokedAt, revokedAtUtc)
                    .SetProperty(rt => rt.ReplacedByTokenHash, replacementTokenHash),
                cancellationToken
            );
    }

    public void Add(RefreshToken refreshToken)
    {
        _context.RefreshTokens.Add(refreshToken);
    }
}
