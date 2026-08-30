namespace OnlineJudge.Tests.SiteSettings;

public sealed class ThemeLibraryFrontendContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Workbench_ProvidesLibrarySearchSortAndCompactActions()
    {
        var source = Read("frontend", "src", "components", "theme-editor", "ThemeEditorWorkbench.tsx");
        Assert.Contains("THEME LIBRARY", source);
        Assert.Contains("Search theme presets", source);
        Assert.Contains("Save As", source);
        Assert.Contains("<summary>More</summary>", source);
        Assert.Contains("Load", source);
        Assert.Contains("Apply", source);
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
        Assert.Contains(">Cancel</button>", source);
        Assert.Contains(">Discard</button>", source);
        Assert.Contains(">Save Draft</button>", source);
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

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root, .. parts]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OnlineJudge.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
