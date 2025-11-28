using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for source generator deserialization code that includes spatial index in GeoLocation.
/// Validates Requirements 5.4, 5.5, 6.3, 6.4.
/// </summary>
[Trait("Category", "Unit")]
public class SpatialIndexDeserializationTests
{
    [Fact]
    public void GenerateEntityImplementation_WithGeoHashProperty_IncludesSpatialIndexInDeserialization()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Store",
            Namespace = "TestNamespace",
            TableName = "stores",
            HasGeospatialPackage = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Location",
                    AttributeName = "location",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "GeoHash",
                    GeoHashPrecision = 6
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("GeoHashExtensions.FromGeoHash(spatialIndexString)",
            "should deserialize GeoHash to coordinates");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(decodedLocation.Latitude, decodedLocation.Longitude, spatialIndexString)",
            "should include spatial index (GeoHash) in GeoLocation constructor");
    }

    [Fact]
    public void GenerateEntityImplementation_WithS2Property_IncludesSpatialIndexInDeserialization()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Store",
            Namespace = "TestNamespace",
            TableName = "stores",
            HasGeospatialPackage = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Location",
                    AttributeName = "location",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "S2",
                    S2Level = 16
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("S2Extensions.FromS2Token(spatialIndexString)",
            "should deserialize S2 token to coordinates");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(decodedLocation.Latitude, decodedLocation.Longitude, spatialIndexString)",
            "should include spatial index (S2 token) in GeoLocation constructor");
    }

    [Fact]
    public void GenerateEntityImplementation_WithH3Property_IncludesSpatialIndexInDeserialization()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Store",
            Namespace = "TestNamespace",
            TableName = "stores",
            HasGeospatialPackage = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Location",
                    AttributeName = "location",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "H3",
                    H3Resolution = 9
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("H3Extensions.FromH3Index(spatialIndexString)",
            "should deserialize H3 index to coordinates");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(decodedLocation.Latitude, decodedLocation.Longitude, spatialIndexString)",
            "should include spatial index (H3 index) in GeoLocation constructor");
    }

    [Fact]
    public void GenerateEntityImplementation_WithCoordinateStorage_IncludesSpatialIndexWhenDeserializingFromCoordinates()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Store",
            Namespace = "TestNamespace",
            TableName = "stores",
            HasGeospatialPackage = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Location",
                    AttributeName = "location",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "S2",
                    S2Level = 16,
                    LatitudeAttributeName = "lat",
                    LongitudeAttributeName = "lon"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        // When coordinates are present, should read spatial index and include it
        result.Should().Contain("item.TryGetValue(\"location\", out var locationIndexValue)",
            "should read spatial index value even when coordinates are present");
        result.Should().Contain("spatialIndexValue = locationIndexValue.S;",
            "should extract spatial index string value");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(latitude, longitude, spatialIndexValue)",
            "should include spatial index in GeoLocation constructor when deserializing from coordinates");
    }

    [Fact]
    public void GenerateEntityImplementation_WithCoordinateStorageFallback_IncludesSpatialIndexWhenDeserializingFromSpatialIndex()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Store",
            Namespace = "TestNamespace",
            TableName = "stores",
            HasGeospatialPackage = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Location",
                    AttributeName = "location",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "H3",
                    H3Resolution = 9,
                    LatitudeAttributeName = "lat",
                    LongitudeAttributeName = "lon"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        // When coordinates are missing, should fallback to spatial index decoding
        result.Should().Contain("else if (item.TryGetValue(\"location\", out var locationValue)",
            "should have fallback to spatial index when coordinates are missing");
        result.Should().Contain("H3Extensions.FromH3Index(spatialIndexString)",
            "should decode H3 index in fallback path");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(decodedLocation.Latitude, decodedLocation.Longitude, spatialIndexString)",
            "should include spatial index in GeoLocation constructor in fallback path");
    }

    [Fact]
    public void GenerateEntityImplementation_RoundTripSerialization_PreservesSpatialIndex()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Store",
            Namespace = "TestNamespace",
            TableName = "stores",
            HasGeospatialPackage = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Location",
                    AttributeName = "location",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "S2",
                    S2Level = 16
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        // Serialization: should encode GeoLocation to S2 token
        result.Should().Contain("var locationIndex = typedEntity.@Location.ToS2Token(16);",
            "should serialize GeoLocation to S2 token");
        result.Should().Contain("item[\"location\"] = new AttributeValue { S = locationIndex };",
            "should store S2 token in DynamoDB attribute");

        // Deserialization: should decode S2 token and preserve it in GeoLocation
        result.Should().Contain("S2Extensions.FromS2Token(spatialIndexString)",
            "should decode S2 token to coordinates");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(decodedLocation.Latitude, decodedLocation.Longitude, spatialIndexString)",
            "should preserve original S2 token in deserialized GeoLocation");

        // The round-trip should preserve the spatial index:
        // 1. Serialize: GeoLocation → S2 token string
        // 2. Deserialize: S2 token string → GeoLocation(lat, lon, S2 token)
        // 3. The deserialized GeoLocation.SpatialIndex should equal the original S2 token
    }

    [Fact]
    public void GenerateEntityImplementation_WithGeoHashAndCoordinateStorage_RoundTripPreservesSpatialIndex()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Store",
            Namespace = "TestNamespace",
            TableName = "stores",
            HasGeospatialPackage = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Location",
                    AttributeName = "location_hash",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "GeoHash",
                    GeoHashPrecision = 7,
                    LatitudeAttributeName = "location_lat",
                    LongitudeAttributeName = "location_lon"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        // Serialization: should encode to GeoHash and store coordinates
        result.Should().Contain("var locationIndex = typedEntity.@Location.ToGeoHash(7);",
            "should serialize GeoLocation to GeoHash");
        result.Should().Contain("item[\"location_lat\"]",
            "should serialize latitude");
        result.Should().Contain("item[\"location_lon\"]",
            "should serialize longitude");

        // Deserialization: should read spatial index and include it with coordinates
        result.Should().Contain("item.TryGetValue(\"location_hash\", out var locationIndexValue)",
            "should read GeoHash value");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(latitude, longitude, spatialIndexValue)",
            "should include GeoHash in deserialized GeoLocation");

        // Fallback path should also preserve spatial index
        result.Should().Contain("else if (item.TryGetValue(\"location_hash\", out var locationValue)",
            "should have fallback to GeoHash");
        result.Should().Contain("GeoHashExtensions.FromGeoHash(spatialIndexString)",
            "should decode GeoHash in fallback");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(decodedLocation.Latitude, decodedLocation.Longitude, spatialIndexString)",
            "should include GeoHash in fallback deserialization");
    }

    [Fact]
    public void GenerateEntityImplementation_WithH3AndCoordinateStorage_RoundTripPreservesSpatialIndex()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Store",
            Namespace = "TestNamespace",
            TableName = "stores",
            HasGeospatialPackage = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Location",
                    AttributeName = "location",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "H3",
                    H3Resolution = 11,
                    LatitudeAttributeName = "lat",
                    LongitudeAttributeName = "lon"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        // Serialization: should encode to H3 and store coordinates
        result.Should().Contain("var locationIndex = typedEntity.@Location.ToH3Index(11);",
            "should serialize GeoLocation to H3 index");
        result.Should().Contain("item[\"lat\"]",
            "should serialize latitude");
        result.Should().Contain("item[\"lon\"]",
            "should serialize longitude");

        // Deserialization: should read spatial index and include it with coordinates
        result.Should().Contain("item.TryGetValue(\"location\", out var locationIndexValue)",
            "should read H3 index value");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(latitude, longitude, spatialIndexValue)",
            "should include H3 index in deserialized GeoLocation");

        // Fallback path should also preserve spatial index
        result.Should().Contain("else if (item.TryGetValue(\"location\", out var locationValue)",
            "should have fallback to H3 index");
        result.Should().Contain("H3Extensions.FromH3Index(spatialIndexString)",
            "should decode H3 index in fallback");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(decodedLocation.Latitude, decodedLocation.Longitude, spatialIndexString)",
            "should include H3 index in fallback deserialization");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNullableGeoLocation_IncludesSpatialIndexInDeserialization()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Store",
            Namespace = "TestNamespace",
            TableName = "stores",
            HasGeospatialPackage = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Location",
                    AttributeName = "location",
                    PropertyType = "GeoLocation?",
                    IsNullable = true,
                    SpatialIndexType = "S2",
                    S2Level = 16
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("S2Extensions.FromS2Token(spatialIndexString)",
            "should deserialize S2 token for nullable GeoLocation");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(decodedLocation.Latitude, decodedLocation.Longitude, spatialIndexString)",
            "should include spatial index in nullable GeoLocation constructor");
    }

    [Fact]
    public void GenerateEntityImplementation_WithMultipleSpatialIndexTypes_IncludesSpatialIndexForEach()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Store",
            Namespace = "TestNamespace",
            TableName = "stores",
            HasGeospatialPackage = true,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Location1",
                    AttributeName = "location1",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "GeoHash",
                    GeoHashPrecision = 6
                },
                new PropertyModel
                {
                    PropertyName = "Location2",
                    AttributeName = "location2",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "S2",
                    S2Level = 16
                },
                new PropertyModel
                {
                    PropertyName = "Location3",
                    AttributeName = "location3",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "H3",
                    H3Resolution = 9
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        // GeoHash property - each property uses its own decoded variable name
        result.Should().Contain("GeoHashExtensions.FromGeoHash(spatialIndexString)",
            "should deserialize GeoHash for Location1");
        result.Should().Contain("var decodedLocation1 = GeoHashExtensions.FromGeoHash(spatialIndexString)",
            "should decode GeoHash to decodedLocation1 variable");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(decodedLocation1.Latitude, decodedLocation1.Longitude, spatialIndexString)",
            "should include GeoHash in Location1 deserialization");

        // S2 property
        result.Should().Contain("S2Extensions.FromS2Token(spatialIndexString)",
            "should deserialize S2 token for Location2");
        result.Should().Contain("var decodedLocation2 = S2Extensions.FromS2Token(spatialIndexString)",
            "should decode S2 token to decodedLocation2 variable");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(decodedLocation2.Latitude, decodedLocation2.Longitude, spatialIndexString)",
            "should include S2 token in Location2 deserialization");

        // H3 property
        result.Should().Contain("H3Extensions.FromH3Index(spatialIndexString)",
            "should deserialize H3 index for Location3");
        result.Should().Contain("var decodedLocation3 = H3Extensions.FromH3Index(spatialIndexString)",
            "should decode H3 index to decodedLocation3 variable");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(decodedLocation3.Latitude, decodedLocation3.Longitude, spatialIndexString)",
            "should include H3 index in Location3 deserialization");
    }
}
