using backend.DTOs;
using backend.Models;

namespace backend.Mappers;

public sealed class UserResponseMapper : IUserResponseMapper
{
    public UserResponseDto Map(User user, string scheme, string host)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            Role = user.Role,
            AvatarUrl = BuildAvatarUrl(user.AvatarPath, scheme, host),
            IsActive = user.IsActive
        };
    }

    public List<UserResponseDto> MapMany(
        IEnumerable<User> users,
        string scheme,
        string host
    )
    {
        return users
            .Select(user => Map(user, scheme, host))
            .ToList();
    }

    private static string? BuildAvatarUrl(string? avatarPath, string scheme, string host)
    {
        if (string.IsNullOrWhiteSpace(avatarPath))
        {
            return null;
        }

        var normalizedPath = avatarPath.Replace('\\', '/').TrimStart('/');
        return $"{scheme}://{host}/uploads/{normalizedPath}";
    }
}

