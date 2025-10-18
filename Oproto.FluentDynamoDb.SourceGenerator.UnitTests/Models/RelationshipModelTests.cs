using FluentAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Models;

public class RelationshipModelTests
{
    [Fact]
    public void IsWildcardPattern_WithWildcardPattern_ReturnsTrue()
    {
        // Arrange
        var relationship = new RelationshipModel
        {
            SortKeyPattern = "audit#*"
        };

        // Act
        var isWildcard = relationship.IsWildcardPattern;

        // Assert
        isWildcard.Should().BeTrue();
    }

    [Fact]
    public void IsWildcardPattern_WithoutWildcardPattern_ReturnsFalse()
    {
        // Arrange
        var relationship = new RelationshipModel
        {
            SortKeyPattern = "audit"
        };

        // Act
        var isWildcard = relationship.IsWildcardPattern;

        // Assert
        isWildcard.Should().BeFalse();
    }

    [Fact]
    public void IsWildcardPattern_WithMultipleWildcards_ReturnsTrue()
    {
        // Arrange
        var relationship = new RelationshipModel
        {
            SortKeyPattern = "audit#*#*"
        };

        // Act
        var isWildcard = relationship.IsWildcardPattern;

        // Assert
        isWildcard.Should().BeTrue();
    }

    [Fact]
    public void HasSpecificEntityType_WithEntityType_ReturnsTrue()
    {
        // Arrange
        var relationship = new RelationshipModel
        {
            EntityType = "AuditEntry"
        };

        // Act
        var hasSpecificType = relationship.HasSpecificEntityType;

        // Assert
        hasSpecificType.Should().BeTrue();
    }

    [Fact]
    public void HasSpecificEntityType_WithoutEntityType_ReturnsFalse()
    {
        // Arrange
        var relationship = new RelationshipModel
        {
            EntityType = null
        };

        // Act
        var hasSpecificType = relationship.HasSpecificEntityType;

        // Assert
        hasSpecificType.Should().BeFalse();
    }

    [Fact]
    public void HasSpecificEntityType_WithEmptyEntityType_ReturnsFalse()
    {
        // Arrange
        var relationship = new RelationshipModel
        {
            EntityType = ""
        };

        // Act
        var hasSpecificType = relationship.HasSpecificEntityType;

        // Assert
        hasSpecificType.Should().BeFalse();
    }

    [Fact]
    public void RelationshipModel_DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var relationship = new RelationshipModel();

        // Assert
        relationship.PropertyName.Should().Be(string.Empty);
        relationship.SortKeyPattern.Should().Be(string.Empty);
        relationship.EntityType.Should().BeNull();
        relationship.IsCollection.Should().BeFalse();
        relationship.PropertyType.Should().Be(string.Empty);
        relationship.IsWildcardPattern.Should().BeFalse();
        relationship.HasSpecificEntityType.Should().BeFalse();
    }

    [Fact]
    public void RelationshipModel_WithCompleteConfiguration_ShouldSetAllProperties()
    {
        // Arrange & Act
        var relationship = new RelationshipModel
        {
            PropertyName = "AuditEntries",
            SortKeyPattern = "audit#*",
            EntityType = "AuditEntry",
            IsCollection = true,
            PropertyType = "List<AuditEntry>"
        };

        // Assert
        relationship.PropertyName.Should().Be("AuditEntries");
        relationship.SortKeyPattern.Should().Be("audit#*");
        relationship.EntityType.Should().Be("AuditEntry");
        relationship.IsCollection.Should().BeTrue();
        relationship.PropertyType.Should().Be("List<AuditEntry>");
        relationship.IsWildcardPattern.Should().BeTrue();
        relationship.HasSpecificEntityType.Should().BeTrue();
    }

    [Theory]
    [InlineData("audit#*", true)]
    [InlineData("*", true)]
    [InlineData("prefix#*#suffix", true)]
    [InlineData("audit", false)]
    [InlineData("audit#123", false)]
    [InlineData("", false)]
    public void IsWildcardPattern_WithVariousPatterns_ShouldDetectCorrectly(string pattern, bool expectedIsWildcard)
    {
        // Arrange
        var relationship = new RelationshipModel
        {
            SortKeyPattern = pattern
        };

        // Act
        var isWildcard = relationship.IsWildcardPattern;

        // Assert
        isWildcard.Should().Be(expectedIsWildcard);
    }

    [Theory]
    [InlineData("AuditEntry", true)]
    [InlineData("MyNamespace.AuditEntry", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    public void HasSpecificEntityType_WithVariousEntityTypes_ShouldDetectCorrectly(string? entityType, bool expectedHasSpecificType)
    {
        // Arrange
        var relationship = new RelationshipModel
        {
            EntityType = entityType
        };

        // Act
        var hasSpecificType = relationship.HasSpecificEntityType;

        // Assert
        hasSpecificType.Should().Be(expectedHasSpecificType);
    }

    [Fact]
    public void RelationshipModel_CollectionRelationship_ShouldBeConfiguredCorrectly()
    {
        // Arrange & Act
        var relationship = new RelationshipModel
        {
            PropertyName = "RelatedItems",
            SortKeyPattern = "item#*",
            EntityType = "RelatedItem",
            IsCollection = true,
            PropertyType = "List<RelatedItem>"
        };

        // Assert
        relationship.IsCollection.Should().BeTrue();
        relationship.IsWildcardPattern.Should().BeTrue();
        relationship.HasSpecificEntityType.Should().BeTrue();
        relationship.PropertyType.Should().Contain("List<");
    }

    [Fact]
    public void RelationshipModel_SingleRelationship_ShouldBeConfiguredCorrectly()
    {
        // Arrange & Act
        var relationship = new RelationshipModel
        {
            PropertyName = "Summary",
            SortKeyPattern = "summary",
            EntityType = "SummaryItem",
            IsCollection = false,
            PropertyType = "SummaryItem"
        };

        // Assert
        relationship.IsCollection.Should().BeFalse();
        relationship.IsWildcardPattern.Should().BeFalse();
        relationship.HasSpecificEntityType.Should().BeTrue();
        relationship.PropertyType.Should().NotContain("List<");
    }
}