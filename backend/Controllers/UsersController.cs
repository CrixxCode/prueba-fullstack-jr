using System.Security.Claims;
using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    private const long MaxAvatarSizeBytes = 2 * 1024 * 1024;

    private readonly IUserAppService _userAppService;

    public UsersController(IUserAppService userAppService)
    {
        _userAppService = userAppService;
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<UserListResponseDto>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] string? sortBy = "createdAt",
        [FromQuery] string? sortDir = "desc"
    )
    {
        var result = await _userAppService.GetUsersAsync(
            search,
            page,
            size,
            sortBy,
            sortDir,
            BuildRequestContext(),
            HttpContext.RequestAborted
        );

        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }

        return ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponseDto>> GetUserById(Guid id)
    {
        var result = await _userAppService.GetUserByIdAsync(
            id,
            BuildRequestContext(),
            HttpContext.RequestAborted
        );

        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }

        return ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<UserResponseDto>> CreateUser([FromBody] CreateUserDto request)
    {
        var result = await _userAppService.CreateUserAsync(
            request,
            BuildRequestContext(),
            HttpContext.RequestAborted
        );

        if (result.IsSuccess)
        {
            return CreatedAtAction(
                nameof(GetUserById),
                new { id = result.Data!.Id },
                result.Data
            );
        }

        return ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponseDto>> UpdateUser(Guid id, [FromBody] UpdateUserDto request)
    {
        var result = await _userAppService.UpdateUserAsync(
            id,
            request,
            BuildRequestContext(),
            HttpContext.RequestAborted
        );

        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }

        return ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var result = await _userAppService.DeleteUserAsync(
            id,
            BuildRequestContext(),
            HttpContext.RequestAborted
        );

        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }

        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAvatarSizeBytes + 1024)]
    public async Task<ActionResult<UserResponseDto>> UploadAvatar(Guid id, IFormFile? file)
    {
        var result = await _userAppService.UploadAvatarAsync(
            id,
            file,
            BuildRequestContext(),
            HttpContext.RequestAborted
        );

        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }

        return ToActionResult(result);
    }

    [HttpDelete("{id:guid}/avatar")]
    public async Task<ActionResult<UserResponseDto>> DeleteAvatar(Guid id)
    {
        var result = await _userAppService.DeleteAvatarAsync(
            id,
            BuildRequestContext(),
            HttpContext.RequestAborted
        );

        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }

        return ToActionResult(result);
    }

    private UserRequestContext BuildRequestContext()
    {
        return new UserRequestContext(
            CurrentUserId: GetCurrentUserId(),
            CurrentUserRole: GetCurrentUserRole(),
            Scheme: Request.Scheme,
            Host: Request.Host.Value
        );
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Guid.Empty;
        }

        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : Guid.Empty;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }

    private ActionResult ToActionResult<T>(AppServiceResult<T> result) where T : class
    {
        var error = result.Error!;

        return error.Type switch
        {
            AppServiceErrorType.BadRequest =>
                BadRequestError(error.Message, error.Code),
            AppServiceErrorType.Unauthorized =>
                UnauthorizedError(error.Message, error.Code),
            AppServiceErrorType.Forbidden =>
                ForbiddenError(error.Message, error.Code),
            AppServiceErrorType.NotFound =>
                NotFoundError(error.Message, error.Code),
            AppServiceErrorType.Conflict =>
                ConflictError(error.Message, error.Code),
            _ => BadRequestError(error.Message, error.Code)
        };
    }
}
