using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for ExactMatch precedence in PatternOverlapAnalyzer.
/// 
/// **Feature: discriminator-enhancement, Property 2: ExactMatch always scores higher than any wildcard pattern**
/// **Validates: Requirements 2.4**
/// 
/// For any discriminator pattern containing at least one wildcard character,
/// the specificity score of an ExactMatch discriminator SHALL be strictly greater
/// than the wildcard pattern's specificity score.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class ExactMatchPrecedencePropertyTests
{
    /// <summary>
    /// **Feature: discriminator-enhancement, Property 2: ExactMatch always scores higher than any wildcard pattern**
    /// **Validates: Requirements 2.4**
    /// 
    /// Property: For any wildcard pattern, an ExactMatch config's specificity score
    /// is strictly greater than the wildcard pattern's specificity score.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExactMatch_AlwaysScoresHigherThanAnyWildcardPattern()
    {
        // Generate arbitrary wildcard patterns: non-empty strings containing at least one '*'
        var wildcardPatternArb = GenerateWildcardPattern();

        // Generate arbitrary non-null ExactValue strings for the ExactMatch config
        var exactValueArb = Arb.From(Gen.Elements(
            "USER", "ORDER#123", "INVOICE", "A", "some-long-exact-value#with#segments"));

        // Generate arbitrary wildcard strategies (anything except ExactMatch and None)
        var wildcardStrategyArb = Arb.From(Gen.Elements(
            DiscriminatorStrategy.StartsWith,
            DiscriminatorStrategy.EndsWith,
            DiscriminatorStrategy.Contains,
            DiscriminatorStrategy.Complex));

        return Prop.ForAll(wildcardPatternArb, exactValueArb, wildcardStrategyArb,
            (wildcardPattern, exactValue, wildcardStrategy) =>
            {
                // Arrange: create an ExactMatch config
                var exactMatchConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Strategy = DiscriminatorStrategy.ExactMatch,
                    ExactValue = exactValue
                };

                // Arrange: create a wildcard config with the generated pattern and strategy
                var wildcardConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Strategy = wildcardStrategy,
                    Pattern = wildcardPattern
                };

                // Act
                var exactMatchScore = PatternOverlapAnalyzer.ComputeSpecificityScore(exactMatchConfig);
                var wildcardScore = PatternOverlapAnalyzer.ComputeSpecificityScore(wildcardConfig);

                // Assert: ExactMatch score must be strictly greater than any wildcard score
                return exactMatchScore > wildcardScore;
            });
    }

    /// <summary>
    /// **Feature: discriminator-enhancement, Property 2: ExactMatch always scores higher than any wildcard pattern**
    /// **Validates: Requirements 2.4**
    /// 
    /// Property: ExactMatch score equals int.MaxValue regardless of ExactValue content.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExactMatch_AlwaysReturnsIntMaxValue()
    {
        // Generate arbitrary strings as ExactValue (including null and empty)
        var exactValueArb = Arb.From(
            Gen.OneOf(
                Gen.Constant((string?)null),
                Gen.Constant((string?)""),
                Arb.From<NonEmptyString>().Generator.Select(s => (string?)s.Get)
            ));

        return Prop.ForAll(exactValueArb, (string? exactValue) =>
        {
            var config = new DiscriminatorConfig
            {
                PropertyName = "pk",
                Strategy = DiscriminatorStrategy.ExactMatch,
                ExactValue = exactValue
            };

            var score = PatternOverlapAnalyzer.ComputeSpecificityScore(config);

            return score == int.MaxValue;
        });
    }

    /// <summary>
    /// Generates arbitrary wildcard patterns containing at least one '*' character.
    /// Produces patterns like "PREFIX#*", "*#SUFFIX", "*#MIDDLE#*", "A#*#B#*#C#*".
    /// </summary>
    private static Arbitrary<string> GenerateWildcardPattern()
    {
        var gen = Gen.OneOf(
            // StartsWith patterns: "LITERAL*"
            Arb.From<NonEmptyString>().Generator.Select(s =>
                s.Get.Replace("*", "") + "#*"),
            // EndsWith patterns: "*LITERAL"
            Arb.From<NonEmptyString>().Generator.Select(s =>
                "*#" + s.Get.Replace("*", "")),
            // Contains patterns: "*LITERAL*"
            Arb.From<NonEmptyString>().Generator.Select(s =>
                "*#" + s.Get.Replace("*", "") + "#*"),
            // Complex patterns with multiple segments: "A#*#B#*"
            Gen.Two(Arb.From<NonEmptyString>().Generator)
                .Select(pair => pair.Item1.Get.Replace("*", "") + "#*#" + pair.Item2.Get.Replace("*", "") + "#*"),
            // Triple segment patterns: "A#*#B#*#C#*"
            Gen.Three(Arb.From<NonEmptyString>().Generator)
                .Select(triple => triple.Item1.Get.Replace("*", "") + "#*#" +
                                  triple.Item2.Get.Replace("*", "") + "#*#" +
                                  triple.Item3.Get.Replace("*", "") + "#*")
        );

        return gen.ToArbitrary();
    }
}
