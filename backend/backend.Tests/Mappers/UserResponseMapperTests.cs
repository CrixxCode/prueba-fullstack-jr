using backend.Mappers;
using backend.Models;

namespace backend.Tests.Mappers;

public class UserResponseMapperTests
{
    [Fact]
    public void Map_WithAvatarPath_BuildsAbsoluteAvatarUrl()
    {
        var mapper = new UserResponseMapper();
        var user = BuildUser();
        user.AvatarPath = "avatars/photo.png";

        var dto = mapper.Map(user, "https", "api.local");

        Assert.Equal("https://api.local/uploads/avatars/photo.png", dto.AvatarUrl);
    }

    [Fact]
    public void MapMany_MapsAllUsers()
    {
        var mapper = new UserResponseMapper();
        var users = new[]
        {
            BuildUser("one@example.com", "One"),
            BuildUser("two@example.com", "Two")
        };

        var dtos = mapper.MapMany(users, "http", "localhost:5241");

        Assert.Equal(2, dtos.Count);
        Assert.Equal("one@example.com", dtos[0].Email);
        Assert.Equal("two@example.com", dtos[1].Email);
    }

    private static User BuildUser(
        string email = "user@example.com",
        string name = "User"
    )
    {
        return new User
        {
            Email = email,
            Name = name,
            Role = "user",
            PasswordHash = "hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}

