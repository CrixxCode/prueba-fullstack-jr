using backend.DTOs;
using backend.Models;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Controllers;

public class UsersControllerTests
{
    [Fact]
    public async Task GetUserById_AsRegularUser_ForAnotherUser_Returns403()
    {
        using var db = new SqliteTestDbContext();

        var currentUserId = Guid.NewGuid();
        var requestedUserId = Guid.NewGuid();

        db.Context.Users.AddRange(
            new User
            {
                Id = currentUserId,
                Email = "owner@example.com",
                Name = "Owner",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("StrongPass1!"),
                Role = "user",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = requestedUserId,
                Email = "another@example.com",
                Name = "Another",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("StrongPass1!"),
                Role = "user",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        );

        await db.Context.SaveChangesAsync();

        var controller = ControllerFactory.CreateUsersController(
            db.Context,
            currentUserId,
            "user"
        );

        var result = await controller.GetUserById(requestedUserId);

        var forbidden = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Admin_CanRun_BasicCrudFlow()
    {
        using var db = new SqliteTestDbContext();

        var adminId = Guid.NewGuid();
        db.Context.Users.Add(new User
        {
            Id = adminId,
            Email = "admin@example.com",
            Name = "Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("StrongPass1!"),
            Role = "admin",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.Context.SaveChangesAsync();

        var controller = ControllerFactory.CreateUsersController(
            db.Context,
            adminId,
            "admin"
        );

        var createResult = await controller.CreateUser(new CreateUserDto
        {
            Email = "new.user@example.com",
            Name = "New User",
            Password = "StrongPass1!",
            Role = "user",
            IsActive = true
        });

        var createdAt = Assert.IsType<CreatedAtActionResult>(createResult.Result);
        var createdUser = Assert.IsType<UserResponseDto>(createdAt.Value);
        Assert.Equal("new.user@example.com", createdUser.Email);
        Assert.Equal("user", createdUser.Role);

        var getByIdResult = await controller.GetUserById(createdUser.Id);
        var okGetById = Assert.IsType<OkObjectResult>(getByIdResult.Result);
        var loadedUser = Assert.IsType<UserResponseDto>(okGetById.Value);
        Assert.Equal(createdUser.Id, loadedUser.Id);

        var updateResult = await controller.UpdateUser(createdUser.Id, new UpdateUserDto
        {
            Name = "Updated Name",
            Role = "admin",
            IsActive = false
        });

        var okUpdate = Assert.IsType<OkObjectResult>(updateResult.Result);
        var updatedUser = Assert.IsType<UserResponseDto>(okUpdate.Value);
        Assert.Equal("Updated Name", updatedUser.Name);
        Assert.Equal("admin", updatedUser.Role);
        Assert.False(updatedUser.IsActive);

        var deleteResult = await controller.DeleteUser(createdUser.Id);
        Assert.IsType<OkObjectResult>(deleteResult);

        Assert.False(await db.Context.Users.AnyAsync(u => u.Id == createdUser.Id));
    }
}
