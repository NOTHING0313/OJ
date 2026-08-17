using Microsoft.EntityFrameworkCore;
using OnlineJudge.Application.SiteSettings.Dtos;
using OnlineJudge.Application.SiteSettings.Requests;
using OnlineJudge.Domain.Entities;
using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Persistence;
using OnlineJudge.Infrastructure.SiteSettings;

namespace OnlineJudge.Tests.SiteSettings;

public class SiteSettingsServiceTests
{
    [Fact]
    public async Task MissingAppearance_ReturnsDefault()
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);

        var result = await service.GetAppearanceAsync();

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Theme.BackgroundEnabled);
        Assert.Equal(0.65, result.Value.Theme.BackgroundOverlayOpacity);
        Assert.Equal(0.72, result.Value.Theme.PanelOpacity);
        Assert.Equal(12, result.Value.Theme.PanelBlur);
        Assert.Contains("global", result.Value.Pages.Keys);
        Assert.Contains("file-task", result.Value.Pages.Keys);
    }

    [Fact]
    public async Task Root_CanUpdateMultiPageAppearance()
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);
        var userId = Guid.NewGuid();

        var result = await service.UpdateAppearanceAsync(CreateRequest(pageImageUrl: "/uploads/images/problems.png"), userId, UserRole.Root);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Theme.BackgroundEnabled);
        Assert.Equal(0.5, result.Value.Theme.BackgroundOverlayOpacity);
        Assert.Equal(0.7, result.Value.Theme.PanelOpacity);
        Assert.Equal(14, result.Value.Theme.PanelBlur);
        Assert.Equal("/uploads/images/problems.png", result.Value.Pages["problems"].ImageUrl);
        Assert.True(result.Value.Pages["problems"].Enabled);
        Assert.Equal(1.2, result.Value.Pages["problems"].Scale);

        var setting = await dbContext.SiteSettings.SingleAsync();
        Assert.Equal("appearance", setting.Key);
        Assert.Equal(userId, setting.UpdatedByUserId);
    }

    [Theory]
    [InlineData(UserRole.Answerer)]
    [InlineData(UserRole.ProblemSetter)]
    public async Task NonRoot_CannotUpdateSiteAppearance(UserRole role)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);

        var result = await service.UpdateAppearanceAsync(CreateRequest(), Guid.NewGuid(), role);

        Assert.True(result.IsFailure);
        Assert.Equal("Forbidden.", result.ErrorMessage);
        Assert.Empty(dbContext.SiteSettings);
    }

    [Theory]
    [InlineData("https://example.com/background.png")]
    [InlineData("data:image/png;base64,abc")]
    [InlineData("/uploads/images/../secret.png")]
    [InlineData("/other/background.png")]
    public async Task UpdateSiteAppearance_RejectsUnsafeUrl(string url)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);

        var result = await service.UpdateAppearanceAsync(CreateRequest(pageImageUrl: url), Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Equal("Background image URL must point to uploaded images.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("http://localhost:5101/uploads/images/background.png")]
    [InlineData("http://127.0.0.1:5101/uploads/images/background.png")]
    [InlineData("https://oj.local/uploads/images/background.png")]
    public async Task LocalUploadUrl_NormalizedToRelativePath(string url)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);

        var result = await service.UpdateAppearanceAsync(CreateRequest(pageImageUrl: url), Guid.NewGuid(), UserRole.Root, "oj.local");

        Assert.True(result.IsSuccess);
        Assert.Equal("/uploads/images/background.png", result.Value!.Pages["problems"].ImageUrl);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public async Task UpdateSiteAppearance_RejectsInvalidOpacity(double opacity)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);
        var request = CreateRequest();
        request.Theme.BackgroundOverlayOpacity = opacity;

        var result = await service.UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Equal("Background overlay opacity must be between 0 and 1.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(0.34)]
    [InlineData(0.96)]
    public async Task UpdateSiteAppearance_RejectsInvalidPanelOpacity(double opacity)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);
        var request = CreateRequest();
        request.Theme.PanelOpacity = opacity;

        var result = await service.UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Equal("Panel opacity must be between 0.35 and 0.95.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(0.49)]
    [InlineData(2.51)]
    public async Task UpdateSiteAppearance_RejectsInvalidScale(double scale)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);
        var request = CreateRequest();
        request.Pages["problems"].Scale = scale;

        var result = await service.UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Equal("Background scale for problems must be between 0.5 and 2.5.", result.ErrorMessage);
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(101, 50)]
    [InlineData(50, -1)]
    [InlineData(50, 101)]
    public async Task UpdateSiteAppearance_RejectsInvalidPosition(double positionX, double positionY)
    {
        await using var dbContext = CreateDbContext();
        var service = new SiteSettingsService(dbContext);
        var request = CreateRequest();
        request.Pages["problems"].PositionX = positionX;
        request.Pages["problems"].PositionY = positionY;

        var result = await service.UpdateAppearanceAsync(request, Guid.NewGuid(), UserRole.Root);

        Assert.True(result.IsFailure);
        Assert.Contains("must be between 0 and 100.", result.ErrorMessage);
    }

    [Fact]
    public async Task OldAppearanceJson_CanBeReadWithoutThrowing()
    {
        await using var dbContext = CreateDbContext();
        dbContext.SiteSettings.Add(new SiteSetting
        {
            Id = Guid.NewGuid(),
            Key = "appearance",
            Value = "{\"backgroundImageUrl\":\"/uploads/images/old.png\",\"backgroundOverlayOpacity\":0.4,\"backgroundEnabled\":true}",
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var service = new SiteSettingsService(dbContext);

        var result = await service.GetAppearanceAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Theme.BackgroundEnabled);
        Assert.Equal(0.4, result.Value.Theme.BackgroundOverlayOpacity);
        Assert.True(result.Value.Pages["global"].Enabled);
        Assert.Equal("/uploads/images/old.png", result.Value.Pages["global"].ImageUrl);
    }

    private static UpdateSiteAppearanceRequest CreateRequest(string pageImageUrl = "/uploads/images/background.png")
    {
        return new UpdateSiteAppearanceRequest
        {
            Theme = new SiteAppearanceThemeDto
            {
                BackgroundEnabled = true,
                BackgroundOverlayOpacity = 0.5,
                PanelOpacity = 0.7,
                PanelBlur = 14
            },
            Pages = new Dictionary<string, SitePageBackgroundDto>
            {
                ["global"] = new()
                {
                    Enabled = false,
                    ImageUrl = null,
                    PositionX = 50,
                    PositionY = 50,
                    Scale = 1
                },
                ["problems"] = new()
                {
                    Enabled = true,
                    ImageUrl = pageImageUrl,
                    PositionX = 52,
                    PositionY = 46,
                    Scale = 1.2
                }
            }
        };
    }

    private static OnlineJudgeDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OnlineJudgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OnlineJudgeDbContext(options);
    }
}
