using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.UnitTests.MetadataTests;

/// <summary>
/// Integration tests verifying that the source generator populates PropertyMetadata.KeyFormat
/// correctly for partition key and sort key properties.
/// Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8
/// </summary>
public class KeyFormatMetadataTests
{
    [Fact]
    public void PartitionKey_KeyFormat_IsNotNull()
    {
        // Act
        var metadata = KeyFormatTestEntity.GetEntityMetadata();
        var pkProperty = metadata.Properties.First(p => p.PropertyName == "Pk");

        // Assert
        pkProperty.KeyFormat.Should().NotBeNull();
    }

    [Fact]
    public void SortKey_KeyFormat_IsNotNull()
    {
        // Act
        var metadata = KeyFormatTestEntity.GetEntityMetadata();
        var skProperty = metadata.Properties.First(p => p.PropertyName == "Sk");

        // Assert
        skProperty.KeyFormat.Should().NotBeNull();
    }

    [Fact]
    public void PartitionKey_KeyFormat_Prefix_MatchesAttributeConfiguration()
    {
        // Act
        var metadata = KeyFormatTestEntity.GetEntityMetadata();
        var pkProperty = metadata.Properties.First(p => p.PropertyName == "Pk");

        // Assert
        pkProperty.KeyFormat!.Prefix.Should().Be("TEST");
    }

    [Fact]
    public void SortKey_KeyFormat_Prefix_MatchesAttributeConfiguration()
    {
        // Act
        var metadata = KeyFormatTestEntity.GetEntityMetadata();
        var skProperty = metadata.Properties.First(p => p.PropertyName == "Sk");

        // Assert
        skProperty.KeyFormat!.Prefix.Should().Be("SK");
    }

    [Fact]
    public void NonKeyProperty_KeyFormat_IsNull()
    {
        // Act
        var metadata = KeyFormatTestEntity.GetEntityMetadata();
        var nameProperty = metadata.Properties.First(p => p.PropertyName == "Name");

        // Assert
        nameProperty.KeyFormat.Should().BeNull();
    }

    [Fact]
    public void PartitionKey_WithCustomSeparator_KeyFormat_SeparatorMatchesConfiguration()
    {
        // Act - Use entity with custom separator to verify non-default separators are populated
        var metadata = KeyFormatCustomSeparatorTestEntity.GetEntityMetadata();
        var pkProperty = metadata.Properties.First(p => p.PropertyName == "Pk");

        // Assert
        pkProperty.KeyFormat!.Separator.Should().Be("_");
    }

    [Fact]
    public void SortKey_WithDefaultSeparator_KeyFormat_SeparatorIsNullOrDefault()
    {
        // The source generator only emits Separator when it differs from the default "#".
        // When separator is "#", the generated code does not set it, leaving it null.
        var metadata = KeyFormatTestEntity.GetEntityMetadata();
        var skProperty = metadata.Properties.First(p => p.PropertyName == "Sk");

        // The separator is null when the default "#" is used (source generator optimization)
        skProperty.KeyFormat!.Separator.Should().BeNull();
    }

    [Fact]
    public void PartitionKey_WithExplicitDefaultSeparator_KeyFormat_SeparatorIsNullOrDefault()
    {
        // Even when explicitly specifying "#" as separator, the source generator treats it as default
        var metadata = KeyFormatTestEntity.GetEntityMetadata();
        var pkProperty = metadata.Properties.First(p => p.PropertyName == "Pk");

        // The separator is null when the default "#" is used (source generator optimization)
        pkProperty.KeyFormat!.Separator.Should().BeNull();
    }

    [Fact]
    public void KeyFormat_IsPopulated_OnlyForKeyProperties()
    {
        // Act
        var metadata = KeyFormatTestEntity.GetEntityMetadata();

        // Assert - verify all properties have correct KeyFormat presence
        var pkProperty = metadata.Properties.First(p => p.PropertyName == "Pk");
        var skProperty = metadata.Properties.First(p => p.PropertyName == "Sk");
        var nameProperty = metadata.Properties.First(p => p.PropertyName == "Name");

        pkProperty.KeyFormat.Should().NotBeNull();
        skProperty.KeyFormat.Should().NotBeNull();
        nameProperty.KeyFormat.Should().BeNull();
    }
}

/// <summary>
/// Test entity with partition key and sort key using default separator.
/// The source generator processes this entity and populates KeyFormat metadata.
/// </summary>
[DynamoDbTable("TestKeyFormatTable")]
public partial class KeyFormatTestEntity
{
    [PartitionKey(Prefix = "TEST", Separator = "#")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "SK")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Test entity with a custom (non-default) separator to verify separator metadata is populated.
/// </summary>
[DynamoDbTable("TestKeyFormatCustomSepTable")]
public partial class KeyFormatCustomSeparatorTestEntity
{
    [PartitionKey(Prefix = "CUSTOM", Separator = "_")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "DATA", Separator = ":")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("value")]
    public string Value { get; set; } = string.Empty;
}
