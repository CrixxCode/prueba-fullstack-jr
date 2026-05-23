namespace backend.Models;

public class AuthAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string EventType { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    public Guid? UserId { get; set; }

    public User? User { get; set; }

    public string? Email { get; set; }

    public string? FailureReason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
