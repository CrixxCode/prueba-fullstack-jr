using System.Net;
using System.Security.Claims;
using backend.Controllers;
using backend.Data;
using backend.Factories;
using backend.Mappers;
using backend.Repositories;
using backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Infrastructure;

internal static class ControllerFactory
{
    public static AuthController CreateAuthController(AppDbContext context)
    {
        var configuration = TestConfiguration.Build();
        var authAppService = new AuthAppService(
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

        var controller = new AuthController(
            authAppService
        );

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = BuildHttpContext()
        };

        return controller;
    }

    public static UsersController CreateUsersController(
        AppDbContext context,
        Guid currentUserId,
        string currentUserRole
    )
    {
        var configuration = TestConfiguration.Build();
        var userAppService = new UserAppService(
            context,
            new UserRepository(context),
            new UserFactory(),
            new UserResponseMapper(),
            new AvatarStorageService(new TestWebHostEnvironment()),
            new AuthSecurityService(configuration),
            NullLogger<UserAppService>.Instance
        );

        var controller = new UsersController(
            userAppService
        );

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = BuildHttpContext(
                currentUserId: currentUserId,
                role: currentUserRole
            )
        };

        return controller;
    }

    private static DefaultHttpContext BuildHttpContext(
        Guid? currentUserId = null,
        string? role = null
    )
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost");

        if (currentUserId.HasValue && !string.IsNullOrWhiteSpace(role))
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, currentUserId.Value.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.Email, "test@example.com"),
                new Claim(ClaimTypes.Name, "Test User")
            };

            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(claims, "TestAuth")
            );
        }

        return context;
    }
}
