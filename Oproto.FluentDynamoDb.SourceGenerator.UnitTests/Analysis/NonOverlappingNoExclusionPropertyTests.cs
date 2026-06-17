using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for PatternOverlapAnalyzer.Analyze — non-overlapping entities produce
/// no exclusion logic or overlap diagnostics.
///
/// Feature: discriminator-enhancement, Property 7: Non-overlapping entities produce no exclusion logic or overlap diagnostics
/// **Validates: Requirements 1.5, 4.1, 4.3, 4.4**
/// </summary>
public class NonOverlappingNoExclusionPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 7: Non-overlapping entities produce no exclusion logic or overlap diagnostics
    // Feature: discriminator-enhancement, Property 7: Non-overlapping entities produce no exclusion logic or overlap diagnostics
    // **Validates: Requirements 1.5, 4.1, 4.3, 4.4**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any table group where all entities have non-overlapping StartsWith patterns
    /// (distinct prefixes), the analyzer SHALL return an empty diagnostics list (no DISC004/DISC005)
    /// AND no entity SHALL have any entries in OverlappingPatterns.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Analyze_NonOverlappingStartsWithPatterns_NoDiagnosticsAndNoExclusions()
    {
        return Prop.ForAll(
            GenNonOverlappingStartsWithPair().ToArbitrary(),
            pair =>
            {
                pair.EntityA.Discriminator!.OverlappingPatterns.Clear();
                pair.EntityB.Discriminator!.OverlappingPatterns.Clear();

                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };

                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                // No diagnostics should be emitted
                var noDiagnostics = diagnostics.Count == 0;

                // Neither entity should have any exclusion patterns
                var noExclusionA = pair.EntityA.Discriminator!.OverlappingPatterns.Count == 0;
                var noExclusionB = pair.EntityB.Discriminator!.OverlappingPatterns.Count == 0;

                return noDiagnostics && noExclusionA && noExclusionB;
            });
    }

    /// <summary>
    /// For any table group where entities have non-overlapping EndsWith patterns
    /// (distinct suffixes), the analyzer SHALL return no diagnostics and no exclusion patterns.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Analyze_NonOverlappingEndsWithPatterns_NoDiagnosticsAndNoExclusions()
    {
        return Prop.ForAll(
            GenNonOverlappingEndsWithPair().ToArbitrary(),
            pair =>
            {
                pair.EntityA.Discriminator!.OverlappingPatterns.Clear();
                pair.EntityB.Discriminator!.OverlappingPatterns.Clear();

                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };

                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var noDiagnostics = diagnostics.Count == 0;
                var noExclusionA = pair.EntityA.Discriminator!.OverlappingPatterns.Count == 0;
                var noExclusionB = pair.EntityB.Discriminator!.OverlappingPatterns.Count == 0;

                return noDiagnostics && noExclusionA && noExclusionB;
            });
    }

    /// <summary>
    /// For any table group where entities use different DiscriminatorProperty values,
    /// the analyzer SHALL treat them as non-overlapping regardless of pattern content,
    /// producing no diagnostics and no exclusion patterns.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Analyze_DifferentDiscriminatorProperties_NoDiagnosticsAndNoExclusions()
    {
        return Prop.ForAll(
            GenDifferentPropertyPair().ToArbitrary(),
            pair =>
            {
                pair.EntityA.Discriminator!.OverlappingPatterns.Clear();
                pair.EntityB.Discriminator!.OverlappingPatterns.Clear();

                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };

                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var noDiagnostics = diagnostics.Count == 0;
                var noExclusionA = pair.EntityA.Discriminator!.OverlappingPatterns.Count == 0;
                var noExclusionB = pair.EntityB.Discriminator!.OverlappingPatterns.Count == 0;

                return noDiagnostics && noExclusionA && noExclusionB;
            });
    }

    /// <summary>
    /// For any table group with three or more entities all having non-overlapping StartsWith
    /// patterns, the analyzer SHALL produce no diagnostics and no exclusion patterns on any entity.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Analyze_ThreeNonOverlappingEntities_NoDiagnosticsAndNoExclusions()
    {
        return Prop.ForAll(
            GenThreeNonOverlappingEntities().ToArbitrary(),
            triple =>
            {
                triple.EntityA.Discriminator!.OverlappingPatterns.Clear();
                triple.EntityB.Discriminator!.OverlappingPatterns.Clear();
                triple.EntityC.Discriminator!.OverlappingPatterns.Clear();

                var tableEntities = new List<EntityModel>
                {
                    triple.EntityA, triple.EntityB, triple.EntityC
                };

                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var noDiagnostics = diagnostics.Count == 0;
                var noExclusionA = triple.EntityA.Discriminator!.OverlappingPatterns.Count == 0;
                var noExclusionB = triple.EntityB.Discriminator!.OverlappingPatterns.Count == 0;
                var noExclusionC = triple.EntityC.Discriminator!.OverlappingPatterns.Count == 0;

                return noDiagnostics && noExclusionA && noExclusionB && noExclusionC;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test Data Models
    // ──────────────────────────────────────────────────────────────────────

    private record EntityPair(EntityModel EntityA, EntityModel EntityB);
    private record EntityTriple(EntityModel EntityA, EntityModel EntityB, EntityModel EntityC);

    // ──────────────────────────────────────────────────────────────────────
    // Generators
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates pairs of entities with non-overlapping StartsWith patterns on the same property.
    /// Uses distinct prefixes that cannot be prefixes of each other (e.g., "USER#*" and "ORDER#*").
    /// </summary>
    private static Gen<EntityPair> GenNonOverlappingStartsWithPair()
    {
        // Prefixes chosen so that no prefix is a prefix of any other
        var prefixes = new[]
        {
            "USER#", "ORDER#", "INVOICE#", "PRODUCT#", "CUSTOMER#",
            "PAYMENT#", "SHIPMENT#", "REFUND#", "RECEIPT#", "TICKET#"
        };

        var genDistinctPrefixes = Gen.Elements(prefixes).Two()
            .Where(t => t.Item1 != t.Item2);

        var genClassName = Gen.Elements(
            "Alpha", "Beta", "Gamma", "Delta", "Epsilon",
            "Zeta", "Eta", "Theta", "Iota", "Kappa");

        var genPropertyName = Gen.Elements("sk", "pk", "gsi1sk", "entity_type", "type_key");

        return genDistinctPrefixes.SelectMany(prefixPair =>
            genClassName.Two().SelectMany(names =>
                genPropertyName.Select(propName =>
                {
                    var (nameA, nameB) = names;
                    if (nameA == nameB) nameB += "Alt";

                    var entityA = CreateEntity(
                        nameA, propName,
                        prefixPair.Item1 + "*",
                        DiscriminatorStrategy.StartsWith);

                    var entityB = CreateEntity(
                        nameB, propName,
                        prefixPair.Item2 + "*",
                        DiscriminatorStrategy.StartsWith);

                    return new EntityPair(entityA, entityB);
                })));
    }

    /// <summary>
    /// Generates pairs of entities with non-overlapping EndsWith patterns on the same property.
    /// Uses distinct suffixes that cannot be suffixes of each other.
    /// </summary>
    private static Gen<EntityPair> GenNonOverlappingEndsWithPair()
    {
        // Suffixes chosen so that no suffix is a suffix of any other
        var suffixes = new[]
        {
            "#USER", "#ORDER", "#INVOICE", "#PRODUCT", "#CUSTOMER",
            "#PAYMENT", "#SHIPMENT", "#REFUND", "#RECEIPT", "#TICKET"
        };

        var genDistinctSuffixes = Gen.Elements(suffixes).Two()
            .Where(t => t.Item1 != t.Item2);

        var genClassName = Gen.Elements(
            "Report", "Audit", "Log", "Event", "Metric",
            "Alert", "Signal", "Trace", "Record", "Entry");

        return genDistinctSuffixes.SelectMany(suffixPair =>
            genClassName.Two().Select(names =>
            {
                var (nameA, nameB) = names;
                if (nameA == nameB) nameB += "Alt";

                var entityA = CreateEntity(
                    nameA, "sk",
                    "*" + suffixPair.Item1,
                    DiscriminatorStrategy.EndsWith);

                var entityB = CreateEntity(
                    nameB, "sk",
                    "*" + suffixPair.Item2,
                    DiscriminatorStrategy.EndsWith);

                return new EntityPair(entityA, entityB);
            }));
    }

    /// <summary>
    /// Generates pairs of entities where each uses a different DiscriminatorProperty,
    /// ensuring they can never overlap regardless of pattern content.
    /// </summary>
    private static Gen<EntityPair> GenDifferentPropertyPair()
    {
        var properties = new[] { "sk", "pk", "gsi1sk", "entity_type", "type_key" };

        var genDistinctProperties = Gen.Elements(properties).Two()
            .Where(t => t.Item1 != t.Item2);

        var genClassName = Gen.Elements(
            "Invoice", "Order", "User", "Product", "Customer",
            "Payment", "Shipment", "Refund", "Receipt", "Ticket");

        // Use identical pattern content to emphasize property-scoping
        var genPattern = Gen.Elements("ITEM#*", "DATA#*", "REC#*", "NODE#*", "EDGE#*");

        return genDistinctProperties.SelectMany(props =>
            genClassName.Two().SelectMany(names =>
                genPattern.Select(pattern =>
                {
                    var (nameA, nameB) = names;
                    if (nameA == nameB) nameB += "Alt";

                    var entityA = CreateEntity(
                        nameA, props.Item1,
                        pattern,
                        DiscriminatorStrategy.StartsWith);

                    var entityB = CreateEntity(
                        nameB, props.Item2,
                        pattern,
                        DiscriminatorStrategy.StartsWith);

                    return new EntityPair(entityA, entityB);
                })));
    }

    /// <summary>
    /// Generates three entities with non-overlapping StartsWith patterns on the same property.
    /// All use distinct prefixes so no pair overlaps.
    /// </summary>
    private static Gen<EntityTriple> GenThreeNonOverlappingEntities()
    {
        // Use 10 distinct prefixes and select 3 unique ones
        var prefixes = new[]
        {
            "USER#", "ORDER#", "INVOICE#", "PRODUCT#", "CUSTOMER#",
            "PAYMENT#", "SHIPMENT#", "REFUND#", "RECEIPT#", "TICKET#"
        };

        var genThreeDistinctPrefixes = Gen.Elements(prefixes).Three()
            .Where(t => t.Item1 != t.Item2 && t.Item2 != t.Item3 && t.Item1 != t.Item3);

        var genClassName = Gen.Elements(
            "Alpha", "Beta", "Gamma", "Delta", "Epsilon",
            "Zeta", "Eta", "Theta", "Iota", "Kappa");

        return genThreeDistinctPrefixes.SelectMany(prefixTriple =>
            genClassName.Three().Select(names =>
            {
                var (nameA, nameB, nameC) = names;
                // Ensure distinct class names
                if (nameA == nameB) nameB += "Alt";
                if (nameA == nameC || nameB == nameC) nameC += "Ext";

                var entityA = CreateEntity(nameA, "sk", prefixTriple.Item1 + "*", DiscriminatorStrategy.StartsWith);
                var entityB = CreateEntity(nameB, "sk", prefixTriple.Item2 + "*", DiscriminatorStrategy.StartsWith);
                var entityC = CreateEntity(nameC, "sk", prefixTriple.Item3 + "*", DiscriminatorStrategy.StartsWith);

                return new EntityTriple(entityA, entityB, entityC);
            }));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private static EntityModel CreateEntity(
        string className,
        string propertyName,
        string pattern,
        DiscriminatorStrategy strategy)
    {
        return new EntityModel
        {
            ClassName = className,
            Namespace = "TestNamespace",
            TableName = "test-table",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = propertyName,
                Pattern = pattern,
                Strategy = strategy,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };
    }
}
