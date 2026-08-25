using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Bug condition exploration test for Complex Pattern Exclusion Contains Separator issue.
///
/// When PatternOverlapAnalyzer.CreateExclusionPattern() decomposes a Complex pattern like "CAP#*#*"
/// into segments, the internal segment between adjacent wildcards is just the separator character "#".
/// This produces a Contains("#") exclusion guard that is always true for any value already passing
/// StartsWith("CAP#"), making the less-specific entity invisible to all queries.
///
/// This test is EXPECTED TO FAIL on unfixed code — failure confirms the bug exists.
/// When the fix is applied, this test will PASS, confirming the expected behavior.
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5**
/// </summary>
public class ComplexPatternExclusionBugConditionTests
{
    /// <summary>
    /// Creates a pair of entities simulating the bare-separator bug condition.
    /// Less-specific entity uses a StartsWith pattern, more-specific uses a Complex pattern
    /// where the internal segment is just the separator character.
    /// </summary>
    private static (EntityModel lessSpecific, EntityModel moreSpecific) CreateOverlappingEntities(
        string prefix, string separator, string lessPattern, string morePattern)
    {
        var lessStrategy = DiscriminatorAnalyzer.DeterminePatternStrategy(lessPattern);
        var moreStrategy = DiscriminatorAnalyzer.DeterminePatternStrategy(morePattern);

        var lessSpecific = new EntityModel
        {
            ClassName = "LessSpecificEntity",
            TableName = "test-table",
            Namespace = "TestNamespace",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = lessPattern,
                Strategy = lessStrategy,
                IsAutoDerived = true
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    NormalizedKeyFormat = "{0}",
                    DerivedDiscriminatorPattern = null
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    NormalizedKeyFormat = $"{prefix}{separator}{{0}}",
                    DerivedDiscriminatorPattern = lessPattern
                }
            }
        };

        var moreSpecific = new EntityModel
        {
            ClassName = "MoreSpecificEntity",
            TableName = "test-table",
            Namespace = "TestNamespace",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = morePattern,
                Strategy = moreStrategy,
                IsAutoDerived = true
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    NormalizedKeyFormat = "{0}",
                    DerivedDiscriminatorPattern = null
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    NormalizedKeyFormat = $"{prefix}{separator}{{0}}{separator}{{1}}",
                    DerivedDiscriminatorPattern = morePattern
                }
            }
        };

        return (lessSpecific, moreSpecific);
    }

    /// <summary>
    /// Hash separator (#): Pattern "CAP#*#*" with less-specific "CAP#*" — verify the exclusion
    /// correctly discriminates "CAP#capability1" (not excluded) from "CAP#svc1#cap1" (excluded).
    ///
    /// The bug produces Contains("#") which is always true after StartsWith("CAP#").
    /// The fix should produce a positional check that only matches when "#" appears AFTER the prefix.
    /// </summary>
    [Fact]
    public void HashSeparator_ExclusionShouldDiscriminate_SingleSegmentFromMultiSegment()
    {
        // Arrange
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("CAP", "#", "CAP#*", "CAP#*#*");
        var tableEntities = new List<EntityModel> { lessSpecific, moreSpecific };

        // Act — run the analyzer to populate exclusion patterns
        PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert — the less-specific entity should have an exclusion pattern
        lessSpecific.Discriminator!.OverlappingPatterns.Should().NotBeEmpty(
            "less-specific entity 'CAP#*' should have an exclusion for more-specific 'CAP#*#*'");

        var exclusion = lessSpecific.Discriminator.OverlappingPatterns[0];

        // The exclusion should NOT be a bare Contains("#") because that is tautological
        // after StartsWith("CAP#"). If it IS Contains("#") with no offset, the bug exists.
        // Expected behavior: either the exclusion uses a positional approach (OffsetIndex > 0)
        // or it uses a different strategy that actually discriminates.
        var isTautological = exclusion.Strategy == DiscriminatorStrategy.Contains
                             && exclusion.LiteralText == "#";

        isTautological.Should().BeFalse(
            "exclusion for 'CAP#*#*' should NOT be a bare Contains(\"#\") because any value " +
            "passing StartsWith(\"CAP#\") inherently contains '#' — making the exclusion " +
            "tautological and the less-specific entity invisible to all queries");
    }

    /// <summary>
    /// Underscore separator (_): Pattern "CAP_*_*" with less-specific "CAP_*" — verify the exclusion
    /// correctly discriminates with '_' separator.
    /// </summary>
    [Fact]
    public void UnderscoreSeparator_ExclusionShouldDiscriminate_SingleSegmentFromMultiSegment()
    {
        // Arrange
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("CAP", "_", "CAP_*", "CAP_*_*");
        var tableEntities = new List<EntityModel> { lessSpecific, moreSpecific };

        // Act
        PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert
        lessSpecific.Discriminator!.OverlappingPatterns.Should().NotBeEmpty(
            "less-specific entity 'CAP_*' should have an exclusion for more-specific 'CAP_*_*'");

        var exclusion = lessSpecific.Discriminator.OverlappingPatterns[0];

        var isTautological = exclusion.Strategy == DiscriminatorStrategy.Contains
                             && exclusion.LiteralText == "_";

        isTautological.Should().BeFalse(
            "exclusion for 'CAP_*_*' should NOT be a bare Contains(\"_\") because any value " +
            "passing StartsWith(\"CAP_\") inherently contains '_' — making the exclusion " +
            "tautological and the less-specific entity invisible to all queries");
    }

    /// <summary>
    /// Colon separator (:): Pattern "NS:*:*" with less-specific "NS:*" — verify the exclusion
    /// correctly discriminates with ':' separator.
    /// </summary>
    [Fact]
    public void ColonSeparator_ExclusionShouldDiscriminate_SingleSegmentFromMultiSegment()
    {
        // Arrange
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("NS", ":", "NS:*", "NS:*:*");
        var tableEntities = new List<EntityModel> { lessSpecific, moreSpecific };

        // Act
        PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert
        lessSpecific.Discriminator!.OverlappingPatterns.Should().NotBeEmpty(
            "less-specific entity 'NS:*' should have an exclusion for more-specific 'NS:*:*'");

        var exclusion = lessSpecific.Discriminator.OverlappingPatterns[0];

        var isTautological = exclusion.Strategy == DiscriminatorStrategy.Contains
                             && exclusion.LiteralText == ":";

        isTautological.Should().BeFalse(
            "exclusion for 'NS:*:*' should NOT be a bare Contains(\":\") because any value " +
            "passing StartsWith(\"NS:\") inherently contains ':' — making the exclusion " +
            "tautological and the less-specific entity invisible to all queries");
    }

    /// <summary>
    /// Test that IsTautologicalExclusion detects semantic subsumption: Contains("#") is
    /// tautological when positive match is StartsWith("CAP#").
    ///
    /// Currently IsTautologicalExclusion only checks identity (same strategy AND same literal).
    /// It should also detect when the exclusion literal is inherently contained in the prefix.
    /// </summary>
    [Fact]
    public void IsTautologicalExclusion_ShouldDetect_SemanticSubsumption()
    {
        // Arrange — create entities where the exclusion Contains("#") is semantically
        // subsumed by the positive StartsWith("CAP#") check
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("CAP", "#", "CAP#*", "CAP#*#*");
        var tableEntities = new List<EntityModel> { lessSpecific, moreSpecific };

        // Act — run the analyzer
        PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert — if the analyzer detects the tautology, the exclusion should either:
        // 1. Not be added at all (filtered out as tautological), OR
        // 2. Be modified to use a positional approach (not bare Contains)
        //
        // On unfixed code, the exclusion IS added as Contains("#") because
        // IsTautologicalExclusion fails to detect the subsumption.
        // The bug manifests as: the exclusion exists AND is a bare Contains of the separator.
        var exclusions = lessSpecific.Discriminator!.OverlappingPatterns;

        if (exclusions.Count > 0)
        {
            var exclusion = exclusions[0];
            // If an exclusion exists, it should NOT be a bare separator that's tautological
            var isBareSeparatorExclusion = exclusion.Strategy == DiscriminatorStrategy.Contains
                                           && exclusion.LiteralText == "#"
                                           && exclusion.LiteralText.Length <= 1;

            isBareSeparatorExclusion.Should().BeFalse(
                "IsTautologicalExclusion should detect that Contains(\"#\") is semantically " +
                "subsumed by StartsWith(\"CAP#\") — the separator is already guaranteed " +
                "present in any matching value");
        }
        // If no exclusions exist, the tautology was detected and filtered — that's correct
    }

    /// <summary>
    /// Test that GenerateComplexPatternCheck() for "CAP#*#*" does NOT produce a
    /// non-discriminating Contains("#") clause. The generated code should be just
    /// StartsWith("CAP#") without the redundant Contains.
    /// </summary>
    [Fact]
    public void GenerateComplexPatternCheck_ShouldNotProduceNonDiscriminatingContains()
    {
        // Arrange — create a more-specific entity with pattern "CAP#*#*"
        var moreSpecific = new EntityModel
        {
            ClassName = "MoreSpecificEntity",
            TableName = "test-table",
            Namespace = "TestNamespace",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "CAP#*#*",
                Strategy = DiscriminatorStrategy.Complex,
                IsAutoDerived = true
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    NormalizedKeyFormat = "{0}",
                    DerivedDiscriminatorPattern = null
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    NormalizedKeyFormat = "CAP#{0}#{1}",
                    DerivedDiscriminatorPattern = "CAP#*#*"
                }
            }
        };

        // Act — generate the entity implementation code
        var generatedSource = MapperGenerator.GenerateEntityImplementation(moreSpecific);

        // Assert — the generated code should contain StartsWith("CAP#") for the positive check
        generatedSource.Should().Contain(
            "StartsWith(\"CAP#\")",
            "Generated code should check StartsWith(\"CAP#\") as the primary discriminator");

        // Assert — the generated code should NOT contain Contains("#") as a non-discriminating clause
        // For pattern "CAP#*#*", the Contains("#") adds zero filtering power since
        // StartsWith("CAP#") already implies the string contains "#"
        generatedSource.Should().NotContain(
            "Contains(\"#\")",
            "Generated code for 'CAP#*#*' should NOT include Contains(\"#\") because it adds " +
            "zero discrimination power — any value passing StartsWith(\"CAP#\") already contains '#'. " +
            "The correct behavior is to omit non-discriminating Contains clauses.");
    }

    /// <summary>
    /// Comprehensive verification: The generated MatchesEntity for the less-specific entity
    /// with hash separator should correctly discriminate values.
    /// "CAP#capability1" (single segment after prefix) should NOT be excluded.
    /// "CAP#svc1#cap1" (multiple segments) SHOULD be excluded.
    /// </summary>
    [Fact]
    public void HashSeparator_GeneratedMatchesEntity_ShouldNotExcludeAllValues()
    {
        // Arrange
        var (lessSpecific, moreSpecific) = CreateOverlappingEntities("CAP", "#", "CAP#*", "CAP#*#*");
        var tableEntities = new List<EntityModel> { lessSpecific, moreSpecific };
        PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act — generate the less-specific entity implementation
        var generatedSource = MapperGenerator.GenerateEntityImplementation(lessSpecific);

        // Assert — The generated MatchesEntity should NOT contain a bare Contains("#") exclusion
        // that would make ALL values fail (since any value matching StartsWith("CAP#") contains "#")
        //
        // On unfixed code, the generated exclusion is:
        //   if (discriminatorValue.S.Contains("#")) return false;
        // This makes MatchesEntity always return false for all valid values.
        //
        // Expected behavior: The exclusion should use a positional check like:
        //   if (discriminatorValue.S.IndexOf("#", 4) >= 0) return false;
        // This correctly excludes "CAP#svc1#cap1" but NOT "CAP#capability1"

        // The generated code should NOT have a Contains("#") exclusion check
        // (specifically in the exclusion section, not in the positive match section)
        var hasContainsSeparatorExclusion = generatedSource.Contains("Contains(\"#\")") 
                                            && generatedSource.Contains("return false");

        // If Contains("#") appears as an exclusion guard followed by return false,
        // it means the bug exists — all values would be excluded
        hasContainsSeparatorExclusion.Should().BeFalse(
            "Generated MatchesEntity for 'CAP#*' entity should NOT use Contains(\"#\") as an " +
            "exclusion guard because it is tautological — every value passing StartsWith(\"CAP#\") " +
            "inherently contains '#', making the entity invisible to all queries");
    }
}
