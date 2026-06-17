using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for PatternOverlapAnalyzer.Analyze — resolved overlap diagnostics.
///
/// Feature: discriminator-enhancement, Property 9: Resolved overlaps produce an informational diagnostic
/// **Validates: Requirements 2.5**
/// </summary>
public class ResolvedOverlapDiagnosticsPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 9: Resolved overlaps produce an informational diagnostic
    // Feature: discriminator-enhancement, Property 9: Resolved overlaps produce an informational diagnostic
    // **Validates: Requirements 2.5**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any pair of entities in the same table group with overlapping patterns
    /// on the same DiscriminatorProperty AND different specificity scores,
    /// the analyzer SHALL emit a diagnostic with severity Info containing
    /// the less-specific entity name, the more-specific entity name, and the excluded pattern.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Analyze_DifferentScoreOverlappingPatterns_EmitsInfoDiagnosticWithBothNamesAndPattern()
    {
        return Prop.ForAll(
            GenDifferentScoreOverlappingEntityPair().ToArbitrary(),
            pair =>
            {
                // Clear OverlappingPatterns before each test run since Analyze mutates entities
                pair.LessSpecific.Discriminator!.OverlappingPatterns.Clear();
                pair.MoreSpecific.Discriminator!.OverlappingPatterns.Clear();

                var tableEntities = new List<EntityModel> { pair.LessSpecific, pair.MoreSpecific };

                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                // Should have at least one Info diagnostic
                var infoDiagnostics = diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Info)
                    .ToList();

                if (infoDiagnostics.Count == 0)
                    return false;

                // At least one Info diagnostic should mention the less-specific entity,
                // the more-specific entity, and the excluded pattern
                var moreSpecificPattern = pair.MoreSpecific.Discriminator!.Pattern
                    ?? pair.MoreSpecific.Discriminator!.ExactValue
                    ?? string.Empty;

                var hasAllDetails = infoDiagnostics.Any(d =>
                {
                    var message = d.GetMessage();
                    return message.Contains(pair.LessSpecific.ClassName)
                        && message.Contains(pair.MoreSpecific.ClassName)
                        && message.Contains(moreSpecificPattern);
                });

                return hasAllDetails;
            });
    }

    /// <summary>
    /// For any pair of entities with different-score overlapping patterns,
    /// the analyzer SHALL emit at least one diagnostic with Info severity (not Error).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Analyze_DifferentScoreOverlap_EmitsInfoNotError()
    {
        return Prop.ForAll(
            GenDifferentScoreOverlappingEntityPair().ToArbitrary(),
            pair =>
            {
                pair.LessSpecific.Discriminator!.OverlappingPatterns.Clear();
                pair.MoreSpecific.Discriminator!.OverlappingPatterns.Clear();

                var tableEntities = new List<EntityModel> { pair.LessSpecific, pair.MoreSpecific };

                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var hasInfo = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Info);
                var hasError = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

                // Should produce Info but not Error diagnostics
                return hasInfo && !hasError;
            });
    }

    /// <summary>
    /// For any pair with different-score overlapping patterns using ExactMatch as more-specific,
    /// the analyzer SHALL emit an Info diagnostic mentioning the exact value as the excluded pattern.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Analyze_ExactMatchMoreSpecific_EmitsInfoDiagnosticWithExactValue()
    {
        return Prop.ForAll(
            GenExactMatchVsWildcardPair().ToArbitrary(),
            pair =>
            {
                pair.LessSpecific.Discriminator!.OverlappingPatterns.Clear();
                pair.MoreSpecific.Discriminator!.OverlappingPatterns.Clear();

                var tableEntities = new List<EntityModel> { pair.LessSpecific, pair.MoreSpecific };

                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var infoDiagnostics = diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Info)
                    .ToList();

                if (infoDiagnostics.Count == 0)
                    return false;

                // The excluded pattern should be the ExactMatch value
                var exactValue = pair.MoreSpecific.Discriminator!.ExactValue ?? string.Empty;

                return infoDiagnostics.Any(d =>
                {
                    var message = d.GetMessage();
                    return message.Contains(pair.LessSpecific.ClassName)
                        && message.Contains(pair.MoreSpecific.ClassName)
                        && message.Contains(exactValue);
                });
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test Data Model
    // ──────────────────────────────────────────────────────────────────────

    private record EntityPair(EntityModel LessSpecific, EntityModel MoreSpecific);

    // ──────────────────────────────────────────────────────────────────────
    // Generators
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates pairs of entities with overlapping patterns that have DIFFERENT specificity scores.
    /// The less-specific entity has a lower score (fewer literal segments) and the more-specific
    /// entity has a higher score (more literal segments).
    /// </summary>
    private static Gen<EntityPair> GenDifferentScoreOverlappingEntityPair()
    {
        var genClassName = Gen.Elements(
            "Invoice", "Order", "User", "Product", "Customer",
            "Payment", "Shipment", "Refund", "Receipt", "Ticket");

        var genPropertyName = Gen.Elements("sk", "pk", "gsi1sk", "entity_type", "type_key");

        // Pairs where the first pattern is less-specific (score 1) and the second is
        // more-specific (score 2+). Both overlap because the more-specific pattern
        // starts with the same prefix as the less-specific one.
        var genOverlappingPatternPair = Gen.Elements(
            ("INVOICE#*", "INVOICE#*#LINE#*"),       // score 1 vs score 2
            ("ORDER#*", "ORDER#*#ITEM#*"),           // score 1 vs score 2
            ("USER#*", "USER#*#SESSION#*"),          // score 1 vs score 2
            ("CUST#*", "CUST#*#ADDR#*"),            // score 1 vs score 2
            ("PAY#*", "PAY#*#TXN#*#DETAIL#*")      // score 1 vs score 3
        );

        return genClassName.Two().SelectMany(names =>
            genPropertyName.SelectMany(propName =>
                genOverlappingPatternPair.Select(patterns =>
                {
                    var (nameA, nameB) = names;
                    // Ensure distinct class names
                    if (nameA == nameB) nameB = nameB + "Line";

                    var lessSpecific = CreateEntity(nameA, propName, patterns.Item1, DiscriminatorStrategy.StartsWith);
                    var moreSpecific = CreateEntity(nameB, propName, patterns.Item2, DiscriminatorStrategy.StartsWith);
                    return new EntityPair(lessSpecific, moreSpecific);
                })));
    }

    /// <summary>
    /// Generates pairs where the more-specific entity uses ExactMatch (score int.MaxValue)
    /// and the less-specific entity uses a wildcard pattern that the exact value matches.
    /// </summary>
    private static Gen<EntityPair> GenExactMatchVsWildcardPair()
    {
        var genClassName = Gen.Elements(
            "Alpha", "Beta", "Gamma", "Delta", "Epsilon",
            "Zeta", "Eta", "Theta", "Iota", "Kappa");

        // Pairs: wildcard pattern (less-specific) and an ExactMatch value that matches it
        var genPatternExactPair = Gen.Elements(
            ("INVOICE#*", "INVOICE#001"),
            ("ORDER#*", "ORDER#SPECIAL"),
            ("USER#*", "USER#ADMIN"),
            ("DATA#*", "DATA#CONFIG"),
            ("REC#*", "REC#HEADER")
        );

        return genClassName.Two().SelectMany(names =>
            genPatternExactPair.Select(pair =>
            {
                var (nameA, nameB) = names;
                if (nameA == nameB) nameB = nameB + "Exact";

                var lessSpecific = CreateEntity(nameA, "sk", pair.Item1, DiscriminatorStrategy.StartsWith);
                var moreSpecific = CreateEntityExactMatch(nameB, "sk", pair.Item2);
                return new EntityPair(lessSpecific, moreSpecific);
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

    private static EntityModel CreateEntityExactMatch(
        string className,
        string propertyName,
        string exactValue)
    {
        return new EntityModel
        {
            ClassName = className,
            Namespace = "TestNamespace",
            TableName = "test-table",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = propertyName,
                ExactValue = exactValue,
                Strategy = DiscriminatorStrategy.ExactMatch,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };
    }
}
