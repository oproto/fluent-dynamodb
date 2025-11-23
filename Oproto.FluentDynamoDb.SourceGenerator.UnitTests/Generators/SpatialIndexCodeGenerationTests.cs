using AwesomeAssertions;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

[Trait("Category", "Unit")]
public class SpatialIndexCodeGenerationTests
{
    [Fact]
    public void GenerateEntityImplementation_WithGeoHashProperty_GeneratesGeoHashCode()
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
                    GeoHashPrecision = 7
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("using Oproto.FluentDynamoDb.Geospatial.GeoHash;", 
            "should include GeoHash using statement");
        result.Should().Contain("ToGeoHash(7)", 
            "should use configured GeoHash precision");
        result.Should().Contain("GeoHashExtensions.FromGeoHash", 
            "should use GeoHash deserialization");
    }

    [Fact]
    public void GenerateEntityImplementation_WithS2Property_GeneratesS2Code()
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
                    S2Level = 20
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("using Oproto.FluentDynamoDb.Geospatial.S2;", 
            "should include S2 using statement");
        result.Should().Contain("ToS2Token(20)", 
            "should use configured S2 level");
        result.Should().Contain("S2Extensions.FromS2Token", 
            "should use S2 deserialization");
        result.Should().Contain("Serialize GeoLocation property Location to S2", 
            "should include S2 serialization comment");
        result.Should().Contain("Deserialize GeoLocation property Location from S2 token", 
            "should include S2 deserialization comment");
    }

    [Fact]
    public void GenerateEntityImplementation_WithH3Property_GeneratesH3Code()
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
                    H3Resolution = 11
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("using Oproto.FluentDynamoDb.Geospatial.H3;", 
            "should include H3 using statement");
        result.Should().Contain("ToH3Index(11)", 
            "should use configured H3 resolution");
        result.Should().Contain("H3Extensions.FromH3Index", 
            "should use H3 deserialization");
        result.Should().Contain("Serialize GeoLocation property Location to H3", 
            "should include H3 serialization comment");
        result.Should().Contain("Deserialize GeoLocation property Location from H3 index", 
            "should include H3 deserialization comment");
    }

    [Fact]
    public void GenerateEntityImplementation_WithDefaultS2Level_UsesDefaultValue()
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
                    S2Level = null // Not specified, should use default
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("ToS2Token(16)", 
            "should use default S2 level of 16 when not specified");
    }

    [Fact]
    public void GenerateEntityImplementation_WithDefaultH3Resolution_UsesDefaultValue()
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
                    H3Resolution = null // Not specified, should use default
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("ToH3Index(9)", 
            "should use default H3 resolution of 9 when not specified");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNoSpatialIndexType_DefaultsToGeoHash()
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
                    SpatialIndexType = null, // Not specified, should default to GeoHash
                    GeoHashPrecision = 6
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().Contain("ToGeoHash(6)", 
            "should default to GeoHash when spatial index type is not specified");
        result.Should().Contain("GeoHashExtensions.FromGeoHash", 
            "should use GeoHash deserialization by default");
    }

    [Fact]
    public void GenerateEntityImplementation_WithNullableGeoLocation_HandlesNullCorrectly()
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
        result.Should().Contain("if (typedEntity.@Location != null)", 
            "should check for null before serializing nullable GeoLocation");
        result.Should().Contain(".Value.ToS2Token(16)", 
            "should access Value property for nullable GeoLocation");
    }

    [Fact]
    public void GenerateEntityImplementation_WithAllSpatialIndexTypes_IncludesAllUsingStatements()
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
        result.Should().Contain("using Oproto.FluentDynamoDb.Geospatial.GeoHash;", 
            "should include GeoHash using statement");
        result.Should().Contain("using Oproto.FluentDynamoDb.Geospatial.S2;", 
            "should include S2 using statement");
        result.Should().Contain("using Oproto.FluentDynamoDb.Geospatial.H3;", 
            "should include H3 using statement");
    }

    [Fact]
    public void GenerateEntityImplementation_WithCoordinateStorage_GeneratesCoordinateSerializationCode()
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
        result.Should().Contain("Serialize GeoLocation property Location to S2 with coordinate storage", 
            "should include coordinate storage comment in serialization");
        result.Should().Contain("Store full-resolution coordinates", 
            "should include coordinate storage comment");
        result.Should().Contain("item[\"lat\"]", 
            "should serialize latitude attribute");
        result.Should().Contain("item[\"lon\"]", 
            "should serialize longitude attribute");
        result.Should().Contain(".Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)", 
            "should serialize latitude value");
        result.Should().Contain(".Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)", 
            "should serialize longitude value");
    }

    [Fact]
    public void GenerateEntityImplementation_WithCoordinateStorage_GeneratesCoordinateDeserializationCode()
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
        result.Should().Contain("Deserialize GeoLocation property Location from coordinates (if available) or S2 token", 
            "should include coordinate deserialization comment");
        result.Should().Contain("Priority: 1) Exact coordinates, 2) Spatial index decoding", 
            "should document priority order");
        result.Should().Contain("item.TryGetValue(\"lat\"", 
            "should check for latitude attribute");
        result.Should().Contain("item.TryGetValue(\"lon\"", 
            "should check for longitude attribute");
        result.Should().Contain("Reconstruct from exact coordinates", 
            "should include reconstruction comment");
        result.Should().Contain("double.Parse(locationLatValue.N, System.Globalization.CultureInfo.InvariantCulture)", 
            "should parse latitude from number attribute");
        result.Should().Contain("double.Parse(locationLonValue.N, System.Globalization.CultureInfo.InvariantCulture)", 
            "should parse longitude from number attribute");
        result.Should().Contain("new Oproto.FluentDynamoDb.Geospatial.GeoLocation(latitude, longitude)", 
            "should reconstruct GeoLocation from coordinates");
    }

    [Fact]
    public void GenerateEntityImplementation_WithCoordinateStorage_GeneratesFallbackToSpatialIndex()
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
        result.Should().Contain("else if (item.TryGetValue(\"location\"", 
            "should have fallback to spatial index");
        result.Should().Contain("Fallback to spatial index decoding (for backward compatibility or when coordinates are missing)", 
            "should include fallback comment");
        result.Should().Contain("H3Extensions.FromH3Index", 
            "should use H3 deserialization in fallback");
    }

    [Fact]
    public void GenerateEntityImplementation_WithCoordinateStorageAndGeoHash_GeneratesCorrectCode()
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
        result.Should().Contain("Serialize GeoLocation property Location to GeoHash with coordinate storage", 
            "should include GeoHash coordinate storage comment");
        result.Should().Contain("item[\"location_lat\"]", 
            "should serialize custom latitude attribute name");
        result.Should().Contain("item[\"location_lon\"]", 
            "should serialize custom longitude attribute name");
        result.Should().Contain("item.TryGetValue(\"location_lat\"", 
            "should check for custom latitude attribute name");
        result.Should().Contain("item.TryGetValue(\"location_lon\"", 
            "should check for custom longitude attribute name");
        result.Should().Contain("GeoHashExtensions.FromGeoHash", 
            "should use GeoHash deserialization in fallback");
    }

    [Fact]
    public void GenerateEntityImplementation_WithoutCoordinateStorage_DoesNotGenerateCoordinateCode()
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
                    LatitudeAttributeName = null,
                    LongitudeAttributeName = null
                }
            }
        };

        // Act
        var result = MapperGenerator.GenerateEntityImplementation(entity);

        // Assert
        result.Should().NotContain("Store full-resolution coordinates", 
            "should not include coordinate storage comment");
        result.Should().NotContain("Reconstruct from exact coordinates", 
            "should not include coordinate reconstruction comment");
        result.Should().NotContain("Priority: 1) Exact coordinates", 
            "should not include priority comment");
        result.Should().Contain("Serialize GeoLocation property Location to S2", 
            "should include standard serialization comment");
        result.Should().Contain("Deserialize GeoLocation property Location from S2 token", 
            "should include standard deserialization comment");
    }
}
