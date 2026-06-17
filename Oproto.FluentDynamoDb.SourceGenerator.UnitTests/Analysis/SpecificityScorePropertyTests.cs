using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for PatternOverlapAnalyzer.ComputeSpecificityScore.
///
/// Feature: discriminator-enhancement, Property 1: Specificity score equals non-empty literal segment count
/// **Validates: Requirements 1.3, 2.2**
/// </summary>
public class SpecificityScorePropertyTests
{
    /// <summary>
    /// For any valid discriminator pattern string containing wildcard characters,
    /// the computed specificity score SHALL equal the number of non-empty strings
    /// produced by splitting the pattern on the '*' character.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpecificityScore_EqualsNonEmptyLiteralSegmentCount()
    {
        return Prop.ForAll(
            GenPatternWithWildcards().ToArbitrary(),
            pattern =>
            {
                var config = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = pattern,
                    Strategy = InferNonExactStrategy(pattern)
                };

                var expectedScore = pattern.Split('*').Count(s => s.Length > 0);
                var actualScore = PatternOverlapAnalyzer.ComputeSpecificityScore(config);

                return expectedScore == actualScore;
            });
    }

    /// <summary>
    /// For patterns using StartsWith strategy (pattern ends with *),
    /// the score equals the count of non-empty literal segments.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StartsWithPattern_ScoreEqualsNonEmptySegmentCount()
    {
        return Prop.ForAll(
            GenStartsWithPattern().ToArbitrary(),
            pattern =>
            {
                var config = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = pattern,
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                var expectedScore = pattern.Split('*').Count(s => s.Length > 0);
                var actualScore = PatternOverlapAnalyzer.ComputeSpecificityScore(config);

                return expectedScore == actualScore;
            });
    }

    /// <summary>
    /// For patterns using EndsWith strategy (pattern starts with *),
    /// the score equals the count of non-empty literal segments.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EndsWithPattern_ScoreEqualsNonEmptySegmentCount()
    {
        return Prop.ForAll(
            GenEndsWithPattern().ToArbitrary(),
            pattern =>
            {
                var config = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = pattern,
                    Strategy = DiscriminatorStrategy.EndsWith
                };

                var expectedScore = pattern.Split('*').Count(s => s.Length > 0);
                var actualScore = PatternOverlapAnalyzer.ComputeSpecificityScore(config);

                return expectedScore == actualScore;
            });
    }

    /// <summary>
    /// For patterns using Complex strategy (multiple wildcards),
    /// the score equals the count of non-empty literal segments.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComplexPattern_ScoreEqualsNonEmptySegmentCount()
    {
        return Prop.ForAll(
            GenComplexPattern().ToArbitrary(),
            pattern =>
            {
                var config = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = pattern,
                    Strategy = DiscriminatorStrategy.Complex
                };

                var expectedScore = pattern.Split('*').Count(s => s.Length > 0);
                var actualScore = PatternOverlapAnalyzer.ComputeSpecificityScore(config);

                return expectedScore == actualScore;
            });
    }

    /// <summary>
    /// The specificity score is independent of the PropertyName —
    /// changing the property name does not affect the score.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpecificityScore_IsIndependentOfPropertyName()
    {
        return Prop.ForAll(
            GenPatternWithWildcards().ToArbitrary(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (pattern, prop1Raw, prop2Raw) =>
            {
                var prop1 = prop1Raw.Get;
                var prop2 = prop2Raw.Get;

                var strategy = InferNonExactStrategy(pattern);

                var config1 = new DiscriminatorConfig
                {
                    PropertyName = prop1,
                    Pattern = pattern,
                    Strategy = strategy
                };

                var config2 = new DiscriminatorConfig
                {
                    PropertyName = prop2,
                    Pattern = pattern,
                    Strategy = strategy
                };

                return PatternOverlapAnalyzer.ComputeSpecificityScore(config1)
                    == PatternOverlapAnalyzer.ComputeSpecificityScore(config2);
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Generators
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates arbitrary pattern strings containing at least one '*' character.
    /// Patterns are built from literal segments (alphanumeric + # + _) separated by '*'.
    /// </summary>
    private static Gen<string> GenPatternWithWildcards()
    {
        var literalSegment = Gen.Elements("INVOICE#", "#LINE#", "USER#", "#ORDER#", "A#", "#B#", "C", "DATA_", "#")
            .SelectMany(prefix =>
                Gen.Choose(0, 5).Select(n => n == 0 ? prefix : prefix + n));

        return Gen.Choose(1, 5).SelectMany(wildcardCount =>
            Gen.Choose(0, wildcardCount + 1).SelectMany(segmentCount =>
            {
                // Build a pattern with 'wildcardCount' wildcards and 'segmentCount' literal segments
                // distributed around the wildcards
                return Gen.ListOf(segmentCount, literalSegment).Select(segments =>
                {
                    var parts = new List<string>();
                    var segQueue = new Queue<string>(segments);

                    for (var i = 0; i <= wildcardCount; i++)
                    {
                        if (segQueue.Count > 0 && i <= segQueue.Count)
                        {
                            parts.Add(segQueue.Dequeue());
                        }
                        else
                        {
                            parts.Add(string.Empty);
                        }

                        if (i < wildcardCount)
                        {
                            parts.Add("*");
                        }
                    }

                    var result = string.Join("", parts);
                    // Ensure at least one wildcard
                    return result.Contains('*') ? result : result + "*";
                });
            }));
    }

    /// <summary>
    /// Generates StartsWith patterns (literal prefix followed by *).
    /// </summary>
    private static Gen<string> GenStartsWithPattern()
    {
        return Gen.Elements("INVOICE#", "USER#", "ORDER#", "TENANT#", "A#B#")
            .SelectMany(prefix =>
                Gen.Choose(0, 3).Select(extra =>
                {
                    var suffix = extra > 0 ? string.Concat(Enumerable.Repeat("#X", extra)) : "";
                    return prefix + suffix + "*";
                }));
    }

    /// <summary>
    /// Generates EndsWith patterns (* followed by literal suffix).
    /// </summary>
    private static Gen<string> GenEndsWithPattern()
    {
        return Gen.Elements("#AUDIT", "#USER", "#ORDER", "#META", "#B#C")
            .SelectMany(suffix =>
                Gen.Choose(0, 3).Select(extra =>
                {
                    var prefix = extra > 0 ? string.Concat(Enumerable.Repeat("X#", extra)) : "";
                    return "*" + prefix + suffix;
                }));
    }

    /// <summary>
    /// Generates Complex patterns (multiple wildcards in non-trivial positions).
    /// </summary>
    private static Gen<string> GenComplexPattern()
    {
        return Gen.Choose(2, 4).SelectMany(wildcardCount =>
        {
            var segment = Gen.Elements("INVOICE#", "#LINE#", "#ORDER#", "USER#", "#META#", "A#");
            return Gen.ListOf(wildcardCount + 1, segment).Select(segments =>
            {
                // Interleave segments with wildcards
                var result = string.Join("*", segments);
                return result;
            });
        });
    }

    /// <summary>
    /// Infers a non-ExactMatch strategy based on pattern structure.
    /// </summary>
    private static DiscriminatorStrategy InferNonExactStrategy(string pattern)
    {
        if (!pattern.Contains('*'))
            return DiscriminatorStrategy.StartsWith;

        var wildcardCount = pattern.Count(c => c == '*');
        if (wildcardCount >= 2)
            return DiscriminatorStrategy.Complex;

        if (pattern.StartsWith("*"))
            return DiscriminatorStrategy.EndsWith;

        if (pattern.EndsWith("*"))
            return DiscriminatorStrategy.StartsWith;

        return DiscriminatorStrategy.Contains;
    }
}
