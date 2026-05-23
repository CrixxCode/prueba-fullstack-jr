using System.Net;
using System.Security.Claims;
using backend.Controllers;
using backend.Data;
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
        var controller = new AuthController(
            context,
            new TokenService(configuration),
            new RefreshTokenService(configuration),
            new AuthSecurityService(configuration),
            NullLogger<AuthController>.Instance
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
        var controller = new UsersController(
            context,
            new AuthSecurityService(configuration),
            new TestWebHostEnvironment()
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
