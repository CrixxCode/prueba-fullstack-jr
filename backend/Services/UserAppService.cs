using backend.Data;
using backend.DTOs;
using backend.Factories;
using backend.Mappers;
using backend.Models;
using backend.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class UserAppService : IUserAppService
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;
    private const long MaxAvatarSizeBytes = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedAvatarExtensions = new(
        [".jpg", ".jpeg", ".png", ".webp"],
        StringComparer.OrdinalIgnoreCase
    );

    private static readonly HashSet<string> AllowedAvatarContentTypes = new(
        ["image/jpeg", "image/png", "image/webp"],
        StringComparer.OrdinalIgnoreCase
    );

    private readonly AppDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IUserFactory _userFactory;
    private readonly IUserResponseMapper _userResponseMapper;
    private readonly IAvatarStorageService _avatarStorageService;
    private readonly AuthSecurityService _authSecurityService;
    private readonly ILogger<UserAppService> _logger;

    public UserAppService(
        AppDbContext context,
        IUserRepository userRepository,
        IUserFactory userFactory,
        IUserResponseMapper userResponseMapper,
        IAvatarStorageService avatarStorageService,
        AuthSecurityService authSecurityService,
        ILogger<UserAppService> logger
    )
    {
        _context = context;
        _userRepository = userRepository;
        _userFactory = userFactory;
        _userResponseMapper = userResponseMapper;
        _avatarStorageService = avatarStorageService;
        _authSecurityService = authSecurityService;
        _logger = logger;
    }

    public async Task<AppServiceResult<UserListResponseDto>> GetUsersAsync(
        string? search,
        int page,
        int size,
        string? sortBy,
        string? sortDir,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsAdmin(requestContext.CurrentUserRole))
        {
            return AppServiceResult<UserListResponseDto>.Forbidden();
        }

        if (page <= 0) page = 1;
        if (size <= 0) size = DefaultPageSize;
        if (size > MaxPageSize) size = MaxPageSize;

        var query = _userRepository.Query(asNoTracking: true);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            var pattern = $"%{normalizedSearch}%";

            query = query.Where(u =>
                EF.Functions.Like(u.Email, pattern) ||
                EF.Functions.Like(u.Name, pattern)
            );
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var sortedQuery = ApplySorting(query, sortBy, sortDir);

        var users = await sortedQuery
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return AppServiceResult<UserListResponseDto>.Success(new UserListResponseDto
        {
            Page = page,
            Size = size,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)size),
            Items = _userResponseMapper.MapMany(
                users,
                requestContext.Scheme,
                requestContext.Host
            )
        });
    }

    public async Task<AppServiceResult<UserResponseDto>> GetUserByIdAsync(
        Guid id,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsAdmin(requestContext.CurrentUserRole) && requestContext.CurrentUserId != id)
        {
            return AppServiceResult<UserResponseDto>.Forbidden();
        }

        var user = await _userRepository.GetByIdAsync(
            id,
            asNoTracking: true,
            cancellationToken: cancellationToken
        );

        if (user == null)
        {
            return AppServiceResult<UserResponseDto>.NotFound("Usuario no encontrado.");
        }

        return AppServiceResult<UserResponseDto>.Success(
            _userResponseMapper.Map(user, requestContext.Scheme, requestContext.Host)
        );
    }

    public async Task<AppServiceResult<UserResponseDto>> CreateUserAsync(
        CreateUserDto? request,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null)
        {
            return AppServiceResult<UserResponseDto>.BadRequest(
                "El cuerpo de la solicitud es obligatorio."
            );
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Name))
        {
            return AppServiceResult<UserResponseDto>.BadRequest(
                "Email, contrasena y nombre son obligatorios.",
                code: "validation_error"
            );
        }

        var passwordErrors = _authSecurityService.ValidatePassword(request.Password);
        if (passwordErrors.Count > 0)
        {
            return AppServiceResult<UserResponseDto>.BadRequest(
                BuildPasswordPolicyMessage(passwordErrors),
                code: "validation_error"
            );
        }

        var role = request.Role?.Trim().ToLowerInvariant() ?? "user";
        if (role != "admin" && role != "user")
        {
            return AppServiceResult<UserResponseDto>.BadRequest(
                "El rol debe ser admin o user.",
                code: "validation_error"
            );
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var actorUserId = NormalizeActorUserId(requestContext.CurrentUserId);
        var emailExists = await _userRepository.EmailExistsAsync(
            normalizedEmail,
            cancellationToken: cancellationToken
        );

        if (emailExists)
        {
            return AppServiceResult<UserResponseDto>.Conflict("El correo ya esta registrado.");
        }

        var user = _userFactory.CreateByAdmin(
            normalizedEmail: normalizedEmail,
            name: request.Name.Trim(),
            passwordHash: BCrypt.Net.BCrypt.HashPassword(request.Password),
            role: role,
            isActive: request.IsActive,
            actorUserId: actorUserId,
            createdAtUtc: DateTime.UtcNow
        );

        _userRepository.Add(user);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueEmailViolation(ex))
        {
            return AppServiceResult<UserResponseDto>.Conflict("El correo ya esta registrado.");
        }

        _logger.LogInformation(
            "Usuario creado. UserId: {UserId}, ActorUserId: {ActorUserId}, Role: {Role}",
            user.Id,
            actorUserId,
            user.Role
        );

        return AppServiceResult<UserResponseDto>.Success(
            _userResponseMapper.Map(user, requestContext.Scheme, requestContext.Host)
        );
    }

    public async Task<AppServiceResult<UserResponseDto>> UpdateUserAsync(
        Guid id,
        UpdateUserDto? request,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (request is null)
        {
            return AppServiceResult<UserResponseDto>.BadRequest(
                "El cuerpo de la solicitud es obligatorio."
            );
        }

        if (!IsAdmin(requestContext.CurrentUserRole) && requestContext.CurrentUserId != id)
        {
            return AppServiceResult<UserResponseDto>.Forbidden();
        }

        var user = await _userRepository.GetByIdAsync(id, cancellationToken: cancellationToken);
        if (user == null)
        {
            return AppServiceResult<UserResponseDto>.NotFound("Usuario no encontrado.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var emailInUse = await _userRepository.EmailExistsAsync(
                normalizedEmail,
                excludingUserId: user.Id,
                cancellationToken: cancellationToken
            );

            if (emailInUse)
            {
                return AppServiceResult<UserResponseDto>.Conflict("El correo ya esta registrado.");
            }

            user.Email = normalizedEmail;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            user.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            var passwordErrors = _authSecurityService.ValidatePassword(request.Password);
            if (passwordErrors.Count > 0)
            {
                return AppServiceResult<UserResponseDto>.BadRequest(
                    BuildPasswordPolicyMessage(passwordErrors),
                    code: "validation_error"
                );
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        if (IsAdmin(requestContext.CurrentUserRole))
        {
            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                var role = request.Role.Trim().ToLowerInvariant();
                if (role != "admin" && role != "user")
                {
                    return AppServiceResult<UserResponseDto>.BadRequest(
                        "El rol debe ser admin o user.",
                        code: "validation_error"
                    );
                }

                user.Role = role;
            }

            if (request.IsActive.HasValue)
            {
                user.IsActive = request.IsActive.Value;
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = NormalizeActorUserId(requestContext.CurrentUserId);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueEmailViolation(ex))
        {
            return AppServiceResult<UserResponseDto>.Conflict("El correo ya esta registrado.");
        }

        _logger.LogInformation(
            "Usuario actualizado. UserId: {UserId}, ActorUserId: {ActorUserId}, IsAdminActor: {IsAdminActor}",
            user.Id,
            NormalizeActorUserId(requestContext.CurrentUserId),
            IsAdmin(requestContext.CurrentUserRole)
        );

        return AppServiceResult<UserResponseDto>.Success(
            _userResponseMapper.Map(user, requestContext.Scheme, requestContext.Host)
        );
    }

    public async Task<AppServiceResult<MessageResponseDto>> DeleteUserAsync(
        Guid id,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsAdmin(requestContext.CurrentUserRole))
        {
            return AppServiceResult<MessageResponseDto>.Forbidden();
        }

        var user = await _userRepository.GetByIdAsync(id, cancellationToken: cancellationToken);
        if (user == null)
        {
            return AppServiceResult<MessageResponseDto>.NotFound("Usuario no encontrado.");
        }

        var avatarPath = user.AvatarPath;
        _userRepository.Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
        _avatarStorageService.DeleteIfExists(avatarPath);

        _logger.LogInformation(
            "Usuario eliminado. DeletedUserId: {DeletedUserId}, ActorUserId: {ActorUserId}",
            id,
            NormalizeActorUserId(requestContext.CurrentUserId)
        );

        return AppServiceResult<MessageResponseDto>.Success(new MessageResponseDto
        {
            Message = "Usuario eliminado correctamente."
        });
    }

    public async Task<AppServiceResult<UserResponseDto>> UploadAvatarAsync(
        Guid id,
        IFormFile? file,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (file is null || file.Length == 0)
        {
            return AppServiceResult<UserResponseDto>.BadRequest(
                "Debes adjuntar una imagen de avatar.",
                code: "validation_error"
            );
        }

        if (file.Length > MaxAvatarSizeBytes)
        {
            return AppServiceResult<UserResponseDto>.BadRequest(
                "El avatar supera el tamano maximo permitido de 2 MB.",
                code: "validation_error"
            );
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedAvatarExtensions.Contains(extension))
        {
            return AppServiceResult<UserResponseDto>.BadRequest(
                "Formato de imagen no permitido. Usa JPG, PNG o WEBP.",
                code: "validation_error"
            );
        }

        var contentType = file.ContentType?.Trim();
        if (string.IsNullOrWhiteSpace(contentType) ||
            !AllowedAvatarContentTypes.Contains(contentType))
        {
            return AppServiceResult<UserResponseDto>.BadRequest(
                "Tipo MIME de imagen no valido. Usa image/jpeg, image/png o image/webp.",
                code: "validation_error"
            );
        }

        if (!IsAdmin(requestContext.CurrentUserRole) && requestContext.CurrentUserId != id)
        {
            return AppServiceResult<UserResponseDto>.Forbidden();
        }

        var user = await _userRepository.GetByIdAsync(id, cancellationToken: cancellationToken);
        if (user == null)
        {
            return AppServiceResult<UserResponseDto>.NotFound("Usuario no encontrado.");
        }

        var savedAvatar = await _avatarStorageService.SaveAsync(file, extension, cancellationToken);
        var previousAvatarPath = user.AvatarPath;

        user.AvatarPath = savedAvatar.RelativePath;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = NormalizeActorUserId(requestContext.CurrentUserId);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            _avatarStorageService.DeleteIfExists(savedAvatar.RelativePath);
            throw;
        }

        _avatarStorageService.DeleteIfExists(previousAvatarPath);

        _logger.LogInformation(
            "Avatar cargado. UserId: {UserId}, ActorUserId: {ActorUserId}, AvatarPath: {AvatarPath}",
            user.Id,
            NormalizeActorUserId(requestContext.CurrentUserId),
            user.AvatarPath
        );

        return AppServiceResult<UserResponseDto>.Success(
            _userResponseMapper.Map(user, requestContext.Scheme, requestContext.Host)
        );
    }

    public async Task<AppServiceResult<UserResponseDto>> DeleteAvatarAsync(
        Guid id,
        UserRequestContext requestContext,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsAdmin(requestContext.CurrentUserRole) && requestContext.CurrentUserId != id)
        {
            return AppServiceResult<UserResponseDto>.Forbidden();
        }

        var user = await _userRepository.GetByIdAsync(id, cancellationToken: cancellationToken);
        if (user == null)
        {
            return AppServiceResult<UserResponseDto>.NotFound("Usuario no encontrado.");
        }

        var previousAvatarPath = user.AvatarPath;
        user.AvatarPath = null;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = NormalizeActorUserId(requestContext.CurrentUserId);

        await _context.SaveChangesAsync(cancellationToken);
        _avatarStorageService.DeleteIfExists(previousAvatarPath);

        _logger.LogInformation(
            "Avatar eliminado. UserId: {UserId}, ActorUserId: {ActorUserId}",
            user.Id,
            NormalizeActorUserId(requestContext.CurrentUserId)
        );

        return AppServiceResult<UserResponseDto>.Success(
            _userResponseMapper.Map(user, requestContext.Scheme, requestContext.Host)
        );
    }

    private static bool IsAdmin(string role)
    {
        return string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUniqueEmailViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlException &&
               (sqlException.Number == 2601 || sqlException.Number == 2627);
    }

    private static Guid? NormalizeActorUserId(Guid currentUserId)
    {
        return currentUserId == Guid.Empty ? null : currentUserId;
    }

    private static string BuildPasswordPolicyMessage(
        IReadOnlyCollection<string> errors
    )
    {
        return errors.Count == 0
            ? "La contrasena no cumple la politica de seguridad."
            : string.Join(" ", errors);
    }

    private static IOrderedQueryable<User> ApplySorting(
        IQueryable<User> query,
        string? sortBy,
        string? sortDir
    )
    {
        var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy)
            ? "createdat"
            : sortBy.Trim().ToLowerInvariant();

        var isDescending = !string.Equals(
            sortDir?.Trim(),
            "asc",
            StringComparison.OrdinalIgnoreCase
        );

        return normalizedSortBy switch
        {
            "email" => isDescending
                ? query.OrderByDescending(u => u.Email).ThenByDescending(u => u.Id)
                : query.OrderBy(u => u.Email).ThenBy(u => u.Id),
            "name" => isDescending
                ? query.OrderByDescending(u => u.Name).ThenByDescending(u => u.Id)
                : query.OrderBy(u => u.Name).ThenBy(u => u.Id),
            "role" => isDescending
                ? query.OrderByDescending(u => u.Role).ThenByDescending(u => u.Id)
                : query.OrderBy(u => u.Role).ThenBy(u => u.Id),
            "isactive" => isDescending
                ? query.OrderByDescending(u => u.IsActive).ThenByDescending(u => u.Id)
                : query.OrderBy(u => u.IsActive).ThenBy(u => u.Id),
            "created_at" or "createdat" => isDescending
                ? query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
                : query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id),
            _ => isDescending
                ? query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
                : query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
        };
    }
}
