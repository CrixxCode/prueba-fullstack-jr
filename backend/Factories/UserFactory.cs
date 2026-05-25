using backend.Models;

namespace backend.Factories;

public sealed class UserFactory : IUserFactory
{
    public User CreateForRegistration(
        Guid userId,
        string normalizedEmail,
        string name,
        string passwordHash,
        string role,
        DateTime createdAtUtc
    )
    {
        return new User
        {
            Id = userId,
            Email = normalizedEmail,
            Name = name,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            FailedLoginAttempts = 0,
            LockoutEndAt = null,
            CreatedAt = createdAtUtc,
            CreatedBy = userId,
            UpdatedBy = userId
        };
    }

    public User CreateByAdmin(
        string normalizedEmail,
        string name,
        string passwordHash,
        string role,
        bool isActive,
        Guid? actorUserId,
        DateTime createdAtUtc
    )
    {
        return new User
        {
            Email = normalizedEmail,
            Name = name,
            PasswordHash = passwordHash,
            Role = role,
            IsActive = isActive,
            FailedLoginAttempts = 0,
            LockoutEndAt = null,
            CreatedAt = createdAtUtc,
            CreatedBy = actorUserId,
            UpdatedBy = actorUserId
        };
    }
}

