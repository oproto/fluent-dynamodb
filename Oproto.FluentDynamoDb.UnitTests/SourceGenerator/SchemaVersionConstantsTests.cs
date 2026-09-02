using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Unit tests for <see cref="SchemaVersionConstants"/>.
/// Validates: Requirements 2.3, 7.4
/// </summary>
public class SchemaVersionConstantsTests
{
    [Fact]
    public void Current_Equals_1_0()
    {
        SchemaVersionConstants.Current.Major.Should().Be(1);
        SchemaVersionConstants.Current.Minor.Should().Be(0);
    }

    [Fact]
    public void MinimumSupported_Equals_1_0()
    {
        SchemaVersionConstants.MinimumSupported.Major.Should().Be(1);
        SchemaVersionConstants.MinimumSupported.Minor.Should().Be(0);
    }

    [Fact]
    public void Default_Equals_1_0()
    {
        SchemaVersionConstants.Default.Major.Should().Be(1);
        SchemaVersionConstants.Default.Minor.Should().Be(0);
    }

    [Fact]
    public void MinimumSupported_IsLessThanOrEqualTo_Current()
    {
        (SchemaVersionConstants.MinimumSupported <= SchemaVersionConstants.Current).Should().BeTrue();
    }

    [Fact]
    public void MigrationGuideUrl_IsNonEmpty_ValidUrl()
    {
        SchemaVersionConstants.MigrationGuideUrl.Should().NotBeNullOrEmpty();
        Uri.IsWellFormedUriString(SchemaVersionConstants.MigrationGuideUrl, UriKind.Absolute).Should().BeTrue(
            $"MigrationGuideUrl '{SchemaVersionConstants.MigrationGuideUrl}' should be a valid absolute URL");
    }

    [Fact]
    public void UpgradeGuideUrl_IsNonEmpty_ValidUrl()
    {
        SchemaVersionConstants.UpgradeGuideUrl.Should().NotBeNullOrEmpty();
        Uri.IsWellFormedUriString(SchemaVersionConstants.UpgradeGuideUrl, UriKind.Absolute).Should().BeTrue(
            $"UpgradeGuideUrl '{SchemaVersionConstants.UpgradeGuideUrl}' should be a valid absolute URL");
    }
}
