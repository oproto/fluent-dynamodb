# Design Document: v0.9.0 Enhancements

## Overview

This design document covers the implementation of v0.9.0 enhancements for Oproto.FluentDynamoDb:

1. **Logging Cleanup**: Remove ineffective `DISABLE_DYNAMODB_LOGGING` preprocessor directives and rely on runtime configuration
2. **Example Applications**: Create three new example applications demonstrating JsonBlob, S3 blob storage, and encryption features
3. **TransactionDemo Update**: Add `[RequireWriteTransaction]` demonstration
4. **Documentation Updates**: Update all affected documentation

## Architecture

### Logging Architecture (Current vs. New)

**Current (Problematic) Approach:**
```csharp
// In source-generated code and library code
#if !DISABLE_DYNAMODB_LOGGING
logger?.LogInformation(LogEventIds.ExecutingQuery, "Executing query...");
#endif
```

**Problem**: Users consume NuGet packages (compiled DLLs), not source code. Preprocessor directives in the library have no effect on the compiled package - they only affect how the library itself is compiled.

**New (Correct) Approach:**
```csharp
// Runtime check - works in compiled packages
if (logger.IsEnabled(LogLevel.Information))
{
    logger.LogInformation(LogEventIds.ExecutingQuery, "Executing query...");
}
```

The `NoOpLogger.IsEnabled()` always returns `false`, causing the logging call to be skipped entirely. This provides near-zero overhead when logging is disabled.

### Example Applications Architecture

All example applications follow the established pattern:
```
examples/{ExampleName}/
├── Entities/           # Entity definitions with attributes
├── Program.cs          # Interactive console menu
├── README.md           # Documentation
└── {ExampleName}.csproj
```

Each example uses:
- `Examples.Shared` for common utilities (DynamoDB Local setup, console helpers)
- Source-generated table classes from entity definitions
- Interactive menu-driven console interface

## Components and Interfaces

### 1. Logging Components

**IDynamoDbLogger** (existing - no changes needed):
```csharp
public interface IDynamoDbLogger
{
    bool IsEnabled(LogLevel logLevel);
    void LogTrace(int eventId, string message, params object[] args);
    void LogDebug(int eventId, string message, params object[] args);
    void LogInformation(int eventId, string message, params object[] args);
    void LogWarning(int eventId, string message, params object[] args);
    void LogError(int eventId, string message, params object[] args);
    void LogError(int eventId, Exception exception, string message, params object[] args);
    void LogCritical(int eventId, Exception exception, string message, params object[] args);
}
```

**NoOpLogger** (existing - no changes needed):
```csharp
public sealed class NoOpLogger : IDynamoDbLogger
{
    public static readonly NoOpLogger Instance = new();
    public bool IsEnabled(LogLevel logLevel) => false;
    // All Log* methods are empty
}
```

### 2. JsonBlobDemo Components

**Entities:**
- `Document` - Entity with `[JsonBlob]` property for complex metadata
- `DocumentMetadata` - Complex nested object for JSON serialization

**JsonSerializerContext (for AOT):**
```csharp
[JsonSerializable(typeof(DocumentMetadata))]
[JsonSerializable(typeof(List<string>))]
public partial class DocumentJsonContext : JsonSerializerContext { }
```

### 3. S3BlobDemo Components

**Entities:**
- `MediaItem` - Entity with `[BlobReference]` property for S3 storage

**Configuration:**
- S3 bucket name (required)
- Key prefix (optional)
- AWS profile (optional)

### 4. EncryptionDemo Components

**Entities:**
- `SecureRecord` - Entity with `[Encrypted]` and `[Sensitive]` properties

**ConsoleLogger:**
```csharp
public class ConsoleLogger : IDynamoDbLogger
{
    public bool IsEnabled(LogLevel logLevel) => true;
    // Writes to Console.WriteLine with timestamps and colors
}
```

### 5. TransactionDemo Updates

**New Entity:**
- `FinancialTransaction` - Entity with `[RequireWriteTransaction]` attribute

**New Menu Option:**
- "Demonstrate RequireWriteTransaction" - Shows exception on direct write, success on transactional write

## Data Models

### JsonBlobDemo Entities

