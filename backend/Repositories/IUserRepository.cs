using backend.Models;

namespace backend.Repositories;

public interface IUserRepository
{
    IQueryable<User> Query(bool asNoTracking = false);

    Task<User?> GetByIdAsync(
        Guid id,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    );

    Task<User?> GetByEmailAsync(
        string normalizedEmail,
        bool asNoTracking = false,
        CancellationToken cancellationToken = default
    );

    Task<bool> EmailExistsAsync(
        string normalizedEmail,
        Guid? excludingUserId = null,
        CancellationToken cancellationToken = default
    );

    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    void Add(User user);

    void Remove(User user);
}
