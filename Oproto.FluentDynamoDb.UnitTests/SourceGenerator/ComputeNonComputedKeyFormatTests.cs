using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Unit tests for EntityAnalyzer.ComputeNonComputedKeyFormat.
/// Validates Requirements 1.1, 1.2, 1.3, 1.5.
/// </summary>
public class ComputeNonComputedKeyFormatTests
{
    [Fact]
    public void PrefixOrder_SeparatorHash_ProducesOrderHashPlaceholder()
    {
        // Arrange
        var keyFormat = new KeyFormatModel { Prefix = "ORDER", Separator = "#" };

        // Act
        var result = EntityAnalyzer.ComputeNonComputedKeyFormat(keyFormat);

        // Assert
        result.Should().Be("ORDER#{0}");
    }

    [Fact]
    public void PrefixUser_SeparatorUnderscore_ProducesUserUnderscorePlaceholder()
    {
        // Arrange
        var keyFormat = new KeyFormatModel { Prefix = "USER", Separator = "_" };

        // Act
        var result = EntityAnalyzer.ComputeNonComputedKeyFormat(keyFormat);

        // Assert
        result.Should().Be("USER_{0}");
    }

    [Fact]
    public void PrefixA_SeparatorEmpty_ProducesAPlaceholder()
    {
        // Arrange
        var keyFormat = new KeyFormatModel { Prefix = "A", Separator = "" };

        // Act
        var result = EntityAnalyzer.ComputeNonComputedKeyFormat(keyFormat);

        // Assert
        result.Should().Be("A{0}");
    }

    [Fact]
    public void PrefixNull_ProducesBarePlaceholder()
    {
        // Arrange
        var keyFormat = new KeyFormatModel { Prefix = null, Separator = "#" };

        // Act
        var result = EntityAnalyzer.ComputeNonComputedKeyFormat(keyFormat);

        // Assert
        result.Should().Be("{0}");
    }

    [Fact]
    public void PrefixEmpty_ProducesBarePlaceholder()
    {
        // Arrange
        var keyFormat = new KeyFormatModel { Prefix = "", Separator = "#" };

        // Act
        var result = EntityAnalyzer.ComputeNonComputedKeyFormat(keyFormat);

        // Assert
        result.Should().Be("{0}");
    }

    [Fact]
    public void NullKeyFormat_ProducesBarePlaceholder()
    {
        // Act
        var result = EntityAnalyzer.ComputeNonComputedKeyFormat(null);

        // Assert
        result.Should().Be("{0}");
    }

    [Fact]
    public void PrefixWithNullSeparator_DefaultsToHash()
    {
        // Arrange - Separator defaults to "#" in KeyFormatModel, but test null explicitly
        var keyFormat = new KeyFormatModel { Prefix = "ITEM" };
        // KeyFormatModel.Separator defaults to "#", so this verifies the default behavior

        // Act
        var result = EntityAnalyzer.ComputeNonComputedKeyFormat(keyFormat);

        // Assert
        result.Should().Be("ITEM#{0}");
    }
}
