# Documentation Changelog

This changelog tracks corrections and updates to the Oproto.FluentDynamoDb documentation.

## Purpose

This file is maintained **separately from the repository `CHANGELOG.md`** to facilitate synchronization with derived documentation maintained by other teams (e.g., website documentation at [fluentdynamodb.dev](https://fluentdynamodb.dev)).

The repository `CHANGELOG.md` tracks code changes, new features, and bug fixes. This file specifically tracks:
- Documentation corrections (fixing incorrect API references, outdated patterns)
- Documentation improvements (clarifications, additional examples)
- Documentation restructuring (file moves, reorganization)

## How to Use This Changelog

### For Documentation Maintainers

When syncing derived documentation:
1. Note the date of your last sync
2. Review all entries since that date
3. Apply the same corrections to your derived documentation
4. Update your sync date

### Entry Format

Each entry follows this structure:

```markdown
## [YYYY-MM-DD]

### File: path/to/file.md

**Before:**
```csharp
// incorrect code example
```

**After:**
```csharp
// corrected code example
```

**Reason:** Brief explanation of why this change was made.
```

### Categories

Entries may be categorized as:
- **API Correction**: Fixing incorrect method names or signatures
- **Pattern Update**: Updating code patterns to match current best practices
- **Clarification**: Adding notes or explanations for clarity
- **Example Fix**: Correcting code examples that wouldn't compile or work correctly

---

## Changelog Entries

<!-- Add new entries below this line, with most recent at the top -->

## [2025-12-12]

### File: docs/core-features/DynamicFields.md - Update Pattern Correction

**Category:** API Correction - Removed Redundant Methods

**Before:**
```csharp
// Updating dynamic fields section showed builder methods
await table.Products.Update(pk, sk)
    .SetDynamicField("sale_price", 24.99m)
    .RemoveDynamicField("temporary_note")
    .UpdateAsync();
```

**After:**
```csharp
// PREFERRED: Lambda expression with DynamicFieldCollection
var changes = new DynamicFieldCollection();
changes.SetDecimal("sale_price", 24.99m);
changes.Remove("temporary_note");

await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel { DynamicFields = changes })
    .UpdateAsync();

// EXPLICIT CONTROL: Manual expression strings
await table.Products.Update(pk, sk)
    .Set("#salePrice = :salePrice")
    .Remove("#tempNote")
    .WithAttribute("#salePrice", "sale_price")
    .WithAttribute("#tempNote", "temporary_note")
    .WithValue(":salePrice", new AttributeValue { N = "24.99" })
    .UpdateAsync();
```

**Reason:** The `SetDynamicField()` and `RemoveDynamicField()` methods were removed from `UpdateItemRequestBuilder` as they were redundant. The same functionality is available via:
1. Lambda expressions with `DynamicFieldCollection` on update models (preferred)
2. Manual expression strings with `Set()`, `Remove()`, `WithAttribute()`, `WithValue()`

---

### File: docs/core-features/DynamicFields.md (NEW)

**Category:** New Documentation - Dynamic Fields Feature

**Summary:** Added comprehensive documentation for the new dynamic fields feature, including:
- Overview and use cases (multi-tenant custom attributes)
- Enabling dynamic fields with `[EnableDynamicFields]` attribute
- Reading dynamic fields with typed accessors (`GetString`, `GetInt`, `GetBool`, etc.)
- Type detection with `GetFieldType()` and `DynamicFieldType` enum
- Writing dynamic fields with typed setters
- Updating dynamic fields via lambda expressions with `DynamicFieldCollection` (preferred) or manual expression strings
- Change tracking with `ChangesOnly()`, `HasChanges`, `RemovedFields`, and `ResetChangeTracking()`
- Filtering by dynamic fields in expressions
- Existence checks with `Exists()` and `NotExists()`
- Security considerations (logging redaction)
- Performance considerations
- Limitations and best practices

**Files added:**
- `docs/core-features/DynamicFields.md`

**Reason:** New feature documentation for dynamic fields support enabling entities to capture unmapped DynamoDB attributes.

---

### File: README.md - Dynamic Fields Feature Section

**Category:** New Documentation - Feature Overview

**Summary:** Added dynamic fields feature to the Key Features section of the main README with a code example demonstrating:
- Entity definition with `[EnableDynamicFields]`
- Reading dynamic fields with typed accessors
- Writing dynamic fields
- Filtering by dynamic fields

**Files updated:**
- `README.md`

**Reason:** Feature visibility in main project documentation.

---

### File: examples/DynamicFieldsDemo/README.md (NEW)

**Category:** New Documentation - Example Application

**Summary:** Added README documentation for the DynamicFieldsDemo example application demonstrating:
- Multi-tenant custom attributes use case
- Enabling dynamic fields
- Reading, writing, and updating dynamic fields
- Filtering by dynamic fields
- Supported dynamic field types
- Security considerations

**Files added:**
- `examples/DynamicFieldsDemo/README.md`

**Reason:** Example application documentation for dynamic fields feature.

---

## [2025-12-11]

### File: docs/advanced-topics/SchemaValidation.md (NEW)

**Category:** New Documentation - Schema Validation Feature

**Summary:** Added comprehensive documentation for the new schema validation feature, including usage examples, error codes, validation options, and best practices.

**Files added:**
- `docs/advanced-topics/SchemaValidation.md`

**Reason:** New feature documentation for runtime schema validation of DynamoDB tables against entity metadata.

---

### File: docs/reference/AttributeReference.md - LocalSecondaryIndex Attribute

**Category:** New Documentation - LSI Attribute

**Summary:** Added documentation for the new `[LocalSecondaryIndex]` attribute that enables Local Secondary Index definitions in entity metadata.

**Files updated:**
- `docs/reference/AttributeReference.md`

**Added:**
```markdown
## [LocalSecondaryIndex]

Marks a property as the sort key for a Local Secondary Index (LSI).

### Purpose

Identifies the property that serves as the sort key for a Local Secondary Index. LSIs share the same partition key as the base table but have a different sort key, enabling alternative query patterns without the cost of a GSI.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `indexName` | `string` | Yes | The name of the Local Secondary Index |

### Example

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string OrderId { get; set; } = string.Empty;
    
    // LSI for querying orders by date within a customer
    [LocalSecondaryIndex("orders-by-date")]
    [DynamoDbAttribute("order_date")]
    public string OrderDate { get; set; } = string.Empty;
}
```
```

**Reason:** New attribute added for Local Secondary Index support, required for accurate schema validation.

---

## [2025-12-09]

### File: docs/advanced-topics/AdvancedTypes.md - Blob Storage Redesign

**Category:** Pattern Update - Blob Storage API

**Summary:** Updated blob storage documentation to reflect the redesign from `[BlobReference]` to `[BlobStorage]` with `BlobData<T>` wrapper type. This is a breaking change that introduces lazy/eager loading control and failure handling strategies.

---

#### Part 1: External Blob Storage Section Rewrite

**Files updated:**
- `docs/advanced-topics/AdvancedTypes.md`

**Before:**
```csharp
// External Blob Storage with [BlobReference]
[DynamoDbTable("files")]
public partial class FileMetadata
{
    [DynamoDbAttribute("file_id")]
    public string FileId { get; set; }
    
    [DynamoDbAttribute("data_ref")]
    [BlobReference(BlobProvider.S3, BucketName = "my-files-bucket", KeyPrefix = "uploads")]
    public byte[] Data { get; set; }
}

// Save entity with blob
var file = new FileMetadata
{
    FileId = "file-123",
    Data = File.ReadAllBytes("large-file.pdf")
};

var item = await FileMetadata.ToDynamoDbAsync(file, blobProvider);
```

**After:**
```csharp
// External Blob Storage with [BlobStorage] and BlobData<T>
[DynamoDbTable("files")]
public partial class FileMetadata
{
    [PartitionKey]
    [DynamoDbAttribute("file_id")]
    public string FileId { get; set; } = string.Empty;
    
    // Eager loading (default) - data loaded during deserialization
    [BlobStorage]
    [DynamoDbAttribute("data")]
    public BlobData<byte[]> Data { get; set; } = default!;
    
    // Lazy loading - data loaded on explicit LoadAsync() call
    [BlobStorage(LazyLoad = true)]
    [DynamoDbAttribute("thumbnail")]
    public BlobData<byte[]> Thumbnail { get; set; } = default!;
}

// Configuration with strategy
var options = new FluentDynamoDbOptions()
    .WithBlobStorage(new S3BlobProvider(s3Client, "my-bucket"))
    .WithBlobStorageStrategy(new BestEffortCleanupStrategy(provider)); // Optional, default

var table = new FileTable(dynamoDbClient, "files", options);

// Save entity with blob
var file = new FileMetadata
{
    FileId = "file-123",
    Data = BlobData<byte[]>.Create(File.ReadAllBytes("large-file.pdf")),
    Thumbnail = BlobData<byte[]>.Create(thumbnailBytes)
};

await table.Files.PutAsync(file);

// Retrieve with eager loading
var loaded = await table.Files.GetAsync("file-123");
var data = loaded.Data.Value; // Already loaded

// Retrieve with lazy loading
await loaded.Thumbnail.LoadAsync(); // Explicit load
var thumbnail = loaded.Thumbnail.Value;
```

**Reason:** The `[BlobReference]` attribute has been replaced with `[BlobStorage]` and `BlobData<T>` wrapper type. The new design provides:
- Clearer semantics (attribute name indicates storage, not reference)
- Lazy/eager loading control via `LazyLoad` property
- Failure handling strategies via `IBlobStorageStrategy`
- Better encapsulation of blob state via `BlobData<T>` wrapper

---

#### Part 2: New Blob Storage Strategies Section

**Files updated:**
- `docs/advanced-topics/AdvancedTypes.md`

**Added:**
```markdown
### Blob Storage Strategies

Configure how failures between blob storage and DynamoDB operations are handled:

#### BestEffortCleanupStrategy (Default)

Attempts to clean up orphaned blobs when DynamoDB operations fail:

```csharp
var options = new FluentDynamoDbOptions()
    .WithBlobStorage(provider)
    .WithBlobStorageStrategy(new BestEffortCleanupStrategy(provider, logger));
```

- Uploads blobs before DynamoDB write
- Attempts to delete uploaded blobs if DynamoDB write fails
- Logs cleanup failures but doesn't throw
- Deletes blobs after successful DynamoDB delete

#### NoCleanupStrategy

Simple strategy for non-critical data where orphaned blobs are acceptable:

```csharp
var options = new FluentDynamoDbOptions()
    .WithBlobStorage(provider)
    .WithBlobStorageStrategy(new NoCleanupStrategy(provider));
```

- Uploads blobs before DynamoDB write
- No cleanup on DynamoDB write failure (orphaned blobs may remain)
- No blob deletion on DynamoDB delete
```

**Reason:** New section documenting the `IBlobStorageStrategy` interface and built-in implementations.

---

#### Part 3: BlobData<T> API Reference

**Files updated:**
- `docs/advanced-topics/AdvancedTypes.md`

**Added:**
```markdown
### BlobData<T> Wrapper Type

The `BlobData<T>` wrapper encapsulates blob storage behavior:

| Property/Method | Description |
|-----------------|-------------|
| `Value` | Gets the loaded data. Throws `InvalidOperationException` if not loaded. |
| `ReferenceKey` | Gets the storage key, or null if not yet stored. |
| `IsLoaded` | Returns true when data has been loaded from storage. |
| `HasPendingData` | Returns true when instance has data to be stored. |
| `Create(T value)` | Static factory to create instance with data to be stored. |
| `LoadAsync()` | Loads data from storage. Idempotent - returns immediately if already loaded. |

**Error Handling:**

| Scenario | Exception |
|----------|-----------|
| Access `Value` before load | `InvalidOperationException` |
| `LoadAsync()` without provider | `InvalidOperationException` |
| Provider store/retrieve failure | `BlobStorageException` |
| `[Encrypted]` without encryptor | `EncryptionRequiredException` |
```

**Reason:** New section documenting the `BlobData<T>` wrapper type API.

---

#### Part 4: Attribute Combinations

**Files updated:**
- `docs/advanced-topics/AdvancedTypes.md`

**Before:**
```csharp
// Combined JSON Blob + Blob Reference
[DynamoDbAttribute("content_ref")]
[JsonBlob]
[BlobReference(BlobProvider.S3, BucketName = "large-docs")]
public ComplexContent Content { get; set; }
```

**After:**
```csharp
// Combined [BlobStorage] + [JsonBlob] - serialize to JSON before blob upload
[BlobStorage]
[JsonBlob]
[DynamoDbAttribute("content")]
public BlobData<ComplexContent> Content { get; set; } = default!;

// Combined [BlobStorage] + [Encrypted] - encrypt before blob upload
[BlobStorage]
[Encrypted]
[DynamoDbAttribute("secret")]
public BlobData<byte[]> SecretData { get; set; } = default!;

// Combined [BlobStorage] + [Sensitive] - redact in logs
[BlobStorage]
[Sensitive]
[DynamoDbAttribute("pii")]
public BlobData<byte[]> PersonalData { get; set; } = default!;

// All three combined - JSON serialize, then encrypt, then upload
[BlobStorage]
[JsonBlob]
[Encrypted]
[DynamoDbAttribute("encrypted_content")]
public BlobData<SensitiveContent> EncryptedContent { get; set; } = default!;
```

**Reason:** Updated attribute combination examples to use new `[BlobStorage]` and `BlobData<T>` pattern.

---

### File: examples/S3BlobDemo - Complete Rewrite

**Category:** Example Update - Blob Storage Redesign

**Summary:** Updated S3BlobDemo example application to demonstrate the new `[BlobStorage]` attribute and `BlobData<T>` wrapper type, including lazy/eager loading and strategy demonstrations.

**Files updated:**
- `examples/S3BlobDemo/Entities/MediaItem.cs`
- `examples/S3BlobDemo/Program.cs`
- `examples/S3BlobDemo/README.md`

**Reason:** Example application updated to demonstrate new blob storage API patterns.

---

## [2025-12-09]

### File: Multiple documentation files - Logging Runtime Configuration Update

**Category:** Pattern Update - Logging Configuration

**Summary:** Updated logging documentation to reflect the removal of `DISABLE_DYNAMODB_LOGGING` conditional compilation in favor of runtime configuration via `FluentDynamoDbOptions`. All conditional compilation documentation has been removed as the library no longer uses any conditional compilation.

---

#### Part 0: docs/core-features/ConditionalCompilation.md (DELETED)

**Files deleted:**
- `docs/core-features/ConditionalCompilation.md` (deleted entirely)

**Action Required:** Remove this file from any derived documentation. The content was redundant with `docs/core-features/LoggingConfiguration.md` and the filename was misleading since the library no longer uses conditional compilation.

**References removed from:**
- `README.md` - Removed link from logging documentation section
- `docs/README.md` - Removed from core features list
- `docs/core-features/README.md` - Removed from numbered list
- `docs/core-features/StructuredLogging.md` - Removed from related topics
- `docs/core-features/LogLevelsAndEventIds.md` - Updated references to point to LoggingConfiguration.md

---

#### Part 1: docs/advanced-topics/runtime-logging-configuration.md

**Files updated:**
- `docs/advanced-topics/conditional-compilation-logging.md` (removed and replaced with `runtime-logging-configuration.md`)

**Before:**
```markdown
# Conditional Compilation for Logging

All logging code in the library is wrapped in conditional compilation directives:

```csharp
#if !DISABLE_DYNAMODB_LOGGING
logger?.LogInformation(LogEventIds.ExecutingQuery, ...);
#endif
```

When you define the `DISABLE_DYNAMODB_LOGGING` symbol, the C# compiler completely removes all logging code.
```

**After:** File removed. Conditional compilation for logging is no longer supported.

**Reason:** The `DISABLE_DYNAMODB_LOGGING` preprocessor directive has been removed. Logging is now controlled entirely at runtime via `FluentDynamoDbOptions.WithLogger()`. The `NoOpLogger.Instance` provides zero-overhead logging when disabled through the `IsEnabled()` check pattern.

---

#### Part 2: docs/core-features/LoggingConfiguration.md

**Files updated:**
- `docs/core-features/LoggingConfiguration.md`

**Before:**
```markdown
### Conditional Compilation (Zero Overhead in Production)

Disable logging completely in production builds:

```xml
<!-- .csproj -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
</PropertyGroup>
```

See [Logging Configuration](LoggingConfiguration.md) for details.
```

**After:**
```markdown
### Disabling Logging (Zero Overhead)

Use `NoOpLogger.Instance` (the default) for zero-overhead logging:

```csharp
// Default behavior - no logging, zero overhead
var table = new ProductsTable(client, "products");

// Explicit NoOpLogger
var options = new FluentDynamoDbOptions()
    .WithLogger(NoOpLogger.Instance);
var table = new ProductsTable(client, "products", options);
```

The `NoOpLogger.IsEnabled()` method always returns `false`, causing all logging calls to be skipped with minimal overhead.
```

**Reason:** Conditional compilation is no longer supported. Runtime configuration via `NoOpLogger` provides equivalent zero-overhead behavior.

---

#### Part 3: docs/reference/LoggingTroubleshooting.md

**Files updated:**
- `docs/reference/LoggingTroubleshooting.md`

**Before:**
```markdown
#### Issue: Conditional compilation disabled logging

**Symptoms:**
- Logs worked in Debug build
- No logs in Release build

**Diagnosis:**
```bash
dotnet build -c Release -v detailed | grep DISABLE_DYNAMODB_LOGGING
```

**Solution:**
```xml
<!-- Remove or comment out in .csproj -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <!-- <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants> -->
</PropertyGroup>
```
```

**After:** Section removed. Conditional compilation is no longer supported.

**Reason:** The `DISABLE_DYNAMODB_LOGGING` preprocessor directive has been removed from the library.

---

#### Part 4: README.md

**Files updated:**
- `README.md`

**Before:**
```markdown
### Conditional Compilation (Zero Overhead in Production)

Disable logging completely in production builds:

```xml
<!-- .csproj -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
</PropertyGroup>
```

When `DISABLE_DYNAMODB_LOGGING` is defined:
- All logging code is removed at compile time
- Zero runtime overhead
- Zero allocations
- Smaller binary size
```

**After:**
```markdown
### Disabling Logging

By default, the library uses `NoOpLogger.Instance` which provides zero-overhead logging:

```csharp
// Default - no logging configured, uses NoOpLogger
var table = new ProductsTable(client, "products");
```

The `NoOpLogger.IsEnabled()` method always returns `false`, causing all logging calls to be skipped with minimal overhead.
```

**Reason:** Conditional compilation is no longer supported. Runtime configuration via `NoOpLogger` is the recommended approach.

---

### File: docs/reference/AttributeReference.md - v0.9.0 Attribute Updates

**Category:** Documentation Update - New and Updated Attributes

**Summary:** Updated attribute reference documentation for v0.9.0 release including new `[RequireWriteTransaction]` attribute, `[Queryable]` deprecation notice, and `[DynamoDbTable]` `Namespace` parameter.

---

#### Part 1: New [RequireWriteTransaction] Attribute

**Files updated:**
- `docs/reference/AttributeReference.md`

**Added:**
```markdown
## [RequireWriteTransaction]

Marks an entity class as requiring write operations within a transaction.

### Purpose

Enforces transactional consistency for entities where atomic operations are required.
When applied, Put, Update, Delete, and BatchWrite operations throw `InvalidOperationException`
unless performed within a TransactWrite operation.

### Example

```csharp
[DynamoDbTable("FinancialTransactions")]
[RequireWriteTransaction]
public partial class Transaction
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string AccountId { get; set; } = string.Empty;
}

// Throws InvalidOperationException:
await table.Transactions.Put(transaction).PutAsync();

// Allowed:
await DynamoDbTransactions.Write()
    .Put(table.Transactions, transaction)
    .ExecuteAsync();
```
```

**Reason:** New attribute added in v0.9.0 for enforcing transactional writes.

---

#### Part 2: [Queryable] Deprecation Notice

**Files updated:**
- `docs/reference/AttributeReference.md`

**Before:**
```markdown
## [Queryable]

Marks a property as queryable and specifies the supported operations and indexes.
```

**After:**
```markdown
## [Queryable] ⚠️ DEPRECATED

> **Deprecation Notice:** The `[Queryable]` attribute is deprecated as of v0.9.0.
> Query capabilities are now automatically derived from `[PartitionKey]` and `[SortKey]` attributes.
> This attribute will be removed in v1.0.
>
> **Migration:** Remove `[Queryable]` attributes from your entities. The source generator
> automatically determines supported operations based on key attributes.

Marks a property as queryable and specifies the supported operations and indexes.
```

**Reason:** `[Queryable]` is deprecated in v0.9.0 - query capabilities are now derived from key metadata.

---

#### Part 3: [DynamoDbTable] Namespace Parameter

**Files updated:**
- `docs/reference/AttributeReference.md`

**Before:**
```markdown
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `tableName` | `string` | Yes | The DynamoDB table name |
```

**After:**
```markdown
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `tableName` | `string` | Yes | The DynamoDB table name |

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Namespace` | `string?` | `null` | Custom namespace for the generated table class. If null, uses the entity's namespace. |
```

**Reason:** New `Namespace` property added in v0.9.0 for controlling generated table class namespace.

---

### File: docs/core-features/Configuration.md - Default Request Options

**Category:** Documentation Update - New Configuration Options

**Summary:** Added documentation for new default request options in `FluentDynamoDbOptions`.

**Added:**
```markdown
## Default Request Options

Configure default settings that apply to all request builders.

### Consistent Reads

```csharp
var options = new FluentDynamoDbOptions()
    .UseConsistentRead(true);

// All Get and Query operations now use consistent reads by default
var user = await table.Users.Get(userId).GetItemAsync();
```

### Return Consumed Capacity

```csharp
var options = new FluentDynamoDbOptions()
    .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL);

// All operations return consumed capacity
var response = await table.Users.Query()
    .Where(x => x.Pk == tenantId)
    .ToDynamoDbResponseAsync();
// response.ConsumedCapacity is populated
```

### Return Values

```csharp
var options = new FluentDynamoDbOptions()
    .ReturnValues(ReturnValue.ALL_NEW);

// Update operations return the new values by default
var response = await table.Users.Update(userId)
    .Set(x => new UserUpdateModel { Name = "Jane" })
    .ToDynamoDbResponseAsync();
// response.Attributes contains the updated item
```

### Overriding Defaults

Explicit builder method calls override default options:

```csharp
// Default is consistent read
var options = new FluentDynamoDbOptions().UseConsistentRead(true);

// This specific query uses eventually consistent read
var users = await table.Users.Query()
    .Where(x => x.Pk == tenantId)
    .UseConsistentRead(false)  // Overrides default
    .ToListAsync();
```
```

**Reason:** New default request options feature added in v0.9.0.
## [2025-12-06]

### File: Multiple documentation files - Batch and Transaction API Pattern Corrections

**Category:** API Correction - Static Entry Point Patterns

**Summary:** Corrected batch and transaction operation examples across documentation to use the correct static entry point patterns (`DynamoDbBatch.Write`, `DynamoDbBatch.Get`, `DynamoDbTransactions.Write`, `DynamoDbTransactions.Get`) instead of incorrect constructor-based patterns. Also corrected `CommitAsync()` to `ExecuteAsync()` for transaction execution.

---

#### Part 1: Batch Operation Pattern Corrections

**Files corrected:**
- `docs/core-features/BasicOperations.md`
- `docs/advanced-topics/PerformanceOptimization.md`
- `docs/advanced-topics/GlobalSecondaryIndexes.md`
- `docs/QUICK_REFERENCE.md`

**Before (Incorrect - constructor-based patterns):**
```csharp
// Batch Write - INCORRECT
var batchWrite = new BatchWriteItemRequestBuilder(client);
batchWrite.AddPutItem(table, user1);
batchWrite.AddPutItem(table, user2);
await batchWrite.ExecuteAsync();

// Batch Get - INCORRECT
var batchGet = new BatchGetItemRequestBuilder(client);
batchGet.AddKey(table, pk1, sk1);
batchGet.AddKey(table, pk2, sk2);
var response = await batchGet.ExecuteAsync();
```

**After (Correct - static entry point patterns):**
```csharp
// Batch Write - CORRECT
await DynamoDbBatch.Write
    .Add(table.Users.Put(user1))
    .Add(table.Users.Put(user2))
    .ExecuteAsync(client);

// Batch Get - CORRECT
var response = await DynamoDbBatch.Get
    .Add(table.Users.Get(pk1, sk1))
    .Add(table.Users.Get(pk2, sk2))
    .ExecuteAsync(client);
```

**Reason:** The `BatchWriteItemRequestBuilder` and `BatchGetItemRequestBuilder` constructors are internal implementation details. The correct public API uses `DynamoDbBatch.Write` and `DynamoDbBatch.Get` static entry points with the fluent `.Add()` pattern.

---

#### Part 2: Transaction Operation Pattern Corrections

**Files corrected:**
- `docs/advanced-topics/CompositeEntities.md`
- `docs/QUICK_REFERENCE.md`
- `docs/DeveloperGuide.md`

**Before (Incorrect - constructor-based patterns):**
```csharp
// Transaction Write - INCORRECT
var transaction = new TransactWriteItemsRequestBuilder(client);
transaction.AddPut(table, order);
transaction.AddPut(table, orderLine);
await transaction.CommitAsync();

// Transaction Get - INCORRECT
var transaction = new TransactGetItemsRequestBuilder(client);
transaction.AddGet(table, pk1, sk1);
var response = await transaction.ExecuteAsync();
```

**After (Correct - static entry point patterns):**
```csharp
// Transaction Write - CORRECT
await DynamoDbTransactions.Write
    .Add(table.Orders.Put(order))
    .Add(table.OrderLines.Put(orderLine))
    .ExecuteAsync(client);

// Transaction Get - CORRECT
var response = await DynamoDbTransactions.Get
    .Add(table.Orders.Get(pk1, sk1))
    .Add(table.OrderLines.Get(pk2, sk2))
    .ExecuteAsync(client);
```

**Reason:** The `TransactWriteItemsRequestBuilder` and `TransactGetItemsRequestBuilder` constructors are internal implementation details. The correct public API uses `DynamoDbTransactions.Write` and `DynamoDbTransactions.Get` static entry points with the fluent `.Add()` pattern.

---

#### Part 3: CommitAsync to ExecuteAsync Corrections

**Files corrected:**
- `docs/advanced-topics/CompositeEntities.md`
- `docs/advanced-topics/MultiEntityTables.md`
- `docs/getting-started/SingleEntityTables.md`

**Before (Incorrect method name):**
```csharp
await DynamoDbTransactions.Write
    .Add(table.Orders.Put(order))
    .Add(table.OrderLines.Put(orderLine))
    .CommitAsync(client);
```

**After (Correct method name):**
```csharp
await DynamoDbTransactions.Write
    .Add(table.Orders.Put(order))
    .Add(table.OrderLines.Put(orderLine))
    .ExecuteAsync(client);
```

**Reason:** The `CommitAsync()` method does not exist on `TransactionWriteBuilder`. The correct method is `ExecuteAsync()`, which is consistent with other builder patterns in the library.

---

#### Part 4: STSIntegration.md Reorganization

**Files affected:**
- `docs/advanced-topics/STSIntegration.md` (deleted)
- `docs/advanced-topics/ClientConfiguration.md` (created)
- `docs/advanced-topics/ScopedSecurity.md` (created)
- `docs/advanced-topics/README.md` (updated)

**Before (Single conflated document):**
```
docs/advanced-topics/STSIntegration.md
- Mixed client configuration topics (DynamoDB Local, LocalStack, multi-region)
- Mixed STS-scoped credentials topics (WithClient, multi-tenancy)
```

**After (Focused documents):**
```
docs/advanced-topics/ClientConfiguration.md
- Development environments (DynamoDB Local, LocalStack)
- Custom client settings (timeouts, retries, connection pooling)
- Multi-region deployments (static routing)
- Proxy configuration

docs/advanced-topics/ScopedSecurity.md
- WithClient() method for per-request client customization
- STS-scoped credentials for multi-tenancy
- Complete multi-tenancy implementation example
- Security best practices
- Performance considerations (client reuse, credential caching)
```

**Reason:** The original `STSIntegration.md` conflated two distinct topics: client configuration (applied at table creation time) and scoped security (per-request client customization). Splitting into focused documents improves discoverability and allows developers to find relevant information more easily.

---

#### Part 5: Verification Pass - Additional Pattern Corrections

**Files corrected:**
- `docs/advanced-topics/CompositeEntities.md`
- `docs/advanced-topics/MultiEntityTables.md`

**CompositeEntities.md - Batch Operations Section:**

**Before (Incorrect - constructor-based pattern):**
```csharp
// Batch write for composite entity
var batchBuilder = new BatchWriteItemRequestBuilder(client);

// Add order header
batchBuilder.Put(table, builder => builder
    .WithItem(/* order header attributes */));

// Add all line items in batch
foreach (var item in order.Items)
{
    batchBuilder.Put(table, builder => builder
        .WithItem(/* line item attributes */));
}

// Execute batch (up to 25 items per batch)
await batchBuilder.ExecuteAsync();
```

**After (Correct - static entry point pattern):**
```csharp
// Batch write for composite entity using static entry point
var batchBuilder = DynamoDbBatch.Write
    .WithClient(client);

// Add order header
batchBuilder.Add(table.Orders.Put(orderHeader));

// Add all line items in batch
foreach (var item in order.Items)
{
    batchBuilder.Add(table.OrderLines.Put(item));
}

// Execute batch (up to 25 items per batch)
await batchBuilder.ExecuteAsync();
```

**MultiEntityTables.md - Generated Table Class Example:**

**Before (Incorrect - showing transaction/batch methods as generated):**
```csharp
// Transaction and batch operations (table level only)
public TransactWriteItemsRequestBuilder TransactWrite()
{
    return new TransactWriteItemsRequestBuilder(Client);
}

public BatchWriteItemBuilder BatchWrite()
{
    return new BatchWriteItemBuilder(Client);
}
```

**After (Correct - removed from generated code example):**
Transaction and batch methods removed from the generated code example because the source generator does not generate these methods. Transaction and batch operations are inherently cross-table and should use the static entry points `DynamoDbTransactions.Write` and `DynamoDbBatch.Write` directly.

**Reason:** These patterns were missed in the initial documentation correction pass. The verification step identified remaining constructor-based patterns that needed to be updated to use the correct static entry point patterns.

---

#### Part 6: Documentation API Style Corrections

**Files corrected:**
- `docs/reference/AdvancedTypesMigration.md`
- `docs/CodeExamples.md`
- `docs/advanced-topics/PerformanceOptimization.md`
- `docs/reference/AdoptionGuide.md`

**Summary:** Corrected API patterns to use typed table classes, entity accessors, and lambda expressions following the documentation style priority (lambda expressions preferred over format strings over manual WithValue).

---

**AdvancedTypesMigration.md - Multiple Corrections:**

**Before (Incorrect - non-existent generic type):**
```csharp
private readonly DynamoDbTableBase<Product> _table;

var response = await _table.Get
    .WithKey("pk", productId)
    .ExecuteAsync<Product>();

await _table.Put
    .WithItem(product)
    .ExecuteAsync();
```

**After (Correct - typed table class with entity accessors):**
```csharp
private readonly ProductTable _table;

var product = await _table.Products.GetAsync(productId);

await _table.Products.PutAsync(product);
```

**Reason:** `DynamoDbTableBase<T>` does not exist - `DynamoDbTableBase` is not generic. Use concrete typed table classes with entity accessors for type-safe operations.

---

**CodeExamples.md - Get Request Method Correction:**

**Before (Incorrect method name):**
```csharp
var response = await _table.Get()
    .WithClient(scopedClient)
    .WithKey(TenantResourceFields.Pk, TenantResourceKeys.Pk(tenantId, resourceType))
    .WithKey(TenantResourceFields.Sk, TenantResourceKeys.Sk(resourceId))
    .ExecuteAsync<TenantResource>();
```

**After (Correct method name):**
```csharp
var response = await _table.Get()
    .WithClient(scopedClient)
    .WithKey(TenantResourceFields.Pk, TenantResourceKeys.Pk(tenantId, resourceType))
    .WithKey(TenantResourceFields.Sk, TenantResourceKeys.Sk(resourceId))
    .GetItemAsync<TenantResource>();
```

**Reason:** `ExecuteAsync<T>()` does not exist on `GetItemRequestBuilder`. The correct method is `GetItemAsync<T>()`.

---

**PerformanceOptimization.md - Get Request Method Correction:**

**Before (Incorrect method name):**
```csharp
await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>();

await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .UsingConsistentRead()
    .ExecuteAsync<User>();
```

**After (Correct method name):**
```csharp
await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .GetItemAsync<User>();

await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .UsingConsistentRead()
    .GetItemAsync<User>();
```

**Reason:** `ExecuteAsync<T>()` does not exist on `GetItemRequestBuilder`. The correct method is `GetItemAsync<T>()`.

---

**AdoptionGuide.md - Get Request Method Correction:**

**Before (Incorrect method name):**
```csharp
var order = await table.Get
    .WithKey(OrderFields.Pk, OrderKeys.Pk("tenant123", "order456"))
    .ExecuteAsync<Order>();
```

**After (Correct method name):**
```csharp
var order = await table.Get
    .WithKey(OrderFields.Pk, OrderKeys.Pk("tenant123", "order456"))
    .GetItemAsync<Order>();
```

**Reason:** `ExecuteAsync<T>()` does not exist on `GetItemRequestBuilder`. The correct method is `GetItemAsync<T>()`.

---

**AdvancedTypesMigration.md - Versioned Entities Section:**

**Before (Incorrect method name):**
```csharp
public async Task<IProduct> GetProductAsync(string id, int version = 2)
{
    if (version == 1)
    {
        return await _tableV1.Get.WithKey("pk", id).ExecuteAsync<ProductV1>();
    }
    else
    {
        return await _tableV2.Get.WithKey("pk", id).ExecuteAsync<ProductV2>();
    }
}
```

**After (Correct method name and return pattern):**
```csharp
public async Task<IProduct?> GetProductAsync(string id, int version = 2)
{
    if (version == 1)
    {
        var response = await _tableV1.Get.WithKey("pk", id).GetItemAsync<ProductV1>();
        return response.Item;
    }
    else
    {
        var response = await _tableV2.Get.WithKey("pk", id).GetItemAsync<ProductV2>();
        return response.Item;
    }
}
```

**Reason:** `ExecuteAsync<T>()` does not exist on `GetItemRequestBuilder`. The correct method is `GetItemAsync<T>()` which returns a response object containing the `Item` property.

---

**AdoptionGuide.md - Removed Misleading "Dynamic Table Names" Example:**

**Before (Removed - misleading pattern):**
```csharp
### Use Case 2: Dynamic Table Names with Generated Entities

[DynamoDbTable("users")] // Default table name
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
}

// Use different table at runtime
var table = new DynamoDbTableBase(client, GetTableNameForTenant(tenantId));

var response = await table.Get()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .GetItemAsync<User>();
```

**After:** Section removed entirely. Use Case 3 renumbered to Use Case 2.

**Reason:** This example was misleading because it showed creating a raw `DynamoDbTableBase` with a dynamic table name, then using generated field constants (`UserFields`, `UserKeys`) which are generated for typed table classes, not for raw `DynamoDbTableBase`. The generated entity accessors, indexes, and convenience methods are all part of the generated table class - using `DynamoDbTableBase` directly loses most of what the library provides. For multi-tenant scenarios with different table names, use the generated table class constructor which accepts a table name parameter.

---

#### Part 7: Batch Static Entry Point Client Parameter Correction

**Files corrected:**
- `docs/getting-started/SingleEntityTables.md`

**Before (Incorrect - client as constructor parameter, generic methods):**
```csharp
// Batch write operations - INCORRECT
await DynamoDbBatch.Write(client)
    .Add(usersTable.Put<User>().WithItem(user1))
    .Add(usersTable.Put<User>().WithItem(user2))
    .Add(usersTable.Delete<User>().WithKey(User.Fields.UserId, "user3"))
    .ExecuteAsync();

// Batch get operations - INCORRECT
var batchGetResponse = await DynamoDbBatch.Get(client)
    .Add(usersTable.Get<User>().WithKey(User.Fields.UserId, "user1"))
    .ExecuteAsync();
```

**After (Correct - static property, entity accessor pattern):**
```csharp
// Batch write operations - CORRECT
await DynamoDbBatch.Write
    .Add(usersTable.Users.Put(user1))
    .Add(usersTable.Users.Put(user2))
    .Add(usersTable.Users.Delete("user3"))
    .ExecuteAsync();

// Batch get operations - CORRECT
var batchGetResponse = await DynamoDbBatch.Get
    .Add(usersTable.Users.Get("user1"))
    .ExecuteAsync();
```

**Reason:** 
1. `DynamoDbBatch.Write` and `DynamoDbBatch.Get` are static properties that return new builder instances, not methods that accept a client parameter. The client is automatically inferred from the first request builder added, or can be explicitly specified using `.WithClient(client)` or by passing to `.ExecuteAsync(client: client)`.
2. Use entity accessor pattern (`usersTable.Users.Put(user)`) instead of generic methods (`usersTable.Put<User>().WithItem(user)`) for cleaner, more readable code.

---

#### Part 8: Transaction and Batch Pattern Corrections (Multiple Files)

**Files corrected:**
- `docs/advanced-topics/MultiEntityTables.md`
- `docs/DeveloperGuide.md`
- `docs/reference/ErrorHandling.md`
- `docs/QUICK_REFERENCE.md`
- `docs/reference/LoggingTroubleshooting.md`
- `docs/reference/Troubleshooting.md`
- `docs/core-features/encryption-guide.md`

**Before (Incorrect - table-level TransactWrite/BatchWrite methods):**
```csharp
// Transaction - INCORRECT (table method doesn't exist)
await ecommerceTable.TransactWrite()
    .AddPut(ecommerceTable.Orders, order)
    .AddPut(ecommerceTable.OrderLines, line1)
    .ExecuteAsync();

// Batch write - INCORRECT
await ecommerceTable.BatchWrite()
    .AddPut(order1)
    .AddPut(line1)
    .ExecuteAsync();

// Batch get - INCORRECT
var batchResponse = await ecommerceTable.BatchGet()
    .AddKey(OrderKeys.Pk("customer123"), OrderKeys.Sk("ORDER#order456"))
    .ExecuteAsync();
```

**After (Correct - static entry points with entity accessors):**
```csharp
// Transaction - CORRECT (static entry point)
await DynamoDbTransactions.Write
    .Add(ecommerceTable.Orders.Put(order))
    .Add(ecommerceTable.OrderLines.Put(line1))
    .ExecuteAsync();

// Batch write - CORRECT
await DynamoDbBatch.Write
    .Add(ecommerceTable.Orders.Put(order1))
    .Add(ecommerceTable.OrderLines.Put(line1))
    .ExecuteAsync();

// Batch get - CORRECT
var batchResponse = await DynamoDbBatch.Get
    .Add(ecommerceTable.Orders.Get("customer123", "ORDER#order456"))
    .ExecuteAsync();
```

**Reason:** The source generator does not generate `TransactWrite()`, `BatchWrite()`, or `BatchGet()` methods on table classes. Transaction and batch operations are inherently cross-table and use static entry points (`DynamoDbTransactions.Write`, `DynamoDbBatch.Write`, `DynamoDbBatch.Get`). Also updated to use entity accessor pattern for cleaner code.

---

#### Part 9: GlobalSecondaryIndexes.md API Pattern Corrections

**Files corrected:**
- `docs/advanced-topics/GlobalSecondaryIndexes.md`

**Before (Incorrect - UsingIndex pattern, ExecuteAsync on queries):**
```csharp
// Query using UsingIndex - INCORRECT
var response = await table.Query
    .UsingIndex(OrderIndexes.StatusIndex)
    .Where($"{OrderFields.StatusIndex.Status} = {{0}}", "pending")
    .ExecuteAsync<Order>();

foreach (var order in response.Items) { ... }
```

**After (Correct - index accessor, lambda expressions, ToListAsync):**
```csharp
// Query using index accessor - CORRECT
var orders = await table.StatusIndex.Query<Order>()
    .Where(x => x.Status == "pending")
    .ToListAsync();

foreach (var order in orders) { ... }
```

**Reason:** 
1. Use index accessor pattern (`table.StatusIndex.Query<Order>()`) instead of `table.Query.UsingIndex()` for cleaner, more discoverable API
2. Use lambda expressions (`x => x.Status == "pending"`) instead of format strings for type-safe queries
3. Use `ToListAsync()` instead of `ExecuteAsync<T>()` for queries - `ExecuteAsync<T>()` does not exist on QueryRequestBuilder
4. When pagination is needed, use `ToResponseAsync()` to get the full response with `LastEvaluatedKey`

---

## [2025-12-05]

### File: Multiple documentation files - Release 0.8.0 Documentation Corrections

**Category:** Pattern Update - Installation Instructions and API Patterns

**Summary:** Corrected installation instructions and API patterns across documentation for the 0.8.0 release. This includes removing references to non-existent separate packages and updating property-based API patterns to method-based patterns.

---

#### Part 1: Installation Instruction Corrections

**Files corrected:**
- `README.md` (Quick Start section)
- `docs/getting-started/QuickStart.md`
- `docs/getting-started/Installation.md`

**Before (Incorrect - referencing non-existent packages):**
```bash
dotnet add package Oproto.FluentDynamoDb
dotnet add package Oproto.FluentDynamoDb.SourceGenerator
dotnet add package Oproto.FluentDynamoDb.Attributes
```

**After (Correct - single package installation):**
```bash
dotnet add package Oproto.FluentDynamoDb
```

**Reason:** The source generator is bundled in the main NuGet package (included as an analyzer). The attributes are also in the main package. There are no separate `Oproto.FluentDynamoDb.SourceGenerator` or `Oproto.FluentDynamoDb.Attributes` packages to install.

---

#### Part 2: API Pattern Corrections (Property-based to Method-based)

**Files corrected:**
- `README.md`
- `docs/getting-started/QuickStart.md`
- `docs/core-features/BasicOperations.md`
- `docs/core-features/LinqExpressions.md`
- `docs/advanced-topics/AdvancedTypes.md`
- `docs/advanced-topics/Discriminators.md`
- `docs/reference/ErrorHandling.md`
- `docs/reference/AdoptionGuide.md`
- `docs/reference/AdvancedTypesQuickReference.md`
- `docs/reference/LoggingTroubleshooting.md`
- `docs/TroubleshootingGuide.md`
- `Oproto.FluentDynamoDb/Expressions/EXPRESSION_EXAMPLES.md`

**Before (Property-based access - deprecated):**
```csharp
// Property-based patterns (OLD - do not use)
await table.Put.WithItem(user).PutAsync();
await table.Query.Where(...).ToListAsync();
await table.Get.WithKey(...).GetItemAsync();
await table.Update.WithKey(...).UpdateAsync();
await table.Delete.WithKey(...).DeleteAsync();
await table.Scan.ToListAsync();
```

**After (Method-based access - correct):**
```csharp
// Option 1: Convenience Methods (simplest - for basic operations)
await table.Users.PutAsync(user);
await table.Users.GetAsync("user123");
await table.Users.DeleteAsync("user123");

// Option 2: Entity Accessor + Builder (for operations with options)
await table.Users.Put(user)
    .Where(x => x.Pk.AttributeNotExists())
    .PutAsync();

await table.Users.Query()
    .Where(x => x.Status == "active")
    .ToListAsync();

await table.Users.Get("user123")
    .WithProjection("name, email")
    .GetItemAsync();

await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Status = "inactive" })
    .UpdateAsync();

await table.Users.Delete("user123")
    .Where(x => x.Status == "pending")
    .DeleteAsync();

// Option 3: Generic Methods (for dynamic scenarios)
await table.Put<User>().WithItem(user).PutAsync();
await table.Query<User>().Where(...).ToListAsync();
await table.Get<User>().WithKey(...).GetItemAsync();
```

**Reason:** The property-based API patterns (`table.Put.`, `table.Query.`, etc.) were deprecated in favor of method-based patterns (`table.Put()`, `table.Query()`, etc.). The method-based patterns provide better IntelliSense support and are consistent with the generated entity accessor patterns.

**API Style Priority (from documentation.md steering):**
1. **Convenience Methods** - Direct async methods for simple operations (`PutAsync(item)`, `GetAsync(pk)`)
2. **Entity Accessor + Builder** - For operations requiring conditions, projections, etc.
3. **Generic Methods** - For dynamic scenarios where entity type is determined at runtime

---

#### Part 3: Additional API Pattern and Package Reference Corrections (Verification Pass)

**Files corrected:**
- `docs/reference/AdvancedTypesMigration.md`
- `docs/core-features/encryption-guide.md`
- `docs/core-features/format-strings-guide.md`
- `docs/reference/Troubleshooting.md`
- `docs/advanced-topics/FieldLevelSecurity.md`

**API Pattern Corrections:**

**Before (Property-based access):**
```csharp
await _table.Put.WithItem(oldProduct).ExecuteAsync();
await _table.Scan.ExecuteAsync<Session>();
await foreach (var user in table.Scan.ExecuteAsync())
```

**After (Method-based access):**
```csharp
await _table.Put<Product>().WithItem(oldProduct).ExecuteAsync();
await _table.Scan<Session>().ExecuteAsync();
await foreach (var user in table.Scan().ExecuteAsync())
```

**Package Reference Corrections:**

**Before (Incorrect - referencing non-existent packages):**
```bash
dotnet add package Oproto.FluentDynamoDb
dotnet add package Oproto.FluentDynamoDb.SourceGenerator
dotnet add package Oproto.FluentDynamoDb.Attributes
```

**After (Correct - single package installation):**
```bash
dotnet add package Oproto.FluentDynamoDb
```

**Reason:** These files were missed in the initial documentation correction pass. The verification step identified remaining property-based API patterns and incorrect package references that needed to be updated.

---

## [2025-12-05]

### File: Multiple documentation files - Namespace Reorganization

**Category:** Documentation Restructuring

**Summary:** Updated all documentation references to reflect the namespace reorganization from the monolithic `Oproto.FluentDynamoDb.Storage` namespace to the new organized namespace structure.

**Namespace Changes:**

| Old Namespace | New Namespace | Types |
|---------------|---------------|-------|
| `Oproto.FluentDynamoDb.Storage` | `Oproto.FluentDynamoDb.Storage` | DynamoDbTableBase, DynamoDbIndex, IDynamoDbTable (unchanged) |
| `Oproto.FluentDynamoDb.Storage` | `Oproto.FluentDynamoDb.Entities` | IDynamoDbEntity, IProjectionModel, IDiscriminatedProjection |
| `Oproto.FluentDynamoDb.Storage` | `Oproto.FluentDynamoDb.Metadata` | EntityMetadata, PropertyMetadata, RelationshipMetadata, IndexMetadata, IEntityMetadataProvider |
| `Oproto.FluentDynamoDb.Storage` | `Oproto.FluentDynamoDb.Hydration` | IAsyncEntityHydrator, IEntityHydratorRegistry, DefaultEntityHydratorRegistry |
| `Oproto.FluentDynamoDb.Storage` | `Oproto.FluentDynamoDb.Providers.Encryption` | IFieldEncryptor, FieldEncryptionContext |
| `Oproto.FluentDynamoDb.Storage` | `Oproto.FluentDynamoDb.Providers.BlobStorage` | IBlobStorageProvider, IJsonBlobSerializer |
| `Oproto.FluentDynamoDb.Storage` | `Oproto.FluentDynamoDb.Mapping` | MappingErrorHandler, DynamoDbMappingException, DiscriminatorMismatchException, ProjectionValidationException, FieldEncryptionException |
| `Oproto.FluentDynamoDb.Storage` | `Oproto.FluentDynamoDb.Context` | DynamoDbOperationContext, DynamoDbOperationContextDiagnostics, OperationContextData |

---

**Before:**
```csharp
using Oproto.FluentDynamoDb.Storage;

// All types were in the Storage namespace
public class MyEntity : IDynamoDbEntity { }
var metadata = new EntityMetadata();
var context = DynamoDbOperationContext.Current;
```

**After:**
```csharp
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Context;

// Types are now in dedicated namespaces
public class MyEntity : IDynamoDbEntity { }
var metadata = new EntityMetadata();
var context = DynamoDbOperationContext.Current;
```

**Reason:** The `Storage/` folder previously contained a mix of concerns (physical storage, entity contracts, metadata, hydration, providers, mapping, and context). This reorganization separates these concerns into distinct folders and namespaces, improving code organization, discoverability, and maintainability. This is a breaking change for users who import types from `Oproto.FluentDynamoDb.Storage` that have been moved to new namespaces.

**Migration Guide:**
1. Update `using Oproto.FluentDynamoDb.Storage;` to the appropriate new namespace(s) based on the types you use
2. For entity interfaces (`IDynamoDbEntity`, `IProjectionModel`, `IDiscriminatedProjection`): use `Oproto.FluentDynamoDb.Entities`
3. For metadata classes: use `Oproto.FluentDynamoDb.Metadata`
4. For hydration interfaces: use `Oproto.FluentDynamoDb.Hydration`
5. For encryption providers: use `Oproto.FluentDynamoDb.Providers.Encryption`
6. For blob storage providers: use `Oproto.FluentDynamoDb.Providers.BlobStorage`
7. For mapping exceptions: use `Oproto.FluentDynamoDb.Mapping`
8. For operation context: use `Oproto.FluentDynamoDb.Context`

---

## [2025-12-04]

### File: Multiple documentation files - Put().ExecuteAsync() → PutAsync() corrections

**Category:** API Correction

**Summary:** Corrected remaining `Put().ExecuteAsync()` patterns to use the correct `PutAsync()` method across documentation and source files.

**Files corrected:**
- `docs/advanced-topics/AdvancedTypes.md` (2 occurrences)
- `docs/advanced-topics/TableGenerationCustomization.md` (10 occurrences)
- `docs/reference/AttributeReference.md` (1 occurrence)
- `docs/DOCUMENTATION_CHANGELOG.md` (1 occurrence - example code)
- `Oproto.FluentDynamoDb.SystemTextJson/README.md` (1 occurrence)
- `Oproto.FluentDynamoDb.NewtonsoftJson/README.md` (1 occurrence)
- `Oproto.FluentDynamoDb/Attributes/GenerateAccessorsAttribute.cs` (1 occurrence - XML documentation)
- `.kiro/specs/integration-test-build-fixes/design.md` (1 occurrence)

---

**Before:**
```csharp
await table.Documents.Put(document).ExecuteAsync();
await table.Orders.Put(order).ExecuteAsync();
await OrderLines.Put(line).ExecuteAsync();
```

**After:**
```csharp
await table.Documents.Put(document).PutAsync();
await table.Orders.Put(order).PutAsync();
await OrderLines.Put(line).PutAsync();
```

**Reason:** `ExecuteAsync()` does not exist on `PutItemRequestBuilder`. The correct method is `PutAsync()`. This is consistent with other request builders: `GetItemAsync()`, `UpdateAsync()`, `DeleteAsync()`.

---

## [2025-12-04]

### File: docs/DOCUMENTATION_CHANGELOG.md, docs/examples/ProjectionModelsExamples.md, docs/core-features/ProjectionModels.md, docs/advanced-topics/FieldLevelSecurity.md

**Category:** Pattern Update - Example Entity Cleanup

**Summary:** Cleaned up entity definitions in documentation to follow correct attribute patterns. Removed incorrect `[DynamoDbEntity]` attribute from table entities, removed manual `: IDynamoDbEntity` interface implementations, and removed redundant `CreatePk()`/`CreateSk()` methods.

---

**Before (Incorrect - combining attributes):**
```csharp
[DynamoDbEntity]  // ❌ Not needed for table entities
[DynamoDbTable("Orders")]
public partial class Order : IDynamoDbEntity  // ❌ Auto-generated by source generator
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    
    // ❌ Duplicates source-generated key methods
    public static string CreatePk(string orderId) => $"ORDER#{orderId}";
    public static string CreateSk() => MetaSk;
}
```

**After (Correct - clean entity definition):**
```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]  // ✅ Configure prefix for generated Keys.Pk() method
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    
    // ✅ Use source-generated Order.Keys.Pk(orderId) instead
}
```

**Reason:** 
- `[DynamoDbEntity]` is only for nested map types requiring hydration, not for top-level table entities
- `: IDynamoDbEntity` is automatically added by the source generator's partial class implementation
- Manual `CreatePk()`/`CreateSk()` methods duplicate the source-generated `Keys` class functionality
- Use `[PartitionKey(Prefix = "ORDER")]` to configure key formatting, then use `Order.Keys.Pk(orderId)` which returns `"ORDER#" + orderId`

---

**Before (Using manual key methods):**
```csharp
// Manual key construction
var pk = $"ORDER#{orderId}";
var sk = $"LINE#{lineId}";

// Or using manual CreatePk/CreateSk methods
var pk = Order.CreatePk(orderId);
var sk = OrderLine.CreateSk(lineId);
```

**After (Using source-generated Keys class):**
```csharp
// Use source-generated Keys class methods
var pk = Order.Keys.Pk(orderId);      // Returns "ORDER#" + orderId
var sk = OrderLine.Keys.Sk(lineId);   // Returns "LINE#" + lineId

// Or get both keys at once
var (pk, sk) = Order.Keys.Key(orderId, sortKeyValue);
```

**Reason:** The source generator creates a `Keys` nested class with `Pk()`, `Sk()`, and `Key()` methods that use the configured `Prefix` and `Separator` values from the key attributes. This eliminates manual key construction and ensures consistency.

---

## [2025-12-01]

### File: docs/core-features/QueryingData.md, docs/getting-started/SingleEntityTables.md

**Category:** Pattern Update - Scan Opt-In Pattern

**Before:**
```csharp
// Scan was available on all tables via base class
var allOrders = await table.Scan<Order>().ToListAsync();
```

**After:**
```csharp
// Scan now requires [Scannable] attribute on the entity
[DynamoDbTable("Orders")]
[Scannable]  // Required for Scan operations
public partial class Order { ... }

// Then use entity accessor or table method (if default entity)
var allOrders = await table.Orders.Scan().ToListAsync();
// Or for default entity:
var allOrders = await table.Scan().ToListAsync();
// Generic method still works when entity has [Scannable]:
var allOrders = await table.Scan<Order>().ToListAsync();
```

**Reason:** Scan operations are expensive and not a recommended DynamoDB access pattern. The `table.Scan<TEntity>()` method has been removed from `DynamoDbTableBase` to enforce an opt-in pattern. Developers must now explicitly add the `[Scannable]` attribute to entities that need Scan support. This prevents accidental table scans and encourages proper access pattern design.

**Migration Steps:**
1. Add `[Scannable]` attribute to entities that require Scan operations
2. Update code from `table.Scan<TEntity>()` to use entity accessor `table.Entitys.Scan()` or `table.Scan()` for default entity
3. The generic `table.Scan<TEntity>()` method is still available when the entity has `[Scannable]` attribute

---

## [2025-12-01]

### File: docs/core-features/BasicOperations.md

**Category:** API Correction

**Before:**
```csharp
var orderLines = await table.OrderLines.Query()
    .Where(x => x.OrderId == "order123")
    .ExecuteAsync();
```

**After:**
```csharp
var orderLines = await table.OrderLines.Query()
    .Where(x => x.OrderId == "order123")
    .ToListAsync();
```

**Reason:** `ExecuteAsync()` does not exist on QueryRequestBuilder. The correct method is `ToListAsync()` for returning a list of entities.

---

**Before:**
```csharp
await table.Put<User>().WithItem(user).ExecuteAsync();
```

**After:**
```csharp
await table.Put<User>().WithItem(user).PutAsync();
```

**Reason:** `ExecuteAsync()` does not exist on PutItemRequestBuilder. The correct method is `PutAsync()`.

---

**Before:**
```csharp
var response = await table.Get<User>()
    .WithKey(User.Fields.UserId, User.Keys.Pk("user123"))
    .WithKey(User.Fields.ProfileType, User.Keys.Sk("MAIN"))
    .ExecuteAsync();
```

**After:**
```csharp
var response = await table.Get<User>()
    .WithKey(User.Fields.UserId, User.Keys.Pk("user123"))
    .WithKey(User.Fields.ProfileType, User.Keys.Sk("MAIN"))
    .GetItemAsync();
```

**Reason:** `ExecuteAsync()` does not exist on GetItemRequestBuilder. The correct method is `GetItemAsync()`.

---

**Before:**
```csharp
var users = await table.Query<User>()
    .Where($"{User.Fields.UserId} = {{0}}", User.Keys.Pk("user123"))
    .ExecuteAsync();
```

**After:**
```csharp
var users = await table.Query<User>()
    .Where($"{User.Fields.UserId} = {{0}}", User.Keys.Pk("user123"))
    .ToListAsync();
```

**Reason:** `ExecuteAsync()` does not exist on QueryRequestBuilder. The correct method is `ToListAsync()`.

---

**Before:**
```csharp
await table.Delete<User>()
    .WithKey(User.Fields.UserId, User.Keys.Pk("user123"))
    .WithKey(User.Fields.ProfileType, User.Keys.Sk("MAIN"))
    .ExecuteAsync();
```

**After:**
```csharp
await table.Delete<User>()
    .WithKey(User.Fields.UserId, User.Keys.Pk("user123"))
    .WithKey(User.Fields.ProfileType, User.Keys.Sk("MAIN"))
    .DeleteAsync();
```

**Reason:** `ExecuteAsync()` does not exist on DeleteItemRequestBuilder. The correct method is `DeleteAsync()`.

---

**Before:**
```csharp
await table.Get<User>()
    .WithKey(User.Fields.UserId, pk)
    .WithKey(User.Fields.ProfileType, sk)
    .ExecuteAsync();
```

**After:**
```csharp
await table.Get<User>()
    .WithKey(User.Fields.UserId, pk)
    .WithKey(User.Fields.ProfileType, sk)
    .GetItemAsync();
```

**Reason:** `ExecuteAsync()` does not exist on GetItemRequestBuilder. The correct method is `GetItemAsync()`.

---

**Before:**
```csharp
var orders = await table.Query<Order>()
    .UsingIndex(Order.Indexes.StatusIndex)
    .Where($"{Order.Fields.Status} = {{0}}", Order.Keys.StatusIndex.Pk("pending"))
    .ExecuteAsync();
```

**After:**
```csharp
var orders = await table.Query<Order>()
    .UsingIndex(Order.Indexes.StatusIndex)
    .Where($"{Order.Fields.Status} = {{0}}", Order.Keys.StatusIndex.Pk("pending"))
    .ToListAsync();
```

**Reason:** `ExecuteAsync()` does not exist on QueryRequestBuilder. The correct method is `ToListAsync()`.

---

**Before:**
```csharp
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = if_not_exists({UserFields.Name}, {{0}})", "Default Name")
    .ExecuteAsync();
```

**After:**
```csharp
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = if_not_exists({UserFields.Name}, {{0}})", "Default Name")
    .UpdateAsync();
```

**Reason:** `ExecuteAsync()` does not exist on UpdateItemRequestBuilder. The correct method is `UpdateAsync()`.

---

**Before (Performance Considerations section):**
```csharp
.ExecuteAsync<User>()
// and
.UsingConsistentRead().ExecuteAsync<User>()
// and
await table.Get.WithKey(...).ExecuteAsync();
// and
await table.Put.WithItem(user).ExecuteAsync();
```

**After:**
```csharp
.GetItemAsync()
// and
.UsingConsistentRead().GetItemAsync()
// and
await table.Get.WithKey(...).GetItemAsync();
// and
await table.Put.WithItem(user).PutAsync();
```

**Reason:** Updated generic examples in Performance Considerations and Error Handling sections to use correct method names (`GetItemAsync()`, `PutAsync()`) instead of non-existent `ExecuteAsync()`.

---

### File: docs/core-features/QueryingData.md

**Category:** API Correction

**Before:**
```csharp
await table.Query
    .Where<User>(x => x.UserId == userId && x.SortKey.StartsWith("ORDER#"))
    .WithFilter<User>(x => x.Status == "ACTIVE" && x.Age >= 18)
    .ExecuteAsync();
```

**After:**
```csharp
await table.Query
    .Where<User>(x => x.UserId == userId && x.SortKey.StartsWith("ORDER#"))
    .WithFilter<User>(x => x.Status == "ACTIVE" && x.Age >= 18)
    .ToListAsync();
```

**Reason:** `ExecuteAsync()` does not exist on QueryRequestBuilder. The correct method is `ToListAsync()` for returning a list of entities.

---

**Before:**
```csharp
var response = await scannableTable.Scan
    .ExecuteAsync();
```

**After:**
```csharp
var response = await scannableTable.Scan
    .ToListAsync();
```

**Reason:** `ExecuteAsync()` does not exist on ScanRequestBuilder. The correct method is `ToListAsync()`.

---

**Summary of QueryingData.md corrections:**
- Replaced all `ExecuteAsync()` calls on Query builders with `ToListAsync()`
- Replaced all `ExecuteAsync()` calls on Scan builders with `ToListAsync()`
- Updated pagination examples to use `ToListAsync()`
- Updated GSI query examples to use `ToListAsync()`
- Updated performance optimization examples to use `ToListAsync()`
- Total of 35+ ExecuteAsync references corrected

---

## [2025-12-01]

### File: docs/core-features/BasicOperations.md

**Category:** Pattern Update

**Before (Put with Return Values):**
```csharp
// Builder API required for return values
var response = await table.Users.Put(user)
    .ReturnAllOldValues()
    .PutAsync();

// Check if an item was replaced
if (response.Attributes != null && response.Attributes.Count > 0)
{
    var oldUser = UserMapper.FromAttributeMap(response.Attributes);
    Console.WriteLine($"Replaced user: {oldUser.Name}");
}
```

**After:**
```csharp
// Option 1: Use ToDynamoDbResponseAsync to get the raw AWS SDK response
var response = await table.Users.Put(user)
    .ReturnAllOldValues()
    .ToDynamoDbResponseAsync();

if (response.Attributes != null && response.Attributes.Count > 0)
{
    var oldUser = UserMapper.FromAttributeMap(response.Attributes);
    Console.WriteLine($"Replaced user: {oldUser.Name}");
}

// Option 2: Primary API populates DynamoDbOperationContext automatically
await table.Users.Put(user)
    .ReturnAllOldValues()
    .PutAsync();

var context = DynamoDbOperationContext.Current;
if (context?.PreOperationValues != null && context.PreOperationValues.Count > 0)
{
    var oldUser = context.DeserializePreOperationValue<User>();
    // ...
}
```

**Reason:** `PutAsync()` returns `Task` (void), not a response object. To access `response.Attributes`, use `ToDynamoDbResponseAsync()` which returns the raw AWS SDK response. Alternatively, use `DynamoDbOperationContext.Current.PreOperationValues` for context-based access. Added warning about AsyncLocal not being suitable for unit testing.

---

**Before (Update with Return Values):**
```csharp
var response = await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Name = "Jane Doe" })
    .ReturnAllNewValues()
    .UpdateAsync();

var updatedUser = UserMapper.FromAttributeMap(response.Attributes);
```

**After:**
```csharp
// Option 1: Use ToDynamoDbResponseAsync
var response = await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Name = "Jane Doe" })
    .ReturnAllNewValues()
    .ToDynamoDbResponseAsync();

var updatedUser = UserMapper.FromAttributeMap(response.Attributes);

// Option 2: Use context-based access
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Name = "Jane Doe" })
    .ReturnAllNewValues()
    .UpdateAsync();

var context = DynamoDbOperationContext.Current;
var updatedUser = context?.DeserializePostOperationValue<User>();
```

**Reason:** `UpdateAsync()` returns `Task` (void), not a response object. To access `response.Attributes`, use `ToDynamoDbResponseAsync()`. Added alternative using `DynamoDbOperationContext.Current.PostOperationValues` and warning about AsyncLocal.

---

**Before (Delete with Return Values):**
```csharp
var response = await table.Users.Delete("user123")
    .ReturnAllOldValues()
    .DeleteAsync();

if (response.Attributes != null && response.Attributes.Count > 0)
{
    var deletedUser = UserMapper.FromAttributeMap(response.Attributes);
    // ...
}
```

**After:**
```csharp
// Option 1: Use ToDynamoDbResponseAsync
var response = await table.Users.Delete("user123")
    .ReturnAllOldValues()
    .ToDynamoDbResponseAsync();

if (response.Attributes != null && response.Attributes.Count > 0)
{
    var deletedUser = UserMapper.FromAttributeMap(response.Attributes);
    // ...
}

// Option 2: Use context-based access
await table.Users.Delete("user123")
    .ReturnAllOldValues()
    .DeleteAsync();

var context = DynamoDbOperationContext.Current;
var deletedUser = context?.DeserializePreOperationValue<User>();
```

**Reason:** `DeleteAsync()` returns `Task` (void), not a response object. To access `response.Attributes`, use `ToDynamoDbResponseAsync()`. Added alternative using `DynamoDbOperationContext.Current.PreOperationValues` and warning about AsyncLocal.

---

**Before (Mixing Patterns - CreateUserAsync):**
```csharp
public async Task<User?> CreateUserAsync(User user)
{
    var response = await _table.Users.Put(user)
        .Where("attribute_not_exists({0})", User.Fields.UserId)
        .ReturnAllOldValues()
        .PutAsync();
    
    return response.Attributes != null 
        ? UserMapper.FromAttributeMap(response.Attributes) 
        : null;
}
```

**After:**
```csharp
public async Task<User?> CreateUserAsync(User user)
{
    var response = await _table.Users.Put(user)
        .Where("attribute_not_exists({0})", User.Fields.UserId)
        .ReturnAllOldValues()
        .ToDynamoDbResponseAsync();
    
    return response.Attributes != null 
        ? UserMapper.FromAttributeMap(response.Attributes) 
        : null;
}
```

**Reason:** Changed `PutAsync()` to `ToDynamoDbResponseAsync()` since the code needs to access `response.Attributes` directly.


---

## [2025-12-01]

### File: Oproto.FluentDynamoDb/Requests/DeleteItemRequestBuilder.cs

**Category:** API Correction (XML Documentation)

**Before:**
```csharp
/// // Simple delete by primary key
/// await table.Delete<Transaction>()
///     .WithKey("id", "user123")
///     .ExecuteAsync();
/// 
/// // Conditional delete with return values
/// var response = await table.Delete<Transaction>()
///     .WithKey("pk", "USER", "sk", "user123")
///     .Where("attribute_exists(#status)")
///     .WithAttribute("#status", "status")
///     .ReturnAllOldValues()
///     .ExecuteAsync();
```

**After:**
```csharp
/// // Simple delete by primary key
/// await table.Delete<Transaction>()
///     .WithKey("id", "user123")
///     .DeleteAsync();
/// 
/// // Conditional delete with return values (use ToDynamoDbResponseAsync to access response.Attributes)
/// var response = await table.Delete<Transaction>()
///     .WithKey("pk", "USER", "sk", "user123")
///     .Where("attribute_exists(#status)")
///     .WithAttribute("#status", "status")
///     .ReturnAllOldValues()
///     .ToDynamoDbResponseAsync();
```

**Reason:** `ExecuteAsync()` does not exist on DeleteItemRequestBuilder. The correct methods are `DeleteAsync()` for void operations and `ToDynamoDbResponseAsync()` when accessing response attributes.

---

### File: Oproto.FluentDynamoDb/Requests/UpdateItemRequestBuilder.cs

**Category:** API Correction (XML Documentation)

**Before:**
```csharp
/// // Update specific attributes
/// var response = await table.Update<Transaction>()
///     .WithKey("id", "123")
///     .Set("SET #name = :name, #status = :status")
///     ...
///     .ExecuteAsync();
/// 
/// // Conditional update
/// var response = await table.Update<Transaction>()
///     .WithKey("id", "123")
///     .Set("SET #count = #count + :inc")
///     .Where("attribute_exists(id)")
///     ...
///     .ExecuteAsync();
```

**After:**
```csharp
/// // Update specific attributes
/// await table.Update<Transaction>()
///     .WithKey("id", "123")
///     .Set("SET #name = :name, #status = :status")
///     ...
///     .UpdateAsync();
/// 
/// // Conditional update with return values (use ToDynamoDbResponseAsync to access response.Attributes)
/// var response = await table.Update<Transaction>()
///     .WithKey("id", "123")
///     .Set("SET #count = #count + :inc")
///     .Where("attribute_exists(id)")
///     ...
///     .ReturnAllNewValues()
///     .ToDynamoDbResponseAsync();
```

**Reason:** `ExecuteAsync()` does not exist on UpdateItemRequestBuilder. The correct methods are `UpdateAsync()` for void operations and `ToDynamoDbResponseAsync()` when accessing response attributes.

---

### File: Oproto.FluentDynamoDb/Requests/PutItemRequestBuilder.cs

**Category:** API Correction (XML Documentation)

**Before:**
```csharp
/// // Put an entity
/// var response = await table.Put<MyEntity>()
///     .WithItem(myEntity)
///     .ExecuteAsync();
/// 
/// // Put with raw attributes
/// var response = await table.Put<MyEntity>()
///     .WithItem(new Dictionary<string, AttributeValue> { ... })
///     .ExecuteAsync();
/// 
/// // Conditional put (only if item doesn't exist)
/// var response = await table.Put<MyEntity>()
///     .WithItem(myEntity)
///     .Where("attribute_not_exists(id)")
///     .ExecuteAsync();
```

**After:**
```csharp
/// // Put an entity
/// await table.Put<MyEntity>()
///     .WithItem(myEntity)
///     .PutAsync();
/// 
/// // Put with raw attributes
/// await table.Put<MyEntity>()
///     .WithItem(new Dictionary<string, AttributeValue> { ... })
///     .PutAsync();
/// 
/// // Conditional put with return values (use ToDynamoDbResponseAsync to access response.Attributes)
/// var response = await table.Put<MyEntity>()
///     .WithItem(myEntity)
///     .Where("attribute_not_exists(id)")
///     .ReturnAllOldValues()
///     .ToDynamoDbResponseAsync();
```

**Reason:** `ExecuteAsync()` does not exist on PutItemRequestBuilder. The correct methods are `PutAsync()` for void operations and `ToDynamoDbResponseAsync()` when accessing response attributes. Also corrected the `WithItem<T>` method example.


## [2025-12-01]

### File: docs/advanced-topics/CompositeEntities.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`
- Replaced `ExecuteAsync()` on Query builders with `ToListAsync()`
- Replaced `ExecuteAsync<T>()` on Query builders with `Query<T>().ToListAsync()`
- Replaced `ExecuteAsync<T>()` on Get builders with `Get<T>().GetItemAsync()`
- Replaced `ExecuteAsync()` on TransactWrite builders with `CommitAsync()`

**Reason:** `ExecuteAsync()` does not exist on these request builders. The correct methods are `PutAsync()`, `ToListAsync()`, `GetItemAsync()`, and `CommitAsync()` respectively.

---

### File: docs/advanced-topics/STSIntegration.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync<User>()` on Get builders with `Get<User>().GetItemAsync()`
- Replaced `ExecuteAsync<User>()` on Query builders with `Query<User>().ToListAsync()`
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`
- Replaced `ExecuteAsync()` on Update builders with `UpdateAsync()`
- Replaced `ExecuteAsync()` on Delete builders with `DeleteAsync()`
- Replaced `ExecuteAsync()` on TransactWrite builders with `CommitAsync()`

**Reason:** `ExecuteAsync()` does not exist on these request builders. The correct methods are `GetItemAsync()`, `ToListAsync()`, `PutAsync()`, `UpdateAsync()`, `DeleteAsync()`, and `CommitAsync()` respectively.

---

### File: docs/advanced-topics/MultiEntityTables.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`
- Replaced `ExecuteAsync()` on Query builders with `ToListAsync()`
- Replaced `ExecuteAsync()` on Get builders with `GetItemAsync()`
- Replaced `ExecuteAsync()` on TransactWrite builders with `CommitAsync()`
- Replaced `ExecuteAsync()` on Scan builders with `ToListAsync()`

**Reason:** `ExecuteAsync()` does not exist on these request builders. The correct methods are `PutAsync()`, `ToListAsync()`, `GetItemAsync()`, `CommitAsync()` respectively.

---

### File: docs/advanced-topics/Discriminators.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync<User>()` on Query builders with `Query<User>().ToListAsync()`
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`

**Reason:** `ExecuteAsync()` does not exist on these request builders. The correct methods are `ToListAsync()` and `PutAsync()` respectively.

---

### File: docs/advanced-topics/PerformanceOptimization.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync<T>()` on Get builders with `Get<T>().GetItemAsync()`
- Replaced `ExecuteAsync<T>()` on Query builders with `Query<T>().ToListAsync()`
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`
- Replaced `ExecuteAsync()` on Scan builders with `ToListAsync()`
- Replaced `ExecuteAsync()` on Query builders with `ToListAsync()`

**Reason:** `ExecuteAsync()` does not exist on these request builders. The correct methods are `GetItemAsync()`, `ToListAsync()`, and `PutAsync()` respectively. Note: `ExecuteAsync()` is correct for BatchGetItemRequestBuilder and BatchWriteItemRequestBuilder.



---

### File: docs/examples/AdvancedTypesExamples.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`
- Replaced `ExecuteAsync()` on Update builders with `UpdateAsync()`
- Replaced `ExecuteAsync<T>()` on Get builders with `Get<T>().GetItemAsync()`
- Replaced `ExecuteAsync<T>()` on Query builders with `Query<T>().ToListAsync()`

**Reason:** `ExecuteAsync()` does not exist on these request builders. The correct methods are `PutAsync()`, `UpdateAsync()`, `GetItemAsync()`, and `ToListAsync()` respectively.

---

### File: docs/examples/ProjectionModelsExamples.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Query builders with `ToListAsync()`

**Reason:** `ExecuteAsync()` does not exist on QueryRequestBuilder. The correct method is `ToListAsync()`.

---

### File: docs/examples/EntitySpecificBuildersExamples.md

**Category:** Pattern Update

**Summary of corrections:**
- Changed `UpdateAsync()` to `ToDynamoDbResponseAsync()` when accessing `response.Attributes`

**Reason:** `UpdateAsync()` returns `Task` (void), not a response object. To access `response.Attributes`, use `ToDynamoDbResponseAsync()` which returns the raw AWS SDK response.



---

### File: docs/getting-started/Installation.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`

**Reason:** `ExecuteAsync()` does not exist on PutItemRequestBuilder. The correct method is `PutAsync()`.

---

### File: docs/getting-started/QuickStart.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Get builders with `GetItemAsync()`
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`
- Replaced `ExecuteAsync()` on Update builders with `UpdateAsync()`
- Replaced `ExecuteAsync()` on Delete builders with `DeleteAsync()`
- Replaced `ExecuteAsync()` on Query builders with `ToListAsync()`

**Reason:** `ExecuteAsync()` does not exist on these request builders. The correct methods are `GetItemAsync()`, `PutAsync()`, `UpdateAsync()`, `DeleteAsync()`, and `ToListAsync()` respectively.

---

### File: docs/getting-started/FirstEntity.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Get builders with `GetItemAsync()`
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`
- Replaced `ExecuteAsync()` on Update builders with `UpdateAsync()`
- Replaced `ExecuteAsync()` on Query builders with `ToListAsync()`

**Reason:** `ExecuteAsync()` does not exist on these request builders. The correct methods are `GetItemAsync()`, `PutAsync()`, `UpdateAsync()`, and `ToListAsync()` respectively.

---

### File: docs/getting-started/SingleEntityTables.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Get builders with `GetItemAsync()`
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`
- Replaced `ExecuteAsync()` on Update builders with `UpdateAsync()`
- Replaced `ExecuteAsync()` on Delete builders with `DeleteAsync()`
- Replaced `ExecuteAsync()` on Query builders with `ToListAsync()`
- Replaced `ExecuteAsync()` on Scan builders with `ToListAsync()`
- Replaced `ExecuteAsync()` on DynamoDbTransactions.Write with `CommitAsync()`

**Reason:** `ExecuteAsync()` does not exist on these request builders. The correct methods are `GetItemAsync()`, `PutAsync()`, `UpdateAsync()`, `DeleteAsync()`, `ToListAsync()`, and `CommitAsync()` respectively. Note: `ExecuteAsync()` is correct for DynamoDbBatch.Write and DynamoDbBatch.Get.



---

### File: docs/reference/LoggingTroubleshooting.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Query builders with `ToListAsync()`

**Reason:** `ExecuteAsync()` does not exist on QueryRequestBuilder. The correct method is `ToListAsync()`. Note: `ExecuteAsync()` is correct for BatchGet operations.

---

### File: docs/reference/FormatSpecifiers.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Query builders with `ToListAsync()`

**Reason:** `ExecuteAsync()` does not exist on QueryRequestBuilder. The correct method is `ToListAsync()`. Note: Many ExecuteAsync references remain in this file for Update and Delete operations that need further review.

---

### File: docs/reference/ApiImprovementsMigration.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`

**Reason:** `ExecuteAsync()` does not exist on PutItemRequestBuilder. The correct method is `PutAsync()`. Note: Many ExecuteAsync references remain in this file for Query operations that need further review.

---

### File: docs/reference/AdoptionGuide.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`

**Reason:** `ExecuteAsync()` does not exist on PutItemRequestBuilder. The correct method is `PutAsync()`.

---

### File: docs/reference/AdvancedTypesQuickReference.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Update builders with `UpdateAsync()`
- Replaced `ExecuteAsync()` on Query builders with `ToListAsync()`

**Reason:** `ExecuteAsync()` does not exist on these request builders. The correct methods are `UpdateAsync()` and `ToListAsync()` respectively.

---

### File: docs/reference/ErrorHandling.md

**Category:** API Correction

**Summary of corrections:**
- Replaced `ExecuteAsync()` on Put builders with `PutAsync()`
- Replaced `ExecuteAsync()` on Update builders with `UpdateAsync()`
- Replaced `ExecuteAsync()` on Delete builders with `DeleteAsync()`

**Reason:** `ExecuteAsync()` does not exist on these request builders. The correct methods are `PutAsync()`, `UpdateAsync()`, and `DeleteAsync()` respectively.

---

### File: docs/SourceGeneratorGuide.md

**Category:** API Correction

**Before:**
```csharp
// Introduction text
The Oproto.FluentDynamoDb source generator automatically creates entity mapping code, field constants, key builders, and enhanced ExecuteAsync methods...

// Put operation
await transactionsTable.Put(transaction)
    .ExecuteAsync();

// Get operation
var response = await transactionsTable.Get()
    .WithKey(TransactionFields.TenantId, TransactionKeys.Pk("tenant123"))
    .WithKey(TransactionFields.TransactionId, TransactionKeys.Sk("txn456"))
    .ExecuteAsync();

// Multi-entity Put operations
await ecommerceTable.Orders.Put(order)
    .ExecuteAsync();
await ecommerceTable.OrderLines.Put(orderLine)
    .ExecuteAsync();

// Query operations
var orders = await ecommerceTable.Orders.Query()
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .ExecuteAsync();

// Manual pattern Get
var response = await table.Get()
    .WithKey(...)
    .ExecuteAsync<Transaction>();

// Query with strongly-typed results
var queryResponse = await table.Query()
    .Where($"{TransactionFields.TenantId} = :pk", new { pk = TransactionKeys.Pk("tenant123") })
    .ExecuteAsync<Transaction>();

// GSI Query
var statusResponse = await table.Query<Transaction>()
    .UsingIndex("StatusIndex")
    .Where($"{Transaction.Fields.Status} = {{0}}", "pending")
    .ExecuteAsync<Transaction>();

// Multi-item entity query
var response = await table.Query()
    .Where(...)
    .ExecuteAsync<TransactionWithEntries>();

// STS scoped client
var response = await _table.Get()
    .WithClient(scopedClient)
    .WithKey(...)
    .ExecuteAsync<Transaction>();

// FluentResults integration
var result = await table.Get()
    .WithKey(...)
    .ExecuteAsync<Transaction>();

// Migration guide
3. Replace manual mapping code with generated `ExecuteAsync<T>()` calls
```

**After:**
```csharp
// Introduction text
The Oproto.FluentDynamoDb source generator automatically creates entity mapping code, field constants, key builders, and type-safe async methods...

// Put operation
await transactionsTable.Put(transaction)
    .PutAsync();

// Get operation
var transaction = await transactionsTable.Get()
    .WithKey(TransactionFields.TenantId, TransactionKeys.Pk("tenant123"))
    .WithKey(TransactionFields.TransactionId, TransactionKeys.Sk("txn456"))
    .GetItemAsync();

// Multi-entity Put operations
await ecommerceTable.Orders.Put(order)
    .PutAsync();
await ecommerceTable.OrderLines.Put(orderLine)
    .PutAsync();

// Query operations
var orders = await ecommerceTable.Orders.Query()
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .ToListAsync();

// Manual pattern Get
var response = await table.Get<Transaction>()
    .WithKey(...)
    .GetItemAsync();

// Query with strongly-typed results
var transactions = await table.Query<Transaction>()
    .Where($"{TransactionFields.TenantId} = :pk", new { pk = TransactionKeys.Pk("tenant123") })
    .ToListAsync();

// GSI Query
var pendingTransactions = await table.Query<Transaction>()
    .UsingIndex("StatusIndex")
    .Where($"{Transaction.Fields.Status} = {{0}}", "pending")
    .ToListAsync();

// Multi-item entity query
var transactionsWithEntries = await table.Query<TransactionWithEntries>()
    .Where(...)
    .ToListAsync();

// STS scoped client
var transaction = await _table.Get<Transaction>()
    .WithClient(scopedClient)
    .WithKey(...)
    .GetItemAsync();

// FluentResults integration
var result = await table.Get<Transaction>()
    .WithKey(...)
    .GetItemAsync();

// Migration guide
3. Replace manual mapping code with generated type-safe async methods (`GetItemAsync()`, `PutAsync()`, `ToListAsync()`, etc.)
```

**Reason:** `ExecuteAsync()` does not exist on these request builders. The correct methods are:
- `PutAsync()` for PutItemRequestBuilder
- `GetItemAsync()` for GetItemRequestBuilder
- `ToListAsync()` for QueryRequestBuilder and ScanRequestBuilder
- Updated introduction text to reflect accurate terminology
- Updated migration guide to reference correct method names



---

## [2025-12-04]

### File: Oproto.FluentDynamoDb.SystemTextJson/README.md

**Category:** Pattern Update - JSON Serializer Refactor

**Summary:** Complete rewrite of the SystemTextJson package README to document the new runtime configuration pattern via `FluentDynamoDbOptions` instead of the removed compile-time assembly attribute approach.

---

**Before (Old assembly attribute pattern):**
```csharp
// Assembly-level configuration (compile-time)
[assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]

// Entity with JsonBlob
[DynamoDbTable("documents")]
public partial class Document
{
    [JsonBlob]
    [DynamoDbAttribute("content")]
    public DocumentContent Content { get; set; }
}

// Usage - no way to customize serializer options
var table = new DocumentTable(client, "documents");
```

**After (New runtime configuration pattern):**
```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.SystemTextJson;

// Entity with JsonBlob (no assembly attribute needed)
[DynamoDbTable("documents")]
public partial class Document
{
    [JsonBlob]
    [DynamoDbAttribute("content")]
    public DocumentContent Content { get; set; }
}

// Configure at runtime with options
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();  // Default options

// Or with custom JsonSerializerOptions
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson(new JsonSerializerOptions 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    });

// Or for AOT with JsonSerializerContext
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson(MyJsonContext.Default);

var table = new DocumentTable(client, "documents", options);
```

**Reason:** The JSON serialization system was refactored from compile-time assembly attributes to runtime configuration via `FluentDynamoDbOptions`. This enables users to customize serializer options (camelCase, null handling, etc.) and provides full AOT compatibility with `JsonSerializerContext`.

---

### File: Oproto.FluentDynamoDb.NewtonsoftJson/README.md

**Category:** Pattern Update - JSON Serializer Refactor

**Summary:** Complete rewrite of the NewtonsoftJson package README to document the new runtime configuration pattern via `FluentDynamoDbOptions` instead of the removed compile-time assembly attribute approach.

---

**Before (Old assembly attribute pattern):**
```csharp
// Assembly-level configuration (compile-time)
[assembly: DynamoDbJsonSerializer(JsonSerializerType.NewtonsoftJson)]

// Entity with JsonBlob
[DynamoDbTable("documents")]
public partial class Document
{
    [JsonBlob]
    [DynamoDbAttribute("content")]
    public DocumentContent Content { get; set; }
}

// Usage - no way to customize serializer settings
var table = new DocumentTable(client, "documents");
```

**After (New runtime configuration pattern):**
```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.NewtonsoftJson;

// Entity with JsonBlob (no assembly attribute needed)
[DynamoDbTable("documents")]
public partial class Document
{
    [JsonBlob]
    [DynamoDbAttribute("content")]
    public DocumentContent Content { get; set; }
}

// Configure at runtime with options
var options = new FluentDynamoDbOptions()
    .WithNewtonsoftJson();  // Default settings

// Or with custom JsonSerializerSettings
var options = new FluentDynamoDbOptions()
    .WithNewtonsoftJson(new JsonSerializerSettings 
    { 
        ContractResolver = new CamelCasePropertyNamesContractResolver() 
    });

var table = new DocumentTable(client, "documents", options);
```

**Reason:** The JSON serialization system was refactored from compile-time assembly attributes to runtime configuration via `FluentDynamoDbOptions`. This enables users to customize serializer settings and provides a consistent configuration pattern across all FluentDynamoDb options.

---

### File: docs/advanced-topics/AdvancedTypes.md

**Category:** Pattern Update - JSON Serializer Refactor

**Summary:** Updated the JSON Blob Serialization section to document the new runtime configuration pattern. Removed all references to the `[assembly: DynamoDbJsonSerializer]` attribute and replaced with `FluentDynamoDbOptions` configuration examples.

---

**Before (Old assembly attribute pattern):**
```csharp
// Assembly-level configuration
[assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]

// Entity with JsonBlob
[DynamoDbTable("documents")]
public partial class Document
{
    [JsonBlob]
    [DynamoDbAttribute("content")]
    public DocumentContent Content { get; set; }
}
```

**After (New runtime configuration pattern):**
```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.SystemTextJson;

// Entity with JsonBlob
[DynamoDbTable("documents")]
public partial class Document
{
    [JsonBlob]
    [DynamoDbAttribute("content")]
    public DocumentContent Content { get; set; }
}

// Configure FluentDynamoDbOptions with JSON serializer
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();

var table = new DocumentTable(dynamoDbClient, "documents", options);
```

**Reason:** JSON serialization is now configured at runtime via `FluentDynamoDbOptions` instead of compile-time assembly attributes. This provides flexibility to customize serializer options and supports AOT-compatible `JsonSerializerContext`.

---

**Before (Error handling section):**
```csharp
// DYNDB102: Missing JSON serializer package
[JsonBlob]
public ComplexObject Data { get; set; } // Warning: Add SystemTextJson or NewtonsoftJson package
```

**After (Error handling section):**
```csharp
// Compile-time: DYNDB102 warning when [JsonBlob] used without JSON package reference
// Runtime: InvalidOperationException when no JSON serializer configured

// This will throw InvalidOperationException at runtime:
var options = new FluentDynamoDbOptions(); // No JSON serializer configured!
var table = new DocumentTable(dynamoDbClient, "documents", options);

await table.Documents.Put(document).PutAsync();
// InvalidOperationException: Property 'Content' has [JsonBlob] attribute but no JSON serializer is configured. 
// Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.
```

**Reason:** Added documentation for the new runtime exception that occurs when `[JsonBlob]` properties are used without configuring a JSON serializer via `FluentDynamoDbOptions`.

---

### File: docs/reference/AttributeReference.md

**Category:** Pattern Update - JSON Serializer Refactor

**Summary:** Removed the `[DynamoDbJsonSerializer]` attribute section entirely as this attribute has been deleted. The JSON serialization is now configured via `FluentDynamoDbOptions` at runtime.

---

**Before:**
```markdown
## [DynamoDbJsonSerializer]

Assembly-level attribute to configure JSON serialization for `[JsonBlob]` properties.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `serializerType` | `JsonSerializerType` | Yes | The JSON serializer to use |

### Example

```csharp
[assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]
```
```

**After:**
Section removed entirely. JSON serialization is now documented in the `[JsonBlob]` attribute section with reference to `FluentDynamoDbOptions` configuration.

**Reason:** The `[assembly: DynamoDbJsonSerializer]` attribute and `JsonSerializerType` enum have been deleted as part of the JSON serializer refactor. JSON serialization is now configured at runtime via `FluentDynamoDbOptions.WithSystemTextJson()` or `FluentDynamoDbOptions.WithNewtonsoftJson()`.

---

### File: docs/reference/AdvancedTypesQuickReference.md

**Category:** Pattern Update - JSON Serializer Refactor

**Summary:** Updated the JSON Blobs section to show the new runtime configuration pattern via `FluentDynamoDbOptions`.

---

**Before:**
```csharp
// Assembly-level configuration
[assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]

[DynamoDbAttribute("content")]
[JsonBlob]
public ComplexObject Content { get; set; }
```

**After:**
```csharp
// 1. Define entity with [JsonBlob] property
[DynamoDbTable("documents")]
public partial class Document
{
    [DynamoDbAttribute("content")]
    [JsonBlob]
    public ComplexObject Content { get; set; } = new();
}

// 2. Configure FluentDynamoDbOptions with JSON serializer
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();  // or .WithNewtonsoftJson()

// 3. Create table with options
var table = new DocumentTable(dynamoDbClient, "documents", options);
```

**Reason:** JSON serialization is now configured at runtime via `FluentDynamoDbOptions` instead of compile-time assembly attributes.

---

### File: docs/examples/AdvancedTypesExamples.md

**Category:** Pattern Update - JSON Serializer Refactor

**Summary:** Updated all JSON Blob examples to show the new runtime configuration pattern via `FluentDynamoDbOptions`.

---

**Before:**
```csharp
// Install: dotnet add package Oproto.FluentDynamoDb.SystemTextJson
// Add assembly attribute
[assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]

[DynamoDbTable("orders")]
public partial class Order
{
    [DynamoDbAttribute("details")]
    [JsonBlob]
    public OrderDetails Details { get; set; }
}

// Usage
var order = new Order { ... };
await orderTable.Put.WithItem(order).PutAsync();
```

**After:**
```csharp
// Install: dotnet add package Oproto.FluentDynamoDb.SystemTextJson
using Oproto.FluentDynamoDb.SystemTextJson;

[DynamoDbTable("orders")]
public partial class Order
{
    [DynamoDbAttribute("details")]
    [JsonBlob]
    public OrderDetails Details { get; set; }
}

// Configure JSON serialization at runtime
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();  // Uses default options

// Or with custom options
var customOptions = new FluentDynamoDbOptions()
    .WithSystemTextJson(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    });

var orderTable = new OrderTable(dynamoDbClient, "orders", options);

// Usage
var order = new Order { ... };
await orderTable.Put.WithItem(order).PutAsync();
```

**Reason:** JSON serialization is now configured at runtime via `FluentDynamoDbOptions` instead of compile-time assembly attributes. This enables customization of serializer options.

---

### File: docs/QUICK_REFERENCE.md

**Category:** Pattern Update - JSON Serializer Refactor

**Summary:** Updated the JSON Blob section in the Advanced Types quick reference to show the new runtime configuration pattern.

---

**Before:**
```csharp
// Assembly-level configuration
[assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]

[DynamoDbAttribute("content")]
[JsonBlob]
public ComplexObject Content { get; set; }
```

**After:**
```csharp
// 1. Define entity with [JsonBlob] property
[DynamoDbTable("documents")]
public partial class Document
{
    [DynamoDbAttribute("content")]
    [JsonBlob]
    public ComplexObject Content { get; set; } = new();
}

// 2. Configure FluentDynamoDbOptions with JSON serializer
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();  // or .WithNewtonsoftJson()

// Custom serializer options
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson(jsonOptions);

// AOT-compatible with JsonSerializerContext
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson(MyJsonContext.Default);
```

**Reason:** JSON serialization is now configured at runtime via `FluentDynamoDbOptions` instead of compile-time assembly attributes.

---

### Summary of JSON Serializer Refactor Documentation Changes

**Breaking Changes Documented:**

1. **Removed `[assembly: DynamoDbJsonSerializer]` attribute** - No longer exists, replaced by runtime configuration
2. **Removed `JsonSerializerType` enum** - No longer needed
3. **Changed `IDynamoDbEntity` interface** - `ToDynamoDb`/`FromDynamoDb` methods now accept `FluentDynamoDbOptions?` instead of `IDynamoDbLogger?`

**New Features Documented:**

1. **`IJsonBlobSerializer` interface** - Core interface for JSON serialization
2. **`FluentDynamoDbOptions.WithJsonSerializer()`** - Builder method for configuring JSON serializer
3. **`WithSystemTextJson()` extension methods** - Configure System.Text.Json with default, custom, or AOT-compatible options
4. **`WithNewtonsoftJson()` extension methods** - Configure Newtonsoft.Json with default or custom settings
5. **Runtime exception** - Clear error message when `[JsonBlob]` used without configured serializer

**Files Updated:**
- `Oproto.FluentDynamoDb.SystemTextJson/README.md`
- `Oproto.FluentDynamoDb.NewtonsoftJson/README.md`
- `docs/advanced-topics/AdvancedTypes.md`
- `docs/reference/AttributeReference.md`
- `docs/reference/AdvancedTypesQuickReference.md`
- `docs/examples/AdvancedTypesExamples.md`
- `docs/QUICK_REFERENCE.md`
