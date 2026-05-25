using backend.Services;
using backend.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace backend.Tests.Services;

public class AvatarStorageServiceTests
{
    [Fact]
    public async Task SaveAsync_WritesAvatarFile_AndReturnsRelativePath()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root);
            var file = CreateFormFile("photo.png", "image/png");

            var result = await service.SaveAsync(file, ".png");
            var fullPath = ResolveFullPath(root, result.RelativePath);

            Assert.StartsWith("avatars/", result.RelativePath);
            Assert.True(File.Exists(fullPath));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task DeleteIfExists_RemovesExistingAvatarFile()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root);
            var file = CreateFormFile("photo.webp", "image/webp");
            var saved = await service.SaveAsync(file, ".webp");
            var fullPath = ResolveFullPath(root, saved.RelativePath);

            service.DeleteIfExists(saved.RelativePath);

            Assert.False(File.Exists(fullPath));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void DeleteIfExists_WithMissingPath_DoesNotThrow()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root);

            var exception = Record.Exception(() => service.DeleteIfExists("avatars/missing.png"));

            Assert.Null(exception);
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static AvatarStorageService CreateService(string contentRootPath)
    {
        var environment = new TestWebHostEnvironment
        {
            ContentRootPath = contentRootPath
        };

        return new AvatarStorageService(environment);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType)
    {
        var bytes = new byte[] { 10, 20, 30 };
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static string ResolveFullPath(string root, string relativePath)
    {
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(root, "uploads", normalizedRelativePath);
    }

    private static string CreateTempRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"avatar-storage-tests-{Guid.NewGuid():N}");
    }

    private static void Cleanup(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

