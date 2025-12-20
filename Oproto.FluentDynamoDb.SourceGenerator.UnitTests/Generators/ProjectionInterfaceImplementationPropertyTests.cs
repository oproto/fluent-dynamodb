using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text.RegularExpressions;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for projection interface implementation generation.
/// 
/// **Feature: projection-interface-enhancement, Property 1: Generated projections implement both interfaces**
/// **Validates: Requirements 2.1, 6.4**
/// </summary>
public class ProjectionInterfaceImplementationPropertyTests
{
    /// <summary>
    /// Property 1: For any generated projection class, it SHALL implement both
    /// IProjectionModel&lt;TSelf&gt; and IReadOnlyEntity interfaces.
    /// **Validates: Requirements 2.1, 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedProjection_ShouldImplementBothInterfaces()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                var projection = CreateTestProjection(cleanProjectionName, cleanSourceEntityName);
                
                // Act
                var generatedCode = ProjectionExpressionGenerator.GenerateFromDynamoDbMethod(projection);
                
                // Assert - generated code should implement IProjectionModel<TSelf> AND IReadOnlyEntity
                var implementsProjectionModel = 
                    generatedCode.Contains($"IProjectionModel<{cleanProjectionName}>") ||
                    generatedCode.Contains($"IDiscriminatedProjection<{cleanProjectionName}>");
                
                var implementsReadOnlyEntity = generatedCode.Contains("IReadOnlyEntity");
                
                return implementsProjectionModel && implementsReadOnlyEntity;
            });
    }

    /// <summary>
    /// Property 1: For any generated projection class with discriminator, it SHALL implement
    /// IDiscriminatedProjection&lt;TSelf&gt; interface.
    /// **Validates: Requirements 2.1, 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedProjectionWithDiscriminator_ShouldImplementDiscriminatedProjection()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName, discriminatorValue) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                var cleanDiscriminatorValue = SanitizeName(discriminatorValue.Get);
                
                var projection = CreateTestProjectionWithDiscriminator(
                    cleanProjectionName, 
                    cleanSourceEntityName, 
                    cleanDiscriminatorValue);
                
                // Act
                var generatedCode = ProjectionExpressionGenerator.GenerateFromDynamoDbMethod(projection);
                
                // Assert - generated code should implement IDiscriminatedProjection<TSelf>
                return generatedCode.Contains($"IDiscriminatedProjection<{cleanProjectionName}>");
            });
    }

    /// <summary>
    /// Property 1: For any generated projection class without discriminator, it SHALL implement
    /// IProjectionModel&lt;TSelf&gt; interface.
    /// **Validates: Requirements 2.1, 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedProjectionWithoutDiscriminator_ShouldImplementProjectionModel()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                var projection = CreateTestProjection(cleanProjectionName, cleanSourceEntityName);
                // Ensure no discriminator
                projection.Discriminator = null;
                
                // Act
                var generatedCode = ProjectionExpressionGenerator.GenerateFromDynamoDbMethod(projection);
                
                // Assert - generated code should implement IProjectionModel<TSelf>
                return generatedCode.Contains($"IProjectionModel<{cleanProjectionName}>");
            });
    }

    /// <summary>
    /// Property 1: For any generated projection, the FromDynamoDb method SHALL be generated.
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedProjection_ShouldHaveFromDynamoDbMethod()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                var projection = CreateTestProjection(cleanProjectionName, cleanSourceEntityName);
                
                // Act
                var generatedCode = ProjectionExpressionGenerator.GenerateFromDynamoDbMethod(projection);
                
                // Assert - generated code should have FromDynamoDb method
                return generatedCode.Contains($"public static {cleanProjectionName} FromDynamoDb(Dictionary<string, AttributeValue> item)");
            });
    }

    /// <summary>
    /// Property 1: For any generated projection, the class SHALL be partial.
    /// **Validates: Requirements 2.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedProjection_ShouldBePartialClass()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                var projection = CreateTestProjection(cleanProjectionName, cleanSourceEntityName);
                
                // Act
                var generatedCode = ProjectionExpressionGenerator.GenerateFromDynamoDbMethod(projection);
                
                // Assert - generated code should be a partial class
                return generatedCode.Contains($"public partial class {cleanProjectionName}");
            });
    }

    /// <summary>
    /// Property 1: For any generated projection, it SHALL have GetPartitionKey method.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedProjection_ShouldHaveGetPartitionKeyMethod()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                var projection = CreateTestProjection(cleanProjectionName, cleanSourceEntityName);
                
                // Act
                var generatedCode = ProjectionExpressionGenerator.GenerateFromDynamoDbMethod(projection);
                
                // Assert - generated code should have GetPartitionKey method
                return generatedCode.Contains("public static string GetPartitionKey(Dictionary<string, AttributeValue> item)");
            });
    }

    /// <summary>
    /// Property 1: For any generated projection, it SHALL have GetEntityMetadata method.
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedProjection_ShouldHaveGetEntityMetadataMethod()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                var projection = CreateTestProjection(cleanProjectionName, cleanSourceEntityName);
                
                // Act
                var generatedCode = ProjectionExpressionGenerator.GenerateFromDynamoDbMethod(projection);
                
                // Assert - generated code should have GetEntityMetadata method
                return generatedCode.Contains("public static EntityMetadata GetEntityMetadata()");
            });
    }

    #region Helper Methods

    private static string SanitizeName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "Test" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static ProjectionModel CreateTestProjection(string projectionName, string sourceEntityName)
    {
        return new ProjectionModel
        {
            ClassName = projectionName,
            Namespace = "TestNamespace",
            SourceEntityType = sourceEntityName,
            Properties = new[]
            {
                new ProjectionPropertyModel
                {
                    PropertyName = "Id",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsNullable = false
                },
                new ProjectionPropertyModel
                {
                    PropertyName = "Name",
                    PropertyType = "string",
                    AttributeName = "name",
                    IsNullable = true
                }
            },
            ProjectionExpression = "pk, name"
        };
    }

    private static ProjectionModel CreateTestProjectionWithDiscriminator(
        string projectionName, 
        string sourceEntityName, 
        string discriminatorValue)
    {
        return new ProjectionModel
        {
            ClassName = projectionName,
            Namespace = "TestNamespace",
            SourceEntityType = sourceEntityName,
            Properties = new[]
            {
                new ProjectionPropertyModel
                {
                    PropertyName = "Id",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsNullable = false
                }
            },
            ProjectionExpression = "pk, entity_type",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "entity_type",
                ExactValue = discriminatorValue,
                Strategy = DiscriminatorStrategy.ExactMatch
            }
        };
    }

    #endregion
}


