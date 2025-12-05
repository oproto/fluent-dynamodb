using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for H3 geospatial storage with DynamoDB.
/// Tests verify that the source generator correctly serializes and deserializes H3-indexed
/// GeoLocation properties to/from H3 cell indices in DynamoDB.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "H3")]
[Trait("Feature", "SourceGenerator")]
public class H3StorageIntegrationTests : IntegrationTestBase
{
    public H3StorageIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    #region 25.1 Test H3 single-field serialization
    
    [Fact]
    public async Task WriteToDynamoDb_WithH3Location_StoresAsH3IndexString()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        var store = new H3StoreEntity
        {
            StoreId = "store-001",
            Region = "west",
            Name = "Downtown Store",
            Location = new GeoLocation(37.7749, -122.4194), // San Francisco
            Description = "Main downtown location"
        };
        
        // Act - Write entity to DynamoDB
        var item = H3StoreEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Read back the raw item from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        
        // Assert - Verify location is stored as H3 index string in DynamoDB
        response.Item.Should().ContainKey("location");
        response.Item["location"].S.Should().NotBeNullOrEmpty();
        
        // Verify it's a valid H3 index (15-character hexadecimal string)
        response.Item["location"].S.Should().MatchRegex("^[0-9a-f]{15}$");
        
        // Verify the H3 index is correct for resolution 9
        var expectedIndex = H3Extensions.ToH3Index(store.Location, 9);
        response.Item["location"].S.Should().Be(expectedIndex);
        
