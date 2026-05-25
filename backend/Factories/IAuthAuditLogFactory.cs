using backend.Models;

namespace backend.Factories;

public interface IAuthAuditLogFactory
{
    AuthAuditLog Create(
        string eventType,
        bool isSuccess,
        Guid? userId,
        string? email,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        DateTime createdAtUtc
    );
}

