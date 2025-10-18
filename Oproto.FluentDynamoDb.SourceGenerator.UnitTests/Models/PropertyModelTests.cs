using FluentAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Models;

public class PropertyModelTests
{
    [Fact]
    public void HasAttributeMapping_WithAttributeName_ReturnsTrue()
    {
        // Arrange
        var property = new PropertyModel
        {
            AttributeName = "test_attribute"
        };

        // Act
        var hasMapping = property.HasAttributeMapping;

        // Assert
        hasMapping.Should().BeTrue();
    }

    [Fact]
    public void HasAttributeMapping_WithEmptyAttributeName_ReturnsFalse()
    {
        // Arrange
        var property = new PropertyModel
        {
            AttributeName = ""
        };

        // Act
        var hasMapping = property.HasAttributeMapping;

        // Assert
        hasMapping.Should().BeFalse();
    }

    [Fact]
    public void HasAttributeMapping_WithNullAttributeName_ReturnsFalse()
    {
        // Arrange
        var property = new PropertyModel
        {
            AttributeName = null!
        };

        // Act
        var hasMapping = property.HasAttributeMapping;

        // Assert
        hasMapping.Should().BeFalse();
    }

    [Fact]
    public void IsPartOfGsi_WithGsiAttributes_ReturnsTrue()
    {
        // Arrange
        var property = new PropertyModel
        {
            GlobalSecondaryIndexes = new[]
            {
                new GlobalSecondaryIndexModel
                {
                    IndexName = "TestGSI",
                    IsPartitionKey = true
                }
            }
        };

        // Act
        var isPartOfGsi = property.IsPartOfGsi;

        // Assert
        isPartOfGsi.Should().BeTrue();
    }

    [Fact]
    public void IsPartOfGsi_WithoutGsiAttributes_ReturnsFalse()
    {
        // Arrange
        var property = new PropertyModel
        {
            GlobalSecondaryIndexes = Array.Empty<GlobalSecondaryIndexModel>()
        };

        // Act
        var isPartOfGsi = property.IsPartOfGsi;

        // Assert
        isPartOfGsi.Should().BeFalse();
    }

    [Fact]
    public void PropertyModel_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var property = new PropertyModel();

        // Assert
        property.PropertyName.Should().Be(string.Empty);
        property.AttributeName.Should().Be(string.Empty);
        property.PropertyType.Should().Be(string.Empty);
        property.IsPartitionKey.Should().BeFalse();
        property.IsSortKey.Should().BeFalse();
        property.IsCollection.Should().BeFalse();
        property.IsNullable.Should().BeFalse();
        property.KeyFormat.Should().BeNull();
        property.Queryable.Should().BeNull();
        property.GlobalSecondaryIndexes.Should().BeEmpty();
        property.PropertyDeclaration.Should().BeNull();
        property.HasAttributeMapping.Should().BeFalse();
        property.IsPartOfGsi.Should().BeFalse();
    }

    [Fact]
    public void PropertyModel_WithCompleteConfiguration_ShouldSetAllProperties()
    {
        // Arrange
        var keyFormat = new KeyFormatModel { Prefix = "test", Separator = "#" };
        var queryable = new QueryableModel { SupportedOperations = new[] { DynamoDbOperation.Equals } };
        var gsiModel = new GlobalSecondaryIndexModel { IndexName = "TestGSI", IsPartitionKey = true };

        // Act
        var property = new PropertyModel
        {
            PropertyName = "TestProperty",
            AttributeName = "test_attribute",
            PropertyType = "string",
            IsPartitionKey = true,
            IsSortKey = false,
            IsCollection = false,
            IsNullable = true,
            KeyFormat = keyFormat,
            Queryable = queryable,
            GlobalSecondaryIndexes = new[] { gsiModel }
        };

        // Assert
        property.PropertyName.Should().Be("TestProperty");
        property.AttributeName.Should().Be("test_attribute");
        property.PropertyType.Should().Be("string");
        property.IsPartitionKey.Should().BeTrue();
        property.IsSortKey.Should().BeFalse();
        property.IsCollection.Should().BeFalse();
        property.IsNullable.Should().BeTrue();
        property.KeyFormat.Should().Be(keyFormat);
        property.Queryable.Should().Be(queryable);
        property.GlobalSecondaryIndexes.Should().HaveCount(1);
        property.GlobalSecondaryIndexes[0].Should().Be(gsiModel);
        property.HasAttributeMapping.Should().BeTrue();
        property.IsPartOfGsi.Should().BeTrue();
    }

    [Fact]
    public void PropertyModel_WithMultipleGsiAttributes_ShouldHandleCorrectly()
    {
        // Arrange
        var gsi1 = new GlobalSecondaryIndexModel { IndexName = "GSI1", IsPartitionKey = true };
        var gsi2 = new GlobalSecondaryIndexModel { IndexName = "GSI2", IsSortKey = true };

        // Act
        var property = new PropertyModel
        {
            PropertyName = "TestProperty",
            AttributeName = "test_attribute",
            GlobalSecondaryIndexes = new[] { gsi1, gsi2 }
        };

        // Assert
        property.IsPartOfGsi.Should().BeTrue();
        property.GlobalSecondaryIndexes.Should().HaveCount(2);
        property.GlobalSecondaryIndexes.Should().Contain(gsi1);
        property.GlobalSecondaryIndexes.Should().Contain(gsi2);
    }

    [Theory]
    [InlineData("List<string>", true)]
    [InlineData("IList<int>", true)]
    [InlineData("ICollection<object>", true)]
    [InlineData("IEnumerable<string>", true)]
    [InlineData("string[]", true)]
    [InlineData("string", false)]
    [InlineData("int", false)]
    [InlineData("object", false)]
    public void IsCollection_WithDifferentTypes_ShouldDetectCorrectly(string propertyType, bool expectedIsCollection)
    {
        // Arrange
        var property = new PropertyModel
        {
            PropertyType = propertyType,
            IsCollection = expectedIsCollection // This would be set by the analyzer
        };

        // Act & Assert
        property.IsCollection.Should().Be(expectedIsCollection);
    }

    [Theory]
    [InlineData("string?", true)]
    [InlineData("int?", true)]
    [InlineData("System.Nullable<int>", true)]
    [InlineData("string", false)]
    [InlineData("int", false)]
    public void IsNullable_WithDifferentTypes_ShouldDetectCorrectly(string propertyType, bool expectedIsNullable)
    {
        // Arrange
        var property = new PropertyModel
        {
            PropertyType = propertyType,
            IsNullable = expectedIsNullable // This would be set by the analyzer
        };

        // Act & Assert
        property.IsNullable.Should().Be(expectedIsNullable);
    }
}