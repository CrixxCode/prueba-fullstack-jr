using System.Security.Claims;
using backend.Data;
using backend.DTOs;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ApiControllerBase
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
    private readonly AuthSecurityService _authSecurityService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        AppDbContext context,
        AuthSecurityService authSecurityService,
        IWebHostEnvironment environment,
        ILogger<UsersController> logger
    )
    {
        _context = context;
        _authSecurityService = authSecurityService;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<UserListResponseDto>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int size = DefaultPageSize,
        [FromQuery] string? sortBy = "createdAt",
        [FromQuery] string? sortDir = "desc"
    )
    {
        if (page <= 0) page = 1;
        if (size <= 0) size = DefaultPageSize;
        if (size > MaxPageSize) size = MaxPageSize;

        var query = _context.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            var pattern = $"%{normalizedSearch}%";

            query = query.Where(u =>
                EF.Functions.Like(u.Email, pattern) ||
                EF.Functions.Like(u.Name, pattern)
            );
        }

        var totalItems = await query.CountAsync();

        var sortedQuery = ApplySorting(query, sortBy, sortDir);

        var users = await sortedQuery
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var response = new UserListResponseDto
        {
            Page = page,
            Size = size,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)size),
            Items = users.Select(BuildUserResponse).ToList()
        };

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponseDto>> GetUserById(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();

        if (currentUserRole != "admin" && currentUserId != id)
        {
            return ForbiddenError();
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFoundError("Usuario no encontrado.");
        }

        return Ok(BuildUserResponse(user));
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<UserResponseDto>> CreateUser([FromBody] CreateUserDto request)
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

        var role = request.Role?.Trim().ToLowerInvariant() ?? "user";

        if (role != "admin" && role != "user")
        {
            return BadRequestError(
                "El rol debe ser admin o user.",
                code: "validation_error"
            );
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var currentUserId = GetCurrentUserId();
        var actorUserId = NormalizeActorUserId(currentUserId);

        var emailExists = await _context.Users
            .AnyAsync(u => u.Email == normalizedEmail);

        if (emailExists)
        {
            return ConflictError("El correo ya esta registrado.");
        }

        var user = new User
        {
            Email = normalizedEmail,
            Name = request.Name.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            IsActive = request.IsActive,
            FailedLoginAttempts = 0,
            LockoutEndAt = null,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actorUserId,
            UpdatedBy = actorUserId
        };

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueEmailViolation(ex))
        {
            return ConflictError("El correo ya esta registrado.");
        }

        _logger.LogInformation(
            "Usuario creado. UserId: {UserId}, ActorUserId: {ActorUserId}, Role: {Role}",
            user.Id,
            actorUserId,
            user.Role
        );

        return CreatedAtAction(
            nameof(GetUserById),
            new { id = user.Id },
            BuildUserResponse(user)
        );
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponseDto>> UpdateUser(Guid id, [FromBody] UpdateUserDto request)
    {
        if (request is null)
        {
            return BadRequestError("El cuerpo de la solicitud es obligatorio.");
        }

        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();

        if (currentUserRole != "admin" && currentUserId != id)
        {
            return ForbiddenError();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFoundError("Usuario no encontrado.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            var emailInUse = await _context.Users
                .AnyAsync(u => u.Email == normalizedEmail && u.Id != user.Id);

            if (emailInUse)
            {
                return ConflictError("El correo ya esta registrado.");
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
                return BadRequestError(
                    BuildPasswordPolicyMessage(passwordErrors),
                    code: "validation_error"
                );
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        if (currentUserRole == "admin")
        {
            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                var role = request.Role.Trim().ToLowerInvariant();

                if (role != "admin" && role != "user")
                {
                    return BadRequestError(
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
        user.UpdatedBy = NormalizeActorUserId(currentUserId);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueEmailViolation(ex))
        {
            return ConflictError("El correo ya esta registrado.");
        }

        _logger.LogInformation(
            "Usuario actualizado. UserId: {UserId}, ActorUserId: {ActorUserId}, IsAdminActor: {IsAdminActor}",
            user.Id,
            NormalizeActorUserId(currentUserId),
            string.Equals(currentUserRole, "admin", StringComparison.OrdinalIgnoreCase)
        );

        return Ok(BuildUserResponse(user));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var currentUserId = GetCurrentUserId();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFoundError("Usuario no encontrado.");
        }

        var avatarPath = user.AvatarPath;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        DeleteAvatarFileIfExists(avatarPath);

        _logger.LogInformation(
            "Usuario eliminado. DeletedUserId: {DeletedUserId}, ActorUserId: {ActorUserId}",
            id,
            NormalizeActorUserId(currentUserId)
        );

        return Ok(new
        {
            message = "Usuario eliminado correctamente."
        });
    }

    [HttpPost("{id:guid}/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxAvatarSizeBytes + 1024)]
    public async Task<ActionResult<UserResponseDto>> UploadAvatar(
        Guid id,
        IFormFile? file
    )
    {
        if (file is null || file.Length == 0)
        {
            return BadRequestError(
                "Debes adjuntar una imagen de avatar.",
                code: "validation_error"
            );
        }

        if (file.Length > MaxAvatarSizeBytes)
        {
            return BadRequestError(
                "El avatar supera el tamano maximo permitido de 2 MB.",
                code: "validation_error"
            );
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !AllowedAvatarExtensions.Contains(extension))
        {
            return BadRequestError(
                "Formato de imagen no permitido. Usa JPG, PNG o WEBP.",
                code: "validation_error"
            );
        }

        var contentType = file.ContentType?.Trim();
        if (string.IsNullOrWhiteSpace(contentType) ||
            !AllowedAvatarContentTypes.Contains(contentType))
        {
            return BadRequestError(
                "Tipo MIME de imagen no valido. Usa image/jpeg, image/png o image/webp.",
                code: "validation_error"
            );
        }

        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();

        if (currentUserRole != "admin" && currentUserId != id)
        {
            return ForbiddenError();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFoundError("Usuario no encontrado.");
        }

        var avatarsDirectory = GetAvatarStoragePath();
        Directory.CreateDirectory(avatarsDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var relativeAvatarPath = Path.Combine("avatars", fileName).Replace('\\', '/');
        var avatarFullPath = Path.Combine(avatarsDirectory, fileName);

        await using (var stream = new FileStream(
            avatarFullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None
        ))
        {
            await file.CopyToAsync(stream);
        }

        var previousAvatarPath = user.AvatarPath;
        user.AvatarPath = relativeAvatarPath;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = NormalizeActorUserId(currentUserId);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            DeleteAvatarFileIfExists(relativeAvatarPath);
            throw;
        }

        DeleteAvatarFileIfExists(previousAvatarPath);

        _logger.LogInformation(
            "Avatar cargado. UserId: {UserId}, ActorUserId: {ActorUserId}, AvatarPath: {AvatarPath}",
            user.Id,
            NormalizeActorUserId(currentUserId),
            user.AvatarPath
        );

        return Ok(BuildUserResponse(user));
    }

    [HttpDelete("{id:guid}/avatar")]
    public async Task<ActionResult<UserResponseDto>> DeleteAvatar(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();

        if (currentUserRole != "admin" && currentUserId != id)
        {
            return ForbiddenError();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null)
        {
            return NotFoundError("Usuario no encontrado.");
        }

        var previousAvatarPath = user.AvatarPath;
        user.AvatarPath = null;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = NormalizeActorUserId(currentUserId);

        await _context.SaveChangesAsync();
        DeleteAvatarFileIfExists(previousAvatarPath);

        _logger.LogInformation(
            "Avatar eliminado. UserId: {UserId}, ActorUserId: {ActorUserId}",
            user.Id,
            NormalizeActorUserId(currentUserId)
        );

        return Ok(BuildUserResponse(user));
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

    private UserResponseDto BuildUserResponse(User user)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            Role = user.Role,
            AvatarUrl = BuildAvatarUrl(user.AvatarPath),
            IsActive = user.IsActive
        };
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

    private string GetAvatarStoragePath()
    {
        return Path.Combine(_environment.ContentRootPath, "uploads", "avatars");
    }

    private void DeleteAvatarFileIfExists(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var normalizedRelativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var fullPath = Path.Combine(_environment.ContentRootPath, "uploads", normalizedRelativePath);

        if (!System.IO.File.Exists(fullPath))
        {
            return;
        }

        try
        {
            System.IO.File.Delete(fullPath);
        }
        catch
        {
            // Eliminar avatar previo es best-effort.
        }
    }
}
