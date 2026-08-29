namespace OnlineJudge.Tests.SiteSettings;

public sealed class GlobalAppearanceCoverageTests
{
    [Theory]
    [InlineData("--oj-page-bg:")]
    [InlineData("--oj-panel-bg:")]
    [InlineData("--oj-elevated-bg:")]
    [InlineData("--oj-panel-border:")]
    [InlineData("--oj-text-primary:")]
    [InlineData("--oj-text-muted:")]
    [InlineData("--oj-accent:")]
    [InlineData("--oj-hover-bg:")]
    [InlineData("--oj-accent-soft:")]
    [InlineData("--oj-success:")]
    [InlineData("--oj-warning:")]
    [InlineData("--oj-danger:")]
    [InlineData("--oj-input-bg:")]
    [InlineData("--oj-code-bg:")]
    public void SemanticTokenInventory_HasCssFallback(string token)
    {
        Assert.Contains(token, Styles(), StringComparison.Ordinal);
    }

    [Fact]
    public void TeamChat_UsesSemanticOwnOtherSystemAndAnnouncementTokens()
    {
        var coverage = CoverageStyles();

        Assert.Contains(".team-chat-message.other .team-chat-message-row p", coverage, StringComparison.Ordinal);
        Assert.Contains("background: var(--oj-elevated-bg)", coverage, StringComparison.Ordinal);
        Assert.Contains(".team-chat-message.mine .team-chat-message-row p", coverage, StringComparison.Ordinal);
        Assert.Contains("background: var(--oj-accent-soft)", coverage, StringComparison.Ordinal);
        Assert.Contains(".team-system-message", coverage, StringComparison.Ordinal);
        Assert.Contains("color: var(--oj-text-muted)", coverage, StringComparison.Ordinal);
        Assert.Contains(".team-announcements > button", coverage, StringComparison.Ordinal);
        Assert.DoesNotContain("#6e7bff", coverage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LeaderboardCardsAndRanks_UseSharedAppearanceTokens()
    {
        var coverage = CoverageStyles();

        Assert.Contains(".leaderboard-overview-card", coverage, StringComparison.Ordinal);
        Assert.Contains("background: var(--oj-panel-bg)", coverage, StringComparison.Ordinal);
        Assert.Contains("--oj-rank-accent", coverage, StringComparison.Ordinal);
        Assert.Contains("color: var(--oj-rank-accent)", coverage, StringComparison.Ordinal);
    }

    [Fact]
    public void SeasonSectionsTablesResultsAndModal_UseSharedTokens()
    {
        var coverage = CoverageStyles();

        Assert.Contains(".season-overview", coverage, StringComparison.Ordinal);
        Assert.Contains(".season-row-status.enabled", coverage, StringComparison.Ordinal);
        Assert.Contains("color: var(--oj-success)", coverage, StringComparison.Ordinal);
        Assert.Contains(".season-editor-modal", coverage, StringComparison.Ordinal);
        Assert.Contains("background: var(--oj-elevated-bg)", coverage, StringComparison.Ordinal);
        Assert.Contains(".season-editor-backdrop", coverage, StringComparison.Ordinal);
        Assert.Contains("background: var(--oj-overlay-bg)", coverage, StringComparison.Ordinal);
    }

    [Fact]
    public void AnonymousSwitch_UsesCurrentAccentToken()
    {
        var account = Read("frontend", "src", "pages", "AccountSettingsPage.tsx");
        var coverage = CoverageStyles();

        Assert.Contains("site-settings-switch", account, StringComparison.Ordinal);
        Assert.Contains(".site-settings-switch.active", coverage, StringComparison.Ordinal);
        Assert.Contains("background: var(--oj-accent-soft)", coverage, StringComparison.Ordinal);
        Assert.Contains("background: var(--oj-accent)", coverage, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpMarkdown_LinksQuoteTableInlineAndBlockCodeUseTokens()
    {
        var coverage = CoverageStyles();

        Assert.Contains(".help-markdown a", coverage, StringComparison.Ordinal);
        Assert.Contains(".help-markdown blockquote", coverage, StringComparison.Ordinal);
        Assert.Contains(".help-markdown :not(pre) > code", coverage, StringComparison.Ordinal);
        Assert.Contains(".help-markdown pre", coverage, StringComparison.Ordinal);
        Assert.Contains(".help-markdown :is(th, td)", coverage, StringComparison.Ordinal);
        Assert.Contains("background: var(--oj-code-bg)", coverage, StringComparison.Ordinal);
        Assert.Contains("color: var(--oj-code-text)", coverage, StringComparison.Ordinal);
    }

    [Fact]
    public void CommonInputsTablesButtonsEmptyAndStatusSurfacesUseTokens()
    {
        var coverage = CoverageStyles();

        Assert.Contains(".site-theme-content :is(input, select, textarea)", coverage, StringComparison.Ordinal);
        Assert.Contains(".site-theme-content .button.primary", coverage, StringComparison.Ordinal);
        Assert.Contains(".site-theme-content table th", coverage, StringComparison.Ordinal);
        Assert.Contains(".state-line, .empty-state, .compact-empty", coverage, StringComparison.Ordinal);
        Assert.Contains(".alert.error, .team-create-error", coverage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Button")]
    [InlineData("Input")]
    [InlineData("Card")]
    [InlineData("Table")]
    [InlineData("Badge")]
    [InlineData("Toggle preview")]
    [InlineData("Link")]
    [InlineData("const themed = true;")]
    public void AppearancePreview_CoversRepresentativeComponents(string marker)
    {
        Assert.Contains(marker, Read("frontend", "src", "pages", "AdminSiteSettingsPage.tsx"), StringComparison.Ordinal);
    }

    [Fact]
    public void AppearancePreview_WarnsAboutLowContrast()
    {
        var page = Read("frontend", "src", "pages", "AdminSiteSettingsPage.tsx");

        Assert.Contains("getContrastWarnings", page, StringComparison.Ordinal);
        Assert.Contains("contrastRatio", page, StringComparison.Ordinal);
        Assert.Contains("4.5", page, StringComparison.Ordinal);
        Assert.Contains("3:1", page, StringComparison.Ordinal);
    }

    [Fact]
    public void SiteAppearanceApiFailure_FallsBackToDefaults()
    {
        var context = Read("frontend", "src", "theme", "ThemeContext.tsx");

        Assert.Contains("catch {", context, StringComparison.Ordinal);
        Assert.Contains("setSiteAppearance(createDefaultSiteAppearance())", context, StringComparison.Ordinal);
    }

    [Fact]
    public void SavedTheme_PropagatesWithoutPageReload()
    {
        var page = Read("frontend", "src", "pages", "AdminSiteSettingsPage.tsx");

        Assert.Contains("await reloadSiteAppearance()", page, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload", page, StringComparison.Ordinal);
    }

    [Fact]
    public void CoverageLayer_DoesNotAddFixedPrimaryAccent()
    {
        Assert.DoesNotContain("#6e7bff", CoverageStyles(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rgba(110, 123, 255", CoverageStyles(), StringComparison.OrdinalIgnoreCase);
    }

    private static string CoverageStyles()
    {
        var styles = Styles();
        var marker = styles.IndexOf("/* Global appearance semantic coverage */", StringComparison.Ordinal);
        Assert.True(marker >= 0);
        return styles[marker..];
    }

    private static string Styles() => Read("frontend", "src", "styles.css");

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(parts.Prepend(ProjectRoot()).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
