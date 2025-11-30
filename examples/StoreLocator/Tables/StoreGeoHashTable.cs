using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using StoreLocator.Entities;

namespace StoreLocator.Tables;

/// <summary>
/// Table class for managing stores indexed with GeoHash spatial encoding.
/// </summary>
public class StoreGeoHashTable : DynamoDbTableBase
{
    public const string TableName = "stores-geohash";
    public DynamoDbIndex LocationIndex { get; }
    public int LastQueryCount { get; private set; }

    public StoreGeoHashTable(IAmazonDynamoDB client) : base(client, TableName)
    {
        LocationIndex = new DynamoDbIndex(this, "geohash-index");
    }

    public async Task<StoreGeoHash> AddStoreAsync(string storeId, string name, string address, GeoLocation location)
    {
        var store = new StoreGeoHash
        {
            StoreId = storeId,
            Category = "retail",
            Name = name,
            Address = address,
            Location = location
        };

        var item = StoreGeoHash.ToDynamoDb(store);
        await DynamoDbClient.PutItemAsync(Name, item);
        return store;
    }

    public async Task<List<(StoreGeoHash Store, double DistanceKm)>> FindStoresNearbyAsync(
        GeoLocation center,
        double radiusKilometers)
    {
        var result = await LocationIndex.SpatialQueryAsync<StoreGeoHash>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.GeoHash,
            precision: 7,
            center: center,
            radiusKilometers: radiusKilometers,
            queryBuilder: (query, cell, pagination) => query
                .Where("geohash_cell = {0}", cell),
            pageSize: null);

        LastQueryCount = result.TotalCellsQueried;

        return result.Items
            .Select(store => (Store: store, DistanceKm: store.Location.DistanceToKilometers(center)))
            .OrderBy(x => x.DistanceKm)
            .ToList();
    }

    public async Task<List<StoreGeoHash>> GetAllStoresAsync()
    {
        return await Scan<StoreGeoHash>().ToListAsync();
    }

    public async Task DeleteAllStoresAsync()
    {
        var stores = await GetAllStoresAsync();
        foreach (var store in stores)
        {
            await Delete<StoreGeoHash>()
                .WithKey("pk", store.StoreId)
                .WithKey("sk", store.Category)
                .DeleteAsync();
        }
    }
}
