using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for PatternOverlapAnalyzer.Analyze — ambiguous same-score overlap diagnostics.
///
/// Feature: discriminator-enhancement, Property 8: Ambiguous same-score overlaps produce an error diagnostic
/// **Validates: Requirements 2.3**
/// </summary>
public class AmbiguousSameScoreDiagnosticsPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 8: Ambiguous same-score overlaps produce an error diagnostic
    // Feature: discriminator-enhancement, Property 8: Ambiguous same-score overlaps produce an error diagnostic
    // **Validates: Requirements 2.3**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any pair of entities in the same table group with overlapping patterns
    /// on the same DiscriminatorProperty AND the same specificity score,
    /// the analyzer SHALL emit a diagnostic with severity Error containing both entity class names.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Analyze_SameScoreOverlappingPatterns_EmitsErrorDiagnosticWithBothEntityNames()
    {
        return Prop.ForAll(
            GenSameScoreOverlappingEntityPair().ToArbitrary(),
            pair =>
            {
                // Clear OverlappingPatterns before each test run
                pair.EntityA.Discriminator!.OverlappingPatterns.Clear();
                pair.EntityB.Discriminator!.OverlappingPatterns.Clear();

                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };

                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                // Should have at least one Error diagnostic
                var errorDiagnostics = diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();

                if (errorDiagnostics.Count == 0)
                    return false;

                // At least one error diagnostic should mention both entity names
                var hasBothNames = errorDiagnostics.Any(d =>
                {
                    var message = d.GetMessage();
                    return message.Contains(pair.EntityA.ClassName)
                        && message.Contains(pair.EntityB.ClassName);
                });

                return hasBothNames;
            });
    }

    /// <summary>
    /// For any pair of entities with same-score overlapping StartsWith patterns,
    /// the analyzer SHALL emit at least one Error diagnostic.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Analyze_SameScoreStartsWithOverlap_EmitsErrorDiagnostic()
    {
        return Prop.ForAll(
            GenSameScoreStartsWithPair().ToArbitrary(),
            pair =>
            {
                pair.EntityA.Discriminator!.OverlappingPatterns.Clear();
                pair.EntityB.Discriminator!.OverlappingPatterns.Clear();

                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };

                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var hasError = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
                return hasError;
            });
    }

    /// <summary>
    /// For any pair of entities with same-score overlapping patterns using Contains strategy,
    /// the analyzer SHALL emit an Error diagnostic mentioning both entity names.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Analyze_SameScoreContainsOverlap_EmitsErrorDiagnosticWithBothNames()
    {
        return Prop.ForAll(
            GenSameScoreContainsPair().ToArbitrary(),
            pair =>
            {
                pair.EntityA.Discriminator!.OverlappingPatterns.Clear();
                pair.EntityB.Discriminator!.OverlappingPatterns.Clear();

                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };

                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var errorDiagnostics = diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();

                if (errorDiagnostics.Count == 0)
                    return false;

                return errorDiagnostics.Any(d =>
                {
                    var message = d.GetMessage();
                    return message.Contains(pair.EntityA.ClassName)
                        && message.Contains(pair.EntityB.ClassName);
                });
            });
    }

    /// <summary>
    /// For any pair with same-score overlapping patterns, no exclusion patterns
    /// SHALL be added (since ambiguity cannot be resolved).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Analyze_SameScoreOverlap_DoesNotPopulateExclusionPatterns()
    {
        return Prop.ForAll(
            GenSameScoreOverlappingEntityPair().ToArbitrary(),
            pair =>
            {
                pair.EntityA.Discriminator!.OverlappingPatterns.Clear();
                pair.EntityB.Discriminator!.OverlappingPatterns.Clear();

                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };

                PatternOverlapAnalyzer.Analyze(tableEntities);

                // Ambiguous overlaps should NOT add exclusion patterns
                return pair.EntityA.Discriminator.OverlappingPatterns.Count == 0
                    && pair.EntityB.Discriminator.OverlappingPatterns.Count == 0;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test Data Model
    // ──────────────────────────────────────────────────────────────────────

    private record EntityPair(EntityModel EntityA, EntityModel EntityB);

    // ──────────────────────────────────────────────────────────────────────
    // Generators
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates pairs of entities with overlapping patterns that have the same specificity score.
    /// Uses StartsWith strategy with one prefix being a prefix of the other (ensuring overlap)
    /// and both having the same number of non-empty literal segments (same score).
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlappingEntityPair()
    {
        var genClassName = Gen.Elements(
            "Invoice", "Order", "User", "Product", "Customer",
            "Payment", "Shipment", "Refund", "Receipt", "Ticket");

        var genPropertyName = Gen.Elements("sk", "pk", "gsi1sk", "entity_type", "type_key");

        // Generate pairs of StartsWith patterns with same segment count
        // where one literal is a prefix of the other (ensuring overlap)
        var genSameScoreStartsWithPair = Gen.Elements(
            ("A#*", "A#B#*"),       // "A#" is prefix of "A#B#" — both score 1 but wait...
            ("X#*", "X#*"),         // Identical patterns — both score 1
            ("ORDER#*", "ORDER#*"), // Identical patterns — both score 1
            ("INV#*", "INV#*"),     // Identical patterns — both score 1
            ("USER#*", "USER#*")    // Identical patterns — both score 1
        ).Where(pair =>
        {
            // Verify both have same score
            var configA = new DiscriminatorConfig { PropertyName = "sk", Pattern = pair.Item1, Strategy = DiscriminatorStrategy.StartsWith };
            var configB = new DiscriminatorConfig { PropertyName = "sk", Pattern = pair.Item2, Strategy = DiscriminatorStrategy.StartsWith };
            return PatternOverlapAnalyzer.ComputeSpecificityScore(configA)
                == PatternOverlapAnalyzer.ComputeSpecificityScore(configB)
                && PatternOverlapAnalyzer.PatternsOverlap(configA, configB);
        });

        return genClassName.Two().SelectMany(names =>
            genPropertyName.SelectMany(propName =>
                genSameScoreStartsWithPair.Select(patterns =>
                {
                    var (nameA, nameB) = names;
                    // Ensure distinct class names
                    if (nameA == nameB) nameB = nameB + "Child";

                    var entityA = CreateEntity(nameA, propName, patterns.Item1, DiscriminatorStrategy.StartsWith);
                    var entityB = CreateEntity(nameB, propName, patterns.Item2, DiscriminatorStrategy.StartsWith);
                    return new EntityPair(entityA, entityB);
                })));
    }

    /// <summary>
    /// Generates pairs of entities with same-score StartsWith patterns that overlap.
    /// Both patterns have the same number of non-empty literal segments.
    /// </summary>
    private static Gen<EntityPair> GenSameScoreStartsWithPair()
    {
        var genClassName = Gen.Elements(
            "Alpha", "Beta", "Gamma", "Delta", "Epsilon",
            "Zeta", "Eta", "Theta", "Iota", "Kappa");

        // Pairs where one literal prefix is a prefix of the other, with same segment count
        var genPatternPair = Gen.Elements(
            ("ITEM#*", "ITEM#*"),
            ("DATA#*", "DATA#*"),
            ("REC#*", "REC#*"),
            ("NODE#*", "NODE#*"),
            ("EDGE#*", "EDGE#*")
        );

        return genClassName.Two().SelectMany(names =>
            genPatternPair.Select(patterns =>
            {
                var (nameA, nameB) = names;
                if (nameA == nameB) nameB = nameB + "Alt";

                var entityA = CreateEntity(nameA, "sk", patterns.Item1, DiscriminatorStrategy.StartsWith);
                var entityB = CreateEntity(nameB, "sk", patterns.Item2, DiscriminatorStrategy.StartsWith);
                return new EntityPair(entityA, entityB);
            }));
    }

    /// <summary>
    /// Generates pairs of entities with same-score Contains patterns that overlap.
    /// Contains patterns overlap when one literal is a substring of the other.
    /// Both patterns must have the same specificity score (1 non-empty segment each).
    /// </summary>
    private static Gen<EntityPair> GenSameScoreContainsPair()
    {
        var genClassName = Gen.Elements(
            "Report", "Audit", "Log", "Event", "Metric",
            "Alert", "Signal", "Trace", "Record", "Entry");

        // Contains patterns where one literal is a substring of the other (ensuring overlap)
        // Both have 1 non-empty segment → same score
        var genPatternPair = Gen.Elements(
            ("*ORDER*", "*ORD*"),           // "ORD" ⊂ "ORDER"
            ("*#LINE#ITEM#*", "*#LINE#*"),  // "#LINE#" ⊂ "#LINE#ITEM#"
            ("*METADATA*", "*DATA*"),       // "DATA" ⊂ "METADATA"
            ("*#USER#*", "*USER*"),         // "USER" ⊂ "#USER#"
            ("*INVOICE*", "*VOICE*")        // "VOICE" ⊂ "INVOICE"
        );

        return genClassName.Two().SelectMany(names =>
            genPatternPair.Select(patterns =>
            {
                var (nameA, nameB) = names;
                if (nameA == nameB) nameB += "Ext";

                var entityA = CreateEntity(nameA, "sk", patterns.Item1, DiscriminatorStrategy.Contains);
                var entityB = CreateEntity(nameB, "sk", patterns.Item2, DiscriminatorStrategy.Contains);
                return new EntityPair(entityA, entityB);
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
