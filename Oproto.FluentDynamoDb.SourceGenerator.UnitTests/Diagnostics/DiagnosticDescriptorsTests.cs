using FluentAssertions;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Diagnostics;

public class DiagnosticDescriptorsTests
{
    [Fact]
    public void MissingPartitionKey_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.MissingPartitionKey;

        // Assert
        descriptor.Id.Should().Be("DYNDB001");
        descriptor.Title.ToString().Should().Be("Missing partition key");
        descriptor.MessageFormat.ToString().Should().Be("Entity '{0}' must have exactly one property marked with [PartitionKey]");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void MultiplePartitionKeys_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.MultiplePartitionKeys;

        // Assert
        descriptor.Id.Should().Be("DYNDB002");
        descriptor.Title.ToString().Should().Be("Multiple partition keys");
        descriptor.MessageFormat.ToString().Should().Be("Entity '{0}' has multiple properties marked with [PartitionKey]. Only one is allowed.");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void MultipleSortKeys_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.MultipleSortKeys;

        // Assert
        descriptor.Id.Should().Be("DYNDB003");
        descriptor.Title.ToString().Should().Be("Multiple sort keys");
        descriptor.MessageFormat.ToString().Should().Be("Entity '{0}' has multiple properties marked with [SortKey]. Only one is allowed.");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void InvalidKeyFormat_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.InvalidKeyFormat;

        // Assert
        descriptor.Id.Should().Be("DYNDB004");
        descriptor.Title.ToString().Should().Be("Invalid key format");
        descriptor.MessageFormat.ToString().Should().Be("Property '{0}' has invalid key format: {1}");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void ConflictingEntityTypes_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.ConflictingEntityTypes;

        // Assert
        descriptor.Id.Should().Be("DYNDB005");
        descriptor.Title.ToString().Should().Be("Conflicting entity types");
        descriptor.MessageFormat.ToString().Should().Be("Multiple entities in table '{0}' have conflicting sort key patterns");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void EntityMustBePartial_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.EntityMustBePartial;

        // Assert
        descriptor.Id.Should().Be("DYNDB010");
        descriptor.Title.ToString().Should().Be("Entity must be partial");
        descriptor.MessageFormat.ToString().Should().Be("Entity class '{0}' must be declared as 'partial' to support source generation");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void AmbiguousRelatedEntityPattern_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.AmbiguousRelatedEntityPattern;

        // Assert
        descriptor.Id.Should().Be("DYNDB008");
        descriptor.Title.ToString().Should().Be("Ambiguous related entity pattern");
        descriptor.MessageFormat.ToString().Should().Be("Related entity pattern '{0}' on property '{1}' might match multiple entity types");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void RelatedEntitiesRequireSortKey_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.RelatedEntitiesRequireSortKey;

        // Assert
        descriptor.Id.Should().Be("DYNDB016");
        descriptor.Title.ToString().Should().Be("Related entities require sort key");
        descriptor.MessageFormat.ToString().Should().Be("Entity '{0}' has related entity properties but no sort key for pattern matching");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void ConflictingRelatedEntityPatterns_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.ConflictingRelatedEntityPatterns;

        // Assert
        descriptor.Id.Should().Be("DYNDB017");
        descriptor.Title.ToString().Should().Be("Conflicting related entity patterns");
        descriptor.MessageFormat.ToString().Should().Be("Related entity patterns '{0}' and '{1}' in entity '{2}' may conflict");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void UnsupportedPropertyType_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.UnsupportedPropertyType;

        // Assert
        descriptor.Id.Should().Be("DYNDB009");
        descriptor.Title.ToString().Should().Be("Unsupported property type");
        descriptor.MessageFormat.ToString().Should().Be("Property '{0}' has type '{1}' which is not supported for DynamoDB mapping");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void ReservedWordUsage_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.ReservedWordUsage;

        // Assert
        descriptor.Id.Should().Be("DYNDB021");
        descriptor.Title.ToString().Should().Be("Reserved word usage");
        descriptor.MessageFormat.ToString().Should().Be("Property '{0}' uses DynamoDB reserved word '{1}' as attribute name");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void AllDiagnosticDescriptors_ShouldHaveUniqueIds()
    {
        // Arrange
        var descriptorFields = typeof(DiagnosticDescriptors)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .ToArray();

        // Act
        var descriptors = descriptorFields
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!)
            .ToArray();

        var ids = descriptors.Select(d => d.Id).ToArray();

        // Assert
        ids.Should().OnlyHaveUniqueItems("All diagnostic descriptors should have unique IDs");
        ids.Should().AllSatisfy(id => id.Should().StartWith("DYNDB"), "All IDs should start with DYNDB prefix");
    }

    [Fact]
    public void AllDiagnosticDescriptors_ShouldHaveDynamoDbCategory()
    {
        // Arrange
        var descriptorFields = typeof(DiagnosticDescriptors)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .ToArray();

        // Act
        var descriptors = descriptorFields
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!)
            .ToArray();

        // Assert
        descriptors.Should().AllSatisfy(d => 
            d.Category.Should().Be("DynamoDb", "All descriptors should use DynamoDb category"));
    }

    [Fact]
    public void AllDiagnosticDescriptors_ShouldBeEnabledByDefault()
    {
        // Arrange
        var descriptorFields = typeof(DiagnosticDescriptors)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .ToArray();

        // Act
        var descriptors = descriptorFields
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!)
            .ToArray();

        // Assert
        descriptors.Should().AllSatisfy(d => 
            d.IsEnabledByDefault.Should().BeTrue("All descriptors should be enabled by default"));
    }

    [Fact]
    public void AllDiagnosticDescriptors_ShouldHaveNonEmptyTitleAndMessage()
    {
        // Arrange
        var descriptorFields = typeof(DiagnosticDescriptors)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor))
            .ToArray();

        // Act
        var descriptors = descriptorFields
            .Select(f => (DiagnosticDescriptor)f.GetValue(null)!)
            .ToArray();

        // Assert
        descriptors.Should().AllSatisfy(d =>
        {
            d.Title.ToString().Should().NotBeNullOrEmpty("All descriptors should have a title");
            d.MessageFormat.ToString().Should().NotBeNullOrEmpty("All descriptors should have a message format");
        });
    }
}