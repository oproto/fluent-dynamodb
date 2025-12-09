// JsonBlobDemo example application - demonstrates JSON blob serialization with FluentDynamoDb
// This example shows how to use [JsonBlob] properties with different JSON serializers

using System.Text.Json;
using Examples.Shared;
using JsonBlobDemo;
using JsonBlobDemo.Entities;
using Newtonsoft.Json;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.NewtonsoftJson;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.SystemTextJson;

// Alias for the generated table class
using DocumentsTable = JsonBlobDemo.Entities.JsonBlobDemoTable;

// Table name as external configuration - in real apps this would come from
// environment variables, configuration files, or other external sources
const string TableName = "json-blob-demo";

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         JsonBlobDemo - FluentDynamoDb Example              ║");
Console.WriteLine("║                                                            ║");
Console.WriteLine("║  Demonstrates: JsonBlob serialization with multiple        ║");
Console.WriteLine("║  serializers (System.Text.Json AOT, Reflection, Newtonsoft)║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Initialize DynamoDB Local connection
ConsoleHelpers.ShowInfo("Connecting to DynamoDB Local...");
var client = DynamoDbSetup.CreateLocalClient();

// Ensure table exists (idempotent)
ConsoleHelpers.ShowInfo("Ensuring table exists...");
var created = await DynamoDbSetup.EnsureTableExistsAsync(
    client,
    TableName,
    "pk");

if (created)
{
    ConsoleHelpers.ShowSuccess($"Created table '{TableName}'");
}
else
{
    ConsoleHelpers.ShowInfo($"Table '{TableName}' already exists");
}

// Current serializer configuration
SerializerType currentSerializer = SerializerType.SystemTextJsonAot;
FluentDynamoDbOptions currentOptions = CreateOptions(currentSerializer);
var table = new DocumentsTable(client, TableName, currentOptions);

// Main menu loop
while (true)
{
    var choice = ConsoleHelpers.ShowMenu(
        $"JsonBlob Demo Menu (Current: {GetSerializerName(currentSerializer)})",
        "Switch to System.Text.Json (AOT Context)",
        "Switch to System.Text.Json (Reflection)",
        "Switch to Newtonsoft.Json",
        "Create Document",
        "List All Documents",
        "View Document Details",
        "Update Document Metadata",
        "Delete Document",
        "Exit");

    try
    {
        switch (choice)
        {
            case 1:
                currentSerializer = SerializerType.SystemTextJsonAot;
                currentOptions = CreateOptions(currentSerializer);
                table = new DocumentsTable(client, TableName, currentOptions);
                ConsoleHelpers.ShowSuccess("Switched to System.Text.Json with AOT Context");
                ShowSerializerInfo(currentSerializer);
                break;
            case 2:
                currentSerializer = SerializerType.SystemTextJsonReflection;
                currentOptions = CreateOptions(currentSerializer);
                table = new DocumentsTable(client, TableName, currentOptions);
                ConsoleHelpers.ShowSuccess("Switched to System.Text.Json with Reflection");
                ShowSerializerInfo(currentSerializer);
                break;
            case 3:
                currentSerializer = SerializerType.NewtonsoftJson;
                currentOptions = CreateOptions(currentSerializer);
                table = new DocumentsTable(client, TableName, currentOptions);
                ConsoleHelpers.ShowSuccess("Switched to Newtonsoft.Json");
                ShowSerializerInfo(currentSerializer);
                break;
            case 4:
                await CreateDocumentAsync(table);
                break;
            case 5:
                await ListDocumentsAsync(table);
                break;
            case 6:
                await ViewDocumentDetailsAsync(table);
                break;
            case 7:
                await UpdateDocumentMetadataAsync(table);
                break;
            case 8:
                await DeleteDocumentAsync(table);
                break;
            case 9:
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
/// Creates FluentDynamoDbOptions with the specified serializer.
/// </summary>
static FluentDynamoDbOptions CreateOptions(SerializerType serializerType)
{
    var options = new FluentDynamoDbOptions();
    
    return serializerType switch
    {
        SerializerType.SystemTextJsonAot => options.WithSystemTextJson(DocumentJsonContext.Default),
        SerializerType.SystemTextJsonReflection => options.WithSystemTextJson(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        }),
        SerializerType.NewtonsoftJson => options.WithNewtonsoftJson(new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(serializerType))
    };
}

/// <summary>
/// Gets a display name for the serializer type.
/// </summary>
static string GetSerializerName(SerializerType serializerType)
{
    return serializerType switch
    {
        SerializerType.SystemTextJsonAot => "System.Text.Json (AOT)",
        SerializerType.SystemTextJsonReflection => "System.Text.Json (Reflection)",
        SerializerType.NewtonsoftJson => "Newtonsoft.Json",
        _ => "Unknown"
    };
}

/// <summary>
/// Shows information about the current serializer.
/// </summary>
static void ShowSerializerInfo(SerializerType serializerType)
{
    Console.WriteLine();
    switch (serializerType)
    {
        case SerializerType.SystemTextJsonAot:
            Console.WriteLine("  AOT Mode: Uses DocumentJsonContext for source-generated serialization.");
            Console.WriteLine("  Benefits: No runtime reflection, works with Native AOT and trimming.");
            Console.WriteLine("  Trade-off: Must register all types in the JsonSerializerContext.");
            break;
        case SerializerType.SystemTextJsonReflection:
            Console.WriteLine("  Reflection Mode: Uses JsonSerializerOptions with runtime reflection.");
            Console.WriteLine("  Benefits: Flexible, no type registration needed.");
            Console.WriteLine("  Trade-off: Not compatible with Native AOT or aggressive trimming.");
            break;
        case SerializerType.NewtonsoftJson:
            Console.WriteLine("  Newtonsoft.Json: Popular JSON library with extensive features.");
            Console.WriteLine("  Benefits: Rich feature set, wide ecosystem support.");
            Console.WriteLine("  Trade-off: Uses reflection, not AOT-compatible.");
            break;
    }
    Console.WriteLine();
}

/// <summary>
/// Creates a new document with sample metadata.
/// </summary>
static async Task CreateDocumentAsync(DocumentsTable table)
{
    ConsoleHelpers.ShowSection("Create New Document");
    
    var title = ConsoleHelpers.GetInput("Enter document title");
    if (string.IsNullOrWhiteSpace(title))
        return;

    var author = ConsoleHelpers.GetInput("Enter author name");
    if (string.IsNullOrWhiteSpace(author))
        return;

    Console.Write("Enter tags (comma-separated, or press Enter to skip): ");
    var tagsInput = Console.ReadLine();
    var tags = string.IsNullOrWhiteSpace(tagsInput) 
        ? new List<string>() 
        : tagsInput.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();

    Console.Write("Enter category (or press Enter to skip): ");
    var category = Console.ReadLine()?.Trim();

    var priority = ConsoleHelpers.GetIntInput("Enter priority (1-5)", 1, 5) ?? 3;

    var document = new Document
    {
        Id = Guid.NewGuid().ToString(),
        Title = title,
        CreatedAt = DateTime.UtcNow,
        Metadata = new DocumentMetadata
        {
            Author = author,
            Tags = tags,
            CustomFields = new Dictionary<string, string>
            {
                ["source"] = "JsonBlobDemo",
                ["version"] = "1.0"
            },
            AdditionalInfo = string.IsNullOrEmpty(category) ? null : new NestedInfo
            {
                Category = category,
                Priority = priority
            }
        }
    };

    // Store the document - the Metadata property will be serialized as JSON
    await table.Documents.PutAsync(document);
    
    ConsoleHelpers.ShowSuccess($"Created document with ID: {document.Id[..8]}...");
    Console.WriteLine();
    Console.WriteLine("  The Metadata property was serialized as JSON:");
    Console.WriteLine($"  - Author: {document.Metadata.Author}");
    Console.WriteLine($"  - Tags: [{string.Join(", ", document.Metadata.Tags)}]");
    Console.WriteLine($"  - CustomFields: {document.Metadata.CustomFields.Count} entries");
    if (document.Metadata.AdditionalInfo != null)
    {
        Console.WriteLine($"  - AdditionalInfo.Category: {document.Metadata.AdditionalInfo.Category}");
        Console.WriteLine($"  - AdditionalInfo.Priority: {document.Metadata.AdditionalInfo.Priority}");
    }
}

/// <summary>
/// Lists all documents in the table.
/// </summary>
static async Task ListDocumentsAsync(DocumentsTable table)
{
    ConsoleHelpers.ShowSection("All Documents");
    
    var documents = await table.Documents.Scan().ToListAsync();
    
    var documentList = documents.ToList();
    
    if (documentList.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No documents found. Create some!");
        return;
    }

    ConsoleHelpers.DisplayTable(
        documentList,
        ("ID (first 8)", doc => doc.Id[..Math.Min(8, doc.Id.Length)]),
        ("Title", doc => TruncateString(doc.Title, 25)),
        ("Author", doc => TruncateString(doc.Metadata.Author, 15)),
        ("Tags", doc => doc.Metadata.Tags.Count.ToString()),
        ("Created", doc => doc.CreatedAt.ToString("yyyy-MM-dd HH:mm")));

    ConsoleHelpers.ShowInfo($"Total: {documentList.Count} documents");
}

/// <summary>
/// Views detailed information about a specific document.
/// </summary>
static async Task ViewDocumentDetailsAsync(DocumentsTable table)
{
    ConsoleHelpers.ShowSection("View Document Details");
    
    var documentList = await table.Documents.Scan().ToListAsync();
    
    if (documentList.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No documents to view.");
        return;
    }

    Console.WriteLine("Available documents:");
    ConsoleHelpers.DisplayTable(
        documentList,
        ("ID (first 8)", doc => doc.Id[..Math.Min(8, doc.Id.Length)]),
        ("Title", doc => TruncateString(doc.Title, 40)));

    var id = ConsoleHelpers.GetInput("Enter document ID (or first 8 chars)");
    if (string.IsNullOrWhiteSpace(id))
        return;

    var document = documentList.FirstOrDefault(d => d.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase));
    if (document == null)
    {
        ConsoleHelpers.ShowError($"No document found matching '{id}'");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"  Document ID: {document.Id}");
    Console.WriteLine($"  Title: {document.Title}");
    Console.WriteLine($"  Created: {document.CreatedAt:yyyy-MM-dd HH:mm:ss}");
    if (document.UpdatedAt.HasValue)
        Console.WriteLine($"  Updated: {document.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
    Console.WriteLine();
    Console.WriteLine("  Metadata (deserialized from JSON):");
    Console.WriteLine($"    Author: {document.Metadata.Author}");
    Console.WriteLine($"    Tags: [{string.Join(", ", document.Metadata.Tags)}]");
    Console.WriteLine("    CustomFields:");
    foreach (var kvp in document.Metadata.CustomFields)
    {
        Console.WriteLine($"      {kvp.Key}: {kvp.Value}");
    }
    if (document.Metadata.AdditionalInfo != null)
    {
        Console.WriteLine("    AdditionalInfo:");
        Console.WriteLine($"      Category: {document.Metadata.AdditionalInfo.Category}");
        Console.WriteLine($"      Priority: {document.Metadata.AdditionalInfo.Priority}");
    }
    else
    {
        Console.WriteLine("    AdditionalInfo: (none)");
    }
}

/// <summary>
/// Updates the metadata of an existing document.
/// </summary>
static async Task UpdateDocumentMetadataAsync(DocumentsTable table)
{
    ConsoleHelpers.ShowSection("Update Document Metadata");
    
    var documentList = await table.Documents.Scan().ToListAsync();
    
    if (documentList.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No documents to update.");
        return;
    }

    Console.WriteLine("Available documents:");
    ConsoleHelpers.DisplayTable(
        documentList,
        ("ID (first 8)", doc => doc.Id[..Math.Min(8, doc.Id.Length)]),
        ("Title", doc => TruncateString(doc.Title, 40)));

    var id = ConsoleHelpers.GetInput("Enter document ID (or first 8 chars)");
    if (string.IsNullOrWhiteSpace(id))
        return;

    var document = documentList.FirstOrDefault(d => d.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase));
    if (document == null)
    {
        ConsoleHelpers.ShowError($"No document found matching '{id}'");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"Current metadata for '{document.Title}':");
    Console.WriteLine($"  Author: {document.Metadata.Author}");
    Console.WriteLine($"  Tags: [{string.Join(", ", document.Metadata.Tags)}]");
    Console.WriteLine();

    Console.Write($"Enter new author (or press Enter to keep '{document.Metadata.Author}'): ");
    var newAuthor = Console.ReadLine()?.Trim();
    if (!string.IsNullOrEmpty(newAuthor))
    {
        document.Metadata.Author = newAuthor;
    }

    Console.Write("Add a new tag (or press Enter to skip): ");
    var newTag = Console.ReadLine()?.Trim();
    if (!string.IsNullOrEmpty(newTag) && !document.Metadata.Tags.Contains(newTag))
    {
        document.Metadata.Tags.Add(newTag);
    }

    Console.Write("Add a custom field? Enter key=value (or press Enter to skip): ");
    var customField = Console.ReadLine()?.Trim();
    if (!string.IsNullOrEmpty(customField) && customField.Contains('='))
    {
        var parts = customField.Split('=', 2);
        document.Metadata.CustomFields[parts[0].Trim()] = parts[1].Trim();
    }

    document.UpdatedAt = DateTime.UtcNow;

    // Update the document - the entire Metadata object is re-serialized
    await table.Documents.PutAsync(document);
    
    ConsoleHelpers.ShowSuccess("Document metadata updated successfully");
    Console.WriteLine();
    Console.WriteLine("  Updated metadata:");
    Console.WriteLine($"    Author: {document.Metadata.Author}");
    Console.WriteLine($"    Tags: [{string.Join(", ", document.Metadata.Tags)}]");
    Console.WriteLine($"    CustomFields: {document.Metadata.CustomFields.Count} entries");
}

/// <summary>
/// Deletes a document from the table.
/// </summary>
static async Task DeleteDocumentAsync(DocumentsTable table)
{
    ConsoleHelpers.ShowSection("Delete Document");
    
    var documentList = await table.Documents.Scan().ToListAsync();
    
    if (documentList.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No documents to delete.");
        return;
    }

    Console.WriteLine("Available documents:");
    ConsoleHelpers.DisplayTable(
        documentList,
        ("ID (first 8)", doc => doc.Id[..Math.Min(8, doc.Id.Length)]),
        ("Title", doc => TruncateString(doc.Title, 40)));

    var id = ConsoleHelpers.GetInput("Enter document ID (or first 8 chars)");
    if (string.IsNullOrWhiteSpace(id))
        return;

    var document = documentList.FirstOrDefault(d => d.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase));
    if (document == null)
    {
        ConsoleHelpers.ShowError($"No document found matching '{id}'");
        return;
    }

    Console.Write($"Are you sure you want to delete '{document.Title}'? (y/n): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (confirm != "y" && confirm != "yes")
    {
        ConsoleHelpers.ShowInfo("Delete cancelled");
        return;
    }

    await table.Documents.DeleteAsync(document.Id);
    ConsoleHelpers.ShowSuccess("Document deleted successfully");
}

/// <summary>
/// Truncates a string to the specified maximum length.
/// </summary>
static string TruncateString(string value, int maxLength)
{
    if (string.IsNullOrEmpty(value))
        return string.Empty;
    
    return value.Length <= maxLength 
        ? value 
        : value[..(maxLength - 3)] + "...";
}

/// <summary>
/// Serializer type enumeration.
/// </summary>
enum SerializerType
{
    SystemTextJsonAot,
    SystemTextJsonReflection,
    NewtonsoftJson
}
