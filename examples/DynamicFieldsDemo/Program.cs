// DynamicFieldsDemo - Demonstrates dynamic fields support in FluentDynamoDb
// This example shows how to work with custom attributes that aren't defined in the entity class

using DynamicFieldsDemo.Entities;
using Examples.Shared;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;

// Table name as external configuration
const string TableName = "products";

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║        DynamicFieldsDemo - FluentDynamoDb Example          ║");
Console.WriteLine("║                                                            ║");
Console.WriteLine("║  Demonstrates: Dynamic fields for custom attributes        ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Initialize DynamoDB Local connection
ConsoleHelpers.ShowInfo("Connecting to DynamoDB Local...");
var client = DynamoDbSetup.CreateLocalClient();

// Ensure table exists with pk and sk
ConsoleHelpers.ShowInfo("Ensuring table exists...");
var created = await DynamoDbSetup.EnsureTableExistsAsync(
    client,
    TableName,
    "pk",
    "sk");

if (created)
{
    ConsoleHelpers.ShowSuccess($"Created table '{TableName}'");
}
else
{
    ConsoleHelpers.ShowInfo($"Table '{TableName}' already exists");
}

// Create table instance
var table = new ProductsTable(client, TableName);

// Main menu loop
while (true)
{
    var choice = ConsoleHelpers.ShowMenu(
        "Dynamic Fields Demo",
        "Add Product with Custom Fields",
        "List All Products",
        "Get Product by ID",
        "Update Dynamic Fields",
        "Remove Dynamic Field",
        "Filter by Dynamic Field",
        "Update with ChangesOnly()",
        "Seed Sample Data",
        "Exit");

    try
    {
        switch (choice)
        {
            case 1:
                await AddProductAsync(table);
                break;
            case 2:
                await ListProductsAsync(table);
                break;
            case 3:
                await GetProductAsync(table);
                break;
            case 4:
                await UpdateDynamicFieldsAsync(table);
                break;
            case 5:
                await RemoveDynamicFieldAsync(table);
                break;
            case 6:
                await FilterByDynamicFieldAsync(table);
                break;
            case 7:
                await UpdateWithChangesOnlyAsync(table);
                break;
            case 8:
                await SeedSampleDataAsync(table);
                break;
            case 9:
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
/// Demonstrates creating a product with dynamic fields using typed setters.
/// Requirements: 4.1, 4.2
/// </summary>
static async Task AddProductAsync(ProductsTable table)
{
    ConsoleHelpers.ShowSection("Add Product with Custom Fields");
    
    var name = ConsoleHelpers.GetInput("Product name");
    if (string.IsNullOrWhiteSpace(name)) return;

    var priceInput = ConsoleHelpers.GetDecimalInput("Price");
    if (!priceInput.HasValue) return;

    var category = ConsoleHelpers.GetInput("Category");
    if (string.IsNullOrWhiteSpace(category)) return;

    var productId = Guid.NewGuid().ToString()[..8];
    var product = new Product
    {
        Pk = Product.Keys.Pk(productId),
        Sk = "META",
        Name = name,
        Price = priceInput.Value,
        Category = category,
        CreatedAt = DateTime.UtcNow
    };

    // Add custom fields using typed setters
    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Now add custom fields (press Enter with empty name to finish):");
    
    while (true)
    {
        var fieldName = ConsoleHelpers.GetInput("Custom field name (or Enter to finish)", required: false);
        if (string.IsNullOrWhiteSpace(fieldName)) break;

        var fieldType = ConsoleHelpers.ShowMenu(
            "Field Type",
            "String",
            "Number (int)",
            "Decimal",
            "Boolean",
            "Date");

        switch (fieldType)
        {
            case 1:
                var strValue = ConsoleHelpers.GetInput("String value");
                if (!string.IsNullOrWhiteSpace(strValue))
                    product.DynamicFields.SetString(fieldName, strValue);
                break;
            case 2:
                var intValue = ConsoleHelpers.GetIntInput("Integer value");
                if (intValue.HasValue)
                    product.DynamicFields.SetInt(fieldName, intValue.Value);
                break;
            case 3:
                var decValue = ConsoleHelpers.GetDecimalInput("Decimal value");
                if (decValue.HasValue)
                    product.DynamicFields.SetDecimal(fieldName, decValue.Value);
                break;
            case 4:
                Console.Write("Boolean value (true/false): ");
                if (bool.TryParse(Console.ReadLine(), out var boolValue))
                    product.DynamicFields.SetBool(fieldName, boolValue);
                break;
            case 5:
                product.DynamicFields.SetDateTime(fieldName, DateTime.UtcNow);
                ConsoleHelpers.ShowInfo($"Set {fieldName} to current UTC time");
                break;
        }
    }

    // Save the product with all dynamic fields
    await table.Products.PutAsync(product);
    
    ConsoleHelpers.ShowSuccess($"Created product '{name}' with ID: {productId}");
    if (product.DynamicFields.Count > 0)
    {
        ConsoleHelpers.ShowInfo($"Custom fields: {string.Join(", ", product.DynamicFields.FieldNames)}");
    }
}

/// <summary>
/// Demonstrates reading all products and accessing dynamic fields with typed getters.
/// Requirements: 3.3, 2.1
/// </summary>
static async Task ListProductsAsync(ProductsTable table)
{
    ConsoleHelpers.ShowSection("All Products");
    
    // Scan all products - dynamic fields are automatically populated
    var products = await table.Products.Scan().ToListAsync();
    
    if (products.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No products found. Try 'Seed Sample Data' first!");
        return;
    }

    foreach (var product in products)
    {
        Console.WriteLine();
        Console.WriteLine($"  Product: {product.Name}");
        Console.WriteLine($"  ID: {product.Pk}");
        Console.WriteLine($"  Price: ${product.Price:F2}");
        Console.WriteLine($"  Category: {product.Category}");
        
        // Display dynamic fields with type detection
        if (product.DynamicFields.Count > 0)
        {
            Console.WriteLine("  Custom Fields:");
            foreach (var fieldName in product.DynamicFields.FieldNames)
            {
                var fieldType = product.DynamicFields.GetFieldType(fieldName);
                var displayValue = GetDisplayValue(product.DynamicFields, fieldName, fieldType);
                Console.WriteLine($"    - {fieldName} ({fieldType}): {displayValue}");
            }
        }
        Console.WriteLine($"  {new string('-', 40)}");
    }

    ConsoleHelpers.ShowInfo($"Total: {products.Count} products");
}

/// <summary>
/// Demonstrates GetItem with dynamic fields.
/// Requirements: 3.1
/// </summary>
static async Task GetProductAsync(ProductsTable table)
{
    ConsoleHelpers.ShowSection("Get Product by ID");
    
    var productId = ConsoleHelpers.GetInput("Enter product ID (8 chars)");
    if (string.IsNullOrWhiteSpace(productId)) return;

    var pk = Product.Keys.Pk(productId);
    var product = await table.Products.Get(pk, "META").GetItemAsync();
    
    if (product == null)
    {
        ConsoleHelpers.ShowError($"Product '{productId}' not found");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"  Name: {product.Name}");
    Console.WriteLine($"  Price: ${product.Price:F2}");
    Console.WriteLine($"  Category: {product.Category}");
    Console.WriteLine($"  Created: {product.CreatedAt:yyyy-MM-dd HH:mm}");
    
    // Demonstrate typed accessors for dynamic fields
    if (product.DynamicFields.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("  Dynamic Fields (using typed accessors):");
        
        foreach (var fieldName in product.DynamicFields.FieldNames)
        {
            var fieldType = product.DynamicFields.GetFieldType(fieldName);
            
            // Demonstrate TryGet pattern for safe access
            switch (fieldType)
            {
                case DynamicFieldType.String:
                    if (product.DynamicFields.TryGetString(fieldName, out var strVal))
                        Console.WriteLine($"    {fieldName}: \"{strVal}\"");
                    break;
                case DynamicFieldType.Number:
                    // Try different numeric types
                    if (product.DynamicFields.TryGetDecimal(fieldName, out var decVal))
                        Console.WriteLine($"    {fieldName}: {decVal}");
                    break;
                case DynamicFieldType.Boolean:
                    if (product.DynamicFields.TryGetBool(fieldName, out var boolVal))
                        Console.WriteLine($"    {fieldName}: {boolVal}");
                    break;
                case DynamicFieldType.DateTime:
                    if (product.DynamicFields.TryGetDateTime(fieldName, out var dtVal))
                        Console.WriteLine($"    {fieldName}: {dtVal:yyyy-MM-dd HH:mm}");
                    break;
                default:
                    var raw = product.DynamicFields.GetRaw(fieldName);
                    Console.WriteLine($"    {fieldName}: {raw}");
                    break;
            }
        }
    }
    else
    {
        ConsoleHelpers.ShowInfo("No custom fields on this product");
    }
}

/// <summary>
/// Demonstrates updating dynamic fields using UpdateItem.
/// Requirements: 5.1
/// </summary>
static async Task UpdateDynamicFieldsAsync(ProductsTable table)
{
    ConsoleHelpers.ShowSection("Update Dynamic Fields");
    
    var productId = ConsoleHelpers.GetInput("Enter product ID (8 chars)");
    if (string.IsNullOrWhiteSpace(productId)) return;

    var pk = Product.Keys.Pk(productId);
    var product = await table.Products.Get(pk, "META").GetItemAsync();
    
    if (product == null)
    {
        ConsoleHelpers.ShowError($"Product '{productId}' not found");
        return;
    }

    Console.WriteLine($"Updating: {product.Name}");
    if (product.DynamicFields.Count > 0)
    {
        Console.WriteLine($"Current custom fields: {string.Join(", ", product.DynamicFields.FieldNames)}");
    }

    var fieldName = ConsoleHelpers.GetInput("Field name to set/update");
    if (string.IsNullOrWhiteSpace(fieldName)) return;

    var fieldType = ConsoleHelpers.ShowMenu(
        "Field Type",
        "String",
        "Number (int)",
        "Decimal",
        "Boolean");

    // Create a DynamicFieldCollection with the change
    var changes = new DynamicFieldCollection();
    
    switch (fieldType)
    {
        case 1:
            var strValue = ConsoleHelpers.GetInput("String value");
            if (!string.IsNullOrWhiteSpace(strValue))
            {
                changes.SetString(fieldName, strValue);
            }
            break;
        case 2:
            var intValue = ConsoleHelpers.GetIntInput("Integer value");
            if (intValue.HasValue)
            {
                changes.SetInt(fieldName, intValue.Value);
            }
            break;
        case 3:
            var decValue = ConsoleHelpers.GetDecimalInput("Decimal value");
            if (decValue.HasValue)
            {
                changes.SetDecimal(fieldName, decValue.Value);
            }
            break;
        case 4:
            Console.Write("Boolean value (true/false): ");
            if (bool.TryParse(Console.ReadLine(), out var boolValue))
            {
                changes.SetBool(fieldName, boolValue);
            }
            break;
    }

    // Update using the DynamicFields property on the update model
    if (changes.Count > 0)
    {
        await table.Products.Update(pk, "META")
            .Set(x => new ProductUpdateModel { DynamicFields = changes })
            .UpdateAsync();
        
        ConsoleHelpers.ShowSuccess($"Updated field '{fieldName}' on product '{product.Name}'");
    }
}

/// <summary>
/// Demonstrates removing a dynamic field using UpdateItem REMOVE.
/// Requirements: 5.2
/// </summary>
static async Task RemoveDynamicFieldAsync(ProductsTable table)
{
    ConsoleHelpers.ShowSection("Remove Dynamic Field");
    
    var productId = ConsoleHelpers.GetInput("Enter product ID (8 chars)");
    if (string.IsNullOrWhiteSpace(productId)) return;

    var pk = Product.Keys.Pk(productId);
    var product = await table.Products.Get(pk, "META").GetItemAsync();
    
    if (product == null)
    {
        ConsoleHelpers.ShowError($"Product '{productId}' not found");
        return;
    }

    if (product.DynamicFields.Count == 0)
    {
        ConsoleHelpers.ShowInfo("This product has no custom fields to remove");
        return;
    }

    Console.WriteLine($"Product: {product.Name}");
    Console.WriteLine($"Custom fields: {string.Join(", ", product.DynamicFields.FieldNames)}");

    var fieldName = ConsoleHelpers.GetInput("Field name to remove");
    if (string.IsNullOrWhiteSpace(fieldName)) return;

    if (!product.DynamicFields.ContainsKey(fieldName))
    {
        ConsoleHelpers.ShowError($"Field '{fieldName}' not found on this product");
        return;
    }

    // Create a DynamicFieldCollection with the removal tracked
    var changes = new DynamicFieldCollection();
    changes.Remove(fieldName);

    // Update using the DynamicFields property on the update model
    await table.Products.Update(pk, "META")
        .Set(x => new ProductUpdateModel { DynamicFields = changes })
        .UpdateAsync();

    ConsoleHelpers.ShowSuccess($"Removed field '{fieldName}' from product '{product.Name}'");
}

/// <summary>
/// Demonstrates using ChangesOnly() for efficient updates that only send modified fields.
/// Requirements: 11.4
/// </summary>
static async Task UpdateWithChangesOnlyAsync(ProductsTable table)
{
    ConsoleHelpers.ShowSection("Update with ChangesOnly()");
    
    var productId = ConsoleHelpers.GetInput("Enter product ID (8 chars)");
    if (string.IsNullOrWhiteSpace(productId)) return;

    var pk = Product.Keys.Pk(productId);
    var product = await table.Products.Get(pk, "META").GetItemAsync();
    
    if (product == null)
    {
        ConsoleHelpers.ShowError($"Product '{productId}' not found");
        return;
    }

    Console.WriteLine($"Loaded: {product.Name}");
    Console.WriteLine($"Price: ${product.Price:F2}");
    if (product.DynamicFields.Count > 0)
    {
        Console.WriteLine($"Current custom fields: {string.Join(", ", product.DynamicFields.FieldNames)}");
    }
    Console.WriteLine();
    
    // Show that change tracking starts after loading
    ConsoleHelpers.ShowInfo("Change tracking is now active. Let's make some changes...");
    Console.WriteLine();
    
    // Make some changes to demonstrate tracking
    Console.WriteLine("Making changes:");
    
    // Add/modify a field
    var newFieldName = ConsoleHelpers.GetInput("Field name to add/modify (or Enter to skip)", required: false);
    if (!string.IsNullOrWhiteSpace(newFieldName))
    {
        var newValue = ConsoleHelpers.GetInput($"Value for '{newFieldName}'");
        if (!string.IsNullOrWhiteSpace(newValue))
        {
            product.DynamicFields.SetString(newFieldName, newValue);
            Console.WriteLine($"  + Set '{newFieldName}' = \"{newValue}\"");
        }
    }
    
    // Remove a field
    if (product.DynamicFields.Count > 0)
    {
        var removeFieldName = ConsoleHelpers.GetInput("Field name to remove (or Enter to skip)", required: false);
        if (!string.IsNullOrWhiteSpace(removeFieldName) && product.DynamicFields.ContainsKey(removeFieldName))
        {
            product.DynamicFields.Remove(removeFieldName);
            Console.WriteLine($"  - Marked '{removeFieldName}' for removal");
        }
    }
    
    // Check if there are changes
    if (!product.DynamicFields.HasChanges)
    {
        ConsoleHelpers.ShowInfo("No changes were made.");
        return;
    }
    
    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Change tracking summary:");
    Console.WriteLine($"  HasChanges: {product.DynamicFields.HasChanges}");
    Console.WriteLine($"  Fields to remove: {string.Join(", ", product.DynamicFields.RemovedFields)}");
    
    // Use ChangesOnly() to get only the modified fields
    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Using ChangesOnly() to update only modified fields...");
    
    var changes = product.DynamicFields.ChangesOnly();
    Console.WriteLine($"  Changes collection has {changes.Count} field(s) to SET");
    Console.WriteLine($"  Changes collection has {changes.RemovedFields.Count} field(s) to REMOVE");
    
    // Perform the update using the update model with DynamicFields
    await table.Products.Update(pk, "META")
        .Set(x => new ProductUpdateModel 
        { 
            DynamicFields = changes
        })
        .UpdateAsync();
    
    ConsoleHelpers.ShowSuccess("Update completed with only the changed fields!");
    
    // Verify the changes
    Console.WriteLine();
    ConsoleHelpers.ShowInfo("Verifying changes...");
    var updated = await table.Products.Get(pk, "META").GetItemAsync();
    if (updated != null)
    {
        Console.WriteLine($"  Current fields: {string.Join(", ", updated.DynamicFields.FieldNames)}");
    }
    
    // Show that tracking was reset
    Console.WriteLine();
    Console.WriteLine($"  Original product HasChanges after ChangesOnly(): {product.DynamicFields.HasChanges}");
    ConsoleHelpers.ShowInfo("(Change tracking was reset on the original collection)");
}

/// <summary>
/// Demonstrates filtering by dynamic field values in Scan operations.
/// Requirements: 6.1, 6.3
/// </summary>
static async Task FilterByDynamicFieldAsync(ProductsTable table)
{
    ConsoleHelpers.ShowSection("Filter by Dynamic Field");
    
    var fieldName = ConsoleHelpers.GetInput("Dynamic field name to filter by");
    if (string.IsNullOrWhiteSpace(fieldName)) return;

    var filterType = ConsoleHelpers.ShowMenu(
        "Filter Type",
        "Equals (string)",
        "Greater than (number)",
        "Field exists",
        "Field does not exist");

    List<Product> results;
    
    switch (filterType)
    {
        case 1:
            var strValue = ConsoleHelpers.GetInput("Value to match");
            if (string.IsNullOrWhiteSpace(strValue)) return;
            
            // Filter using lambda expression with dynamic field - natural string comparison
            results = await table.Products.Scan()
                .WithFilter(x => x.DynamicFields[fieldName] == strValue)
                .ToListAsync();
            break;
            
        case 2:
            var numValue = ConsoleHelpers.GetIntInput("Minimum value");
            if (!numValue.HasValue) return;
            
            // Filter using lambda expression with dynamic field - natural numeric comparison
            results = await table.Products.Scan()
                .WithFilter(x => x.DynamicFields[fieldName] > numValue.Value)
                .ToListAsync();
            break;
            
        case 3:
            // Filter for field existence using Exists method
            results = await table.Products.Scan()
                .WithFilter(x => x.DynamicFields.Exists(fieldName))
                .ToListAsync();
            break;
            
        case 4:
            // Filter for field non-existence using NotExists method
            results = await table.Products.Scan()
                .WithFilter(x => x.DynamicFields.NotExists(fieldName))
                .ToListAsync();
            break;
            
        default:
            return;
    }

    if (results.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No products match the filter");
        return;
    }

    Console.WriteLine();
    ConsoleHelpers.ShowSuccess($"Found {results.Count} matching products:");
    
    foreach (var product in results)
    {
        Console.WriteLine($"  - {product.Name} (${product.Price:F2})");
        if (product.DynamicFields.ContainsKey(fieldName))
        {
            var fieldType = product.DynamicFields.GetFieldType(fieldName);
            var value = GetDisplayValue(product.DynamicFields, fieldName, fieldType);
            Console.WriteLine($"    {fieldName}: {value}");
        }
    }
}

/// <summary>
/// Seeds sample data with various dynamic fields to demonstrate the feature.
/// </summary>
static async Task SeedSampleDataAsync(ProductsTable table)
{
    ConsoleHelpers.ShowSection("Seeding Sample Data");
    
    var products = new[]
    {
        // Clothing product with size/color
        CreateProduct("T-Shirt", 29.99m, "Clothing", new Dictionary<string, object>
        {
            ["size"] = "L",
            ["color"] = "Blue",
            ["material"] = "Cotton"
        }),
        
        // Electronics product with warranty/specs
        CreateProduct("Wireless Mouse", 49.99m, "Electronics", new Dictionary<string, object>
        {
            ["warranty_months"] = 24,
            ["weight_grams"] = 85,
            ["wireless"] = true
        }),
        
        // Food product with nutrition info
        CreateProduct("Organic Honey", 12.99m, "Food", new Dictionary<string, object>
        {
            ["weight_oz"] = 16,
            ["organic"] = true,
            ["origin"] = "New Zealand"
        }),
        
        // Book with metadata
        CreateProduct("C# Programming Guide", 45.00m, "Books", new Dictionary<string, object>
        {
            ["author"] = "John Smith",
            ["pages"] = 450,
            ["isbn"] = "978-1234567890"
        }),
        
        // Product with minimal custom fields
        CreateProduct("Generic Widget", 9.99m, "Misc", new Dictionary<string, object>
        {
            ["sku"] = "WDG-001"
        }),
        
        // Product with no custom fields
        CreateProduct("Basic Item", 5.00m, "Misc", new Dictionary<string, object>())
    };

    foreach (var product in products)
    {
        await table.Products.PutAsync(product);
        var customFieldCount = product.DynamicFields.Count;
        ConsoleHelpers.ShowSuccess($"Created: {product.Name} ({customFieldCount} custom fields)");
    }

    ConsoleHelpers.ShowInfo($"Seeded {products.Length} sample products");
}

/// <summary>
/// Helper to create a product with dynamic fields.
/// </summary>
static Product CreateProduct(string name, decimal price, string category, Dictionary<string, object> customFields)
{
    var productId = Guid.NewGuid().ToString()[..8];
    var product = new Product
    {
        Pk = Product.Keys.Pk(productId),
        Sk = "META",
        Name = name,
        Price = price,
        Category = category,
        CreatedAt = DateTime.UtcNow
    };

    foreach (var (fieldName, value) in customFields)
    {
        switch (value)
        {
            case string s:
                product.DynamicFields.SetString(fieldName, s);
                break;
            case int i:
                product.DynamicFields.SetInt(fieldName, i);
                break;
            case decimal d:
                product.DynamicFields.SetDecimal(fieldName, d);
                break;
            case bool b:
                product.DynamicFields.SetBool(fieldName, b);
                break;
            case DateTime dt:
                product.DynamicFields.SetDateTime(fieldName, dt);
                break;
        }
    }

    return product;
}

/// <summary>
/// Helper to get a display-friendly value from a dynamic field.
/// </summary>
static string GetDisplayValue(DynamicFieldCollection fields, string fieldName, DynamicFieldType fieldType)
{
    return fieldType switch
    {
        DynamicFieldType.String => fields.GetString(fieldName) ?? "(null)",
        DynamicFieldType.Number => fields.GetDecimal(fieldName)?.ToString() ?? "(null)",
        DynamicFieldType.Boolean => fields.GetBool(fieldName)?.ToString() ?? "(null)",
        DynamicFieldType.DateTime => fields.GetDateTime(fieldName)?.ToString("yyyy-MM-dd HH:mm") ?? "(null)",
        DynamicFieldType.Null => "(null)",
        _ => "(complex type)"
    };
}
