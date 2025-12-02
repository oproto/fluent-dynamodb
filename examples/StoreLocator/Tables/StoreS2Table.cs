using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using StoreLocator.Entities;

namespace StoreLocator.Tables;

/// <summary>
/// Table class for managing stores indexed with Google's S2 geometry library.
/// Implements adaptive precision selection based on search radius.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Multi-Precision Strategy:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Fine (Level 14, ~284m): For radius ≤ 2km</description></item>
/// <item><description>Medium (Level 12, ~1.1km): For radius 2-10km</description></item>
/// <item><description>Coarse (Level 10, ~4.5km): For radius > 10km</description></item>
/// </list>
/// </remarks>
public class StoreS2Table : DynamoDbTableBase
{
    public const string TableName = "stores-s2";
    
    /// <summary>
    /// Fine precision index (S2 Level 14, ~284m cells) for nearby searches (radius ≤ 2km).
    /// </summary>
    public DynamoDbIndex FineIndex { get; }
    
    /// <summary>
    /// Medium precision index (S2 Level 12, ~1.1km cells) for city-level searches (radius 2-10km).
    /// </summary>
    public DynamoDbIndex MediumIndex { get; }
    
    /// <summary>
    /// Coarse precision index (S2 Level 10, ~4.5km cells) for regional searches (radius > 10km).
    /// </summary>
    public DynamoDbIndex CoarseIndex { get; }
    
    /// <summary>
    /// Gets the number of cells queried in the last spatial search.
    /// </summary>
    public int LastQueryCount { get; private set; }
    
    /// <summary>
    /// Gets the S2 level used in the last spatial search.
    /// </summary>
    public int LastS2Level { get; private set; }
    
    /// <summary>
    /// Gets the approximate cell size description used in the last spatial search.
    /// </summary>
    public string LastCellSize { get; private set; } = string.Empty;

    public StoreS2Table(IAmazonDynamoDB client) : base(client, TableName)
    {
        FineIndex = new DynamoDbIndex(this, "s2-index-fine");
        MediumIndex = new DynamoDbIndex(this, "s2-index-medium");
        CoarseIndex = new DynamoDbIndex(this, "s2-index-coarse");
    }

    /// <summary>
    /// Creates a new Scan operation builder for this table.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to scan.</typeparam>
    /// <returns>A ScanRequestBuilder configured for this table.</returns>
    public ScanRequestBuilder<TEntity> Scan<TEntity>() where TEntity : class =>
        new ScanRequestBuilder<TEntity>(DynamoDbClient).ForTable(Name);

    /// <summary>
    /// Adds a store with spatial indices at all three precision levels.
    /// </summary>
    public async Task<StoreS2> AddStoreAsync(string storeId, string name, string address, GeoLocation location)
    {
        var store = new StoreS2
        {
            StoreId = storeId,
            Category = "retail",
            Name = name,
            Address = address,
            // All three location properties use the same coordinates
            // The source generator computes different S2 cell tokens based on each property's S2Level
            Location = location,
            LocationMedium = location,
            LocationCoarse = location
        };

        var item = StoreS2.ToDynamoDb(store);
        await DynamoDbClient.PutItemAsync(Name, item);
        return store;
    }

    /// <summary>
    /// Finds stores near a location using adaptive precision selection.
    /// </summary>
    public async Task<List<(StoreS2 Store, double DistanceKm, string CellId)>> FindStoresNearbyAsync(
        GeoLocation center,
        double radiusKilometers)
    {
        // Select precision based on radius
        var (index, level, cellSize, cellAttribute) = SelectPrecision(radiusKilometers);
        LastS2Level = level;
        LastCellSize = cellSize;

        var result = await index.SpatialQueryAsync<StoreS2>(
            locationSelector: store => store.Location,
            spatialIndexType: SpatialIndexType.S2,
            precision: level,
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
    /// <returns>A tuple containing the index, S2 level, cell size description, and cell attribute name.</returns>
    public (DynamoDbIndex Index, int Level, string CellSize, string CellAttribute) SelectPrecision(double radiusKilometers)
    {
        return radiusKilometers switch
        {
            <= 2.0 => (FineIndex, 14, "~284m", "s2_cell_l14"),
            <= 10.0 => (MediumIndex, 12, "~1.1km", "s2_cell_l12"),
            _ => (CoarseIndex, 10, "~4.5km", "s2_cell_l10")
        };
    }

    /// <summary>
    /// Static method to select S2 level based on radius (for testing without table instance).
    /// </summary>
    public static int SelectS2Level(double radiusKilometers)
    {
        return radiusKilometers switch
        {
            <= 2.0 => 14,
            <= 10.0 => 12,
            _ => 10
        };
    }

    public async Task<List<StoreS2>> GetAllStoresAsync()
    {
        return await Scan<StoreS2>().ToListAsync();
    }

    public async Task DeleteAllStoresAsync()
    {
        var stores = await GetAllStoresAsync();
        foreach (var store in stores)
        {
            await Delete<StoreS2>()
                .WithKey("pk", store.StoreId)
                .WithKey("sk", store.Category)
                .DeleteAsync();
        }
    }
}
