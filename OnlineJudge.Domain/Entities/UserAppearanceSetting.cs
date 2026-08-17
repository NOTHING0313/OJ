namespace OnlineJudge.Domain.Entities;

public class UserAppearanceSetting
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public string? BackgroundImageUrl { get; set; }

    public bool BackgroundEnabled { get; set; }

    public decimal PositionX { get; set; } = 50m;

    public decimal PositionY { get; set; } = 50m;

    public decimal Scale { get; set; } = 1m;

    public decimal OverlayOpacity { get; set; } = 0.65m;

    public DateTimeOffset UpdatedAt { get; set; }
}