/// <summary>
/// Property-based tests for ProjectionExpression property preservation.
/// 
/// **Feature: projection-interface-enhancement, Property 12: ProjectionExpression property preservation**
/// **Validates: Requirements 2.5, 6.5**
/// </summary>
public class ProjectionExpressionPreservationPropertyTests
{
    /// <summary>
    /// Property 12: For any generated projection, it SHALL maintain the existing
    /// ProjectionExpression static property.
    /// **Validates: Requirements 2.5, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedProjection_ShouldHaveProjectionExpressionProperty()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                var projection = CreateTestProjection(cleanProjectionName, cleanSourceEntityName);
                
                // Act
                var generatedCode = ProjectionExpressionGenerator.GenerateFromDynamoDbMethod(projection);
                
                // Assert - generated code should have ProjectionExpression property
                return generatedCode.Contains("public static string ProjectionExpression =>");
            });
    }

    /// <summary>
    /// Property 12: For any generated projection, the ProjectionExpression SHALL contain
    /// all projected attribute names.
    /// **Validates: Requirements 2.5, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedProjection_ProjectionExpressionShouldContainAllAttributes()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName, attributeName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                var cleanAttributeName = SanitizeAttributeName(attributeName.Get);
                
                var projection = CreateTestProjectionWithAttribute(
                    cleanProjectionName, 
                    cleanSourceEntityName, 
                    cleanAttributeName);
                
                // Act
                var projectionExpression = ProjectionExpressionGenerator.GenerateProjectionExpression(projection);
                
                // Assert - projection expression should contain the attribute name
                return projectionExpression.Contains(cleanAttributeName);
            });
    }

    /// <summary>
    /// Property 12: For any generated projection with multiple properties, the ProjectionExpression
    /// SHALL contain all attribute names separated by commas.
    /// **Validates: Requirements 2.5, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedProjection_ProjectionExpressionShouldBeCommaSeparated()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                var projection = CreateTestProjectionWithMultipleAttributes(
                    cleanProjectionName, 
                    cleanSourceEntityName);
                
                // Act
                var projectionExpression = ProjectionExpressionGenerator.GenerateProjectionExpression(projection);
                
                // Assert - projection expression should be comma-separated
                var parts = projectionExpression.Split(',').Select(p => p.Trim()).ToList();
                
                // Should have at least 2 parts (pk and name)
                return parts.Count >= 2 && 
                       parts.Contains("pk") && 
                       parts.Contains("name");
            });
    }

    /// <summary>
    /// Property 12: For any generated projection, the ProjectionExpression in the generated code
    /// SHALL match the expression from GenerateProjectionExpression.
    /// **Validates: Requirements 2.5, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeneratedProjection_ProjectionExpressionShouldMatchGeneratedExpression()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (projectionName, sourceEntityName) =>
            {
                // Arrange
                var cleanProjectionName = SanitizeName(projectionName.Get);
                var cleanSourceEntityName = SanitizeName(sourceEntityName.Get);
                
                var projection = CreateTestProjection(cleanProjectionName, cleanSourceEntityName);
                
                // Act
                var expectedExpression = ProjectionExpressionGenerator.GenerateProjectionExpression(projection);
                var generatedCode = ProjectionExpressionGenerator.GenerateFromDynamoDbMethod(projection);
                
                // Assert - generated code should contain the expected projection expression
                return generatedCode.Contains($"public static string ProjectionExpression => \"{expectedExpression}\"");
            });
    }

    #region Helper Methods

    private static string SanitizeName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "Test" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static string SanitizeAttributeName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "attr";
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static ProjectionModel CreateTestProjection(string projectionName, string sourceEntityName)
    {
        return new ProjectionModel
        {
            ClassName = projectionName,
            Namespace = "TestNamespace",
            SourceEntityType = sourceEntityName,
            Properties = new[]
            {
                new ProjectionPropertyModel
                {
                    PropertyName = "Id",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsNullable = false
                },
                new ProjectionPropertyModel
                {
                    PropertyName = "Name",
                    PropertyType = "string",
                    AttributeName = "name",
                    IsNullable = true
                }
            },
            ProjectionExpression = "pk, name"
        };
    }

    private static ProjectionModel CreateTestProjectionWithAttribute(
        string projectionName, 
        string sourceEntityName, 
        string attributeName)
    {
        return new ProjectionModel
        {
            ClassName = projectionName,
            Namespace = "TestNamespace",
            SourceEntityType = sourceEntityName,
            Properties = new[]
            {
                new ProjectionPropertyModel
                {
                    PropertyName = "TestProperty",
                    PropertyType = "string",
                    AttributeName = attributeName,
                    IsNullable = false
                }
            },
            ProjectionExpression = attributeName
        };
    }

    private static ProjectionModel CreateTestProjectionWithMultipleAttributes(
        string projectionName, 
        string sourceEntityName)
    {
        return new ProjectionModel
        {
            ClassName = projectionName,
            Namespace = "TestNamespace",
            SourceEntityType = sourceEntityName,
            Properties = new[]
            {
                new ProjectionPropertyModel
                {
                    PropertyName = "Id",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsNullable = false
                },
                new ProjectionPropertyModel
                {
                    PropertyName = "Name",
                    PropertyType = "string",
                    AttributeName = "name",
                    IsNullable = true
                },
                new ProjectionPropertyModel
                {
                    PropertyName = "Status",
                    PropertyType = "string",
                    AttributeName = "status",
                    IsNullable = true
                }
            },
            ProjectionExpression = "pk, name, status"
        };
    }

    #endregion
}
