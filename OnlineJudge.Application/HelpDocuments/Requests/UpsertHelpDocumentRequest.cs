namespace OnlineJudge.Application.HelpDocuments.Requests;

public sealed class UpsertHelpDocumentRequest
{
    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string MarkdownContent { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
