using Microsoft.AspNetCore.Http;

namespace backend.Services;

public sealed class AvatarStorageService : IAvatarStorageService
{
    private readonly IWebHostEnvironment _environment;

    public AvatarStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<AvatarFileSaveResult> SaveAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken = default
    )
    {
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
            await file.CopyToAsync(stream, cancellationToken);
        }

        return new AvatarFileSaveResult(relativeAvatarPath);
    }

    public void DeleteIfExists(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var normalizedRelativePath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

        var fullPath = Path.Combine(_environment.ContentRootPath, "uploads", normalizedRelativePath);

        if (!File.Exists(fullPath))
        {
            return;
        }

        try
        {
            File.Delete(fullPath);
        }
        catch
        {
            // Eliminar avatar es best-effort.
        }
    }

    private string GetAvatarStoragePath()
    {
        return Path.Combine(_environment.ContentRootPath, "uploads", "avatars");
    }
}

