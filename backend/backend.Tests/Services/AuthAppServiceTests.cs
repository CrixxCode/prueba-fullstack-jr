using backend.DTOs;
using backend.Factories;
using backend.Mappers;
using backend.Models;
using backend.Repositories;
using backend.Services;
using backend.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Services;

public class AuthAppServiceTests
{
    [Fact]
    public async Task Register_WithNullRequest_ReturnsBadRequest()
    {
        using var db = new SqliteTestDbContext();
        var service = CreateService(db.Context);

        var result = await service.RegisterAsync(
            null,
            CreateRequestContext()
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(AppServiceErrorType.BadRequest, result.Error?.Type);
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsUnauthorized()
    {
        using var db = new SqliteTestDbContext();
        var service = CreateService(db.Context);

        var inactiveUser = new User
        {
            Email = "inactive@example.com",
            Name = "Inactive",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("StrongPass1!"),
            Role = "user",
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        db.Context.Users.Add(inactiveUser);
        await db.Context.SaveChangesAsync();

        var result = await service.LoginAsync(
            new LoginRequestDto
            {
                Email = "inactive@example.com",
                Password = "StrongPass1!"
            },
            CreateRequestContext()
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(AppServiceErrorType.Unauthorized, result.Error?.Type);
        Assert.Equal(0, await db.Context.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Logout_WithMissingRefreshToken_ReturnsBadRequest()
    {
        using var db = new SqliteTestDbContext();
        var service = CreateService(db.Context);

        var result = await service.LogoutAsync(
            new LogoutRequestDto { RefreshToken = string.Empty },
            CreateRequestContext()
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(AppServiceErrorType.BadRequest, result.Error?.Type);
        Assert.Equal("validation_error", result.Error?.Code);
    }

    [Fact]
    public async Task Logout_WithValidToken_RevokesToken_AndReturnsMessage()
    {
        using var db = new SqliteTestDbContext();
        var service = CreateService(db.Context);
        var configuration = TestConfiguration.Build();
        var refreshTokenService = new RefreshTokenService(configuration);

        var user = new User
        {
            Email = "logout@example.com",
            Name = "Logout User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("StrongPass1!"),
            Role = "user",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        const string plainRefreshToken = "logout-token";
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenService.ComputeHash(plainRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        };

        db.Context.RefreshTokens.Add(refreshToken);
        await db.Context.SaveChangesAsync();

        var result = await service.LogoutAsync(
            new LogoutRequestDto { RefreshToken = plainRefreshToken },
            CreateRequestContext()
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("Sesion cerrada correctamente.", result.Data?.Message);

        var persistedToken = await db.Context.RefreshTokens.SingleAsync(rt => rt.Id == refreshToken.Id);
        Assert.True(persistedToken.IsRevoked);
    }

    private static AuthAppService CreateService(backend.Data.AppDbContext context)
    {
        var configuration = TestConfiguration.Build();
        return new AuthAppService(
            context,
            new UserRepository(context),
            new RefreshTokenRepository(context),
            new TokenService(configuration),
            new RefreshTokenService(configuration),
            new AuthSecurityService(configuration),
            new UserFactory(),
            new RefreshTokenFactory(),
            new AuthAuditLogFactory(),
            new AuthResponseMapper(new UserResponseMapper()),
            NullLogger<AuthAppService>.Instance
        );
    }

    private static AuthRequestContext CreateRequestContext()
    {
        return new AuthRequestContext(
            Scheme: "http",
            Host: "localhost",
            IpAddress: "127.0.0.1",
            UserAgent: "xunit"
        );
    }
}

