using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests for coordinate storage functionality in code generation.
/// These tests verify that the source generator correctly handles coordinate storage
/// for GeoLocation properties with various spatial index types.
/// </summary>
[Trait("Category", "Unit")]
public class CoordinateStoragePropertyTests
{
    // Feature: s2-h3-geospatial-support, Property 13: Coordinate storage creates separate attributes
    // For any GeoLocation with StoreCoordinatesAttribute, the serialized data should contain 
    // the spatial index attribute plus separate latitude and longitude attributes
    // Validates: Requirements 6.1, 6.2
    [Fact]
    public void CoordinateStorage_WithS2_CreatesSeparateAttributes()
    {
        // Arrange: Create entity with coordinate storage
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test_table",
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
                    AttributeName = "location_s2",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "S2",
                    S2Level = 16,
                    LatitudeAttributeName = "location_lat",
                    LongitudeAttributeName = "location_lon"
                }
            }
        };

        // Act: Generate code
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: Should contain all three attributes
        result.Should().Contain("item[\"location_s2\"]", 
            "should serialize spatial index attribute");
        result.Should().Contain("item[\"location_lat\"]", 
            "should serialize latitude attribute");
        result.Should().Contain("item[\"location_lon\"]", 
            "should serialize longitude attribute");
        result.Should().Contain("Store full-resolution coordinates", 
            "should include coordinate storage comment");
        result.Should().Contain(".Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)", 
            "should serialize latitude value");
        result.Should().Contain(".Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)", 
            "should serialize longitude value");
    }

    [Fact]
    public void CoordinateStorage_WithH3_CreatesSeparateAttributes()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test_table",
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
                    AttributeName = "loc_h3",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "H3",
                    H3Resolution = 9,
                    LatitudeAttributeName = "loc_lat",
                    LongitudeAttributeName = "loc_lon"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("item[\"loc_h3\"]");
        result.Should().Contain("item[\"loc_lat\"]");
        result.Should().Contain("item[\"loc_lon\"]");
        result.Should().Contain("Store full-resolution coordinates");
    }

    [Fact]
    public void CoordinateStorage_WithGeoHash_CreatesSeparateAttributes()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test_table",
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
                    LatitudeAttributeName = "lat",
                    LongitudeAttributeName = "lon"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("item[\"location_hash\"]");
        result.Should().Contain("item[\"lat\"]");
        result.Should().Contain("item[\"lon\"]");
        result.Should().Contain("Store full-resolution coordinates");
    }

    // Feature: s2-h3-geospatial-support, Property 14: Coordinate deserialization preserves exact values
    // For any GeoLocation serialized with coordinate storage, deserializing should return 
    // the exact original coordinates, not the cell center
    // Validates: Requirements 6.3
    [Fact]
    public void CoordinateDeserialization_PreservesExactValues()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test_table",
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

        // Assert: Should prioritize coordinates over spatial index
        result.Should().Contain("item.TryGetValue(\"lat\"", 
            "should check for latitude attribute");
        result.Should().Contain("item.TryGetValue(\"lon\"", 
            "should check for longitude attribute");
        result.Should().Contain("Reconstruct from exact coordinates", 
            "should include reconstruction comment");
        result.Should().Contain("Priority: 1) Exact coordinates, 2) Spatial index decoding", 
            "should document priority order");
        result.Should().Contain("double.Parse(", 
            "should parse coordinate values");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(latitude, longitude)", 
            "should reconstruct GeoLocation from coordinates");
        result.Should().Contain("else if (item.TryGetValue(\"location\"", 
            "should have fallback to spatial index");
    }

    // Feature: s2-h3-geospatial-support, Property 15: Single-field mode stores only spatial index
    // For any GeoLocation without coordinate storage, the serialized data should contain 
    // only one attribute: the spatial index
    // Validates: Requirements 6.4
    [Fact]
    public void SingleFieldMode_StoresOnlySpatialIndex()
    {
        // Arrange: Create entity WITHOUT coordinate storage
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test_table",
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
                    LatitudeAttributeName = null,  // No coordinate storage
                    LongitudeAttributeName = null
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert: Should NOT contain coordinate storage code
        result.Should().NotContain("Store full-resolution coordinates", 
            "should not include coordinate storage comment");
        result.Should().NotContain("Reconstruct from exact coordinates", 
            "should not include coordinate reconstruction comment");
        result.Should().NotContain("Priority: 1) Exact coordinates", 
            "should not include priority comment");
        result.Should().Contain("item[\"location\"]", 
            "should serialize spatial index");
        result.Should().Contain("ToS2Token(16)", 
            "should use S2 encoding");
        result.Should().Contain("Serialize GeoLocation property Location to S2", 
            "should include standard serialization comment");
        result.Should().Contain("Deserialize GeoLocation property Location from S2 token", 
            "should include standard deserialization comment");
    }

    [Fact]
    public void CoordinateStorage_FallsBackToSpatialIndex()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test_table",
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

        // Assert: Should have fallback logic
        result.Should().Contain("else if (item.TryGetValue(\"location\"", 
            "should have fallback to spatial index");
        result.Should().Contain("Fallback to spatial index decoding (for backward compatibility or when coordinates are missing)", 
            "should include fallback comment");
        result.Should().Contain("S2Extensions.FromS2Token", 
            "should use S2 deserialization in fallback");
    }

    [Fact]
    public void CoordinateStorage_WithCustomAttributeNames_UsesCorrectNames()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test_table",
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
                    AttributeName = "custom_spatial",
                    PropertyType = "GeoLocation",
                    SpatialIndexType = "H3",
                    H3Resolution = 9,
                    LatitudeAttributeName = "custom_lat",
                    LongitudeAttributeName = "custom_lon"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("item[\"custom_spatial\"]");
        result.Should().Contain("item[\"custom_lat\"]");
        result.Should().Contain("item[\"custom_lon\"]");
        result.Should().Contain("item.TryGetValue(\"custom_lat\"");
        result.Should().Contain("item.TryGetValue(\"custom_lon\"");
    }

    [Fact]
    public void CoordinateStorage_WithNullableGeoLocation_HandlesNullCorrectly()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "TestEntity",
            Namespace = "TestNamespace",
            TableName = "test_table",
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
                    S2Level = 16,
                    LatitudeAttributeName = "lat",
                    LongitudeAttributeName = "lon"
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("if (typedEntity.@Location != null)", 
            "should check for null before serializing");
        result.Should().Contain(".Value.ToS2Token(16)", 
            "should access Value property for nullable GeoLocation");
        result.Should().Contain(".Value.Latitude", 
            "should access Value property for latitude");
        result.Should().Contain(".Value.Longitude", 
            "should access Value property for longitude");
    }
}
