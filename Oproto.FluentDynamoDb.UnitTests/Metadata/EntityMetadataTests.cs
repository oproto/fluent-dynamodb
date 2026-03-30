using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.UnitTests.MetadataTests;

/// <summary>
/// Unit tests for EntityMetadata class.
/// </summary>
public class EntityMetadataTests
{
    [Fact]
    public void IsDynamicEntity_DefaultValue_IsFalse()
    {
        // Arrange & Act
        var metadata = new EntityMetadata();

        // Assert
        metadata.IsDynamicEntity.Should().BeFalse();
    }

    [Fact]
    public void IsDynamicEntity_WhenSetToTrue_ReturnsTrue()
    {
        // Arrange & Act
        var metadata = new EntityMetadata
        {
            IsDynamicEntity = true
        };

        // Assert
        metadata.IsDynamicEntity.Should().BeTrue();
    }

    [Fact]
    public void IsDynamicEntity_WhenExplicitlySetToFalse_ReturnsFalse()
    {
        // Arrange & Act
        var metadata = new EntityMetadata
        {
            IsDynamicEntity = false
        };

        // Assert
        metadata.IsDynamicEntity.Should().BeFalse();
    }

    [Fact]
    public void RegularEntityMetadata_HasIsDynamicEntityFalse()
    {
        // Arrange - Create metadata representing a regular typed entity
        var metadata = new EntityMetadata
        {
            TableName = "TestTable",
            PartitionKeyAttributeName = "pk",
            PartitionKeyAttributeType = "S",
            SortKeyAttributeName = "sk",
            SortKeyAttributeType = "S"
            // IsDynamicEntity not set - should default to false
        };

        // Assert
        metadata.IsDynamicEntity.Should().BeFalse();
    }

    [Fact]
    public void DynamicEntityMetadata_HasIsDynamicEntityTrue()
    {
        // Arrange - Create metadata representing a DynamicEntity
        // This simulates what DynamicEntity.GetEntityMetadata() will return
        var metadata = new EntityMetadata
        {
            TableName = string.Empty, // DynamicEntity has no fixed table name
            IsDynamicEntity = true,   // Key flag for DynamicEntity
            PartitionKeyAttributeName = string.Empty,
            PartitionKeyAttributeType = "S"
        };

        // Assert
        metadata.IsDynamicEntity.Should().BeTrue();
    }
}
