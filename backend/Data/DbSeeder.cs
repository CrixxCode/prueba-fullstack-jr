using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public static class DbSeeder
{
    private static readonly DemoUserSeed AdminDemoUser = new(
        Email: "admin@demo.com",
        Name: "Admin Demo",
        Password: "Admin123!",
        Role: "admin"
    );

    private static readonly DemoUserSeed RegularDemoUser = new(
        Email: "user@demo.com",
        Name: "User Demo",
        Password: "User123!",
        Role: "user"
    );

    public static async Task SeedDevelopmentDemoUsersAsync(
        AppDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);

        var createdCount = 0;
        var updatedCount = 0;

        var adminResult = await UpsertDemoUserAsync(
            dbContext,
            AdminDemoUser,
            cancellationToken
        );

        if (adminResult == SeedAction.Created)
        {
            createdCount++;
        }
        else if (adminResult == SeedAction.Updated)
        {
            updatedCount++;
        }

        var userResult = await UpsertDemoUserAsync(
            dbContext,
            RegularDemoUser,
            cancellationToken
        );

        if (userResult == SeedAction.Created)
        {
            createdCount++;
        }
        else if (userResult == SeedAction.Updated)
        {
            updatedCount++;
        }

        if (createdCount == 0 && updatedCount == 0)
        {
            logger.LogInformation(
                "Seeder de desarrollo: cuentas demo ya estaban actualizadas."
            );
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeder de desarrollo aplicado. Cuentas creadas: {CreatedCount}, cuentas actualizadas: {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }

    private static async Task<SeedAction> UpsertDemoUserAsync(
        AppDbContext dbContext,
        DemoUserSeed seed,
        CancellationToken cancellationToken
    )
    {
        var normalizedEmail = seed.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(
                u => u.Email == normalizedEmail,
                cancellationToken
            );

        if (user is null)
        {
            dbContext.Users.Add(new User
            {
                Email = normalizedEmail,
                Name = seed.Name,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(seed.Password),
                Role = seed.Role,
                IsActive = true,
                FailedLoginAttempts = 0,
                LockoutEndAt = null,
                CreatedAt = DateTime.UtcNow
            });

            return SeedAction.Created;
        }

        var hasChanges = false;

        if (!string.Equals(user.Email, normalizedEmail, StringComparison.Ordinal))
        {
            user.Email = normalizedEmail;
            hasChanges = true;
        }

        if (!string.Equals(user.Name, seed.Name, StringComparison.Ordinal))
        {
            user.Name = seed.Name;
            hasChanges = true;
        }

        if (!string.Equals(user.Role, seed.Role, StringComparison.OrdinalIgnoreCase))
        {
            user.Role = seed.Role;
            hasChanges = true;
        }

        if (!user.IsActive)
        {
            user.IsActive = true;
            hasChanges = true;
        }

        if (user.FailedLoginAttempts != 0)
        {
            user.FailedLoginAttempts = 0;
            hasChanges = true;
        }

        if (user.LockoutEndAt is not null)
        {
            user.LockoutEndAt = null;
            hasChanges = true;
        }

        var passwordMatches = VerifyPassword(seed.Password, user.PasswordHash);

        if (!passwordMatches)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(seed.Password);
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return SeedAction.NoChanges;
        }

        user.UpdatedAt = DateTime.UtcNow;
        return SeedAction.Updated;
    }

    private static bool VerifyPassword(string plainPassword, string hashedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(plainPassword, hashedPassword);
        }
        catch
        {
            return false;
        }
    }

    private enum SeedAction
    {
        NoChanges,
        Created,
        Updated
    }

    private sealed record DemoUserSeed(
        string Email,
        string Name,
        string Password,
        string Role
    );
}
