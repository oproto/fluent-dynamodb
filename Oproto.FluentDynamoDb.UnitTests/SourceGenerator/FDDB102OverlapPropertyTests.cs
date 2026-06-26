using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for FDDB102 emission constraint in PatternOverlapAnalyzer.
/// Feature: unify-prefix-computed-discriminator, Property 8: FDDB102 Emission Constraint
/// </summary>
public class FDDB102OverlapPropertyTests
{
    /// <summary>
    /// Generates pairs of overlapping patterns with different specificity.
    /// The first pattern is less specific (fewer literal segments) than the second.
    /// Both patterns share the same property name ("sk") so they overlap.
    /// </summary>
    private static Gen<(string lessSpecific, string moreSpecific)> GenOverlappingPatternPair()
    {
        return Gen.Elements(
            ("ORDER#*", "ORDER#*#LINE#*"),
            ("TENANT#*", "TENANT#*#USER#*"),
            ("CUSTOMER#*", "CUSTOMER#*#ORDER#*"),
            ("INVOICE#*", "INVOICE#*#LINE#*"),
            ("PRODUCT#*", "PRODUCT#*#VARIANT#*"),
            ("EVENT#*", "EVENT#*#DETAIL#*"),
            ("SESSION#*", "SESSION#*#ACTION#*"),
            ("ACCT#*", "ACCT#*#TXN#*"),
            ("META#*", "META#*#ENTRY#*"),
            ("USER#*", "USER#*#PROFILE#*"));
    }

    /// <summary>
    /// Generates DynamoDB attribute names for the discriminator property.
    /// </summary>
    private static Gen<string> GenAttributeName()
    {
        return Gen.Elements(
            "sk", "sortKey", "SK", "sort_key",
            "pk", "partitionKey", "PK", "type");
    }

    /// <summary>
    /// Generates entity class names.
    /// </summary>
    private static Gen<string> GenClassName()
    {
        return Gen.Elements(
            "OrderEntity", "CustomerEntity", "InvoiceEntity",
            "ProductEntity", "UserEntity", "TenantEntity",
            "SessionEntity", "EventEntity", "LineEntity",
            "DetailEntity", "ProfileEntity", "AccountEntity");
    }

