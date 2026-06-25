using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.Integration;

/// <summary>
/// Backwards compatibility tests verifying that the new Format-based approach produces
/// byte-for-byte identical output to the old Separator/Prefix/PrefixSeparator approach.
/// This proves that upgrading doesn't change behavior for existing configurations.
/// 
/// **Validates: Requirements 4.4, 5.3**
/// </summary>
public class BackwardsCompatibility_SeparatorConfigTests
{
    #region Separator="#" produces identical values to old string.Join("#", ...)

    [Fact]
    public void Separator_Hash_TwoSources_ProducesIdenticalOutput()
    {
        // Arrange: Old behavior was string.Join("#", ["val1", "val2"]) → "val1#val2"
        var sourceValues = new[] { "val1", "val2" };
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "Prop0", "Prop1" },
            Separator = "#",
            Format = null // Separator-based, no custom format
        };

        // Act: Generate format string via the new approach
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null);
        var newResult = string.Format(generatedFormat, sourceValues.Cast<object>().ToArray());

        // Assert: Must match old string.Join behavior byte-for-byte
        var oldResult = string.Join("#", sourceValues);
        newResult.Should().Be(oldResult);
        newResult.Should().Be("val1#val2");
    }

    [Fact]
    public void Separator_Hash_ThreeSources_ProducesIdenticalOutput()
    {
        // Arrange: Old behavior was string.Join("#", ["a", "b", "c"]) → "a#b#c"
        var sourceValues = new[] { "a", "b", "c" };
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "Prop0", "Prop1", "Prop2" },
            Separator = "#",
            Format = null
        };

        // Act
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null);
        var newResult = string.Format(generatedFormat, sourceValues.Cast<object>().ToArray());

        // Assert
        var oldResult = string.Join("#", sourceValues);
        newResult.Should().Be(oldResult);
        newResult.Should().Be("a#b#c");
    }

    [Fact]
    public void Separator_Hash_SingleSource_ProducesIdenticalOutput()
    {
        // Arrange: Old behavior was string.Join("#", ["onlyValue"]) → "onlyValue"
        var sourceValues = new[] { "onlyValue" };
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "Prop0" },
            Separator = "#",
            Format = null
        };

        // Act
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null);
        var newResult = string.Format(generatedFormat, sourceValues.Cast<object>().ToArray());

        // Assert
        var oldResult = string.Join("#", sourceValues);
        newResult.Should().Be(oldResult);
        newResult.Should().Be("onlyValue");
    }

    #endregion

    #region Separator="_" produces identical values to old string.Join("_", ...)

    [Fact]
    public void Separator_Underscore_TwoSources_ProducesIdenticalOutput()
    {
        // Arrange: Old behavior was string.Join("_", ["abc", "def"]) → "abc_def"
        var sourceValues = new[] { "abc", "def" };
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "Prop0", "Prop1" },
            Separator = "_",
            Format = null
        };

        // Act
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null);
        var newResult = string.Format(generatedFormat, sourceValues.Cast<object>().ToArray());

        // Assert
        var oldResult = string.Join("_", sourceValues);
        newResult.Should().Be(oldResult);
        newResult.Should().Be("abc_def");
    }

    [Fact]
    public void Separator_Underscore_ThreeSources_ProducesIdenticalOutput()
    {
        // Arrange: Old behavior was string.Join("_", ["x", "y", "z"]) → "x_y_z"
        var sourceValues = new[] { "x", "y", "z" };
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "Prop0", "Prop1", "Prop2" },
            Separator = "_",
            Format = null
        };

        // Act
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null);
        var newResult = string.Format(generatedFormat, sourceValues.Cast<object>().ToArray());

        // Assert
        var oldResult = string.Join("_", sourceValues);
        newResult.Should().Be(oldResult);
        newResult.Should().Be("x_y_z");
    }

    #endregion

    #region Separator with key Prefix produces identical values to old Prefix+PrefixSep+string.Join(...)

    [Fact]
    public void Separator_WithPrefix_TwoSources_ProducesIdenticalOutput()
    {
        // Arrange: Old behavior was "ORDER" + "#" + string.Join("#", ["a", "b"]) → "ORDER#a#b"
        var sourceValues = new[] { "a", "b" };
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "Prop0", "Prop1" },
            Separator = "#",
            Format = null
        };
        var keyFormat = new KeyFormatModel
        {
            Prefix = "ORDER",
            Separator = "#"
        };

        // Act
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat);
        var newResult = string.Format(generatedFormat, sourceValues.Cast<object>().ToArray());

        // Assert: Must match old Prefix + PrefixSeparator + string.Join(Separator, values)
        var oldResult = "ORDER" + "#" + string.Join("#", sourceValues);
        newResult.Should().Be(oldResult);
        newResult.Should().Be("ORDER#a#b");
    }

    [Fact]
    public void Separator_WithPrefix_ThreeSources_ProducesIdenticalOutput()
    {
        // Arrange: Old behavior was "CUSTOMER" + "#" + string.Join("#", ["us", "west", "42"]) → "CUSTOMER#us#west#42"
        var sourceValues = new[] { "us", "west", "42" };
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "Prop0", "Prop1", "Prop2" },
            Separator = "#",
            Format = null
        };
        var keyFormat = new KeyFormatModel
        {
            Prefix = "CUSTOMER",
            Separator = "#"
        };

        // Act
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat);
        var newResult = string.Format(generatedFormat, sourceValues.Cast<object>().ToArray());

        // Assert
        var oldResult = "CUSTOMER" + "#" + string.Join("#", sourceValues);
        newResult.Should().Be(oldResult);
        newResult.Should().Be("CUSTOMER#us#west#42");
    }

    [Fact]
    public void Separator_WithPrefix_DifferentKeySeparator_ProducesIdenticalOutput()
    {
        // Arrange: Old behavior was "USER" + "_" + string.Join("#", ["region", "id"]) → "USER_region#id"
        var sourceValues = new[] { "region", "id" };
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "Prop0", "Prop1" },
            Separator = "#",
            Format = null
        };
        var keyFormat = new KeyFormatModel
        {
            Prefix = "USER",
            Separator = "_" // Key separator differs from computed separator
        };

        // Act
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat);
        var newResult = string.Format(generatedFormat, sourceValues.Cast<object>().ToArray());

        // Assert: Old behavior was Prefix + KeySeparator + string.Join(ComputedSeparator, values)
        var oldResult = "USER" + "_" + string.Join("#", sourceValues);
        newResult.Should().Be(oldResult);
        newResult.Should().Be("USER_region#id");
    }

    #endregion

    #region Explicit Format produces expected concrete values

    [Fact]
    public void ExplicitFormat_TenantUser_ProducesExpectedValue()
    {
        // Arrange: Explicit Format="TENANT#{0}#USER#{1}#" with concrete values
        var sourceValues = new object[] { "tenantValue", "userValue" };
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "TenantId", "UserId" },
            Separator = "#", // Should be ignored when Format is set
            Format = "TENANT#{0}#USER#{1}#"
        };

        // Act: ComputeFormatString should pass through the explicit format unchanged
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null);
        var result = string.Format(generatedFormat, sourceValues);

        // Assert: Requirement 5.3 - exact expected output
        result.Should().Be("TENANT#tenantValue#USER#userValue#");
    }

    [Fact]
    public void ExplicitFormat_TenantUser_WithKeyFormat_IgnoresKeyFormat()
    {
        // Arrange: Explicit Format takes priority over any key prefix configuration
        var sourceValues = new object[] { "tenantValue", "userValue" };
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "TenantId", "UserId" },
            Separator = "#",
            Format = "TENANT#{0}#USER#{1}#"
        };
        var keyFormat = new KeyFormatModel
        {
            Prefix = "SHOULD_BE_IGNORED",
            Separator = "_"
        };

        // Act: Even with a keyFormat, the explicit Format takes priority
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat);
        var result = string.Format(generatedFormat, sourceValues);

        // Assert: The key format is ignored; explicit Format is used as-is
        result.Should().Be("TENANT#tenantValue#USER#userValue#");
    }

    [Fact]
    public void ExplicitFormat_CustomPattern_ProducesExpectedValue()
    {
        // Arrange: A custom format pattern with different structure
        var sourceValues = new object[] { "2024", "12", "25" };
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "Year", "Month", "Day" },
            Separator = "#",
            Format = "{0}-{1}-{2}"
        };

        // Act
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null);
        var result = string.Format(generatedFormat, sourceValues);

        // Assert
        result.Should().Be("2024-12-25");
    }

    #endregion

    #region End-to-end: ComputeFormatString → string.Format matches old behavior for all paths

    [Theory]
    [InlineData("#", new[] { "val1", "val2" }, null, null, "val1#val2")]
    [InlineData("_", new[] { "abc", "def" }, null, null, "abc_def")]
    [InlineData("#", new[] { "a", "b" }, "ORDER", "#", "ORDER#a#b")]
    [InlineData("#", new[] { "x", "y", "z" }, "TENANT", "#", "TENANT#x#y#z")]
    [InlineData("_", new[] { "foo", "bar" }, "NS", "_", "NS_foo_bar")]
    public void ComputeFormatString_ProducesExpectedResult_Theory(
        string separator,
        string[] sourceValues,
        string? prefix,
        string? keySeparator,
        string expectedResult)
    {
        // Arrange
        var sourceProperties = Enumerable.Range(0, sourceValues.Length)
            .Select(i => $"Prop{i}")
            .ToArray();

        var computedKey = new ComputedKeyModel
        {
            SourceProperties = sourceProperties,
            Separator = separator,
            Format = null
        };

        KeyFormatModel? keyFormat = prefix != null
            ? new KeyFormatModel { Prefix = prefix, Separator = keySeparator! }
            : null;

        // Act
        var generatedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat);
        var result = string.Format(generatedFormat, sourceValues.Cast<object>().ToArray());

        // Assert
        result.Should().Be(expectedResult);
    }

    #endregion
}
