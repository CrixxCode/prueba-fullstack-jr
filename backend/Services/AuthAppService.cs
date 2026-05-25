using System.Data;
using backend.Data;
using backend.DTOs;
using backend.Factories;
using backend.Mappers;
using backend.Models;
using backend.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class AuthAppService : IAuthAppService
{
    private readonly AppDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly TokenService _tokenService;
    private readonly RefreshTokenService _refreshTokenService;
    private readonly AuthSecurityService _authSecurityService;
    private readonly IUserFactory _userFactory;
    private readonly IRefreshTokenFactory _refreshTokenFactory;
    private readonly IAuthAuditLogFactory _authAuditLogFactory;
    private readonly IAuthResponseMapper _authResponseMapper;
    private readonly ILogger<AuthAppService> _logger;

    public AuthAppService(
        AppDbContext context,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        TokenService tokenService,
        RefreshTokenService refreshTokenService,
        AuthSecurityService authSecurityService,
        IUserFactory userFactory,
        IRefreshTokenFactory refreshTokenFactory,
        IAuthAuditLogFactory authAuditLogFactory,
        IAuthResponseMapper authResponseMapper,
        ILogger<AuthAppService> logger
    )
    {
        _context = context;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _authSecurityService = authSecurityService;
        _userFactory = userFactory;
        _refreshTokenFactory = refreshTokenFactory;
        _authAuditLogFactory = authAuditLogFactory;
        _authResponseMapper = authResponseMapper;
        _logger = logger;
    }

    public async Task<AppServiceResult<AuthResponseDto>> RegisterAsync(
        RegisterRequestDto? request,
        AuthRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null)
        {
            return AppServiceResult<AuthResponseDto>.BadRequest(
                "El cuerpo de la solicitud es obligatorio."
            );
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Name))
        {
            return AppServiceResult<AuthResponseDto>.BadRequest(
                "Email, contrasena y nombre son obligatorios.",
                code: "validation_error"
            );
        }

        var passwordErrors = _authSecurityService.ValidatePassword(request.Password);
        if (passwordErrors.Count > 0)
        {
            return AppServiceResult<AuthResponseDto>.BadRequest(
                BuildPasswordPolicyMessage(passwordErrors),
                code: "validation_error"
            );
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var emailExists = await _userRepository.EmailExistsAsync(
            normalizedEmail,
            cancellationToken: cancellationToken
        );

        if (emailExists)
        {
            return AppServiceResult<AuthResponseDto>.Conflict(
                "El correo ya esta registrado."
            );
        }

        var existsAnyUser = await _userRepository.AnyAsync(cancellationToken);
        var userId = Guid.NewGuid();
        var nowUtc = DateTime.UtcNow;

        var user = _userFactory.CreateForRegistration(
            userId,
            normalizedEmail,
            request.Name.Trim(),
            BCrypt.Net.BCrypt.HashPassword(request.Password),
            existsAnyUser ? "user" : "admin",
            nowUtc
        );

        _userRepository.Add(user);
        var refreshToken = _refreshTokenService.CreateToken();

        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            var refreshTokenEntity = _refreshTokenFactory.Create(
                user.Id,
                refreshToken.TokenHash,
                refreshToken.ExpiresAt,
                DateTime.UtcNow
            );

            _refreshTokenRepository.Add(refreshTokenEntity);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueEmailViolation(ex))
        {
            return AppServiceResult<AuthResponseDto>.Conflict(
                "El correo ya esta registrado."
            );
        }

        _logger.LogInformation(
            "Usuario registrado exitosamente. UserId: {UserId}, Email: {Email}, Role: {Role}",
            user.Id,
            user.Email,
            user.Role
        );

        return AppServiceResult<AuthResponseDto>.Success(
            _authResponseMapper.Map(
                user,
                _tokenService.CreateToken(user),
                refreshToken.Token,
                requestContext.Scheme,
                requestContext.Host
            )
        );
    }

    public async Task<AppServiceResult<AuthResponseDto>> LoginAsync(
        LoginRequestDto? request,
        AuthRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null)
        {
            await WriteAuthAuditAsync(
                requestContext,
                eventType: "login_failed",
                isSuccess: false,
                failureReason: "request_body_missing",
                cancellationToken: cancellationToken
            );

            return AppServiceResult<AuthResponseDto>.BadRequest(
                "El cuerpo de la solicitud es obligatorio."
            );
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            await WriteAuthAuditAsync(
                requestContext,
                eventType: "login_failed",
                isSuccess: false,
                email: request.Email,
                failureReason: "missing_credentials",
                cancellationToken: cancellationToken
            );

            return AppServiceResult<AuthResponseDto>.BadRequest(
                "Email y contrasena son obligatorios.",
                code: "validation_error"
            );
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _userRepository.GetByEmailAsync(
            normalizedEmail,
            cancellationToken: cancellationToken
        );

        if (user == null)
        {
            await WriteAuthAuditAsync(
                requestContext,
                eventType: "login_failed",
                isSuccess: false,
                email: normalizedEmail,
                failureReason: "invalid_credentials",
                cancellationToken: cancellationToken
            );

            return AppServiceResult<AuthResponseDto>.Unauthorized(
                "Credenciales incorrectas."
            );
        }

        var nowUtc = DateTime.UtcNow;

        if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > nowUtc)
        {
            await WriteAuthAuditAsync(
                requestContext,
                eventType: "login_failed",
                isSuccess: false,
                userId: user.Id,
                email: user.Email,
                failureReason: "account_locked",
                cancellationToken: cancellationToken
            );

            return AppServiceResult<AuthResponseDto>.Unauthorized(
                "Cuenta bloqueada temporalmente por intentos fallidos."
            );
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

            user.UpdatedAt = nowUtc;
            user.UpdatedBy = user.Id;
            await _context.SaveChangesAsync(cancellationToken);

            await WriteAuthAuditAsync(
                requestContext,
                eventType: "login_failed",
                isSuccess: false,
                userId: user.Id,
                email: user.Email,
                failureReason: failureReason,
                cancellationToken: cancellationToken
            );

            if (failureReason == "account_locked_after_failures")
            {
                return AppServiceResult<AuthResponseDto>.Unauthorized(
                    "Cuenta bloqueada temporalmente por intentos fallidos."
                );
            }

            return AppServiceResult<AuthResponseDto>.Unauthorized(
                "Credenciales incorrectas."
            );
        }

        if (!user.IsActive)
        {
            await WriteAuthAuditAsync(
                requestContext,
                eventType: "login_failed",
                isSuccess: false,
                userId: user.Id,
                email: user.Email,
                failureReason: "inactive_user",
                cancellationToken: cancellationToken
            );

            return AppServiceResult<AuthResponseDto>.Unauthorized(
                "El usuario esta inactivo."
            );
        }

        var refreshToken = _refreshTokenService.CreateToken();
        var refreshTokenEntity = _refreshTokenFactory.Create(
            user.Id,
            refreshToken.TokenHash,
            refreshToken.ExpiresAt,
            DateTime.UtcNow
        );

        user.FailedLoginAttempts = 0;
        user.LockoutEndAt = null;
        user.UpdatedAt = nowUtc;
        user.UpdatedBy = user.Id;

        _refreshTokenRepository.Add(refreshTokenEntity);
        await _context.SaveChangesAsync(cancellationToken);

        await WriteAuthAuditAsync(
            requestContext,
            eventType: "login_success",
            isSuccess: true,
            userId: user.Id,
            email: user.Email,
            cancellationToken: cancellationToken
        );

        _logger.LogInformation(
            "Inicio de sesion exitoso. UserId: {UserId}, Email: {Email}",
            user.Id,
            user.Email
        );

        return AppServiceResult<AuthResponseDto>.Success(
            _authResponseMapper.Map(
                user,
                _tokenService.CreateToken(user),
                refreshToken.Token,
                requestContext.Scheme,
                requestContext.Host
            )
        );
    }

    public async Task<AppServiceResult<AuthResponseDto>> RefreshAsync(
        RefreshTokenRequestDto? request,
        AuthRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await WriteAuthAuditAsync(
                requestContext,
                eventType: "refresh_failed",
                isSuccess: false,
                failureReason: "missing_refresh_token",
                cancellationToken: cancellationToken
            );

            return AppServiceResult<AuthResponseDto>.BadRequest(
                "Refresh token es obligatorio.",
                code: "validation_error"
            );
        }

        var refreshTokenHash = _refreshTokenService.ComputeHash(request.RefreshToken.Trim());

        var existingToken = await _refreshTokenRepository.GetByHashAsync(
            refreshTokenHash,
            includeUser: true,
            asNoTracking: true,
            cancellationToken: cancellationToken
        );

        if (existingToken is null || existingToken.User is null)
        {
            await WriteAuthAuditAsync(
                requestContext,
                eventType: "refresh_failed",
                isSuccess: false,
                failureReason: "invalid_refresh_token",
                cancellationToken: cancellationToken
            );

            return AppServiceResult<AuthResponseDto>.Unauthorized(
                "Refresh token invalido."
            );
        }

        if (!existingToken.IsActive)
        {
            await WriteAuthAuditAsync(
                requestContext,
                eventType: "refresh_failed",
                isSuccess: false,
                userId: existingToken.UserId,
                email: existingToken.User.Email,
                failureReason: "refresh_token_inactive",
                cancellationToken: cancellationToken
            );

            return AppServiceResult<AuthResponseDto>.Unauthorized(
                "Refresh token invalido o expirado."
            );
        }

        if (!existingToken.User.IsActive)
        {
            await WriteAuthAuditAsync(
                requestContext,
                eventType: "refresh_failed",
                isSuccess: false,
                userId: existingToken.UserId,
                email: existingToken.User.Email,
                failureReason: "inactive_user",
                cancellationToken: cancellationToken
            );

            return AppServiceResult<AuthResponseDto>.Unauthorized(
                "El usuario esta inactivo."
            );
        }

        var newRefreshToken = _refreshTokenService.CreateToken();
        var nowUtc = DateTime.UtcNow;

        await using var transaction = await _context.Database
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var revokedRows = await _refreshTokenRepository.RevokeActiveAndSetReplacementAsync(
            existingToken.Id,
            nowUtc,
            newRefreshToken.TokenHash,
            cancellationToken
        );

        if (revokedRows == 0)
        {
            await transaction.RollbackAsync(cancellationToken);

            await WriteAuthAuditAsync(
                requestContext,
                eventType: "refresh_failed",
                isSuccess: false,
                userId: existingToken.UserId,
                email: existingToken.User.Email,
                failureReason: "refresh_token_replayed_or_inactive",
                cancellationToken: cancellationToken
            );

            return AppServiceResult<AuthResponseDto>.Unauthorized(
                "Refresh token invalido o expirado."
            );
        }

        _refreshTokenRepository.Add(_refreshTokenFactory.Create(
            existingToken.UserId,
            newRefreshToken.TokenHash,
            newRefreshToken.ExpiresAt,
            nowUtc
        ));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await WriteAuthAuditAsync(
            requestContext,
            eventType: "refresh_success",
            isSuccess: true,
            userId: existingToken.UserId,
            email: existingToken.User.Email,
            cancellationToken: cancellationToken
        );

        return AppServiceResult<AuthResponseDto>.Success(
            _authResponseMapper.Map(
                existingToken.User,
                _tokenService.CreateToken(existingToken.User),
                newRefreshToken.Token,
                requestContext.Scheme,
                requestContext.Host
            )
        );
    }

    public async Task<AppServiceResult<MessageResponseDto>> LogoutAsync(
        LogoutRequestDto? request,
        AuthRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await WriteAuthAuditAsync(
                requestContext,
                eventType: "logout_failed",
                isSuccess: false,
                failureReason: "missing_refresh_token",
                cancellationToken: cancellationToken
            );

            return AppServiceResult<MessageResponseDto>.BadRequest(
                "Refresh token es obligatorio.",
                code: "validation_error"
            );
        }

        var refreshTokenHash = _refreshTokenService.ComputeHash(request.RefreshToken.Trim());
        var existingToken = await _refreshTokenRepository.GetByHashAsync(
            refreshTokenHash,
            cancellationToken: cancellationToken
        );

        if (existingToken is null)
        {
            await WriteAuthAuditAsync(
                requestContext,
                eventType: "logout_failed",
                isSuccess: false,
                failureReason: "invalid_refresh_token",
                cancellationToken: cancellationToken
            );
        }
        else if (existingToken.IsRevoked)
        {
            await WriteAuthAuditAsync(
                requestContext,
                eventType: "logout_failed",
                isSuccess: false,
                userId: existingToken.UserId,
                failureReason: "refresh_token_already_revoked",
                cancellationToken: cancellationToken
            );
        }
        else
        {
            existingToken.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await WriteAuthAuditAsync(
                requestContext,
                eventType: "logout_success",
                isSuccess: true,
                userId: existingToken.UserId,
                cancellationToken: cancellationToken
            );
        }

        return AppServiceResult<MessageResponseDto>.Success(
            new MessageResponseDto
            {
                Message = "Sesion cerrada correctamente."
            }
        );
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
        AuthRequestContext requestContext,
        string eventType,
        bool isSuccess,
        Guid? userId = null,
        string? email = null,
        string? failureReason = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _context.AuthAuditLogs.Add(_authAuditLogFactory.Create(
                eventType: eventType,
                isSuccess: isSuccess,
                userId: userId,
                email: NormalizeEmail(email),
                failureReason: TrimToMaxLength(failureReason, 200),
                ipAddress: TrimToMaxLength(requestContext.IpAddress, 45),
                userAgent: TrimToMaxLength(requestContext.UserAgent, 512),
                createdAtUtc: DateTime.UtcNow
            ));

            await _context.SaveChangesAsync(cancellationToken);
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

}

public sealed record AuthRequestContext(
    string Scheme,
    string Host,
    string? IpAddress,
    string? UserAgent
);
