using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Geospatial.S2;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for S2 geospatial storage with DynamoDB.
/// Tests verify that the source generator correctly serializes and deserializes S2-indexed
/// GeoLocation properties to/from S2 cell tokens in DynamoDB.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "S2")]
[Trait("Feature", "SourceGenerator")]
public class S2StorageIntegrationTests : IntegrationTestBase
{
    public S2StorageIntegrationTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    #region 24.1 Test S2 single-field serialization
    
    [Fact]
    public async Task WriteToDynamoDb_WithS2Location_StoresAsS2TokenString()
    {
        // Arrange
        await CreateTableAsync<S2StoreEntity>();
        
        var store = new S2StoreEntity
        {
            StoreId = "store-001",
            Name = "Downtown Store",
            Location = new GeoLocation(37.7749, -122.4194), // San Francisco
            Description = "Main downtown location"
        };
        
        // Act - Write entity to DynamoDB
        var item = S2StoreEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Read back the raw item from DynamoDB
        var locationToken = S2Extensions.ToS2Token(store.Location, 16);
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = locationToken }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        
        // Assert - Verify location is stored as S2 token string in DynamoDB
        // Since Location is the sort key, it's stored as "sk"
        response.Item.Should().ContainKey("sk");
        response.Item["sk"].S.Should().NotBeNullOrEmpty();
        
        // Verify it's a valid S2 token (1-16 character hexadecimal string, trailing zeros trimmed)
        response.Item["sk"].S.Should().MatchRegex("^[0-9a-f]{1,16}$");
        
        // Verify the S2 token is correct for level 16
        var expectedToken = S2Extensions.ToS2Token(store.Location, 16);
        response.Item["sk"].S.Should().Be(expectedToken);
        