```csharp
[DynamoDbTable("json-blob-demo")]
[GenerateEntityProperty(Name = "Documents")]
[Scannable]
public partial class Document
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [DynamoDbAttribute("title")]
    public string Title { get; set; } = string.Empty;

    [JsonBlob]
    [DynamoDbAttribute("metadata")]
    public DocumentMetadata Metadata { get; set; } = new();

    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class DocumentMetadata
{
    public string Author { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> CustomFields { get; set; } = new();
    public NestedInfo? AdditionalInfo { get; set; }
}

public class NestedInfo
{
    public string Category { get; set; } = string.Empty;
    public int Priority { get; set; }
}
```

### S3BlobDemo Entities

```csharp
[DynamoDbTable("s3-blob-demo")]
[GenerateEntityProperty(Name = "MediaItems")]
[Scannable]
public partial class MediaItem
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [BlobReference(Provider = BlobProvider.S3)]
    [DynamoDbAttribute("dataRef")]
    public string DataReference { get; set; } = string.Empty;

    [DynamoDbAttribute("sizeBytes")]
    public long SizeBytes { get; set; }

    [DynamoDbAttribute("uploadedAt")]
    public DateTime UploadedAt { get; set; }
}
```

### EncryptionDemo Entities

```csharp
[DynamoDbTable("encryption-demo")]
[GenerateEntityProperty(Name = "SecureRecords")]
[Scannable]
public partial class SecureRecord
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [DynamoDbAttribute("label")]
    public string Label { get; set; } = string.Empty;

    [Sensitive]
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;

    [Encrypted]
    [DynamoDbAttribute("ssn")]
    public string SocialSecurityNumber { get; set; } = string.Empty;

    [Encrypted]
    [Sensitive]
    [DynamoDbAttribute("creditCard")]
    public string CreditCardNumber { get; set; } = string.Empty;

    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
```

### TransactionDemo New Entity

```csharp
[DynamoDbTable("transaction-demo")]
[GenerateEntityProperty(Name = "FinancialTransactions")]
[RequireWriteTransaction]
public partial class FinancialTransaction
{
    [PartitionKey(Prefix = "ACCOUNT")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "FIN")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [DynamoDbAttribute("transactionId")]
    public string TransactionId { get; set; } = string.Empty;

    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }

    [DynamoDbAttribute("type")]
    public string Type { get; set; } = string.Empty;

    [DynamoDbAttribute("timestamp")]
    public DateTime Timestamp { get; set; }
}
```



## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Based on the prework analysis, the following correctness properties have been identified:

### Property 1: JsonBlob Round-Trip Consistency
*For any* valid complex object stored as a JsonBlob property, serializing to DynamoDB and then deserializing back should produce an equivalent object.

**Validates: Requirements 2.5, 2.6**

### Property 2: S3 Blob Round-Trip Consistency
*For any* valid binary data stored via BlobReference, uploading to S3 and then downloading should return identical data.

**Validates: Requirements 3.3, 3.4**

### Property 3: Encryption Round-Trip Consistency
*For any* valid string value stored in an Encrypted property, encrypting before storage and decrypting after retrieval should return the original value.

**Validates: Requirements 4.4, 4.5**

### Property 4: Sensitive Data Redaction in Logs
*For any* entity with Sensitive properties, log output should contain "[REDACTED]" for sensitive values while DynamoDB should contain the actual values.

**Validates: Requirements 4.6, 4.7**

### Property 5: RequireWriteTransaction Enforcement
*For any* entity marked with `[RequireWriteTransaction]`, direct Put/Update/Delete operations should throw `InvalidOperationException`, while TransactWrite operations should succeed.

**Validates: Requirements 5.3, 5.4**

## Error Handling

### Logging Errors
- If logger throws an exception, it should not propagate to the caller
- Logging failures should be silently ignored to avoid disrupting business logic

### JsonBlob Errors
- `InvalidOperationException` when no JSON serializer is configured
- `JsonException` (System.Text.Json) or `JsonSerializationException` (Newtonsoft) for invalid JSON

### S3 Blob Errors
- `InvalidOperationException` when no blob storage provider is configured
- `KeyNotFoundException` when blob reference doesn't exist in S3
- `AmazonS3Exception` for S3 service errors (wrapped in `InvalidOperationException`)

### Encryption Errors
- `InvalidOperationException` when no field encryptor is configured
- `FieldEncryptionException` for KMS or encryption failures
- `NotImplementedException` (temporary) for pending AWS Encryption SDK integration

