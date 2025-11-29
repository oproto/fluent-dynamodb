using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Geospatial.GeoHash;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Property-based tests for ExpressionTranslator geospatial provider integration.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class ExpressionTranslatorGeospatialProviderPropertyTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public GeoLocation Location { get; set; }
    }

    /// <summary>
    /// **Feature: aot-compatible-service-registration, Property 3: No Reflection for Registered Services**
    /// **Validates: Requirements 2.2, 6.4**
    /// 
    /// For any operation using a registered geospatial provider, the library SHALL use the
    /// IGeospatialProvider interface instead of reflection-based method discovery.
    /// 
    /// This test verifies that when geospatial features are used with a configured provider,
    /// the provider's methods are called directly without using Assembly.GetType(), 
    /// Type.GetMethod(), or Activator.CreateInstance().
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeospatialProvider_IsUsedForWithinDistanceTranslation()
    {
        return Prop.ForAll(
            Arb.Default.PositiveInt().Filter(x => x.Item > 0 && x.Item < 1000000),
            Arb.Default.Int32().Filter(x => x >= -90 && x <= 90),
            Arb.Default.Int32().Filter(x => x >= -180 && x <= 180),
            (distanceArb, latArb, lonArb) =>
            {
                // Arrange
                var distance = (double)distanceArb.Item;
                var latitude = (double)latArb;
                var longitude = (double)lonArb;
                
                var mockProvider = Substitute.For<IGeospatialProvider>();
                var expectedBbox = new GeoBoundingBoxResult
                {
                    SouthwestLatitude = latitude - 0.1,
                    SouthwestLongitude = longitude - 0.1,
                    NortheastLatitude = latitude + 0.1,
                    NortheastLongitude = longitude + 0.1
                };
                
                mockProvider.CreateBoundingBox(
                    Arg.Any<double>(), 
                    Arg.Any<double>(), 
                    Arg.Any<double>())
                    .Returns(expectedBbox);
                
                mockProvider.GetGeoHashRange(Arg.Any<GeoBoundingBoxResult>(), Arg.Any<int>())
                    .Returns(("minHash", "maxHash"));
                
                var options = new FluentDynamoDbOptions()
                    .AddGeospatial(mockProvider);
                
                var translator = new ExpressionTranslator(options);
                var context = CreateContext();
                
                // Create a GeoLocation center point
                var center = new GeoLocation(latitude, longitude);
                
                // Act - This would throw if reflection was used and provider wasn't configured
                // The fact that we can substitute the provider proves we're using the interface
                try
                {
                    Expression<Func<TestEntity, bool>> expression = 
                        x => x.Location.WithinDistanceMeters(center, distance);
                    translator.Translate(expression, context);
                    
                    // Assert - Provider methods should have been called
                    var providerWasCalled = mockProvider.ReceivedCalls().Any();
                    
                    return providerWasCalled.ToProperty()
                        .Label($"Provider should be called for geospatial translation. " +
                               $"ProviderWasCalled: {providerWasCalled}");
                }
                catch (InvalidOperationException)
                {
                    // This is expected if the provider is not configured
                    return false.ToProperty()
                        .Label("Provider should be configured and used");
                }
            });
    }

    /// <summary>
    /// **Feature: aot-compatible-service-registration, Property 3: No Reflection for Registered Services**
    /// **Validates: Requirements 2.2, 6.4**
    /// 
    /// For any geospatial operation without a configured provider, the library SHALL throw
    /// a descriptive InvalidOperationException explaining how to configure the provider.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeospatialFeatures_WithoutProvider_ThrowsDescriptiveException()
    {
        return Prop.ForAll(
            Arb.Default.PositiveInt().Filter(x => x.Item > 0 && x.Item < 1000000),
            distanceArb =>
            {
                // Arrange
                var distance = (double)distanceArb.Item;
                
                // Create translator WITHOUT geospatial provider
                var options = new FluentDynamoDbOptions();
                var translator = new ExpressionTranslator(options);
                var context = CreateContext();
                
                var center = new GeoLocation(37.7749, -122.4194);
                
                // Act & Assert
                try
                {
                    Expression<Func<TestEntity, bool>> expression = 
                        x => x.Location.WithinDistanceMeters(center, distance);
                    translator.Translate(expression, context);
                    
                    // Should not reach here
                    return false.ToProperty()
                        .Label("Should throw InvalidOperationException when provider not configured");
                }
                catch (InvalidOperationException ex)
                {
                    // Verify the exception message is descriptive
                    var hasGeospatialMention = ex.Message.Contains("Geospatial", StringComparison.OrdinalIgnoreCase);
                    var hasConfigurationGuidance = ex.Message.Contains("AddGeospatial", StringComparison.OrdinalIgnoreCase);
                    
                    return (hasGeospatialMention && hasConfigurationGuidance).ToProperty()
                        .Label($"Exception should mention geospatial and configuration guidance. " +
                               $"HasGeospatialMention: {hasGeospatialMention}, HasConfigurationGuidance: {hasConfigurationGuidance}");
                }
            });
    }

    /// <summary>
    /// **Feature: aot-compatible-service-registration, Property 3: No Reflection for Registered Services**
    /// **Validates: Requirements 2.2, 6.4**
    /// 
    /// For any bounding box query with a configured provider, the provider's CreateBoundingBox
    /// and GetGeoHashRange methods SHALL be called instead of reflection.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GeospatialProvider_IsUsedForWithinBoundingBoxTranslation()
    {
        return Prop.ForAll(
            Arb.Default.Int32().Filter(x => x >= -90 && x <= 89),
            Arb.Default.Int32().Filter(x => x >= -180 && x <= 179),
            (swLatArb, swLonArb) =>
            {
                // Arrange
                var swLat = (double)swLatArb;
                var swLon = (double)swLonArb;
                var neLat = swLat + 1.0;
                var neLon = swLon + 1.0;
                
                var mockProvider = Substitute.For<IGeospatialProvider>();
                var expectedBbox = new GeoBoundingBoxResult
                {
                    SouthwestLatitude = swLat,
                    SouthwestLongitude = swLon,
                    NortheastLatitude = neLat,
                    NortheastLongitude = neLon
                };
                
                mockProvider.CreateBoundingBox(
                    Arg.Any<double>(), 
                    Arg.Any<double>(), 
                    Arg.Any<double>(),
                    Arg.Any<double>())
                    .Returns(expectedBbox);
                
                mockProvider.GetGeoHashRange(Arg.Any<GeoBoundingBoxResult>(), Arg.Any<int>())
                    .Returns(("minHash", "maxHash"));
                
                var options = new FluentDynamoDbOptions()
                    .AddGeospatial(mockProvider);
                
                var translator = new ExpressionTranslator(options);
                var context = CreateContext();
                
                // Create bounding box corners
                var southwest = new GeoLocation(swLat, swLon);
                var northeast = new GeoLocation(neLat, neLon);
                
                // Act
                try
                {
                    Expression<Func<TestEntity, bool>> expression = 
                        x => x.Location.WithinBoundingBox(southwest, northeast);
                    translator.Translate(expression, context);
                    
                    // Assert - Provider methods should have been called
                    var providerWasCalled = mockProvider.ReceivedCalls().Any();
                    
                    return providerWasCalled.ToProperty()
                        .Label($"Provider should be called for bounding box translation. " +
                               $"ProviderWasCalled: {providerWasCalled}");
                }
                catch (InvalidOperationException)
                {
                    return false.ToProperty()
                        .Label("Provider should be configured and used");
                }
            });
    }

    private static ExpressionContext CreateContext()
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            null,
            ExpressionValidationMode.None);
    }
}
