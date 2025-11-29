# Configuration Guide

This guide explains how to configure FluentDynamoDb using the `FluentDynamoDbOptions` class. The options pattern provides a centralized, AOT-compatible way to configure optional features like logging, encryption, blob storage, and geospatial support.

## Overview

`FluentDynamoDbOptions` is the central configuration object for FluentDynamoDb. It uses an immutable builder pattern where each configuration method returns a new instance with the updated settings.

```csharp
// Basic usage - no optional features
var table = new UsersTable(client, "users");

// With configuration options
var options = new FluentDynamoDbOptions()
    .WithLogger(logger);

var table = new UsersTable(client, "users", options);
```

## Basic Configuration

### Default Options

When you don't need any optional features, you can create a table without options:

```csharp
var client = new AmazonDynamoDBClient();
var table = new UsersTable(client, "users");
```

Or explicitly pass default options:

```csharp
var options = new FluentDynamoDbOptions();
var table = new UsersTable(client, "users", options);
```

### Table Constructor Signature

Generated table classes accept an optional `FluentDynamoDbOptions` parameter:

```csharp
public class UsersTable : DynamoDbTableBase
{
    public UsersTable(IAmazonDynamoDB client, string tableName)
        : base(client, tableName) { }

    public UsersTable(IAmazonDynamoDB client, string tableName, FluentDynamoDbOptions? options)
        : base(client, tableName, options) { }
}
```

## Logging Configuration

Use `WithLogger()` to enable logging for DynamoDB operations.

### With Microsoft.Extensions.Logging

Install the logging extensions package:

```bash
dotnet add package Oproto.FluentDynamoDb.Logging.Extensions
```

Configure logging using the `ToDynamoDbLogger()` extension method:

```csharp
using Oproto.FluentDynamoDb.Logging.Extensions;

// From ILogger
var logger = loggerFactory.CreateLogger<UsersTable>();
var options = new FluentDynamoDbOptions()
    .WithLogger(logger.ToDynamoDbLogger());

var table = new UsersTable(client, "users", options);
```

```csharp
// From ILoggerFactory
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<UsersTable>());

var table = new UsersTable(client, "users", options);
```

### With Custom Logger

Implement `IDynamoDbLogger` for custom logging:

```csharp
public class MyCustomLogger : IDynamoDbLogger
{
    public bool IsEnabled(LogLevel logLevel) => true;
    public void LogTrace(int eventId, string message, params object[] args) { /* ... */ }
    public void LogDebug(int eventId, string message, params object[] args) { /* ... */ }
    // ... other methods
}

var options = new FluentDynamoDbOptions()
    .WithLogger(new MyCustomLogger());
```

## Geospatial Configuration

Use `AddGeospatial()` to enable geospatial features (GeoHash, S2, H3).

### Installation

```bash
dotnet add package Oproto.FluentDynamoDb.Geospatial
```

### Configuration

```csharp
using Oproto.FluentDynamoDb;

var options = new FluentDynamoDbOptions()
    .AddGeospatial();

var table = new LocationsTable(client, "locations", options);
```

### Error Without Configuration

If you attempt to use geospatial features without configuration, you'll receive an error:

> "Geospatial features require configuration. Add the Oproto.FluentDynamoDb.Geospatial package and call options.AddGeospatial() when creating your table."

## Blob Storage Configuration

Use `WithBlobStorage()` to enable large object storage in S3.

### Installation

```bash
dotnet add package Oproto.FluentDynamoDb.BlobStorage.S3
```

### Configuration

```csharp
using Oproto.FluentDynamoDb.BlobStorage.S3;

var s3Client = new AmazonS3Client();
var blobProvider = new S3BlobProvider(s3Client, "my-bucket");

var options = new FluentDynamoDbOptions()
    .WithBlobStorage(blobProvider);

var table = new DocumentsTable(client, "documents", options);
```

### With Key Prefix

```csharp
var blobProvider = new S3BlobProvider(s3Client, "my-bucket", "documents/");

var options = new FluentDynamoDbOptions()
    .WithBlobStorage(blobProvider);
```

