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
        ids.Should().AllSatisfy(id => 
        {
            var validPrefixes = new[] { "DYNDB", "PROJ", "DISC", "SEC", "FDDB" };
            validPrefixes.Should().Contain(prefix => id.StartsWith(prefix), 
                $"ID '{id}' should start with one of the valid prefixes: {string.Join(", ", validPrefixes)}");
        }, "All IDs should start with a valid category prefix");
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

    // Advanced Type System Diagnostics Tests

    [Fact]
    public void InvalidTtlType_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.InvalidTtlType;

        // Assert
        descriptor.Id.Should().Be("DYNDB101");
        descriptor.Title.ToString().Should().Be("Invalid TTL property type");
        descriptor.MessageFormat.ToString().Should().Be("[TimeToLive] can only be used on DateTime or DateTimeOffset properties. Property '{0}' is type '{1}'");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void MissingJsonSerializer_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.MissingJsonSerializer;

        // Assert
        descriptor.Id.Should().Be("DYNDB102");
        descriptor.Title.ToString().Should().Be("Missing JSON serializer package");
        descriptor.MessageFormat.ToString().Should().Be("[JsonBlob] on property '{0}' requires referencing a JSON serializer package (SystemTextJson or NewtonsoftJson)");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void MissingBlobProvider_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.MissingBlobProvider;

        // Assert
        descriptor.Id.Should().Be("DYNDB103");
        descriptor.Title.ToString().Should().Be("Missing blob provider package");
        descriptor.MessageFormat.ToString().Should().Be("[BlobReference] on property '{0}' requires referencing a blob provider package like Oproto.FluentDynamoDb.BlobStorage.S3");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void IncompatibleAttributes_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.IncompatibleAttributes;

        // Assert
        descriptor.Id.Should().Be("DYNDB104");
        descriptor.Title.ToString().Should().Be("Incompatible attribute combination");
        descriptor.MessageFormat.ToString().Should().Be("Property '{0}' has incompatible attribute combination: {1}");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void MultipleTtlFields_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.MultipleTtlFields;

        // Assert
        descriptor.Id.Should().Be("DYNDB105");
        descriptor.Title.ToString().Should().Be("Multiple TTL fields");
        descriptor.MessageFormat.ToString().Should().Be("Entity '{0}' has multiple [TimeToLive] properties. Only one TTL field is allowed per entity");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void UnsupportedCollectionType_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.UnsupportedCollectionType;

        // Assert
        descriptor.Id.Should().Be("DYNDB106");
        descriptor.Title.ToString().Should().Be("Unsupported collection type");
        descriptor.MessageFormat.ToString().Should().Be("Property '{0}' has unsupported collection type '{1}'. Use Dictionary<string, T>, HashSet<T>, or List<T>");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void NestedMapTypeMissingEntity_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.NestedMapTypeMissingEntity;

        // Assert
        descriptor.Id.Should().Be("DYNDB107");
        descriptor.Title.ToString().Should().Be("Nested map type missing [DynamoDbEntity]");
        descriptor.MessageFormat.ToString().Should().Contain("Property '{0}' with [DynamoDbMap] has type '{1}' which must be marked with [DynamoDbEntity]");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }
}