using backend.Mappers;
using backend.Models;

namespace backend.Tests.Mappers;

public class AuthResponseMapperTests
{
    [Fact]
    public void Map_MapsTokensAndUserData()
    {
        var mapper = new AuthResponseMapper(new UserResponseMapper());
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "mapper@example.com",
            Name = "Mapper",
            Role = "admin",
            PasswordHash = "hash",
            IsActive = true,
            AvatarPath = "avatars/mapper.png",
            CreatedAt = DateTime.UtcNow
        };

        var response = mapper.Map(
            user,
            accessToken: "access-token",
            refreshToken: "refresh-token",
            scheme: "http",
            host: "localhost:5241"
        );

        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("refresh-token", response.RefreshToken);
        Assert.Equal(user.Id, response.User.Id);
        Assert.Equal("http://localhost:5241/uploads/avatars/mapper.png", response.User.AvatarUrl);
    }
}

