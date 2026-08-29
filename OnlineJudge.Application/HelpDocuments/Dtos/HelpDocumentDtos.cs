namespace OnlineJudge.Application.HelpDocuments.Dtos;

public sealed class HelpDocumentListItemDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public bool IsPublished { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class HelpDocumentDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string MarkdownContent { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