        // Verify it's a valid H3 index that can be decoded
        var decoded = H3Extensions.FromH3Index(response.Item["location"].S);
        decoded.Latitude.Should().BeApproximately(37.7749, 0.01);
        decoded.Longitude.Should().BeApproximately(-122.4194, 0.01);
    }
    
    [Fact]
    public async Task ToDynamoDb_WithH3Location_SerializesToH3IndexString()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        var store = new H3StoreEntity
        {
            StoreId = "store-001",
            Region = "west",
            Name = "Downtown Store",
            Location = new GeoLocation(37.7749, -122.4194), // San Francisco
            Description = "Main downtown location"
        };
        
        // Act - Convert to DynamoDB item
        var item = H3StoreEntity.ToDynamoDb(store);
        
        // Assert - Verify location is stored as H3 index string
        item.Should().ContainKey("location");
        item["location"].S.Should().NotBeNullOrEmpty();
        
        // Verify it's a valid H3 index (15-character hexadecimal string)
        item["location"].S.Should().MatchRegex("^[0-9a-f]{15}$");
        
        // Verify the H3 index is correct for resolution 9
        var expectedIndex = H3Extensions.ToH3Index(store.Location, 9);
        item["location"].S.Should().Be(expectedIndex);
        
        // Verify it's a valid H3 index that can be decoded
        var decoded = H3Extensions.FromH3Index(item["location"].S);
        decoded.Latitude.Should().BeApproximately(37.7749, 0.01);
        decoded.Longitude.Should().BeApproximately(-122.4194, 0.01);
    }
    
    [Fact]
    public async Task ToDynamoDb_WithH3Location_IndexFormatIsCorrect()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        var store = new H3StoreEntity
        {
            StoreId = "store-002",
            Region = "east",
            Name = "East Side Store",
            Location = new GeoLocation(40.7128, -74.0060) // New York
        };
        
        // Act - Convert to DynamoDB item
        var item = H3StoreEntity.ToDynamoDb(store);
        
        // Assert - Verify index is valid H3 format (exactly 15 characters, hexadecimal)
        item["location"].S.Length.Should().Be(15, "H3 index should be exactly 15 characters");
        item["location"].S.Should().MatchRegex("^[0-9a-f]{15}$", "H3 index should be hexadecimal");
    }
    
    [Fact]
    public async Task ToDynamoDb_WithH3Location_IndexCanBeDecodedBackToCoordinates()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        var originalLocation = new GeoLocation(51.5074, -0.1278); // London
        var store = new H3StoreEntity
        {
            StoreId = "store-003",
            Region = "europe",
            Name = "London Store",
            Location = originalLocation
        };
        
        // Act - Convert to DynamoDB item
        var item = H3StoreEntity.ToDynamoDb(store);
        
        // Assert - Verify index can be decoded back to coordinates
        var h3Index = item["location"].S;
        var decoded = H3Extensions.FromH3Index(h3Index);
        
        // H3 resolution 9 has ~174m edge length, so we expect coordinates within ~0.01 degrees
        decoded.Latitude.Should().BeApproximately(originalLocation.Latitude, 0.01);
        decoded.Longitude.Should().BeApproximately(originalLocation.Longitude, 0.01);
    }
    
    #endregion
    
    #region 25.2 Test H3 single-field deserialization
    
    [Fact]
    public async Task FromDynamoDb_WithH3IndexString_DeserializesToGeoLocation()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        var sanFrancisco = new GeoLocation(37.7749, -122.4194);
        var h3Index = H3Extensions.ToH3Index(sanFrancisco, 9);
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-004" },
            ["sk"] = new AttributeValue { S = "west" },
            ["name"] = new AttributeValue { S = "SF Store" },
            ["location"] = new AttributeValue { S = h3Index }
        };
        
        // Act - Convert from DynamoDB item
        var store = H3StoreEntity.FromDynamoDb<H3StoreEntity>(item);
        
        // Assert - Verify location is deserialized correctly
        store.Location.Should().NotBe(default(GeoLocation));
        store.Location.Latitude.Should().BeApproximately(37.7749, 0.01);
        store.Location.Longitude.Should().BeApproximately(-122.4194, 0.01);
    }
    
    [Fact]
    public async Task FromDynamoDb_WithH3Index_ReconstructsGeoLocationCorrectly()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        // Create a known H3 index for Tokyo
        var tokyo = new GeoLocation(35.6762, 139.6503);
        var h3Index = H3Extensions.ToH3Index(tokyo, 9);
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-005" },
            ["sk"] = new AttributeValue { S = "asia" },
            ["name"] = new AttributeValue { S = "Tokyo Store" },
            ["location"] = new AttributeValue { S = h3Index }
        };
        
        // Act - Convert from DynamoDB item
        var store = H3StoreEntity.FromDynamoDb<H3StoreEntity>(item);
        
        // Assert - Verify location is reconstructed correctly
        store.Location.Latitude.Should().BeApproximately(35.6762, 0.01);
        store.Location.Longitude.Should().BeApproximately(139.6503, 0.01);
    }
    
    [Fact]
    public async Task FromDynamoDb_WithH3Index_CoordinatesAreWithinCellBounds()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        var originalLocation = new GeoLocation(48.8566, 2.3522); // Paris
        var h3Index = H3Extensions.ToH3Index(originalLocation, 9);
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-006" },
            ["sk"] = new AttributeValue { S = "europe" },
            ["name"] = new AttributeValue { S = "Paris Store" },
            ["location"] = new AttributeValue { S = h3Index }
        };
        
        // Act - Convert from DynamoDB item
        var store = H3StoreEntity.FromDynamoDb<H3StoreEntity>(item);
        
        // Assert - Verify coordinates are within the cell bounds
        var cell = H3Extensions.ToH3Cell(originalLocation, 9);
        var bounds = cell.Bounds;
        
        store.Location.Latitude.Should().BeInRange(bounds.Southwest.Latitude, bounds.Northeast.Latitude);
        store.Location.Longitude.Should().BeInRange(bounds.Southwest.Longitude, bounds.Northeast.Longitude);
    }
    
    #endregion
    
    #region 25.3 Test H3 round-trip persistence
    
    [Fact]
    public async Task RoundTrip_WithH3Location_PreservesLocationWithinCellPrecision()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        var originalStore = new H3StoreEntity
        {
            StoreId = "store-007",
            Region = "west",
            Name = "Round Trip Store",
            Location = new GeoLocation(37.7749, -122.4194), // San Francisco
            Description = "Testing round-trip persistence"
        };
        
        // Act - Write to DynamoDB
        var item = H3StoreEntity.ToDynamoDb(originalStore);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Read back from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = originalStore.StoreId },
            ["sk"] = new AttributeValue { S = originalStore.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var retrievedStore = H3StoreEntity.FromDynamoDb<H3StoreEntity>(response.Item);
        
        // Assert - Verify location is preserved within H3 cell precision
        // H3 resolution 9 has ~174m edge length, so we expect coordinates within ~0.01 degrees
        retrievedStore.Location.Latitude.Should().BeApproximately(originalStore.Location.Latitude, 0.01);
        retrievedStore.Location.Longitude.Should().BeApproximately(originalStore.Location.Longitude, 0.01);
        
        // Verify both locations encode to the same H3 cell
        var originalIndex = H3Extensions.ToH3Index(originalStore.Location, 9);
        var retrievedIndex = H3Extensions.ToH3Index(retrievedStore.Location, 9);
        retrievedIndex.Should().Be(originalIndex, "both locations should be in the same H3 cell");
    }
    
    [Fact]
    public async Task RoundTrip_WithMultipleLocations_PreservesAllLocations()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        var testLocations = new[]
        {
            new { Id = "store-008", Region = "west", Name = "San Francisco", Location = new GeoLocation(37.7749, -122.4194) },
            new { Id = "store-009", Region = "east", Name = "New York", Location = new GeoLocation(40.7128, -74.0060) },
            new { Id = "store-010", Region = "europe", Name = "London", Location = new GeoLocation(51.5074, -0.1278) },
            new { Id = "store-011", Region = "asia", Name = "Tokyo", Location = new GeoLocation(35.6762, 139.6503) },
            new { Id = "store-012", Region = "oceania", Name = "Sydney", Location = new GeoLocation(-33.8688, 151.2093) }
        };
        
        // Act - Write all stores to DynamoDB
        foreach (var testData in testLocations)
        {
            var store = new H3StoreEntity
            {
                StoreId = testData.Id,
                Region = testData.Region,
                Name = testData.Name,
                Location = testData.Location
            };
            
            var item = H3StoreEntity.ToDynamoDb(store);
            await DynamoDb.PutItemAsync(TableName, item);
        }
        
        // Read back all stores and verify
        foreach (var testData in testLocations)
        {
            var key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testData.Id },
                ["sk"] = new AttributeValue { S = testData.Region }
            };
            var response = await DynamoDb.GetItemAsync(TableName, key);
            var retrievedStore = H3StoreEntity.FromDynamoDb<H3StoreEntity>(response.Item);
            
            // Assert - Verify location is preserved within H3 cell precision
            retrievedStore.Location.Latitude.Should().BeApproximately(testData.Location.Latitude, 0.01,
                $"latitude should be preserved for {testData.Name}");
            retrievedStore.Location.Longitude.Should().BeApproximately(testData.Location.Longitude, 0.01,
                $"longitude should be preserved for {testData.Name}");
            
            // Verify both locations encode to the same H3 cell
            var originalIndex = H3Extensions.ToH3Index(testData.Location, 9);
            var retrievedIndex = H3Extensions.ToH3Index(retrievedStore.Location, 9);
            retrievedIndex.Should().Be(originalIndex, $"both locations should be in the same H3 cell for {testData.Name}");
        }
    }
    
    [Fact]
    public async Task RoundTrip_AtEquator_PreservesLocation()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        var store = new H3StoreEntity
        {
            StoreId = "store-013",
            Region = "equator",
            Name = "Equator Store",
            Location = new GeoLocation(0.0, 0.0), // Null Island (equator and prime meridian)
            Description = "Testing equator location"
        };
        
        // Act - Write to DynamoDB and read back
        var item = H3StoreEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var retrievedStore = H3StoreEntity.FromDynamoDb<H3StoreEntity>(response.Item);
        
        // Assert - Verify location is preserved
        retrievedStore.Location.Latitude.Should().BeApproximately(0.0, 0.01);
        retrievedStore.Location.Longitude.Should().BeApproximately(0.0, 0.01);
    }
    
    [Fact]
    public async Task RoundTrip_AtDateLine_PreservesLocation()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        var store = new H3StoreEntity
        {
            StoreId = "store-014",
            Region = "pacific",
            Name = "Date Line Store",
            Location = new GeoLocation(0.0, 180.0), // International Date Line
            Description = "Testing date line location"
        };
        
        // Act - Write to DynamoDB and read back
        var item = H3StoreEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var retrievedStore = H3StoreEntity.FromDynamoDb<H3StoreEntity>(response.Item);
        
        // Assert - Verify location is preserved (note: 180 and -180 are the same longitude)
        retrievedStore.Location.Latitude.Should().BeApproximately(0.0, 0.01);
        Math.Abs(retrievedStore.Location.Longitude).Should().BeApproximately(180.0, 0.01);
    }
    
    [Fact]
    public async Task RoundTrip_NearNorthPole_PreservesLocation()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        var store = new H3StoreEntity
        {
            StoreId = "store-015",
            Region = "arctic",
            Name = "North Pole Store",
            Location = new GeoLocation(85.0, 0.0), // Near North Pole
            Description = "Testing near-pole location"
        };
        
        // Act - Write to DynamoDB and read back
        var item = H3StoreEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var retrievedStore = H3StoreEntity.FromDynamoDb<H3StoreEntity>(response.Item);
        
        // Assert - Verify location is preserved
        retrievedStore.Location.Latitude.Should().BeApproximately(85.0, 0.01);
        retrievedStore.Location.Longitude.Should().BeApproximately(0.0, 0.1); // Longitude is less precise near poles
    }
    
    [Fact]
    public async Task RoundTrip_NearSouthPole_PreservesLocation()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        var store = new H3StoreEntity
        {
            StoreId = "store-016",
            Region = "antarctic",
            Name = "South Pole Store",
            Location = new GeoLocation(-85.0, 0.0), // Near South Pole
            Description = "Testing near-pole location"
        };
        
        // Act - Write to DynamoDB and read back
        var item = H3StoreEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var retrievedStore = H3StoreEntity.FromDynamoDb<H3StoreEntity>(response.Item);
        
        // Assert - Verify location is preserved
        retrievedStore.Location.Latitude.Should().BeApproximately(-85.0, 0.01);
        retrievedStore.Location.Longitude.Should().BeApproximately(0.0, 0.1); // Longitude is less precise near poles
    }
    
    [Fact]
    public async Task RoundTrip_WithPentagonCell_PreservesLocation()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        // H3 has 12 pentagon base cells. One of them is near the North Pole.
        // Pentagon base cell 4 is centered around (58.2°N, 11.25°E)
        // Let's use a location that falls into a pentagon cell
        var store = new H3StoreEntity
        {
            StoreId = "store-017",
            Region = "pentagon",
            Name = "Pentagon Cell Store",
            Location = new GeoLocation(58.2, 11.25), // Near pentagon base cell 4
            Description = "Testing pentagon cell handling"
        };
        
        // Act - Write to DynamoDB and read back
        var item = H3StoreEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var retrievedStore = H3StoreEntity.FromDynamoDb<H3StoreEntity>(response.Item);
        
        // Assert - Verify location is preserved within H3 cell precision
        retrievedStore.Location.Latitude.Should().BeApproximately(store.Location.Latitude, 0.01);
        retrievedStore.Location.Longitude.Should().BeApproximately(store.Location.Longitude, 0.01);
        
        // Verify both locations encode to the same H3 cell
        var originalIndex = H3Extensions.ToH3Index(store.Location, 9);
        var retrievedIndex = H3Extensions.ToH3Index(retrievedStore.Location, 9);
        retrievedIndex.Should().Be(originalIndex, "both locations should be in the same H3 cell");
    }
    
    [Fact]
    public async Task RoundTrip_WithMultiplePentagonCells_PreservesAllLocations()
    {
        // Arrange
        await CreateTableAsync<H3StoreEntity>();
        
        // H3 has 12 pentagon base cells at specific locations
        // Testing a few locations that fall into different pentagon cells
        var pentagonLocations = new[]
        {
            new { Id = "store-018", Region = "pentagon-1", Name = "Pentagon 1", Location = new GeoLocation(58.2, 11.25) },
            new { Id = "store-019", Region = "pentagon-2", Name = "Pentagon 2", Location = new GeoLocation(52.0, -170.0) },
            new { Id = "store-020", Region = "pentagon-3", Name = "Pentagon 3", Location = new GeoLocation(-58.2, 11.25) }
        };
        
        // Act - Write all stores to DynamoDB
        foreach (var testData in pentagonLocations)
        {
            var store = new H3StoreEntity
            {
                StoreId = testData.Id,
                Region = testData.Region,
                Name = testData.Name,
                Location = testData.Location
            };
            
            var item = H3StoreEntity.ToDynamoDb(store);
            await DynamoDb.PutItemAsync(TableName, item);
        }
        
        // Read back all stores and verify
        foreach (var testData in pentagonLocations)
        {
            var key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testData.Id },
                ["sk"] = new AttributeValue { S = testData.Region }
            };
            var response = await DynamoDb.GetItemAsync(TableName, key);
            var retrievedStore = H3StoreEntity.FromDynamoDb<H3StoreEntity>(response.Item);
            
            // Assert - Verify location is preserved within H3 cell precision
            retrievedStore.Location.Latitude.Should().BeApproximately(testData.Location.Latitude, 0.01,
                $"latitude should be preserved for {testData.Name}");
            retrievedStore.Location.Longitude.Should().BeApproximately(testData.Location.Longitude, 0.01,
                $"longitude should be preserved for {testData.Name}");
            
            // Verify both locations encode to the same H3 cell
            var originalIndex = H3Extensions.ToH3Index(testData.Location, 9);
            var retrievedIndex = H3Extensions.ToH3Index(retrievedStore.Location, 9);
            retrievedIndex.Should().Be(originalIndex, $"both locations should be in the same H3 cell for {testData.Name}");
        }
    }
    
    #endregion
    
    #region 25.4 Test H3 multi-field serialization with StoreCoordinatesAttribute
    
    [Fact]
    public async Task WriteToDynamoDb_WithH3LocationAndCoordinateStorage_CreatesThreeAttributes()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var store = new H3StoreWithCoordsEntity
        {
            StoreId = "store-coords-001",
            Region = "west",
            Name = "SF Store with Coords",
            Location = new GeoLocation(37.7749, -122.4194), // San Francisco
            Description = "Testing coordinate storage"
        };
        
        // Act - Write entity to DynamoDB
        var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Read back the raw item from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        
        // Assert - Verify three attributes are created: H3 index, latitude, longitude
        response.Item.Should().ContainKey("location", "H3 index attribute should exist");
        response.Item.Should().ContainKey("location_lat", "latitude attribute should exist");
        response.Item.Should().ContainKey("location_lon", "longitude attribute should exist");
        
        // Verify we have exactly these three location-related attributes (plus pk, sk, name, description)
        response.Item.Keys.Should().Contain(new[] { "pk", "sk", "name", "location", "location_lat", "location_lon", "description" });
    }
    
    [Fact]
    public async Task ToDynamoDb_WithH3LocationAndCoordinateStorage_AttributeNamesMatchConfiguration()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var store = new H3StoreWithCoordsEntity
        {
            StoreId = "store-coords-002",
            Region = "east",
            Name = "NYC Store with Coords",
            Location = new GeoLocation(40.7128, -74.0060) // New York
        };
        
        // Act - Convert to DynamoDB item
        var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
        
        // Assert - Verify attribute names match the StoreCoordinatesAttribute configuration
        // StoreCoordinatesAttribute specifies: LatitudeAttributeName = "location_lat", LongitudeAttributeName = "location_lon"
        item.Should().ContainKey("location", "H3 index should use the base attribute name");
        item.Should().ContainKey("location_lat", "latitude should use configured attribute name");
        item.Should().ContainKey("location_lon", "longitude should use configured attribute name");
        
        // Verify the attribute names are exactly as configured (not auto-generated)
        item.Keys.Where(k => k.StartsWith("location")).Should().BeEquivalentTo(new[] { "location", "location_lat", "location_lon" });
    }
    
    [Fact]
    public async Task ToDynamoDb_WithH3LocationAndCoordinateStorage_AllThreeFieldsHaveCorrectValues()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(51.5074, -0.1278); // London
        var store = new H3StoreWithCoordsEntity
        {
            StoreId = "store-coords-003",
            Region = "europe",
            Name = "London Store with Coords",
            Location = originalLocation,
            Description = "Testing all three field values"
        };
        
        // Act - Convert to DynamoDB item
        var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
        
        // Assert - Verify H3 index field has correct value
        item["location"].S.Should().NotBeNullOrEmpty("H3 index should be stored");
        item["location"].S.Should().MatchRegex("^[0-9a-f]{15}$", "H3 index should be valid 15-character hex");
        
        var expectedIndex = H3Extensions.ToH3Index(originalLocation, 9);
        item["location"].S.Should().Be(expectedIndex, "H3 index should match expected value");
        
        // Assert - Verify latitude field has correct value
        item["location_lat"].N.Should().NotBeNullOrEmpty("latitude should be stored as number");
        double.Parse(item["location_lat"].N).Should().Be(originalLocation.Latitude, "latitude should match exactly");
        
        // Assert - Verify longitude field has correct value
        item["location_lon"].N.Should().NotBeNullOrEmpty("longitude should be stored as number");
        double.Parse(item["location_lon"].N).Should().Be(originalLocation.Longitude, "longitude should match exactly");
    }
    
    [Fact]
    public async Task WriteToDynamoDb_WithH3LocationAndCoordinateStorage_AllFieldsPersistedCorrectly()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(35.6762, 139.6503); // Tokyo
        var store = new H3StoreWithCoordsEntity
        {
            StoreId = "store-coords-004",
            Region = "asia",
            Name = "Tokyo Store with Coords",
            Location = originalLocation,
            Description = "Testing persistence of all three fields"
        };
        
        // Act - Write entity to DynamoDB
        var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Read back the raw item from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        
        // Assert - Verify H3 index is persisted correctly
        var expectedIndex = H3Extensions.ToH3Index(originalLocation, 9);
        response.Item["location"].S.Should().Be(expectedIndex);
        
        // Assert - Verify latitude is persisted correctly
        double.Parse(response.Item["location_lat"].N).Should().Be(originalLocation.Latitude);
        
        // Assert - Verify longitude is persisted correctly
        double.Parse(response.Item["location_lon"].N).Should().Be(originalLocation.Longitude);
    }
    
    [Fact]
    public async Task ToDynamoDb_WithH3LocationAndCoordinateStorage_CoordinatesHaveFullPrecision()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        // Use a location with many decimal places to test full precision storage
        var preciseLocation = new GeoLocation(37.774929, -122.419415);
        var store = new H3StoreWithCoordsEntity
        {
            StoreId = "store-coords-005",
            Region = "west",
            Name = "Precise Location Store",
            Location = preciseLocation
        };
        
        // Act - Convert to DynamoDB item
        var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
        
        // Assert - Verify coordinates are stored with full precision (not rounded to H3 cell center)
        double.Parse(item["location_lat"].N).Should().Be(preciseLocation.Latitude, "latitude should have full precision");
        double.Parse(item["location_lon"].N).Should().Be(preciseLocation.Longitude, "longitude should have full precision");
        
        // Verify the stored coordinates are NOT the H3 cell center
        var h3Index = item["location"].S;
        var cellCenter = H3Extensions.FromH3Index(h3Index);
        
        // The stored coordinates should be the original precise values, not the cell center
        double.Parse(item["location_lat"].N).Should().NotBe(cellCenter.Latitude, 
            "stored latitude should be original value, not cell center");
        double.Parse(item["location_lon"].N).Should().NotBe(cellCenter.Longitude,
            "stored longitude should be original value, not cell center");
    }
    
    [Fact]
    public async Task WriteToDynamoDb_WithMultipleH3LocationsAndCoordinateStorage_AllFieldsCorrect()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var testLocations = new[]
        {
            new { Id = "store-coords-006", Region = "west", Name = "San Francisco", Location = new GeoLocation(37.7749, -122.4194) },
            new { Id = "store-coords-007", Region = "east", Name = "New York", Location = new GeoLocation(40.7128, -74.0060) },
            new { Id = "store-coords-008", Region = "europe", Name = "London", Location = new GeoLocation(51.5074, -0.1278) },
            new { Id = "store-coords-009", Region = "asia", Name = "Tokyo", Location = new GeoLocation(35.6762, 139.6503) },
            new { Id = "store-coords-010", Region = "oceania", Name = "Sydney", Location = new GeoLocation(-33.8688, 151.2093) }
        };
        
        // Act - Write all stores to DynamoDB
        foreach (var testData in testLocations)
        {
            var store = new H3StoreWithCoordsEntity
            {
                StoreId = testData.Id,
                Region = testData.Region,
                Name = testData.Name,
                Location = testData.Location
            };
            
            var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
            await DynamoDb.PutItemAsync(TableName, item);
        }
        
        // Read back all stores and verify all three fields
        foreach (var testData in testLocations)
        {
            var key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testData.Id },
                ["sk"] = new AttributeValue { S = testData.Region }
            };
            var response = await DynamoDb.GetItemAsync(TableName, key);
            
            // Assert - Verify all three attributes exist
            response.Item.Should().ContainKey("location", $"H3 index should exist for {testData.Name}");
            response.Item.Should().ContainKey("location_lat", $"latitude should exist for {testData.Name}");
            response.Item.Should().ContainKey("location_lon", $"longitude should exist for {testData.Name}");
            
            // Assert - Verify H3 index is correct
            var expectedIndex = H3Extensions.ToH3Index(testData.Location, 9);
            response.Item["location"].S.Should().Be(expectedIndex, $"H3 index should be correct for {testData.Name}");
            
            // Assert - Verify coordinates are exact
            double.Parse(response.Item["location_lat"].N).Should().Be(testData.Location.Latitude,
                $"latitude should be exact for {testData.Name}");
            double.Parse(response.Item["location_lon"].N).Should().Be(testData.Location.Longitude,
                $"longitude should be exact for {testData.Name}");
        }
    }
    
    #endregion
    
    #region 25.5 Test H3 multi-field deserialization with coordinates
    
    [Fact]
    public async Task FromDynamoDb_WithH3IndexAndCoordinates_ReconstructsFromCoordinates()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(37.7749, -122.4194); // San Francisco
        var h3Index = H3Extensions.ToH3Index(originalLocation, 9);
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-coords-011" },
            ["sk"] = new AttributeValue { S = "west" },
            ["name"] = new AttributeValue { S = "SF Store with Coords" },
            ["location"] = new AttributeValue { S = h3Index },
            ["location_lat"] = new AttributeValue { N = originalLocation.Latitude.ToString() },
            ["location_lon"] = new AttributeValue { N = originalLocation.Longitude.ToString() }
        };
        
        // Act - Convert from DynamoDB item
        var store = H3StoreWithCoordsEntity.FromDynamoDb<H3StoreWithCoordsEntity>(item);
        
        // Assert - Verify location is reconstructed from coordinates, not H3 index
        store.Location.Latitude.Should().Be(originalLocation.Latitude, "latitude should be exact from coordinates");
        store.Location.Longitude.Should().Be(originalLocation.Longitude, "longitude should be exact from coordinates");
        
        // Verify it's NOT the H3 cell center (which would be different)
        var cellCenter = H3Extensions.FromH3Index(h3Index);
        store.Location.Latitude.Should().NotBe(cellCenter.Latitude, 
            "deserialized latitude should be from coordinates, not cell center");
        store.Location.Longitude.Should().NotBe(cellCenter.Longitude,
            "deserialized longitude should be from coordinates, not cell center");
    }
    
    [Fact]
    public async Task FromDynamoDb_WithH3IndexAndCoordinates_PreservesExactPrecision()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        // Use a location with many decimal places to test precision preservation
        var preciseLocation = new GeoLocation(37.774929, -122.419415);
        var h3Index = H3Extensions.ToH3Index(preciseLocation, 9);
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-coords-012" },
            ["sk"] = new AttributeValue { S = "west" },
            ["name"] = new AttributeValue { S = "Precise Location Store" },
            ["location"] = new AttributeValue { S = h3Index },
            ["location_lat"] = new AttributeValue { N = preciseLocation.Latitude.ToString() },
            ["location_lon"] = new AttributeValue { N = preciseLocation.Longitude.ToString() }
        };
        
        // Act - Convert from DynamoDB item
        var store = H3StoreWithCoordsEntity.FromDynamoDb<H3StoreWithCoordsEntity>(item);
        
        // Assert - Verify exact precision is preserved
        store.Location.Latitude.Should().Be(preciseLocation.Latitude, "latitude precision should be preserved");
        store.Location.Longitude.Should().Be(preciseLocation.Longitude, "longitude precision should be preserved");
        
        // Verify the precision is better than H3 cell center precision
        var cellCenter = H3Extensions.FromH3Index(h3Index);
        var coordinatePrecision = Math.Abs(store.Location.Latitude - preciseLocation.Latitude);
        var cellCenterPrecision = Math.Abs(cellCenter.Latitude - preciseLocation.Latitude);
        
        coordinatePrecision.Should().BeLessThan(cellCenterPrecision,
            "coordinate storage should provide better precision than cell center");
    }
    
    [Fact]
    public async Task RoundTrip_WithH3IndexAndCoordinates_PreservesExactCoordinates()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(40.7128, -74.0060); // New York
        var originalStore = new H3StoreWithCoordsEntity
        {
            StoreId = "store-coords-013",
            Region = "east",
            Name = "NYC Store Round Trip",
            Location = originalLocation,
            Description = "Testing round-trip with coordinate storage"
        };
        
        // Act - Write to DynamoDB
        var item = H3StoreWithCoordsEntity.ToDynamoDb(originalStore);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Read back from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = originalStore.StoreId },
            ["sk"] = new AttributeValue { S = originalStore.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var retrievedStore = H3StoreWithCoordsEntity.FromDynamoDb<H3StoreWithCoordsEntity>(response.Item);
        
        // Assert - Verify exact coordinates are preserved (not H3 cell center)
        retrievedStore.Location.Latitude.Should().Be(originalLocation.Latitude, 
            "latitude should be exactly preserved");
        retrievedStore.Location.Longitude.Should().Be(originalLocation.Longitude,
            "longitude should be exactly preserved");
        
        // Verify it's NOT the H3 cell center
        var h3Index = H3Extensions.ToH3Index(originalLocation, 9);
        var cellCenter = H3Extensions.FromH3Index(h3Index);
        retrievedStore.Location.Latitude.Should().NotBe(cellCenter.Latitude,
            "retrieved latitude should be original value, not cell center");
        retrievedStore.Location.Longitude.Should().NotBe(cellCenter.Longitude,
            "retrieved longitude should be original value, not cell center");
    }
    
    [Fact]
    public async Task RoundTrip_WithMultipleH3LocationsAndCoordinates_PreservesAllExactCoordinates()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var testLocations = new[]
        {
            new { Id = "store-coords-014", Region = "west", Name = "San Francisco", Location = new GeoLocation(37.7749, -122.4194) },
            new { Id = "store-coords-015", Region = "east", Name = "New York", Location = new GeoLocation(40.7128, -74.0060) },
            new { Id = "store-coords-016", Region = "europe", Name = "London", Location = new GeoLocation(51.5074, -0.1278) },
            new { Id = "store-coords-017", Region = "asia", Name = "Tokyo", Location = new GeoLocation(35.6762, 139.6503) },
            new { Id = "store-coords-018", Region = "oceania", Name = "Sydney", Location = new GeoLocation(-33.8688, 151.2093) }
        };
        
        // Act - Write all stores to DynamoDB
        foreach (var testData in testLocations)
        {
            var store = new H3StoreWithCoordsEntity
            {
                StoreId = testData.Id,
                Region = testData.Region,
                Name = testData.Name,
                Location = testData.Location
            };
            
            var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
            await DynamoDb.PutItemAsync(TableName, item);
        }
        
        // Read back all stores and verify exact coordinates are preserved
        foreach (var testData in testLocations)
        {
            var key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testData.Id },
                ["sk"] = new AttributeValue { S = testData.Region }
            };
            var response = await DynamoDb.GetItemAsync(TableName, key);
            var retrievedStore = H3StoreWithCoordsEntity.FromDynamoDb<H3StoreWithCoordsEntity>(response.Item);
            
            // Assert - Verify exact coordinates are preserved
            retrievedStore.Location.Latitude.Should().Be(testData.Location.Latitude,
                $"latitude should be exactly preserved for {testData.Name}");
            retrievedStore.Location.Longitude.Should().Be(testData.Location.Longitude,
                $"longitude should be exactly preserved for {testData.Name}");
            
            // Verify it's NOT the H3 cell center
            var h3Index = H3Extensions.ToH3Index(testData.Location, 9);
            var cellCenter = H3Extensions.FromH3Index(h3Index);
            retrievedStore.Location.Latitude.Should().NotBe(cellCenter.Latitude,
                $"retrieved latitude should be original value for {testData.Name}, not cell center");
            retrievedStore.Location.Longitude.Should().NotBe(cellCenter.Longitude,
                $"retrieved longitude should be original value for {testData.Name}, not cell center");
        }
    }
    
    [Fact]
    public async Task RoundTrip_WithPreciseH3LocationAndCoordinates_PreservesFullPrecision()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        // Use locations with many decimal places to test full precision
        var preciseLocations = new[]
        {
            new { Id = "store-coords-019", Region = "west", Name = "Precise SF", Location = new GeoLocation(37.774929, -122.419415) },
            new { Id = "store-coords-020", Region = "east", Name = "Precise NYC", Location = new GeoLocation(40.712776, -74.005974) },
            new { Id = "store-coords-021", Region = "europe", Name = "Precise London", Location = new GeoLocation(51.507351, -0.127758) }
        };
        
        // Act - Write all stores to DynamoDB
        foreach (var testData in preciseLocations)
        {
            var store = new H3StoreWithCoordsEntity
            {
                StoreId = testData.Id,
                Region = testData.Region,
                Name = testData.Name,
                Location = testData.Location
            };
            
            var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
            await DynamoDb.PutItemAsync(TableName, item);
        }
        
        // Read back all stores and verify full precision is preserved
        foreach (var testData in preciseLocations)
        {
            var key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testData.Id },
                ["sk"] = new AttributeValue { S = testData.Region }
            };
            var response = await DynamoDb.GetItemAsync(TableName, key);
            var retrievedStore = H3StoreWithCoordsEntity.FromDynamoDb<H3StoreWithCoordsEntity>(response.Item);
            
            // Assert - Verify full precision is preserved (all decimal places)
            retrievedStore.Location.Latitude.Should().Be(testData.Location.Latitude,
                $"full latitude precision should be preserved for {testData.Name}");
            retrievedStore.Location.Longitude.Should().Be(testData.Location.Longitude,
                $"full longitude precision should be preserved for {testData.Name}");
        }
    }
    
    #endregion
    
    #region 25.6 Test H3 multi-field deserialization fallback
    
    [Fact]
    public async Task FromDynamoDb_WithOnlyH3Index_FallsBackToCellCenter()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(48.8566, 2.3522); // Paris
        var h3Index = H3Extensions.ToH3Index(originalLocation, 9);
        
        // Create item with only H3 index (no coordinate fields)
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-coords-022" },
            ["sk"] = new AttributeValue { S = "europe" },
            ["name"] = new AttributeValue { S = "Paris Store No Coords" },
            ["location"] = new AttributeValue { S = h3Index }
            // Note: no location_lat or location_lon fields
        };
        
        // Act - Convert from DynamoDB item
        var store = H3StoreWithCoordsEntity.FromDynamoDb<H3StoreWithCoordsEntity>(item);
        
        // Assert - Verify location falls back to H3 cell center
        var cellCenter = H3Extensions.FromH3Index(h3Index);
        store.Location.Latitude.Should().BeApproximately(cellCenter.Latitude, 0.0001,
            "should fall back to cell center latitude when coordinates are missing");
        store.Location.Longitude.Should().BeApproximately(cellCenter.Longitude, 0.0001,
            "should fall back to cell center longitude when coordinates are missing");
        
        // Verify it's approximately the original location (within H3 cell precision)
        store.Location.Latitude.Should().BeApproximately(originalLocation.Latitude, 0.01);
        store.Location.Longitude.Should().BeApproximately(originalLocation.Longitude, 0.01);
    }
    
    [Fact]
    public async Task RoundTrip_WriteWithCoordinates_ReadWithoutCoordinates_FallsBackCorrectly()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(35.6762, 139.6503); // Tokyo
        var store = new H3StoreWithCoordsEntity
        {
            StoreId = "store-coords-fallback-001",
            Region = "asia",
            Name = "Tokyo Fallback Test",
            Location = originalLocation,
            Description = "Testing fallback behavior"
        };
        
        // Act - Write entity with coordinates to DynamoDB
        var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Manually remove the coordinate fields to simulate missing coordinates
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        
        // Remove coordinate fields
        response.Item.Remove("location_lat");
        response.Item.Remove("location_lon");
        
        // Read entity back (should fall back to H3 index)
        var retrievedStore = H3StoreWithCoordsEntity.FromDynamoDb<H3StoreWithCoordsEntity>(response.Item);
        
        // Assert - Verify fallback to H3 cell center works correctly
        var h3Index = H3Extensions.ToH3Index(originalLocation, 9);
        var cellCenter = H3Extensions.FromH3Index(h3Index);
        
        retrievedStore.Location.Latitude.Should().BeApproximately(cellCenter.Latitude, 0.0001,
            "should fall back to cell center latitude");
        retrievedStore.Location.Longitude.Should().BeApproximately(cellCenter.Longitude, 0.0001,
            "should fall back to cell center longitude");
        
        // Verify it's approximately the original location (within H3 cell precision)
        retrievedStore.Location.Latitude.Should().BeApproximately(originalLocation.Latitude, 0.01);
        retrievedStore.Location.Longitude.Should().BeApproximately(originalLocation.Longitude, 0.01);
    }
    
    #endregion
    
    [Fact]
    public async Task RoundTrip_AtEquatorWithCoordinates_PreservesExactLocation()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var store = new H3StoreWithCoordsEntity
        {
            StoreId = "store-coords-023",
            Region = "equator",
            Name = "Equator Store with Coords",
            Location = new GeoLocation(0.0, 0.0), // Null Island
            Description = "Testing equator with coordinate storage"
        };
        
        // Act - Write to DynamoDB and read back
        var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var retrievedStore = H3StoreWithCoordsEntity.FromDynamoDb<H3StoreWithCoordsEntity>(response.Item);
        
        // Assert - Verify exact location is preserved
        retrievedStore.Location.Latitude.Should().Be(0.0, "latitude should be exactly 0.0");
        retrievedStore.Location.Longitude.Should().Be(0.0, "longitude should be exactly 0.0");
    }
    
    [Fact]
    public async Task RoundTrip_AtDateLineWithCoordinates_PreservesExactLocation()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var store = new H3StoreWithCoordsEntity
        {
            StoreId = "store-coords-024",
            Region = "pacific",
            Name = "Date Line Store with Coords",
            Location = new GeoLocation(0.0, 180.0), // International Date Line
            Description = "Testing date line with coordinate storage"
        };
        
        // Act - Write to DynamoDB and read back
        var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var retrievedStore = H3StoreWithCoordsEntity.FromDynamoDb<H3StoreWithCoordsEntity>(response.Item);
        
        // Assert - Verify exact location is preserved (note: 180 and -180 are the same)
        retrievedStore.Location.Latitude.Should().Be(0.0, "latitude should be exactly 0.0");
        Math.Abs(retrievedStore.Location.Longitude).Should().Be(180.0, "longitude should be exactly ±180.0");
    }
    
    [Fact]
    public async Task RoundTrip_NearNorthPoleWithCoordinates_PreservesExactLocation()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var store = new H3StoreWithCoordsEntity
        {
            StoreId = "store-coords-025",
            Region = "arctic",
            Name = "North Pole Store with Coords",
            Location = new GeoLocation(85.0, 0.0), // Near North Pole
            Description = "Testing near-pole with coordinate storage"
        };
        
        // Act - Write to DynamoDB and read back
        var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var retrievedStore = H3StoreWithCoordsEntity.FromDynamoDb<H3StoreWithCoordsEntity>(response.Item);
        
        // Assert - Verify exact location is preserved
        retrievedStore.Location.Latitude.Should().Be(85.0, "latitude should be exactly 85.0");
        retrievedStore.Location.Longitude.Should().Be(0.0, "longitude should be exactly 0.0");
    }
    
    [Fact]
    public async Task RoundTrip_NearSouthPoleWithCoordinates_PreservesExactLocation()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithCoordsEntity>();
        
        var store = new H3StoreWithCoordsEntity
        {
            StoreId = "store-coords-026",
            Region = "antarctic",
            Name = "South Pole Store with Coords",
            Location = new GeoLocation(-85.0, 0.0), // Near South Pole
            Description = "Testing near-pole with coordinate storage"
        };
        
        // Act - Write to DynamoDB and read back
        var item = H3StoreWithCoordsEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var retrievedStore = H3StoreWithCoordsEntity.FromDynamoDb<H3StoreWithCoordsEntity>(response.Item);
        
        // Assert - Verify exact location is preserved
        retrievedStore.Location.Latitude.Should().Be(-85.0, "latitude should be exactly -85.0");
        retrievedStore.Location.Longitude.Should().Be(0.0, "longitude should be exactly 0.0");
    }
    
    #region 25.7 Test H3 computed property serialization
    
    // NOTE: These tests are disabled because the source generator cannot deserialize into computed properties.
    // See Oproto.FluentDynamoDb.SourceGenerator/KNOWN_LIMITATIONS.md for details.
    // Tests are preserved for when the limitation is resolved.
    
