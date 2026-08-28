using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Account.Dtos;

public class AccountUserDto
{
    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public UserRole Role { get; set; }

    public bool IsBlacklisted { get; set; }

    public bool IsLeaderboardAnonymous { get; set; }

    public string? PhoneNumberMasked { get; set; }

    public bool PhoneNumberConfirmed { get; set; }
}
