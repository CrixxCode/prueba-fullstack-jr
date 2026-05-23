namespace backend.DTOs;

public class AuthAuditLogResponseDto
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public Guid? UserId { get; set; }

    public string? Email { get; set; }

    public string? FailureReason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }
}
