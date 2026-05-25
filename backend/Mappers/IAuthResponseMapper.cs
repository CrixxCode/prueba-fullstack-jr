using backend.DTOs;
using backend.Models;

namespace backend.Mappers;

public interface IAuthResponseMapper
{
    AuthResponseDto Map(
        User user,
        string accessToken,
        string refreshToken,
        string scheme,
        string host
    );
}

