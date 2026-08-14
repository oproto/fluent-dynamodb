using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Preservation tests for the extracted property type conversion fix.
/// These tests verify that EXISTING working behavior is unchanged by the fix:
/// - String extracted properties use direct assignment (no conversion)
/// - Non-extracted enum properties serialize with .ToString() and deserialize with Enum.Parse
/// - Computed key generation logic is unchanged
///
/// These tests MUST PASS on both unfixed and fixed code.
///
/// **Feature: extracted-property-type-conversion, Property 3, 4, 5: Preservation**
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**
/// </summary>
[Trait("Category", "Preservation")]
public class ExtractedPropertyTypeConversionPreservationTests
{
    /// <summary>
    /// Test 1: Entity with string [Extracted("Pk", 0)] property.
    /// Assert generated FromDynamoDb contains entity.Component = pkParts[0] (direct assignment, no conversion).
    /// Validates: Requirement 3.1
    /// </summary>
    [Fact]
    public void StringExtractedProperty_FromDynamoDb_ShouldContainDirectAssignment()
    {
        // Arrange
        var entity = CreateEntityWithStringExtractedProperty();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - string extracted properties should use direct assignment without any Parse call
        result.Should().Contain("entity.Component = pkParts[0]",
            "String extracted properties should be directly assigned from the parts array without any conversion");
    }

    /// <summary>
    /// Test 2: Entity with string [Extracted("Pk", 0)] property.
    /// Assert generated ExtractPkComponents returns parts[0] directly.
    /// Validates: Requirement 3.1
    /// </summary>
    [Fact]
    public void StringExtractedProperty_ExtractionHelper_ShouldReturnDirectly()
    {
        // Arrange
        var entity = CreateEntityWithStringExtractedProperty();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - string extraction should return parts[index] directly without any conversion
        result.Should().Contain("parts[0]",
            "String extracted properties should be returned directly from parts array");
        result.Should().NotContain("Parse(parts[0])",
            "String extracted properties should not use any Parse conversion");
    }

    /// <summary>
    /// Test 3: Entity with multiple string extracted properties.
    /// Assert all assignments are direct string assignments.
    /// Validates: Requirement 3.1, 3.6
    /// </summary>
    [Fact]
    public void MultipleStringExtractedProperties_FromDynamoDb_ShouldAllBeDirectAssignments()
    {
        // Arrange
        var entity = CreateEntityWithMultipleStringExtractedProperties();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - all string extracted properties should use direct assignment
        result.Should().Contain("entity.TenantId = pkParts[0]",
            "First string extracted property should use direct assignment");
        result.Should().Contain("entity.UserId = pkParts[1]",
            "Second string extracted property should use direct assignment");
    }

    /// <summary>
    /// Test 4: Entity with non-extracted enum property (regular [DynamoDbAttribute]).
    /// Assert ToDynamoDb still generates new AttributeValue { S = ... .ToString() } for the enum.
    /// Validates: Requirement 3.2
    /// </summary>
    [Fact]
    public void NonExtractedEnumProperty_ToDynamoDb_ShouldUseToString()
    {
        // Arrange
        var entity = CreateEntityWithNonExtractedEnumProperty();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - enum serialization should use .ToString() within an AttributeValue { S = ... }
        result.Should().Contain(".ToString()",
            "Non-extracted enum properties should serialize using .ToString()");
        result.Should().Contain("S =",
            "Non-extracted enum properties should be stored as DynamoDB String type");
    }

    /// <summary>
    /// Test 5: Entity with non-extracted enum property.
    /// Assert FromDynamoDb still generates Enum.Parse&lt;T&gt;(attr.S) for the enum.
    /// Validates: Requirement 3.3
    /// </summary>
    [Fact]
    public void NonExtractedEnumProperty_FromDynamoDb_ShouldUseEnumParse()
    {
        // Arrange
        var entity = CreateEntityWithNonExtractedEnumProperty();

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - enum deserialization should use Enum.Parse<T>
        result.Should().Contain("Enum.Parse<OrderStatus>",
            "Non-extracted enum properties should deserialize using Enum.Parse<T>");
    }

    /// <summary>
    /// Test 6: Entity with [Computed] property alongside [Extracted] properties.
    /// Assert computed key generation logic is unchanged.
    /// Validates: Requirement 3.5
    /// </summary>
    [Fact]
    public void ComputedPropertyWithExtracted_KeysGeneration_ShouldGeneratePkMethod()
    {
        // Arrange
        var entity = CreateEntityWithComputedAndExtractedProperties();

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - computed key should generate Pk method
        result.Should().Contain("Pk(",
            "Computed key should generate a Pk method");
        result.Should().Contain("ExtractPkComponents",
            "Extracted properties should generate an ExtractPkComponents method");
    }

    #region Helper Methods

    private static EntityModel CreateEntityWithStringExtractedProperty()
    {
        return new EntityModel
        {
            ClassName = "Document",
            Namespace = "TestNamespace",
            TableName = "documents",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "Component", "SubComponent" },
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Component",
                    AttributeName = "component",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Pk",
                        Index = 0,
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "SubComponent",
                    AttributeName = "subComponent",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Pk",
                        Index = 1,
                        Separator = "#"
                    }
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Update | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    private static EntityModel CreateEntityWithMultipleStringExtractedProperties()
    {
        return new EntityModel
        {
            ClassName = "UserRecord",
            Namespace = "TestNamespace",
            TableName = "users",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "TenantId", "UserId" },
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "TenantId",
                    AttributeName = "tenantId",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Pk",
                        Index = 0,
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "UserId",
                    AttributeName = "userId",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Pk",
                        Index = 1,
                        Separator = "#"
                    }
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Update | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    private static EntityModel CreateEntityWithNonExtractedEnumProperty()
    {
        return new EntityModel
        {
            ClassName = "OrderEntity",
            Namespace = "TestNamespace",
            TableName = "orders",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Status",
                    AttributeName = "status",
                    PropertyType = "OrderStatus"
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Update | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    private static EntityModel CreateEntityWithComputedAndExtractedProperties()
    {
        return new EntityModel
        {
            ClassName = "Event",
            Namespace = "TestNamespace",
            TableName = "events",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "Region", "EventId" },
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Region",
                    AttributeName = "region",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Pk",
                        Index = 0,
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "EventId",
                    AttributeName = "eventId",
                    PropertyType = "string",
                    ExtractedKey = new ExtractedKeyModel
                    {
                        SourceProperty = "Pk",
                        Index = 1,
                        Separator = "#"
                    }
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>
            {
                new AccessorConfig
                {
                    Operations = TableOperation.Get | TableOperation.Update | TableOperation.Delete,
                    Modifier = SourceGenAccessModifier.Public
                }
            }
        };
    }

    #endregion
}
