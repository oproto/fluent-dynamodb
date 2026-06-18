using System.Reflection;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Unit tests for private helper methods in PatternOverlapAnalyzer.
/// Uses reflection to invoke private static methods directly.
///
/// **Validates: Requirements 8.1, 8.2, 8.3**
/// </summary>
public class PatternOverlapAnalyzerTests
{
    // ──────────────────────────────────────────────────────────────────────
    // GetLiteralSegments Tests
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetLiteralSegments_ComplexPatternWithTwoSegments_ReturnsCorrectSegments()
    {
        // Requirement 8.2: "EMPLOYEE#*#DEDUCTION#*" → ["EMPLOYEE#", "#DEDUCTION#"]
        var result = InvokeGetLiteralSegments("EMPLOYEE#*#DEDUCTION#*");

        result.Should().BeEquivalentTo(new[] { "EMPLOYEE#", "#DEDUCTION#" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void GetLiteralSegments_ContainsPattern_ReturnsSingleSegment()
    {
        // Requirement 8.3: "*#DEDUCTION#*" → ["#DEDUCTION#"]
        var result = InvokeGetLiteralSegments("*#DEDUCTION#*");

        result.Should().BeEquivalentTo(new[] { "#DEDUCTION#" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void GetLiteralSegments_StartsWithPattern_ReturnsSingleSegment()
    {
        // "EMPLOYEE#*" → ["EMPLOYEE#"]
        var result = InvokeGetLiteralSegments("EMPLOYEE#*");

        result.Should().BeEquivalentTo(new[] { "EMPLOYEE#" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void GetLiteralSegments_AllWildcards_ReturnsEmptyArray()
    {
        // Edge case: all-wildcards pattern → empty array
        var result = InvokeGetLiteralSegments("***");

        result.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Reflection Helper
    // ──────────────────────────────────────────────────────────────────────

    private static string[] InvokeGetLiteralSegments(string pattern)
    {
        var method = typeof(PatternOverlapAnalyzer)
            .GetMethod("GetLiteralSegments", BindingFlags.NonPublic | BindingFlags.Static);
        return (string[])method!.Invoke(null, new object[] { pattern })!;
    }
}
