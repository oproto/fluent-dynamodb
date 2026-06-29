using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Unit tests for KeysGenerator format specifier handling in computed key builders.
/// Verifies that generated code correctly handles format specifiers by:
/// - Passing typed values cast to object for indices with format specifiers
/// - Using existing pre-stringification (GetValueExpression) for indices without specifiers
/// - Including CultureInfo.InvariantCulture when format specifiers are present
/// - Preserving backwards compatibility when no format specifiers are used
///
/// Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 5.4
/// </summary>
[Trait("Category", "Unit")]
public class KeysGenerator_FormatSpecifierTests
{
    /// <summary>
    /// Verifies that for format "{0:yyyy-MM-dd}#{1}", index 0 (DateOnly) is emitted as (object)eventDate
    /// and index 1 (string) uses pre-stringification (just the parameter name since it's string type).
    /// Requirement 3.1, 3.2: Typed value cast to object for indices with format specifiers;
    /// indices without specifiers use existing value expression logic.
    /// </summary>
    [Fact]
    public void GenerateComputedKeyBuilder_WithDateFormatSpecifier_EmitsTypedValueForFormattedIndex()
    {
        // Arrange
        var entity = CreateEntityWithFormatSpecifier(
            format: "{0:yyyy-MM-dd}#{1}",
            sourceProperties: new[]
            {
                ("EventDate", "System.DateOnly"),
                ("Category", "string")
            });

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - index 0 should be cast to object (typed value for format specifier)
        result.Should().Contain("(object)eventDate",
            "index 0 with format specifier should pass typed value cast to object");

        // Assert - index 1 (string without specifier) should just use the parameter name directly
        // String types use the parameterName as-is in GetValueExpression
        result.Should().NotContain("(object)category",
            "index 1 without format specifier should NOT be cast to object");
    }

    /// <summary>
    /// Verifies that for format "{0:D4}#{1}", index 0 (int) is emitted as (object)priority.
    /// Requirement 3.1: Typed value cast to object for format specifier indices.
    /// </summary>
    [Fact]
    public void GenerateComputedKeyBuilder_WithIntegerFormatSpecifier_EmitsTypedValueForFormattedIndex()
    {
        // Arrange
        var entity = CreateEntityWithFormatSpecifier(
            format: "{0:D4}#{1}",
            sourceProperties: new[]
            {
                ("Priority", "int"),
                ("Name", "string")
            });

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - index 0 should be cast to object
        result.Should().Contain("(object)priority",
            "index 0 with D4 format specifier should pass typed value cast to object");

        // Assert - index 1 (string without specifier) should not be cast to object
        result.Should().NotContain("(object)name",
            "index 1 without format specifier should NOT be cast to object");
    }

    /// <summary>
    /// Verifies that for format "{0}#{1}" (no format specifiers), all indices use
    /// GetValueExpression logic (backwards compatibility).
    /// Requirement 3.3: Without format specifiers, existing pre-stringification applies.
    /// </summary>
    [Fact]
    public void GenerateComputedKeyBuilder_WithNoFormatSpecifiers_UsesGetValueExpressionForAll()
    {
        // Arrange
        var entity = CreateEntityWithFormatSpecifier(
            format: "{0}#{1}",
            sourceProperties: new[]
            {
                ("EventDate", "System.DateOnly"),
                ("Category", "string")
            });

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - no (object) casts should be present
        result.Should().NotContain("(object)eventDate",
            "without format specifiers, index 0 should use GetValueExpression, not typed cast");
        result.Should().NotContain("(object)category",
            "without format specifiers, index 1 should use GetValueExpression, not typed cast");
    }

