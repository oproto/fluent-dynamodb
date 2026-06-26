using System.Reflection;
using System.Runtime.Serialization;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete but needed for testing private methods

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for EntityAnalyzer.ApplyAutoDerivedDiscriminator.
/// Feature: unify-prefix-computed-discriminator, Property 3: Discriminator Selection Priority
/// </summary>
public class DiscriminatorSelectionPropertyTests
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
    /// Generates DynamoDB attribute names for sort key properties.
    /// </summary>
    private static Gen<string> GenSkAttributeName()
    {
        return Gen.Elements(
            "sk", "sortKey", "SK", "sort_key", "gsi1sk",
            "range", "rangeKey", "entitySort", "itemType",
            "skValue", "compositeSort", "sortAttr");
    }

    /// <summary>
    /// Generates DynamoDB attribute names for partition key properties.
    /// </summary>
    private static Gen<string> GenPkAttributeName()
    {
        return Gen.Elements(
            "pk", "partitionKey", "PK", "partition_key", "gsi1pk",
            "hash", "hashKey", "entityPk", "itemPk",
            "pkValue", "compositePk", "partAttr");
    }

    /// <summary>
    /// Generates non-null derived discriminator patterns (patterns that start with a literal prefix).
    /// These represent patterns derived from key formats like "PREFIX#*".
    /// </summary>
    private static Gen<string> GenNonNullDerivedPattern()
    {
        return Gen.Elements(
            "ORDER#*", "USER#*", "CUSTOMER#*", "TENANT#*",
            "INVOICE#*", "PRODUCT#*", "EVENT#*", "SESSION#*",
            "ACCT#*", "META#*", "LINE#*", "DETAIL#*",
            "TENANT#*#USER#*", "ORDER#*#LINE#*",
            "PREFIX_*", "TYPE:*");
    }

    /// <summary>
    /// **Validates: Requirements 2.6, 2.8, 2.9**
    /// For any entity without explicit discriminator where SK has a non-null derived pattern,
    /// verify entity's auto-derived discriminator uses SK's attribute name and pattern.
    /// The sort key is always preferred when it has a non-null derived pattern.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "3")]
    public Property SkPreferred_WhenSkHasNonNullDerivedPattern()
    {
        var testCaseGen = from skAttr in GenSkAttributeName()
                          from pkAttr in GenPkAttributeName()
                          from skPattern in GenNonNullDerivedPattern()
                          from pkPattern in GenNonNullDerivedPattern()
                          where skAttr != pkAttr // Ensure PK and SK have different attribute names
                          select (skAttr, pkAttr, skPattern, pkPattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (skAttr, pkAttr, skPattern, pkPattern) = testCase;

                // Arrange: entity with no explicit discriminator,
                // both SK and PK have non-null DerivedDiscriminatorPattern
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = null, // No explicit discriminator
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "Pk",
                            AttributeName = pkAttr,
                            IsPartitionKey = true,
                            DerivedDiscriminatorPattern = pkPattern
                        },
                        new PropertyModel
                        {
                            PropertyName = "Sk",
                            AttributeName = skAttr,
                            IsSortKey = true,
                            DerivedDiscriminatorPattern = skPattern
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzer();
                InvokeApplyAutoDerivedDiscriminator(analyzer, entity);

                // Assert: SK should be selected
                var discriminatorSet = entity.Discriminator != null;
                var usesSkAttr = entity.Discriminator?.PropertyName == skAttr;
                var usesSkPattern = entity.Discriminator?.Pattern == skPattern;
                var isAutoDerived = entity.Discriminator?.IsAutoDerived == true;
                var strategyCorrect = entity.Discriminator?.Strategy ==
                    DiscriminatorAnalyzer.DeterminePatternStrategy(skPattern);

                return (discriminatorSet && usesSkAttr && usesSkPattern &&
                        isAutoDerived && strategyCorrect).ToProperty()
                    .Label($"skAttr='{skAttr}', pkAttr='{pkAttr}', " +
                           $"skPattern='{skPattern}', pkPattern='{pkPattern}', " +
                           $"discriminatorSet={discriminatorSet}, usesSkAttr={usesSkAttr}, " +
                           $"usesSkPattern={usesSkPattern}, isAutoDerived={isAutoDerived}, " +
                           $"strategyCorrect={strategyCorrect}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 2.6, 2.8, 2.9**
    /// For any entity without explicit discriminator where SK has a non-null derived pattern
    /// and PK has a null pattern, the SK pattern is still selected (SK preferred).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "3")]
    public Property SkSelected_WhenSkHasPatternAndPkDoesNot()
    {
        var testCaseGen = from skAttr in GenSkAttributeName()
                          from pkAttr in GenPkAttributeName()
                          from skPattern in GenNonNullDerivedPattern()
                          where skAttr != pkAttr
                          select (skAttr, pkAttr, skPattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (skAttr, pkAttr, skPattern) = testCase;

                // Arrange: entity with SK having a pattern, PK has null (trivial format)
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Discriminator = null,
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "Pk",
                            AttributeName = pkAttr,
                            IsPartitionKey = true,
                            DerivedDiscriminatorPattern = null // Trivial "{0}" format
                        },
                        new PropertyModel
                        {
                            PropertyName = "Sk",
                            AttributeName = skAttr,
                            IsSortKey = true,
                            DerivedDiscriminatorPattern = skPattern
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzer();
                InvokeApplyAutoDerivedDiscriminator(analyzer, entity);

                // Assert: SK should be selected
                var discriminatorSet = entity.Discriminator != null;
                var usesSkAttr = entity.Discriminator?.PropertyName == skAttr;
                var usesSkPattern = entity.Discriminator?.Pattern == skPattern;
                var isAutoDerived = entity.Discriminator?.IsAutoDerived == true;

                return (discriminatorSet && usesSkAttr && usesSkPattern && isAutoDerived).ToProperty()
                    .Label($"skAttr='{skAttr}', skPattern='{skPattern}', " +
                           $"discriminatorSet={discriminatorSet}, usesSkAttr={usesSkAttr}, " +
                           $"usesSkPattern={usesSkPattern}, isAutoDerived={isAutoDerived}");
            });
    }
}
