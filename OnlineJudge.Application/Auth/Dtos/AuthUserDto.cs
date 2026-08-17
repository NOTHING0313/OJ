using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Auth.Dtos;

public class AuthUserDto
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public UserRole Role { get; set; }

    public bool IsBlacklisted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
