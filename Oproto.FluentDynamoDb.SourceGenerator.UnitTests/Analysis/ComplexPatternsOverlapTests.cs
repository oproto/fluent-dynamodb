using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Unit tests for ComplexPatternsOverlap logic, exercised through the public PatternsOverlap method.
/// Tests cover same-structure non-overlap, same-structure overlap, different-structure overlap
/// (subsumption), different-structure non-overlap, and conservative fallback scenarios.
///
/// **Validates: Requirements 2.1, 2.2, 2.3, 3.1, 3.2, 7.1, 7.2**
/// </summary>
public class ComplexPatternsOverlapTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Same-structure: Non-overlapping (distinguishing segment)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PatternsOverlap_Complex_SameStructure_DistinguishingSegment_ReturnsFalse()
    {
        // Requirement 2.1: Same-structure patterns with a distinguishing segment are non-overlapping
        // "EMPLOYEE#*#DEDUCTION#*" vs "EMPLOYEE#*#GARNISHMENT#*"
        // Segments: ["EMPLOYEE#", "#DEDUCTION#"] vs ["EMPLOYEE#", "#GARNISHMENT#"]
        // Second segment pair is distinguishing (no substring relationship)
        var configA = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Complex,
            Pattern = "EMPLOYEE#*#DEDUCTION#*"
        };
        var configB = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Complex,
            Pattern = "EMPLOYEE#*#GARNISHMENT#*"
        };

        var result = PatternOverlapAnalyzer.PatternsOverlap(configA, configB);

        result.Should().BeFalse("'#DEDUCTION#' and '#GARNISHMENT#' are distinguishing segments with no substring relationship");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Same-structure: Overlapping (substring relationship in segments)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PatternsOverlap_Complex_SameStructure_SubstringSegment_ReturnsTrue()
    {
        // Requirement 2.2: Same-structure patterns where all segment pairs have substring relationship → overlap
        // "EMPLOYEE#*#LINE#*" vs "EMPLOYEE#*#LINE#ITEM#*"
        // Segments: ["EMPLOYEE#", "#LINE#"] vs ["EMPLOYEE#", "#LINE#ITEM#"]
        // First pair: "EMPLOYEE#" == "EMPLOYEE#" → compatible
        // Second pair: "#LINE#" is a substring of "#LINE#ITEM#" → compatible
        // All segments compatible → overlap
        var configA = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Complex,
            Pattern = "EMPLOYEE#*#LINE#*"
        };
        var configB = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Complex,
            Pattern = "EMPLOYEE#*#LINE#ITEM#*"
        };

        var result = PatternOverlapAnalyzer.PatternsOverlap(configA, configB);

        result.Should().BeTrue("'#LINE#' is a substring of '#LINE#ITEM#', so segments are compatible");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Different-structure: Overlap (subsumption - StartsWith vs Complex)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PatternsOverlap_StartsWith_Vs_Complex_Subsumption_ReturnsTrue()
    {
        // Requirement 3.1, 3.2: A broader StartsWith pattern subsumes a more-specific Complex pattern
        // "EMPLOYEE#*" (StartsWith) vs "EMPLOYEE#*#DEDUCTION#*" (Complex)
        // The StartsWith pattern's segment "EMPLOYEE#" appears in the Complex pattern text
        var configA = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.StartsWith,
            Pattern = "EMPLOYEE#*"
        };
        var configB = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Complex,
            Pattern = "EMPLOYEE#*#DEDUCTION#*"
        };

        var result = PatternOverlapAnalyzer.PatternsOverlap(configA, configB);

        result.Should().BeTrue("StartsWith 'EMPLOYEE#*' subsumes the Complex pattern 'EMPLOYEE#*#DEDUCTION#*'");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Different-structure: Non-overlap (Complex vs Contains, no relationship)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PatternsOverlap_Complex_Vs_Contains_NoRelationship_ReturnsFalse()
    {
        // Requirement 3.1, 3.2: Different-structure patterns with no segment relationship → non-overlapping
        // "EMPLOYEE#*#PAYRATE#*" (Complex) vs "*#DEDUCTION#*" (Contains)
        // Complex segments: ["EMPLOYEE#", "#PAYRATE#"]
        // Contains segments: ["#DEDUCTION#"]
        // Contains has fewer segments; check if "#DEDUCTION#" appears in "EMPLOYEE#*#PAYRATE#*" → no
        var configA = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Complex,
            Pattern = "EMPLOYEE#*#PAYRATE#*"
        };
        var configB = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Contains,
            Pattern = "*#DEDUCTION#*"
        };

        var result = PatternOverlapAnalyzer.PatternsOverlap(configA, configB);

        result.Should().BeFalse("'#DEDUCTION#' does not appear in 'EMPLOYEE#*#PAYRATE#*'");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Conservative fallback: Empty segments → true
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PatternsOverlap_Complex_EmptySegments_ReturnsTrue()
    {
        // Requirement 7.1, 7.2: Conservative fallback when structural analysis is inconclusive
        // A pattern like "***" produces no non-empty segments → conservative true
        var configA = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Complex,
            Pattern = "***"
        };
        var configB = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Complex,
            Pattern = "EMPLOYEE#*#DEDUCTION#*"
        };

        var result = PatternOverlapAnalyzer.PatternsOverlap(configA, configB);

        result.Should().BeTrue("empty segments trigger the conservative fallback, assuming overlap");
    }
}
