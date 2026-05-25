using backend.DTOs;

namespace backend.Services;

public interface IAuthAppService
{
    Task<AppServiceResult<AuthResponseDto>> RegisterAsync(
        RegisterRequestDto? request,
        AuthRequestContext requestContext,
        CancellationToken cancellationToken = default
    );

    Task<AppServiceResult<AuthResponseDto>> LoginAsync(
        LoginRequestDto? request,
        AuthRequestContext requestContext,
        CancellationToken cancellationToken = default
    );

    Task<AppServiceResult<AuthResponseDto>> RefreshAsync(
        RefreshTokenRequestDto? request,
        AuthRequestContext requestContext,
        CancellationToken cancellationToken = default
    );

    Task<AppServiceResult<MessageResponseDto>> LogoutAsync(
        LogoutRequestDto? request,
        AuthRequestContext requestContext,
        CancellationToken cancellationToken = default
    );
}
