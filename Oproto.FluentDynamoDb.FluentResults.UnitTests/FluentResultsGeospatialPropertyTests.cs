using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentResults;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.FluentResults;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Pagination;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
namespace Oproto.FluentDynamoDb.FluentResults.UnitTests;

/// <summary>
/// Property-based tests for FluentResults geospatial extension methods.
/// These tests verify the correctness properties defined in the design document.
/// </summary>
public class FluentResultsGeospatialPropertyTests
{
    #region Property 3: Result Failure Contains Error (Geospatial Variant)

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (geospatial variant)
    /// *For any* failed spatial query operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError containing the original exception.
    /// **Validates: Requirements 7.1-7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpatialQueryAsyncResult_Proximity_Failure_ContainsError()
    {
        return Prop.ForAll(
            GeospatialExceptionArbitrary(),
            ValidGeoLocationArbitrary(),
            PositiveDoubleArbitrary(),
            (ex, center, radius) =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var mockTable = CreateMockTable(mockClient);

                // Configure the mock to throw when QueryAsync is called
                mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<QueryResponse>(ex));

                // Act
                var result = mockTable.SpatialQueryAsyncResult<GeoTestEntity>(
                    locationSelector: e => e.Location,
                    spatialIndexType: SpatialIndexType.GeoHash,
                    precision: 5,
                    center: center,
                    radiusKilometers: radius,
                    queryBuilder: (query, cell, pagination) => query.Where("pk = :pk").WithValue(":pk", cell)
                ).GetAwaiter().GetResult();

                // Assert
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError dynamoDbError &&
                       dynamoDbError.InnerException != null;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (bounding box variant)
    /// *For any* failed spatial bounding box query operation (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError containing the original exception.
    /// **Validates: Requirements 7.2, 7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpatialQueryAsyncResult_BoundingBox_Failure_ContainsError()
    {
        return Prop.ForAll(
            GeospatialExceptionArbitrary(),
            ValidBoundingBoxArbitrary(),
            (ex, boundingBox) =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var mockTable = CreateMockTable(mockClient);

                // Configure the mock to throw when QueryAsync is called
                mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<QueryResponse>(ex));

                // Act
                var result = mockTable.SpatialQueryAsyncResult<GeoTestEntity>(
                    locationSelector: e => e.Location,
                    spatialIndexType: SpatialIndexType.GeoHash,
                    precision: 5,
                    boundingBox: boundingBox,
                    queryBuilder: (query, cell, pagination) => query.Where("pk = :pk").WithValue(":pk", cell)
                ).GetAwaiter().GetResult();

                // Assert
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError dynamoDbError &&
                       dynamoDbError.InnerException != null;
            });
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 3: Result Failure Contains Error** (index proximity variant)
    /// *For any* failed spatial query on an index (exception thrown), the Result-returning extension method 
    /// SHALL return Result.Fail with a DynamoDbError containing the original exception.
    /// **Validates: Requirements 7.3, 7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpatialQueryAsyncResult_Index_Failure_ContainsError()
    {
        return Prop.ForAll(
            GeospatialExceptionArbitrary(),
            ValidGeoLocationArbitrary(),
            PositiveDoubleArbitrary(),
            (ex, center, radius) =>
            {
                // Arrange
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var mockTable = CreateMockTable(mockClient);
                var index = new DynamoDbIndex(mockTable, "gsi1");

                // Configure the mock to throw when QueryAsync is called
                mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromException<QueryResponse>(ex));

                // Act
                var result = index.SpatialQueryAsyncResult<GeoTestEntity>(
                    locationSelector: e => e.Location,
                    spatialIndexType: SpatialIndexType.GeoHash,
                    precision: 5,
                    center: center,
                    radiusKilometers: radius,
                    queryBuilder: (query, cell, pagination) => query.Where("pk = :pk").WithValue(":pk", cell)
                ).GetAwaiter().GetResult();

                // Assert
                return result.IsFailed &&
                       result.Errors.Count >= 1 &&
                       result.Errors[0] is DynamoDbError dynamoDbError &&
                       dynamoDbError.InnerException != null;
            });
    }

    #endregion

    #region Cancellation Token Tests

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (geospatial variant)
    /// *For any* Result-returning geospatial extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task SpatialQueryAsyncResult_Proximity_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockTable = CreateMockTable(mockClient);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<QueryResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => mockTable.SpatialQueryAsyncResult<GeoTestEntity>(
                locationSelector: e => e.Location,
                spatialIndexType: SpatialIndexType.GeoHash,
                precision: 5,
                center: new GeoLocation(40.7128, -74.0060),
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query.Where("pk = :pk").WithValue(":pk", cell),
                cancellationToken: cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (bounding box variant)
    /// *For any* Result-returning geospatial extension method, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task SpatialQueryAsyncResult_BoundingBox_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockTable = CreateMockTable(mockClient);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<QueryResponse>(cts.Token));

        var boundingBox = new GeoBoundingBox(
            new GeoLocation(40.0, -75.0),
            new GeoLocation(41.0, -73.0));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => mockTable.SpatialQueryAsyncResult<GeoTestEntity>(
                locationSelector: e => e.Location,
                spatialIndexType: SpatialIndexType.GeoHash,
                precision: 5,
                boundingBox: boundingBox,
                queryBuilder: (query, cell, pagination) => query.Where("pk = :pk").WithValue(":pk", cell),
                cancellationToken: cts.Token));
    }

    /// <summary>
    /// **Feature: fluentresults-comprehensive-api, Property 4: Cancellation Token Passthrough** (index variant)
    /// *For any* Result-returning geospatial extension method on an index, when the CancellationToken is cancelled, 
    /// the method SHALL throw OperationCanceledException without wrapping it in a Result.
    /// **Validates: Requirements 12.1**
    /// </summary>
    [Fact]
    public async Task SpatialQueryAsyncResult_Index_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var mockClient = Substitute.For<IAmazonDynamoDB>();
        var mockTable = CreateMockTable(mockClient);
        var index = new DynamoDbIndex(mockTable, "gsi1");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockClient.QueryAsync(Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<QueryResponse>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => index.SpatialQueryAsyncResult<GeoTestEntity>(
                locationSelector: e => e.Location,
                spatialIndexType: SpatialIndexType.GeoHash,
                precision: 5,
                center: new GeoLocation(40.7128, -74.0060),
                radiusKilometers: 10.0,
                queryBuilder: (query, cell, pagination) => query.Where("pk = :pk").WithValue(":pk", cell),
                cancellationToken: cts.Token));
    }

    #endregion

    #region Error Type Tests

    /// <summary>
    /// Verifies that SpatialQueryError contains the correct error code.
    /// **Validates: Requirements 7.4**
    /// </summary>
    [Fact]
    public void SpatialQueryError_HasCorrectErrorCode()
    {
        // Arrange & Act
        var error = new SpatialQueryError(
            "Test error",
            latitude: 40.7128,
            longitude: -74.0060,
            radiusKilometers: 10.0,
            spatialIndexType: "GeoHash",
            precision: 5);

        // Assert
        Assert.Equal("SPATIAL_QUERY_ERROR", error.ErrorCode);
        Assert.Equal(40.7128, error.Latitude);
        Assert.Equal(-74.0060, error.Longitude);
        Assert.Equal(10.0, error.RadiusKilometers);
        Assert.Equal("GeoHash", error.SpatialIndexType);
        Assert.Equal(5, error.Precision);
    }

    /// <summary>
    /// Verifies that InvalidCoordinatesError contains the correct error code.
    /// **Validates: Requirements 7.4**
    /// </summary>
    [Fact]
    public void InvalidCoordinatesError_HasCorrectErrorCode()
    {
        // Arrange & Act
        var error = new InvalidCoordinatesError(
            "Invalid coordinates",
            latitude: 91.0,
            longitude: -74.0060);

        // Assert
        Assert.Equal("INVALID_COORDINATES", error.ErrorCode);
        Assert.Equal(91.0, error.Latitude);
        Assert.Equal(-74.0060, error.Longitude);
    }

    /// <summary>
    /// Verifies that InvalidBoundingBoxError contains the correct error code.
    /// **Validates: Requirements 7.4**
    /// </summary>
    [Fact]
    public void InvalidBoundingBoxError_HasCorrectErrorCode()
    {
        // Arrange & Act
        var error = new InvalidBoundingBoxError(
            "Invalid bounding box",
            southwestLatitude: 40.0,
            southwestLongitude: -75.0,
            northeastLatitude: 41.0,
            northeastLongitude: -73.0);

        // Assert
        Assert.Equal("INVALID_BOUNDING_BOX", error.ErrorCode);
        Assert.Equal(40.0, error.SouthwestLatitude);
        Assert.Equal(-75.0, error.SouthwestLongitude);
        Assert.Equal(41.0, error.NortheastLatitude);
        Assert.Equal(-73.0, error.NortheastLongitude);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a mock table for testing.
    /// </summary>
    private static IDynamoDbTable CreateMockTable(IAmazonDynamoDB mockClient)
    {
        var mockTable = Substitute.For<IDynamoDbTable>();
        mockTable.DynamoDbClient.Returns(mockClient);
        mockTable.Name.Returns("test-table");
        return mockTable;
    }

    /// <summary>
    /// Generates arbitrary valid GeoLocation values for property testing.
    /// </summary>
    private static Arbitrary<GeoLocation> ValidGeoLocationArbitrary()
    {
        var generator = from lat in Gen.Choose(-89, 89).Select(x => (double)x)
                        from lon in Gen.Choose(-179, 179).Select(x => (double)x)
                        select new GeoLocation(lat, lon);

        return Arb.From(generator);
    }

    /// <summary>
    /// Generates arbitrary valid bounding boxes for property testing.
    /// </summary>
    private static Arbitrary<GeoBoundingBox> ValidBoundingBoxArbitrary()
    {
        var generator = from swLat in Gen.Choose(-89, 88).Select(x => (double)x)
                        from swLon in Gen.Choose(-179, 178).Select(x => (double)x)
                        from latDiff in Gen.Choose(1, 10).Select(x => (double)x)
                        from lonDiff in Gen.Choose(1, 10).Select(x => (double)x)
                        let neLat = Math.Min(swLat + latDiff, 89)
                        let neLon = Math.Min(swLon + lonDiff, 179)
                        select new GeoBoundingBox(
                            new GeoLocation(swLat, swLon),
                            new GeoLocation(neLat, neLon));

        return Arb.From(generator);
    }

    /// <summary>
    /// Generates arbitrary positive double values for radius testing.
    /// </summary>
    private static Arbitrary<double> PositiveDoubleArbitrary()
    {
        var generator = Gen.Choose(1, 100).Select(x => (double)x);
        return Arb.From(generator);
    }

    /// <summary>
    /// Generates arbitrary exceptions for testing geospatial failure scenarios.
    /// Excludes OperationCanceledException as it has special handling.
    /// </summary>
    private static Arbitrary<Exception> GeospatialExceptionArbitrary()
    {
        var generators = new[]
        {
            Gen.Constant<Exception>(new Exception("Generic error")),
            Gen.Constant<Exception>(new InvalidOperationException("Invalid operation")),
            Gen.Constant<Exception>(new ResourceNotFoundException("Resource not found")),
            Gen.Constant<Exception>(new ProvisionedThroughputExceededException("Throughput exceeded")),
        };

        return Arb.From(Gen.OneOf(generators));
    }

    #endregion
}

