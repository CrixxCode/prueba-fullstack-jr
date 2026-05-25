using backend.DTOs;
using backend.Factories;
using backend.Mappers;
using backend.Models;
using backend.Repositories;
using backend.Services;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Services;

public class UserAppServiceTests
{
    [Fact]
    public async Task GetUsers_AsNonAdmin_ReturnsForbidden()
    {
        using var db = new SqliteTestDbContext();
        var service = CreateService(db.Context);

        var result = await service.GetUsersAsync(
            search: null,
            page: 1,
            size: 10,
            sortBy: "createdAt",
            sortDir: "desc",
            requestContext: CreateRequestContext(Guid.NewGuid(), "user")
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(AppServiceErrorType.Forbidden, result.Error?.Type);
    }

    [Fact]
    public async Task GetUsers_AsAdmin_ReturnsPaginatedList()
    {
        using var db = new SqliteTestDbContext();
        var service = CreateService(db.Context);

        db.Context.Users.AddRange(
            BuildUser("b@example.com", "Beta"),
            BuildUser("a@example.com", "Alpha"),
            BuildUser("c@example.com", "Gamma")
        );
        await db.Context.SaveChangesAsync();

        var result = await service.GetUsersAsync(
            search: null,
            page: 1,
            size: 2,
            sortBy: "name",
            sortDir: "asc",
            requestContext: CreateRequestContext(Guid.NewGuid(), "admin")
        );

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.TotalItems);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal("Alpha", result.Data.Items[0].Name);
        Assert.Equal("Beta", result.Data.Items[1].Name);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_ReturnsConflict()
    {
        using var db = new SqliteTestDbContext();
        var service = CreateService(db.Context);

        db.Context.Users.Add(BuildUser("dup@example.com", "Existing"));
        await db.Context.SaveChangesAsync();

        var result = await service.CreateUserAsync(
            new CreateUserDto
            {
                Email = "dup@example.com",
                Name = "Another",
                Password = "StrongPass1!",
                Role = "user",
                IsActive = true
            },
            CreateRequestContext(Guid.NewGuid(), "admin")
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(AppServiceErrorType.Conflict, result.Error?.Type);
    }

    [Fact]
    public async Task UploadAvatar_WithInvalidMimeType_ReturnsBadRequest()
    {
        using var db = new SqliteTestDbContext();
        var service = CreateService(db.Context);
        var user = BuildUser("avatar@example.com", "Avatar");
        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        var file = CreateFormFile(
            fileName: "avatar.png",
            contentType: "application/pdf"
        );

        var result = await service.UploadAvatarAsync(
            user.Id,
            file,
            CreateRequestContext(user.Id, "user")
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(AppServiceErrorType.BadRequest, result.Error?.Type);
        Assert.Equal("validation_error", result.Error?.Code);
    }

    [Fact]
    public async Task DeleteUser_RemovesUser_AndReturnsMessage()
    {
        using var db = new SqliteTestDbContext();
        var service = CreateService(db.Context);
        var user = BuildUser("delete@example.com", "Delete");
        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        var result = await service.DeleteUserAsync(
            user.Id,
            CreateRequestContext(Guid.NewGuid(), "admin")
        );

        Assert.True(result.IsSuccess);
        Assert.Equal("Usuario eliminado correctamente.", result.Data?.Message);
        Assert.False(await db.Context.Users.AnyAsync(u => u.Id == user.Id));
    }

    private static UserAppService CreateService(backend.Data.AppDbContext context)
    {
        var configuration = TestConfiguration.Build();
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = Path.Combine(
                Path.GetTempPath(),
                $"backend-tests-user-app-{Guid.NewGuid():N}"
            )
        };

        return new UserAppService(
            context,
            new UserRepository(context),
            new UserFactory(),
            new UserResponseMapper(),
            new AvatarStorageService(environment),
            new AuthSecurityService(configuration),
            NullLogger<UserAppService>.Instance
        );
    }

    private static User BuildUser(string email, string name)
    {
        return new User
        {
            Email = email,
            Name = name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("StrongPass1!"),
            Role = "user",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static UserRequestContext CreateRequestContext(Guid userId, string role)
    {
        return new UserRequestContext(
            CurrentUserId: userId,
            CurrentUserRole: role,
            Scheme: "http",
            Host: "localhost"
        );
    }

    private static IFormFile CreateFormFile(string fileName, string contentType)
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}

