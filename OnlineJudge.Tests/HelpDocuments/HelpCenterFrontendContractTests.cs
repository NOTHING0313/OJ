namespace OnlineJudge.Tests.HelpDocuments;

public class HelpCenterFrontendContractTests
{
    [Fact]
    public void AuthLayout_DoesNotRequestOrRenderSeasonState()
    {
        var layout = Read("frontend", "src", "components", "auth", "AuthStudioLayout.tsx");
        var login = Read("frontend", "src", "pages", "LoginPage.tsx");
        var register = Read("frontend", "src", "pages", "RegisterPage.tsx");

        Assert.DoesNotContain("getCurrentSeasonPublicSummary", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("SeasonRefreshIntervalMs", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("auth-studio-season", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("STARTS IN", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("ACTIVE", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("RESULTS PUBLIC", layout, StringComparison.Ordinal);
        Assert.Contains("<AuthStudioLayout", login, StringComparison.Ordinal);
        Assert.Contains("<AuthStudioLayout", register, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpRoutesAndNavigation_AreRoleCorrect()
    {
        var main = Read("frontend", "src", "main.tsx");
        var layout = Read("frontend", "src", "AppLayout.tsx");
        var reader = Read("frontend", "src", "pages", "HelpCenterPage.tsx");

        Assert.Contains("path=\"/help\"", main, StringComparison.Ordinal);
        Assert.Contains("path=\"/help/:slug\"", main, StringComparison.Ordinal);
        Assert.Contains("path=\"/help/manage\"", main, StringComparison.Ordinal);
        Assert.Contains("allowedRoles={[2, 3]}", main, StringComparison.Ordinal);
        Assert.Contains("to=\"/help\">帮助", layout, StringComparison.Ordinal);
        Assert.Contains("canManage && <Link className=\"button\" to=\"/help/manage\">文档管理</Link>", reader, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpCenter_OpensFirstPublishedDocumentAndSupportsDeepLinks()
    {
        var reader = Read("frontend", "src", "pages", "HelpCenterPage.tsx");
        var api = Read("frontend", "src", "api", "helpDocumentsApi.ts");

        Assert.Contains("const selectedSlug = slug ?? list[0].slug", reader, StringComparison.Ordinal);
        Assert.Contains("navigate(`/help/${selectedSlug}`, { replace: true })", reader, StringComparison.Ordinal);
        Assert.Contains("to={`/help/${item.slug}`}", reader, StringComparison.Ordinal);
        Assert.Contains("/api/help-documents/${encodeURIComponent(slug)}", api, StringComparison.Ordinal);
        Assert.Contains("暂无帮助文档", reader, StringComparison.Ordinal);
    }

    [Fact]
    public void MarkdownRenderer_UsesGfmSanitizeAndSafeExternalLinks()
    {
        var markdown = Read("frontend", "src", "components", "help", "HelpMarkdown.tsx");
        var package = Read("frontend", "package.json");

        Assert.Contains("remarkPlugins={[remarkGfm]}", markdown, StringComparison.Ordinal);
        Assert.Contains("rehypePlugins={[rehypeSanitize]}", markdown, StringComparison.Ordinal);
        Assert.Contains("skipHtml", markdown, StringComparison.Ordinal);
        Assert.Contains("target={isExternal ? \"_blank\" : undefined}", markdown, StringComparison.Ordinal);
        Assert.Contains("rel={isExternal ? \"noopener noreferrer\" : undefined}", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("dangerouslySetInnerHTML", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("rehypeRaw", markdown, StringComparison.Ordinal);
        Assert.Contains("\"react-markdown\"", package, StringComparison.Ordinal);
        Assert.Contains("\"remark-gfm\"", package, StringComparison.Ordinal);
        Assert.Contains("\"rehype-sanitize\"", package, StringComparison.Ordinal);
    }

    [Fact]
    public void Editor_UsesTextareaPreviewAndContentOnlyMarkdownImport()
    {
        var editor = Read("frontend", "src", "pages", "HelpDocumentEditorPage.tsx");

        Assert.Contains("<textarea", editor, StringComparison.Ordinal);
        Assert.Contains("<HelpMarkdown>", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("Monaco", editor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accept=\".md,text/markdown,text/plain\"", editor, StringComparison.Ordinal);
        Assert.Contains("MaxImportBytes = 1024 * 1024", editor, StringComparison.Ordinal);
        Assert.Contains("setMarkdownContent(await file.text())", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("setTitle(await file.text())", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("setSlug(await file.text())", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("setSortOrder(await file.text())", editor, StringComparison.Ordinal);
        Assert.Contains("mobileMode", editor, StringComparison.Ordinal);
    }

    [Fact]
    public void Management_ProvidesAllRequiredActionsAndDeleteConfirmation()
    {
        var manage = Read("frontend", "src", "pages", "HelpDocumentManagePage.tsx");
        var editor = Read("frontend", "src", "pages", "HelpDocumentEditorPage.tsx");

        Assert.Contains("新建文档", manage, StringComparison.Ordinal);
        Assert.Contains("编辑", manage, StringComparison.Ordinal);
        Assert.Contains("下架", manage, StringComparison.Ordinal);
        Assert.Contains("发布", manage, StringComparison.Ordinal);
        Assert.Contains("删除", manage, StringComparison.Ordinal);
        Assert.Contains("window.confirm", manage, StringComparison.Ordinal);
        Assert.Contains("保存草稿", editor, StringComparison.Ordinal);
        Assert.Contains("handlePublish", editor, StringComparison.Ordinal);
    }

    [Fact]
    public void Management_EmptyState_HasOnlyTheHeaderCreateAction()
    {
        var manage = Read("frontend", "src", "pages", "HelpDocumentManagePage.tsx");

        Assert.Equal(1, CountOccurrences(manage, "to=\"/help/manage/new\">新建文档</Link>"));
        Assert.Contains("暂无文档", manage, StringComparison.Ordinal);
        Assert.Contains("创建文档后即可向答题人发布平台使用说明。", manage, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpPages_UseSafeFriendlyApiErrors()
    {
        var httpClient = Read("frontend", "src", "api", "httpClient.ts");
        var reader = Read("frontend", "src", "pages", "HelpCenterPage.tsx");
        var manage = Read("frontend", "src", "pages", "HelpDocumentManagePage.tsx");
        var editor = Read("frontend", "src", "pages", "HelpDocumentEditorPage.tsx");

        Assert.Contains("export function getApiErrorMessage", httpClient, StringComparison.Ordinal);
        Assert.Contains("\"Slug already exists.\": \"Slug 已被使用。\"", httpClient, StringComparison.Ordinal);
        Assert.Contains("apiBusinessMessages[error.message.trim()] ?? fallback", httpClient, StringComparison.Ordinal);
        Assert.DoesNotContain("err instanceof Error ? err.message", reader, StringComparison.Ordinal);
        Assert.DoesNotContain("err instanceof Error ? err.message", manage, StringComparison.Ordinal);
        Assert.DoesNotContain("err instanceof Error ? err.message", editor, StringComparison.Ordinal);
        Assert.Contains("getApiErrorMessage(err, \"加载文档失败，请稍后重试。\")", reader, StringComparison.Ordinal);
        Assert.Contains("getApiErrorMessage(err, \"保存文档失败，请稍后重试。\")", editor, StringComparison.Ordinal);
        Assert.Contains("getApiErrorMessage(err, \"发布失败，请稍后重试。\")", editor, StringComparison.Ordinal);
        Assert.Contains("getApiErrorMessage(err, \"删除失败，请稍后重试。\")", manage, StringComparison.Ordinal);
    }

    [Fact]
    public void AddHelpDocuments_RemainsInChainBeforeCurrentMigration()
    {
        var migrationDirectory = Path.Combine(ProjectRoot(), "OnlineJudge.Infrastructure", "Persistence", "Migrations");
        var migrations = Directory.GetFiles(migrationDirectory, "*_*.cs")
            .Where(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal))
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();

        var helpMigrationPath = Assert.Single(migrations, path => path.EndsWith("20260829132321_AddHelpDocuments.cs", StringComparison.Ordinal));
        var sessionMigrationIndex = Array.FindIndex(migrations, path => path.EndsWith("AddSingleActiveUserSession.cs", StringComparison.Ordinal));
        var auditMigrationIndex = Array.FindIndex(migrations, path => path.EndsWith("AddSecurityAuditLogs.cs", StringComparison.Ordinal));
        Assert.True(sessionMigrationIndex >= 0);
        Assert.Equal(migrations.Length - 1, auditMigrationIndex);
        Assert.True(sessionMigrationIndex < auditMigrationIndex);
        var migration = File.ReadAllText(helpMigrationPath);
        Assert.Contains("migrationBuilder.CreateTable(", migration, StringComparison.Ordinal);
        Assert.Contains("name: \"HelpDocuments\"", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpLayout_HasBoundedReadingWidthAndResponsiveOverflow()
    {
        var styles = Read("frontend", "src", "styles.css");

        Assert.Contains("grid-template-columns: 260px minmax(0, 1fr)", styles, StringComparison.Ordinal);
        Assert.Contains("width: min(900px, 100%)", styles, StringComparison.Ordinal);
        Assert.Contains(".help-markdown table", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto", styles, StringComparison.Ordinal);
        Assert.Contains(".help-markdown img", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 760px)", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminApi_UsesDatabaseAuthoritativePolicy()
    {
        var controller = Read("OnlineJudge.Api", "Controllers", "AdminHelpDocumentsController.cs");
        var service = Read("OnlineJudge.Infrastructure", "HelpDocuments", "HelpDocumentService.cs");

        Assert.Contains("[Authorize(Policy = \"RequireProblemSetter\")]", controller, StringComparison.Ordinal);
        Assert.Contains("RequireManagerAsync", service, StringComparison.Ordinal);
        Assert.Contains("!item.IsDeleted && !item.IsBlacklisted", service, StringComparison.Ordinal);
        Assert.Contains("UserRole.ProblemSetter or UserRole.Root", service, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[HttpGet]")]
    [InlineData("[HttpGet(\"{id:guid}\")]")]
    [InlineData("[HttpPost]")]
    [InlineData("[HttpPut(\"{id:guid}\")]")]
    [InlineData("[HttpPost(\"{id:guid}/publish\")]")]
    [InlineData("[HttpPost(\"{id:guid}/unpublish\")]")]
    [InlineData("[HttpDelete(\"{id:guid}\")]")]
    public void AdminApi_ExposesRequiredCrudAndLifecycleRoutes(string routeMarker)
    {
        var controller = Read("OnlineJudge.Api", "Controllers", "AdminHelpDocumentsController.cs");
        Assert.Contains(routeMarker, controller, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".help-markdown h1")]
    [InlineData(".help-markdown h2")]
    [InlineData(".help-markdown h3")]
    [InlineData(".help-markdown blockquote")]
    [InlineData(".help-markdown pre")]
    [InlineData(".help-markdown table")]
    [InlineData(".help-markdown img")]
    public void MarkdownReadingStyles_CoverRequiredContentTypes(string styleMarker)
    {
        var styles = Read("frontend", "src", "styles.css");
        Assert.Contains(styleMarker, styles, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(parts.Prepend(ProjectRoot()).ToArray()));

    private static int CountOccurrences(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;

    private static string ProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
