using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Geospatial.GeoHash;
using Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;
using Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration tests for source generator integration with GeoLocation properties.
/// Tests verify that the source generator correctly serializes and deserializes GeoLocation
/// properties to/from GeoHash strings in DynamoDB.
/// </summary>
[Collection("DynamoDB Local")]
[Trait("Category", "Integration")]
[Trait("Feature", "Geospatial")]
[Trait("Feature", "SourceGenerator")]
public class GeospatialSourceGeneratorTests : IntegrationTestBase
{
    public GeospatialSourceGeneratorTests(DynamoDbLocalFixture fixture) : base(fixture)
    {
    }
    
    #region StoreEntity Tests (Precision 7)
    
    [Fact]
    public async Task ToDynamoDb_WithGeoLocation_SerializesToGeoHashString()
    {
        // Arrange
        await CreateTableAsync<StoreEntity>();
        
        var store = new StoreEntity
        {
            StoreId = "store-001",
            Region = "west",
            Name = "Downtown Store",
            Location = new GeoLocation(37.7749, -122.4194), // San Francisco
            Description = "Main downtown location"
        };
        
        // Act - Convert to DynamoDB item
        var item = StoreEntity.ToDynamoDb(store);
        
        // Assert - Verify location is stored as GeoHash string with precision 7
        item.Should().ContainKey("location");
        item["location"].S.Should().NotBeNullOrEmpty();
        item["location"].S.Length.Should().Be(7); // Precision 7
        
        // Verify the GeoHash is correct
        var expectedHash = store.Location.ToGeoHash(7);
        item["location"].S.Should().Be(expectedHash);
        
        // Verify it's a valid GeoHash that can be decoded
        var decoded = GeoHashExtensions.FromGeoHash(item["location"].S);
        decoded.Latitude.Should().BeApproximately(37.7749, 0.01);
        decoded.Longitude.Should().BeApproximately(-122.4194, 0.01);
    }
    
