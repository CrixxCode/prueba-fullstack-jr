using System.Data;
using backend.Data;
using backend.DTOs;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ApiControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokenService;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly AuthSecurityService _authSecurityService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AppDbContext context,
        TokenService tokenService,
        RefreshTokenService refreshTokenService,
        AuthSecurityService authSecurityService,
        ILogger<AuthController> logger
    )
    {
        _context = context;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _authSecurityService = authSecurityService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        if (request is null)
        {
            return BadRequestError("El cuerpo de la solicitud es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequestError(
                "Email, contrasena y nombre son obligatorios.",
                code: "validation_error"
            );
        }

        var passwordErrors = _authSecurityService.ValidatePassword(request.Password);
        if (passwordErrors.Count > 0)
        {
            return BadRequestError(
                BuildPasswordPolicyMessage(passwordErrors),
                code: "validation_error"
            );
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable);

        var emailExists = await _context.Users
            .AnyAsync(u => u.Email == normalizedEmail);

        if (emailExists)
        {
            return ConflictError("El correo ya esta registrado.");
        }

        var existsAnyUser = await _context.Users.AnyAsync();

        var user = new User
        {
            Email = normalizedEmail,
            Name = request.Name.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = existsAnyUser ? "user" : "admin",
            IsActive = true,
            FailedLoginAttempts = 0,
            LockoutEndAt = null,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        var refreshToken = _refreshTokenService.CreateToken();

        try
        {
            await _context.SaveChangesAsync();

            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = refreshToken.TokenHash,
                ExpiresAt = refreshToken.ExpiresAt,
                CreatedAt = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueEmailViolation(ex))
        {
            return ConflictError("El correo ya esta registrado.");
        }

        return Ok(BuildAuthResponse(user, refreshToken.Token));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (request is null)
        {
            await WriteAuthAuditAsync(
                eventType: "login_failed",
                isSuccess: false,
                failureReason: "request_body_missing"
            );
            return BadRequestError("El cuerpo de la solicitud es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            await WriteAuthAuditAsync(
                eventType: "login_failed",
                isSuccess: false,
                email: request.Email,
                failureReason: "missing_credentials"
            );
            return BadRequestError(
                "Email y contrasena son obligatorios.",
                code: "validation_error"
            );
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null)
        {
            await WriteAuthAuditAsync(
                eventType: "login_failed",
                isSuccess: false,
                email: normalizedEmail,
                failureReason: "invalid_credentials"
            );
            return UnauthorizedError("Credenciales incorrectas.");
        }

        var nowUtc = DateTime.UtcNow;

        if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > nowUtc)
        {
            await WriteAuthAuditAsync(
                eventType: "login_failed",
                isSuccess: false,
                userId: user.Id,
                email: user.Email,
                failureReason: "account_locked"
            );

            return UnauthorizedError("Cuenta bloqueada temporalmente por intentos fallidos.");
        }

        if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value <= nowUtc)
        {
            user.LockoutEndAt = null;
            user.FailedLoginAttempts = 0;
        }

        var passwordIsValid = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash
        );

        if (!passwordIsValid)
        {
            user.FailedLoginAttempts += 1;

            var failureReason = "invalid_credentials";

            if (user.FailedLoginAttempts >= _authSecurityService.MaxFailedLoginAttempts)
            {
                user.LockoutEndAt = nowUtc.Add(_authSecurityService.LockoutDuration);
                user.FailedLoginAttempts = 0;
                failureReason = "account_locked_after_failures";
            }

            await _context.SaveChangesAsync();

            await WriteAuthAuditAsync(
                eventType: "login_failed",
                isSuccess: false,
                userId: user.Id,
                email: user.Email,
                failureReason: failureReason
            );

            if (failureReason == "account_locked_after_failures")
            {
                return UnauthorizedError("Cuenta bloqueada temporalmente por intentos fallidos.");
            }

            return UnauthorizedError("Credenciales incorrectas.");
        }

        if (!user.IsActive)
        {
            await WriteAuthAuditAsync(
                eventType: "login_failed",
                isSuccess: false,
                userId: user.Id,
                email: user.Email,
                failureReason: "inactive_user"
            );
            return UnauthorizedError("El usuario esta inactivo.");
        }

        var refreshToken = _refreshTokenService.CreateToken();
        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshToken.TokenHash,
            ExpiresAt = refreshToken.ExpiresAt,
            CreatedAt = DateTime.UtcNow
        };

        user.FailedLoginAttempts = 0;
        user.LockoutEndAt = null;

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();
        await WriteAuthAuditAsync(
            eventType: "login_success",
            isSuccess: true,
            userId: user.Id,
            email: user.Email
        );

        return Ok(BuildAuthResponse(user, refreshToken.Token));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await WriteAuthAuditAsync(
                eventType: "refresh_failed",
                isSuccess: false,
                failureReason: "missing_refresh_token"
            );
            return BadRequestError(
                "Refresh token es obligatorio.",
                code: "validation_error"
            );
        }

        var refreshTokenHash = _refreshTokenService.ComputeHash(request.RefreshToken.Trim());

        var existingToken = await _context.RefreshTokens
            .AsNoTracking()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == refreshTokenHash);

        if (existingToken is null || existingToken.User is null)
        {
            await WriteAuthAuditAsync(
                eventType: "refresh_failed",
                isSuccess: false,
                failureReason: "invalid_refresh_token"
            );
            return UnauthorizedError("Refresh token invalido.");
        }

        if (!existingToken.IsActive)
        {
            await WriteAuthAuditAsync(
                eventType: "refresh_failed",
                isSuccess: false,
                userId: existingToken.UserId,
                email: existingToken.User.Email,
                failureReason: "refresh_token_inactive"
            );
            return UnauthorizedError("Refresh token invalido o expirado.");
        }

        if (!existingToken.User.IsActive)
        {
            await WriteAuthAuditAsync(
                eventType: "refresh_failed",
                isSuccess: false,
                userId: existingToken.UserId,
                email: existingToken.User.Email,
                failureReason: "inactive_user"
            );
            return UnauthorizedError("El usuario esta inactivo.");
        }

        var newRefreshToken = _refreshTokenService.CreateToken();
        var nowUtc = DateTime.UtcNow;

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted);

        var revokedRows = await _context.RefreshTokens
            .Where(rt =>
                rt.Id == existingToken.Id &&
                rt.RevokedAt == null &&
                rt.ExpiresAt > nowUtc
            )
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(rt => rt.RevokedAt, nowUtc)
                .SetProperty(rt => rt.ReplacedByTokenHash, newRefreshToken.TokenHash)
            );

        if (revokedRows == 0)
        {
            await transaction.RollbackAsync();
            await WriteAuthAuditAsync(
                eventType: "refresh_failed",
                isSuccess: false,
                userId: existingToken.UserId,
                email: existingToken.User.Email,
                failureReason: "refresh_token_replayed_or_inactive"
            );
            return UnauthorizedError("Refresh token invalido o expirado.");
        }

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = existingToken.UserId,
            TokenHash = newRefreshToken.TokenHash,
            ExpiresAt = newRefreshToken.ExpiresAt,
            CreatedAt = nowUtc
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        await WriteAuthAuditAsync(
            eventType: "refresh_success",
            isSuccess: true,
            userId: existingToken.UserId,
            email: existingToken.User.Email
        );

        return Ok(BuildAuthResponse(existingToken.User, newRefreshToken.Token));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await WriteAuthAuditAsync(
                eventType: "logout_failed",
                isSuccess: false,
                failureReason: "missing_refresh_token"
            );
            return BadRequestError(
                "Refresh token es obligatorio.",
                code: "validation_error"
            );
        }

        var refreshTokenHash = _refreshTokenService.ComputeHash(request.RefreshToken.Trim());

        var existingToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == refreshTokenHash);

        if (existingToken is null)
        {
            await WriteAuthAuditAsync(
                eventType: "logout_failed",
                isSuccess: false,
                failureReason: "invalid_refresh_token"
            );
        }
        else if (existingToken.IsRevoked)
        {
            await WriteAuthAuditAsync(
                eventType: "logout_failed",
                isSuccess: false,
                userId: existingToken.UserId,
                failureReason: "refresh_token_already_revoked"
            );
        }
        else
        {
            existingToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await WriteAuthAuditAsync(
                eventType: "logout_success",
                isSuccess: true,
                userId: existingToken.UserId
            );
        }

        return Ok(new
        {
            success = true,
            message = "Sesion cerrada correctamente."
        });
    }

    private AuthResponseDto BuildAuthResponse(User user, string refreshToken)
    {
        var accessToken = _tokenService.CreateToken(user);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                Role = user.Role,
                AvatarUrl = BuildAvatarUrl(user.AvatarPath),
                IsActive = user.IsActive
            }
        };
    }

    private static bool IsUniqueEmailViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlException &&
               (sqlException.Number == 2601 || sqlException.Number == 2627);
    }

    private static string BuildPasswordPolicyMessage(
        IReadOnlyCollection<string> errors
    )
    {
        return errors.Count == 0
            ? "La contrasena no cumple la politica de seguridad."
            : string.Join(" ", errors);
    }

    private async Task WriteAuthAuditAsync(
        string eventType,
        bool isSuccess,
        Guid? userId = null,
        string? email = null,
        string? failureReason = null
    )
    {
        try
        {
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

            _context.AuthAuditLogs.Add(new AuthAuditLog
            {
                EventType = eventType,
                IsSuccess = isSuccess,
                UserId = userId,
                Email = NormalizeEmail(email),
                FailureReason = TrimToMaxLength(failureReason, 200),
                IpAddress = TrimToMaxLength(HttpContext.Connection.RemoteIpAddress?.ToString(), 45),
                UserAgent = TrimToMaxLength(userAgent, 512),
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No se pudo guardar el registro de auditoria. EventType: {EventType}",
                eventType
            );
        }
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return TrimToMaxLength(email.Trim().ToLowerInvariant(), 256);
    }

    private static string? TrimToMaxLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private string? BuildAvatarUrl(string? avatarPath)
    {
        if (string.IsNullOrWhiteSpace(avatarPath))
        {
            return null;
        }

        var normalizedPath = avatarPath.Replace('\\', '/').TrimStart('/');
        return $"{Request.Scheme}://{Request.Host}/uploads/{normalizedPath}";
    }
}
