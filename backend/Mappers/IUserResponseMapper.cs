using backend.DTOs;
using backend.Models;

namespace backend.Mappers;

public interface IUserResponseMapper
{
    UserResponseDto Map(User user, string scheme, string host);

    List<UserResponseDto> MapMany(
        IEnumerable<User> users,
        string scheme,
        string host
    );
}

