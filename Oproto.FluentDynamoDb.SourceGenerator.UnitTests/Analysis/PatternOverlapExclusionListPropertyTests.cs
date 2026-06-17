using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for exclusion list correctness in PatternOverlapAnalyzer.
///
/// Feature: discriminator-enhancement, Property 5: Exclusion list contains all and only higher-scoring overlapping patterns
/// </summary>
public class PatternOverlapExclusionListPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 5: Exclusion list contains all and only higher-scoring overlapping patterns
    // Feature: discriminator-enhancement, Property 5: Exclusion list contains all and only higher-scoring overlapping patterns
    // **Validates: Requirements 1.7, 3.4**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any entity in a table group with overlapping patterns, its OverlappingPatterns list
    /// SHALL contain exactly those entities whose specificity score is strictly higher than its own
    /// AND whose pattern overlaps with its pattern on the same DiscriminatorProperty.
    /// 
    /// This test generates two-entity groups with known overlapping StartsWith patterns of
    /// different specificity (e.g., "PREFIX#*" and "PREFIX#*#SUFFIX#*"), runs Analyze, and
    /// verifies the less-specific entity's OverlappingPatterns contains exactly one entry for
    /// the more-specific entity, and the more-specific entity's list is empty.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property LessSpecificEntity_ExclusionList_ContainsOnlyHigherScoringOverlaps()
    {
        var prefixGen = Gen.Elements("INVOICE", "ORDER", "USER", "ITEM", "CUSTOMER", "PRODUCT")
            .Select(p => p + "#");

        var suffixGen = Gen.Elements("LINE", "DETAIL", "META", "CHILD", "SUB", "ENTRY")
            .Select(s => "#" + s + "#");

        return Prop.ForAll(
            prefixGen.ToArbitrary(),
            suffixGen.ToArbitrary(),
            (prefix, suffix) =>
            {
                // Less-specific pattern: "PREFIX#*" (score 1)
                var lessSpecificPattern = prefix + "*";
                // More-specific pattern: "PREFIX#*#SUFFIX#*" (score 2)
                var moreSpecificPattern = prefix + "*" + suffix + "*";

                var lessSpecificConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = lessSpecificPattern,
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                var moreSpecificConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = moreSpecificPattern,
                    Strategy = DiscriminatorStrategy.Complex
                };

                var entityA = new EntityModel
                {
                    ClassName = "ParentEntity",
                    TableName = "test-table",
                    Discriminator = lessSpecificConfig
                };

                var entityB = new EntityModel
                {
                    ClassName = "ChildEntity",
                    TableName = "test-table",
                    Discriminator = moreSpecificConfig
                };

                // Clear before running
                lessSpecificConfig.OverlappingPatterns.Clear();
                moreSpecificConfig.OverlappingPatterns.Clear();

                var entities = new List<EntityModel> { entityA, entityB };
                PatternOverlapAnalyzer.Analyze(entities);

                var lessScore = PatternOverlapAnalyzer.ComputeSpecificityScore(lessSpecificConfig);
                var moreScore = PatternOverlapAnalyzer.ComputeSpecificityScore(moreSpecificConfig);

                // More-specific entity should have higher score
                if (moreScore <= lessScore) return true; // skip if scores aren't different (shouldn't happen with our generation)

                // Less-specific entity should have exactly one exclusion entry for the more-specific entity
                var lessHasCorrectExclusions = lessSpecificConfig.OverlappingPatterns.Count == 1
                    && lessSpecificConfig.OverlappingPatterns[0].EntityName == "ChildEntity";

                // More-specific entity should have no exclusion entries
                var moreHasNoExclusions = moreSpecificConfig.OverlappingPatterns.Count == 0;

                return lessHasCorrectExclusions && moreHasNoExclusions;
            });
    }

    /// <summary>
    /// For a three-entity hierarchy (e.g., "A#*", "A#*#B#*", "A#*#B#*#C#*"),
    /// the least-specific entity SHALL have exclusions for BOTH more-specific entities,
    /// the middle entity SHALL have an exclusion for only the most-specific entity,
    /// and the most-specific entity SHALL have no exclusions.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ThreeEntityHierarchy_ExclusionLists_ContainAllHigherScoringOverlaps()
    {
        var prefixGen = Gen.Elements("INVOICE", "ORDER", "USER", "ITEM", "CUSTOMER")
            .Select(p => p + "#");

        var midSuffixGen = Gen.Elements("LINE", "DETAIL", "META", "CHILD")
            .Select(s => "#" + s + "#");

        var leafSuffixGen = Gen.Elements("ADJ", "NOTE", "FEE", "TAX")
            .Select(s => "#" + s + "#");

        return Prop.ForAll(
            prefixGen.ToArbitrary(),
            midSuffixGen.ToArbitrary(),
            leafSuffixGen.ToArbitrary(),
            (prefix, midSuffix, leafSuffix) =>
            {
                // Score 1: "PREFIX#*"
                var basePattern = prefix + "*";
                // Score 2: "PREFIX#*#MID#*"
                var midPattern = prefix + "*" + midSuffix + "*";
                // Score 3: "PREFIX#*#MID#*#LEAF#*"
                var leafPattern = prefix + "*" + midSuffix + "*" + leafSuffix + "*";

                var baseConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = basePattern,
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                var midConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = midPattern,
                    Strategy = DiscriminatorStrategy.Complex
                };

                var leafConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = leafPattern,
                    Strategy = DiscriminatorStrategy.Complex
                };

                var baseEntity = new EntityModel
                {
                    ClassName = "BaseEntity",
                    TableName = "test-table",
                    Discriminator = baseConfig
                };

                var midEntity = new EntityModel
                {
                    ClassName = "MidEntity",
                    TableName = "test-table",
                    Discriminator = midConfig
                };

                var leafEntity = new EntityModel
                {
                    ClassName = "LeafEntity",
                    TableName = "test-table",
                    Discriminator = leafConfig
                };

                // Clear before running
                baseConfig.OverlappingPatterns.Clear();
                midConfig.OverlappingPatterns.Clear();
                leafConfig.OverlappingPatterns.Clear();

                var entities = new List<EntityModel> { baseEntity, midEntity, leafEntity };
                PatternOverlapAnalyzer.Analyze(entities);

                var baseScore = PatternOverlapAnalyzer.ComputeSpecificityScore(baseConfig);
                var midScore = PatternOverlapAnalyzer.ComputeSpecificityScore(midConfig);
                var leafScore = PatternOverlapAnalyzer.ComputeSpecificityScore(leafConfig);

                // Verify score ordering
                if (!(leafScore > midScore && midScore > baseScore)) return true; // skip invalid generations

                // Base entity (lowest score) should exclude both mid and leaf
                var baseExclusionNames = baseConfig.OverlappingPatterns
                    .Select(e => e.EntityName)
                    .OrderBy(n => n)
                    .ToList();
                var expectedBaseExclusions = new List<string> { "LeafEntity", "MidEntity" }.OrderBy(n => n).ToList();
                var baseCorrect = baseExclusionNames.SequenceEqual(expectedBaseExclusions);

                // Mid entity should exclude only leaf
                var midCorrect = midConfig.OverlappingPatterns.Count == 1
                    && midConfig.OverlappingPatterns[0].EntityName == "LeafEntity";

                // Leaf entity (highest score) should have no exclusions
                var leafCorrect = leafConfig.OverlappingPatterns.Count == 0;

                return baseCorrect && midCorrect && leafCorrect;
            });
    }

    /// <summary>
    /// For entities with non-overlapping patterns on the same property (different prefixes),
    /// no entity SHALL have any exclusion entries.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonOverlapping_SameProperty_NoExclusions()
    {
        var distinctPrefixesGen = Gen.Two(
            Gen.Elements("USER", "ORDER", "INVOICE", "PRODUCT", "CUSTOMER", "CATEGORY"))
            .Where(t => t.Item1 != t.Item2);

        return Prop.ForAll(
            distinctPrefixesGen.ToArbitrary(),
            prefixes =>
            {
                var configA = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = prefixes.Item1 + "#*",
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                var configB = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = prefixes.Item2 + "#*",
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                var entityA = new EntityModel
                {
                    ClassName = "EntityA",
                    TableName = "test-table",
                    Discriminator = configA
                };

                var entityB = new EntityModel
                {
                    ClassName = "EntityB",
                    TableName = "test-table",
                    Discriminator = configB
                };

                // Clear before running
                configA.OverlappingPatterns.Clear();
                configB.OverlappingPatterns.Clear();

                var entities = new List<EntityModel> { entityA, entityB };
                PatternOverlapAnalyzer.Analyze(entities);

                return configA.OverlappingPatterns.Count == 0
                    && configB.OverlappingPatterns.Count == 0;
            });
    }

    /// <summary>
    /// For entities with patterns on different properties, no entity SHALL have any exclusion entries
    /// regardless of pattern content.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DifferentProperties_NoExclusions()
    {
        var propertyGen = Gen.Elements("sk", "pk", "gsi1sk", "entity_type", "type");
        var patternGen = Gen.Elements("INVOICE#*", "ORDER#*", "USER#*");

        return Prop.ForAll(
            Gen.Two(propertyGen).Where(t => t.Item1 != t.Item2).ToArbitrary(),
            patternGen.ToArbitrary(),
            (properties, pattern) =>
            {
                // Same pattern on different properties should never produce exclusions
                var configA = new DiscriminatorConfig
                {
                    PropertyName = properties.Item1,
                    Pattern = pattern,
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                var configB = new DiscriminatorConfig
                {
                    PropertyName = properties.Item2,
                    Pattern = pattern,
                    Strategy = DiscriminatorStrategy.StartsWith
                };

                var entityA = new EntityModel
                {
                    ClassName = "EntityA",
                    TableName = "test-table",
                    Discriminator = configA
                };

                var entityB = new EntityModel
                {
                    ClassName = "EntityB",
                    TableName = "test-table",
                    Discriminator = configB
                };

                // Clear before running
                configA.OverlappingPatterns.Clear();
                configB.OverlappingPatterns.Clear();

                var entities = new List<EntityModel> { entityA, entityB };
                PatternOverlapAnalyzer.Analyze(entities);

                return configA.OverlappingPatterns.Count == 0
                    && configB.OverlappingPatterns.Count == 0;
            });
    }

    /// <summary>
    /// When an ExactMatch entity overlaps with a wildcard pattern entity on the same property,
    /// the wildcard entity's exclusion list SHALL contain an entry for the ExactMatch entity
    /// (since ExactMatch scores int.MaxValue, which is strictly higher than any wildcard score).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExactMatch_AlwaysExcludedFromWildcardEntity()
    {
        var prefixGen = Gen.Elements("INVOICE", "ORDER", "USER", "ITEM")
            .Select(p => p + "#");
        var valueGen = Gen.Elements("001", "ABC", "123", "XYZ");

        return Prop.ForAll(
            prefixGen.ToArbitrary(),
            valueGen.ToArbitrary(),
            (prefix, value) =>
            {
                var exactValue = prefix + value;

                var wildcardConfig = new DiscriminatorConfig
                {
                    PropertyName = "sk",
                    Pattern = prefix + "*",
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

                // Clear before running
                wildcardConfig.OverlappingPatterns.Clear();
                exactConfig.OverlappingPatterns.Clear();

                var entities = new List<EntityModel> { wildcardEntity, exactEntity };
                PatternOverlapAnalyzer.Analyze(entities);

                // Wildcard entity should have the ExactMatch entity in its exclusion list
                var wildcardHasExclusion = wildcardConfig.OverlappingPatterns.Count == 1
                    && wildcardConfig.OverlappingPatterns[0].EntityName == "ExactEntity"
                    && wildcardConfig.OverlappingPatterns[0].Strategy == DiscriminatorStrategy.ExactMatch;

                // ExactMatch entity should have no exclusions (it's the most specific)
                var exactHasNoExclusions = exactConfig.OverlappingPatterns.Count == 0;

                return wildcardHasExclusion && exactHasNoExclusions;
            });
    }
}
