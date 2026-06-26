using System.Reflection;
using System.Runtime.Serialization;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete but needed for testing private methods

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for backwards compatibility of explicit discriminators.
/// Feature: unify-prefix-computed-discriminator, Property 7: Backwards Compatibility of Explicit Discriminators
/// </summary>
public class BackwardsCompatibilityPropertyTests
{
    private static readonly MethodInfo ApplyAutoDerivedDiscriminatorMethod =
        typeof(EntityAnalyzer).GetMethod(
            "ApplyAutoDerivedDiscriminator",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// Creates an EntityAnalyzer instance without calling the constructor,
    /// avoiding the Roslyn assembly dependency that fails at runtime in this test project.
    /// </summary>
    private static object CreateAnalyzer()
    {
        return FormatterServices.GetUninitializedObject(typeof(EntityAnalyzer));
    }

    /// <summary>
    /// Invokes the private instance ApplyAutoDerivedDiscriminator method via reflection.
    /// </summary>
    private static void InvokeApplyAutoDerivedDiscriminator(object analyzer, EntityModel entity)
    {
        ApplyAutoDerivedDiscriminatorMethod.Invoke(analyzer, new object[] { entity });
    }

    /// <summary>
    /// Generates DynamoDB attribute names for discriminator properties.
    /// </summary>
    private static Gen<string> GenDiscriminatorPropertyName()
    {
        return Gen.Elements(
            "sk", "pk", "entityType", "type", "entity_type",
            "sortKey", "partitionKey", "discriminator", "kind",
            "itemType", "category", "SK", "PK");
    }

    /// <summary>
    /// Generates explicit discriminator patterns (non-trivial patterns with at least one literal prefix).
    /// </summary>
    private static Gen<string> GenExplicitPattern()
    {
        return Gen.Elements(
            "ORDER#*", "USER#*", "CUSTOMER#*", "TENANT#*",
            "INVOICE#*", "PRODUCT#*", "EVENT#*", "SESSION#*",
            "ACCT#*", "META#*", "LINE#*", "DETAIL#*",
            "TENANT#*#USER#*", "ORDER#*#LINE#*",
            "PREFIX_*", "TYPE:*", "A#*#B#*#C#*");
    }

    /// <summary>
    /// Generates explicit discriminator exact values (used with DiscriminatorValue / ExactMatch strategy).
    /// </summary>
    private static Gen<string> GenExplicitValue()
    {
        return Gen.Elements(
            "ORDER", "USER", "CUSTOMER", "INVOICE",
            "PRODUCT", "EVENT", "SESSION", "ACCOUNT",
            "LineItem", "Metadata", "Config", "Settings");
    }

    /// <summary>
    /// Generates non-null derived discriminator patterns that the SK/PK might have.
    /// These represent what auto-derivation would have produced.
    /// </summary>
    private static Gen<string> GenDerivedPattern()
    {
        return Gen.Elements(
            "ORDER#*", "USER#*", "CUSTOMER#*", "TENANT#*",
            "INVOICE#*", "PRODUCT#*", "EVENT#*", "SESSION#*",
            "ACCT#*", "META#*", "LINE#*", "DETAIL#*",
            "TENANT#*#USER#*", "ORDER#*#LINE#*");
    }

    /// <summary>
    /// **Validates: Requirements 10.5, 10.7**
    /// For any entity with an explicit DiscriminatorProperty and DiscriminatorPattern,
    /// verify that ApplyAutoDerivedDiscriminator does NOT override the explicit discriminator.
    /// The explicit discriminator config remains intact after auto-derivation runs.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "7")]
    public Property ExplicitPatternDiscriminator_NotOverriddenByAutoDerivedDiscriminator()
    {
        var testCaseGen = from discProp in GenDiscriminatorPropertyName()
                          from explicitPattern in GenExplicitPattern()
                          from skDerivedPattern in GenDerivedPattern()
                          from pkDerivedPattern in GenDerivedPattern()
                          select (discProp, explicitPattern, skDerivedPattern, pkDerivedPattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (discProp, explicitPattern, skDerivedPattern, pkDerivedPattern) = testCase;

                var explicitStrategy = DiscriminatorAnalyzer.DeterminePatternStrategy(explicitPattern);

                // Arrange: entity with explicit valid discriminator (Pattern-based)
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = new DiscriminatorConfig
                    {
                        PropertyName = discProp,
                        Pattern = explicitPattern,
                        Strategy = explicitStrategy,
                        IsAutoDerived = false
                    },
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "Pk",
                            AttributeName = "pk",
                            IsPartitionKey = true,
                            DerivedDiscriminatorPattern = pkDerivedPattern
                        },
                        new PropertyModel
                        {
                            PropertyName = "Sk",
                            AttributeName = "sk",
                            IsSortKey = true,
                            DerivedDiscriminatorPattern = skDerivedPattern
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzer();
                InvokeApplyAutoDerivedDiscriminator(analyzer, entity);

                // Assert: explicit discriminator must be unchanged
                var discriminatorPreserved = entity.Discriminator != null;
                var propertyNameUnchanged = entity.Discriminator?.PropertyName == discProp;
                var patternUnchanged = entity.Discriminator?.Pattern == explicitPattern;
                var strategyUnchanged = entity.Discriminator?.Strategy == explicitStrategy;
                var stillExplicit = entity.Discriminator?.IsAutoDerived == false;

                return (discriminatorPreserved && propertyNameUnchanged && patternUnchanged &&
                        strategyUnchanged && stillExplicit).ToProperty()
                    .Label($"discProp='{discProp}', explicitPattern='{explicitPattern}', " +
                           $"discriminatorPreserved={discriminatorPreserved}, " +
                           $"propertyNameUnchanged={propertyNameUnchanged}, " +
                           $"patternUnchanged={patternUnchanged}, " +
                           $"strategyUnchanged={strategyUnchanged}, " +
                           $"stillExplicit={stillExplicit}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 10.5, 10.7**
    /// For any entity with an explicit DiscriminatorProperty and DiscriminatorValue (exact match),
    /// verify that ApplyAutoDerivedDiscriminator does NOT override the explicit discriminator.
    /// The explicit discriminator config remains intact after auto-derivation runs.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "7")]
    public Property ExplicitValueDiscriminator_NotOverriddenByAutoDerivedDiscriminator()
    {
        var testCaseGen = from discProp in GenDiscriminatorPropertyName()
                          from explicitValue in GenExplicitValue()
                          from skDerivedPattern in GenDerivedPattern()
                          from pkDerivedPattern in GenDerivedPattern()
                          select (discProp, explicitValue, skDerivedPattern, pkDerivedPattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (discProp, explicitValue, skDerivedPattern, pkDerivedPattern) = testCase;

                // Arrange: entity with explicit valid discriminator (ExactMatch / DiscriminatorValue)
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = new DiscriminatorConfig
                    {
                        PropertyName = discProp,
                        ExactValue = explicitValue,
                        Strategy = DiscriminatorStrategy.ExactMatch,
                        IsAutoDerived = false
                    },
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "Pk",
                            AttributeName = "pk",
                            IsPartitionKey = true,
                            DerivedDiscriminatorPattern = pkDerivedPattern
                        },
                        new PropertyModel
                        {
                            PropertyName = "Sk",
                            AttributeName = "sk",
                            IsSortKey = true,
                            DerivedDiscriminatorPattern = skDerivedPattern
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzer();
                InvokeApplyAutoDerivedDiscriminator(analyzer, entity);

                // Assert: explicit discriminator must be unchanged
                var discriminatorPreserved = entity.Discriminator != null;
                var propertyNameUnchanged = entity.Discriminator?.PropertyName == discProp;
                var exactValueUnchanged = entity.Discriminator?.ExactValue == explicitValue;
                var strategyUnchanged = entity.Discriminator?.Strategy == DiscriminatorStrategy.ExactMatch;
                var stillExplicit = entity.Discriminator?.IsAutoDerived == false;

                return (discriminatorPreserved && propertyNameUnchanged && exactValueUnchanged &&
                        strategyUnchanged && stillExplicit).ToProperty()
                    .Label($"discProp='{discProp}', explicitValue='{explicitValue}', " +
                           $"discriminatorPreserved={discriminatorPreserved}, " +
                           $"propertyNameUnchanged={propertyNameUnchanged}, " +
                           $"exactValueUnchanged={exactValueUnchanged}, " +
                           $"strategyUnchanged={strategyUnchanged}, " +
                           $"stillExplicit={stillExplicit}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 10.5, 10.7**
    /// For any entity with an explicit DiscriminatorProperty and DiscriminatorPattern
    /// that exactly matches what auto-derivation would produce, verify the explicit
    /// discriminator is still preserved (auto-derivation skips it entirely).
    /// This validates FDDB103 scenario — redundant explicit discriminator is preserved for backwards compat.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "7")]
    public Property RedundantExplicitDiscriminator_StillPreservedByAutoDerivedDiscriminator()
    {
        var testCaseGen = from pattern in GenDerivedPattern()
                          select pattern;

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            pattern =>
            {
                var strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern);

                // Arrange: entity where the explicit discriminator exactly matches
                // what auto-derivation would produce from the SK pattern.
                // The explicit discriminator should still be preserved unchanged.
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = new DiscriminatorConfig
                    {
                        PropertyName = "sk",
                        Pattern = pattern,
                        Strategy = strategy,
                        IsAutoDerived = false // Explicitly set by developer
                    },
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "Pk",
                            AttributeName = "pk",
                            IsPartitionKey = true,
                            DerivedDiscriminatorPattern = null
                        },
                        new PropertyModel
                        {
                            PropertyName = "Sk",
                            AttributeName = "sk",
                            IsSortKey = true,
                            DerivedDiscriminatorPattern = pattern // Same as explicit
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzer();
                InvokeApplyAutoDerivedDiscriminator(analyzer, entity);

                // Assert: explicit discriminator preserved, still marked as NOT auto-derived
                var discriminatorPreserved = entity.Discriminator != null;
                var propertyNameUnchanged = entity.Discriminator?.PropertyName == "sk";
                var patternUnchanged = entity.Discriminator?.Pattern == pattern;
                var strategyUnchanged = entity.Discriminator?.Strategy == strategy;
                var stillExplicit = entity.Discriminator?.IsAutoDerived == false;

                return (discriminatorPreserved && propertyNameUnchanged && patternUnchanged &&
                        strategyUnchanged && stillExplicit).ToProperty()
                    .Label($"pattern='{pattern}', " +
                           $"discriminatorPreserved={discriminatorPreserved}, " +
                           $"propertyNameUnchanged={propertyNameUnchanged}, " +
                           $"patternUnchanged={patternUnchanged}, " +
                           $"strategyUnchanged={strategyUnchanged}, " +
                           $"stillExplicit={stillExplicit}");
            });
    }
}
