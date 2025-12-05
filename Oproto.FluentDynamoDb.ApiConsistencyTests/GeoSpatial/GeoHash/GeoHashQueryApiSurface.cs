using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Geospatial.GeoHash;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.GeoSpatial.GeoHash;

public class GeoHashQueryApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllGeoHashQueryPatterns_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        GeohashTable table = new GeohashTable(client, "geohash", options: null);

        GeoLocation center = new GeoLocation(44.9778d, 93.2650d);

        var results = await table.gsi1.Query<GeoHashEntity>()
            .Where(x => x.Gsi1PartitionKey == "category1"
                                      && x.Location.WithinDistanceKilometers(center, 20))
            .ToListAsync();
    }
}