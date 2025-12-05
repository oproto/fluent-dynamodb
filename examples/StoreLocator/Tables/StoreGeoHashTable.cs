using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Geospatial.GeoHash;
using Oproto.FluentDynamoDb.Requests;
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

    /// <summary>
    /// Creates a new Scan operation builder for this table.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to scan.</typeparam>
    /// <returns>A ScanRequestBuilder configured for this table.</returns>
    public ScanRequestBuilder<TEntity> Scan<TEntity>() where TEntity : class =>
        new ScanRequestBuilder<TEntity>(DynamoDbClient).ForTable(Name);

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
        // GeoHash always uses a single BETWEEN query (unlike S2/H3 which use multiple discrete cell queries)
        LastQueryCount = 1;
        
        // Use lambda expression with WithinDistanceKilometers which translates to a DynamoDB BETWEEN query
        var results = await LocationIndex.Query<StoreGeoHash>()
            .Where(x => x.Location.WithinDistanceKilometers(center, radiusKilometers))
            .ToListAsync();
        
        // Post-filter by exact distance (BETWEEN returns rectangular approximation, not circular)
        // and sort results by distance
        return results
            .Select(store => (Store: store, DistanceKm: store.Location.DistanceToKilometers(center)))
            .Where(x => x.DistanceKm <= radiusKilometers)
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
