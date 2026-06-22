using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for mutual exclusivity of MatchesEntity across overlapping entities.
///
/// Feature: discriminator-enhancement, Property 4: Mutual exclusivity of MatchesEntity across overlapping entities
/// **Validates: Requirements 1.1, 1.4**
/// </summary>
public class MutualExclusivityPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 4: Mutual exclusivity of MatchesEntity across overlapping entities
    // Feature: discriminator-enhancement, Property 4: Mutual exclusivity of MatchesEntity across overlapping entities
    // **Validates: Requirements 1.1, 1.4**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For a two-entity hierarchy with patterns "PREFIX#*" (entity A) and "PREFIX#*#MID#*" (entity B)
    /// on the same property, after overlap analysis and exclusion guard application, exactly one
    /// entity's MatchesEntity logic claims each generated discriminator value that matches at least
    /// one entity's pattern.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TwoEntityHierarchy_ExactlyOneEntityClaimsEachValue()
    {
        var testCaseGen = Gen.Elements("INVOICE", "ORDER", "USER", "ITEM", "CUSTOMER", "PRODUCT")
            .Select(p => p + "#")
            .SelectMany(prefix =>
                Gen.Elements("LINE", "DETAIL", "META", "CHILD", "SUB", "ENTRY")
                    .Select(s => "#" + s + "#")
                    .SelectMany(midLiteral =>
                        Gen.Elements("001", "ABC", "XYZ", "123", "hello", "test", "foo-bar")
                            .SelectMany(suffix =>
                                Gen.Elements(true, false)
                                    .Select(includeMiddle => new TwoEntityTestCase(
                                        prefix, midLiteral, suffix, includeMiddle)))));

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            tc =>
            {
                // Set up hierarchy:
                // Entity A (less specific): "PREFIX#*" — StartsWith "PREFIX#"
                // Entity B (more specific): "PREFIX#*#MID#*" — StartsWith "PREFIX#" AND Contains "#MID#"
                var patternA = tc.Prefix + "*";
                var patternB = tc.Prefix + "*" + tc.MidLiteral + "*";

                var configA = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = patternA,
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                var configB = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = patternB,
                    Strategy = DiscriminatorStrategy.Complex
                };

                var entityA = new EntityModel
                {
                    ClassName = "ParentEntity",
                    TableName = "test-table",
                    Discriminator = configA
                };

                var entityB = new EntityModel
                {
                    ClassName = "ChildEntity",
                    TableName = "test-table",
                    Discriminator = configB
                };

                // Clear and run analysis
                configA.OverlappingPatterns.Clear();
                configB.OverlappingPatterns.Clear();

                var entities = new List<EntityModel> { entityA, entityB };
                PatternOverlapAnalyzer.Analyze(entities);

                // Verify score ordering (skip invalid cases)
                var scoreA = PatternOverlapAnalyzer.ComputeSpecificityScore(configA);
                var scoreB = PatternOverlapAnalyzer.ComputeSpecificityScore(configB);
                if (scoreB <= scoreA) return true; // skip if scores aren't properly ordered

                // Generate a discriminator value that matches at least one entity
                string discriminatorValue;
                if (tc.IncludeMiddle)
                {
                    // Value that matches both patterns: starts with prefix AND contains midLiteral
                    discriminatorValue = tc.Prefix + tc.Suffix + tc.MidLiteral + tc.Suffix;
                }
                else
                {
                    // Value that matches only entity A: starts with prefix but does NOT contain midLiteral
                    discriminatorValue = tc.Prefix + tc.Suffix;
                    // Ensure it doesn't accidentally contain the mid literal
                    var midTrimmed = tc.MidLiteral.Trim('#');
                    if (discriminatorValue.Contains(midTrimmed))
                        discriminatorValue = tc.Prefix + "UNIQUE_VALUE_NO_MID";
                }

                // Simulate MatchesEntity logic for each entity
                var entityAClaims = SimulateMatchesEntity(discriminatorValue, configA);
                var entityBClaims = SimulateMatchesEntity(discriminatorValue, configB);

                // Exactly one entity should claim the value
                var claimCount = (entityAClaims ? 1 : 0) + (entityBClaims ? 1 : 0);
                return claimCount == 1;
            });
    }

    /// <summary>
    /// For a three-entity hierarchy with patterns "PREFIX#*" (A), "PREFIX#*#MID#*" (B),
    /// and "PREFIX#*#MID#*#LEAF#*" (C), after overlap analysis, exactly one entity's
    /// MatchesEntity logic claims each generated discriminator value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ThreeEntityHierarchy_ExactlyOneEntityClaimsEachValue()
    {
        var testCaseGen = Gen.Elements("INVOICE", "ORDER", "USER", "ITEM", "CUSTOMER")
            .Select(p => p + "#")
            .SelectMany(prefix =>
                Gen.Elements("LINE", "DETAIL", "META", "CHILD")
                    .Select(s => "#" + s + "#")
                    .SelectMany(midLiteral =>
                        Gen.Elements("ADJ", "NOTE", "FEE", "TAX")
                            .Select(s => "#" + s + "#")
                            .SelectMany(leafLiteral =>
                                Gen.Elements("001", "ABC", "XYZ", "123", "test")
                                    .SelectMany(suffix =>
                                        Gen.Choose(0, 2)
                                            .Select(level => new ThreeEntityTestCase(
                                                prefix, midLiteral, leafLiteral, suffix, level))))));

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            tc =>
            {
                // Set up three-entity hierarchy
                var patternA = tc.Prefix + "*";
                var patternB = tc.Prefix + "*" + tc.MidLiteral + "*";
                var patternC = tc.Prefix + "*" + tc.MidLiteral + "*" + tc.LeafLiteral + "*";

                var configA = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = patternA,
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                var configB = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = patternB,
                    Strategy = DiscriminatorStrategy.Complex
                };

                var configC = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = patternC,
                    Strategy = DiscriminatorStrategy.Complex
                };

                var entityA = new EntityModel
                {
                    ClassName = "GrandparentEntity",
                    TableName = "test-table",
                    Discriminator = configA
                };

                var entityB = new EntityModel
                {
                    ClassName = "ParentEntity",
                    TableName = "test-table",
                    Discriminator = configB
                };

                var entityC = new EntityModel
                {
                    ClassName = "ChildEntity",
                    TableName = "test-table",
                    Discriminator = configC
                };

                // Clear and run analysis
                configA.OverlappingPatterns.Clear();
                configB.OverlappingPatterns.Clear();
                configC.OverlappingPatterns.Clear();

                var entities = new List<EntityModel> { entityA, entityB, entityC };
                PatternOverlapAnalyzer.Analyze(entities);

                // Verify score ordering
                var scoreA = PatternOverlapAnalyzer.ComputeSpecificityScore(configA);
                var scoreB = PatternOverlapAnalyzer.ComputeSpecificityScore(configB);
                var scoreC = PatternOverlapAnalyzer.ComputeSpecificityScore(configC);
                if (!(scoreC > scoreB && scoreB > scoreA)) return true; // skip invalid

                // Generate discriminator value based on level
                string discriminatorValue;
                switch (tc.Level)
                {
                    case 0:
                        // Matches only A: starts with prefix, no mid or leaf literals
                        discriminatorValue = tc.Prefix + tc.Suffix;
                        var midTrimmed = tc.MidLiteral.Trim('#');
                        if (discriminatorValue.Contains(midTrimmed))
                            discriminatorValue = tc.Prefix + "ONLY_A_VALUE";
                        break;
                    case 1:
                        // Matches A and B but not C: has prefix and mid literal, but not leaf literal
                        discriminatorValue = tc.Prefix + tc.Suffix + tc.MidLiteral + tc.Suffix;
                        var leafTrimmed = tc.LeafLiteral.Trim('#');
                        if (discriminatorValue.Contains(leafTrimmed))
                            discriminatorValue = tc.Prefix + tc.Suffix + tc.MidLiteral + "NO_LEAF";
                        break;
                    default:
                        // Matches A, B, and C: has prefix, mid literal, and leaf literal
                        discriminatorValue = tc.Prefix + tc.Suffix + tc.MidLiteral + tc.Suffix + tc.LeafLiteral + tc.Suffix;
                        break;
                }

                // Simulate MatchesEntity for each entity
                var entityAClaims = SimulateMatchesEntity(discriminatorValue, configA);
                var entityBClaims = SimulateMatchesEntity(discriminatorValue, configB);
                var entityCClaims = SimulateMatchesEntity(discriminatorValue, configC);

                // Exactly one entity should claim the value
                var claimCount = (entityAClaims ? 1 : 0) + (entityBClaims ? 1 : 0) + (entityCClaims ? 1 : 0);
                return claimCount == 1;
            });
    }

    /// <summary>
    /// For a two-entity hierarchy where entity A uses StartsWith and entity B uses ExactMatch
    /// on the same property, after overlap analysis, exactly one entity claims each value
    /// that matches at least one entity's pattern.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExactMatchVsWildcard_ExactlyOneEntityClaimsEachValue()
    {
        var testCaseGen = Gen.Elements("INVOICE", "ORDER", "USER", "ITEM", "CUSTOMER")
            .Select(p => p + "#")
            .SelectMany(prefix =>
                Gen.Elements("001", "SPECIAL", "DEFAULT", "PRIMARY")
                    .SelectMany(exactSuffix =>
                        Gen.Elements("002", "OTHER", "SECONDARY", "ALT", "RANDOM")
                            .SelectMany(otherSuffix =>
                                Gen.Elements(true, false)
                                    .Select(useExactValue => new ExactMatchTestCase(
                                        prefix, exactSuffix, otherSuffix, useExactValue)))));

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            tc =>
            {
                var exactValue = tc.Prefix + tc.ExactSuffix;

                var wildcardConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = tc.Prefix + "*",
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                var exactConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    ExactValue = exactValue,
                    Strategy = DiscriminatorStrategy.ExactMatch
                };

                var wildcardEntity = new EntityModel
                {
                    ClassName = "WildcardEntity",
                    TableName = "test-table",
                    Discriminator = wildcardConfig
                };

                var exactEntity = new EntityModel
                {
                    ClassName = "ExactEntity",
                    TableName = "test-table",
                    Discriminator = exactConfig
                };

                // Clear and run analysis
                wildcardConfig.OverlappingPatterns.Clear();
                exactConfig.OverlappingPatterns.Clear();

                var entities = new List<EntityModel> { wildcardEntity, exactEntity };
                PatternOverlapAnalyzer.Analyze(entities);

                // Generate discriminator value
                string discriminatorValue;
                if (tc.UseExactValue)
                {
                    // Matches both patterns — should be claimed only by ExactMatch entity
                    discriminatorValue = exactValue;
                }
                else
                {
                    // Matches only wildcard pattern — should be claimed only by wildcard entity
                    discriminatorValue = tc.Prefix + tc.OtherSuffix;
                    // Make sure it's different from the exact value
                    if (discriminatorValue == exactValue)
                        discriminatorValue = tc.Prefix + "DIFFERENT_VALUE";
                }

                // Simulate MatchesEntity for each entity
                var wildcardClaims = SimulateMatchesEntity(discriminatorValue, wildcardConfig);
                var exactClaims = SimulateMatchesEntity(discriminatorValue, exactConfig);

                // Exactly one entity should claim the value
                var claimCount = (wildcardClaims ? 1 : 0) + (exactClaims ? 1 : 0);
                return claimCount == 1;
            });
    }

    /// <summary>
    /// For overlapping entities using EndsWith patterns of different specificity,
    /// exactly one entity claims each generated value.
    /// E.g., "*#AUDIT" (score 1) vs "*#DETAILED#AUDIT" (score 2).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EndsWithHierarchy_ExactlyOneEntityClaimsEachValue()
    {
        var testCaseGen = Gen.Elements("AUDIT", "LOG", "EVENT", "TRACE", "METRIC")
            .Select(s => "#" + s)
            .SelectMany(suffix =>
                Gen.Elements("DETAILED", "FULL", "EXTENDED", "VERBOSE")
                    .Select(s => "#" + s)
                    .SelectMany(midSuffix =>
                        Gen.Elements("DATA", "SYS", "APP", "NET", "SEC")
                            .SelectMany(valuePrefix =>
                                Gen.Elements(true, false)
                                    .Select(includeMiddle => new EndsWithTestCase(
                                        suffix, midSuffix, valuePrefix, includeMiddle)))));

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            tc =>
            {
                // Less specific: "*#AUDIT" (EndsWith)
                var patternA = "*" + tc.Suffix;
                // More specific: "*#DETAILED#AUDIT" (EndsWith with longer suffix)
                var patternB = "*" + tc.MidSuffix + tc.Suffix;

                var configA = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = patternA,
                    Strategy = DiscriminatorStrategy.EndsWith
                };

                var configB = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = patternB,
                    Strategy = DiscriminatorStrategy.EndsWith
                };

                var entityA = new EntityModel
                {
                    ClassName = "BroadEntity",
                    TableName = "test-table",
                    Discriminator = configA
                };

                var entityB = new EntityModel
                {
                    ClassName = "SpecificEntity",
                    TableName = "test-table",
                    Discriminator = configB
                };

                // Clear and run analysis
                configA.OverlappingPatterns.Clear();
                configB.OverlappingPatterns.Clear();

                var entities = new List<EntityModel> { entityA, entityB };
                PatternOverlapAnalyzer.Analyze(entities);

                var scoreA = PatternOverlapAnalyzer.ComputeSpecificityScore(configA);
                var scoreB = PatternOverlapAnalyzer.ComputeSpecificityScore(configB);
                if (scoreB <= scoreA) return true; // skip if not properly ordered

                // Generate discriminator value
                string discriminatorValue;
                if (tc.IncludeMiddle)
                {
                    // Matches both: ends with "#DETAILED#AUDIT"
                    discriminatorValue = tc.ValuePrefix + tc.MidSuffix + tc.Suffix;
                }
                else
                {
                    // Matches only A: ends with "#AUDIT" but NOT "#DETAILED#AUDIT"
                    discriminatorValue = tc.ValuePrefix + tc.Suffix;
                    // Ensure the value doesn't end with the more-specific suffix
                    var fullSpecificSuffix = tc.MidSuffix + tc.Suffix;
                    if (discriminatorValue.EndsWith(fullSpecificSuffix))
                        discriminatorValue = "UNIQUE_PREFIX" + tc.Suffix;
                }

                // Simulate MatchesEntity for each entity
                var entityAClaims = SimulateMatchesEntity(discriminatorValue, configA);
                var entityBClaims = SimulateMatchesEntity(discriminatorValue, configB);

                // Exactly one entity should claim the value
                var claimCount = (entityAClaims ? 1 : 0) + (entityBClaims ? 1 : 0);
                return claimCount == 1;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test Data Models
    // ──────────────────────────────────────────────────────────────────────

    private record TwoEntityTestCase(string Prefix, string MidLiteral, string Suffix, bool IncludeMiddle);
    private record ThreeEntityTestCase(string Prefix, string MidLiteral, string LeafLiteral, string Suffix, int Level);
    private record ExactMatchTestCase(string Prefix, string ExactSuffix, string OtherSuffix, bool UseExactValue);
    private record EndsWithTestCase(string Suffix, string MidSuffix, string ValuePrefix, bool IncludeMiddle);

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Simulates the generated MatchesEntity logic for a given discriminator config:
    /// 1. Checks positive match (does the value match this entity's own pattern?)
    /// 2. Checks exclusion guards (does the value also match any more-specific pattern?)
    /// Returns true only if positive match succeeds AND no exclusion guard fires.
    /// </summary>
    private static bool SimulateMatchesEntity(string value, DiscriminatorConfig config)
    {
        // Step 1: Positive match check
        if (!MatchesPositivePattern(value, config))
            return false;

        // Step 2: Exclusion guard checks — return false if any exclusion pattern matches
        foreach (var exclusion in config.OverlappingPatterns)
        {
            if (MatchesExclusionPattern(value, exclusion))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks whether a value matches the entity's own positive pattern.
    /// </summary>
    private static bool MatchesPositivePattern(string value, DiscriminatorConfig config)
    {
        switch (config.Strategy)
        {
            case DiscriminatorStrategy.ExactMatch:
                return string.Equals(value, config.ExactValue, StringComparison.Ordinal);

            case DiscriminatorStrategy.StartsWith:
                var startsWithText = DiscriminatorAnalyzer.GetPatternText(config.Pattern!, config.Strategy);
                return value.StartsWith(startsWithText, StringComparison.Ordinal);

            case DiscriminatorStrategy.EndsWith:
                var endsWithText = DiscriminatorAnalyzer.GetPatternText(config.Pattern!, config.Strategy);
                return value.EndsWith(endsWithText, StringComparison.Ordinal);

            case DiscriminatorStrategy.Contains:
                var containsText = DiscriminatorAnalyzer.GetPatternText(config.Pattern!, config.Strategy);
                return value.Contains(containsText, StringComparison.Ordinal);

            case DiscriminatorStrategy.Complex:
                // Complex patterns: check all non-empty segments from splitting on '*'
                var segments = config.Pattern!.Split('*');
                return MatchesComplexPattern(value, segments);

            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether a value matches a complex pattern by verifying all non-empty segments
    /// appear in the value in order.
    /// For the first segment (if non-empty): value must start with it.
    /// For the last segment (if non-empty): value must end with it.
    /// For middle segments (if non-empty): value must contain them in order.
    /// </summary>
    private static bool MatchesComplexPattern(string value, string[] segments)
    {
        var currentIndex = 0;

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (string.IsNullOrEmpty(segment))
                continue;

            if (i == 0)
            {
                // First segment: value must start with it
                if (!value.StartsWith(segment, StringComparison.Ordinal))
                    return false;
                currentIndex = segment.Length;
            }
            else if (i == segments.Length - 1)
            {
                // Last segment: value must end with it
                if (!value.EndsWith(segment, StringComparison.Ordinal))
                    return false;
            }
            else
            {
                // Middle segment: must appear after current position
                var foundIndex = value.IndexOf(segment, currentIndex, StringComparison.Ordinal);
                if (foundIndex < 0)
                    return false;
                currentIndex = foundIndex + segment.Length;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether a value matches an exclusion pattern using the exclusion's strategy
    /// and literal text.
    /// </summary>
    private static bool MatchesExclusionPattern(string value, ExclusionPattern exclusion)
    {
        return exclusion.Strategy switch
        {
            DiscriminatorStrategy.ExactMatch =>
                string.Equals(value, exclusion.LiteralText, StringComparison.Ordinal),
            DiscriminatorStrategy.StartsWith =>
                value.StartsWith(exclusion.LiteralText, StringComparison.Ordinal),
            DiscriminatorStrategy.EndsWith =>
                value.EndsWith(exclusion.LiteralText, StringComparison.Ordinal),
            DiscriminatorStrategy.Contains =>
                value.Contains(exclusion.LiteralText, StringComparison.Ordinal),
            DiscriminatorStrategy.Complex =>
                MatchesComplexExclusion(value, exclusion),
            _ => false
        };
    }

    /// <summary>
    /// For Complex strategy exclusions, checks all literal segments from the exclusion pattern.
    /// Uses the same segment-matching logic as complex positive matching.
    /// </summary>
    private static bool MatchesComplexExclusion(string value, ExclusionPattern exclusion)
    {
        var segments = exclusion.Pattern.Split('*');
        return MatchesComplexPattern(value, segments);
    }
}
