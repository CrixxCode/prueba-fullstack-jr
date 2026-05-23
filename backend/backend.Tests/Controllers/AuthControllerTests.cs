using backend.Controllers;
using backend.DTOs;
using backend.Models;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Controllers;

public class AuthControllerTests
{
    [Fact]
    public async Task Register_CreatesFirstUserAsAdmin_AndReturnsTokens()
    {
        using var db = new SqliteTestDbContext();
        var controller = ControllerFactory.CreateAuthController(db.Context);

        var request = new RegisterRequestDto
        {
            Email = "admin@example.com",
            Password = "StrongPass1!",
            Name = "Admin"
        };

        var result = await controller.Register(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponseDto>(ok.Value);

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.Equal("admin", response.User.Role);
        Assert.Equal("admin@example.com", response.User.Email);

        var persistedUser = await db.Context.Users.SingleAsync();
        Assert.Equal("admin", persistedUser.Role);
        Assert.Equal(response.User.Id, persistedUser.Id);
        Assert.Equal(1, await db.Context.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens_AndResetsSecurityFields()
    {
        using var db = new SqliteTestDbContext();
        var controller = ControllerFactory.CreateAuthController(db.Context);

        var user = new User
        {
            Email = "user@example.com",
            Name = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("StrongPass1!"),
            Role = "user",
            IsActive = true,
            FailedLoginAttempts = 2,
            LockoutEndAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow
        };

        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        var request = new LoginRequestDto
        {
            Email = "user@example.com",
            Password = "StrongPass1!"
        };

        var result = await controller.Login(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponseDto>(ok.Value);

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
        Assert.Equal(user.Id, response.User.Id);
        Assert.Equal("user", response.User.Role);

        var persistedUser = await db.Context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(0, persistedUser.FailedLoginAttempts);
        Assert.Null(persistedUser.LockoutEndAt);
        Assert.Equal(1, await db.Context.RefreshTokens.CountAsync(rt => rt.UserId == user.Id));
    }
}
