# S3BlobDemo

This example demonstrates how to use the `[BlobReference]` attribute with FluentDynamoDb to store large binary data in Amazon S3 while keeping only a reference key in DynamoDB.

## Features Demonstrated

- **S3 Blob Storage Integration**: Store large files in S3 with references in DynamoDB
- **`[BlobReference]` Attribute**: Mark properties for external blob storage
- **S3BlobProvider Configuration**: Configure bucket name, key prefix, and credentials
- **CRUD Operations**: Upload, download, list, and delete media items
- **Error Handling**: Graceful handling of S3-specific errors

## Prerequisites

1. **DynamoDB Local**: Running on `http://localhost:8000`
   ```bash
   # Start DynamoDB Local (from project root)
   java -Djava.library.path=./dynamodb-local/DynamoDBLocal_lib -jar ./dynamodb-local/DynamoDBLocal.jar -sharedDb
   ```

2. **AWS S3 Bucket**: An existing S3 bucket with appropriate permissions

3. **AWS Credentials**: Configured via:
   - AWS CLI profile (`aws configure`)
   - Environment variables (`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`)
   - IAM role (when running on AWS)

## IAM Permissions Required

The AWS credentials used must have the following S3 permissions on the target bucket:

```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": [
                "s3:PutObject",
                "s3:GetObject",
                "s3:DeleteObject",
                "s3:HeadObject",
                "s3:GetBucketLocation"
            ],
            "Resource": [
                "arn:aws:s3:::your-bucket-name",
                "arn:aws:s3:::your-bucket-name/*"
            ]
        }
    ]
}
```

## Running the Example

```bash
cd examples/S3BlobDemo
dotnet run
```

The application will prompt for:
1. **S3 Bucket Name** (required): The name of your S3 bucket
2. **Key Prefix** (optional): A prefix for all S3 object keys (e.g., `media/`)
3. **AWS Profile** (optional): The AWS CLI profile to use for credentials

## Code Highlights

### Entity with BlobReference

```csharp
[DynamoDbTable("s3-blob-demo")]
[Scannable]
[GenerateEntityProperty(Name = "MediaItems")]
public partial class MediaItem
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    // The actual data is stored in S3, only the key is in DynamoDB
    [BlobReference(BlobProvider.S3)]
    [DynamoDbAttribute("dataRef")]
    public string DataReference { get; set; } = string.Empty;

    [DynamoDbAttribute("sizeBytes")]
    public long SizeBytes { get; set; }
}
```

### Configuring S3BlobProvider

```csharp
using Amazon.S3;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.BlobStorage.S3;

// Create S3 client
var s3Client = new AmazonS3Client();

// Create blob provider with bucket and optional prefix
var blobProvider = new S3BlobProvider(s3Client, "my-bucket", "media/");

// Configure FluentDynamoDb options
var options = new FluentDynamoDbOptions()
    .WithBlobStorage(blobProvider);

var table = new MyTable(dynamoDbClient, "my-table", options);
```

### Uploading Data to S3

```csharp
// Upload binary data to S3
using var stream = new MemoryStream(data);
var s3Key = await blobProvider.StoreAsync(stream, "unique-key.bin");

// Store reference in DynamoDB
var mediaItem = new MediaItem
{
    Id = Guid.NewGuid().ToString(),
    Name = "My File",
    DataReference = s3Key,  // S3 key stored in DynamoDB
    SizeBytes = data.Length
};

await table.MediaItems.PutAsync(mediaItem);
```

### Downloading Data from S3

```csharp
// Get media item from DynamoDB
var mediaItem = await table.MediaItems.GetAsync(id);

// Download data from S3 using the stored reference
await using var dataStream = await blobProvider.RetrieveAsync(mediaItem.DataReference);

// Use the data...
await dataStream.CopyToAsync(outputStream);
```

### Deleting Both S3 and DynamoDB Data

```csharp
// Delete from S3 first
await blobProvider.DeleteAsync(mediaItem.DataReference);

// Then delete from DynamoDB
await table.MediaItems.DeleteAsync(mediaItem.Id);
```

## Architecture

```
┌─────────────────┐     ┌─────────────────┐
│   Application   │     │   DynamoDB      │
│                 │────▶│   (metadata)    │
│                 │     │   - id          │
│                 │     │   - name        │
│                 │     │   - dataRef ────┼──┐
│                 │     │   - sizeBytes   │  │
└────────┬────────┘     └─────────────────┘  │
         │                                    │
         │              ┌─────────────────┐   │
         │              │   Amazon S3     │   │
         └─────────────▶│   (blob data)   │◀──┘
                        │   - binary data │
                        └─────────────────┘
```

## Error Handling

The example demonstrates handling common S3 errors:

- **NoSuchBucket**: Bucket doesn't exist
- **AccessDenied**: Insufficient IAM permissions
- **NoSuchKey**: Object not found in S3
- **InvalidAccessKeyId**: Invalid AWS credentials
- **Network errors**: Connection issues

## Related Documentation

- [Oproto.FluentDynamoDb.BlobStorage.S3 README](../../Oproto.FluentDynamoDb.BlobStorage.S3/README.md)
- [BlobReference Attribute](../../docs/reference/Attributes.md)
