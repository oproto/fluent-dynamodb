using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for overlap detection symmetry and property scoping.
///
/// Feature: discriminator-enhancement, Property 3: Overlap detection is symmetric and property-scoped
/// </summary>
public class PatternOverlapSymmetryPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 3: Overlap detection is symmetric and property-scoped
    // Feature: discriminator-enhancement, Property 3: Overlap detection is symmetric and property-scoped
    // **Validates: Requirements 1.6, 2.1**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any two DiscriminatorConfig instances A and B,
    /// PatternsOverlap(A, B) SHALL return the same value as PatternsOverlap(B, A).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PatternsOverlap_IsSymmetric()
    {
        return Prop.ForAll(
            ArbDiscriminatorConfig(),
            ArbDiscriminatorConfig(),
            (a, b) =>
            {
                var ab = PatternOverlapAnalyzer.PatternsOverlap(a, b);
                var ba = PatternOverlapAnalyzer.PatternsOverlap(b, a);

                return ab == ba;
            });
    }

    /// <summary>
    /// For any two DiscriminatorConfig instances A and B with DIFFERENT PropertyName values,
    /// PatternsOverlap SHALL return false regardless of pattern content.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PatternsOverlap_DifferentProperties_AlwaysReturnsFalse()
    {
        return Prop.ForAll(
            ArbDiscriminatorConfigWithProperty(),
            ArbDiscriminatorConfigWithProperty(),
            (a, b) =>
            {
                // Ensure they have different property names
                if (string.Equals(a.PropertyName, b.PropertyName, StringComparison.Ordinal))
                {
                    // Make them different by appending a suffix
                    b = CloneWithDifferentProperty(b, a.PropertyName);
                }

                return !PatternOverlapAnalyzer.PatternsOverlap(a, b);
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Generators
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates arbitrary DiscriminatorConfig instances with various strategies,
    /// patterns, and a shared property name for symmetry testing.
    /// </summary>
    private static Arbitrary<DiscriminatorConfig> ArbDiscriminatorConfig()
    {
        var gen = Gen.OneOf(
            GenExactMatchConfig(),
            GenStartsWithConfig(),
            GenEndsWithConfig(),
            GenContainsConfig(),
            GenComplexConfig());

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates arbitrary DiscriminatorConfig instances with varied property names
    /// for testing property-scoping behavior.
    /// </summary>
    private static Arbitrary<DiscriminatorConfig> ArbDiscriminatorConfigWithProperty()
    {
        var propertyNames = Gen.Elements("sk", "pk", "entity_type", "gsi1sk", "type", "category");

        var gen = from propName in propertyNames
                  from config in Gen.OneOf(
                      GenExactMatchConfig(),
                      GenStartsWithConfig(),
                      GenEndsWithConfig(),
                      GenContainsConfig(),
                      GenComplexConfig())
                  select new DiscriminatorConfig
                  {
                      PropertyName = propName,
                      Strategy = config.Strategy,
                      ExactValue = config.ExactValue,
                      Pattern = config.Pattern
                  };

        return gen.ToArbitrary();
    }

    private static Gen<DiscriminatorConfig> GenExactMatchConfig()
    {
        return from value in GenLiteralSegment()
               select new DiscriminatorConfig
               {
                   PropertyName = "sk",
                   Strategy = DiscriminatorStrategy.ExactMatch,
                   ExactValue = value,
                   Pattern = null
               };
    }

    private static Gen<DiscriminatorConfig> GenStartsWithConfig()
    {
        return from prefix in GenLiteralSegment()
               select new DiscriminatorConfig
               {
                   PropertyName = "sk",
                   Strategy = DiscriminatorStrategy.StartsWith,
                   Pattern = prefix + "*",
                   ExactValue = null
               };
    }

    private static Gen<DiscriminatorConfig> GenEndsWithConfig()
    {
        return from suffix in GenLiteralSegment()
               select new DiscriminatorConfig
               {
                   PropertyName = "sk",
                   Strategy = DiscriminatorStrategy.EndsWith,
                   Pattern = "*" + suffix,
                   ExactValue = null
               };
    }

    private static Gen<DiscriminatorConfig> GenContainsConfig()
    {
        return from middle in GenLiteralSegment()
               select new DiscriminatorConfig
               {
                   PropertyName = "sk",
                   Strategy = DiscriminatorStrategy.Contains,
                   Pattern = "*" + middle + "*",
                   ExactValue = null
               };
    }

    private static Gen<DiscriminatorConfig> GenComplexConfig()
    {
        return from count in Gen.Choose(2, 4)
               from segments in Gen.ListOf(count, GenLiteralSegment())
               let pattern = string.Join("*", segments) + "*"
               select new DiscriminatorConfig
               {
                   PropertyName = "sk",
                   Strategy = DiscriminatorStrategy.Complex,
                   Pattern = pattern,
                   ExactValue = null
               };
    }

    /// <summary>
    /// Generates realistic literal segments that resemble DynamoDB key patterns
    /// (e.g., "INVOICE#", "#LINE#", "USER", "ORDER#").
    /// </summary>
    private static Gen<string> GenLiteralSegment()
    {
        return Gen.Elements(
            "INVOICE#", "#LINE#", "#ADJUSTMENT#", "USER#", "ORDER#",
            "#AUDIT", "#META", "CUSTOMER#", "#ITEM#", "PRODUCT#",
            "A#", "B#", "#C#", "TYPE_", "STATUS_");
    }

    /// <summary>
    /// Creates a clone of a DiscriminatorConfig with a property name guaranteed
    /// to differ from the excluded property name.
    /// </summary>
    private static DiscriminatorConfig CloneWithDifferentProperty(DiscriminatorConfig config, string excludedPropertyName)
    {
        var differentName = config.PropertyName == excludedPropertyName
            ? config.PropertyName + "_other"
            : config.PropertyName;

        return new DiscriminatorConfig
        {
            PropertyName = differentName,
            Strategy = config.Strategy,
            ExactValue = config.ExactValue,
            Pattern = config.Pattern
        };
    }
}