### RequireWriteTransaction Errors
- `InvalidOperationException` with message: "Entity type '{EntityName}' requires write operations to be performed within a transaction. Use DynamoDbTransactions.Write() instead."

## Testing Strategy

### Dual Testing Approach

This feature uses both unit tests and property-based tests:

**Unit Tests** verify:
- Specific examples of logging behavior
- Console output formatting
- Menu navigation in example apps
- Error message content

**Property-Based Tests** verify:
- Round-trip consistency for JsonBlob serialization
- Round-trip consistency for S3 blob storage
- Round-trip consistency for encryption
- Sensitive data redaction patterns
- RequireWriteTransaction enforcement

### Property-Based Testing Framework

Use **FsCheck** (already used in the project) for property-based testing in C#.

Each property test should:
- Run a minimum of 100 iterations
- Use appropriate generators for the data types
- Be tagged with the property number and requirements reference

### Test Organization

```
Examples.Tests/
├── JsonBlobDemo/
│   └── JsonBlobRoundTripTests.cs
├── S3BlobDemo/
│   └── S3BlobRoundTripTests.cs (integration tests)
├── EncryptionDemo/
│   └── SensitiveDataRedactionTests.cs
└── TransactionDemo/
    └── RequireWriteTransactionTests.cs
```

### Property Test Examples

```csharp
// Property 1: JsonBlob Round-Trip
[Property]
public Property JsonBlob_RoundTrip_PreservesData()
{
    return Prop.ForAll(
        Arb.From<DocumentMetadata>(),
        metadata =>
        {
            var document = new Document { Id = "test", Metadata = metadata };
            var attributes = Document.ToDynamoDb(document, options);
            var restored = Document.FromDynamoDb(attributes, options);
            return restored.Metadata.Equals(metadata);
        });
}

// Property 5: RequireWriteTransaction Enforcement
[Property]
public Property RequireWriteTransaction_DirectWrite_Throws()
{
    return Prop.ForAll(
        Arb.From<FinancialTransaction>(),
        transaction =>
        {
            var threw = false;
            try
            {
                table.FinancialTransactions.Put(transaction).PutAsync().Wait();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
            return threw;
        });
}
```

## Implementation Notes

### Files to Modify for Logging Cleanup

The following files contain `#if !DISABLE_DYNAMODB_LOGGING` directives that need to be removed:

1. Source generator templates (if any)
2. Request builder classes
3. Any other library code with conditional compilation

**Search pattern**: `#if !DISABLE_DYNAMODB_LOGGING` and `#endif` pairs

**Replacement pattern**: Keep the logging code, ensure it uses `IsEnabled()` guard pattern

### Example Application Structure

Each new example follows this template:

```csharp
// Program.cs structure
const string TableName = "example-name";

// Display banner
Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║        ExampleName - FluentDynamoDb Example                ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");

// Initialize DynamoDB Local
var client = DynamoDbSetup.CreateLocalClient();
await DynamoDbSetup.EnsureTableExistsAsync(client, TableName, "pk");

// Create table with options
var options = new FluentDynamoDbOptions()
    .WithLogger(new ConsoleLogger())  // For EncryptionDemo
    .WithSystemTextJson();             // For JsonBlobDemo
var table = new ExampleTable(client, TableName, options);

// Menu loop
while (true)
{
    var choice = ConsoleHelpers.ShowMenu("Menu Title", "Option 1", "Option 2", "Exit");
    // Handle choices
}
```

### AWS Encryption SDK Status

The `AwsEncryptionSdkFieldEncryptor` implementation is incomplete (throws `NotImplementedException`). The EncryptionDemo should:

1. Display a warning that encryption is not fully implemented
2. Demonstrate the API and configuration pattern
3. Show what would happen when encryption is complete
4. Focus on demonstrating `[Sensitive]` attribute logging redaction (which works)

### Documentation Files to Update

1. **CHANGELOG.md** - Add entries under `[Unreleased]`
2. **docs/DOCUMENTATION_CHANGELOG.md** - Track documentation changes
3. **docs/advanced-topics/conditional-compilation-logging.md** - Rewrite or remove
4. **docs/core-features/LoggingConfiguration.md** - Update examples
5. **docs/reference/LoggingTroubleshooting.md** - Remove DISABLE_DYNAMODB_LOGGING
6. **README.md** - Remove DISABLE_DYNAMODB_LOGGING section

