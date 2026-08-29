namespace OnlineJudge.Domain.Entities;

public sealed class HelpDocument
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string MarkdownContent { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    public int SortOrder { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid UpdatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