#if FALSE // TODO: Enable when source generator supports computed properties
    
    [Fact]
    public async Task WriteToDynamoDb_WithH3ComputedProperties_CreatesThreeAttributes()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithComputedPropsEntity>();
        
        var store = new H3StoreWithComputedPropsEntity
        {
            StoreId = "store-computed-001",
            Region = "west",
            Name = "SF Store with Computed Props",
            Location = new GeoLocation(37.7749, -122.4194), // San Francisco
            Description = "Testing computed property serialization"
        };
        
        // Act - Write entity to DynamoDB
        var item = H3StoreWithComputedPropsEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Read back the raw item from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        
        // Assert - Verify three attributes are created: H3 index, latitude, longitude
        response.Item.Should().ContainKey("location", "H3 index attribute should exist");
        response.Item.Should().ContainKey("lat", "latitude attribute should exist");
        response.Item.Should().ContainKey("lon", "longitude attribute should exist");
        
        // Verify we have exactly these three location-related attributes (plus pk, sk, name, description)
        response.Item.Keys.Should().Contain(new[] { "pk", "sk", "name", "location", "lat", "lon", "description" });
    }
    
    [Fact]
    public async Task ToDynamoDb_WithH3ComputedProperties_SerializesCorrectly()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithComputedPropsEntity>();
        
        var originalLocation = new GeoLocation(40.7128, -74.0060); // New York
        var store = new H3StoreWithComputedPropsEntity
        {
            StoreId = "store-computed-002",
            Region = "east",
            Name = "NYC Store with Computed Props",
            Location = originalLocation,
            Description = "Testing computed property values"
        };
        
        // Act - Convert to DynamoDB item
        var item = H3StoreWithComputedPropsEntity.ToDynamoDb(store);
        
        // Assert - Verify H3 index field has correct value
        item["location"].S.Should().NotBeNullOrEmpty("H3 index should be stored");
        item["location"].S.Should().MatchRegex("^[0-9a-f]{15}$", "H3 index should be valid 15-character hex");
        
        var expectedIndex = H3Extensions.ToH3Index(originalLocation, 9);
        item["location"].S.Should().Be(expectedIndex, "H3 index should match expected value");
        
        // Assert - Verify computed latitude property is serialized correctly
        item["lat"].N.Should().NotBeNullOrEmpty("latitude should be stored as number");
        double.Parse(item["lat"].N).Should().Be(originalLocation.Latitude, "latitude should match exactly");
        
        // Assert - Verify computed longitude property is serialized correctly
        item["lon"].N.Should().NotBeNullOrEmpty("longitude should be stored as number");
        double.Parse(item["lon"].N).Should().Be(originalLocation.Longitude, "longitude should match exactly");
    }
    
    [Fact]
    public async Task WriteToDynamoDb_WithH3ComputedProperties_AllFieldsPersistedCorrectly()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithComputedPropsEntity>();
        
        var originalLocation = new GeoLocation(51.5074, -0.1278); // London
        var store = new H3StoreWithComputedPropsEntity
        {
            StoreId = "store-computed-003",
            Region = "europe",
            Name = "London Store with Computed Props",
            Location = originalLocation,
            Description = "Testing persistence of computed properties"
        };
        
        // Act - Write entity to DynamoDB
        var item = H3StoreWithComputedPropsEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Read back the raw item from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        
        // Assert - Verify H3 index is persisted correctly
        var expectedIndex = H3Extensions.ToH3Index(originalLocation, 9);
        response.Item["location"].S.Should().Be(expectedIndex);
        
        // Assert - Verify computed latitude is persisted correctly
        double.Parse(response.Item["lat"].N).Should().Be(originalLocation.Latitude);
        
        // Assert - Verify computed longitude is persisted correctly
        double.Parse(response.Item["lon"].N).Should().Be(originalLocation.Longitude);
    }
    
    [Fact]
    public async Task FromDynamoDb_WithH3ComputedProperties_ReconstructsFromCoordinates()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithComputedPropsEntity>();
        
        var originalLocation = new GeoLocation(35.6762, 139.6503); // Tokyo
        var h3Index = H3Extensions.ToH3Index(originalLocation, 9);
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-computed-004" },
            ["sk"] = new AttributeValue { S = "asia" },
            ["name"] = new AttributeValue { S = "Tokyo Store with Computed Props" },
            ["location"] = new AttributeValue { S = h3Index },
            ["lat"] = new AttributeValue { N = originalLocation.Latitude.ToString() },
            ["lon"] = new AttributeValue { N = originalLocation.Longitude.ToString() }
        };
        
        // Act - Convert from DynamoDB item
        var store = H3StoreWithComputedPropsEntity.FromDynamoDb<H3StoreWithComputedPropsEntity>(item);
        
        // Assert - Verify location is reconstructed from coordinates, not H3 index
        store.Location.Latitude.Should().Be(originalLocation.Latitude, "latitude should be exact from coordinates");
        store.Location.Longitude.Should().Be(originalLocation.Longitude, "longitude should be exact from coordinates");
        
        // Verify computed properties return the correct values
        store.Latitude.Should().Be(originalLocation.Latitude, "computed Latitude property should return correct value");
        store.Longitude.Should().Be(originalLocation.Longitude, "computed Longitude property should return correct value");
        
        // Verify it's NOT the H3 cell center (which would be different)
        var cellCenter = H3Extensions.FromH3Index(h3Index);
        store.Location.Latitude.Should().NotBe(cellCenter.Latitude, 
            "deserialized latitude should be from coordinates, not cell center");
        store.Location.Longitude.Should().NotBe(cellCenter.Longitude,
            "deserialized longitude should be from coordinates, not cell center");
    }
    
    [Fact]
    public async Task RoundTrip_WithH3ComputedProperties_PreservesExactCoordinates()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithComputedPropsEntity>();
        
        var originalLocation = new GeoLocation(48.8566, 2.3522); // Paris
        var originalStore = new H3StoreWithComputedPropsEntity
        {
            StoreId = "store-computed-005",
            Region = "europe",
            Name = "Paris Store Round Trip",
            Location = originalLocation,
            Description = "Testing round-trip with computed properties"
        };
        
        // Act - Write to DynamoDB
        var item = H3StoreWithComputedPropsEntity.ToDynamoDb(originalStore);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Read back from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = originalStore.StoreId },
            ["sk"] = new AttributeValue { S = originalStore.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var retrievedStore = H3StoreWithComputedPropsEntity.FromDynamoDb<H3StoreWithComputedPropsEntity>(response.Item);
        
        // Assert - Verify exact coordinates are preserved (not H3 cell center)
        retrievedStore.Location.Latitude.Should().Be(originalLocation.Latitude, 
            "latitude should be exactly preserved");
        retrievedStore.Location.Longitude.Should().Be(originalLocation.Longitude,
            "longitude should be exactly preserved");
        
        // Verify computed properties return the correct values
        retrievedStore.Latitude.Should().Be(originalLocation.Latitude, 
            "computed Latitude property should return correct value");
        retrievedStore.Longitude.Should().Be(originalLocation.Longitude,
            "computed Longitude property should return correct value");
        
        // Verify it's NOT the H3 cell center
        var h3Index = H3Extensions.ToH3Index(originalLocation, 9);
        var cellCenter = H3Extensions.FromH3Index(h3Index);
        retrievedStore.Location.Latitude.Should().NotBe(cellCenter.Latitude,
            "retrieved latitude should be original value, not cell center");
        retrievedStore.Location.Longitude.Should().NotBe(cellCenter.Longitude,
            "retrieved longitude should be original value, not cell center");
    }
    
    [Fact]
    public async Task RoundTrip_WithMultipleH3ComputedProperties_PreservesAllExactCoordinates()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithComputedPropsEntity>();
        
        var testLocations = new[]
        {
            new { Id = "store-computed-006", Region = "west", Name = "San Francisco", Location = new GeoLocation(37.7749, -122.4194) },
            new { Id = "store-computed-007", Region = "east", Name = "New York", Location = new GeoLocation(40.7128, -74.0060) },
            new { Id = "store-computed-008", Region = "europe", Name = "London", Location = new GeoLocation(51.5074, -0.1278) },
            new { Id = "store-computed-009", Region = "asia", Name = "Tokyo", Location = new GeoLocation(35.6762, 139.6503) },
            new { Id = "store-computed-010", Region = "oceania", Name = "Sydney", Location = new GeoLocation(-33.8688, 151.2093) }
        };
        
        // Act - Write all stores to DynamoDB
        foreach (var testData in testLocations)
        {
            var store = new H3StoreWithComputedPropsEntity
            {
                StoreId = testData.Id,
                Region = testData.Region,
                Name = testData.Name,
                Location = testData.Location
            };
            
            var item = H3StoreWithComputedPropsEntity.ToDynamoDb(store);
            await DynamoDb.PutItemAsync(TableName, item);
        }
        
        // Read back all stores and verify exact coordinates are preserved
        foreach (var testData in testLocations)
        {
            var key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testData.Id },
                ["sk"] = new AttributeValue { S = testData.Region }
            };
            var response = await DynamoDb.GetItemAsync(TableName, key);
            var retrievedStore = H3StoreWithComputedPropsEntity.FromDynamoDb<H3StoreWithComputedPropsEntity>(response.Item);
            
            // Assert - Verify exact coordinates are preserved
            retrievedStore.Location.Latitude.Should().Be(testData.Location.Latitude,
                $"latitude should be exactly preserved for {testData.Name}");
            retrievedStore.Location.Longitude.Should().Be(testData.Location.Longitude,
                $"longitude should be exactly preserved for {testData.Name}");
            
            // Verify computed properties return the correct values
            retrievedStore.Latitude.Should().Be(testData.Location.Latitude,
                $"computed Latitude property should return correct value for {testData.Name}");
            retrievedStore.Longitude.Should().Be(testData.Location.Longitude,
                $"computed Longitude property should return correct value for {testData.Name}");
            
            // Verify it's NOT the H3 cell center
            var h3Index = H3Extensions.ToH3Index(testData.Location, 9);
            var cellCenter = H3Extensions.FromH3Index(h3Index);
            retrievedStore.Location.Latitude.Should().NotBe(cellCenter.Latitude,
                $"retrieved latitude should be original value for {testData.Name}, not cell center");
            retrievedStore.Location.Longitude.Should().NotBe(cellCenter.Longitude,
                $"retrieved longitude should be original value for {testData.Name}, not cell center");
        }
    }
    
    [Fact]
    public async Task RoundTrip_WithPreciseH3ComputedProperties_PreservesFullPrecision()
    {
        // Arrange
        await CreateTableAsync<H3StoreWithComputedPropsEntity>();
        
        // Use locations with many decimal places to test full precision
        var preciseLocations = new[]
        {
            new { Id = "store-computed-011", Region = "west", Name = "Precise SF", Location = new GeoLocation(37.774929, -122.419415) },
            new { Id = "store-computed-012", Region = "east", Name = "Precise NYC", Location = new GeoLocation(40.712776, -74.005974) },
            new { Id = "store-computed-013", Region = "europe", Name = "Precise London", Location = new GeoLocation(51.507351, -0.127758) }
        };
        
        // Act - Write all stores to DynamoDB
        foreach (var testData in preciseLocations)
        {
            var store = new H3StoreWithComputedPropsEntity
            {
                StoreId = testData.Id,
                Region = testData.Region,
                Name = testData.Name,
                Location = testData.Location
            };
            
            var item = H3StoreWithComputedPropsEntity.ToDynamoDb(store);
            await DynamoDb.PutItemAsync(TableName, item);
        }
        
        // Read back all stores and verify full precision is preserved
        foreach (var testData in preciseLocations)
        {
            var key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = testData.Id },
                ["sk"] = new AttributeValue { S = testData.Region }
            };
            var response = await DynamoDb.GetItemAsync(TableName, key);
            var retrievedStore = H3StoreWithComputedPropsEntity.FromDynamoDb<H3StoreWithComputedPropsEntity>(response.Item);
            
            // Assert - Verify full precision is preserved (all decimal places)
            retrievedStore.Location.Latitude.Should().Be(testData.Location.Latitude,
                $"full latitude precision should be preserved for {testData.Name}");
            retrievedStore.Location.Longitude.Should().Be(testData.Location.Longitude,
                $"full longitude precision should be preserved for {testData.Name}");
            
            // Verify computed properties return the correct values with full precision
            retrievedStore.Latitude.Should().Be(testData.Location.Latitude,
                $"computed Latitude property should preserve full precision for {testData.Name}");
            retrievedStore.Longitude.Should().Be(testData.Location.Longitude,
                $"computed Longitude property should preserve full precision for {testData.Name}");
        }
    }
    
#endif // FALSE - Computed properties not yet supported
    
    #endregion
}
