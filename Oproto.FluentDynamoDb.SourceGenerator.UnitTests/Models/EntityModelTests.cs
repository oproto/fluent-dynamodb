using FluentAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Models;

public class EntityModelTests
{
    [Fact]
    public void PartitionKeyProperty_WithPartitionKeyProperty_ReturnsCorrectProperty()
    {
        // Arrange
        var entity = new EntityModel
        {
            Properties = new[]
            {
                new PropertyModel { PropertyName = "Id", IsPartitionKey = true },
                new PropertyModel { PropertyName = "Name", IsPartitionKey = false }
            }
        };

        // Act
        var partitionKey = entity.PartitionKeyProperty;

        // Assert
        partitionKey.Should().NotBeNull();
        partitionKey!.PropertyName.Should().Be("Id");
        partitionKey.IsPartitionKey.Should().BeTrue();
    }

    [Fact]
    public void PartitionKeyProperty_WithoutPartitionKeyProperty_ReturnsNull()
    {
        // Arrange
        var entity = new EntityModel
        {
            Properties = new[]
            {
                new PropertyModel { PropertyName = "Name", IsPartitionKey = false }
            }
        };

        // Act
        var partitionKey = entity.PartitionKeyProperty;

        // Assert
        partitionKey.Should().BeNull();
    }

    [Fact]
    public void SortKeyProperty_WithSortKeyProperty_ReturnsCorrectProperty()
    {
        // Arrange
        var entity = new EntityModel
        {
            Properties = new[]
            {
                new PropertyModel { PropertyName = "Id", IsPartitionKey = true },
                new PropertyModel { PropertyName = "SortKey", IsSortKey = true }
            }
        };

        // Act
        var sortKey = entity.SortKeyProperty;

        // Assert
        sortKey.Should().NotBeNull();
        sortKey!.PropertyName.Should().Be("SortKey");
        sortKey.IsSortKey.Should().BeTrue();
    }

    [Fact]
    public void SortKeyProperty_WithoutSortKeyProperty_ReturnsNull()
    {
        // Arrange
        var entity = new EntityModel
        {
            Properties = new[]
            {
                new PropertyModel { PropertyName = "Id", IsPartitionKey = true }
            }
        };

        // Act
        var sortKey = entity.SortKeyProperty;

        // Assert
        sortKey.Should().BeNull();
    }

    [Fact]
    public void HasValidKeyStructure_WithPartitionKey_ReturnsTrue()
    {
        // Arrange
        var entity = new EntityModel
        {
            Properties = new[]
            {
                new PropertyModel { PropertyName = "Id", IsPartitionKey = true }
            }
        };

        // Act
        var hasValidStructure = entity.HasValidKeyStructure;

        // Assert
        hasValidStructure.Should().BeTrue();
    }

    [Fact]
    public void HasValidKeyStructure_WithoutPartitionKey_ReturnsFalse()
    {
        // Arrange
        var entity = new EntityModel
        {
            Properties = new[]
            {
                new PropertyModel { PropertyName = "Name", IsPartitionKey = false }
            }
        };

        // Act
        var hasValidStructure = entity.HasValidKeyStructure;

        // Assert
        hasValidStructure.Should().BeFalse();
    }

    [Fact]
    public void EntityModel_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var entity = new EntityModel();

        // Assert
        entity.ClassName.Should().Be(string.Empty);
        entity.Namespace.Should().Be(string.Empty);
        entity.TableName.Should().Be(string.Empty);
        entity.EntityDiscriminator.Should().BeNull();
        entity.Properties.Should().BeEmpty();
        entity.Indexes.Should().BeEmpty();
        entity.Relationships.Should().BeEmpty();
        entity.IsMultiItemEntity.Should().BeFalse();
        entity.ClassDeclaration.Should().BeNull();
    }

    [Fact]
    public void EntityModel_WithCompleteConfiguration_ShouldSetAllProperties()
    {
        // Arrange & Act
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            EntityDiscriminator = "TEST_ENTITY",
            IsMultiItemEntity = true,
            Properties = new[]
            {
                new PropertyModel { PropertyName = "Id", IsPartitionKey = true },
                new PropertyModel { PropertyName = "SortKey", IsSortKey = true }
            },
            Indexes = new[]
            {
                new IndexModel { IndexName = "TestIndex" }
            },
            Relationships = new[]
            {
                new RelationshipModel { PropertyName = "RelatedItems" }
            }
        };

        // Assert
        entity.ClassName.Should().Be("TestEntity");
        entity.Namespace.Should().Be("TestNamespace");
        entity.TableName.Should().Be("test-table");
        entity.EntityDiscriminator.Should().Be("TEST_ENTITY");
        entity.IsMultiItemEntity.Should().BeTrue();
        entity.Properties.Should().HaveCount(2);
        entity.Indexes.Should().HaveCount(1);
        entity.Relationships.Should().HaveCount(1);
        entity.HasValidKeyStructure.Should().BeTrue();
    }
}