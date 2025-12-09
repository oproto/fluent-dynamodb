// S3BlobDemo example application - demonstrates S3 blob storage integration with FluentDynamoDb
// This example shows how to use [BlobReference] properties to store large data in S3

using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Examples.Shared;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.BlobStorage.S3;
using Oproto.FluentDynamoDb.Requests.Extensions;
using S3BlobDemo.Entities;

// Alias for the generated table class
using MediaTable = S3BlobDemo.Entities.S3BlobDemoTable;

// Table name as external configuration - in real apps this would come from
// environment variables, configuration files, or other external sources
const string TableName = "s3-blob-demo";

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          S3BlobDemo - FluentDynamoDb Example               ║");
Console.WriteLine("║                                                            ║");
Console.WriteLine("║  Demonstrates: S3 blob storage for large data with         ║");
Console.WriteLine("║  [BlobReference] attribute                                 ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Prompt for S3 configuration
ConsoleHelpers.ShowSection("S3 Configuration");
Console.WriteLine("  This demo stores binary data in S3 with references in DynamoDB.");
Console.WriteLine("  You need an S3 bucket and appropriate AWS credentials.");
Console.WriteLine();

var bucketName = ConsoleHelpers.GetInput("Enter S3 bucket name");
if (string.IsNullOrWhiteSpace(bucketName))
{
    ConsoleHelpers.ShowError("S3 bucket name is required. Exiting.");
    return;
}

Console.Write("Enter key prefix (optional, press Enter to skip): ");
var keyPrefix = Console.ReadLine()?.Trim();
if (string.IsNullOrEmpty(keyPrefix))
    keyPrefix = null;

Console.Write("Enter AWS profile name (optional, press Enter for default credentials): ");
var profileName = Console.ReadLine()?.Trim();

// Create S3 client with appropriate credentials
IAmazonS3 s3Client;
try
{
    s3Client = CreateS3Client(profileName);
    ConsoleHelpers.ShowSuccess("S3 client created successfully");
}
catch (Exception ex)
{
    ConsoleHelpers.ShowError(ex, "Failed to create S3 client");
    return;
}

// Verify bucket access
ConsoleHelpers.ShowInfo("Verifying S3 bucket access...");
try
{
    await s3Client.EnsureBucketExistsAsync(bucketName);
    ConsoleHelpers.ShowSuccess($"Bucket '{bucketName}' is accessible");
}
catch (Exception ex)
{
    ConsoleHelpers.ShowError(ex, "Failed to access S3 bucket");
    ConsoleHelpers.ShowWarning("Make sure the bucket exists and you have appropriate permissions.");
    ConsoleHelpers.ShowWarning("Required permissions: s3:PutObject, s3:GetObject, s3:DeleteObject, s3:HeadObject");
    return;
}

// Initialize DynamoDB Local connection
ConsoleHelpers.ShowInfo("Connecting to DynamoDB Local...");
var dynamoClient = DynamoDbSetup.CreateLocalClient();

// Ensure table exists (idempotent)
ConsoleHelpers.ShowInfo("Ensuring DynamoDB table exists...");
var created = await DynamoDbSetup.EnsureTableExistsAsync(
    dynamoClient,
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

// Create S3BlobProvider and configure options
var blobProvider = new S3BlobProvider(s3Client, bucketName, keyPrefix);
var options = new FluentDynamoDbOptions()
    .WithBlobStorage(blobProvider);

var table = new MediaTable(dynamoClient, TableName, options);

Console.WriteLine();
ConsoleHelpers.ShowInfo($"Configuration:");
Console.WriteLine($"  S3 Bucket: {bucketName}");
Console.WriteLine($"  Key Prefix: {keyPrefix ?? "(none)"}");
Console.WriteLine($"  DynamoDB Table: {TableName}");
Console.WriteLine();

// Main menu loop
while (true)
{
    var choice = ConsoleHelpers.ShowMenu(
        "S3 Blob Demo Menu",
        "Upload Media (from text)",
        "Upload Media (from file)",
        "List All Media Items",
        "Download Media",
        "Delete Media",
        "Exit");

    try
    {
        switch (choice)
        {
            case 1:
                await UploadTextMediaAsync(table, blobProvider);
                break;
            case 2:
                await UploadFileMediaAsync(table, blobProvider);
                break;
            case 3:
                await ListMediaItemsAsync(table);
                break;
            case 4:
                await DownloadMediaAsync(table, blobProvider);
                break;
            case 5:
                await DeleteMediaAsync(table, blobProvider);
                break;
            case 6:
                ConsoleHelpers.ShowInfo("Goodbye!");
                return;
            case 0:
                // Invalid selection - menu already showed error
                break;
        }
    }
    catch (Exception ex)
    {
        HandleS3Error(ex);
    }
}


/// <summary>
/// Creates an S3 client with the specified profile or default credentials.
/// </summary>
static IAmazonS3 CreateS3Client(string? profileName)
{
    if (!string.IsNullOrEmpty(profileName))
    {
        var chain = new CredentialProfileStoreChain();
        if (chain.TryGetAWSCredentials(profileName, out var credentials))
        {
            return new AmazonS3Client(credentials);
        }
        throw new InvalidOperationException($"AWS profile '{profileName}' not found.");
    }
    
    // Use default credential chain (environment variables, IAM role, etc.)
    return new AmazonS3Client();
}

/// <summary>
/// Uploads text content as a media item.
/// </summary>
static async Task UploadTextMediaAsync(MediaTable table, S3BlobProvider blobProvider)
{
    ConsoleHelpers.ShowSection("Upload Text as Media");
    
    var name = ConsoleHelpers.GetInput("Enter media name");
    if (string.IsNullOrWhiteSpace(name))
        return;

    Console.Write("Enter description (optional): ");
    var description = Console.ReadLine()?.Trim();

    Console.WriteLine("Enter text content (press Enter twice to finish):");
    var lines = new List<string>();
    string? line;
    while (!string.IsNullOrEmpty(line = Console.ReadLine()))
    {
        lines.Add(line);
    }

    if (lines.Count == 0)
    {
        ConsoleHelpers.ShowError("No content provided.");
        return;
    }

    var content = string.Join(Environment.NewLine, lines);
    var bytes = System.Text.Encoding.UTF8.GetBytes(content);

    // Generate unique ID and S3 key
    var id = Guid.NewGuid().ToString();
    var s3Key = $"{id}.txt";

    // Upload to S3
    ConsoleHelpers.ShowInfo("Uploading to S3...");
    using var stream = new MemoryStream(bytes);
    var storedKey = await blobProvider.StoreAsync(stream, s3Key);

    // Create media item record in DynamoDB
    var mediaItem = new MediaItem
    {
        Id = id,
        Name = name,
        ContentType = "text/plain",
        DataReference = storedKey,
        SizeBytes = bytes.Length,
        UploadedAt = DateTime.UtcNow,
        Description = string.IsNullOrEmpty(description) ? null : description
    };

    await table.MediaItems.PutAsync(mediaItem);
    
    ConsoleHelpers.ShowSuccess($"Uploaded media item: {id[..8]}...");
    Console.WriteLine($"  Name: {name}");
    Console.WriteLine($"  Size: {bytes.Length} bytes");
    Console.WriteLine($"  S3 Key: {storedKey}");
}

/// <summary>
/// Uploads a file as a media item.
/// </summary>
static async Task UploadFileMediaAsync(MediaTable table, S3BlobProvider blobProvider)
{
    ConsoleHelpers.ShowSection("Upload File as Media");
    
    var filePath = ConsoleHelpers.GetInput("Enter file path");
    if (string.IsNullOrWhiteSpace(filePath))
        return;

    if (!File.Exists(filePath))
    {
        ConsoleHelpers.ShowError($"File not found: {filePath}");
        return;
    }

    var fileInfo = new FileInfo(filePath);
    var name = ConsoleHelpers.GetInput($"Enter media name (default: {fileInfo.Name})", required: false);
    if (string.IsNullOrWhiteSpace(name))
        name = fileInfo.Name;

    Console.Write("Enter description (optional): ");
    var description = Console.ReadLine()?.Trim();

    // Determine content type from extension
    var contentType = GetContentType(fileInfo.Extension);

    // Generate unique ID and S3 key
    var id = Guid.NewGuid().ToString();
    var s3Key = $"{id}{fileInfo.Extension}";

    // Upload to S3
    ConsoleHelpers.ShowInfo($"Uploading {fileInfo.Length} bytes to S3...");
    await using var fileStream = File.OpenRead(filePath);
    var storedKey = await blobProvider.StoreAsync(fileStream, s3Key);

    // Create media item record in DynamoDB
    var mediaItem = new MediaItem
    {
        Id = id,
        Name = name,
        ContentType = contentType,
        DataReference = storedKey,
        SizeBytes = fileInfo.Length,
        UploadedAt = DateTime.UtcNow,
        Description = string.IsNullOrEmpty(description) ? null : description
    };

    await table.MediaItems.PutAsync(mediaItem);
    
    ConsoleHelpers.ShowSuccess($"Uploaded media item: {id[..8]}...");
    Console.WriteLine($"  Name: {name}");
    Console.WriteLine($"  Size: {FormatBytes(fileInfo.Length)}");
    Console.WriteLine($"  Content-Type: {contentType}");
    Console.WriteLine($"  S3 Key: {storedKey}");
}


/// <summary>
/// Lists all media items in the table.
/// </summary>
static async Task ListMediaItemsAsync(MediaTable table)
{
    ConsoleHelpers.ShowSection("All Media Items");
    
    var mediaItems = await table.MediaItems.Scan().ToListAsync();
    var itemList = mediaItems.ToList();
    
    if (itemList.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No media items found. Upload some!");
        return;
    }

    ConsoleHelpers.DisplayTable(
        itemList,
        ("ID (first 8)", item => item.Id[..Math.Min(8, item.Id.Length)]),
        ("Name", item => TruncateString(item.Name, 20)),
        ("Type", item => TruncateString(item.ContentType, 15)),
        ("Size", item => FormatBytes(item.SizeBytes)),
        ("Uploaded", item => item.UploadedAt.ToString("yyyy-MM-dd HH:mm")));

    ConsoleHelpers.ShowInfo($"Total: {itemList.Count} media items");
}

/// <summary>
/// Downloads a media item from S3.
/// </summary>
static async Task DownloadMediaAsync(MediaTable table, S3BlobProvider blobProvider)
{
    ConsoleHelpers.ShowSection("Download Media");
    
    var mediaItems = await table.MediaItems.Scan().ToListAsync();
    var itemList = mediaItems.ToList();
    
    if (itemList.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No media items to download.");
        return;
    }

    Console.WriteLine("Available media items:");
    ConsoleHelpers.DisplayTable(
        itemList,
        ("ID (first 8)", item => item.Id[..Math.Min(8, item.Id.Length)]),
        ("Name", item => TruncateString(item.Name, 30)),
        ("Size", item => FormatBytes(item.SizeBytes)));

    var id = ConsoleHelpers.GetInput("Enter media ID (or first 8 chars)");
    if (string.IsNullOrWhiteSpace(id))
        return;

    var mediaItem = itemList.FirstOrDefault(m => m.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase));
    if (mediaItem == null)
    {
        ConsoleHelpers.ShowError($"No media item found matching '{id}'");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"  Media: {mediaItem.Name}");
    Console.WriteLine($"  Size: {FormatBytes(mediaItem.SizeBytes)}");
    Console.WriteLine($"  S3 Key: {mediaItem.DataReference}");
    Console.WriteLine();

    Console.Write("Save to file? Enter path (or press Enter to display content): ");
    var outputPath = Console.ReadLine()?.Trim();

    ConsoleHelpers.ShowInfo("Downloading from S3...");
    await using var dataStream = await blobProvider.RetrieveAsync(mediaItem.DataReference);

    if (string.IsNullOrEmpty(outputPath))
    {
        // Display content (for text files)
        if (mediaItem.ContentType.StartsWith("text/") || mediaItem.SizeBytes < 10000)
        {
            using var reader = new StreamReader(dataStream);
            var content = await reader.ReadToEndAsync();
            Console.WriteLine();
            Console.WriteLine("─── Content ───");
            Console.WriteLine(content);
            Console.WriteLine("───────────────");
        }
        else
        {
            ConsoleHelpers.ShowWarning($"Binary content ({FormatBytes(mediaItem.SizeBytes)}). Provide a file path to save.");
        }
    }
    else
    {
        // Save to file
        await using var fileStream = File.Create(outputPath);
        await dataStream.CopyToAsync(fileStream);
        ConsoleHelpers.ShowSuccess($"Saved to: {outputPath}");
    }
}

/// <summary>
/// Deletes a media item from both DynamoDB and S3.
/// </summary>
static async Task DeleteMediaAsync(MediaTable table, S3BlobProvider blobProvider)
{
    ConsoleHelpers.ShowSection("Delete Media");
    
    var mediaItems = await table.MediaItems.Scan().ToListAsync();
    var itemList = mediaItems.ToList();
    
    if (itemList.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No media items to delete.");
        return;
    }

    Console.WriteLine("Available media items:");
    ConsoleHelpers.DisplayTable(
        itemList,
        ("ID (first 8)", item => item.Id[..Math.Min(8, item.Id.Length)]),
        ("Name", item => TruncateString(item.Name, 30)),
        ("Size", item => FormatBytes(item.SizeBytes)));

    var id = ConsoleHelpers.GetInput("Enter media ID (or first 8 chars)");
    if (string.IsNullOrWhiteSpace(id))
        return;

    var mediaItem = itemList.FirstOrDefault(m => m.Id.StartsWith(id, StringComparison.OrdinalIgnoreCase));
    if (mediaItem == null)
    {
        ConsoleHelpers.ShowError($"No media item found matching '{id}'");
        return;
    }

    Console.Write($"Are you sure you want to delete '{mediaItem.Name}'? This will remove both DynamoDB and S3 data. (y/n): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (confirm != "y" && confirm != "yes")
    {
        ConsoleHelpers.ShowInfo("Delete cancelled");
        return;
    }

    // Delete from S3 first
    ConsoleHelpers.ShowInfo("Deleting from S3...");
    await blobProvider.DeleteAsync(mediaItem.DataReference);

    // Delete from DynamoDB
    ConsoleHelpers.ShowInfo("Deleting from DynamoDB...");
    await table.MediaItems.DeleteAsync(mediaItem.Id);

    ConsoleHelpers.ShowSuccess("Media item deleted from both S3 and DynamoDB");
}


/// <summary>
/// Handles S3-specific errors with meaningful messages.
/// </summary>
static void HandleS3Error(Exception ex)
{
    // Check for specific S3 error types
    if (ex is Amazon.S3.AmazonS3Exception s3Ex)
    {
        switch (s3Ex.ErrorCode)
        {
            case "NoSuchBucket":
                ConsoleHelpers.ShowError($"S3 bucket not found: {s3Ex.Message}");
                ConsoleHelpers.ShowWarning("Make sure the bucket exists and the name is correct.");
                break;
            case "AccessDenied":
                ConsoleHelpers.ShowError($"Access denied to S3: {s3Ex.Message}");
                ConsoleHelpers.ShowWarning("Check your AWS credentials and IAM permissions.");
                ConsoleHelpers.ShowWarning("Required permissions: s3:PutObject, s3:GetObject, s3:DeleteObject, s3:HeadObject");
                break;
            case "NoSuchKey":
                ConsoleHelpers.ShowError($"S3 object not found: {s3Ex.Message}");
                break;
            case "InvalidAccessKeyId":
            case "SignatureDoesNotMatch":
                ConsoleHelpers.ShowError($"Invalid AWS credentials: {s3Ex.Message}");
                ConsoleHelpers.ShowWarning("Check your AWS access key and secret key.");
                break;
            default:
                ConsoleHelpers.ShowError($"S3 error ({s3Ex.ErrorCode}): {s3Ex.Message}");
                break;
        }
    }
    else if (ex is KeyNotFoundException)
    {
        ConsoleHelpers.ShowError($"Blob not found: {ex.Message}");
    }
    else if (ex is InvalidOperationException && ex.InnerException is Amazon.S3.AmazonS3Exception innerS3Ex)
    {
        // Unwrap S3 exceptions from InvalidOperationException
        HandleS3Error(innerS3Ex);
    }
    else if (ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
             ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
             ex is System.Net.Http.HttpRequestException)
    {
        ConsoleHelpers.ShowError($"Network error: {ex.Message}");
        ConsoleHelpers.ShowWarning("Check your internet connection and AWS region configuration.");
    }
    else
    {
        ConsoleHelpers.ShowError(ex, "Operation failed");
    }
}

/// <summary>
/// Gets the MIME content type for a file extension.
/// </summary>
static string GetContentType(string extension)
{
    return extension.ToLowerInvariant() switch
    {
        ".txt" => "text/plain",
        ".html" or ".htm" => "text/html",
        ".css" => "text/css",
        ".js" => "application/javascript",
        ".json" => "application/json",
        ".xml" => "application/xml",
        ".pdf" => "application/pdf",
        ".zip" => "application/zip",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".webp" => "image/webp",
        ".mp3" => "audio/mpeg",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream"
    };
}

/// <summary>
/// Formats a byte count as a human-readable string.
/// </summary>
static string FormatBytes(long bytes)
{
    string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
    int suffixIndex = 0;
    double size = bytes;

    while (size >= 1024 && suffixIndex < suffixes.Length - 1)
    {
        size /= 1024;
        suffixIndex++;
    }

    return suffixIndex == 0 
        ? $"{size:0} {suffixes[suffixIndex]}" 
        : $"{size:0.##} {suffixes[suffixIndex]}";
}

/// <summary>
/// Truncates a string to the specified maximum length.
/// </summary>
static string TruncateString(string? value, int maxLength)
{
    if (string.IsNullOrEmpty(value))
        return string.Empty;
    
    return value.Length <= maxLength 
        ? value 
        : value[..(maxLength - 3)] + "...";
}

/// <summary>
/// Extension method to verify S3 bucket exists and is accessible.
/// </summary>
static class S3Extensions
{
    public static async Task EnsureBucketExistsAsync(this IAmazonS3 s3Client, string bucketName)
    {
        try
        {
            await s3Client.GetBucketLocationAsync(bucketName);
        }
        catch (Amazon.S3.AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            throw new InvalidOperationException($"S3 bucket '{bucketName}' does not exist.", ex);
        }
        catch (Amazon.S3.AmazonS3Exception ex) when (ex.ErrorCode == "AccessDenied")
        {
            throw new InvalidOperationException($"Access denied to S3 bucket '{bucketName}'. Check your IAM permissions.", ex);
        }
    }
}
