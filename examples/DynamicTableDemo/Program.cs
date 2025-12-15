// DynamicTableDemo - demonstrates schema-less access to DynamoDB tables
// This example shows how to use DynamicTable and DynamicEntity for working with
// tables without defining entity classes

using Amazon.DynamoDBv2;
using Examples.Shared;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

const string TableName = "dynamic-demo";

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║        DynamicTableDemo - FluentDynamoDb Example           ║");
Console.WriteLine("║                                                            ║");
Console.WriteLine("║  Demonstrates: Schema-less access with DynamicTable        ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Initialize DynamoDB Local connection
ConsoleHelpers.ShowInfo("Connecting to DynamoDB Local...");
var client = DynamoDbSetup.CreateLocalClient();

// Ensure table exists with composite key (pk + sk)
ConsoleHelpers.ShowInfo("Ensuring table exists...");
var created = await DynamoDbSetup.EnsureTableExistsAsync(
    client,
    TableName,
    partitionKeyName: "pk",
    sortKeyName: "sk");

if (created)
{
    ConsoleHelpers.ShowSuccess($"Created table '{TableName}'");
}
else
{
    ConsoleHelpers.ShowInfo($"Table '{TableName}' already exists");
}

// Create DynamicTable with key configuration
// This enables typed key methods (GetAsync, DeleteAsync, Update)
var keyOptions = new DynamicTableKeyOptions
{
    PartitionKeyName = "pk",
    PartitionKeyType = ScalarAttributeType.S,
    SortKeyName = "sk",
    SortKeyType = ScalarAttributeType.S
};

var table = new DynamicTable(client, TableName, keyOptions);

// Main menu loop
while (true)
{
    var choice = ConsoleHelpers.ShowMenu(
        "DynamicTable Demo Menu",
        "Add Item",
        "Get Item by Key",
        "Query Items",
        "Scan All Items",
        "Update Item",
        "Delete Item",
        "Seed Sample Data",
        "Exit");

    try
    {
        switch (choice)
        {
            case 1:
                await AddItemAsync(table);
                break;
            case 2:
                await GetItemAsync(table);
                break;
            case 3:
                await QueryItemsAsync(table);
                break;
            case 4:
                await ScanItemsAsync(table);
                break;
            case 5:
                await UpdateItemAsync(table);
                break;
            case 6:
                await DeleteItemAsync(table);
                break;
            case 7:
                await SeedSampleDataAsync(table);
                break;
            case 8:
                ConsoleHelpers.ShowInfo("Goodbye!");
                return;
            case 0:
                break;
        }
    }
    catch (Exception ex)
    {
        ConsoleHelpers.ShowError(ex, "Operation failed");
    }
}

/// <summary>
/// Adds a new item using DynamicEntity.
/// Demonstrates how to create and populate a DynamicEntity.
/// </summary>
static async Task AddItemAsync(DynamicTable table)
{
    ConsoleHelpers.ShowSection("Add New Item");
    
    var pk = ConsoleHelpers.GetInput("Enter partition key (e.g., USER#123)");
    if (string.IsNullOrWhiteSpace(pk)) return;
    
    var sk = ConsoleHelpers.GetInput("Enter sort key (e.g., PROFILE)");
    if (string.IsNullOrWhiteSpace(sk)) return;
    
    // Create a DynamicEntity and populate fields
    var entity = new DynamicEntity();
    entity.DynamicFields.SetString("pk", pk);
    entity.DynamicFields.SetString("sk", sk);
    
    // Add optional fields
    var name = ConsoleHelpers.GetInput("Enter name (optional)");
    if (!string.IsNullOrWhiteSpace(name))
        entity.DynamicFields.SetString("name", name);
    
    var ageStr = ConsoleHelpers.GetInput("Enter age (optional, numeric)");
    if (!string.IsNullOrWhiteSpace(ageStr) && int.TryParse(ageStr, out var age))
        entity.DynamicFields.SetInt("age", age);
    
    // Always add a timestamp
    entity.DynamicFields.SetDateTime("createdAt", DateTime.UtcNow);
    
    await table.PutAsync(entity);
    ConsoleHelpers.ShowSuccess($"Created item with pk={pk}, sk={sk}");
}

/// <summary>
/// Gets an item by its composite key.
/// Demonstrates typed key access with DynamicTable.
/// </summary>
static async Task GetItemAsync(DynamicTable table)
{
    ConsoleHelpers.ShowSection("Get Item by Key");
    
    var pk = ConsoleHelpers.GetInput("Enter partition key");
    if (string.IsNullOrWhiteSpace(pk)) return;
    
    var sk = ConsoleHelpers.GetInput("Enter sort key");
    if (string.IsNullOrWhiteSpace(sk)) return;
    
    // Use typed key method (enabled by DynamicTableKeyOptions)
    var item = await table.GetAsync(pk, sk);
    
    if (item == null)
    {
        ConsoleHelpers.ShowInfo("Item not found");
        return;
    }
    
    DisplayDynamicEntity(item);
}

/// <summary>
/// Queries items by partition key.
/// Demonstrates lambda expression queries with DynamicEntity.
/// </summary>
static async Task QueryItemsAsync(DynamicTable table)
{
    ConsoleHelpers.ShowSection("Query Items");
    
    var pk = ConsoleHelpers.GetInput("Enter partition key to query");
    if (string.IsNullOrWhiteSpace(pk)) return;
    
    // Query using lambda expression with DynamicFields indexer
    var items = await table.Query()
        .Where(x => x.DynamicFields["pk"] == pk)
        .ToListAsync();
    
    if (items.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No items found");
        return;
    }
    
    ConsoleHelpers.ShowSuccess($"Found {items.Count} item(s):");
    foreach (var item in items)
    {
        DisplayDynamicEntity(item);
        Console.WriteLine("---");
    }
}

/// <summary>
/// Scans all items in the table.
/// Demonstrates scan operations with optional filters.
/// </summary>
static async Task ScanItemsAsync(DynamicTable table)
{
    ConsoleHelpers.ShowSection("Scan All Items");
    
    var filterField = ConsoleHelpers.GetInput("Filter by field name (optional, press Enter to skip)");
    
    List<DynamicEntity> items;
    
    if (!string.IsNullOrWhiteSpace(filterField))
    {
        var filterValue = ConsoleHelpers.GetInput($"Filter value for '{filterField}'") ?? string.Empty;
        
        // Scan with filter using format string
        items = await table.Scan()
            .WithFilter("{0} = {1}", filterField, filterValue)
            .ToListAsync();
    }
    else
    {
        // Scan all items
        items = await table.Scan().ToListAsync();
    }
    
    if (items.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No items found");
        return;
    }
    
    ConsoleHelpers.ShowSuccess($"Found {items.Count} item(s):");
    foreach (var item in items)
    {
        DisplayDynamicEntity(item);
        Console.WriteLine("---");
    }
}

/// <summary>
/// Updates an existing item.
/// Demonstrates update operations with DynamicTable.
/// </summary>
static async Task UpdateItemAsync(DynamicTable table)
{
    ConsoleHelpers.ShowSection("Update Item");
    
    var pk = ConsoleHelpers.GetInput("Enter partition key of item to update");
    if (string.IsNullOrWhiteSpace(pk)) return;
    
    var sk = ConsoleHelpers.GetInput("Enter sort key of item to update");
    if (string.IsNullOrWhiteSpace(sk)) return;
    
    // First check if item exists
    var existing = await table.GetAsync(pk, sk);
    if (existing == null)
    {
        ConsoleHelpers.ShowError("Item not found");
        return;
    }
    
    Console.WriteLine("Current item:");
    DisplayDynamicEntity(existing);
    
    var fieldName = ConsoleHelpers.GetInput("Enter field name to update");
    if (string.IsNullOrWhiteSpace(fieldName)) return;
    
    var newValue = ConsoleHelpers.GetInput($"Enter new value for '{fieldName}'");
    if (string.IsNullOrWhiteSpace(newValue)) return;
    
    // Update using the builder pattern with format string
    await table.Update(pk, sk)
        .Set("{0} = {1}", fieldName, newValue)
        .Set("updatedAt = {0}", DateTime.UtcNow.ToString("o"))
        .UpdateAsync();
    
    ConsoleHelpers.ShowSuccess("Item updated successfully");
    
    // Show updated item
    var updated = await table.GetAsync(pk, sk);
    if (updated != null)
    {
        Console.WriteLine("Updated item:");
        DisplayDynamicEntity(updated);
    }
}

/// <summary>
/// Deletes an item by its composite key.
/// Demonstrates typed key delete with DynamicTable.
/// </summary>
static async Task DeleteItemAsync(DynamicTable table)
{
    ConsoleHelpers.ShowSection("Delete Item");
    
    var pk = ConsoleHelpers.GetInput("Enter partition key of item to delete");
    if (string.IsNullOrWhiteSpace(pk)) return;
    
    var sk = ConsoleHelpers.GetInput("Enter sort key of item to delete");
    if (string.IsNullOrWhiteSpace(sk)) return;
    
    // Check if item exists first
    var existing = await table.GetAsync(pk, sk);
    if (existing == null)
    {
        ConsoleHelpers.ShowError("Item not found");
        return;
    }
    
    Console.WriteLine("Item to delete:");
    DisplayDynamicEntity(existing);
    
    Console.Write("Are you sure you want to delete this item? (y/n): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (confirm != "y" && confirm != "yes")
    {
        ConsoleHelpers.ShowInfo("Delete cancelled");
        return;
    }
    
    // Use typed key method for delete
    await table.DeleteAsync(pk, sk);
    ConsoleHelpers.ShowSuccess("Item deleted successfully");
}

/// <summary>
/// Seeds the table with sample data.
/// Demonstrates bulk insert with various data types.
/// </summary>
static async Task SeedSampleDataAsync(DynamicTable table)
{
    ConsoleHelpers.ShowSection("Seed Sample Data");
    
    var sampleItems = new[]
    {
        CreateSampleEntity("USER#001", "PROFILE", "Alice", 28, "alice@example.com"),
        CreateSampleEntity("USER#001", "SETTINGS", null, null, null, new Dictionary<string, string>
        {
            ["theme"] = "dark",
            ["notifications"] = "enabled"
        }),
        CreateSampleEntity("USER#002", "PROFILE", "Bob", 35, "bob@example.com"),
        CreateSampleEntity("USER#002", "SETTINGS", null, null, null, new Dictionary<string, string>
        {
            ["theme"] = "light",
            ["notifications"] = "disabled"
        }),
        CreateSampleEntity("PRODUCT#001", "INFO", "Widget", null, null, null, 29.99m, 100),
        CreateSampleEntity("PRODUCT#002", "INFO", "Gadget", null, null, null, 49.99m, 50),
    };
    
    foreach (var item in sampleItems)
    {
        await table.PutAsync(item);
    }
    
    ConsoleHelpers.ShowSuccess($"Seeded {sampleItems.Length} sample items");
}

/// <summary>
/// Creates a sample DynamicEntity with various field types.
/// </summary>
static DynamicEntity CreateSampleEntity(
    string pk, 
    string sk, 
    string? name = null, 
    int? age = null, 
    string? email = null,
    Dictionary<string, string>? settings = null,
    decimal? price = null,
    int? quantity = null)
{
    var entity = new DynamicEntity();
    entity.DynamicFields.SetString("pk", pk);
    entity.DynamicFields.SetString("sk", sk);
    entity.DynamicFields.SetDateTime("createdAt", DateTime.UtcNow);
    
    if (name != null) entity.DynamicFields.SetString("name", name);
    if (age.HasValue) entity.DynamicFields.SetInt("age", age.Value);
    if (email != null) entity.DynamicFields.SetString("email", email);
    if (price.HasValue) entity.DynamicFields.SetDecimal("price", price.Value);
    if (quantity.HasValue) entity.DynamicFields.SetInt("quantity", quantity.Value);
    
    if (settings != null)
    {
        foreach (var (key, value) in settings)
        {
            entity.DynamicFields.SetString(key, value);
        }
    }
    
    return entity;
}

/// <summary>
/// Displays a DynamicEntity's fields in a readable format.
/// </summary>
static void DisplayDynamicEntity(DynamicEntity entity)
{
    Console.WriteLine("  Fields:");
    foreach (var fieldName in entity.DynamicFields.FieldNames.OrderBy(n => n))
    {
        var fieldType = entity.DynamicFields.GetFieldType(fieldName);
        var value = fieldType switch
        {
            DynamicFieldType.String => entity.DynamicFields.GetString(fieldName),
            DynamicFieldType.Number => entity.DynamicFields.GetDecimal(fieldName)?.ToString(),
            DynamicFieldType.Boolean => entity.DynamicFields.GetBool(fieldName)?.ToString(),
            DynamicFieldType.DateTime => entity.DynamicFields.GetDateTime(fieldName)?.ToString("o"),
            _ => $"[{fieldType}]"
        };
        Console.WriteLine($"    {fieldName}: {value}");
    }
}
