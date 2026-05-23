namespace backend.DTOs;

public class AuthAuditQueryDto
{
    public string? EventType { get; set; }

    public bool? IsSuccess { get; set; }

    public Guid? UserId { get; set; }

    public string? Email { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    public string? SortBy { get; set; } = "createdAt";

    public string? SortDir { get; set; } = "desc";

    public int Page { get; set; } = 1;

    public int Size { get; set; } = 20;
}
