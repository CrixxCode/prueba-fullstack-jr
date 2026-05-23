namespace backend.DTOs;

public class AuthAuditLogListResponseDto
{
    public int Page { get; set; }

    public int Size { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }

    public IReadOnlyCollection<AuthAuditLogResponseDto> Items { get; set; }
        = Array.Empty<AuthAuditLogResponseDto>();
}
