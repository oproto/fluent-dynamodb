using System.Reflection;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Unit tests for the private static SegmentsCanMatch helper method.
/// Uses reflection to invoke the method directly.
///
/// Validates: Requirements 2.1
/// </summary>
public class SegmentsCanMatchTests
{
    private static bool InvokeSegmentsCanMatch(string segmentA, string segmentB)
    {
        var method = typeof(PatternOverlapAnalyzer)
            .GetMethod("SegmentsCanMatch", BindingFlags.NonPublic | BindingFlags.Static);
        return (bool)method!.Invoke(null, new object[] { segmentA, segmentB })!;
    }

    [Fact]
    public void DistinguishingSegments_NeitherIsSubstring_ReturnsFalse()
    {
        // "#DEDUCTION#" vs "#GARNISHMENT#" → false (distinguishing)
        var result = InvokeSegmentsCanMatch("#DEDUCTION#", "#GARNISHMENT#");

        result.Should().BeFalse();
    }

    [Fact]
    public void SubstringRelationship_OneContainsOther_ReturnsTrue()
    {
        // "#LINE#" vs "#LINE#ITEM#" → true (substring relationship)
        var result = InvokeSegmentsCanMatch("#LINE#", "#LINE#ITEM#");

        result.Should().BeTrue();
    }

    [Fact]
    public void IdenticalSegments_ReturnsTrue()
    {
        // "EMPLOYEE#" vs "EMPLOYEE#" → true (identical)
        var result = InvokeSegmentsCanMatch("EMPLOYEE#", "EMPLOYEE#");

        result.Should().BeTrue();
    }
}