    /// <summary>
    /// Verifies that CultureInfo.InvariantCulture is included in the string.Format call
    /// when the computed format string contains format specifiers.
    /// Requirement 5.4: InvariantCulture must be used with format specifiers.
    /// </summary>
    [Fact]
    public void GenerateComputedKeyBuilder_WithFormatSpecifiers_IncludesInvariantCulture()
    {
        // Arrange
        var entity = CreateEntityWithFormatSpecifier(
            format: "{0:yyyy-MM-dd}#{1}",
            sourceProperties: new[]
            {
                ("EventDate", "System.DateOnly"),
                ("Category", "string")
            });

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - CultureInfo.InvariantCulture should be present
        result.Should().Contain("System.Globalization.CultureInfo.InvariantCulture",
            "format specifiers require InvariantCulture to ensure deterministic output");
    }

    /// <summary>
    /// Verifies that CultureInfo.InvariantCulture is NOT included when no format specifiers
    /// are present (backwards compatibility).
    /// Requirement 3.3: Without format specifiers, behavior is unchanged.
    /// </summary>
    [Fact]
    public void GenerateComputedKeyBuilder_WithNoFormatSpecifiers_DoesNotIncludeInvariantCulture()
    {
        // Arrange
        var entity = CreateEntityWithFormatSpecifier(
            format: "{0}#{1}",
            sourceProperties: new[]
            {
                ("EventDate", "System.DateOnly"),
                ("Category", "string")
            });

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - CultureInfo.InvariantCulture should NOT be present
        result.Should().NotContain("System.Globalization.CultureInfo.InvariantCulture",
            "without format specifiers, InvariantCulture should not be emitted for backwards compatibility");
    }

    /// <summary>
    /// Verifies that the generated code compiles successfully when format specifiers are used.
    /// </summary>
    [Fact]
    public void GenerateComputedKeyBuilder_WithFormatSpecifiers_GeneratesCompilableCode()
    {
        // Arrange
        var entity = CreateEntityWithFormatSpecifier(
            format: "{0:yyyy-MM-dd}#{1}",
            sourceProperties: new[]
            {
                ("EventDate", "System.DateOnly"),
                ("Category", "string")
            });

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - generated code compiles
        CompilationVerifier.AssertGeneratedCodeCompiles(result);
    }

    /// <summary>
    /// Verifies that the generated code uses string.Format with the format string
    /// when format specifiers are present.
    /// </summary>
    [Fact]
    public void GenerateComputedKeyBuilder_WithFormatSpecifiers_UsesStringFormatWithFormatString()
    {
        // Arrange
        var entity = CreateEntityWithFormatSpecifier(
            format: "{0:D4}#{1}",
            sourceProperties: new[]
            {
                ("Priority", "int"),
                ("Name", "string")
            });

        // Act
        var result = KeysGenerator.GenerateKeysClass(entity);

        // Assert - string.Format is used with the correct format string
        result.Should().Contain("string.Format(System.Globalization.CultureInfo.InvariantCulture, \"{0:D4}#{1}\"",
            "should use string.Format with InvariantCulture and the format string");
    }

    #region Helper Methods

    /// <summary>
    /// Creates an EntityModel with a computed partition key using the specified format and source properties.
    /// </summary>
    private static EntityModel CreateEntityWithFormatSpecifier(
        string format,
        (string Name, string Type)[] sourceProperties)
    {
        var properties = new List<PropertyModel>
        {
            new PropertyModel
            {
                PropertyName = "Pk",
                PropertyType = "string",
                AttributeName = "pk",
                IsPartitionKey = true,
                IsNullable = false,
                KeyFormat = null,
                ComputedKey = new ComputedKeyModel
                {
                    SourceProperties = sourceProperties.Select(sp => sp.Name).ToArray(),
                    Format = format,
                    Separator = "#"
                }
            }
        };

        // Add source properties
        foreach (var (name, type) in sourceProperties)
        {
            properties.Add(new PropertyModel
            {
                PropertyName = name,
                PropertyType = type,
                AttributeName = name.ToLowerInvariant(),
                IsNullable = false
            });
        }

        return new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = properties.ToArray(),
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            IsDefault = true
        };
    }

    #endregion
}
