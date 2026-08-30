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

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root, .. parts]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