    /// <summary>
    /// Creates an EntityModel with a discriminator configured.
    /// </summary>
    private static EntityModel CreateEntityWithDiscriminator(
        string className, string tableName, string attributeName,
        string pattern, bool isAutoDerived)
    {
        var strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern);
        return new EntityModel
        {
            ClassName = className,
            TableName = tableName,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = attributeName,
                Pattern = pattern,
                Strategy = strategy,
                IsAutoDerived = isAutoDerived
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = attributeName,
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = pattern
                }
            }
        };
    }

    /// <summary>
    /// **Validates: Requirements 5.1, 5.6**
    /// For any pair of overlapping patterns with different specificity where both are auto-derived,
    /// FDDB102 SHALL be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "8")]
    public Property FDDB102_Emitted_WhenBothAutoDerived()
    {
        var testCaseGen = from patterns in GenOverlappingPatternPair()
                          from attrName in GenAttributeName()
                          from classNameA in GenClassName()
                          from classNameB in GenClassName()
                          where classNameA != classNameB
                          select (patterns.lessSpecific, patterns.moreSpecific, attrName, classNameA, classNameB);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (lessSpecificPattern, moreSpecificPattern, attrName, classNameA, classNameB) = testCase;

                // Arrange: two entities with overlapping auto-derived patterns, different specificity
                var entityA = CreateEntityWithDiscriminator(
                    classNameA, "shared-table", attrName, lessSpecificPattern, isAutoDerived: true);
                var entityB = CreateEntityWithDiscriminator(
                    classNameB, "shared-table", attrName, moreSpecificPattern, isAutoDerived: true);

                var tableEntities = new List<EntityModel> { entityA, entityB };

                // Act
                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                // Assert: FDDB102 should be emitted
                var fddb102Emitted = diagnostics.Any(d => d.Id == "FDDB102");

                return fddb102Emitted.ToProperty()
                    .Label($"lessSpecific='{lessSpecificPattern}', moreSpecific='{moreSpecificPattern}', " +
                           $"attrName='{attrName}', classA='{classNameA}', classB='{classNameB}', " +
                           $"fddb102Emitted={fddb102Emitted}, " +
                           $"diagnosticCount={diagnostics.Count}, " +
                           $"diagnosticIds=[{string.Join(", ", diagnostics.Select(d => d.Id))}]");
            });
    }

    /// <summary>
    /// **Validates: Requirements 5.1, 5.6**
    /// For any pair of overlapping patterns with different specificity where one pattern is explicit
    /// (not auto-derived), FDDB102 SHALL NOT be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "8")]
    public Property FDDB102_NotEmitted_WhenOneIsExplicit()
    {
        var testCaseGen = from patterns in GenOverlappingPatternPair()
                          from attrName in GenAttributeName()
                          from classNameA in GenClassName()
                          from classNameB in GenClassName()
                          from explicitIsLessSpecific in Gen.Elements(true, false)
                          where classNameA != classNameB
                          select (patterns.lessSpecific, patterns.moreSpecific, attrName,
                                  classNameA, classNameB, explicitIsLessSpecific);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (lessSpecificPattern, moreSpecificPattern, attrName,
                     classNameA, classNameB, explicitIsLessSpecific) = testCase;

                // Arrange: one entity is explicit, the other auto-derived
                var entityA = CreateEntityWithDiscriminator(
                    classNameA, "shared-table", attrName, lessSpecificPattern,
                    isAutoDerived: !explicitIsLessSpecific);
                var entityB = CreateEntityWithDiscriminator(
                    classNameB, "shared-table", attrName, moreSpecificPattern,
                    isAutoDerived: explicitIsLessSpecific);

                var tableEntities = new List<EntityModel> { entityA, entityB };

                // Act
                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                // Assert: FDDB102 should NOT be emitted
                var fddb102Emitted = diagnostics.Any(d => d.Id == "FDDB102");

                return (!fddb102Emitted).ToProperty()
                    .Label($"lessSpecific='{lessSpecificPattern}', moreSpecific='{moreSpecificPattern}', " +
                           $"attrName='{attrName}', explicitIsLessSpecific={explicitIsLessSpecific}, " +
                           $"fddb102Emitted={fddb102Emitted}, " +
                           $"diagnosticIds=[{string.Join(", ", diagnostics.Select(d => d.Id))}]");
            });
    }

    /// <summary>
    /// **Validates: Requirements 5.1, 5.6**
    /// For any pair of overlapping patterns with different specificity where both patterns are explicit,
    /// FDDB102 SHALL NOT be emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "8")]
    public Property FDDB102_NotEmitted_WhenBothExplicit()
    {
        var testCaseGen = from patterns in GenOverlappingPatternPair()
                          from attrName in GenAttributeName()
                          from classNameA in GenClassName()
                          from classNameB in GenClassName()
                          where classNameA != classNameB
                          select (patterns.lessSpecific, patterns.moreSpecific, attrName, classNameA, classNameB);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (lessSpecificPattern, moreSpecificPattern, attrName, classNameA, classNameB) = testCase;

                // Arrange: both entities have explicit (not auto-derived) discriminators
                var entityA = CreateEntityWithDiscriminator(
                    classNameA, "shared-table", attrName, lessSpecificPattern, isAutoDerived: false);
                var entityB = CreateEntityWithDiscriminator(
                    classNameB, "shared-table", attrName, moreSpecificPattern, isAutoDerived: false);

                var tableEntities = new List<EntityModel> { entityA, entityB };

                // Act
                var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                // Assert: FDDB102 should NOT be emitted
                var fddb102Emitted = diagnostics.Any(d => d.Id == "FDDB102");

                return (!fddb102Emitted).ToProperty()
                    .Label($"lessSpecific='{lessSpecificPattern}', moreSpecific='{moreSpecificPattern}', " +
                           $"attrName='{attrName}', classA='{classNameA}', classB='{classNameB}', " +
                           $"fddb102Emitted={fddb102Emitted}, " +
                           $"diagnosticIds=[{string.Join(", ", diagnostics.Select(d => d.Id))}]");
            });
    }
}
