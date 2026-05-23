using backend.Data;
using backend.Models;
using backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Data;

public class DbSeederTests
{
    [Fact]
    public async Task SeedDevelopmentDemoUsersAsync_CreatesDemoUsers_AndIsIdempotent()
    {
        using var db = new SqliteTestDbContext();

        await DbSeeder.SeedDevelopmentDemoUsersAsync(
            db.Context,
            NullLogger.Instance
        );
        await DbSeeder.SeedDevelopmentDemoUsersAsync(
            db.Context,
            NullLogger.Instance
        );

        var users = await db.Context.Users
            .OrderBy(u => u.Email)
            .ToListAsync();

        Assert.Equal(2, users.Count);

        var admin = users.Single(u => u.Email == "admin@demo.com");
        Assert.Equal("admin", admin.Role);
        Assert.True(admin.IsActive);
        Assert.True(BCrypt.Net.BCrypt.Verify("Admin123!", admin.PasswordHash));

        var regular = users.Single(u => u.Email == "user@demo.com");
        Assert.Equal("user", regular.Role);
        Assert.True(regular.IsActive);
        Assert.True(BCrypt.Net.BCrypt.Verify("User123!", regular.PasswordHash));
    }

    [Fact]
    public async Task SeedDevelopmentDemoUsersAsync_UpdatesExistingDemoUsersToExpectedState()
    {
        using var db = new SqliteTestDbContext();

        db.Context.Users.AddRange(
            new User
            {
                Email = "admin@demo.com",
                Name = "Custom Admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("BadPassword!"),
                Role = "user",
                IsActive = false,
                FailedLoginAttempts = 4,
                LockoutEndAt = DateTime.UtcNow.AddMinutes(10),
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Email = "user@demo.com",
                Name = "Custom User",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("BadPassword!"),
                Role = "admin",
                IsActive = false,
                FailedLoginAttempts = 3,
                LockoutEndAt = DateTime.UtcNow.AddMinutes(10),
                CreatedAt = DateTime.UtcNow
            }
        );
        await db.Context.SaveChangesAsync();

        await DbSeeder.SeedDevelopmentDemoUsersAsync(
            db.Context,
            NullLogger.Instance
        );

        var admin = await db.Context.Users.SingleAsync(u => u.Email == "admin@demo.com");
        Assert.Equal("Admin Demo", admin.Name);
        Assert.Equal("admin", admin.Role);
        Assert.True(admin.IsActive);
        Assert.Equal(0, admin.FailedLoginAttempts);
        Assert.Null(admin.LockoutEndAt);
        Assert.True(BCrypt.Net.BCrypt.Verify("Admin123!", admin.PasswordHash));

        var regular = await db.Context.Users.SingleAsync(u => u.Email == "user@demo.com");
        Assert.Equal("User Demo", regular.Name);
        Assert.Equal("user", regular.Role);
        Assert.True(regular.IsActive);
        Assert.Equal(0, regular.FailedLoginAttempts);
        Assert.Null(regular.LockoutEndAt);
        Assert.True(BCrypt.Net.BCrypt.Verify("User123!", regular.PasswordHash));
    }
}
