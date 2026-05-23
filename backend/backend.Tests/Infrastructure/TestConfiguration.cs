using Microsoft.Extensions.Configuration;

namespace backend.Tests.Infrastructure;

internal static class TestConfiguration
{
    public static IConfiguration Build()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "0123456789ABCDEF0123456789ABCDEF",
            ["Jwt:Issuer"] = "backend-tests",
            ["Jwt:Audience"] = "backend-tests-client",
            ["Jwt:ExpiresInMinutes"] = "60",
            ["Jwt:RefreshTokenExpiresInDays"] = "7",
            ["Security:PasswordMinLength"] = "8",
            ["Security:MaxFailedLoginAttempts"] = "5",
            ["Security:LockoutMinutes"] = "15"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }
}
