using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Diagnostics;

[Trait("Category", "Unit")]
public class SpatialIndexDiagnosticsTests
{
    [Fact]
    public void S2LevelWithoutS2IndexType_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.S2LevelWithoutS2IndexType;

        // Assert
        descriptor.Id.Should().Be("DYNDB108");
        descriptor.Title.ToString().Should().Be("S2Level specified without S2 index type");
        descriptor.MessageFormat.ToString().Should().Contain("S2Level");
        descriptor.MessageFormat.ToString().Should().Contain("SpatialIndexType");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void H3ResolutionWithoutH3IndexType_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.H3ResolutionWithoutH3IndexType;

        // Assert
        descriptor.Id.Should().Be("DYNDB109");
        descriptor.Title.ToString().Should().Be("H3Resolution specified without H3 index type");
        descriptor.MessageFormat.ToString().Should().Contain("H3Resolution");
        descriptor.MessageFormat.ToString().Should().Contain("SpatialIndexType");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void GeoHashPrecisionWithoutGeoHashIndexType_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.GeoHashPrecisionWithoutGeoHashIndexType;

        // Assert
        descriptor.Id.Should().Be("DYNDB110");
        descriptor.Title.ToString().Should().Be("GeoHashPrecision specified without GeoHash index type");
        descriptor.MessageFormat.ToString().Should().Contain("GeoHashPrecision");
        descriptor.MessageFormat.ToString().Should().Contain("SpatialIndexType");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void SpatialIndexOnNonGeoLocation_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.SpatialIndexOnNonGeoLocation;

        // Assert
        descriptor.Id.Should().Be("DYNDB111");
        descriptor.Title.ToString().Should().Be("Spatial index configuration on non-GeoLocation property");
        descriptor.MessageFormat.ToString().Should().Contain("GeoLocation");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void MissingGeospatialPackage_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.MissingGeospatialPackage;

        // Assert
        descriptor.Id.Should().Be("DYNDB112");
        descriptor.Title.ToString().Should().Be("Missing Geospatial package");
        descriptor.MessageFormat.ToString().Should().Contain("Oproto.FluentDynamoDb.Geospatial");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }
}
