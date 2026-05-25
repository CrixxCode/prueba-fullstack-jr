using backend.DTOs;
using Microsoft.AspNetCore.Http;

namespace backend.Services;

public interface IUserAppService
{
    Task<AppServiceResult<UserListResponseDto>> GetUsersAsync(
        string? search,
        int page,
        int size,
        string? sortBy,
        string? sortDir,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    );

    Task<AppServiceResult<UserResponseDto>> GetUserByIdAsync(
        Guid id,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    );

    Task<AppServiceResult<UserResponseDto>> CreateUserAsync(
        CreateUserDto? request,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    );

    Task<AppServiceResult<UserResponseDto>> UpdateUserAsync(
        Guid id,
        UpdateUserDto? request,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    );

    Task<AppServiceResult<MessageResponseDto>> DeleteUserAsync(
        Guid id,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    );

    Task<AppServiceResult<UserResponseDto>> UploadAvatarAsync(
        Guid id,
        IFormFile? file,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    );

    Task<AppServiceResult<UserResponseDto>> DeleteAvatarAsync(
        Guid id,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    );
}

public sealed record UserRequestContext(
    Guid CurrentUserId,
    string CurrentUserRole,
    string Scheme,
    string Host
);