## Encryption Configuration

Use `WithEncryption()` to enable field-level encryption with AWS KMS.

### Installation

```bash
dotnet add package Oproto.FluentDynamoDb.Encryption.Kms
```

### Configuration

```csharp
using Oproto.FluentDynamoDb.Encryption.Kms;

var keyResolver = new DefaultKmsKeyResolver("arn:aws:kms:us-east-1:123456789012:key/my-key");
var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver);

var options = new FluentDynamoDbOptions()
    .WithEncryption(encryptor);

var table = new SecretsTable(client, "secrets", options);
```

### With Caching Options

```csharp
var encryptorOptions = new AwsEncryptionSdkOptions
{
    EnableCaching = true,
    DefaultCacheTtlSeconds = 300,
    MaxMessagesPerDataKey = 1000
};

var encryptor = new AwsEncryptionSdkFieldEncryptor(keyResolver, encryptorOptions);

var options = new FluentDynamoDbOptions()
    .WithEncryption(encryptor);
```

## Combining Features

Chain configuration methods to enable multiple features:

```csharp
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<MyTable>())
    .AddGeospatial()
    .WithBlobStorage(blobProvider)
    .WithEncryption(encryptor);

var table = new MyTable(client, "my-table", options);
```

The order of method calls doesn't matter - each method returns a new instance with all previous settings preserved.

## Test Isolation

Each table instance has its own configuration, which provides excellent test isolation:

```csharp
[Fact]
public async Task Test_WithMockLogger()
{
    var mockLogger = new MockDynamoDbLogger();
    var options = new FluentDynamoDbOptions()
        .WithLogger(mockLogger);

    var table = new UsersTable(client, "test-users", options);
    
    // Test operations...
    
    Assert.True(mockLogger.LoggedMessages.Any());
}

[Fact]
public async Task Test_WithoutLogging()
{
    // No logging configured - uses NoOpLogger by default
    var table = new UsersTable(client, "test-users");
    
    // Test operations...
}
```

### Parallel Test Support

Because configuration is instance-based rather than static, tests can run in parallel without interference:

```csharp
// These tests can run in parallel safely
[Fact]
public async Task Test1()
{
    var options = new FluentDynamoDbOptions().WithLogger(logger1);
    var table = new UsersTable(client, "table1", options);
    // ...
}

[Fact]
public async Task Test2()
{
    var options = new FluentDynamoDbOptions().WithLogger(logger2);
    var table = new UsersTable(client, "table2", options);
    // ...
}
```

## Configuration Properties

| Property | Type | Description |
|----------|------|-------------|
| `Logger` | `IDynamoDbLogger` | Logger for DynamoDB operations. Defaults to `NoOpLogger.Instance`. |
| `GeospatialProvider` | `IGeospatialProvider?` | Provider for geospatial operations. Null if not configured. |
| `BlobStorageProvider` | `IBlobStorageProvider?` | Provider for blob storage. Null if not configured. |
| `FieldEncryptor` | `IFieldEncryptor?` | Encryptor for sensitive fields. Null if not configured. |

## Configuration Methods

| Method | Description |
|--------|-------------|
| `WithLogger(IDynamoDbLogger?)` | Sets the logger for DynamoDB operations. |
| `AddGeospatial()` | Enables geospatial support with the default provider. |
| `AddGeospatial(IGeospatialProvider)` | Enables geospatial support with a custom provider. |
| `WithBlobStorage(IBlobStorageProvider?)` | Sets the blob storage provider. |
| `WithEncryption(IFieldEncryptor?)` | Sets the field encryptor. |

## See Also

- [Logging Configuration](LoggingConfiguration.md) - Detailed logging setup
- [Field-Level Security](../advanced-topics/FieldLevelSecurity.md) - Encryption details
- [Geospatial Support](../advanced-topics/Geospatial.md) - Geospatial features and spatial queries
- [Geospatial Package README](../../Oproto.FluentDynamoDb.Geospatial/README.md) - Complete API reference