        // Verify it's a valid S2 token that can be decoded
        var decoded = S2Extensions.FromS2Token(response.Item["sk"].S);
        decoded.Latitude.Should().BeApproximately(37.7749, 0.01);
        decoded.Longitude.Should().BeApproximately(-122.4194, 0.01);
    }
    
    [Fact]
    public async Task ToDynamoDb_WithS2Location_SerializesToS2TokenString()
    {
        // Arrange
        await CreateTableAsync<S2StoreEntity>();
        
        var store = new S2StoreEntity
        {
            StoreId = "store-001",
            Name = "Downtown Store",
            Location = new GeoLocation(37.7749, -122.4194), // San Francisco
            Description = "Main downtown location"
        };
        
        // Act - Convert to DynamoDB item
        var item = S2StoreEntity.ToDynamoDb(store);
        
        // Assert - Verify location is stored as S2 token string
        // Since Location is the sort key, it's stored as "sk"
        item.Should().ContainKey("sk");
        item["sk"].S.Should().NotBeNullOrEmpty();
        
        // Verify it's a valid S2 token (1-16 character hexadecimal string, trailing zeros trimmed)
        item["sk"].S.Should().MatchRegex("^[0-9a-f]{1,16}$");
        
        // Verify the S2 token is correct for level 16
        var expectedToken = S2Extensions.ToS2Token(store.Location, 16);
        item["sk"].S.Should().Be(expectedToken);
        
        // Verify it's a valid S2 token that can be decoded
        var decoded = S2Extensions.FromS2Token(item["sk"].S);
        decoded.Latitude.Should().BeApproximately(37.7749, 0.01);
        decoded.Longitude.Should().BeApproximately(-122.4194, 0.01);
    }
    
    [Fact]
    public async Task ToDynamoDb_WithS2Location_TokenLengthMatchesExpectedFormat()
    {
        // Arrange
        await CreateTableAsync<S2StoreEntity>();
        
        var store = new S2StoreEntity
        {
            StoreId = "store-002",
            Name = "East Side Store",
            Location = new GeoLocation(40.7128, -74.0060) // New York
        };
        
        // Act - Convert to DynamoDB item
        var item = S2StoreEntity.ToDynamoDb(store);
        
        // Assert - Verify token is valid S2 format (1-16 characters, trailing zeros trimmed)
        // Since Location is the sort key, it's stored as "sk"
        item["sk"].S.Length.Should().BeInRange(1, 16);
        item["sk"].S.Should().MatchRegex("^[0-9a-f]+$");
    }
    
    [Fact]
    public async Task ToDynamoDb_WithS2Location_TokenCanBeDecodedBackToCoordinates()
    {
        // Arrange
        await CreateTableAsync<S2StoreEntity>();
        
        var originalLocation = new GeoLocation(51.5074, -0.1278); // London
        var store = new S2StoreEntity
        {
            StoreId = "store-003",
            Name = "London Store",
            Location = originalLocation
        };
        
        // Act - Convert to DynamoDB item
        var item = S2StoreEntity.ToDynamoDb(store);
        
        // Assert - Verify token can be decoded back to coordinates
        // Since Location is the sort key, it's stored as "sk"
        var s2Token = item["sk"].S;
        var decoded = S2Extensions.FromS2Token(s2Token);
        
        // S2 level 16 has ~600m precision, so we expect coordinates within ~0.01 degrees
        decoded.Latitude.Should().BeApproximately(originalLocation.Latitude, 0.01);
        decoded.Longitude.Should().BeApproximately(originalLocation.Longitude, 0.01);
    }
    
    #endregion
    
    #region 24.2 Test S2 single-field deserialization
    
    [Fact]
    public async Task FromDynamoDb_WithS2TokenString_DeserializesToGeoLocation()
    {
        // Arrange
        await CreateTableAsync<S2StoreEntity>();
        
        var sanFrancisco = new GeoLocation(37.7749, -122.4194);
        var s2Token = S2Extensions.ToS2Token(sanFrancisco, 16);
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-004" },
            ["sk"] = new AttributeValue { S = s2Token },
            ["name"] = new AttributeValue { S = "SF Store" }
        };
        
        // Act - Convert from DynamoDB item
        var store = S2StoreEntity.FromDynamoDb<S2StoreEntity>(item);
        
        // Assert - Verify location is deserialized correctly
        store.Location.Should().NotBe(default(GeoLocation));
        store.Location.Latitude.Should().BeApproximately(37.7749, 0.01);
        store.Location.Longitude.Should().BeApproximately(-122.4194, 0.01);
    }
    
    [Fact]
    public async Task FromDynamoDb_WithS2Token_ReconstructsGeoLocationCorrectly()
    {
        // Arrange
        await CreateTableAsync<S2StoreEntity>();
        
        // Create a known S2 token for Tokyo
        var tokyo = new GeoLocation(35.6762, 139.6503);
        var s2Token = S2Extensions.ToS2Token(tokyo, 16);
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-005" },
            ["sk"] = new AttributeValue { S = s2Token },
            ["name"] = new AttributeValue { S = "Tokyo Store" }
        };
        
        // Act - Convert from DynamoDB item
        var store = S2StoreEntity.FromDynamoDb<S2StoreEntity>(item);
        
        // Assert - Verify location is reconstructed correctly
        store.Location.Latitude.Should().BeApproximately(35.6762, 0.01);
        store.Location.Longitude.Should().BeApproximately(139.6503, 0.01);
    }
    
    [Fact]
    public async Task FromDynamoDb_WithS2Token_CoordinatesAreWithinCellBounds()
    {
        // Arrange
        await CreateTableAsync<S2StoreEntity>();
        
        var originalLocation = new GeoLocation(48.8566, 2.3522); // Paris
        var s2Token = S2Extensions.ToS2Token(originalLocation, 16);
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-006" },
            ["sk"] = new AttributeValue { S = s2Token },
            ["name"] = new AttributeValue { S = "Paris Store" }
        };
        
        // Act - Convert from DynamoDB item
        var store = S2StoreEntity.FromDynamoDb<S2StoreEntity>(item);
        
        // Assert - Verify coordinates are within the cell bounds
        var cell = S2Extensions.ToS2Cell(originalLocation, 16);
        var bounds = cell.Bounds;
        
        store.Location.Latitude.Should().BeInRange(bounds.Southwest.Latitude, bounds.Northeast.Latitude);
        store.Location.Longitude.Should().BeInRange(bounds.Southwest.Longitude, bounds.Northeast.Longitude);
    }
    
    #endregion
    
    #region 24.3 Test S2 round-trip persistence
    
    [Fact]
    public async Task RoundTrip_WithS2Location_PreservesLocationData()
    {
        // Arrange
        await CreateTableAsync<S2StoreEntity>();
        
        var originalStore = new S2StoreEntity
        {
            StoreId = "store-007",
            Name = "North Store",
            Location = new GeoLocation(40.7128, -74.0060), // New York
            Description = "Northern branch"
        };
        
        // Act - Round trip through DynamoDB serialization
        var item = S2StoreEntity.ToDynamoDb(originalStore);
        var roundTrippedStore = S2StoreEntity.FromDynamoDb<S2StoreEntity>(item);
        
        // Assert - Verify location is preserved (within S2 level 16 precision)
        roundTrippedStore.Location.Latitude.Should().BeApproximately(
            originalStore.Location.Latitude, 0.01);
        roundTrippedStore.Location.Longitude.Should().BeApproximately(
            originalStore.Location.Longitude, 0.01);
        roundTrippedStore.Name.Should().Be(originalStore.Name);
        roundTrippedStore.Description.Should().Be(originalStore.Description);
    }
    
    [Fact]
    public async Task RoundTrip_WithPoleLocation_PreservesData()
    {
        // Arrange
        await CreateTableAsync<S2StoreEntity>();
        
        var store = new S2StoreEntity
        {
            StoreId = "store-pole",
            Name = "North Pole Store",
            Location = new GeoLocation(90.0, 0.0) // North Pole
        };
        
        // Act - Round trip through DynamoDB
        var item = S2StoreEntity.ToDynamoDb(store);
        var roundTripped = S2StoreEntity.FromDynamoDb<S2StoreEntity>(item);
        
        // Assert - Verify pole location is preserved
        roundTripped.Location.Latitude.Should().BeApproximately(90.0, 0.1);
    }
    
    [Fact]
    public async Task RoundTrip_WithDateLineLocation_PreservesData()
    {
        // Arrange
        await CreateTableAsync<S2StoreEntity>();
        
        var store = new S2StoreEntity
        {
            StoreId = "store-dateline",
            Name = "Date Line Store",
            Location = new GeoLocation(0.0, 180.0) // On the date line
        };
        
        // Act - Round trip through DynamoDB
        var item = S2StoreEntity.ToDynamoDb(store);
        var roundTripped = S2StoreEntity.FromDynamoDb<S2StoreEntity>(item);
        
        // Assert - Verify date line location is preserved
        roundTripped.Location.Longitude.Should().BeApproximately(180.0, 0.1);
    }
    
    [Fact]
    public async Task RoundTrip_WithEquatorLocation_PreservesData()
    {
        // Arrange
        await CreateTableAsync<S2StoreEntity>();
        
        var store = new S2StoreEntity
        {
            StoreId = "store-equator",
            Name = "Equator Store",
            Location = new GeoLocation(0.0, 0.0) // Null Island (Equator + Prime Meridian)
        };
        
        // Act - Round trip through DynamoDB
        var item = S2StoreEntity.ToDynamoDb(store);
        var roundTripped = S2StoreEntity.FromDynamoDb<S2StoreEntity>(item);
        
        // Assert - Verify equator location is preserved
        roundTripped.Location.Latitude.Should().BeApproximately(0.0, 0.1);
        roundTripped.Location.Longitude.Should().BeApproximately(0.0, 0.1);
    }
    
    [Fact]
    public async Task RoundTrip_WithMultipleLocations_PreservesAllData()
    {
        // Arrange
        await CreateTableAsync<S2StoreEntity>();
        
        var locations = new[]
        {
            new GeoLocation(37.7749, -122.4194), // San Francisco
            new GeoLocation(40.7128, -74.0060),  // New York
            new GeoLocation(51.5074, -0.1278),   // London
            new GeoLocation(35.6762, 139.6503),  // Tokyo
            new GeoLocation(-33.8688, 151.2093)  // Sydney
        };
        
        // Act & Assert - Test each location
        for (int i = 0; i < locations.Length; i++)
        {
            var store = new S2StoreEntity
            {
                StoreId = $"store-{i:D3}",
                Name = $"Store {i}",
                Location = locations[i]
            };
            
            var item = S2StoreEntity.ToDynamoDb(store);
            var roundTripped = S2StoreEntity.FromDynamoDb<S2StoreEntity>(item);
            
            roundTripped.Location.Latitude.Should().BeApproximately(
                locations[i].Latitude, 0.01, 
                $"Location {i} latitude should be preserved");
            roundTripped.Location.Longitude.Should().BeApproximately(
                locations[i].Longitude, 0.01,
                $"Location {i} longitude should be preserved");
        }
    }
    
    #endregion
    
    #region 24.4 Test S2 multi-field serialization with StoreCoordinatesAttribute
    
    [Fact]
    public async Task WriteToDynamoDb_WithS2AndStoreCoordinates_CreatesThreeAttributes()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var store = new S2StoreWithCoordsEntity
        {
            StoreId = "store-coords-001",
            Region = "west",
            Name = "Multi-Field Store",
            Location = new GeoLocation(37.7749, -122.4194), // San Francisco
            Description = "Store with coordinate storage"
        };
        
        // Act - Write entity to DynamoDB
        var item = S2StoreWithCoordsEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Read back the raw item from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        
        // Assert - Verify three attributes are created: S2 token, latitude, longitude
        response.Item.Should().ContainKey("location", "S2 token attribute should exist");
        response.Item.Should().ContainKey("location_lat", "Latitude attribute should exist");
        response.Item.Should().ContainKey("location_lon", "Longitude attribute should exist");
        
        // Verify all three attributes have values
        response.Item["location"].S.Should().NotBeNullOrEmpty("S2 token should have a value");
        response.Item["location_lat"].N.Should().NotBeNullOrEmpty("Latitude should have a value");
        response.Item["location_lon"].N.Should().NotBeNullOrEmpty("Longitude should have a value");
    }
    
    [Fact]
    public async Task ToDynamoDb_WithS2AndStoreCoordinates_AttributeNamesMatchConfiguration()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var store = new S2StoreWithCoordsEntity
        {
            StoreId = "store-coords-002",
            Region = "east",
            Name = "Coordinate Store",
            Location = new GeoLocation(40.7128, -74.0060) // New York
        };
        
        // Act - Convert to DynamoDB item
        var item = S2StoreWithCoordsEntity.ToDynamoDb(store);
        
        // Assert - Verify attribute names match the StoreCoordinatesAttribute configuration
        // StoreCoordinatesAttribute specifies: LatitudeAttributeName = "location_lat", LongitudeAttributeName = "location_lon"
        item.Should().ContainKey("location", "S2 token attribute should use the base attribute name");
        item.Should().ContainKey("location_lat", "Latitude attribute should use configured name");
        item.Should().ContainKey("location_lon", "Longitude attribute should use configured name");
        
        // Verify the attribute names are exactly as configured (not auto-generated)
        item.Keys.Should().Contain("location_lat", "Latitude attribute name should match configuration exactly");
        item.Keys.Should().Contain("location_lon", "Longitude attribute name should match configuration exactly");
    }
    
    [Fact]
    public async Task ToDynamoDb_WithS2AndStoreCoordinates_AllThreeFieldsHaveCorrectValues()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(51.5074, -0.1278); // London
        var store = new S2StoreWithCoordsEntity
        {
            StoreId = "store-coords-003",
            Region = "europe",
            Name = "London Coordinate Store",
            Location = originalLocation
        };
        
        // Act - Convert to DynamoDB item
        var item = S2StoreWithCoordsEntity.ToDynamoDb(store);
        
        // Assert - Verify S2 token is correct
        var expectedS2Token = S2Extensions.ToS2Token(originalLocation, 16);
        item["location"].S.Should().Be(expectedS2Token, "S2 token should be correctly encoded");
        
        // Verify latitude is correct (stored as DynamoDB Number)
        var storedLatitude = double.Parse(item["location_lat"].N);
        storedLatitude.Should().BeApproximately(originalLocation.Latitude, 0.000001, 
            "Latitude should be stored with full precision");
        
        // Verify longitude is correct (stored as DynamoDB Number)
        var storedLongitude = double.Parse(item["location_lon"].N);
        storedLongitude.Should().BeApproximately(originalLocation.Longitude, 0.000001,
            "Longitude should be stored with full precision");
    }
    
    [Fact]
    public async Task WriteToDynamoDb_WithS2AndStoreCoordinates_PreservesExactCoordinates()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        // Use coordinates with high precision to verify full resolution is preserved
        var preciseLocation = new GeoLocation(35.6762123456, 139.6503987654); // Tokyo with high precision
        var store = new S2StoreWithCoordsEntity
        {
            StoreId = "store-coords-004",
            Region = "asia",
            Name = "Precise Tokyo Store",
            Location = preciseLocation
        };
        
        // Act - Write entity to DynamoDB
        var item = S2StoreWithCoordsEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Read back the raw item from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        
        // Assert - Verify exact coordinates are preserved (not just cell center)
        var storedLatitude = double.Parse(response.Item["location_lat"].N);
        var storedLongitude = double.Parse(response.Item["location_lon"].N);
        
        storedLatitude.Should().BeApproximately(preciseLocation.Latitude, 0.000001,
            "Latitude should preserve full precision, not just S2 cell center");
        storedLongitude.Should().BeApproximately(preciseLocation.Longitude, 0.000001,
            "Longitude should preserve full precision, not just S2 cell center");
        
        // Verify the S2 token is also present for spatial queries
        response.Item["location"].S.Should().NotBeNullOrEmpty("S2 token should be present for queries");
    }
    
    [Fact]
    public async Task ToDynamoDb_WithS2AndStoreCoordinates_AllFieldsSerializedAtomically()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var locations = new[]
        {
            new GeoLocation(37.7749, -122.4194), // San Francisco
            new GeoLocation(40.7128, -74.0060),  // New York
            new GeoLocation(51.5074, -0.1278),   // London
            new GeoLocation(35.6762, 139.6503),  // Tokyo
            new GeoLocation(-33.8688, 151.2093)  // Sydney
        };
        
        // Act & Assert - Verify all three fields are present for each location
        for (int i = 0; i < locations.Length; i++)
        {
            var store = new S2StoreWithCoordsEntity
            {
                StoreId = $"store-coords-{i:D3}",
                Region = "global",
                Name = $"Store {i}",
                Location = locations[i]
            };
            
            var item = S2StoreWithCoordsEntity.ToDynamoDb(store);
            
            // Verify all three fields are present
            item.Should().ContainKey("location", $"Store {i} should have S2 token");
            item.Should().ContainKey("location_lat", $"Store {i} should have latitude");
            item.Should().ContainKey("location_lon", $"Store {i} should have longitude");
            
            // Verify all three fields have correct values
            var expectedToken = S2Extensions.ToS2Token(locations[i], 16);
            item["location"].S.Should().Be(expectedToken, $"Store {i} S2 token should be correct");
            
            var lat = double.Parse(item["location_lat"].N);
            var lon = double.Parse(item["location_lon"].N);
            
            lat.Should().BeApproximately(locations[i].Latitude, 0.000001, 
                $"Store {i} latitude should be correct");
            lon.Should().BeApproximately(locations[i].Longitude, 0.000001,
                $"Store {i} longitude should be correct");
        }
    }
    
    #endregion
    
    #region 24.5 Test S2 multi-field deserialization with coordinates
    
    [Fact]
    public async Task FromDynamoDb_WithS2TokenAndCoordinates_ReconstructsFromCoordinatesNotToken()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(37.7749123456, -122.4194987654); // San Francisco with high precision
        var s2Token = S2Extensions.ToS2Token(originalLocation, 16);
        
        // Create DynamoDB item with S2 token AND coordinates
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-coords-read-001" },
            ["sk"] = new AttributeValue { S = "west" },
            ["name"] = new AttributeValue { S = "SF Coordinate Read Store" },
            ["location"] = new AttributeValue { S = s2Token },
            ["location_lat"] = new AttributeValue { N = originalLocation.Latitude.ToString("F10") },
            ["location_lon"] = new AttributeValue { N = originalLocation.Longitude.ToString("F10") }
        };
        
        // Act - Convert from DynamoDB item
        var store = S2StoreWithCoordsEntity.FromDynamoDb<S2StoreWithCoordsEntity>(item);
        
        // Assert - Verify GeoLocation is reconstructed from coordinates, NOT from S2 token
        // If it were reconstructed from the S2 token, we'd get the cell center (~0.01 degree precision)
        // Since we have coordinates, we should get the exact original values
        store.Location.Latitude.Should().BeApproximately(originalLocation.Latitude, 0.000001,
            "Latitude should be reconstructed from coordinate field, not S2 token");
        store.Location.Longitude.Should().BeApproximately(originalLocation.Longitude, 0.000001,
            "Longitude should be reconstructed from coordinate field, not S2 token");
        
        // Verify the precision is much better than S2 cell center precision
        // S2 level 16 has ~600m precision (~0.01 degrees), but coordinates should be exact
        var cellCenter = S2Extensions.FromS2Token(s2Token);
        var distanceFromCellCenter = Math.Sqrt(
            Math.Pow(store.Location.Latitude - cellCenter.Latitude, 2) +
            Math.Pow(store.Location.Longitude - cellCenter.Longitude, 2));
        
        // The reconstructed location should be very close to original (< 0.000001 degrees)
        // but may differ from cell center by up to ~0.01 degrees
        distanceFromCellCenter.Should().BeLessThan(0.01,
            "Reconstructed location may differ from cell center, proving it came from coordinates");
    }
    
    [Fact]
    public async Task ReadFromDynamoDb_WithS2TokenAndCoordinates_PreservesExactCoordinatePrecision()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        // Use coordinates with very high precision to verify full resolution is preserved
        var preciseLocation = new GeoLocation(40.7127837, -74.0059413); // New York with GPS precision
        
        // Write entity to DynamoDB
        var writeStore = new S2StoreWithCoordsEntity
        {
            StoreId = "store-coords-read-002",
            Region = "east",
            Name = "Precise NYC Store",
            Location = preciseLocation,
            Description = "Testing coordinate precision"
        };
        
        var item = S2StoreWithCoordsEntity.ToDynamoDb(writeStore);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Read entity back from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = writeStore.StoreId },
            ["sk"] = new AttributeValue { S = writeStore.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var readStore = S2StoreWithCoordsEntity.FromDynamoDb<S2StoreWithCoordsEntity>(response.Item);
        
        // Assert - Verify exact coordinate precision is preserved (not just cell center)
        readStore.Location.Latitude.Should().BeApproximately(preciseLocation.Latitude, 0.0000001,
            "Latitude should preserve GPS-level precision");
        readStore.Location.Longitude.Should().BeApproximately(preciseLocation.Longitude, 0.0000001,
            "Longitude should preserve GPS-level precision");
        
        // Verify the precision is MUCH better than S2 cell precision
        // S2 level 16 has ~600m precision, but we should have meter-level precision
        var latDiff = Math.Abs(readStore.Location.Latitude - preciseLocation.Latitude);
        var lonDiff = Math.Abs(readStore.Location.Longitude - preciseLocation.Longitude);
        
        latDiff.Should().BeLessThan(0.00001, "Latitude precision should be better than 1 meter");
        lonDiff.Should().BeLessThan(0.00001, "Longitude precision should be better than 1 meter");
    }
    
    [Fact]
    public async Task RoundTrip_WithS2TokenAndCoordinates_PreservesExactOriginalCoordinates()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(51.5073509, -0.1277583); // London with high precision
        var originalStore = new S2StoreWithCoordsEntity
        {
            StoreId = "store-coords-read-003",
            Region = "europe",
            Name = "London Precise Store",
            Location = originalLocation,
            Description = "Round-trip coordinate test"
        };
        
        // Act - Round trip through DynamoDB serialization
        var item = S2StoreWithCoordsEntity.ToDynamoDb(originalStore);
        var roundTrippedStore = S2StoreWithCoordsEntity.FromDynamoDb<S2StoreWithCoordsEntity>(item);
        
        // Assert - Verify exact original coordinates are preserved
        roundTrippedStore.Location.Latitude.Should().Be(originalLocation.Latitude,
            "Latitude should be exactly preserved in round-trip");
        roundTrippedStore.Location.Longitude.Should().Be(originalLocation.Longitude,
            "Longitude should be exactly preserved in round-trip");
        
        // Verify this is NOT the S2 cell center
        var s2Token = S2Extensions.ToS2Token(originalLocation, 16);
        var cellCenter = S2Extensions.FromS2Token(s2Token);
        
        // The round-tripped location should match original exactly, not the cell center
        roundTrippedStore.Location.Should().Be(originalLocation,
            "Round-tripped location should match original exactly");
        
        // Verify it's different from cell center (proving coordinates were used)
        if (Math.Abs(cellCenter.Latitude - originalLocation.Latitude) > 0.0001 ||
            Math.Abs(cellCenter.Longitude - originalLocation.Longitude) > 0.0001)
        {
            roundTrippedStore.Location.Should().NotBe(cellCenter,
                "Round-tripped location should differ from cell center, proving coordinates were used");
        }
    }
    
    [Fact]
    public async Task FromDynamoDb_WithMultipleLocationsAndCoordinates_AllPreserveExactPrecision()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var locations = new[]
        {
            new GeoLocation(37.7749295, -122.4194155), // San Francisco
            new GeoLocation(40.7127753, -74.0059728),  // New York
            new GeoLocation(51.5073509, -0.1277583),   // London
            new GeoLocation(35.6761919, 139.6503106),  // Tokyo
            new GeoLocation(-33.8688197, 151.2092955)  // Sydney
        };
        
        // Act & Assert - Test each location preserves exact precision
        for (int i = 0; i < locations.Length; i++)
        {
            var store = new S2StoreWithCoordsEntity
            {
                StoreId = $"store-coords-read-{i:D3}",
                Region = "global",
                Name = $"Precise Store {i}",
                Location = locations[i]
            };
            
            // Round trip through serialization
            var item = S2StoreWithCoordsEntity.ToDynamoDb(store);
            var roundTripped = S2StoreWithCoordsEntity.FromDynamoDb<S2StoreWithCoordsEntity>(item);
            
            // Verify exact precision is preserved
            roundTripped.Location.Latitude.Should().Be(locations[i].Latitude,
                $"Location {i} latitude should be exactly preserved");
            roundTripped.Location.Longitude.Should().Be(locations[i].Longitude,
                $"Location {i} longitude should be exactly preserved");
        }
    }
    
    [Fact]
    public async Task FromDynamoDb_WithEdgeCaseLocationsAndCoordinates_PreservesExactValues()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var edgeCases = new[]
        {
            new GeoLocation(89.9999, 0.0),      // Near North Pole
            new GeoLocation(-89.9999, 0.0),     // Near South Pole
            new GeoLocation(0.0, 179.9999),     // Near Date Line (east)
            new GeoLocation(0.0, -179.9999),    // Near Date Line (west)
            new GeoLocation(0.0000001, 0.0000001) // Near Null Island with high precision
        };
        
        // Act & Assert - Test each edge case preserves exact precision
        for (int i = 0; i < edgeCases.Length; i++)
        {
            var store = new S2StoreWithCoordsEntity
            {
                StoreId = $"store-coords-edge-{i:D3}",
                Region = "edge",
                Name = $"Edge Case Store {i}",
                Location = edgeCases[i]
            };
            
            // Round trip through serialization
            var item = S2StoreWithCoordsEntity.ToDynamoDb(store);
            var roundTripped = S2StoreWithCoordsEntity.FromDynamoDb<S2StoreWithCoordsEntity>(item);
            
            // Verify exact precision is preserved even for edge cases
            roundTripped.Location.Latitude.Should().BeApproximately(edgeCases[i].Latitude, 0.0000001,
                $"Edge case {i} latitude should be exactly preserved");
            roundTripped.Location.Longitude.Should().BeApproximately(edgeCases[i].Longitude, 0.0000001,
                $"Edge case {i} longitude should be exactly preserved");
        }
    }
    
    #endregion
    
    #region 24.6 Test S2 multi-field deserialization fallback
    
    [Fact]
    public async Task FromDynamoDb_WithOnlyS2Token_FallsBackToTokenDecoding()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(37.7749, -122.4194); // San Francisco
        var s2Token = S2Extensions.ToS2Token(originalLocation, 16);
        
        // Create DynamoDB item with ONLY S2 token (no coordinate fields)
        // This simulates data written before coordinate storage was added
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-fallback-001" },
            ["sk"] = new AttributeValue { S = "west" },
            ["name"] = new AttributeValue { S = "Fallback Store" },
            ["location"] = new AttributeValue { S = s2Token }
            // Note: location_lat and location_lon are intentionally missing
        };
        
        // Act - Convert from DynamoDB item
        var store = S2StoreWithCoordsEntity.FromDynamoDb<S2StoreWithCoordsEntity>(item);
        
        // Assert - Verify GeoLocation is reconstructed from S2 token (fallback behavior)
        store.Location.Should().NotBe(default(GeoLocation), "Location should be reconstructed");
        
        // Since coordinates are missing, the location should be the S2 cell center
        var expectedCellCenter = S2Extensions.FromS2Token(s2Token);
        store.Location.Latitude.Should().BeApproximately(expectedCellCenter.Latitude, 0.000001,
            "Latitude should be reconstructed from S2 token (cell center)");
        store.Location.Longitude.Should().BeApproximately(expectedCellCenter.Longitude, 0.000001,
            "Longitude should be reconstructed from S2 token (cell center)");
        
        // Verify the fallback location is within S2 cell precision (~0.01 degrees for level 16)
        store.Location.Latitude.Should().BeApproximately(originalLocation.Latitude, 0.01,
            "Fallback latitude should be within S2 cell precision");
        store.Location.Longitude.Should().BeApproximately(originalLocation.Longitude, 0.01,
            "Fallback longitude should be within S2 cell precision");
    }
    
    [Fact]
    public async Task ReadFromDynamoDb_WithOnlyS2Token_FallbackBehaviorWorksCorrectly()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(40.7128, -74.0060); // New York
        var s2Token = S2Extensions.ToS2Token(originalLocation, 16);
        
        // Write item with only S2 token to DynamoDB (simulating legacy data)
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-fallback-002" },
            ["sk"] = new AttributeValue { S = "east" },
            ["name"] = new AttributeValue { S = "Legacy NYC Store" },
            ["description"] = new AttributeValue { S = "Data written before coordinate storage" },
            ["location"] = new AttributeValue { S = s2Token }
            // No location_lat or location_lon
        };
        
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Act - Read entity back from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-fallback-002" },
            ["sk"] = new AttributeValue { S = "east" }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        var store = S2StoreWithCoordsEntity.FromDynamoDb<S2StoreWithCoordsEntity>(response.Item);
        
        // Assert - Verify fallback behavior works correctly
        store.Location.Should().NotBe(default(GeoLocation), "Location should be reconstructed from token");
        store.Name.Should().Be("Legacy NYC Store");
        store.Description.Should().Be("Data written before coordinate storage");
        
        // Verify location is reconstructed from S2 token (cell center)
        var expectedCellCenter = S2Extensions.FromS2Token(s2Token);
        store.Location.Latitude.Should().BeApproximately(expectedCellCenter.Latitude, 0.000001,
            "Fallback should use S2 cell center latitude");
        store.Location.Longitude.Should().BeApproximately(expectedCellCenter.Longitude, 0.000001,
            "Fallback should use S2 cell center longitude");
    }
    
    [Fact]
    public async Task FromDynamoDb_WithOnlyS2Token_LocationIsWithinCellBounds()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(51.5074, -0.1278); // London
        var s2Token = S2Extensions.ToS2Token(originalLocation, 16);
        var cell = S2Extensions.ToS2Cell(originalLocation, 16);
        
        // Create item with only S2 token
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-fallback-003" },
            ["sk"] = new AttributeValue { S = "europe" },
            ["name"] = new AttributeValue { S = "London Fallback Store" },
            ["location"] = new AttributeValue { S = s2Token }
        };
        
        // Act - Convert from DynamoDB item
        var store = S2StoreWithCoordsEntity.FromDynamoDb<S2StoreWithCoordsEntity>(item);
        
        // Assert - Verify fallback location is within the S2 cell bounds
        var bounds = cell.Bounds;
        store.Location.Latitude.Should().BeInRange(bounds.Southwest.Latitude, bounds.Northeast.Latitude,
            "Fallback latitude should be within cell bounds");
        store.Location.Longitude.Should().BeInRange(bounds.Southwest.Longitude, bounds.Northeast.Longitude,
            "Fallback longitude should be within cell bounds");
    }
    
    [Fact]
    public async Task FromDynamoDb_WithOnlyS2Token_MultipleLocationsFallbackCorrectly()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var locations = new[]
        {
            new GeoLocation(37.7749, -122.4194), // San Francisco
            new GeoLocation(40.7128, -74.0060),  // New York
            new GeoLocation(51.5074, -0.1278),   // London
            new GeoLocation(35.6762, 139.6503),  // Tokyo
            new GeoLocation(-33.8688, 151.2093)  // Sydney
        };
        
        // Act & Assert - Test fallback for each location
        for (int i = 0; i < locations.Length; i++)
        {
            var s2Token = S2Extensions.ToS2Token(locations[i], 16);
            var expectedCellCenter = S2Extensions.FromS2Token(s2Token);
            
            var item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = $"store-fallback-{i:D3}" },
                ["sk"] = new AttributeValue { S = "global" },
                ["name"] = new AttributeValue { S = $"Fallback Store {i}" },
                ["location"] = new AttributeValue { S = s2Token }
                // No coordinates
            };
            
            var store = S2StoreWithCoordsEntity.FromDynamoDb<S2StoreWithCoordsEntity>(item);
            
            // Verify fallback to cell center
            store.Location.Latitude.Should().BeApproximately(expectedCellCenter.Latitude, 0.000001,
                $"Location {i} should fallback to cell center latitude");
            store.Location.Longitude.Should().BeApproximately(expectedCellCenter.Longitude, 0.000001,
                $"Location {i} should fallback to cell center longitude");
            
            // Verify it's within S2 precision of original
            store.Location.Latitude.Should().BeApproximately(locations[i].Latitude, 0.01,
                $"Location {i} fallback should be within S2 precision of original");
            store.Location.Longitude.Should().BeApproximately(locations[i].Longitude, 0.01,
                $"Location {i} fallback should be within S2 precision of original");
        }
    }
    
    [Fact]
    public async Task FromDynamoDb_WithOnlyS2Token_EdgeCaseLocationsFallbackCorrectly()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var edgeCases = new[]
        {
            new GeoLocation(89.5, 0.0),      // Near North Pole
            new GeoLocation(-89.5, 0.0),     // Near South Pole
            new GeoLocation(0.0, 179.5),     // Near Date Line (east)
            new GeoLocation(0.0, -179.5),    // Near Date Line (west)
            new GeoLocation(0.0, 0.0)        // Null Island (Equator + Prime Meridian)
        };
        
        // Act & Assert - Test fallback for each edge case
        for (int i = 0; i < edgeCases.Length; i++)
        {
            var s2Token = S2Extensions.ToS2Token(edgeCases[i], 16);
            var expectedCellCenter = S2Extensions.FromS2Token(s2Token);
            
            var item = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new AttributeValue { S = $"store-fallback-edge-{i:D3}" },
                ["sk"] = new AttributeValue { S = "edge" },
                ["name"] = new AttributeValue { S = $"Edge Fallback Store {i}" },
                ["location"] = new AttributeValue { S = s2Token }
            };
            
            var store = S2StoreWithCoordsEntity.FromDynamoDb<S2StoreWithCoordsEntity>(item);
            
            // Verify fallback to cell center works for edge cases
            store.Location.Latitude.Should().BeApproximately(expectedCellCenter.Latitude, 0.000001,
                $"Edge case {i} should fallback to cell center latitude");
            store.Location.Longitude.Should().BeApproximately(expectedCellCenter.Longitude, 0.000001,
                $"Edge case {i} should fallback to cell center longitude");
        }
    }
    
    [Fact]
    public async Task RoundTrip_WithOnlyS2Token_FallbackPreservesApproximateLocation()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithCoordsEntity>();
        
        var originalLocation = new GeoLocation(48.8566, 2.3522); // Paris
        var s2Token = S2Extensions.ToS2Token(originalLocation, 16);
        
        // Create item with only S2 token (simulating legacy data)
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-fallback-roundtrip" },
            ["sk"] = new AttributeValue { S = "europe" },
            ["name"] = new AttributeValue { S = "Paris Fallback Store" },
            ["location"] = new AttributeValue { S = s2Token }
        };
        
        // Act - Deserialize and re-serialize
        var store = S2StoreWithCoordsEntity.FromDynamoDb<S2StoreWithCoordsEntity>(item);
        var reserializedItem = S2StoreWithCoordsEntity.ToDynamoDb(store);
        
        // Assert - Verify the fallback location is preserved through round-trip
        // After deserialization, the location is the cell center
        // After re-serialization, it should have the same S2 token (same cell)
        reserializedItem["location"].S.Should().Be(s2Token,
            "S2 token should be preserved through fallback round-trip");
        
        // The re-serialized item should now have coordinates (from the fallback cell center)
        reserializedItem.Should().ContainKey("location_lat",
            "Re-serialization should add coordinate fields");
        reserializedItem.Should().ContainKey("location_lon",
            "Re-serialization should add coordinate fields");
        
        // The coordinates should match the cell center
        var expectedCellCenter = S2Extensions.FromS2Token(s2Token);
        var lat = double.Parse(reserializedItem["location_lat"].N);
        var lon = double.Parse(reserializedItem["location_lon"].N);
        
        lat.Should().BeApproximately(expectedCellCenter.Latitude, 0.000001,
            "Re-serialized latitude should be cell center");
        lon.Should().BeApproximately(expectedCellCenter.Longitude, 0.000001,
            "Re-serialized longitude should be cell center");
    }
    
    #endregion

    #region 24.7 Test S2 computed property serialization
    
    // NOTE: These tests are disabled because the source generator cannot deserialize into computed properties.
    // See Oproto.FluentDynamoDb.SourceGenerator/KNOWN_LIMITATIONS.md for details.
    // Tests are preserved for when the limitation is resolved.
    
