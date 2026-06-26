using System.Reflection;
using System.Runtime.Serialization;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Property-based tests for EntityAnalyzer.ApplyAutoDerivedGsiDiscriminator.
/// Feature: unify-prefix-computed-discriminator, Property 9: GSI Discriminator Auto-Derivation
/// </summary>
public class GsiDiscriminatorDerivationPropertyTests
{
    private static readonly MethodInfo ApplyAutoDerivedGsiDiscriminatorMethod =
        typeof(EntityAnalyzer).GetMethod(
            "ApplyAutoDerivedGsiDiscriminator",
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
    /// Invokes the private instance ApplyAutoDerivedGsiDiscriminator method via reflection.
    /// </summary>
    private static void InvokeApplyAutoDerivedGsiDiscriminator(object analyzer, EntityModel entity)
    {
        ApplyAutoDerivedGsiDiscriminatorMethod.Invoke(analyzer, new object[] { entity });
    }

    /// <summary>
    /// Generates non-empty GSI index names.
    /// </summary>
    private static Gen<string> GenIndexName()
    {
        return Gen.Elements(
            "gsi1", "gsi2", "gsi3", "status-index", "email-index",
            "category-index", "date-index", "type-index", "owner-index",
            "region-index", "tenant-index", "user-index");
    }

    /// <summary>
    /// Generates DynamoDB attribute names for GSI PK properties.
    /// </summary>
    private static Gen<string> GenAttributeName()
    {
        return Gen.Elements(
            "gsi1pk", "gsi2pk", "status", "email", "category",
            "entityType", "ownerPk", "regionPk", "tenantPk",
            "pk", "sk", "gsi1sk", "type");
    }

    /// <summary>
    /// Generates non-null derived discriminator patterns (patterns that start with a literal prefix).
    /// These represent patterns that would have been derived from a key format like "PREFIX#*".
    /// </summary>
    private static Gen<string> GenNonNullDerivedPattern()
    {
        return Gen.Elements(
            "ORDER#*", "USER#*", "CUSTOMER#*", "TENANT#*",
            "INVOICE#*", "PRODUCT#*", "EVENT#*", "SESSION#*",
            "ACCT#*", "META#*", "STATUS#*", "EMAIL#*",
            "TENANT#*#USER#*", "ORDER#*#LINE#*",
            "PREFIX_*", "TYPE:*");
    }

    /// <summary>
    /// **Validates: Requirements 9.1, 9.5, 9.6**
    /// For any GSI partition key property with a DerivedDiscriminatorPattern that is not null
    /// and no explicit GsiDiscriminator configured, the IndexModel.GsiDiscriminator SHALL be
    /// populated with PropertyName equal to the GSI PK property's DynamoDbAttribute name and
    /// Pattern equal to its DerivedDiscriminatorPattern.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "9")]
    public Property GsiDiscriminator_AutoDerived_WhenPatternNotNullAndNoExplicit()
    {
        var testCaseGen = from indexName in GenIndexName()
                          from attributeName in GenAttributeName()
                          from derivedPattern in GenNonNullDerivedPattern()
                          select (indexName, attributeName, derivedPattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (indexName, attributeName, derivedPattern) = testCase;

                // Arrange: create an entity with a GSI that has no explicit discriminator
                // and a property that is the GSI PK with a non-null DerivedDiscriminatorPattern
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "GsiPkProp",
                            AttributeName = attributeName,
                            PropertyType = "string",
                            DerivedDiscriminatorPattern = derivedPattern,
                            GsiPartitionKeys = new[]
                            {
                                new GsiPartitionKeyModel { IndexName = indexName }
                            }
                        }
                    },
                    Indexes = new[]
                    {
                        new IndexModel
                        {
                            IndexName = indexName,
                            IndexType = IndexType.GlobalSecondaryIndex,
                            GsiDiscriminator = null // No explicit discriminator
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzer();
                InvokeApplyAutoDerivedGsiDiscriminator(analyzer, entity);

                // Assert: GsiDiscriminator should be populated
                var gsiDisc = entity.Indexes[0].GsiDiscriminator;
                var isPopulated = gsiDisc != null;
                var propertyNameCorrect = gsiDisc?.PropertyName == attributeName;
                var patternCorrect = gsiDisc?.Pattern == derivedPattern;
                var isAutoDerived = gsiDisc?.IsAutoDerived == true;
                var strategyCorrect = gsiDisc?.Strategy ==
                    DiscriminatorAnalyzer.DeterminePatternStrategy(derivedPattern);

                return (isPopulated && propertyNameCorrect && patternCorrect &&
                        isAutoDerived && strategyCorrect).ToProperty()
                    .Label($"indexName='{indexName}', attributeName='{attributeName}', " +
                           $"derivedPattern='{derivedPattern}', " +
                           $"isPopulated={isPopulated}, propertyNameCorrect={propertyNameCorrect}, " +
                           $"patternCorrect={patternCorrect}, isAutoDerived={isAutoDerived}, " +
                           $"strategyCorrect={strategyCorrect}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 9.1, 9.5, 9.6**
    /// For any GSI partition key property with a null DerivedDiscriminatorPattern (trivial key format),
    /// the IndexModel.GsiDiscriminator SHALL NOT be populated.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "9")]
    public Property GsiDiscriminator_NotPopulated_WhenPatternIsNull()
    {
        var testCaseGen = from indexName in GenIndexName()
                          from attributeName in GenAttributeName()
                          select (indexName, attributeName);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (indexName, attributeName) = testCase;

                // Arrange: GSI PK property has null DerivedDiscriminatorPattern (trivial format "{0}")
                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "GsiPkProp",
                            AttributeName = attributeName,
                            PropertyType = "string",
                            DerivedDiscriminatorPattern = null, // Trivial key format
                            GsiPartitionKeys = new[]
                            {
                                new GsiPartitionKeyModel { IndexName = indexName }
                            }
                        }
                    },
                    Indexes = new[]
                    {
                        new IndexModel
                        {
                            IndexName = indexName,
                            IndexType = IndexType.GlobalSecondaryIndex,
                            GsiDiscriminator = null
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzer();
                InvokeApplyAutoDerivedGsiDiscriminator(analyzer, entity);

                // Assert: GsiDiscriminator should remain null
                var remainsNull = entity.Indexes[0].GsiDiscriminator == null;

                return remainsNull.ToProperty()
                    .Label($"indexName='{indexName}', attributeName='{attributeName}', " +
                           $"GsiDiscriminator should be null but was: {entity.Indexes[0].GsiDiscriminator}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 9.1, 9.5, 9.6**
    /// For any GSI with an explicit GsiDiscriminator already set,
    /// ApplyAutoDerivedGsiDiscriminator SHALL NOT override it.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "unify-prefix-computed-discriminator")]
    [Trait("Property", "9")]
    public Property GsiDiscriminator_NotOverridden_WhenExplicitExists()
    {
        var testCaseGen = from indexName in GenIndexName()
                          from attributeName in GenAttributeName()
                          from derivedPattern in GenNonNullDerivedPattern()
                          from explicitPattern in GenNonNullDerivedPattern()
                          select (indexName, attributeName, derivedPattern, explicitPattern);

        return Prop.ForAll(
            testCaseGen.ToArbitrary(),
            testCase =>
            {
                var (indexName, attributeName, derivedPattern, explicitPattern) = testCase;

                // Arrange: GSI already has an explicit discriminator
                var explicitDiscriminator = new DiscriminatorConfig
                {
                    PropertyName = "explicitProp",
                    Pattern = explicitPattern,
                    Strategy = DiscriminatorStrategy.StartsWith,
                    IsAutoDerived = false
                };

                var entity = new EntityModel
                {
                    ClassName = "TestEntity",
                    TableName = "test-table",
                    Properties = new[]
                    {
                        new PropertyModel
                        {
                            PropertyName = "GsiPkProp",
                            AttributeName = attributeName,
                            PropertyType = "string",
                            DerivedDiscriminatorPattern = derivedPattern,
                            GsiPartitionKeys = new[]
                            {
                                new GsiPartitionKeyModel { IndexName = indexName }
                            }
                        }
                    },
                    Indexes = new[]
                    {
                        new IndexModel
                        {
                            IndexName = indexName,
                            IndexType = IndexType.GlobalSecondaryIndex,
                            GsiDiscriminator = explicitDiscriminator // Already set
                        }
                    }
                };

                // Act
                var analyzer = CreateAnalyzer();
                InvokeApplyAutoDerivedGsiDiscriminator(analyzer, entity);

                // Assert: GsiDiscriminator should remain the explicit one
                var gsiDisc = entity.Indexes[0].GsiDiscriminator;
                var notOverridden = ReferenceEquals(gsiDisc, explicitDiscriminator);
                var propertyNamePreserved = gsiDisc?.PropertyName == "explicitProp";
                var patternPreserved = gsiDisc?.Pattern == explicitPattern;

                return (notOverridden && propertyNamePreserved && patternPreserved).ToProperty()
                    .Label($"indexName='{indexName}', explicitPattern='{explicitPattern}', " +
                           $"notOverridden={notOverridden}, " +
                           $"propertyNamePreserved={propertyNamePreserved}, " +
                           $"patternPreserved={patternPreserved}");
            });
    }
}
