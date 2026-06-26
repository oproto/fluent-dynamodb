using Oproto.FluentDynamoDb.SourceGenerator.Analysis;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Unit tests for EntityAnalyzer.DeriveDiscriminatorPattern.
/// Validates Requirements 2.1, 2.2, 2.3, 2.4, 2.5.
/// </summary>
public class DeriveDiscriminatorPatternTests
{
    [Fact]
    public void OrderHashPlaceholder_ProducesOrderHashWildcard()
    {
        // Arrange
        var format = "ORDER#{0}";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert
        result.Should().Be("ORDER#*");
    }

    [Fact]
    public void TenantUserMultiplePlaceholders_ProducesMultipleWildcards()
    {
        // Arrange
        var format = "TENANT#{0}#USER#{1}";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert
        result.Should().Be("TENANT#*#USER#*");
    }

    [Fact]
    public void TenantUserTrailingHash_PreservesTrailingHash()
    {
        // Arrange
        var format = "TENANT#{0}#USER#{1}#";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert
        result.Should().Be("TENANT#*#USER#*#");
    }

    [Fact]
    public void SinglePlaceholderNoPrefix_ReturnsNull()
    {
        // Arrange - "{0}" is trivial, no discrimination capability
        var format = "{0}";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void MultiplePlaceholdersNoPrefix_ReturnsNull()
    {
        // Arrange - "{0}#{1}" starts with wildcard, no useful discrimination
        var format = "{0}#{1}";

        // Act
        var result = EntityAnalyzer.DeriveDiscriminatorPattern(format);

        // Assert
        result.Should().BeNull();
    }
}
