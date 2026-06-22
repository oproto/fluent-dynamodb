using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Unit tests for the modified SameStrategyOverlap Contains behavior,
/// exercised through the public PatternsOverlap method.
/// Also verifies that StartsWith and EndsWith behavior remains unchanged.
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 5.1, 5.2**
/// </summary>
public class ContainsOverlapTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Contains: Non-overlapping patterns (no substring relationship)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PatternsOverlap_Contains_DifferentLiterals_ReturnsFalse()
    {
        // Requirement 1.1: "*#DEDUCTION#*" vs "*#GARNISHMENT#*" → false
        var configA = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Contains,
            Pattern = "*#DEDUCTION#*"
        };
        var configB = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Contains,
            Pattern = "*#GARNISHMENT#*"
        };

        var result = PatternOverlapAnalyzer.PatternsOverlap(configA, configB);

        result.Should().BeFalse("neither '#DEDUCTION#' nor '#GARNISHMENT#' is a substring of the other");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Contains: Overlapping patterns (substring relationship)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PatternsOverlap_Contains_SubstringRelationship_ReturnsTrue()
    {
        // Requirement 1.2: "*ORDER*" vs "*ORD*" → true ("ORD" is a substring of "ORDER")
        var configA = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Contains,
            Pattern = "*ORDER*"
        };
        var configB = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Contains,
            Pattern = "*ORD*"
        };

        var result = PatternOverlapAnalyzer.PatternsOverlap(configA, configB);

        result.Should().BeTrue("'ORD' is a substring of 'ORDER'");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Contains: Identical patterns
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PatternsOverlap_Contains_IdenticalPatterns_ReturnsTrue()
    {
        // Requirement 1.3: "*#PAYRATE#*" vs "*#PAYRATE#*" → true
        var configA = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Contains,
            Pattern = "*#PAYRATE#*"
        };
        var configB = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.Contains,
            Pattern = "*#PAYRATE#*"
        };

        var result = PatternOverlapAnalyzer.PatternsOverlap(configA, configB);

        result.Should().BeTrue("identical patterns always overlap");
    }

    // ──────────────────────────────────────────────────────────────────────
    // StartsWith: Behavior unchanged
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PatternsOverlap_StartsWith_PrefixRelationship_ReturnsTrue()
    {
        // Requirement 5.1: "EMPLOYEE#*" vs "EMPLOYEE#DETAIL#*" → true (prefix relationship)
        var configA = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.StartsWith,
            Pattern = "EMPLOYEE#*"
        };
        var configB = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.StartsWith,
            Pattern = "EMPLOYEE#DETAIL#*"
        };

        var result = PatternOverlapAnalyzer.PatternsOverlap(configA, configB);

        result.Should().BeTrue("'EMPLOYEE#' is a prefix of 'EMPLOYEE#DETAIL#'");
    }

    [Fact]
    public void PatternsOverlap_StartsWith_NoPrefixRelationship_ReturnsFalse()
    {
        // Requirement 5.1: "EMPLOYEE#*" vs "ORDER#*" → false (no prefix relationship)
        var configA = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.StartsWith,
            Pattern = "EMPLOYEE#*"
        };
        var configB = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.StartsWith,
            Pattern = "ORDER#*"
        };

        var result = PatternOverlapAnalyzer.PatternsOverlap(configA, configB);

        result.Should().BeFalse("neither 'EMPLOYEE#' nor 'ORDER#' is a prefix of the other");
    }

    // ──────────────────────────────────────────────────────────────────────
    // EndsWith: Behavior unchanged
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void PatternsOverlap_EndsWith_NoSuffixRelationship_ReturnsFalse()
    {
        // Requirement 5.2: "*#ACTIVE" vs "*#PENDING" → false (no suffix relationship)
        var configA = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.EndsWith,
            Pattern = "*#ACTIVE"
        };
        var configB = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Strategy = DiscriminatorStrategy.EndsWith,
            Pattern = "*#PENDING"
        };

        var result = PatternOverlapAnalyzer.PatternsOverlap(configA, configB);

        result.Should().BeFalse("neither '#ACTIVE' nor '#PENDING' is a suffix of the other");
    }
}
