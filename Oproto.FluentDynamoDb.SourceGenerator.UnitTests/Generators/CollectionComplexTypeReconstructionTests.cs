// ============================================================================
// Collection Complex Type Reconstruction Tests
// ============================================================================
// These tests verify that the source generator correctly generates deserialization
// code for complex-type collection properties using FromDynamoDb instead of a
// TODO stub with new T().
//
// Requirements: 2.1, 2.2 from collection-complex-type-reconstruction-fix spec
// ============================================================================

using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for complex type collection property deserialization code generation.
/// Verifies that FromDynamoDb is used for Map AttributeValues and that the
/// TODO stub with new T() is no longer generated.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "collection-complex-type-reconstruction-fix")]
public class CollectionComplexTypeReconstructionTests
{
    /// <summary>
    /// Verifies that generated code for a complex-type collection property deserializes
    /// elements using FromDynamoDb from Map AttributeValues, includes Map and List checks,
    /// and does not contain TODO stubs or empty instance creation.
    /// 
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Fact]
    public void GenerateEntityImplementation_WithComplexTypeCollection_DeserializesFromMapAttributeValues()
    {
        // Arrange - Create entity with a complex-type collection property
        var entity = new EntityModel
        {
            ClassName = "CustomerEntity",
            Namespace = "TestNamespace",
            TableName = "customers",
            IsMultiItemEntity = true,
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
                },
                new PropertyModel
                {
                    PropertyName = "Addresses",
                    AttributeName = "addresses",
                    PropertyType = "List<Address>",
                    IsCollection = true
                }
            },
            Relationships = new[]
            {
                new RelationshipModel
                {
                    PropertyName = "Orders",
                    SortKeyPattern = "ORDER#*",
                    PropertyType = "List<OrderEntity>",
                    IsCollection = true,
                    EntityType = "OrderEntity"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert - Verify proper FromDynamoDb deserialization is generated
        result.Should().Contain("Address.FromDynamoDb<Address>(",
            "generated code should call FromDynamoDb to deserialize complex type elements from Map AttributeValues");

        // Assert - Verify no TODO stubs remain
        result.Should().NotContain("// TODO",
            "generated code should not contain TODO comments - the stub should be replaced with actual deserialization");

        // Assert - Verify no empty instance creation
        result.Should().NotContain("new Address()",
            "generated code should not create empty Address instances - it should deserialize using FromDynamoDb");

        // Assert - Verify Map null check is present (primary path)
        result.Should().Contain(".M != null",
            "generated code should check .M != null for Map AttributeValue deserialization");

        // Assert - Verify List null check is present (fallback path)
        result.Should().Contain(".L != null",
            "generated code should check .L != null for List-of-Maps fallback deserialization");

        // Assert - Verify try/catch error handling is present
        result.Should().Contain("try",
            "generated code should use try/catch for graceful error handling");
        result.Should().Contain("catch (Exception ex)",
            "generated code should catch exceptions during complex type deserialization");
    }
}
