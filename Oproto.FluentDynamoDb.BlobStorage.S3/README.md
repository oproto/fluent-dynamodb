# Oproto.FluentDynamoDb.BlobStorage.S3

S3 blob storage provider for [Oproto.FluentDynamoDb](https://www.nuget.org/packages/Oproto.FluentDynamoDb), enabling storage of large data externally in Amazon S3 with only a reference stored in DynamoDB.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.BlobStorage.S3
```

## Overview

DynamoDB has a 400KB item size limit. This package allows you to transparently store large attributes (like documents, images, or JSON blobs) in S3 while keeping a reference in your DynamoDB item.

## Usage

```csharp
using Oproto.FluentDynamoDb.BlobStorage.S3;

// Configure S3 blob storage
var options = new FluentDynamoDbOptions
{
    BlobStorageProvider = new S3BlobStorageProvider(s3Client, "my-bucket")
};

// Use [BlobStorage] attribute on large properties
[DynamoDbEntity]
public class Document
{
    [PartitionKey]
    public string Id { get; set; }
    
    [BlobStorage]
    public byte[] Content { get; set; }  // Stored in S3, reference in DynamoDB
}
```

## Features

- **Transparent Storage**: Large attributes automatically stored in S3
- **Reference Tracking**: DynamoDB stores only the S3 reference
- **Automatic Retrieval**: Content fetched from S3 on read
- **AOT Compatible**: Full support for Native AOT compilation
- **Configurable**: Custom bucket, prefix, and storage options

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Oproto.FluentDynamoDb.BlobStorage.S3](https://www.nuget.org/packages/Oproto.FluentDynamoDb.BlobStorage.S3)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
