using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using StoreLocator.Entities;

namespace StoreLocator.Tables;

/// <summary>
/// Table class for managing stores indexed with Uber's H3 hexagonal spatial index.
/// Implements adaptive precision selection based on search radius.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Multi-Precision Strategy:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Fine (Resolution 9, ~174m): For radius ≤ 2km</description></item>
/// <item><description>Medium (Resolution 7, ~1.2km): For radius 2-10km</description></item>
/// <item><description>Coarse (Resolution 5, ~8.5km): For radius > 10km</description></item>
/// </list>
/// </remarks>
public class StoreH3Table : DynamoDbTableBase
{
    public const string TableName = "stores-h3";
    
    /// <summary>
    /// Fine precision index (H3 Resolution 9, ~174m cells) for nearby searches (radius ≤ 2km).
    /// </summary>
    public DynamoDbIndex FineIndex { get; }
    
    /// <summary>
    /// Medium precision index (H3 Resolution 7, ~1.2km cells) for city-level searches (radius 2-10km).
    /// </summary>
    public DynamoDbIndex MediumIndex { get; }
    
    /// <summary>
    /// Coarse precision index (H3 Resolution 5, ~8.5km cells) for regional searches (radius > 10km).
    /// </summary>
    public DynamoDbIndex CoarseIndex { get; }
    
    /// <summary>
    /// Gets the number of cells queried in the last spatial search.
    /// </summary>
    public int LastQueryCount { get; private set; }
    
    /// <summary>
    /// Gets the H3 resolution used in the last spatial search.
    /// </summary>
    public int LastH3Resolution { get; private set; }

    /// <summary>
    /// Gets the approximate cell size description used in the last spatial search.
    /// </summary>
    public string LastCellSize { get; private set; } = string.Empty;

    public StoreH3Table(IAmazonDynamoDB client) : base(client, TableName)
    {
        FineIndex = new DynamoDbIndex(this, "h3-index-fine");
        MediumIndex = new DynamoDbIndex(this, "h3-index-medium");
        CoarseIndex = new DynamoDbIndex(this, "h3-index-coarse");
    }

    /// <summary>
    /// Adds a store with spatial indices at all three precision levels.
    /// </summary>
    public async Task<StoreH3> AddStoreAsync(string storeId, string name, string address, GeoLocation location)
    {
        var store = new StoreH3
        {
            StoreId = storeId,
            Category = "retail",
            Name = name,
            Address = address,
            // All three location properties use the same coordinates
            // The source generator computes different H3 cell indices based on each property's H3Resolution
            Location = location,
            LocationMedium = location,
            LocationCoarse = location
        };

        var item = StoreH3.ToDynamoDb(store);
        await DynamoDbClient.PutItemAsync(Name, item);
        return store;
    }

    /// <summary>
    /// Finds stores near a location using adaptive precision selection.
    /// </summary>
    public async Task<List<(StoreH3 Store, double DistanceKm, string CellId)>> FindStoresNearbyAsync(
        GeoLocation center,
        double radiusKilometers)
    {
        // Select precision based on radius
        var (index, resolution, cellSize, cellAttribute) = SelectPrecision(radiusKilometers);
        LastH3Resolution = resolution;
        LastCellSize = cellSize;

        var result = await index.SpatialQueryAsync<StoreH3>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.H3,
            precision: resolution,
            center: center,
            radiusKilometers: radiusKilometers,
            queryBuilder: (query, cell, pagination) => query
                .Where($"{cellAttribute} = {{0}}", cell),
            pageSize: null);

        LastQueryCount = result.TotalCellsQueried;

        return result.Items
            .Select(store => (
                Store: store,
                DistanceKm: store.Location.DistanceToKilometers(center),
                CellId: store.Location.SpatialIndex ?? "unknown"))
            .OrderBy(x => x.DistanceKm)
            .ToList();
    }

    /// <summary>
    /// Selects the appropriate precision level based on search radius.
    /// </summary>
    /// <param name="radiusKilometers">The search radius in kilometers.</param>
    /// <returns>A tuple containing the index, H3 resolution, cell size description, and cell attribute name.</returns>
    public (DynamoDbIndex Index, int Resolution, string CellSize, string CellAttribute) SelectPrecision(double radiusKilometers)
    {
        return radiusKilometers switch
        {
            <= 2.0 => (FineIndex, 9, "~174m", "h3_cell_r9"),
            <= 10.0 => (MediumIndex, 7, "~1.2km", "h3_cell_r7"),
            _ => (CoarseIndex, 5, "~8.5km", "h3_cell_r5")
        };
    }

    /// <summary>
    /// Static method to select H3 resolution based on radius (for testing without table instance).
    /// </summary>
    public static int SelectH3Resolution(double radiusKilometers)
    {
        return radiusKilometers switch
        {
            <= 2.0 => 9,
            <= 10.0 => 7,
            _ => 5
        };
    }

    public async Task<List<StoreH3>> GetAllStoresAsync()
    {
        return await Scan<StoreH3>().ToListAsync();
    }

    public async Task DeleteAllStoresAsync()
    {
        var stores = await GetAllStoresAsync();
        foreach (var store in stores)
        {
            await Delete<StoreH3>()
                .WithKey("pk", store.StoreId)
                .WithKey("sk", store.Category)
                .DeleteAsync();
        }
    }
}