    [Fact]
    public async Task FromDynamoDb_WithGeoHashString_DeserializesToGeoLocation()
    {
        // Arrange
        await CreateTableAsync<StoreEntity>();
        
        var sanFrancisco = new GeoLocation(37.7749, -122.4194);
        var geoHash = sanFrancisco.ToGeoHash(7);
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "store-002" },
            ["sk"] = new AttributeValue { S = "east" },
            ["name"] = new AttributeValue { S = "East Side Store" },
            ["location"] = new AttributeValue { S = geoHash }
        };
        
        // Act - Convert from DynamoDB item
        var store = StoreEntity.FromDynamoDb<StoreEntity>(item);
        
        // Assert - Verify location is deserialized correctly
        store.Location.Should().NotBe(default(GeoLocation));
        store.Location.Latitude.Should().BeApproximately(37.7749, 0.01);
        store.Location.Longitude.Should().BeApproximately(-122.4194, 0.01);
    }
    
    [Fact]
    public async Task RoundTrip_WithGeoLocation_PreservesLocationData()
    {
        // Arrange
        await CreateTableAsync<StoreEntity>();
        
        var originalStore = new StoreEntity
        {
            StoreId = "store-003",
            Region = "north",
            Name = "North Store",
            Location = new GeoLocation(40.7128, -74.0060), // New York
            Description = "Northern branch"
        };
        
        // Act - Round trip through DynamoDB serialization
        var item = StoreEntity.ToDynamoDb(originalStore);
        var roundTrippedStore = StoreEntity.FromDynamoDb<StoreEntity>(item);
        
        // Assert - Verify location is preserved (within precision bounds)
        roundTrippedStore.Location.Latitude.Should().BeApproximately(
            originalStore.Location.Latitude, 0.01);
        roundTrippedStore.Location.Longitude.Should().BeApproximately(
            originalStore.Location.Longitude, 0.01);
        roundTrippedStore.Name.Should().Be(originalStore.Name);
        roundTrippedStore.Description.Should().Be(originalStore.Description);
    }
    
    #endregion
    
    #region LocationEntity Tests (Default Precision 6)
    
    [Fact]
    public async Task ToDynamoDb_WithDefaultPrecision_UsesDefaultPrecision6()
    {
        // Arrange
        await CreateTableAsync<LocationEntity>();
        
        var location = new LocationEntity
        {
            LocationId = "loc-001",
            Name = "Central Park",
            Location = new GeoLocation(40.7829, -73.9654) // Central Park, NYC
        };
        
        // Act - Convert to DynamoDB item
        var item = LocationEntity.ToDynamoDb(location);
        
        // Assert - Verify location uses default precision 6
        item.Should().ContainKey("location");
        item["location"].S.Should().NotBeNullOrEmpty();
        item["location"].S.Length.Should().Be(6); // Default precision
        
        // Verify the GeoHash is correct
        var expectedHash = location.Location.ToGeoHash(6);
        item["location"].S.Should().Be(expectedHash);
    }
    
    [Fact]
    public async Task RoundTrip_WithDefaultPrecision_PreservesLocationData()
    {
        // Arrange
        await CreateTableAsync<LocationEntity>();
        
        var originalLocation = new LocationEntity
        {
            LocationId = "loc-002",
            Name = "Golden Gate Bridge",
            Location = new GeoLocation(37.8199, -122.4783)
        };
        
        // Act - Round trip through DynamoDB serialization
        var item = LocationEntity.ToDynamoDb(originalLocation);
        var roundTrippedLocation = LocationEntity.FromDynamoDb<LocationEntity>(item);
        
        // Assert - Verify location is preserved (within precision bounds)
        roundTrippedLocation.Location.Latitude.Should().BeApproximately(
            originalLocation.Location.Latitude, 0.1); // Precision 6 is ~0.61km
        roundTrippedLocation.Location.Longitude.Should().BeApproximately(
            originalLocation.Location.Longitude, 0.1);
        roundTrippedLocation.Name.Should().Be(originalLocation.Name);
    }
    
    #endregion
    
    #region VenueEntity Tests (Nullable GeoLocation)
    
    [Fact]
    public async Task ToDynamoDb_WithNullGeoLocation_OmitsLocationAttribute()
    {
        // Arrange
        await CreateTableAsync<VenueEntity>();
        
        var venue = new VenueEntity
        {
            VenueId = "venue-001",
            Name = "Virtual Venue",
            Location = null
        };
        
        // Act - Convert to DynamoDB item
        var item = VenueEntity.ToDynamoDb(venue);
        
        // Assert - Verify location attribute is not present
        item.Should().NotContainKey("location");
        item.Should().ContainKey("pk");
        item.Should().ContainKey("name");
    }
    
    [Fact]
    public async Task ToDynamoDb_WithDefaultGeoLocation_SerializesAsNullIsland()
    {
        // Arrange
        await CreateTableAsync<VenueEntity>();
        
        var venue = new VenueEntity
        {
            VenueId = "venue-002",
            Name = "TBD Venue",
            Location = default(GeoLocation) // Default struct value (0, 0) - Null Island
        };
        
        // Act - Convert to DynamoDB item
        var item = VenueEntity.ToDynamoDb(venue);
        
        // Assert - Verify location is serialized as Null Island (0,0)
        // Note: The source generator serializes (0,0) because it's a valid location
        item.Should().ContainKey("location");
        var decoded = GeoHashExtensions.FromGeoHash(item["location"].S);
        decoded.Latitude.Should().BeApproximately(0.0, 0.1);
        decoded.Longitude.Should().BeApproximately(0.0, 0.1);
    }
    
    [Fact]
    public async Task FromDynamoDb_WithMissingLocation_ReturnsNullGeoLocation()
    {
        // Arrange
        await CreateTableAsync<VenueEntity>();
        
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "venue-003" },
            ["name"] = new AttributeValue { S = "No Location Venue" }
            // location attribute is missing
        };
        
        // Act - Convert from DynamoDB item
        var venue = VenueEntity.FromDynamoDb<VenueEntity>(item);
        
        // Assert - Verify location is null or default
        venue.Location.Should().BeNull();
    }
    
    [Fact]
    public async Task RoundTrip_WithValidNullableGeoLocation_PreservesLocationData()
    {
        // Arrange
        await CreateTableAsync<VenueEntity>();
        
        var originalVenue = new VenueEntity
        {
            VenueId = "venue-004",
            Name = "Concert Hall",
            Location = new GeoLocation(51.5074, -0.1278) // London
        };
        
        // Act - Round trip through DynamoDB serialization
        var item = VenueEntity.ToDynamoDb(originalVenue);
        var roundTrippedVenue = VenueEntity.FromDynamoDb<VenueEntity>(item);
        
        // Assert - Verify location is preserved
        roundTrippedVenue.Location.Should().NotBeNull();
        roundTrippedVenue.Location!.Value.Latitude.Should().BeApproximately(
            originalVenue.Location!.Value.Latitude, 0.001); // Precision 8 is ~19m
        roundTrippedVenue.Location!.Value.Longitude.Should().BeApproximately(
            originalVenue.Location!.Value.Longitude, 0.001);
    }
    
    #endregion
    
    #region Different Precision Tests
    
    [Fact]
    public async Task ToDynamoDb_WithDifferentPrecisions_UsesCorrectPrecision()
    {
        // Arrange - No need to create tables for this test, we're just testing serialization
        var location = new GeoLocation(35.6762, 139.6503); // Tokyo
        
        var store = new StoreEntity
        {
            StoreId = "store-tokyo",
            Region = "asia",
            Name = "Tokyo Store",
            Location = location
        };
        
        var locationEntity = new LocationEntity
        {
            LocationId = "loc-tokyo",
            Name = "Tokyo Location",
            Location = location
        };
        
        var venue = new VenueEntity
        {
            VenueId = "venue-tokyo",
            Name = "Tokyo Venue",
            Location = location
        };
        
        // Act - Convert all entities
        var storeItem = StoreEntity.ToDynamoDb(store);
        var locationItem = LocationEntity.ToDynamoDb(locationEntity);
        var venueItem = VenueEntity.ToDynamoDb(venue);
        
        // Assert - Verify each uses its configured precision
        storeItem["location"].S.Length.Should().Be(7); // StoreEntity uses precision 7
        locationItem["location"].S.Length.Should().Be(6); // LocationEntity uses default 6
        venueItem["location"].S.Length.Should().Be(8); // VenueEntity uses precision 8
        
        // Verify all are valid GeoHashes for the same location
        var storeHash = storeItem["location"].S;
        var locationHash = locationItem["location"].S;
        var venueHash = venueItem["location"].S;
        
        // Lower precision hashes should be prefixes of higher precision hashes
        venueHash.Should().StartWith(storeHash.Substring(0, 6)); // First 6 chars match
        storeHash.Should().StartWith(locationHash); // Precision 7 starts with precision 6
    }
    
    #endregion
    
    #region Edge Cases
    
    [Fact]
    public async Task RoundTrip_WithPoleLocation_PreservesData()
    {
        // Arrange
        await CreateTableAsync<StoreEntity>();
        
        var store = new StoreEntity
        {
            StoreId = "store-pole",
            Region = "arctic",
            Name = "North Pole Store",
            Location = new GeoLocation(90.0, 0.0) // North Pole
        };
        
        // Act - Round trip
        var item = StoreEntity.ToDynamoDb(store);
        var roundTripped = StoreEntity.FromDynamoDb<StoreEntity>(item);
        
        // Assert
        roundTripped.Location.Latitude.Should().BeApproximately(90.0, 0.1);
    }
    
    [Fact]
    public async Task RoundTrip_WithDateLineLocation_PreservesData()
    {
        // Arrange
        await CreateTableAsync<StoreEntity>();
        
        var store = new StoreEntity
        {
            StoreId = "store-dateline",
            Region = "pacific",
            Name = "Date Line Store",
            Location = new GeoLocation(0.0, 180.0) // On the date line
        };
        
        // Act - Round trip
        var item = StoreEntity.ToDynamoDb(store);
        var roundTripped = StoreEntity.FromDynamoDb<StoreEntity>(item);
        
        // Assert
        roundTripped.Location.Longitude.Should().BeApproximately(180.0, 0.1);
    }
    
    [Fact]
    public async Task RoundTrip_WithNullIsland_PreservesData()
    {
        // Arrange
        await CreateTableAsync<LocationEntity>();
        
        var location = new LocationEntity
        {
            LocationId = "loc-null-island",
            Name = "Null Island",
            Location = new GeoLocation(0.0, 0.0) // Null Island
        };
        
        // Act - Round trip
        var item = LocationEntity.ToDynamoDb(location);
        var roundTripped = LocationEntity.FromDynamoDb<LocationEntity>(item);
        
        // Assert
        roundTripped.Location.Latitude.Should().BeApproximately(0.0, 0.1);
        roundTripped.Location.Longitude.Should().BeApproximately(0.0, 0.1);
    }
    
    #endregion
}