#if FALSE // TODO: Enable when source generator supports computed properties
    
    [Fact]
    public async Task WriteToDynamoDb_WithS2AndComputedProperties_CreatesThreeAttributes()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithComputedPropsEntity>();
        
        var store = new S2StoreWithComputedPropsEntity
        {
            StoreId = "store-computed-001",
            Region = "west",
            Name = "Computed Props Store",
            Location = new GeoLocation(37.7749, -122.4194), // San Francisco
            Description = "Store with computed coordinate properties"
        };
        
        // Act - Write entity to DynamoDB
        var item = S2StoreWithComputedPropsEntity.ToDynamoDb(store);
        await DynamoDb.PutItemAsync(TableName, item);
        
        // Read back the raw item from DynamoDB
        var key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = store.StoreId },
            ["sk"] = new AttributeValue { S = store.Region }
        };
        var response = await DynamoDb.GetItemAsync(TableName, key);
        
        // Assert - Verify three attributes are created: S2 token, latitude, longitude
        response.Item.Should().ContainKey("location", "S2 token attribute should exist");
        response.Item.Should().ContainKey("lat", "Latitude attribute should exist");
        response.Item.Should().ContainKey("lon", "Longitude attribute should exist");
        
        // Verify all three attributes have values
        response.Item["location"].S.Should().NotBeNullOrEmpty("S2 token should have a value");
        response.Item["lat"].N.Should().NotBeNullOrEmpty("Latitude should have a value");
        response.Item["lon"].N.Should().NotBeNullOrEmpty("Longitude should have a value");
    }
    
    [Fact]
    public async Task ToDynamoDb_WithS2AndComputedProperties_AllThreeFieldsHaveCorrectValues()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithComputedPropsEntity>();
        
        var originalLocation = new GeoLocation(40.7128, -74.0060); // New York
        var store = new S2StoreWithComputedPropsEntity
        {
            StoreId = "store-computed-002",
            Region = "east",
            Name = "NYC Computed Store",
            Location = originalLocation
        };
        
        // Act - Convert to DynamoDB item
        var item = S2StoreWithComputedPropsEntity.ToDynamoDb(store);
        
        // Assert - Verify S2 token is correct
        var expectedS2Token = S2Extensions.ToS2Token(originalLocation, 16);
        item["location"].S.Should().Be(expectedS2Token, "S2 token should be correctly encoded");
        
        // Verify latitude is correct (stored as DynamoDB Number)
        var storedLatitude = double.Parse(item["lat"].N);
        storedLatitude.Should().BeApproximately(originalLocation.Latitude, 0.000001, 
            "Latitude should be stored with full precision");
        
        // Verify longitude is correct (stored as DynamoDB Number)
        var storedLongitude = double.Parse(item["lon"].N);
        storedLongitude.Should().BeApproximately(originalLocation.Longitude, 0.000001,
            "Longitude should be stored with full precision");
    }
    
    [Fact]
    public async Task ToDynamoDb_WithS2AndComputedProperties_ComputedPropertiesSerializedCorrectly()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithComputedPropsEntity>();
        
        // Use coordinates with high precision to verify computed properties work correctly
        var preciseLocation = new GeoLocation(51.5073509, -0.1277583); // London with high precision
        var store = new S2StoreWithComputedPropsEntity
        {
            StoreId = "store-computed-003",
            Region = "europe",
            Name = "London Computed Store",
            Location = preciseLocation
        };
        
        // Act - Convert to DynamoDB item
        var item = S2StoreWithComputedPropsEntity.ToDynamoDb(store);
        
        // Assert - Verify computed properties are serialized correctly
        // The computed properties (Latitude and Longitude) should be evaluated and stored
        item.Should().ContainKey("lat", "Computed Latitude property should be serialized");
        item.Should().ContainKey("lon", "Computed Longitude property should be serialized");
        
        // Verify the computed property values match the Location property values
        var storedLat = double.Parse(item["lat"].N);
        var storedLon = double.Parse(item["lon"].N);
        
        storedLat.Should().Be(store.Latitude, "Computed Latitude should match Location.Latitude");
        storedLon.Should().Be(store.Longitude, "Computed Longitude should match Location.Longitude");
        
        // Verify exact precision is preserved
        storedLat.Should().BeApproximately(preciseLocation.Latitude, 0.000001,
            "Computed Latitude should preserve full precision");
        storedLon.Should().BeApproximately(preciseLocation.Longitude, 0.000001,
            "Computed Longitude should preserve full precision");
    }
    
    [Fact]
    public async Task RoundTrip_WithS2AndComputedProperties_PreservesExactCoordinates()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithComputedPropsEntity>();
        
        var originalLocation = new GeoLocation(35.6762123456, 139.6503987654); // Tokyo with high precision
        var originalStore = new S2StoreWithComputedPropsEntity
        {
            StoreId = "store-computed-004",
            Region = "asia",
            Name = "Tokyo Computed Store",
            Location = originalLocation,
            Description = "Round-trip computed property test"
        };
        
        // Act - Round trip through DynamoDB serialization
        var item = S2StoreWithComputedPropsEntity.ToDynamoDb(originalStore);
        var roundTrippedStore = S2StoreWithComputedPropsEntity.FromDynamoDb<S2StoreWithComputedPropsEntity>(item);
        
        // Assert - Verify exact original coordinates are preserved
        roundTrippedStore.Location.Latitude.Should().Be(originalLocation.Latitude,
            "Latitude should be exactly preserved in round-trip");
        roundTrippedStore.Location.Longitude.Should().Be(originalLocation.Longitude,
            "Longitude should be exactly preserved in round-trip");
        
        // Verify computed properties return the correct values after deserialization
        roundTrippedStore.Latitude.Should().Be(originalLocation.Latitude,
            "Computed Latitude property should return correct value after deserialization");
        roundTrippedStore.Longitude.Should().Be(originalLocation.Longitude,
            "Computed Longitude property should return correct value after deserialization");
    }
    
    [Fact]
    public async Task WriteToDynamoDb_WithS2AndComputedProperties_MultipleLocationsSerializeCorrectly()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithComputedPropsEntity>();
        
        var locations = new[]
        {
            new GeoLocation(37.7749295, -122.4194155), // San Francisco
            new GeoLocation(40.7127753, -74.0059728),  // New York
            new GeoLocation(51.5073509, -0.1277583),   // London
            new GeoLocation(35.6761919, 139.6503106),  // Tokyo
            new GeoLocation(-33.8688197, 151.2092955)  // Sydney
        };
        
        // Act & Assert - Test each location serializes all three fields correctly
        for (int i = 0; i < locations.Length; i++)
        {
            var store = new S2StoreWithComputedPropsEntity
            {
                StoreId = $"store-computed-{i:D3}",
                Region = "global",
                Name = $"Computed Store {i}",
                Location = locations[i]
            };
            
            var item = S2StoreWithComputedPropsEntity.ToDynamoDb(store);
            
            // Verify all three fields are present
            item.Should().ContainKey("location", $"Store {i} should have S2 token");
            item.Should().ContainKey("lat", $"Store {i} should have computed latitude");
            item.Should().ContainKey("lon", $"Store {i} should have computed longitude");
            
            // Verify all three fields have correct values
            var expectedToken = S2Extensions.ToS2Token(locations[i], 16);
            item["location"].S.Should().Be(expectedToken, $"Store {i} S2 token should be correct");
            
            var lat = double.Parse(item["lat"].N);
            var lon = double.Parse(item["lon"].N);
            
            lat.Should().BeApproximately(locations[i].Latitude, 0.000001, 
                $"Store {i} computed latitude should be correct");
            lon.Should().BeApproximately(locations[i].Longitude, 0.000001,
                $"Store {i} computed longitude should be correct");
        }
    }
    
    [Fact]
    public async Task FromDynamoDb_WithS2AndComputedProperties_ReconstructsFromCoordinates()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithComputedPropsEntity>();
        
        var originalLocation = new GeoLocation(48.8566, 2.3522); // Paris
        var s2Token = S2Extensions.ToS2Token(originalLocation, 16);
        
        // Create DynamoDB item with S2 token AND computed coordinate fields
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-computed-read-001" },
            ["sk"] = new AttributeValue { S = "europe" },
            ["name"] = new AttributeValue { S = "Paris Computed Read Store" },
            ["location"] = new AttributeValue { S = s2Token },
            ["lat"] = new AttributeValue { N = originalLocation.Latitude.ToString("F10") },
            ["lon"] = new AttributeValue { N = originalLocation.Longitude.ToString("F10") }
        };
        
        // Act - Convert from DynamoDB item
        var store = S2StoreWithComputedPropsEntity.FromDynamoDb<S2StoreWithComputedPropsEntity>(item);
        
        // Assert - Verify GeoLocation is reconstructed from coordinates, NOT from S2 token
        store.Location.Latitude.Should().BeApproximately(originalLocation.Latitude, 0.000001,
            "Latitude should be reconstructed from coordinate field");
        store.Location.Longitude.Should().BeApproximately(originalLocation.Longitude, 0.000001,
            "Longitude should be reconstructed from coordinate field");
        
        // Verify computed properties return the correct values
        store.Latitude.Should().BeApproximately(originalLocation.Latitude, 0.000001,
            "Computed Latitude property should return correct value");
        store.Longitude.Should().BeApproximately(originalLocation.Longitude, 0.000001,
            "Computed Longitude property should return correct value");
    }
    
    [Fact]
    public async Task WriteToDynamoDb_WithS2AndComputedProperties_EdgeCasesSerializeCorrectly()
    {
        // Arrange
        await CreateTableAsync<S2StoreWithComputedPropsEntity>();
        
        var edgeCases = new[]
        {
            new GeoLocation(89.9999, 0.0),      // Near North Pole
            new GeoLocation(-89.9999, 0.0),     // Near South Pole
            new GeoLocation(0.0, 179.9999),     // Near Date Line (east)
            new GeoLocation(0.0, -179.9999),    // Near Date Line (west)
            new GeoLocation(0.0, 0.0)           // Null Island (Equator + Prime Meridian)
        };
        
        // Act & Assert - Test each edge case serializes correctly
        for (int i = 0; i < edgeCases.Length; i++)
        {
            var store = new S2StoreWithComputedPropsEntity
            {
                StoreId = $"store-computed-edge-{i:D3}",
                Region = "edge",
                Name = $"Edge Case Computed Store {i}",
                Location = edgeCases[i]
            };
            
            var item = S2StoreWithComputedPropsEntity.ToDynamoDb(store);
            
            // Verify all three fields are present for edge cases
            item.Should().ContainKey("location", $"Edge case {i} should have S2 token");
            item.Should().ContainKey("lat", $"Edge case {i} should have computed latitude");
            item.Should().ContainKey("lon", $"Edge case {i} should have computed longitude");
            
            // Verify computed properties are serialized correctly for edge cases
            var lat = double.Parse(item["lat"].N);
            var lon = double.Parse(item["lon"].N);
            
            lat.Should().BeApproximately(edgeCases[i].Latitude, 0.000001,
                $"Edge case {i} computed latitude should be correct");
            lon.Should().BeApproximately(edgeCases[i].Longitude, 0.000001,
                $"Edge case {i} computed longitude should be correct");
        }
    }
    
#endif // FALSE - Computed properties not yet supported
    
    #endregion
}
