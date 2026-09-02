namespace OnlineJudge.Tests.SiteSettings;

public sealed class ThemeLibraryFrontendContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Workbench_ProvidesLibrarySearchSortAndCompactActions()
    {
        var source = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        Assert.Contains("主题库", source);
        Assert.Contains("搜索主题", source);
        Assert.Contains("另存为主题", source);
        Assert.Contains("<summary>更多</summary>", source);
        Assert.Contains("载入预览", source);
        Assert.Contains("应用全站", source);
    }

    [Fact]
    public void LoadingPreset_ChangesDraftWithoutAppearanceUpdateCall()
    {
        var source = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        var start = source.IndexOf("async function executeLibraryAction", StringComparison.Ordinal);
        var end = source.IndexOf("function saveThenContinuePendingAction", start, StringComparison.Ordinal);
        var method = source[start..end];
        var loadBranch = method[..method.IndexOf("setApplyPresetDialog", StringComparison.Ordinal)];
        Assert.Contains("dispatch({ type: \"change\", value: preset.appearance })", loadBranch);
        Assert.DoesNotContain("updateSiteAppearance", loadBranch);
    }

    [Fact]
    public void DirtyPresetTransition_OffersSaveDiscardAndCancel()
    {
        var source = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        Assert.Contains("function DraftTransitionDialog", source);
        Assert.Contains(">取消</button>", source);
        Assert.Contains(">放弃并继续</button>", source);
        Assert.Contains(">先另存草稿</button>", source);
    }

    [Fact]
    public void ImportExport_UseServerPackEndpointsWithoutClientZipDependency()
    {
        var source = Read("frontend", "src", "api", "siteSettingsApi.ts");
        Assert.Contains("/export", source);
        Assert.Contains("theme-presets/import", source);
        Assert.Contains("new FormData()", source);
        Assert.DoesNotContain("JSZip", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeThemeProvider_DoesNotQueryPresetLibrary()
    {
        var source = Read("frontend", "src", "theme", "ThemeContext.tsx");
        Assert.DoesNotContain("theme-presets", source);
        Assert.DoesNotContain("listThemePresets", source);
    }

    [Fact]
    public void LibraryStyles_RemainScopedAndResponsive()
    {
        var styles = Read("frontend", "src", "styles.css");
        Assert.Contains(".theme-library-panel", styles);
        Assert.Contains(".theme-library-grid", styles);
        Assert.Contains("@media (max-width: 760px)", styles);
    }

    [Fact]
    public void ArtistWorkflow_UsesGroupedPlainLanguageInspectorAndSearchableAssetPicker()
    {
        var source = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        Assert.Contains("function InspectorGroup", source);
        Assert.Contains("title=\"背景素材\"", source);
        Assert.Contains("title=\"构图\"", source);
        Assert.Contains("title=\"效果\"", source);
        Assert.Contains("只看未引用", source);
        Assert.Contains("全部类型", source);
        Assert.Contains("getAssetDisplayName", source);
        Assert.DoesNotContain(">Asset Library</button>", source);
        Assert.DoesNotContain(">Save & Apply</button>", source);
    }

    [Fact]
    public void SavedPresetCheckpointAndDirtyTransitions_AreDistinctFromActiveAppearance()
    {
        var source = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        Assert.Contains("const [draftCheckpoint, setDraftCheckpoint]", source);
        Assert.Contains("const dirty = !appearanceEquals(draftCheckpoint, history.present)", source);
        Assert.Contains("compareMode === \"saved\" ? draftCheckpoint", source);
        Assert.Contains("setDraftCheckpoint(preset.appearance)", source);
        Assert.Contains("setDraftCheckpoint(created.appearance)", source);
        Assert.Contains("dispatch({ type: \"initialize\", value: draftCheckpoint })", source);
        Assert.Contains("kind: \"navigate\"", source);
        Assert.Contains("kind: \"reset\"", source);
        Assert.Contains("先另存草稿", source);
        Assert.Contains("放弃并继续", source);
    }

    [Fact]
    public void ThemeDialogs_UseSharedFocusTrapAndNoNativePromptOrConfirm()
    {
        var source = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        var dialog = Read("frontend", "src", "components", "theme-editor", "ThemeEditorDialog.tsx");

        Assert.DoesNotContain("window.prompt", source);
        Assert.DoesNotContain("window.confirm", source);
        Assert.Contains("<ThemeEditorDialog", source);
        Assert.Contains("event.key !== \"Tab\"", dialog);
        Assert.Contains("event.shiftKey", dialog);
        Assert.Contains("event.key === \"Escape\"", dialog);
        Assert.Contains("previousFocus?.focus()", dialog);
        Assert.Contains("aria-modal=\"true\"", dialog);
        Assert.Contains("document.body.style.overflow = \"hidden\"", dialog);
    }

    [Fact]
    public void Import_RequiresValidatedManifestReviewBeforeCommit()
    {
        var source = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        var api = Read("frontend", "src", "api", "siteSettingsApi.ts");

        Assert.Contains("preflightThemePresetImport", source);
        Assert.Contains("主题包已通过安全检查", source);
        Assert.Contains("尚未导入", source);
        Assert.Contains("不会自动应用", source);
        Assert.Contains("theme-presets/import/preflight", api);
        Assert.Contains("确认导入主题库", source);
    }

    [Fact]
    public void AssetPicker_UsesPersistedDisplayNameAndStillSearchesAssetId()
    {
        var source = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        var api = Read("frontend", "src", "api", "siteSettingsApi.ts");

        Assert.Contains("asset.displayName?.trim()", source);
        Assert.Contains("getAssetDisplayName(asset), asset.assetId", source);
        Assert.Contains("renameThemeAsset", source);
        Assert.Contains("displayName: string | null", api);
    }

    [Fact]
    public void LeaderboardRuntimeAndThemePreview_UseTheSamePresentationalView()
    {
        var runtime = Read("frontend", "src", "pages", "LeaderboardHomePage.tsx");
        var preview = Read("frontend", "src", "components", "theme-editor", "ThemeEditorPreview.tsx");

        Assert.Contains("<LeaderboardHomeView", runtime);
        Assert.Contains("<LeaderboardHomeView", preview);
        Assert.DoesNotContain("leaderboard-v2-feature-card", runtime);
        Assert.DoesNotContain("leaderboard-v2-feature-card", preview);
    }

    [Fact]
    public void LeaderboardPreview_UsesSyntheticDataWithoutLeaderboardApiCalls()
    {
        var preview = Read("frontend", "src", "components", "theme-editor", "ThemeEditorPreview.tsx");
        var fixtures = Read("frontend", "src", "components", "theme-editor", "themePreviewFixtures.json");

        Assert.Contains("leaderboardPreviewFixture", preview);
        Assert.Contains("Theme Preview User", fixtures);
        Assert.Contains("测试挑战", fixtures);
        Assert.DoesNotContain("getCurrentSeasonLeaderboard", preview);
        Assert.DoesNotContain("getChallengeLeaderboardIndex", preview);
        Assert.DoesNotContain("getCurrentSeasonPublicSummary", preview);
    }

    [Fact]
    public void LeaderboardPreview_PreservesProductionCopyAndRemovesFakeComposition()
    {
        var preview = Read("frontend", "src", "components", "theme-editor", "ThemeEditorPreview.tsx");
        var view = Read("frontend", "src", "components", "leaderboards", "LeaderboardHomeView.tsx");

        Assert.Contains("榜单中心", view);
        Assert.Contains("榜单管理", view);
        Assert.DoesNotContain("主要操作", view);
        Assert.DoesNotContain("TOP 3", preview);
        Assert.Contains("data-surface=\"panel.primary\"", view);
        Assert.Contains("production-preview", preview);
    }

    [Fact]
    public void ThemePreview_UsesProductionNavigationComponent()
    {
        var runtime = Read("frontend", "src", "AppLayout.tsx");
        var preview = Read("frontend", "src", "components", "theme-editor", "ThemeEditorPreview.tsx");

        Assert.Contains("<AppHeaderView", runtime);
        Assert.Contains("<AppHeaderView", preview);
        Assert.Contains("interactive={false}", preview);
    }

    [Fact]
    public void ProblemRuntimeAndThemePreview_UseTheSamePresentationalViewWithoutPreviewApiImports()
    {
        var runtime = Read("frontend", "src", "pages", "ProblemDetailPage.tsx");
        var preview = Read("frontend", "src", "components", "theme-editor", "ThemeEditorPreview.tsx");

        Assert.Contains("<ProblemDetailView", runtime);
        Assert.Contains("<ProblemDetailView", preview);
        Assert.DoesNotContain("getProblem", preview);
        Assert.DoesNotContain("createSubmission", preview);
        Assert.DoesNotContain("CodeEditor", preview);
    }

    [Fact]
    public void HelpRuntimeAndThemePreview_UseTheSamePresentationalViewAndMarkdownRenderer()
    {
        var runtime = Read("frontend", "src", "pages", "HelpCenterPage.tsx");
        var preview = Read("frontend", "src", "components", "theme-editor", "ThemeEditorPreview.tsx");
        var view = Read("frontend", "src", "components", "help", "HelpCenterView.tsx");

        Assert.Contains("<HelpCenterView", runtime);
        Assert.Contains("<HelpCenterView", preview);
        Assert.Contains("<HelpMarkdown", view);
        Assert.DoesNotContain("getPublishedHelpDocument", preview);
    }

    [Fact]
    public void FidelityHarness_UsesSharedFixtureAndStrictGeometryPixelGates()
    {
        var harness = Read("scripts", "e2e", "theme-preview-fidelity.mjs");

        Assert.Contains("geometryTolerance = 2", harness);
        Assert.Contains("pixelThreshold = 0.002", harness);
        Assert.Contains("antiAliasingPixelThreshold = 0.005", harness);
        Assert.Contains("leaderboard-production.png", harness);
        Assert.Contains("problem-production.png", harness);
        Assert.Contains("help-production.png", harness);
    }

    [Fact]
    public void ProductionPreviewPages_RemoveLegacyFakeCompositionsAndExposeRealThemeSurfaces()
    {
        var preview = Read("frontend", "src", "components", "theme-editor", "ThemeEditorPreview.tsx");
        var problem = Read("frontend", "src", "components", "problems", "ProblemDetailView.tsx");
        var help = Read("frontend", "src", "components", "help", "HelpCenterView.tsx");

        Assert.DoesNotContain("主要操作", preview);
        Assert.DoesNotContain("TOP 3", preview);
        Assert.DoesNotContain("示例记录", preview);
        Assert.Contains("data-surface=\"panel.primary\"", problem);
        Assert.Contains("data-surface=\"decoration.pageHeader\"", problem);
        Assert.Contains("data-surface=\"panel.primary\"", help);
        Assert.Contains("data-surface=\"decoration.pageHeader\"", help);
    }

    [Fact]
    public void PreviewWorkbench_ProvidesFocusCollapsiblePanesZoomAndSurfaceFeedback()
    {
        var workbench = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        var preview = Read("frontend", "src", "components", "theme-editor", "ThemeEditorPreview.tsx");
        var model = Read("frontend", "src", "components", "theme-editor", "themeEditorModel.ts");
        var styles = Read("frontend", "src", "styles.css");

        Assert.Contains("专注预览", workbench);
        Assert.Contains("收起导航", workbench);
        Assert.Contains("收起属性", workbench);
        Assert.Contains("当前选择：", workbench);
        Assert.Contains("getAffectedSurfaceMessage", workbench);
        Assert.Contains("pulseSurface", preview);
        Assert.Contains("zoom-${zoom}", preview);
        Assert.Contains("适应宽度", model);
        Assert.Contains("label: \"100%\"", model);
        Assert.Contains("label: \"75%\"", model);
        Assert.Contains("label: \"50%\"", model);
        Assert.Contains(".theme-editor-page.focus-mode", styles);
        Assert.Contains("theme-editor-surface-pulse", styles);
    }

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root, .. parts]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
