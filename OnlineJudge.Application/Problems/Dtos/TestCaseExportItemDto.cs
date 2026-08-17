using System.Text.Json;
using System.Text.Json.Serialization;
using OnlineJudge.Domain.Enums;

namespace OnlineJudge.Application.Problems.Dtos;

public class TestCaseExportItemDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Input { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpectedOutput { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? ArgumentsJson { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? ExpectedJson { get; set; }

    public int Score { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<TestCaseVisibility>))]
    public TestCaseVisibility Visibility { get; set; }
}
