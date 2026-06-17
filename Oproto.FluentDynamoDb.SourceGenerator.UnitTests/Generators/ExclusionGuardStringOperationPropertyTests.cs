using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for MapperGenerator exclusion guard code generation — string operation correctness.
///
/// Feature: discriminator-enhancement, Property 6: Exclusion guard uses the correct string operation
/// **Validates: Requirements 3.1, 3.2**
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class ExclusionGuardStringOperationPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 6: Exclusion guard uses the correct string operation
    // Feature: discriminator-enhancement, Property 6: Exclusion guard uses the correct string operation
    // **Validates: Requirements 3.1, 3.2**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any exclusion pattern with StartsWith strategy, the generated code SHALL contain
    /// a call to .StartsWith("literalText") with the exclusion's literal text.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExclusionGuard_StartsWithStrategy_GeneratesStartsWithCall()
    {
        return Prop.ForAll(
            GenExclusionPatternWithStrategy(DiscriminatorStrategy.StartsWith).ToArbitrary(),
            exclusion =>
            {
                var entity = CreateEntityWithExclusion(exclusion);
                var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

                // The generated code should contain .StartsWith("literalText") for this exclusion
                var expectedCall = $"discriminatorValue.S.StartsWith(\"{exclusion.LiteralText}\")";
                return generatedCode.Contains(expectedCall)
                    .Label($"Expected '{expectedCall}' in generated code for exclusion from {exclusion.EntityName}");
            });
    }

    /// <summary>
    /// For any exclusion pattern with EndsWith strategy, the generated code SHALL contain
    /// a call to .EndsWith("literalText") with the exclusion's literal text.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExclusionGuard_EndsWithStrategy_GeneratesEndsWithCall()
    {
        return Prop.ForAll(
            GenExclusionPatternWithStrategy(DiscriminatorStrategy.EndsWith).ToArbitrary(),
            exclusion =>
            {
                var entity = CreateEntityWithExclusion(exclusion);
                var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

                // The generated code should contain .EndsWith("literalText") for this exclusion
                var expectedCall = $"discriminatorValue.S.EndsWith(\"{exclusion.LiteralText}\")";
                return generatedCode.Contains(expectedCall)
                    .Label($"Expected '{expectedCall}' in generated code for exclusion from {exclusion.EntityName}");
            });
    }

    /// <summary>
    /// For any exclusion pattern with Contains strategy, the generated code SHALL contain
    /// a call to .Contains("literalText") with the exclusion's literal text.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExclusionGuard_ContainsStrategy_GeneratesContainsCall()
    {
        return Prop.ForAll(
            GenExclusionPatternWithStrategy(DiscriminatorStrategy.Contains).ToArbitrary(),
            exclusion =>
            {
                var entity = CreateEntityWithExclusion(exclusion);
                var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

                // The generated code should contain .Contains("literalText") for this exclusion
                var expectedCall = $"discriminatorValue.S.Contains(\"{exclusion.LiteralText}\")";
                return generatedCode.Contains(expectedCall)
                    .Label($"Expected '{expectedCall}' in generated code for exclusion from {exclusion.EntityName}");
            });
    }

    /// <summary>
    /// For any exclusion pattern with ExactMatch strategy, the generated code SHALL contain
    /// an equality check == "literalText" with the exclusion's literal text.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExclusionGuard_ExactMatchStrategy_GeneratesEqualityCheck()
    {
        return Prop.ForAll(
            GenExclusionPatternWithStrategy(DiscriminatorStrategy.ExactMatch).ToArbitrary(),
            exclusion =>
            {
                var entity = CreateEntityWithExclusion(exclusion);
                var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

                // The generated code should contain == "literalText" for this exclusion
                var expectedCall = $"discriminatorValue.S == \"{exclusion.LiteralText}\"";
                return generatedCode.Contains(expectedCall)
                    .Label($"Expected '{expectedCall}' in generated code for exclusion from {exclusion.EntityName}");
            });
    }

    /// <summary>
    /// For any exclusion pattern with any supported strategy, the generated code SHALL contain
    /// the string method call matching the strategy — verifying the mapping is correct across all strategies.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExclusionGuard_AnyStrategy_GeneratesCorrectStringOperation()
    {
        return Prop.ForAll(
            GenExclusionPatternWithAnyStrategy().ToArbitrary(),
            exclusion =>
            {
                var entity = CreateEntityWithExclusion(exclusion);
                var generatedCode = MapperGenerator.GenerateEntityImplementation(entity);

                var expectedCall = exclusion.Strategy switch
                {
                    DiscriminatorStrategy.StartsWith => $"discriminatorValue.S.StartsWith(\"{exclusion.LiteralText}\")",
                    DiscriminatorStrategy.EndsWith => $"discriminatorValue.S.EndsWith(\"{exclusion.LiteralText}\")",
                    DiscriminatorStrategy.Contains => $"discriminatorValue.S.Contains(\"{exclusion.LiteralText}\")",
                    DiscriminatorStrategy.ExactMatch => $"discriminatorValue.S == \"{exclusion.LiteralText}\"",
                    _ => string.Empty
                };

                if (string.IsNullOrEmpty(expectedCall))
                    return true.Label("Unsupported strategy skipped");

                return generatedCode.Contains(expectedCall)
                    .Label($"Strategy={exclusion.Strategy}, Expected '{expectedCall}'");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Generators
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates ExclusionPattern instances with a specific strategy and random literal text.
    /// </summary>
    private static Gen<ExclusionPattern> GenExclusionPatternWithStrategy(DiscriminatorStrategy strategy)
    {
        var genEntityName = Gen.Elements(
            "InvoiceLine", "OrderLine", "Payment", "Adjustment",
            "LineItem", "Shipment", "Receipt", "Refund", "Detail", "SubItem");

        var genLiteralText = Gen.Elements(
            "INVOICE#", "ORDER#", "#LINE#", "#PAYMENT#", "USER#",
            "#DETAIL#", "ITEM#", "#AUDIT", "#META#", "CUSTOMER#",
            "#ADJUSTMENT#", "PRODUCT#", "#VARIANT#", "#HOURS#", "TENANT#");

        var genPattern = strategy switch
        {
            DiscriminatorStrategy.StartsWith => genLiteralText.Select(lit => lit + "*"),
            DiscriminatorStrategy.EndsWith => genLiteralText.Select(lit => "*" + lit),
            DiscriminatorStrategy.Contains => genLiteralText.Select(lit => "*" + lit + "*"),
            DiscriminatorStrategy.ExactMatch => genLiteralText.Select(lit => lit.TrimEnd('#')),
            _ => genLiteralText
        };

        return genEntityName.SelectMany(entityName =>
            genLiteralText.SelectMany(literal =>
                genPattern.Select(pattern => new ExclusionPattern
                {
                    EntityName = entityName,
                    Pattern = pattern,
                    Strategy = strategy,
                    LiteralText = literal
                })));
    }

    /// <summary>
    /// Generates ExclusionPattern instances with any of the four supported strategies.
    /// </summary>
    private static Gen<ExclusionPattern> GenExclusionPatternWithAnyStrategy()
    {
        return Gen.Elements(
            DiscriminatorStrategy.StartsWith,
            DiscriminatorStrategy.EndsWith,
            DiscriminatorStrategy.Contains,
            DiscriminatorStrategy.ExactMatch
        ).SelectMany(strategy => GenExclusionPatternWithStrategy(strategy));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a minimal EntityModel with a discriminator configured with the given exclusion pattern.
    /// The entity uses a StartsWith strategy for its own discriminator so exclusion guards are generated.
    /// </summary>
    private static EntityModel CreateEntityWithExclusion(ExclusionPattern exclusion)
    {
        return new EntityModel
        {
            ClassName = "ParentEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            TableEntityCount = 2,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true
                }
            },
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "PARENT#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                OverlappingPatterns = new List<ExclusionPattern> { exclusion }
            }
        };
    }
}
