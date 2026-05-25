using backend.DTOs;
using backend.Models;

namespace backend.Mappers;

public sealed class AuthResponseMapper : IAuthResponseMapper
{
    private readonly IUserResponseMapper _userResponseMapper;

    public AuthResponseMapper(IUserResponseMapper userResponseMapper)
    {
        _userResponseMapper = userResponseMapper;
    }

    public AuthResponseDto Map(
        User user,
        string accessToken,
        string refreshToken,
        string scheme,
        string host
    )
    {
        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = _userResponseMapper.Map(user, scheme, host)
        };
    }
}

