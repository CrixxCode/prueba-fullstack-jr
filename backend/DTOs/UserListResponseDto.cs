namespace backend.DTOs;

public class UserListResponseDto
{
    public int Page { get; set; }

    public int Size { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }

    public List<UserResponseDto> Items { get; set; } = new();
}