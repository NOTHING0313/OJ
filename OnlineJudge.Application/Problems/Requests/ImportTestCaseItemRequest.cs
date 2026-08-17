using System.Text.Json;
using System.Text.Json.Serialization;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Requests;

public class ImportTestCaseItemRequest
{
    public string? Input { get; set; }

    public string? ExpectedOutput { get; set; }

    public JsonElement? ArgumentsJson { get; set; }

    public JsonElement? ExpectedJson { get; set; }

    public int? Score { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<TestCaseVisibility>))]
    public TestCaseVisibility? Visibility { get; set; }
}
