using backend.Common;
using backend.Controllers;
using backend.DTOs;
using backend.Models;
using backend.Services;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace backend.Tests.Scenarios;

public class TechnicalTestScenariosTests
{
    [Fact]
    public async Task Register_Login_And_Authorization_UserCannotEditAnotherProfile()
    {
        using var db = new SqliteTestDbContext();
        var authController = ControllerFactory.CreateAuthController(db.Context);

        var registerAdminResult = await authController.Register(new RegisterRequestDto
        {
            Email = "admin@scenario.com",
            Password = "Admin123!",
            Name = "Admin Scenario"
        });

        var registerAdminOk = Assert.IsType<OkObjectResult>(registerAdminResult);
        var adminAuth = Assert.IsType<AuthResponseDto>(registerAdminOk.Value);

        var registerUserResult = await authController.Register(new RegisterRequestDto
        {
            Email = "user@scenario.com",
            Password = "User123!",
            Name = "User Scenario"
        });

        var registerUserOk = Assert.IsType<OkObjectResult>(registerUserResult);
        var userAuth = Assert.IsType<AuthResponseDto>(registerUserOk.Value);

        var loginUserResult = await authController.Login(new LoginRequestDto
        {
            Email = "user@scenario.com",
            Password = "User123!"
        });

        var loginUserOk = Assert.IsType<OkObjectResult>(loginUserResult);
        var loginUserAuth = Assert.IsType<AuthResponseDto>(loginUserOk.Value);

        Assert.False(string.IsNullOrWhiteSpace(loginUserAuth.AccessToken));
        Assert.Equal(userAuth.User.Id, loginUserAuth.User.Id);

        var usersControllerAsRegularUser = ControllerFactory.CreateUsersController(
            db.Context,
            loginUserAuth.User.Id,
            "user"
        );

        var updateResult = await usersControllerAsRegularUser.UpdateUser(
            adminAuth.User.Id,
            new UpdateUserDto
            {
                Name = "Intento no autorizado"
            }
        );

        var forbidden = Assert.IsType<ObjectResult>(updateResult.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        using var db = new SqliteTestDbContext();
        var authController = ControllerFactory.CreateAuthController(db.Context);

        var firstRegister = await authController.Register(new RegisterRequestDto
        {
            Email = "duplicate@scenario.com",
            Password = "StrongPass1!",
            Name = "First User"
        });

        Assert.IsType<OkObjectResult>(firstRegister);

        var secondRegister = await authController.Register(new RegisterRequestDto
        {
            Email = "duplicate@scenario.com",
            Password = "StrongPass1!",
            Name = "Second User"
        });

        var conflict = Assert.IsType<ConflictObjectResult>(secondRegister);
        var error = Assert.IsType<ApiError>(conflict.Value);

        Assert.Equal("conflict", error.Code);
    }

    [Fact]
    public async Task Admin_CreatesUser_And_NewUserCanAuthenticate()
    {
        using var db = new SqliteTestDbContext();
        var authController = ControllerFactory.CreateAuthController(db.Context);

        var registerAdminResult = await authController.Register(new RegisterRequestDto
        {
            Email = "admin-create@scenario.com",
            Password = "Admin123!",
            Name = "Admin Creator"
        });

        var registerAdminOk = Assert.IsType<OkObjectResult>(registerAdminResult);
        var adminAuth = Assert.IsType<AuthResponseDto>(registerAdminOk.Value);

        var usersControllerAsAdmin = ControllerFactory.CreateUsersController(
            db.Context,
            adminAuth.User.Id,
            "admin"
        );

        var createUserResult = await usersControllerAsAdmin.CreateUser(new CreateUserDto
        {
            Email = "new-user@scenario.com",
            Password = "User123!",
            Name = "Created User",
            Role = "user",
            IsActive = true
        });

        var createdAt = Assert.IsType<CreatedAtActionResult>(createUserResult.Result);
        var createdUser = Assert.IsType<UserResponseDto>(createdAt.Value);

        var loginResult = await authController.Login(new LoginRequestDto
        {
            Email = "new-user@scenario.com",
            Password = "User123!"
        });

        var loginOk = Assert.IsType<OkObjectResult>(loginResult);
        var loginAuth = Assert.IsType<AuthResponseDto>(loginOk.Value);

        Assert.Equal(createdUser.Id, loginAuth.User.Id);
        Assert.False(string.IsNullOrWhiteSpace(loginAuth.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(loginAuth.RefreshToken));
    }

    [Fact]
    public async Task Refresh_RotatesToken_And_RejectsReplay()
    {
        using var db = new SqliteTestDbContext();
        var authController = ControllerFactory.CreateAuthController(db.Context);

        await authController.Register(new RegisterRequestDto
        {
            Email = "refresh@scenario.com",
            Password = "StrongPass1!",
            Name = "Refresh User"
        });

        var loginResult = await authController.Login(new LoginRequestDto
        {
            Email = "refresh@scenario.com",
            Password = "StrongPass1!"
        });

        var loginOk = Assert.IsType<OkObjectResult>(loginResult);
        var loginAuth = Assert.IsType<AuthResponseDto>(loginOk.Value);
        var originalRefreshToken = loginAuth.RefreshToken;

        var refreshResult = await authController.Refresh(new RefreshTokenRequestDto
        {
            RefreshToken = originalRefreshToken
        });

        var refreshOk = Assert.IsType<OkObjectResult>(refreshResult);
        var refreshedAuth = Assert.IsType<AuthResponseDto>(refreshOk.Value);

        Assert.NotEqual(originalRefreshToken, refreshedAuth.RefreshToken);

        var replayResult = await authController.Refresh(new RefreshTokenRequestDto
        {
            RefreshToken = originalRefreshToken
        });

        Assert.IsType<UnauthorizedObjectResult>(replayResult);

    }

    [Fact]
    public async Task Logout_RevokesRefreshToken_And_PreventsFurtherRefresh()
    {
        using var db = new SqliteTestDbContext();
        var authController = ControllerFactory.CreateAuthController(db.Context);

        await authController.Register(new RegisterRequestDto
        {
            Email = "logout@scenario.com",
            Password = "StrongPass1!",
            Name = "Logout User"
        });

        var loginResult = await authController.Login(new LoginRequestDto
        {
            Email = "logout@scenario.com",
            Password = "StrongPass1!"
        });

        var loginOk = Assert.IsType<OkObjectResult>(loginResult);
        var loginAuth = Assert.IsType<AuthResponseDto>(loginOk.Value);
        var refreshToken = loginAuth.RefreshToken;

        var logoutResult = await authController.Logout(new LogoutRequestDto
        {
            RefreshToken = refreshToken
        });

        Assert.IsType<OkObjectResult>(logoutResult);

        var refreshAfterLogoutResult = await authController.Refresh(new RefreshTokenRequestDto
        {
            RefreshToken = refreshToken
        });

        Assert.IsType<UnauthorizedObjectResult>(refreshAfterLogoutResult);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ReturnsUnauthorized()
    {
        using var db = new SqliteTestDbContext();
        var configuration = TestConfiguration.Build();
        var refreshTokenService = new RefreshTokenService(configuration);
        var plainRefreshToken = "expired-refresh-token";

        var user = new User
        {
            Email = "expired@scenario.com",
            Name = "Expired Token User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("StrongPass1!"),
            Role = "user",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Context.Users.Add(user);
        await db.Context.SaveChangesAsync();

        db.Context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenService.ComputeHash(plainRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });
        await db.Context.SaveChangesAsync();

        var authController = ControllerFactory.CreateAuthController(db.Context);

        var refreshResult = await authController.Refresh(new RefreshTokenRequestDto
        {
            RefreshToken = plainRefreshToken
        });

        Assert.IsType<UnauthorizedObjectResult>(refreshResult);
    }
}
