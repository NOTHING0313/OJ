using OnlineJudge.Domain.Enums;
using OnlineJudge.Infrastructure.Judging.Function;

namespace OnlineJudge.Tests.Judging.Function;

public class CustomStructFunctionJudgeTests
{
    private const string GeometrySpec = """
        {
          "types": [
            {
              "name": "Point",
              "fields": [
                { "name": "x", "type": "double" },
                { "name": "y", "type": "double" }
              ]
            },
            {
              "name": "Triangle",
              "fields": [
                { "name": "a", "type": "Point" },
                { "name": "b", "type": "Point" },
                { "name": "c", "type": "Point" }
              ]
            }
          ],
          "functionName": "scoreTriangles",
          "returnType": "double",
          "parameters": [
            { "name": "triangles", "type": "Triangle[]" }
          ],
          "supportedLanguages": ["cpp17", "csharp", "c11"]
        }
        """;

    private const string Arguments = """
        {
          "triangles": [
            {
              "a": { "x": 0.0, "y": 0.0 },
              "b": { "x": 1.0, "y": 0.0 },
              "c": { "x": 0.0, "y": 1.0 }
            }
          ]
        }
        """;

    [Fact]
    public void Parse_AllowsNestedCustomStructAndArray()
    {
        var result = FunctionJudgeSpecParser.Parse(GeometrySpec);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Types.Count);
        Assert.Equal("Triangle[]", result.Value.Parameters[0].Type);
        Assert.Equal("Point", result.Value.Types[1].Fields[0].Type);
    }

    [Fact]
    public void ValidateTestCase_AcceptsNestedCustomStructJson()
    {
        var spec = FunctionJudgeSpecParser.Parse(GeometrySpec).Value!;

        var result = FunctionJudgeSpecParser.ValidateTestCase(spec, Arguments, "0.5");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateTestCase_RejectsMissingCustomField()
    {
        var spec = FunctionJudgeSpecParser.Parse(GeometrySpec).Value!;
        var result = FunctionJudgeSpecParser.ValidateTestCase(
            spec,
            """{ "triangles": [{ "a": {"x":0,"y":0}, "b":{"x":1,"y":0} }] }""",
            "0.5");

        Assert.True(result.IsFailure);
        Assert.Contains("must exactly match fields of Triangle", result.ErrorMessage);
    }

    [Fact]
    public void Parse_RejectsUnknownCustomFieldType()
    {
        var result = FunctionJudgeSpecParser.Parse("""
            {
              "types": [
                { "name": "Triangle", "fields": [{ "name": "a", "type": "Point" }] }
              ],
              "functionName": "solve",
              "returnType": "int",
              "parameters": [],
              "supportedLanguages": ["cpp17"]
            }
            """);

        Assert.True(result.IsFailure);
        Assert.Contains("custom field type", result.ErrorMessage);
    }

    [Fact]
    public void Parse_RejectsCustomTypeDependencyCycle()
    {
        var result = FunctionJudgeSpecParser.Parse("""
            {
              "types": [
                { "name": "A", "fields": [{ "name": "b", "type": "B" }] },
                { "name": "B", "fields": [{ "name": "a", "type": "A" }] }
              ],
              "functionName": "solve",
              "returnType": "int",
              "parameters": [],
              "supportedLanguages": ["cpp17"]
            }
            """);

        Assert.True(result.IsFailure);
        Assert.Contains("dependency cycle", result.ErrorMessage);
    }

    [Fact]
    public void Parse_RejectsArrayFieldInsideCustomType()
    {
        var result = FunctionJudgeSpecParser.Parse("""
            {
              "types": [
                { "name": "Polygon", "fields": [{ "name": "points", "type": "double[]" }] }
              ],
              "functionName": "solve",
              "returnType": "int",
              "parameters": [],
              "supportedLanguages": ["cpp17"]
            }
            """);

        Assert.True(result.IsFailure);
        Assert.Contains("field arrays are not supported yet", result.ErrorMessage);
    }

    [Fact]
    public void Cpp17Builder_GeneratesCustomStructArrayHarness()
    {
        var source = """
            struct Point { double x; double y; };
            struct Triangle { Point a; Point b; Point c; };
            class Solution {
            public:
                double scoreTriangles(vector<Triangle>& triangles) { return 0.5; }
            };
            """;

        var request = FunctionJudgeTestData.CreateRequest(GeometrySpec, Arguments, "0.5", JudgeLanguage.Cpp17, source);

        var result = new Cpp17FunctionJudgeCodeBuilder().Build(request);

        Assert.True(result.IsSuccess);
        Assert.Contains("vector<Triangle> __oj_arg_0", result.Value!.SourceCode);
        Assert.Contains("Triangle{Point{", result.Value.SourceCode);
        Assert.Contains("bool __oj_equal_value(const Triangle& left, const Triangle& right)", result.Value.SourceCode);
    }

    [Fact]
    public void CSharpBuilder_GeneratesCustomStructArrayHarness()
    {
        var source = """
            public class Point { public double x; public double y; }
            public class Triangle { public Point a = new(); public Point b = new(); public Point c = new(); }
            public class Solution {
                public double ScoreTriangles(Triangle[] triangles) => 0.5;
            }
            """;

        var request = FunctionJudgeTestData.CreateRequest(GeometrySpec, Arguments, "0.5", JudgeLanguage.CSharp, source);

        var result = new CSharpFunctionJudgeCodeBuilder().Build(request);

        Assert.True(result.IsSuccess);
        Assert.Contains("Triangle[] __oj_arg_0", result.Value!.SourceCode);
        Assert.Contains("new Triangle", result.Value.SourceCode);
        Assert.Contains("private static bool __oj_compare(Triangle actual, Triangle expected)", result.Value.SourceCode);
    }

    [Fact]
    public void C11Builder_GeneratesCustomStructArrayHarness()
    {
        var source = """
            typedef struct Point { double x; double y; } Point;
            typedef struct Triangle { Point a; Point b; Point c; } Triangle;
            double scoreTriangles(Triangle* triangles, int trianglesSize) { return 0.5; }
            """;

        var request = FunctionJudgeTestData.CreateRequest(GeometrySpec, Arguments, "0.5", JudgeLanguage.C11, source);

        var result = new C11FunctionJudgeCodeBuilder().Build(request);

        Assert.True(result.IsSuccess);
        Assert.Contains("Triangle __oj_arg_0[]", result.Value!.SourceCode);
        Assert.Contains("static int __oj_compare_Triangle", result.Value.SourceCode);
        Assert.Contains("static void __oj_print_Triangle_json", result.Value.SourceCode);
    }

    [Fact]
    public void C11Builder_RejectsStringFieldInCustomStruct()
    {
        const string specJson = """
            {
              "types": [
                { "name": "Person", "fields": [{ "name": "name", "type": "string" }] }
              ],
              "functionName": "solve",
              "returnType": "int",
              "parameters": [{ "name": "person", "type": "Person" }],
              "supportedLanguages": ["cpp17", "csharp"]
            }
            """;

        var request = FunctionJudgeTestData.CreateRequest(
            specJson,
            """{ "person": { "name": "A" } }""",
            "1",
            JudgeLanguage.C11,
            "typedef struct Person { int unused; } Person; int solve(Person person) { return 1; }");

        var result = new C11FunctionJudgeCodeBuilder().Build(request);

        Assert.True(result.IsFailure);
        Assert.Contains("does not support custom field type", result.ErrorMessage);
    }
}
