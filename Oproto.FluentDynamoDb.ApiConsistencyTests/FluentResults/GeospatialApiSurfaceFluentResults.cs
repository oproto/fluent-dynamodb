using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.GeoSpatial.GeoHash;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.FluentResults;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.FluentResults;

/// <summary>
/// API Surface validation tests for FluentResults Geospatial operations.
/// These tests validate that all expected FluentResults API patterns compile correctly.
/// Requirements: 7.1-7.4
/// </summary>
public class GeospatialApiSurfaceFluentResults
{
    [Fact(Skip = "API Surface Validation")]
    public async Task SpatialQueryAsyncResult_TableProximity_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GeohashTable table = new GeohashTable(client, "geohash", options: null);

        GeoLocation center = new GeoLocation(44.9778d, -93.2650d);
        double radiusKm = 20.0;

        // === SpatialQueryAsyncResult for proximity queries on table ===
        var result = await table.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            spatialIndexType: SpatialIndexType.GeoHash,
            precision: 6,
            center: center,
            radiusKilometers: radiusKm,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100));

        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var response = result.Value;
            var items = response.Items;
            var hasMore = response.ContinuationToken != null;
            var continuationToken = response.ContinuationToken;
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
            // Check for spatial-specific errors
            var spatialErrors = errors.OfType<SpatialQueryError>();
            var coordinateErrors = errors.OfType<InvalidCoordinatesError>();
        }

        // === With pagination ===
        result = await table.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            spatialIndexType: SpatialIndexType.GeoHash,
            precision: 6,
            center: center,
            radiusKilometers: radiusKm,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100),
            pageSize: 25);

        // === With continuation token ===
        var continuationTokenFromPrevious = result.Value?.ContinuationToken;
        if (continuationTokenFromPrevious != null)
        {
            result = await table.SpatialQueryAsyncResult<GeoHashEntity>(
                locationSelector: e => e.Location,
                spatialIndexType: SpatialIndexType.GeoHash,
                precision: 6,
                center: center,
                radiusKilometers: radiusKm,
                queryBuilder: (builder, cell, pagination) => builder
                    .Where("gsi1pk = {0}", "category1")
                    .Take(pagination.PageSize > 0 ? pagination.PageSize : 100),
                pageSize: 25,
                continuationToken: continuationTokenFromPrevious);
        }

        // === With maxCells ===
        result = await table.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            spatialIndexType: SpatialIndexType.GeoHash,
            precision: 6,
            center: center,
            radiusKilometers: radiusKm,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100),
            maxCells: 50);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task SpatialQueryAsyncResult_TableBoundingBox_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GeohashTable table = new GeohashTable(client, "geohash", options: null);

        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(44.0, -94.0),
            northeast: new GeoLocation(45.0, -93.0));

        // === SpatialQueryAsyncResult for bounding box queries on table ===
        var result = await table.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            spatialIndexType: SpatialIndexType.GeoHash,
            precision: 6,
            boundingBox: boundingBox,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100));

        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var response = result.Value;
            var items = response.Items;
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
            // Check for bounding box errors
            var boundingBoxErrors = errors.OfType<InvalidBoundingBoxError>();
        }

        // === With pagination ===
        result = await table.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            spatialIndexType: SpatialIndexType.GeoHash,
            precision: 6,
            boundingBox: boundingBox,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100),
            pageSize: 25,
            maxCells: 50);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task SpatialQueryAsyncResult_IndexProximity_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GeohashTable table = new GeohashTable(client, "geohash", options: null);

        GeoLocation center = new GeoLocation(44.9778d, -93.2650d);
        double radiusKm = 20.0;

        // === SpatialQueryAsyncResult for proximity queries on index ===
        var result = await table.Gsi1.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            spatialIndexType: SpatialIndexType.GeoHash,
            precision: 6,
            center: center,
            radiusKilometers: radiusKm,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100));

        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var response = result.Value;
            var items = response.Items;
        }

        // === With pagination and maxCells ===
        result = await table.Gsi1.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            spatialIndexType: SpatialIndexType.GeoHash,
            precision: 6,
            center: center,
            radiusKilometers: radiusKm,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100),
            pageSize: 25,
            maxCells: 50);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task SpatialQueryAsyncResult_IndexBoundingBox_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GeohashTable table = new GeohashTable(client, "geohash", options: null);

        var boundingBox = new GeoBoundingBox(
            southwest: new GeoLocation(44.0, -94.0),
            northeast: new GeoLocation(45.0, -93.0));

        // === SpatialQueryAsyncResult for bounding box queries on index ===
        var result = await table.Gsi1.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            spatialIndexType: SpatialIndexType.GeoHash,
            precision: 6,
            boundingBox: boundingBox,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100));

        // === With pagination ===
        result = await table.Gsi1.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            spatialIndexType: SpatialIndexType.GeoHash,
            precision: 6,
            boundingBox: boundingBox,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100),
            pageSize: 25,
            maxCells: 50);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task SpatialQueryAsyncResult_CustomCellList_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GeohashTable table = new GeohashTable(client, "geohash", options: null);

        GeoLocation center = new GeoLocation(44.9778d, -93.2650d);
        var cells = new[] { "9zvxyz", "9zvxyw", "9zvxyx" };

        // === SpatialQueryAsyncResult with custom cell list on table ===
        var result = await table.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            cells: cells,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100));

        // === With center and radius for distance filtering ===
        result = await table.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            cells: cells,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100),
            center: center,
            radiusKilometers: 20.0);

        // === SpatialQueryAsyncResult with custom cell list on index ===
        result = await table.Gsi1.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            cells: cells,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100));

        // === With pagination ===
        result = await table.Gsi1.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            cells: cells,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100),
            pageSize: 25);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task SpatialQueryAsyncResult_WithCancellationToken_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GeohashTable table = new GeohashTable(client, "geohash", options: null);
        var cancellationToken = new CancellationToken();

        GeoLocation center = new GeoLocation(44.9778d, -93.2650d);

        // === Table proximity with cancellation token ===
        var result = await table.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            spatialIndexType: SpatialIndexType.GeoHash,
            precision: 6,
            center: center,
            radiusKilometers: 20.0,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100),
            cancellationToken: cancellationToken);

        // === Index proximity with cancellation token ===
        result = await table.Gsi1.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            spatialIndexType: SpatialIndexType.GeoHash,
            precision: 6,
            center: center,
            radiusKilometers: 20.0,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100),
            cancellationToken: cancellationToken);

        // === Custom cell list with cancellation token ===
        var cells = new[] { "9zvxyz", "9zvxyw" };
        result = await table.SpatialQueryAsyncResult<GeoHashEntity>(
            locationSelector: e => e.Location,
            cells: cells,
            queryBuilder: (builder, cell, pagination) => builder
                .Where("gsi1pk = {0}", "category1")
                .Take(pagination.PageSize > 0 ? pagination.PageSize : 100),
            cancellationToken: cancellationToken);
    }
}