/// <summary>
/// Test entity for geospatial property-based tests.
/// </summary>
public partial class GeoTestEntity : IDynamoDbEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public GeoLocation Location { get; set; }

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        var testEntity = entity as GeoTestEntity;
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = testEntity?.Id ?? string.Empty },
            ["name"] = new AttributeValue { S = testEntity?.Name ?? string.Empty },
            ["lat"] = new AttributeValue { N = testEntity?.Location.Latitude.ToString() ?? "0" },
            ["lon"] = new AttributeValue { N = testEntity?.Location.Longitude.ToString() ?? "0" }
        };
    }

    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity, FluentDynamoDbOptions? options, KeyInputMode keyInputMode)
        where TSelf : IDynamoDbEntity => ToDynamoDb(entity, options);

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where TSelf : IReadOnlyEntity
    {
        var entity = new GeoTestEntity
        {
            Id = item.TryGetValue("pk", out var pk) ? pk.S : string.Empty,
            Name = item.TryGetValue("name", out var name) ? name.S : string.Empty,
            Location = new GeoLocation(
                item.TryGetValue("lat", out var lat) ? double.Parse(lat.N) : 0,
                item.TryGetValue("lon", out var lon) ? double.Parse(lon.N) : 0)
        };
        return (TSelf)(object)entity;
    }

    public static TSelf FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity
    {
        return FromDynamoDb<TSelf>(items.First(), options);
    }

    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        return item.TryGetValue("pk", out var pk) ? pk.S : string.Empty;
    }

    public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
    {
        return item.ContainsKey("pk") && item.ContainsKey("name");
    }

    public static EntityMetadata GetEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "test-table",
            Properties = Array.Empty<PropertyMetadata>(),
            Indexes = Array.Empty<IndexMetadata>(),
            Relationships = Array.Empty<RelationshipMetadata>()
        };
    }

    public static bool RequiresWriteTransaction => false;
        public static Task<TSelf> FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider? blobProvider, IFieldEncryptor? fieldEncryptor, FluentDynamoDbOptions? options, CancellationToken cancellationToken) where TSelf : IDynamoDbEntity => Task.FromResult(FromDynamoDb<TSelf>(items, options));
}
