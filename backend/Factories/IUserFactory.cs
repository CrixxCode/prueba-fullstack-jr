using backend.Models;

namespace backend.Factories;

public interface IUserFactory
{
    User CreateForRegistration(
        Guid userId,
        string normalizedEmail,
        string name,
        string passwordHash,
        string role,
        DateTime createdAtUtc
    );

    User CreateByAdmin(
        string normalizedEmail,
        string name,
        string passwordHash,
        string role,
        bool isActive,
        Guid? actorUserId,
        DateTime createdAtUtc
    );
}

