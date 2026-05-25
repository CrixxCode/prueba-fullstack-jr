using Microsoft.AspNetCore.Http;

namespace backend.Services;

public interface IAvatarStorageService
{
    Task<AvatarFileSaveResult> SaveAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken = default
    );

    void DeleteIfExists(string? relativePath);
}

public sealed record AvatarFileSaveResult(string RelativePath);

