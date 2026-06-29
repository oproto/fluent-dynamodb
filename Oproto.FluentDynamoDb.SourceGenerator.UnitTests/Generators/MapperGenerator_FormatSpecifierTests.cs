using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for MapperGenerator format specifier handling.
/// Tests ComputeFormatString source property Format injection and
/// GenerateComputedKeyLogic InvariantCulture emission.
///
/// Requirements: 5.1, 5.4, 6.1, 6.2, 6.3, 6.5, 6.6
/// </summary>
[Trait("Category", "Unit")]
public class MapperGenerator_FormatSpecifierTests
{
    #region ComputeFormatString - Source Property Format Injection

    [Fact]
    public void ComputeFormatString_InjectsSourcePropertyFormat_WhenNoExplicitSpecifier()
    {
        // Arrange: computed key with no explicit Format, source property has Format="yyyy-MM-dd"
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "EventDate", "Category" },
            Format = null,
            Separator = "#"
        };

        var sourceProperties = new[]
        {
            new PropertyModel { PropertyName = "EventDate", PropertyType = "DateOnly", Format = "yyyy-MM-dd" },
            new PropertyModel { PropertyName = "Category", PropertyType = "string", Format = null }
        };

        // Act
        var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null, sourceProperties);

        // Assert: should inject Format into placeholder 0, leave placeholder 1 unchanged
        result.Should().Be("{0:yyyy-MM-dd}#{1}",
            "source property Format should be injected into placeholder when no explicit specifier exists");
    }

    [Fact]
    public void ComputeFormatString_DoesNotOverrideExplicitSpecifier_WhenFormatStringAlreadyHasSpecifier()
    {
        // Arrange: computed key with explicit Format that already has specifiers
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "EventDate", "Category" },
            Format = "{0:MM/dd/yyyy}#{1}",
            Separator = "#"
        };

        var sourceProperties = new[]
        {
            new PropertyModel { PropertyName = "EventDate", PropertyType = "DateOnly", Format = "yyyy-MM-dd" },
            new PropertyModel { PropertyName = "Category", PropertyType = "string", Format = null }
        };

        // Act
        var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null, sourceProperties);

        // Assert: explicit format takes highest priority, source property Format is ignored
        result.Should().Be("{0:MM/dd/yyyy}#{1}",
            "explicit format specifier in computed format string should NOT be overridden by source property Format");
    }

    [Fact]
    public void ComputeFormatString_TreatsEmptyStringFormat_SameAsNull()
    {
        // Arrange: source property has empty string Format (should be treated as null)
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "EventDate", "Category" },
            Format = null,
            Separator = "#"
        };

        var sourceProperties = new[]
        {
            new PropertyModel { PropertyName = "EventDate", PropertyType = "DateOnly", Format = "" },
            new PropertyModel { PropertyName = "Category", PropertyType = "string", Format = null }
        };

        // Act
        var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null, sourceProperties);

        // Assert: empty string Format should not inject, placeholders remain simple
        result.Should().Be("{0}#{1}",
            "empty string Format on source property should be treated as null (no injection)");
    }

    [Fact]
    public void ComputeFormatString_InjectsMultipleSourceFormats_WhenMultiplePropertiesHaveFormat()
    {
        // Arrange: both source properties have Format values
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "EventDate", "Priority" },
            Format = null,
            Separator = "#"
        };

        var sourceProperties = new[]
        {
            new PropertyModel { PropertyName = "EventDate", PropertyType = "DateOnly", Format = "yyyy-MM-dd" },
            new PropertyModel { PropertyName = "Priority", PropertyType = "int", Format = "D4" }
        };

        // Act
        var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null, sourceProperties);

        // Assert: both formats should be injected
        result.Should().Be("{0:yyyy-MM-dd}#{1:D4}",
            "should inject Format from all source properties that have non-empty Format values");
    }

    [Fact]
    public void ComputeFormatString_WithKeyPrefix_InjectsFormatAndPrependsPrefix()
    {
        // Arrange: computed key with no explicit Format, source has Format, key has prefix
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "EventDate", "Category" },
            Format = null,
            Separator = "#"
        };

        var keyFormat = new KeyFormatModel { Prefix = "EVENT", Separator = "#" };

        var sourceProperties = new[]
        {
            new PropertyModel { PropertyName = "EventDate", PropertyType = "DateOnly", Format = "yyyy-MM-dd" },
            new PropertyModel { PropertyName = "Category", PropertyType = "string", Format = null }
        };

        // Act
        var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat, sourceProperties);

        // Assert: should have prefix prepended and format injected
        result.Should().Be("EVENT#{0:yyyy-MM-dd}#{1}",
            "should inject source Format and prepend key prefix");
    }

    [Fact]
    public void ComputeFormatString_WithNullSourceProperties_ProducesSimplePlaceholders()
    {
        // Arrange: no source properties passed (null array)
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "EventDate", "Category" },
            Format = null,
            Separator = "#"
        };

        // Act
        var result = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null, sourceProperties: null);

        // Assert: without source properties, placeholders remain simple
        result.Should().Be("{0}#{1}",
            "when sourceProperties is null, no format injection should occur");
    }

    #endregion

    #region GenerateComputedKeyLogic - InvariantCulture Emission

    [Fact]
    public void GenerateComputedKeyLogic_EmitsInvariantCulture_WhenFormatSpecifiersPresent()
    {
        // Arrange: entity with a computed property that has format specifiers
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
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
                        SourceProperties = new[] { "EventDate", "Category" },
                        Format = "{0:yyyy-MM-dd}#{1}",
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "EventDate",
                    AttributeName = "eventDate",
                    PropertyType = "DateOnly"
                },
                new PropertyModel
                {
                    PropertyName = "Category",
                    AttributeName = "category",
                    PropertyType = "string"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: CultureInfo.InvariantCulture should be present in the generated code
        result.Should().Contain("System.Globalization.CultureInfo.InvariantCulture",
            "when format specifiers are present, string.Format should use CultureInfo.InvariantCulture");
        result.Should().Contain("string.Format(System.Globalization.CultureInfo.InvariantCulture",
            "should use the string.Format overload that accepts IFormatProvider");
    }

    [Fact]
    public void GenerateComputedKeyLogic_DoesNotEmitInvariantCulture_WhenNoFormatSpecifiers()
    {
        // Arrange: entity with a computed property that has NO format specifiers (uses separator-based)
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
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
                        SourceProperties = new[] { "Region", "Department" },
                        Format = null,
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Region",
                    AttributeName = "region",
                    PropertyType = "string"
                },
                new PropertyModel
                {
                    PropertyName = "Department",
                    AttributeName = "department",
                    PropertyType = "string"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: InvariantCulture should NOT appear in the computed key logic
        // Note: It might appear elsewhere for DateOnly/TimeOnly serialization,
        // so we check that string.Format specifically does NOT use InvariantCulture
        result.Should().NotContain("string.Format(System.Globalization.CultureInfo.InvariantCulture",
            "when no format specifiers are present, string.Format should NOT use CultureInfo.InvariantCulture for backwards compatibility");
    }

    [Fact]
    public void GenerateComputedKeyLogic_EmitsInvariantCulture_WhenExplicitFormatHasSpecifiers()
    {
        // Arrange: entity with explicit Format="{0:D4}#{1}" (custom format with specifiers)
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "Priority", "Name" },
                        Format = "{0:D4}#{1}",
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Priority",
                    AttributeName = "priority",
                    PropertyType = "int"
                },
                new PropertyModel
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = "string"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("string.Format(System.Globalization.CultureInfo.InvariantCulture, \"{0:D4}#{1}\"",
            "should use InvariantCulture with the explicit format string when specifiers are present");
    }

    [Fact]
    public void GenerateComputedKeyLogic_DoesNotEmitInvariantCulture_WhenExplicitFormatHasNoSpecifiers()
    {
        // Arrange: entity with explicit Format="{0}#{1}" (no specifiers)
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    ComputedKey = new ComputedKeyModel
                    {
                        SourceProperties = new[] { "Region", "Category" },
                        Format = "{0}#{1}",
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Region",
                    AttributeName = "region",
                    PropertyType = "string"
                },
                new PropertyModel
                {
                    PropertyName = "Category",
                    AttributeName = "category",
                    PropertyType = "string"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().NotContain("string.Format(System.Globalization.CultureInfo.InvariantCulture",
            "when explicit format has no specifiers, should NOT use InvariantCulture for backwards compatibility");
        result.Should().Contain("string.Format(\"{0}#{1}\"",
            "should use simple string.Format without culture for format strings without specifiers");
    }

    #endregion
}
