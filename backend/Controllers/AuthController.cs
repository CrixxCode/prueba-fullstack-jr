using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthAppService _authAppService;

    public AuthController(IAuthAppService authAppService)
    {
        _authAppService = authAppService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var result = await _authAppService.RegisterAsync(
            request,
            BuildRequestContext(),
            HttpContext.RequestAborted
        );

        return ToActionResult(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authAppService.LoginAsync(
            request,
            BuildRequestContext(),
            HttpContext.RequestAborted
        );

        return ToActionResult(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        var result = await _authAppService.RefreshAsync(
            request,
            BuildRequestContext(),
            HttpContext.RequestAborted
        );

        return ToActionResult(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request)
    {
        var result = await _authAppService.LogoutAsync(
            request,
            BuildRequestContext(),
            HttpContext.RequestAborted
        );

        return ToActionResult(result);
    }

    private AuthRequestContext BuildRequestContext()
    {
        return new AuthRequestContext(
            Scheme: Request.Scheme,
            Host: Request.Host.Value,
            IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent: HttpContext.Request.Headers.UserAgent.ToString()
        );
    }

    private IActionResult ToActionResult<T>(AppServiceResult<T> result) where T : class
    {
        if (result.IsSuccess)
        {
            return Ok(result.Data);
        }

        var error = result.Error!;

        return error.Type switch
        {
            AppServiceErrorType.BadRequest =>
                BadRequestError(error.Message, error.Code),
            AppServiceErrorType.Unauthorized =>
                UnauthorizedError(error.Message, error.Code),
            AppServiceErrorType.Conflict =>
                ConflictError(error.Message, error.Code),
            AppServiceErrorType.Forbidden =>
                ForbiddenError(error.Message, error.Code),
            AppServiceErrorType.NotFound =>
                NotFoundError(error.Message, error.Code),
            _ => BadRequestError(error.Message, error.Code)
        };
    }
}
