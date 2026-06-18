using System.Reflection;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Unit tests for the private static HasSameWildcardStructure helper method.
/// Uses reflection to invoke the method directly.
///
/// Validates: Requirements 9.1, 9.2, 9.3
/// </summary>
public class HasSameWildcardStructureTests
{
    private static bool InvokeHasSameWildcardStructure(string patternA, string patternB)
    {
        var method = typeof(PatternOverlapAnalyzer)
            .GetMethod("HasSameWildcardStructure", BindingFlags.NonPublic | BindingFlags.Static);
        return (bool)method!.Invoke(null, new object[] { patternA, patternB })!;
    }

    [Fact]
    public void SameStructure_BothStartLiteral_BothEndWildcard_ReturnsTrue()
    {
        // "EMPLOYEE#*#DEDUCTION#*" and "EMPLOYEE#*#GARNISHMENT#*"
        // Both start with literal, both end with wildcard
        var result = InvokeHasSameWildcardStructure("EMPLOYEE#*#DEDUCTION#*", "EMPLOYEE#*#GARNISHMENT#*");

        result.Should().BeTrue();
    }

    [Fact]
    public void DifferentStructure_OneStartsLiteral_OtherStartsWildcard_ReturnsFalse()
    {
        // "EMPLOYEE#*#DEDUCTION#*" starts with literal
        // "*#DEDUCTION#*" starts with wildcard
        var result = InvokeHasSameWildcardStructure("EMPLOYEE#*#DEDUCTION#*", "*#DEDUCTION#*");

        result.Should().BeFalse();
    }

    [Fact]
    public void SameStructure_BothStartWithWildcard_ReturnsTrue()
    {
        // "*#A#*" and "*#B#*" both start with wildcard, both end with wildcard
        var result = InvokeHasSameWildcardStructure("*#A#*", "*#B#*");

        result.Should().BeTrue();
    }
}
