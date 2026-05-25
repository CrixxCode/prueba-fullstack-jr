using backend.Models;

namespace backend.Factories;

public sealed class AuthAuditLogFactory : IAuthAuditLogFactory
{
    public AuthAuditLog Create(
        string eventType,
        bool isSuccess,
        Guid? userId,
        string? email,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        DateTime createdAtUtc
    )
    {
        return new AuthAuditLog
        {
            EventType = eventType,
            IsSuccess = isSuccess,
            UserId = userId,
            Email = email,
            FailureReason = failureReason,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = createdAtUtc
        };
    }
}

