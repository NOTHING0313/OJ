using System.Reflection;
using System.Text;
using OnlineJudge.Application.Challenges.Dtos;
using OnlineJudge.Infrastructure.Challenges;
using OnlineJudge.Infrastructure.Exports;

namespace OnlineJudge.Tests.Challenges;

public sealed class ChallengeCsvExportSecurityTests
{
    [Theory]
    [InlineData("=2+2")]
    [InlineData(" +cmd|' /C calc'!A0")]
    [InlineData("-1+2")]
    [InlineData("@SUM(A1:A2)")]
    [InlineData("\t=HYPERLINK(\"https://example.test\")")]
    [InlineData("\r=1+1")]
    public void UntrustedFormulaLikeText_IsPrefixedBeforeRfcCsvQuoting(string value)
    {
        var builder = new StringBuilder();

        SpreadsheetSafeCsvWriter.AppendRow(builder, [CsvCell.Text(value)]);

        var csv = builder.ToString().TrimEnd('\r', '\n');
        Assert.Contains("'", csv, StringComparison.Ordinal);
        Assert.False(csv.StartsWith('=') || csv.StartsWith('+') || csv.StartsWith('-') || csv.StartsWith('@'));
    }

    [Fact]
    public void TrustedNumbersDatesAndIdentifiers_AreNotChanged()
    {
        var builder = new StringBuilder();
        const string identifier = "0b27615b-3276-4e04-9457-91e42a8d0210";

        SpreadsheetSafeCsvWriter.AppendRow(builder,
        [
            CsvCell.Trusted("-42"),
            CsvCell.Trusted(identifier),
            CsvCell.Trusted("2026-09-03 12:00:00 +00:00")
        ]);

        Assert.Equal($"-42,{identifier},2026-09-03 12:00:00 +00:00", builder.ToString().TrimEnd('\r', '\n'));
    }

    [Fact]
    public void ExportClassifiesUserControlledTaskFieldsAsTextAndKeepsUtf8Bom()
    {
        var summary = new ChallengeAdminSummaryDto
        {
            Users =
            [
                new ChallengeAdminUserProgressDto
                {
                    UserId = Guid.NewGuid(),
                    UserName = "=USERNAME()",
                    TaskStatuses =
                    [
                        new ChallengeAdminUserTaskStatusDto
                        {
                            TaskId = Guid.NewGuid(),
                            TaskTitle = "+TITLE()",
                            OriginalFileName = "-FILE()",
                            ReviewComment = "@COMMENT()",
                            ReviewedByUserName = "\t=REVIEWER()"
                        }
                    ]
                }
            ]
        };

        var csv = InvokePrivate<string>("BuildAdminTasksCsv", summary);
        var bytes = InvokePrivate<byte[]>("BuildCsvBytes", csv);

        Assert.Contains("'=USERNAME()", csv, StringComparison.Ordinal);
        Assert.Contains("'+TITLE()", csv, StringComparison.Ordinal);
        Assert.Contains("'-FILE()", csv, StringComparison.Ordinal);
        Assert.Contains("'@COMMENT()", csv, StringComparison.Ordinal);
        Assert.Contains("'\t=REVIEWER()", csv, StringComparison.Ordinal);
        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
    }

    private static T InvokePrivate<T>(string methodName, params object?[] arguments)
    {
        var method = typeof(ChallengeService).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Method {methodName} was not found.");
        return (T)(method.Invoke(null, arguments)
            ?? throw new InvalidOperationException($"Method {methodName} returned null."));
    }
}
