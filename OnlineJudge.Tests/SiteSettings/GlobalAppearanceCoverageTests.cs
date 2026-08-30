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
    [InlineData("theme-editor-preview-nav")]
    [InlineData("page-header")]
    [InlineData("content-block")]
    [InlineData("button")]
    [InlineData("input")]
    [InlineData("theme-editor-badge")]
    [InlineData("table")]
    [InlineData("<code>")]
    [InlineData("empty-state")]
    [InlineData("DraftIcon")]
    [InlineData("decoration.pageHeader")]
    public void AppearancePreview_CoversRepresentativeComponents(string marker)
    {
        Assert.Contains(marker, Read("frontend", "src", "components", "theme-editor", "ThemeEditorPreview.tsx"), StringComparison.Ordinal);
    }

    [Fact]
    public void AppearancePreview_WarnsAboutLowContrast()
    {
        var page = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");

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
        var page = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");

        Assert.Contains("await reloadSiteAppearance()", page, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload", page, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericTheme_DefaultDoesNotAddRenderingLayersOrPanelOverrides()
    {
        var context = Read("frontend", "src", "theme", "ThemeContext.tsx");
        var api = Read("frontend", "src", "api", "siteSettingsApi.ts");

        Assert.Contains("enabled: false", api, StringComparison.Ordinal);
        Assert.Contains("asset: null", api, StringComparison.Ordinal);
        Assert.Contains("panelSkin.enabled && hasPanelSkinStyle", context, StringComparison.Ordinal);
        Assert.Contains("backgroundUrl && (usesGenericBackground || effectiveBackground)", context, StringComparison.Ordinal);
        Assert.Contains("panelSkinActive ?", context, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericBackground_IsAnIndependentNonInteractiveLayerWithScopedFilters()
    {
        var context = Read("frontend", "src", "theme", "ThemeContext.tsx");
        var styles = Styles();

        Assert.Contains("site-theme-background-image", context, StringComparison.Ordinal);
        Assert.Contains("site-theme-background-overlay", context, StringComparison.Ordinal);
        Assert.Contains("style.filter", context, StringComparison.Ordinal);
        Assert.Contains(".site-theme-background", styles, StringComparison.Ordinal);
        Assert.Contains("pointer-events: none", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("body.style.filter", context, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericPanelSkin_UsesOptInSharedModifiersAndRequiredCoverage()
    {
        var context = Read("frontend", "src", "theme", "ThemeContext.tsx");
        var styles = Styles();

        Assert.Contains("theme-panel-bg-texture", context, StringComparison.Ordinal);
        Assert.Contains("theme-panel-header-texture", context, StringComparison.Ordinal);
        Assert.Contains("theme-panel-border-texture", context, StringComparison.Ordinal);
        Assert.Contains(".problem-content", styles, StringComparison.Ordinal);
        Assert.Contains(".challenge-card", styles, StringComparison.Ordinal);
        Assert.Contains(".team-create-card", styles, StringComparison.Ordinal);
        Assert.Contains(".leaderboard-table-wrap", styles, StringComparison.Ordinal);
        Assert.Contains(".help-document-panel", styles, StringComparison.Ordinal);
        Assert.Contains(".security-audit-table-wrap", styles, StringComparison.Ordinal);
        Assert.Contains(".auth-studio-card", styles, StringComparison.Ordinal);
        Assert.Contains(".table-wrap", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void RootEditor_UsesLocalDraftUploadPreviewSaveDiscardAndDefaultReset()
    {
        var page = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        var api = Read("frontend", "src", "api", "siteSettingsApi.ts");

        Assert.Contains("uploadThemeAsset", page, StringComparison.Ordinal);
        Assert.Contains("ThemeEditorPreview", page, StringComparison.Ordinal);
        Assert.Contains("handleDiscard", page, StringComparison.Ordinal);
        Assert.Contains("handleResetSection", page, StringComparison.Ordinal);
        Assert.Contains("handleResetAll", page, StringComparison.Ordinal);
        Assert.Contains("/api/site-settings/theme-assets", api, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", api, StringComparison.Ordinal);
        Assert.DoesNotContain("https://", api, StringComparison.Ordinal);
    }

    [Fact]
    public void ThemeSlotRegistries_AreCentralizedAndContainRequiredGenericSlots()
    {
        var registry = Read("frontend", "src", "theme", "themeSlots.ts");
        var backendRegistry = Read("OnlineJudge.Application", "SiteSettings", "ThemeSlotKeys.cs");

        foreach (var slot in new[] { "problem", "challenge", "leaderboard", "team", "submission", "help", "profile", "chat", "git", "season", "reward" })
        {
            Assert.Contains($"key: \"{slot}\"", registry, StringComparison.Ordinal);
            Assert.Contains($"\"{slot}\"", backendRegistry, StringComparison.Ordinal);
        }

        foreach (var slot in new[] { "pageHeader", "cardHeader", "panelCorner", "emptyState" })
        {
            Assert.Contains($"key: \"{slot}\"", registry, StringComparison.Ordinal);
            Assert.Contains($"\"{slot}\"", backendRegistry, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Anime", registry, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThemeIcon_PreservesDefaultRendererLayoutAndAccessibilityContract()
    {
        var component = Read("frontend", "src", "components", "ThemeIcon.tsx");
        var layout = Read("frontend", "src", "AppLayout.tsx");
        var styles = Styles();

        Assert.Contains("return <>{fallback}</>", component, StringComparison.Ordinal);
        Assert.Contains("availableThemeAssetUrls.has(url)", component, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", component, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain", styles, StringComparison.Ordinal);
        Assert.Contains("width: 20px", styles, StringComparison.Ordinal);
        Assert.Contains("<ThemeIcon slot=\"problem\" />题目", layout, StringComparison.Ordinal);
        Assert.Contains("<ThemeIcon slot=\"help\" />帮助", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void Decorations_AreSharedNonInteractiveLayersWithRequiredCoverage()
    {
        var context = Read("frontend", "src", "theme", "ThemeContext.tsx");
        var styles = Styles();

        Assert.Contains("buildDecorationStyle", context, StringComparison.Ordinal);
        Assert.Contains("theme-decoration-page-header", styles, StringComparison.Ordinal);
        Assert.Contains("theme-decoration-card-header", styles, StringComparison.Ordinal);
        Assert.Contains("theme-decoration-panel-corner", styles, StringComparison.Ordinal);
        Assert.Contains("theme-decoration-empty-state", styles, StringComparison.Ordinal);
        Assert.Contains("pointer-events: none", styles, StringComparison.Ordinal);
        Assert.Contains(".problem-content", styles, StringComparison.Ordinal);
        Assert.Contains(".challenge-card", styles, StringComparison.Ordinal);
        Assert.Contains(".team-chat-workspace", styles, StringComparison.Ordinal);
        Assert.Contains(".leaderboard-v2-feature-card", styles, StringComparison.Ordinal);
        Assert.Contains(".help-document-panel", styles, StringComparison.Ordinal);
        Assert.Contains(".admin-panel", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void RootEditor_UsesOneAppearanceDraftForSlotsPickerReuseAndReset()
    {
        var page = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        var model = Read("frontend", "src", "components", "theme-editor", "themeEditorModel.ts");
        var api = Read("frontend", "src", "api", "siteSettingsApi.ts");

        Assert.Contains("history.present", page, StringComparison.Ordinal);
        Assert.Contains("draft.icons", page, StringComparison.Ordinal);
        Assert.Contains("draft.decorations", page, StringComparison.Ordinal);
        Assert.Contains("主题素材库", page, StringComparison.Ordinal);
        Assert.Contains("listThemeAssets", page, StringComparison.Ordinal);
        Assert.Contains("resetThemeSurface", page, StringComparison.Ordinal);
        Assert.Contains("createDefaultSiteAppearance", page, StringComparison.Ordinal);
        Assert.Contains("ThemeEditorHistoryLimit = 50", model, StringComparison.Ordinal);
        Assert.Contains("background:", api, StringComparison.Ordinal);
        Assert.Contains("panelSkin:", api, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("login")]
    [InlineData("problem")]
    [InlineData("challenge")]
    [InlineData("team")]
    [InlineData("leaderboard")]
    [InlineData("season")]
    [InlineData("help")]
    [InlineData("account")]
    [InlineData("security-audit")]
    public void VisualEditor_PageSelectorUsesSyntheticControlledSamples(string page)
    {
        var model = Read("frontend", "src", "components", "theme-editor", "themeEditorModel.ts");
        var preview = Read("frontend", "src", "components", "theme-editor", "ThemeEditorPreview.tsx");

        Assert.Contains($"key: \"{page}\"", model, StringComparison.Ordinal);
        Assert.DoesNotContain("fetch(", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("iframe", preview, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("desktop", "1120")]
    [InlineData("tablet", "768")]
    [InlineData("mobile", "375")]
    public void VisualEditor_ViewportPresetsChangeCanvasWidthOnly(string viewport, string width)
    {
        var model = Read("frontend", "src", "components", "theme-editor", "themeEditorModel.ts");

        Assert.Contains($"key: \"{viewport}\"", model, StringComparison.Ordinal);
        Assert.Contains($"width: {width}", model, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("global.background")]
    [InlineData("global.colors")]
    [InlineData("page.background")]
    [InlineData("panel.primary")]
    [InlineData("panel.header")]
    [InlineData("panel.border")]
    [InlineData("icon.problem")]
    [InlineData("decoration.pageHeader")]
    [InlineData("decoration.cardHeader")]
    [InlineData("decoration.panelCorner")]
    [InlineData("decoration.emptyState")]
    public void VisualEditor_SurfaceRegistryIsControlledAndSearchable(string surface)
    {
        var model = Read("frontend", "src", "components", "theme-editor", "themeEditorModel.ts");
        var workbench = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");

        if (surface.StartsWith("icon.", StringComparison.Ordinal))
        {
            Assert.Contains("id: `icon.${key}`", model, StringComparison.Ordinal);
            Assert.Contains($"key: \"{surface["icon.".Length..]}\"", Read("frontend", "src", "theme", "themeSlots.ts"), StringComparison.Ordinal);
        }
        else if (surface.StartsWith("decoration.", StringComparison.Ordinal))
        {
            Assert.Contains("id: `decoration.${key}`", model, StringComparison.Ordinal);
            Assert.Contains($"key: \"{surface["decoration.".Length..]}\"", Read("frontend", "src", "theme", "themeSlots.ts"), StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains(surface, model, StringComparison.Ordinal);
        }
        Assert.Contains("surfaceSearch", workbench, StringComparison.Ordinal);
        Assert.DoesNotContain("XPath", model, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("querySelector", model, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualEditor_HistoryCompareDirtySaveDiscardAndResetRemainLocalUntilSave()
    {
        var model = Read("frontend", "src", "components", "theme-editor", "themeEditorModel.ts");
        var workbench = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");

        foreach (var marker in new[] { "undo", "redo", "begin-gesture", "end-gesture", "gestureStart", "discard", "save-success", "ThemeEditorHistoryLimit = 50" })
        {
            Assert.Contains(marker, model, StringComparison.Ordinal);
        }
        foreach (var marker in new[] { "有未保存修改", "已保存版本", "系统默认", "保存并应用全站", "恢复当前区域默认值", "恢复整个默认主题" })
        {
            Assert.Contains(marker, workbench, StringComparison.Ordinal);
        }
        Assert.Equal(1, Count(workbench, "updateSiteAppearance("));
        Assert.DoesNotContain("localStorage", model, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", workbench, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualEditor_PropertyInspectorProvidesAccessiblePairedControlsAndSecureAssetDropZone()
    {
        var workbench = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");

        Assert.Contains("aria-label=\"属性设置\"", workbench, StringComparison.Ordinal);
        Assert.Contains("type=\"range\"", workbench, StringComparison.Ordinal);
        Assert.Contains("type=\"number\"", workbench, StringComparison.Ordinal);
        Assert.Contains("type=\"color\"", workbench, StringComparison.Ordinal);
        Assert.Contains("#RRGGBB", workbench, StringComparison.Ordinal);
        Assert.Contains("onDrop", workbench, StringComparison.Ordinal);
        Assert.Contains("uploadThemeAsset", workbench, StringComparison.Ordinal);
        Assert.Contains("服务器会进行安全检查", workbench, StringComparison.Ordinal);
        Assert.Contains("引用位置", workbench, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualEditor_IsRootProtectedAndRouteLevelLazyLoaded()
    {
        var main = Read("frontend", "src", "main.tsx");
        var layout = Read("frontend", "src", "AppLayout.tsx");

        Assert.Contains("lazy(() => import(\"./pages/AdminSiteSettingsPage\"))", main, StringComparison.Ordinal);
        Assert.Contains("<Suspense", main, StringComparison.Ordinal);
        Assert.Contains("path=\"/admin/site-settings\"", main, StringComparison.Ordinal);
        Assert.Contains("allowedRoles={[3]}", main, StringComparison.Ordinal);
        Assert.Contains("isRoot(role) && <NavLink to=\"/admin/site-settings\"", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void VisualEditor_AddsNoThemeApiMigrationOrUnsafeCustomizationSurface()
    {
        var workbench = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        var preview = Read("frontend", "src", "components", "theme-editor", "ThemeEditorPreview.tsx");

        Assert.DoesNotContain("customCss", workbench, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dangerouslySetInnerHTML", workbench, StringComparison.Ordinal);
        Assert.DoesNotContain("dangerouslySetInnerHTML", preview, StringComparison.Ordinal);
        Assert.DoesNotContain("eval(", workbench, StringComparison.Ordinal);
        Assert.DoesNotContain("selector", workbench, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VisualEditor_HasNoMascotRequirementOrImplementation()
    {
        var workbench = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        var model = Read("frontend", "src", "components", "theme-editor", "themeEditorModel.ts");

        Assert.DoesNotContain("mascot", workbench, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mascot", model, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Live2D", workbench, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Spine", workbench, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingSlotAssets_DoNotRenderBrokenImagesOrRetryLoops()
    {
        var component = Read("frontend", "src", "components", "ThemeIcon.tsx");
        var context = Read("frontend", "src", "theme", "ThemeContext.tsx");

        Assert.Contains("!availableThemeAssetUrls.has(url)", component, StringComparison.Ordinal);
        Assert.Contains("image.onload", context, StringComparison.Ordinal);
        Assert.DoesNotContain("setInterval", context, StringComparison.Ordinal);
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

    private static int Count(string value, string marker) => value.Split(marker, StringSplitOptions.None).Length - 1;

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(parts.Prepend(ProjectRoot()).ToArray()));

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
