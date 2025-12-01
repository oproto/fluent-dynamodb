// StoreLocator example application - demonstrates geospatial queries with FluentDynamoDb
// This example compares three spatial indexing approaches: GeoHash, S2, and H3

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Examples.Shared;
using Oproto.FluentDynamoDb.Geospatial;
using StoreLocator.Data;
using StoreLocator.Tables;

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         StoreLocator - FluentDynamoDb Example              ║");
Console.WriteLine("║                                                            ║");
Console.WriteLine("║  Demonstrates: Geospatial queries, Index comparison        ║");
Console.WriteLine("║                Adaptive precision, GeoHash/S2/H3           ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Initialize DynamoDB Local connection
ConsoleHelpers.ShowInfo("Connecting to DynamoDB Local...");
var client = DynamoDbSetup.CreateLocalClient();

// Ensure tables exist (idempotent)
ConsoleHelpers.ShowInfo("Ensuring tables exist...");
await EnsureTablesExistAsync();

// Validate GSIs exist on all tables
ConsoleHelpers.ShowInfo("Validating GSI indexes...");
var missingGsis = await ValidateGSIsAsync();
if (missingGsis.Count > 0)
{
    ConsoleHelpers.ShowWarning("Missing GSIs detected:");
    foreach (var issue in missingGsis)
        Console.WriteLine($"  - {issue}");
    Console.WriteLine();
    Console.Write("Tables need to be recreated to add missing GSIs. Recreate tables? (y/n): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (confirm == "y" || confirm == "yes")
    {
        await RecreateTablesAsync();
        ConsoleHelpers.ShowSuccess("Tables recreated with all required GSIs");
    }
    else
    {
        ConsoleHelpers.ShowWarning("Continuing with missing GSIs - some queries may fail");
    }
}

// Create table instances
var geoHashTable = new StoreGeoHashTable(client);
var s2Table = new StoreS2Table(client);
var h3Table = new StoreH3Table(client);

// Default search location: San Francisco downtown
var defaultCenter = new GeoLocation(37.7879, -122.4074);
var defaultRadius = 5.0; // km

// Main menu loop
while (true)
{
    var choice = ConsoleHelpers.ShowMenu(
        "Store Locator Menu",
        "Seed Store Data",
        "Search with GeoHash",
        "Search with S2",
        "Search with H3",
        "Compare All Index Types",
        "Clear All Data",
        "Exit");

    try
    {
        switch (choice)
        {
            case 1:
                await SeedDataAsync();
                break;
            case 2:
                await SearchGeoHashAsync();
                break;
            case 3:
                await SearchS2Async();
                break;
            case 4:
                await SearchH3Async();
                break;
            case 5:
                await CompareAllAsync();
                break;
            case 6:
                await ClearDataAsync();
                break;
            case 7:
                ConsoleHelpers.ShowInfo("Goodbye!");
                return;
            case 0:
                // Invalid selection - menu already showed error
                break;
        }
    }
    catch (Exception ex)
    {
        ConsoleHelpers.ShowError(ex, "Operation failed");
    }
}

/// <summary>
/// Ensures all three spatial index tables exist.
/// </summary>
async Task EnsureTablesExistAsync()
{
    // GeoHash table with GSI for spatial queries
    var created1 = await DynamoDbSetup.EnsureTableExistsAsync(
        client,
        StoreGeoHashTable.TableName,
        "pk",
        "sk",
        new List<GlobalSecondaryIndex>
        {
            CreateGsi("geohash-index", "geohash_cell", "pk")
        });
    if (created1) ConsoleHelpers.ShowSuccess($"Created table '{StoreGeoHashTable.TableName}'");

    // S2 table with GSIs for multi-precision spatial queries
    // Fine (Level 14, ~284m), Medium (Level 12, ~1.1km), Coarse (Level 10, ~4.5km)
    var created2 = await DynamoDbSetup.EnsureTableExistsAsync(
        client,
        StoreS2Table.TableName,
        "pk",
        "sk",
        new List<GlobalSecondaryIndex>
        {
            CreateGsi("s2-index-fine", "s2_cell_l14", "pk"),
            CreateGsi("s2-index-medium", "s2_cell_l12", "pk"),
            CreateGsi("s2-index-coarse", "s2_cell_l10", "pk")
        });
    if (created2) ConsoleHelpers.ShowSuccess($"Created table '{StoreS2Table.TableName}' with 3 precision GSIs");

    // H3 table with GSIs for multi-precision spatial queries
    // Fine (Resolution 9, ~174m), Medium (Resolution 7, ~1.2km), Coarse (Resolution 5, ~8.5km)
    var created3 = await DynamoDbSetup.EnsureTableExistsAsync(
        client,
        StoreH3Table.TableName,
        "pk",
        "sk",
        new List<GlobalSecondaryIndex>
        {
            CreateGsi("h3-index-fine", "h3_cell_r9", "pk"),
            CreateGsi("h3-index-medium", "h3_cell_r7", "pk"),
            CreateGsi("h3-index-coarse", "h3_cell_r5", "pk")
        });
    if (created3) ConsoleHelpers.ShowSuccess($"Created table '{StoreH3Table.TableName}' with 3 precision GSIs");
}

/// <summary>
/// Validates that all required GSIs exist on each table.
/// </summary>
/// <returns>List of missing GSI descriptions, or empty if all present.</returns>
async Task<List<string>> ValidateGSIsAsync()
{
    var issues = new List<string>();
    
    try
    {
        // Check S2 table GSIs
        var s2Description = await client.DescribeTableAsync(StoreS2Table.TableName);
        var s2Gsis = s2Description.Table.GlobalSecondaryIndexes?.Select(g => g.IndexName).ToList() ?? new List<string>();
        if (!s2Gsis.Contains("s2-index-fine")) issues.Add("S2 table missing s2-index-fine");
        if (!s2Gsis.Contains("s2-index-medium")) issues.Add("S2 table missing s2-index-medium");
        if (!s2Gsis.Contains("s2-index-coarse")) issues.Add("S2 table missing s2-index-coarse");
    }
    catch (ResourceNotFoundException)
    {
        // Table doesn't exist yet, will be created with GSIs
    }
    
    try
    {
        // Check H3 table GSIs
        var h3Description = await client.DescribeTableAsync(StoreH3Table.TableName);
        var h3Gsis = h3Description.Table.GlobalSecondaryIndexes?.Select(g => g.IndexName).ToList() ?? new List<string>();
        if (!h3Gsis.Contains("h3-index-fine")) issues.Add("H3 table missing h3-index-fine");
        if (!h3Gsis.Contains("h3-index-medium")) issues.Add("H3 table missing h3-index-medium");
        if (!h3Gsis.Contains("h3-index-coarse")) issues.Add("H3 table missing h3-index-coarse");
    }
    catch (ResourceNotFoundException)
    {
        // Table doesn't exist yet, will be created with GSIs
    }
    
    try
    {
        // Check GeoHash table GSI
        var geoHashDescription = await client.DescribeTableAsync(StoreGeoHashTable.TableName);
        var geoHashGsis = geoHashDescription.Table.GlobalSecondaryIndexes?.Select(g => g.IndexName).ToList() ?? new List<string>();
        if (!geoHashGsis.Contains("geohash-index")) issues.Add("GeoHash table missing geohash-index");
    }
    catch (ResourceNotFoundException)
    {
        // Table doesn't exist yet, will be created with GSIs
    }
    
    return issues;
}

/// <summary>
/// Deletes a table if it exists and waits for deletion to complete.
/// </summary>
async Task DeleteTableIfExistsAsync(string tableName)
{
    try
    {
        ConsoleHelpers.ShowInfo($"Deleting table '{tableName}'...");
        await client.DeleteTableAsync(tableName);
        
        // Wait for table deletion to complete
        while (true)
        {
            try
            {
                await client.DescribeTableAsync(tableName);
                await Task.Delay(500); // Wait and check again
            }
            catch (ResourceNotFoundException)
            {
                // Table is deleted
                break;
            }
        }
        ConsoleHelpers.ShowSuccess($"Deleted table '{tableName}'");
    }
    catch (ResourceNotFoundException)
    {
        // Table doesn't exist, nothing to delete
        ConsoleHelpers.ShowInfo($"Table '{tableName}' does not exist, skipping deletion");
    }
}

/// <summary>
/// Recreates all three tables with correct GSI definitions.
/// </summary>
async Task RecreateTablesAsync()
{
    Console.WriteLine();
    ConsoleHelpers.ShowWarning("WARNING: This will delete all existing store data!");
    Console.Write("Are you sure you want to continue? (y/n): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (confirm != "y" && confirm != "yes")
    {
        ConsoleHelpers.ShowInfo("Table recreation cancelled");
        return;
    }
    
    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Deleting existing tables...");
    
    // Delete all three tables
    await DeleteTableIfExistsAsync(StoreGeoHashTable.TableName);
    await DeleteTableIfExistsAsync(StoreS2Table.TableName);
    await DeleteTableIfExistsAsync(StoreH3Table.TableName);
    
    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Recreating tables with correct GSIs...");
    
    // Recreate with correct GSIs
    await EnsureTablesExistAsync();
    
    Console.WriteLine();
}

/// <summary>
/// Creates a GSI definition for table creation.
/// </summary>
GlobalSecondaryIndex CreateGsi(string indexName, string pkName, string skName)
{
    return new GlobalSecondaryIndex
    {
        IndexName = indexName,
        KeySchema = new List<KeySchemaElement>
        {
            new() { AttributeName = pkName, KeyType = KeyType.HASH },
            new() { AttributeName = skName, KeyType = KeyType.RANGE }
        },
        Projection = new Projection { ProjectionType = ProjectionType.ALL },
        ProvisionedThroughput = new ProvisionedThroughput
        {
            ReadCapacityUnits = 5,
            WriteCapacityUnits = 5
        }
    };
}

/// <summary>
/// Seeds store data into all three tables.
/// </summary>
async Task SeedDataAsync()
{
    ConsoleHelpers.ShowSection("Seed Store Data");
    
    // Check if data already exists
    var existingGeoHash = await geoHashTable.GetAllStoresAsync();
    if (existingGeoHash.Count > 0)
    {
        Console.Write($"Tables already contain {existingGeoHash.Count} stores. Reseed? (y/n): ");
        var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
        if (confirm != "y" && confirm != "yes")
        {
            ConsoleHelpers.ShowInfo("Seeding cancelled");
            return;
        }
        
        ConsoleHelpers.ShowInfo("Clearing existing data...");
        await geoHashTable.DeleteAllStoresAsync();
        await s2Table.DeleteAllStoresAsync();
        await h3Table.DeleteAllStoresAsync();
    }

    ConsoleHelpers.ShowInfo($"Seeding {StoreSeedData.StoreCount} stores in the San Francisco Bay Area...");
    
    int count = 0;
    foreach (var (storeId, name, address, location) in StoreSeedData.GetStores())
    {
        // Add to all three tables
        await geoHashTable.AddStoreAsync(storeId, name, address, location);
        await s2Table.AddStoreAsync(storeId, name, address, location);
        await h3Table.AddStoreAsync(storeId, name, address, location);
        
        count++;
        if (count % 10 == 0)
        {
            Console.Write($"\rSeeded {count} stores...");
        }
    }
    
    Console.WriteLine();
    ConsoleHelpers.ShowSuccess($"Seeded {count} stores into all three tables");
}

/// <summary>
/// Searches for stores using GeoHash indexing.
/// </summary>
async Task SearchGeoHashAsync()
{
    ConsoleHelpers.ShowSection("Search with GeoHash");
    
    var (center, radius) = GetSearchParameters();
    
    ConsoleHelpers.ShowInfo($"Searching within {radius}km of ({center.Latitude:F4}, {center.Longitude:F4})...");
    
    var startTime = DateTime.UtcNow;
    var results = await geoHashTable.FindStoresNearbyAsync(center, radius);
    var elapsed = DateTime.UtcNow - startTime;
    
    DisplaySearchResults(
        results.Select(r => (r.Store.Name, r.Store.Address, r.DistanceKm, r.Store.Location.SpatialIndex ?? "N/A")).ToList(),
        "GeoHash",
        geoHashTable.LastQueryCount,
        elapsed,
        "Precision: 7 (~76m cells)");
}

/// <summary>
/// Searches for stores using S2 indexing.
/// </summary>
async Task SearchS2Async()
{
    ConsoleHelpers.ShowSection("Search with S2");
    
    var (center, radius) = GetSearchParameters();
    
    ConsoleHelpers.ShowInfo($"Searching within {radius}km of ({center.Latitude:F4}, {center.Longitude:F4})...");
    
    var startTime = DateTime.UtcNow;
    var results = await s2Table.FindStoresNearbyAsync(center, radius);
    var elapsed = DateTime.UtcNow - startTime;
    
    DisplaySearchResults(
        results.Select(r => (r.Store.Name, r.Store.Address, r.DistanceKm, r.CellId)).ToList(),
        "S2",
        s2Table.LastQueryCount,
        elapsed,
        $"Level: {s2Table.LastS2Level}, Cell Size: {s2Table.LastCellSize} (adaptive)");
}

/// <summary>
/// Searches for stores using H3 indexing.
/// </summary>
async Task SearchH3Async()
{
    ConsoleHelpers.ShowSection("Search with H3");
    
    var (center, radius) = GetSearchParameters();
    
    ConsoleHelpers.ShowInfo($"Searching within {radius}km of ({center.Latitude:F4}, {center.Longitude:F4})...");
    
    var startTime = DateTime.UtcNow;
    var results = await h3Table.FindStoresNearbyAsync(center, radius);
    var elapsed = DateTime.UtcNow - startTime;
    
    DisplaySearchResults(
        results.Select(r => (r.Store.Name, r.Store.Address, r.DistanceKm, r.CellId)).ToList(),
        "H3",
        h3Table.LastQueryCount,
        elapsed,
        $"Resolution: {h3Table.LastH3Resolution}, Cell Size: {h3Table.LastCellSize} (adaptive)");
}

/// <summary>
/// Compares all three index types with the same search parameters.
/// </summary>
async Task CompareAllAsync()
{
    ConsoleHelpers.ShowSection("Compare All Index Types");
    
    var (center, radius) = GetSearchParameters();
    
    ConsoleHelpers.ShowInfo($"Comparing searches within {radius}km of ({center.Latitude:F4}, {center.Longitude:F4})...");
    Console.WriteLine();
    
    // GeoHash search
    var startGeoHash = DateTime.UtcNow;
    var geoHashResults = await geoHashTable.FindStoresNearbyAsync(center, radius);
    var elapsedGeoHash = DateTime.UtcNow - startGeoHash;
    
    // S2 search
    var startS2 = DateTime.UtcNow;
    var s2Results = await s2Table.FindStoresNearbyAsync(center, radius);
    var elapsedS2 = DateTime.UtcNow - startS2;
    
    // H3 search
    var startH3 = DateTime.UtcNow;
    var h3Results = await h3Table.FindStoresNearbyAsync(center, radius);
    var elapsedH3 = DateTime.UtcNow - startH3;
    
    // Display comparison table with cell size information
    Console.WriteLine("┌──────────────────────────────────────────────────────────────────────────────┐");
    Console.WriteLine("│                         Index Type Comparison                                │");
    Console.WriteLine("├─────────────┬──────────┬──────────┬──────────┬─────────────┬────────────────┤");
    Console.WriteLine("│ Index Type  │ Results  │ Queries  │ Time     │ Precision   │ Cell Size      │");
    Console.WriteLine("├─────────────┼──────────┼──────────┼──────────┼─────────────┼────────────────┤");
    Console.WriteLine($"│ GeoHash     │ {geoHashResults.Count,8} │ {geoHashTable.LastQueryCount,8} │ {elapsedGeoHash.TotalMilliseconds,6:F0}ms │ P7 (fixed)  │ ~76m           │");
    Console.WriteLine($"│ S2          │ {s2Results.Count,8} │ {s2Table.LastQueryCount,8} │ {elapsedS2.TotalMilliseconds,6:F0}ms │ L{s2Table.LastS2Level} (adapt) │ {s2Table.LastCellSize,-14} │");
    Console.WriteLine($"│ H3          │ {h3Results.Count,8} │ {h3Table.LastQueryCount,8} │ {elapsedH3.TotalMilliseconds,6:F0}ms │ R{h3Table.LastH3Resolution} (adapt) │ {h3Table.LastCellSize,-14} │");
    Console.WriteLine("└─────────────┴──────────┴──────────┴──────────┴─────────────┴────────────────┘");
    Console.WriteLine();
    
    // Show adaptive precision explanation
    Console.WriteLine("Adaptive Precision Selection:");
    Console.WriteLine($"  Search radius: {radius}km");
    Console.WriteLine($"  S2: Level {s2Table.LastS2Level} ({s2Table.LastCellSize} cells) - " + GetPrecisionReason(radius, "S2"));
    Console.WriteLine($"  H3: Resolution {h3Table.LastH3Resolution} ({h3Table.LastCellSize} cells) - " + GetPrecisionReason(radius, "H3"));
    Console.WriteLine();
    
    // Show top 5 results from each
    Console.WriteLine("Top 5 results from each index type:");
    Console.WriteLine();
    
    Console.WriteLine("GeoHash results:");
    foreach (var (store, dist) in geoHashResults.Take(5))
    {
        Console.WriteLine($"  {dist:F2}km - {store.Name}");
    }
    
    Console.WriteLine();
    Console.WriteLine("S2 results:");
    foreach (var (store, dist, _) in s2Results.Take(5))
    {
        Console.WriteLine($"  {dist:F2}km - {store.Name}");
    }
    
    Console.WriteLine();
    Console.WriteLine("H3 results:");
    foreach (var (store, dist, _) in h3Results.Take(5))
    {
        Console.WriteLine($"  {dist:F2}km - {store.Name}");
    }
    
    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Note: Query counts vary based on cell covering algorithms and precision levels.");
}

/// <summary>
/// Gets a human-readable explanation for why a precision level was selected.
/// </summary>
string GetPrecisionReason(double radiusKm, string indexType)
{
    return radiusKm switch
    {
        <= 2.0 => "Fine precision for nearby search (≤2km)",
        <= 10.0 => "Medium precision for city-level search (2-10km)",
        _ => "Coarse precision for regional search (>10km)"
    };
}

/// <summary>
/// Clears all store data from all tables.
/// </summary>
async Task ClearDataAsync()
{
    ConsoleHelpers.ShowSection("Clear All Data");
    
    Console.Write("Are you sure you want to delete all store data? (y/n): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (confirm != "y" && confirm != "yes")
    {
        ConsoleHelpers.ShowInfo("Clear cancelled");
        return;
    }
    
    ConsoleHelpers.ShowInfo("Clearing data from all tables...");
    
    await geoHashTable.DeleteAllStoresAsync();
    await s2Table.DeleteAllStoresAsync();
    await h3Table.DeleteAllStoresAsync();
    
    ConsoleHelpers.ShowSuccess("All store data cleared");
}

/// <summary>
/// Gets search parameters from user input.
/// </summary>
(GeoLocation Center, double Radius) GetSearchParameters()
{
    Console.WriteLine($"Default: San Francisco downtown ({defaultCenter.Latitude:F4}, {defaultCenter.Longitude:F4}), {defaultRadius}km radius");
    Console.Write("Use defaults? (y/n): ");
    var useDefaults = Console.ReadLine()?.Trim().ToLowerInvariant();
    
    if (useDefaults == "y" || useDefaults == "yes" || string.IsNullOrWhiteSpace(useDefaults))
    {
        return (defaultCenter, defaultRadius);
    }
    
    // Get custom parameters
    var latStr = ConsoleHelpers.GetInput("Enter latitude (e.g., 37.7879)", required: false);
    var lat = string.IsNullOrWhiteSpace(latStr) ? defaultCenter.Latitude : double.Parse(latStr);
    
    var lonStr = ConsoleHelpers.GetInput("Enter longitude (e.g., -122.4074)", required: false);
    var lon = string.IsNullOrWhiteSpace(lonStr) ? defaultCenter.Longitude : double.Parse(lonStr);
    
    var radiusStr = ConsoleHelpers.GetInput("Enter radius in km (e.g., 5)", required: false);
    var radius = string.IsNullOrWhiteSpace(radiusStr) ? defaultRadius : double.Parse(radiusStr);
    
    return (new GeoLocation(lat, lon), radius);
}

/// <summary>
/// Displays search results in a formatted table.
/// </summary>
void DisplaySearchResults(
    List<(string Name, string Address, double DistanceKm, string CellId)> results,
    string indexType,
    int queryCount,
    TimeSpan elapsed,
    string precisionInfo)
{
    Console.WriteLine();
    Console.WriteLine($"Index Type: {indexType}");
    Console.WriteLine($"Queries Executed: {queryCount}");
    Console.WriteLine($"Execution Time: {elapsed.TotalMilliseconds:F0}ms");
    Console.WriteLine($"{precisionInfo}");
    Console.WriteLine();
    
    if (results.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No stores found within the search radius.");
        return;
    }
    
    ConsoleHelpers.DisplayTable(
        results,
        ("Distance", r => $"{r.DistanceKm:F2} km"),
        ("Name", r => TruncateString(r.Name, 25)),
        ("Address", r => TruncateString(r.Address, 35)),
        ("Cell ID", r => TruncateString(r.CellId, 15)));
    
    ConsoleHelpers.ShowInfo($"Found {results.Count} stores");
}

/// <summary>
/// Truncates a string to the specified maximum length, adding ellipsis if truncated.
/// </summary>
static string TruncateString(string value, int maxLength)
{
    if (string.IsNullOrEmpty(value))
        return string.Empty;
    
    return value.Length <= maxLength 
        ? value 
        : value[..(maxLength - 3)] + "...";
}
