using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public IQueryable<User> Query(bool asNoTracking = false)
    {
        return asNoTracking
            ? _context.Users.AsNoTracking()
            : _context.Users;
    }

    public Task<User?> GetByIdAsync(
        Guid id,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    )
    {
        return Query(asNoTracking)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(
        string normalizedEmail,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    )
    {
        return Query(asNoTracking)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public Task<bool> EmailExistsAsync(
        string normalizedEmail,
        Guid? excludingUserId = null,
        CancellationToken cancellationToken = default
    )
    {
        return Query(asNoTracking: true)
            .AnyAsync(
                u => u.Email == normalizedEmail &&
                     (!excludingUserId.HasValue || u.Id != excludingUserId.Value),
                cancellationToken
            );
    }

    public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return Query(asNoTracking: true).AnyAsync(cancellationToken);
    }

    public void Add(User user)
    {
        _context.Users.Add(user);
    }

    public void Remove(User user)
    {
        _context.Users.Remove(user);
    }
}
