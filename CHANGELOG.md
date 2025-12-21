# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Nested Map and List Lambda Expression Support** - Full lambda expression support for nested object properties, list indexing, and collection operations
  - **Nested Property Access in Filters/Conditions**: Query nested map properties using lambda expressions in `.WithFilter()` and `.Where()` on Put/Update/Delete operations
    - Single-level: `.WithFilter(x => x.Address.City == "Seattle")`
    - Multi-level: `.WithFilter(x => x.ShippingAddress.Country.Code == "US")`
    - Works with all comparison operators and logical operators
  - **List Index Access in Filters/Conditions**: Filter on specific list elements by index
    - Direct index: `.WithFilter(x => x.Tags[0] == "featured")`
    - Nested list: `.WithFilter(x => x.Metadata.Keywords[0] == "sale")`
    - Object in list: `.WithFilter(x => x.LineItems[0].ProductId == productId)`
  - **Nested Property Updates**: Partially update nested objects without replacing the entire map
    - Single property: `.Set(x => new CustomerUpdateModel { ShippingAddress = new AddressUpdateModel { City = "Portland" } })`
    - Multiple properties in single expression
    - Multi-level nesting support
    - Combined with top-level updates
  - **List Operations**: New extension methods for list manipulation in update expressions
    - `Append<T>(item)` - Add element to end of list
    - `Prepend<T>(item)` - Add element to beginning of list
    - `AppendRange<T>(items)` - Add multiple elements to end
    - `PrependRange<T>(items)` - Add multiple elements to beginning
    - `Set(x => x.List[index], value)` - Update element at specific index
    - `Remove(x => x.List[index])` - Remove element at specific index
    - Works with nested lists: `.Set(x => x.Metadata.Keywords.Append("sale"))`
  - **Set Operations**: New builder methods for DynamoDB set manipulation
    - `Add(x => x.SetProperty, value)` - Add element to set
    - `Add(x => x.SetProperty, values[])` - Add multiple elements
    - `Delete(x => x.SetProperty, value)` - Remove element from set
    - `Delete(x => x.SetProperty, values[])` - Remove multiple elements
    - Supports `HashSet<string>`, `HashSet<int>`, and other set types
  - **Source Generator Enhancement**: Automatically generates nested `*UpdateModel` types for entities with `[DynamoDbMap]` properties
  - _Requirements: 1.1-1.6, 2.1-2.4, 3.1-3.5, 4.1-4.6, 5.1-5.5, 6.1-6.4_
  
  **Usage:**
  ```csharp
  using Oproto.FluentDynamoDb.Expressions;
  
  // Filter on nested property
  var customers = await table.Customers.Query(x => x.CustomerId == tenantId)
      .WithFilter(x => x.ShippingAddress.City == "Seattle")
      .ToListAsync();
  
  // Update nested property
  await table.Customers.Update(customerId)
      .Set(x => new CustomerUpdateModel 
      { 
          ShippingAddress = new AddressUpdateModel { City = "Portland" } 
      })
      .UpdateAsync();
  
  // List operations
  await table.Items.Update(itemId)
      .Set(x => x.Tags.Append("new-tag"))
      .UpdateAsync();
  
  // Set operations
  await table.Items.Update(itemId)
      .Add(x => x.Categories, "electronics")
      .UpdateAsync();
  ```

### Fixed

- **Nullable Property NULL Handling** - Fixed an issue where nullable properties (e.g., `DateTime?`, `int?`, `decimal?`) would throw a conversion error when reading back records where `null` was stored
  - DynamoDB represents null values as `{ NULL: true }` which is a valid attribute
  - Previously, the generated `FromDynamoDb` code would find the attribute but fail when trying to parse the non-existent value
  - Error message was: "Failed to convert DynamoDB attribute 'PropertyName' to Type. Attribute type: Null"
  - Now properly checks for `NULL == true` before attempting to parse nullable properties
  - Affects all nullable value types: `DateTime?`, `int?`, `long?`, `decimal?`, `double?`, `float?`, `bool?`, `Guid?`, etc.

### Added

- **Multi-Entity Index Consolidation** - Indexes defined on any entity in a multi-entity table are now consolidated and available on the generated table class
  - Indexes from all entities sharing a table are collected and merged
  - Multiple entities defining the same index with identical configuration generate a single index property
  - Indexes defined on non-default entities are now properly generated
  - New diagnostics for conflicting index configurations:
    - `FDDB053`: Conflicting partition keys on same index name
    - `FDDB054`: Conflicting sort keys on same index name
    - `FDDB055`: Conflicting index types (GSI vs LSI) on same index name
  - Index documentation includes all referencing entities
  - Full backward compatibility with existing single-entity and multi-entity tables
  - _Requirements: 1.1-1.3, 2.1-2.4, 3.1-3.4, 4.1-4.3, 5.1-5.3, 6.1-6.3_
  
  **Usage:**
  ```csharp
  // Multi-entity table with indexes on different entities
  [DynamoDbTable("shared-table", IsDefault = true)]
  public partial class Order
  {
      [GlobalSecondaryIndex("status-index", IsPartitionKey = true)]
      [DynamoDbAttribute("status")]
      public string Status { get; set; }
  }
  
  [DynamoDbTable("shared-table")]
  public partial class Customer
  {
      [GlobalSecondaryIndex("email-index", IsPartitionKey = true)]
      [DynamoDbAttribute("email")]
      public string Email { get; set; }
  }
  
  // Both indexes are available on the generated table class
  var ordersByStatus = await table.StatusIndex.Query<Order>()
      .Where(x => x.Status == "pending")
      .ToListAsync();
  
  var customersByEmail = await table.EmailIndex.Query<Customer>()
      .Where(x => x.Email == "user@example.com")
      .ToListAsync();
  ```

- **Conditional Filter Expressions** - Support for natural `||` and `&&` patterns with local boolean conditions in filter expressions
  - `(localCondition || x.Property == value)` - skip filter when local condition is true
  - `(localCondition && x.Property == value)` - include filter only when local condition is true
  - Works with method calls like `string.IsNullOrWhiteSpace()`, negations (`!flag`), and compound expressions
  - Local conditions are evaluated at translation time, not sent to DynamoDB
  - OR between two entity conditions throws `UnsupportedExpressionException` (DynamoDB limitation)
  - _Requirements: 1.1-1.3, 2.1-2.3, 3.1-3.3, 4.1-4.3, 5.1-5.3, 6.1-6.3, 7.1-7.3_
  
  **Usage:**
  ```csharp
  // Optional filter based on parameter presence
  var orders = await table.Orders.Query(x => x.CustomerId == customerId)
      .WithFilter(x => string.IsNullOrWhiteSpace(status) || x.Status == status)
      .ToListAsync();
  
  // Feature flag controlled filter
  var items = await table.Items.Query(x => x.Key == key)
      .WithFilter(x => enableDateFilter && x.Date > minDate)
      .ToListAsync();
  
  // Multiple optional filters
  var results = await table.Orders.Query(x => x.CustomerId == customerId)
      .WithFilter(x => (skipStatusFilter || x.Status == status) && (skipDateFilter || x.Date > minDate))
      .ToListAsync();
  ```

- **Custom Index Property Naming** - New `Name` property on `[GlobalSecondaryIndex]` and `[LocalSecondaryIndex]` attributes for customizing generated index property names
  - Specify `Name = "StatusIndex"` to generate `table.StatusIndex` instead of derived name from DynamoDB index name
  - When not specified, property name is derived from DynamoDB index name using PascalCase conversion (e.g., `"status-index"` → `StatusIndex`)
  - Multi-entity tables: when only one entity specifies a `Name`, it applies to all entities sharing that index
  - _Requirements: 1.1, 1.2, 1.3, 1.5_
  
  **Usage:**
  ```csharp
  // Custom index property name
  [GlobalSecondaryIndex("status-index", Name = "StatusIndex", IsPartitionKey = true)]
  [DynamoDbAttribute("status")]
  public string Status { get; set; }
  
  // Generated: table.StatusIndex.Query<T>()
  var orders = await table.StatusIndex.Query<Order>()
      .Where(x => x.Status == "pending")
      .ToListAsync();
  ```

- **Type-Based Table Class References** - New constructor overload on `[DynamoDbTable]` accepting `Type` for compile-time safe table class references
  - Use `[DynamoDbTable(typeof(MyTableClass))]` instead of string-based table names
  - Provides refactoring support and compile-time validation
  - Referenced type must be declared as `partial` class
  - String-based `[DynamoDbTable("name")]` continues to work unchanged
  - _Requirements: 4.1, 4.2, 4.4, 4.5_
  
  **Usage:**
  ```csharp
  // Define your table class as partial
  public partial class OrdersTable { }
  
  // Reference it in entity definition
  [DynamoDbTable(typeof(OrdersTable))]
  public partial class Order
  {
      // Entity properties...
  }
  ```

- **Enhanced Typed Index Classes** - Generated index classes now inherit from `DynamoDbIndex` and provide consistent Query builder methods
  - Index classes are generated as `partial` for extensibility
  - Generic `Query<T>()` methods with lambda, format string, and key+filter overloads
  - Non-generic `Query()` methods when projection type is defined
  - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.6, 3.1, 3.2_
  
  **Usage:**
  ```csharp
  // Generic query methods
  var orders = await table.StatusIndex.Query<Order>()
      .Where(x => x.Status == "pending")
      .ToListAsync();
  
  // With key and filter expressions
  var filtered = await table.StatusIndex.Query<Order>(
      x => x.Status == "active",
      x => x.Amount > 100)
      .ToListAsync();
  
  // Non-generic when projection type defined
  var projected = await table.StatusIndex.Query()
      .Where(x => x.Status == "pending")
      .ToListAsync();
  ```

- **New Diagnostic Codes** - Compile-time validation for index and table configurations
  - `FDDB050` (Error): Conflicting index `Name` values across entities sharing a table
  - `FDDB051` (Error): Type-based table reference must be a partial class
  - `FDDB052` (Warning): Index `Name` specified on multiple entities (informational)
  - _Requirements: 1.4, 4.3, 5.2_

- **Projection Interface Enhancement** - Projections now implement `IReadOnlyEntity<TSelf>` for seamless `QueryRequestBuilder` compatibility
  - New `IReadOnlyEntity<TSelf>` interface as base for both projections and full entities
  - `IDynamoDbEntity<TSelf>` now inherits from `IReadOnlyEntity<TSelf>` (backward compatible)
  - Generated projections implement both `IProjectionModel<TSelf>` and `IReadOnlyEntity<TSelf>`
  - Projections inherit metadata from source entity (table name, partition key, sort key)
  - `QueryRequestBuilder<T>` constraint updated to `where T : class, IReadOnlyEntity<T>`
  - Existing entity types continue to work without modification due to interface inheritance
  - Projections work with standard `ToListAsync()` - projection expression is automatically applied
  - Non-generic `Query()` methods on `DynamoDbIndex<TDefault>` for projection-typed indexes
  - New diagnostic codes for projection configuration errors:
    - `FDDB060` (Error): Projection source entity not found
    - `FDDB061` (Error): Metadata inheritance failure
    - `FDDB062` (Error): Projection interface violation (projection used in write context)
  - _Requirements: 1.1-1.5, 2.1-2.5, 3.1-3.5, 4.1-4.5, 5.1-5.5, 6.1-6.5, 7.1-7.5, 8.1-8.5_
  
  **Interface Hierarchy:**
  ```
  IEntityMetadataProvider
          │
          ▼
    IReadOnlyEntity<TSelf>  ◄── Projections implement this
          │
          ▼
    IDynamoDbEntity<TSelf>  ◄── Full entities implement this
  ```
  
  **Usage:**
  ```csharp
  // Define a projection for an entity
  [DynamoDbProjection(typeof(Order))]
  public partial class OrderSummary
  {
      [DynamoDbAttribute("orderId")]
      public string OrderId { get; set; } = string.Empty;

      [DynamoDbAttribute("status")]
      public string Status { get; set; } = string.Empty;

      [DynamoDbAttribute("totalAmount")]
      public decimal TotalAmount { get; set; }
  }
  
  // Define an index with default projection type
  public DynamoDbIndex<OrderSummary> StatusIndex => 
      new DynamoDbIndex<OrderSummary>(this, "status-index", OrderSummary.ProjectionExpression);
  
  // Query the index - non-generic Query() uses OrderSummary automatically
  var results = await table.StatusIndex.Query().Where(x => x.Status == "pending").ToListAsync();
  var results = await table.StatusIndex.Query("status = {0}", "pending").ToListAsync();
  
  // Projections are read-only - write operations fail at compile time
  // ❌ await table.Put(orderSummary).PutAsync();  // Won't compile
  // ✅ await table.Put(order).PutAsync();         // Use source entity
  ```

### Changed

- **PaginationExtensions.GetEncodedPaginationToken()** - Updated to support `QueryOperationResponse` and `ScanOperationResponse` types
  - New overload for `QueryOperationResponse` - use via `builder.Response?.GetEncodedPaginationToken()`
  - New overload for `ScanOperationResponse` - use via `builder.Response?.GetEncodedPaginationToken()`
  - New overload for raw AWS SDK `ScanResponse` for direct SDK usage
  - Existing `QueryResponse` overload retained for backward compatibility
  - All overloads return empty string when `LastEvaluatedKey` is null or empty
  
  **Usage:**
  ```csharp
  // Query pagination (recommended)
  var query = table.Users.Query().Where(x => x.TenantId == tenantId).Take(25);
  var users = await query.ToListAsync();
  var nextToken = query.Response?.GetEncodedPaginationToken() ?? string.Empty;
  
  // Scan pagination
  var scan = table.Logs.Scan().Take(100);
  var logs = await scan.ToListAsync();
  var nextToken = scan.Response?.GetEncodedPaginationToken() ?? string.Empty;
  ```

### Added

- **Comprehensive FluentResults API Integration** - Complete Result<T> pattern alternative to traditional async/exception-based API
  - New `GetItemAsyncResult()`, `PutAsyncResult()`, `UpdateAsyncResult()`, `DeleteAsyncResult()` extension methods for all CRUD operations
  - New `ToListAsyncResult()`, `ToCompositeEntityAsyncResult()`, `ToCompositeEntityListAsyncResult()` for query and scan operations
  - New `ExecuteAsyncResult()` for batch operations (`BatchGetBuilder`, `BatchWriteBuilder`, `BatchPartiQLBuilder`)
  - New `ExecuteAsyncResult()` for transaction operations (`TransactionWriteBuilder`, `TransactionGetBuilder`)
  - New `ExecuteAndMapAsyncResult<T1,...,T8>()` tuple methods for batch get operations
  - New `SpatialQueryAsyncResult()` for geospatial proximity and bounding box queries
  - Comprehensive error type hierarchy with `DynamoDbError` base class and typed errors:
    - Transaction errors: `TransactionCancelledError`, `TransactionConflictError`, `TransactionInProgressError`, `OptimisticLockingError`
    - Configuration errors: `MissingClientError`, `ClientMismatchError`, `EmptyOperationError`, `OperationLimitExceededError`
    - Mapping errors: `MappingError`, `DiscriminatorMismatchError`, `ProjectionValidationError`, `ExpressionTranslationError`
    - Validation errors: `SchemaValidationError`, `EmptyCollectionError`, `FormatStringError`
    - Storage errors: `BlobStorageError`, `EncryptionError`, `DecryptionError`
    - Geospatial errors: `SpatialQueryError`, `InvalidCoordinatesError`, `InvalidBoundingBoxError`
  - `DynamoDbErrors.FromException()` factory method maps all AWS SDK and library exceptions to typed errors
  - `UnprocessedItemsWarning` and `BatchStatementErrorWarning` for partial success scenarios in batch operations
  - All errors include `ErrorCode` property for programmatic error handling
  - Cancellation tokens properly re-throw `OperationCanceledException` without wrapping
  - _Requirements: 1.1-1.5, 2.1-2.7, 3.1-3.3, 4.1-4.5, 5.1-5.5, 6.1-6.5, 7.1-7.4, 10.1-10.3, 11.1-11.3, 12.1-12.2, 14.1-14.17, 16.1-16.3_
  
  **Usage:**
  ```csharp
  using Oproto.FluentDynamoDb.FluentResults;
  
  // Traditional (throws exceptions)
  var user = await table.Users.Get(userId).GetItemAsync();
  
  // FluentResults (returns Result<T>)
  var result = await table.Users.Get(userId).GetItemAsyncResult();
  if (result.IsSuccess)
      var user = result.Value;
  else
      foreach (var error in result.Errors.OfType<DynamoDbError>())
          Console.WriteLine($"[{error.ErrorCode}]: {error.Message}");
  ```

- **`[UseFluentResults]` Attribute** - Source generator attribute for opt-in FluentResults API generation
  - Apply to entity classes to generate Result-returning convenience methods on entity accessors
  - `HideGeneratedAsyncMethods` property (default: `true`) controls whether traditional async methods are suppressed
  - Generated methods: `GetAsyncResult()`, `PutAsyncResult()`, `DeleteAsyncResult()`, `QueryAsyncResult()`
  - _Requirements: 8.1-8.5, 9.1-9.5_
  
  **Usage:**
  ```csharp
  [DynamoDbTable("Users")]
  [UseFluentResults]
  public partial class User { ... }
  
  // Generated convenience methods:
  var result = await table.Users.GetAsyncResult(userId);
  var result = await table.Users.PutAsyncResult(user);
  var result = await table.Users.QueryAsyncResult(x => x.TenantId == tenantId);
  ```

- **Response metadata via `.Response` property** - Request builders now expose a `.Response` property after execution containing operation metadata. This keeps IntelliSense clean during request building while providing access to response details.
  - `QueryOperationResponse` - LastEvaluatedKey, ScannedCount, ResultCount, ConsumedCapacity, HasMorePages
  - `ScanOperationResponse` - LastEvaluatedKey, ScannedCount, ResultCount, ConsumedCapacity, HasMorePages
  - `GetItemOperationResponse` - ConsumedCapacity, ResponseMetadata
  - `PutItemOperationResponse` - ConsumedCapacity, ResponseMetadata, ItemCollectionMetrics
  - `UpdateItemOperationResponse` - ConsumedCapacity, ResponseMetadata, ItemCollectionMetrics
  - `DeleteItemOperationResponse` - ConsumedCapacity, ResponseMetadata, ItemCollectionMetrics
  
  **Usage:**
  ```csharp
  var builder = table.Query<User>().Where(x => x.Pk == tenantId);
  var users = await builder.ToListAsync();
  
  // Access response metadata
  var lastKey = builder.Response?.LastEvaluatedKey;
  var scannedCount = builder.Response?.ScannedCount;
  var hasMore = builder.Response?.HasMorePages ?? false;
  ```

- **DynamicEntity and DynamicTable** - Schema-less access to any DynamoDB table without defining entity classes
  - New `DynamicEntity` class where all attributes are stored in `DynamicFields` collection
  - New `DynamicTable` class for working with `DynamicEntity` instances
  - `DynamicTableKeyOptions` for configuring partition key and sort key names/types
  - Typed key methods (`GetAsync(string pk)`, `GetAsync(string pk, string sk)`, etc.) when key options configured
  - Raw `AttributeValue` key methods always available for maximum flexibility
  - Full Query and Scan support with lambda expressions using `DynamicFields` indexer
  - Expression translator skips key validation for `DynamicEntity` to allow flexible key conditions
  - Ideal for schema exploration, migration tools, and truly schema-less scenarios
  - _Requirements: 5.1-5.8, 7.1-7.6, 8.1-8.6_
  
  **Usage:**
  ```csharp
  // Create DynamicTable with key configuration
  var keyOptions = new DynamicTableKeyOptions
  {
      PartitionKeyName = "pk",
      PartitionKeyType = ScalarAttributeType.S,
      SortKeyName = "sk",
      SortKeyType = ScalarAttributeType.S
  };
  var table = new DynamicTable(client, "my-table", keyOptions);
  
  // Get item with typed keys
  var item = await table.GetAsync("USER#123", "PROFILE");
  var name = item?.DynamicFields.GetString("name");
  
  // Query with lambda expressions
  var items = await table.Query()
      .Where(x => x.DynamicFields["pk"] == "USER#123")
      .ToListAsync();
  
  // Put item
  var entity = new DynamicEntity();
  entity.DynamicFields.SetString("pk", "USER#456");
  entity.DynamicFields.SetString("sk", "PROFILE");
  entity.DynamicFields.SetString("name", "Jane Doe");
  await table.PutAsync(entity);
  ```

- **PartiQL Support** - SQL-like query capability with entity hydration
  - New `PartiQLRequestBuilder<TEntity>` following the same pattern as other request builders
  - `ExecutePartiQL<TEntity>(statement, params)` method on table classes
  - Format string placeholders with format specifiers (`{0}`, `{0:o}` for ISO 8601 dates)
  - `ToListAsync()` for SELECT queries with entity hydration
  - `ToCompoundEntityAsync()` for compound entity table results
  - `ExecuteAsync()` for INSERT/UPDATE/DELETE statements
  - `ToRequest()` to access underlying `ExecuteStatementRequest`
  - Response metadata (`ResponseMetadata`, `ConsumedCapacity`) accessible after execution
  - Batch PartiQL via `DynamoDbBatch.PartiQL` with `BatchPartiQLBuilder` and `BatchPartiQLResponse`
  - `ExecuteAndMapAsync<T1>()` through `ExecuteAndMapAsync<T1...T8>()` tuple convenience methods
  - _Requirements: 3.1-3.6_
  
  **Usage:**
  ```csharp
  // SELECT query with hydration
  var users = await table.ExecutePartiQL<User>(
      "SELECT * FROM Users WHERE pk = {0}",
      "USER#123")
      .ToListAsync();
  
  // SELECT with DateTime formatting
  var recentOrders = await table.ExecutePartiQL<Order>(
      "SELECT * FROM Orders WHERE pk = {0} AND created > {1:o}",
      "ORDER#456", DateTime.UtcNow.AddDays(-7))
      .ToListAsync();
  
  // INSERT/UPDATE/DELETE
  await table.ExecutePartiQL<User>(
      "UPDATE Users SET name = {0} WHERE pk = {1}",
      "Jane Doe", "USER#123")
      .ExecuteAsync();
  
  // Batch PartiQL
  var (user, order) = await DynamoDbBatch.PartiQL
      .Add(table.ExecutePartiQL<User>("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
      .Add(table.ExecutePartiQL<Order>("SELECT * FROM Orders WHERE pk = {0}", "ORDER#456"))
      .ExecuteAndMapAsync<User, Order>();
  ```

- **Direct SDK Request Passing** - Accept native AWS SDK request objects with response hydration
  - `WithRequest()` method on all request builders to inject pre-built SDK requests
  - Table-level convenience methods: `Get(GetItemRequest)`, `Query(QueryRequest)`, `Scan(ScanRequest)`, etc.
  - Async convenience methods: `GetAsync(GetItemRequest)`, `QueryAsync(QueryRequest)`, etc.
  - Response metadata accessible on builder after execution
  - `DynamoDbTransactions.WriteAsync(client, TransactWriteItemsRequest)` for direct transaction execution
  - `DynamoDbTransactions.GetAsync(client, TransactGetItemsRequest)` for direct transaction gets
  - `DynamoDbBatch.WriteAsync(client, BatchWriteItemRequest)` for direct batch writes
  - `DynamoDbBatch.GetAsync(client, BatchGetItemRequest)` for direct batch gets
  - _Requirements: 4.1-4.9_
  
  **Usage:**
  ```csharp
  // Inject pre-built SDK request
  var sdkRequest = new GetItemRequest
  {
      TableName = "users",
      Key = new Dictionary<string, AttributeValue>
      {
          ["pk"] = new AttributeValue { S = "USER#123" }
      }
  };
  var user = await table.Users.GetAsync(sdkRequest);
  
  // Builder pattern for response metadata access
  var builder = table.Users.Get(sdkRequest);
  var user = await builder.GetItemAsync();
  var capacity = builder.Response?.ConsumedCapacity;
  
  // Direct transaction execution
  await DynamoDbTransactions.WriteAsync(client, existingTransactWriteRequest);
  ```

- **Table Creation from Metadata** - Create DynamoDB tables programmatically from entity metadata for integration testing
  - New `TableCreator` class with `CreateAsync` and `BuildCreateTableRequest` methods
  - New `TableCreationOptions` class for configuring billing mode, throughput, TTL, and wait behavior
  - New `TableCreationResult` class with table information (TableName, TableArn, TableStatus, TtlEnabled)
  - New `ProvisionedThroughputConfig` class for PROVISIONED billing mode configuration
  - Source-generated static `CreateTableAsync` method on table classes
  - Supports primary key (partition + optional sort key) configuration
  - Supports Global Secondary Index (GSI) creation with projection types (ALL, KEYS_ONLY, INCLUDE)
  - Supports Local Secondary Index (LSI) creation with projection types
  - Supports TTL enablement when entity metadata defines TTL attribute
  - Supports PAY_PER_REQUEST (default) and PROVISIONED billing modes
  - Configurable wait-for-active behavior with timeout and polling interval
  - Complements `ValidateSchemaAsync` for complete table lifecycle management
  - _Requirements: 1.1-1.5, 2.1-2.6, 3.1-3.5, 4.1-4.3, 5.1-5.4, 6.1-6.2, 7.1-7.3_
  
  **Usage:**
  ```csharp
  using Oproto.FluentDynamoDb.Provisioning;
  
  // Using generated static method (recommended for integration tests)
  var result = await UsersTable.CreateTableAsync(client, "test-users-table");
  
  // With options
  var result = await UsersTable.CreateTableAsync(client, "test-users-table",
      new TableCreationOptions
      {
          EnableTtl = true,
          WaitForActive = true,
          WaitTimeout = TimeSpan.FromSeconds(60)
      });
  
  // Using TableCreator directly
  var creator = new TableCreator();
  var result = await creator.CreateAsync(client, "my-table", User.GetEntityMetadata());
  
  // Inspect request before execution
  var request = creator.BuildCreateTableRequest("my-table", User.GetEntityMetadata());
  ```

- **Schema Validation** - Runtime validation of DynamoDB table schemas against entity metadata
  - New `ValidateSchemaAsync(IAmazonDynamoDB, SchemaValidationOptions?)` method generated on table classes
  - Validates primary key configuration (partition key name/type, sort key name/type/presence)
  - Validates Global Secondary Index configuration (existence, key names, key types)
  - Validates Local Secondary Index configuration (existence, sort key name/type)
  - Validates TTL configuration (enabled status, attribute name)
  - Validates index projection compatibility with projection models
  - `SchemaValidationResult` with `IsValid`, `Errors`, and `Warnings` properties
  - `ThrowOnError()` method to throw `SchemaValidationException` when validation fails
  - `LogResults(IDynamoDbLogger)` method for logging validation results
  - `SchemaValidationOptions` with `Strictness` setting (Relaxed/Strict)
  - Comprehensive error codes (`SchemaValidationErrorCode`) for programmatic handling
  - Warning codes (`SchemaValidationWarningCode`) for non-critical differences
  - Designed for startup-time validation (e.g., Lambda cold start) for fail-fast behavior
  - _Requirements: 1.1-1.5, 2.1-2.6, 3.1-3.6, 4.2-4.6, 5.1-5.3, 6.1-6.4, 8.1-8.5, 9.1-9.4_
  
  **Usage:**
  ```csharp
  // Basic validation
  var result = await UsersTable.ValidateSchemaAsync(dynamoDbClient);
  if (!result.IsValid)
  {
      foreach (var error in result.Errors)
          Console.WriteLine($"Error: {error.Message}");
  }
  
  // Fail-fast validation
  var result = await UsersTable.ValidateSchemaAsync(dynamoDbClient);
  result.ThrowOnError(); // Throws SchemaValidationException if errors exist
  
  // Strict validation (missing projection models are errors)
  var options = new SchemaValidationOptions { Strictness = ValidationStrictness.Strict };
  var result = await UsersTable.ValidateSchemaAsync(dynamoDbClient, options);
  
  // Log validation results
  result.LogResults(logger);
  ```

- **Dynamic Fields Support** - Capture and work with DynamoDB attributes not explicitly defined in entity classes
  - New `[EnableDynamicFields]` attribute to opt-in to dynamic field capture on entities
  - New `DynamicFieldCollection` class with typed accessors for common types (string, int, long, double, decimal, bool, DateTime, DateTimeOffset, byte[])
  - New `DynamicFieldType` enum for runtime type detection of dynamic fields
  - New `DynamicFieldTypeException` for type mismatch errors with descriptive messages
  - Source generator automatically adds `DynamicFields` property to entities with `[EnableDynamicFields]`
  - `FromDynamoDb` captures unmapped attributes into `DynamicFields` collection
  - `ToDynamoDb` includes dynamic fields in serialized output (mapped properties take precedence)
  - Expression translator support for dynamic field filtering: `x.DynamicFields["fieldName"] == value`
  - Support for typed comparisons in expressions: `x.DynamicFields["score"] > 100`
  - `Exists()` and `NotExists()` methods for attribute existence checks in expressions
  - Update expression support via `DynamicFields` property on update models with `DynamicFieldCollection`
  - Dynamic field values redacted in logs by default (configurable via `SensitiveLogging` property)
  - Full AOT compatibility with no reflection at runtime
  - **Change Tracking** - Automatic tracking of dynamic field modifications for efficient updates
    - `DynamicFieldCollection` automatically tracks changes after entity deserialization
    - `ChangesOnly()` method returns a new collection with only added/modified fields and tracked removals
    - `ChangesOnly(resetTracking: false)` preserves tracking for retry scenarios
    - `ResetChangeTracking()` method to manually clear all tracked changes
    - `HasChanges` property to check if any modifications have been made
    - `RemovedFields` property provides access to fields marked for removal
    - Generated update models include nullable `DynamicFields` property for lambda expression updates
    - Null `DynamicFields` in update model leaves existing dynamic fields unchanged
    - _Requirements: 11.1-11.7, 12.1-12.4_
  - _Requirements: 1.1-1.4, 2.1-2.5, 3.1-3.4, 4.1-4.4, 5.1-5.4, 6.1-6.4, 7.1-7.3, 8.1-8.4, 9.1-9.3, 10.1-10.3_
  
  **Usage:**
  ```csharp
  // Enable dynamic fields on entity
  [DynamoDbTable("products")]
  [EnableDynamicFields]
  public partial class Product
  {
      [PartitionKey]
      [DynamoDbAttribute("pk")]
      public string ProductId { get; set; } = string.Empty;
  }
  
  // Read dynamic fields with typed accessors
  var product = await table.Products.GetAsync(productId);
  var color = product.DynamicFields.GetString("color");
  var weight = product.DynamicFields.GetInt("weight_grams");
  
  // Write dynamic fields
  product.DynamicFields.SetString("material", "Cotton");
  product.DynamicFields.SetInt("size_us", 10);
  await table.Products.PutAsync(product);
  
  // Update dynamic fields using change tracking
  product.DynamicFields.SetDecimal("sale_price", 24.99m);
  product.DynamicFields.Remove("temporary_note");
  await table.Products.Update(productId)
      .Set(x => new ProductUpdateModel { DynamicFields = product.DynamicFields.ChangesOnly() })
      .UpdateAsync();
  
  // Filter by dynamic fields
  var blueProducts = await table.Products.Scan()
      .WithFilter(x => x.DynamicFields["color"] == "Blue")
      .ToListAsync();
  
  // Check field existence
  var productsWithWarranty = await table.Products.Scan()
      .WithFilter(x => x.DynamicFields.Exists("warranty_months"))
      .ToListAsync();
  ```

- **`[LocalSecondaryIndex]` Attribute** - Support for Local Secondary Index definitions in entity metadata
  - New `[LocalSecondaryIndex(indexName)]` attribute for marking LSI sort key properties
  - LSIs share the same partition key as the base table
  - `IndexType` enum to distinguish between GSI and LSI in `IndexMetadata`
  - Source generator emits correct `IndexType` for both GSI and LSI attributes
  - Schema validation validates LSI configuration against actual table
  - _Requirements: 4.1, 7.1, 7.2_
  
  **Usage:**
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
      
      // Local Secondary Index - shares partition key with base table
      [LocalSecondaryIndex("orders-by-date")]
      [DynamoDbAttribute("order_date")]
      public string OrderDate { get; set; } = string.Empty;
  }
  ```

- **Enhanced `IndexMetadata`** - Additional metadata for schema validation and tooling
  - `IndexType` property to distinguish GSI from LSI
  - `PartitionKeyAttributeName` and `PartitionKeyAttributeType` properties
  - `SortKeyAttributeName` and `SortKeyAttributeType` properties
  - `ProjectionType` property (All, KeysOnly, Include)
  - `HasProjectionModel` property indicating if a projection model is defined
  - _Requirements: 7.1, 7.2, 10.1, 10.2, 10.3_

- **Enhanced `EntityMetadata`** - Additional metadata for schema validation
  - `PartitionKeyAttributeName` and `PartitionKeyAttributeType` properties
  - `SortKeyAttributeName` and `SortKeyAttributeType` properties
  - `TtlAttributeName` property for TTL configuration
  - _Requirements: 2.1, 2.2, 5.1, 5.2_

- **`[RequireWriteTransaction]` Attribute** - New attribute to enforce transactional writes for specific entity types
  - Marks entity classes as requiring write operations within a transaction
  - Put, Update, Delete, and BatchWrite operations throw `InvalidOperationException` when attempted outside a transaction
  - TransactWrite operations are allowed and proceed normally
  - `RequiresWriteTransaction` static property added to `IDynamoDbEntity` interface
  - `RequiresWriteTransaction` property added to `EntityMetadata` for tooling support
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_
  
  **Usage:**
  ```csharp
  [DynamoDbTable("FinancialTransactions")]
  [RequireWriteTransaction]
  public partial class Transaction
  {
      // Properties...
  }
  
  // This throws InvalidOperationException:
  await table.Transactions.Put(transaction).PutAsync();
  
  // This is allowed:
  await DynamoDbTransactions.Write()
      .Put(table.Transactions, transaction)
      .ExecuteAsync();
  ```

- **Default Request Options in FluentDynamoDbOptions** - Configure common request settings once and apply to all operations
  - `UseConsistentRead(bool)` - Default consistent read setting for Get and Query operations
  - `ReturnConsumedCapacity(ReturnConsumedCapacity)` - Default consumed capacity reporting for all operations
  - `ReturnItemCollectionMetrics(ReturnItemCollectionMetrics)` - Default item collection metrics for write operations
  - `ReturnValues(ReturnValue)` - Default return values for Put, Update, and Delete operations
  - Explicit builder method calls override default options
  - Method names match existing request builder methods for consistency
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_
  
  **Usage:**
  ```csharp
  var options = new FluentDynamoDbOptions()
      .UseConsistentRead(true)
      .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
      .ReturnValues(ReturnValue.ALL_NEW);
  
  var table = new UsersTable(client, "users", options);
  
  // All operations now use these defaults
  var user = await table.Users.Get(userId).GetItemAsync();  // Uses consistent read
  ```

- **Custom Namespace Support for Generated Table Classes** - Control the namespace of generated table classes
  - New `Namespace` property on `[DynamoDbTable]` attribute
  - When specified, generated table class is placed in the custom namespace
  - When not specified, uses the entity's namespace (existing behavior)
  - Appropriate using directives generated for referenced types
  - _Requirements: 2.1, 2.2, 2.3_
  
  **Usage:**
  ```csharp
  [DynamoDbTable("users", Namespace = "MyApp.Data.Tables")]
  public partial class User
  {
      // Entity stays in its declared namespace
  }
  // Generated UsersTable class is in MyApp.Data.Tables namespace
  ```

- **New Example Applications** - Three new example applications demonstrating advanced features
  - **JsonBlobDemo** - Demonstrates JSON blob serialization with System.Text.Json (AOT and reflection modes) and Newtonsoft.Json
  - **S3BlobDemo** - Demonstrates S3 blob storage integration with `[BlobReference]` attribute for storing large data externally
  - **EncryptionDemo** - Demonstrates field encryption with `[Encrypted]` attribute and sensitive data logging with `[Sensitive]` attribute
  - All examples follow the established pattern with interactive console menus and comprehensive README documentation
  - _Requirements: 2.1-2.8, 3.1-3.8, 4.1-4.11_

- **TransactionDemo RequireWriteTransaction Demonstration** - Updated TransactionDemo example to showcase the `[RequireWriteTransaction]` attribute
  - New `FinancialTransaction` entity demonstrating transactional write enforcement
  - Interactive demonstration showing direct write failure and transactional write success
  - _Requirements: 5.1-5.6_

- **Blob Storage Redesign** - Complete redesign of the blob storage feature with improved semantics, lazy loading, and failure handling strategies
  - New `[BlobStorage]` attribute replaces `[BlobReference]` with clearer semantics
  - New `BlobData<T>` wrapper type for lazy/eager loading control
  - New `IBlobStorageStrategy` interface for coordinating failure handling between blob storage and DynamoDB operations
  - Built-in `BestEffortCleanupStrategy` (default) - attempts to clean up orphaned blobs on DynamoDB write failure
  - Built-in `NoCleanupStrategy` - simple implementation for non-critical data where orphaned blobs are acceptable
  - `LazyLoad` property on `[BlobStorage]` attribute to defer blob loading until explicitly requested
  - `BlobData<T>.LoadAsync()` method for explicit lazy loading
  - `BlobData<T>.Value`, `ReferenceKey`, `IsLoaded`, `HasPendingData` properties for state inspection
  - `BlobData<T>.Create(T value)` factory method for creating instances with data to be stored
  - `BlobStoreOptions` class for optional metadata (ContentType, Metadata, Tags)
  - `BlobStorageException` for blob storage operation failures
  - Integration with `[JsonBlob]` attribute for JSON serialization before blob upload
  - Integration with `[Encrypted]` attribute for encryption before blob upload
  - Integration with `[Sensitive]` attribute for log redaction
  - Automatic strategy lifecycle invocation in Put, Update, Delete, Batch, and Transaction operations
  - `FluentDynamoDbOptions.WithBlobStorageStrategy(IBlobStorageStrategy)` for custom strategy configuration
  - _Requirements: 1.1-1.3, 2.1-2.7, 3.1-3.4, 4.1-4.6, 5.1-5.4, 6.1-6.3, 7.1-7.4, 8.1-8.4, 9.1-9.3, 10.1-10.3, 11.1-11.5, 12.1-12.6, 13.1-13.4_
  
  **Usage:**
  ```csharp
  // Entity definition with BlobStorage
  [DynamoDbTable("documents")]
  public partial class Document
  {
      [PartitionKey]
      [DynamoDbAttribute("pk")]
      public string Id { get; set; } = string.Empty;
      
      // Eager loading (default) - data loaded during deserialization
      [BlobStorage]
      [DynamoDbAttribute("content")]
      public BlobData<byte[]> Content { get; set; } = default!;
      
      // Lazy loading - data loaded on explicit LoadAsync() call
      [BlobStorage(LazyLoad = true)]
      [DynamoDbAttribute("thumbnail")]
      public BlobData<byte[]> Thumbnail { get; set; } = default!;
  }
  
  // Configuration
  var options = new FluentDynamoDbOptions()
      .WithBlobStorage(new S3BlobProvider(s3Client, "my-bucket"))
      .WithBlobStorageStrategy(new BestEffortCleanupStrategy(provider)); // Optional, this is the default
  
  var table = new DocumentTable(dynamoDbClient, "documents", options);
  
  // Creating and storing blob data
  var document = new Document
  {
      Id = "doc-123",
      Content = BlobData<byte[]>.Create(fileBytes),
      Thumbnail = BlobData<byte[]>.Create(thumbnailBytes)
  };
  await table.Documents.PutAsync(document);
  
  // Retrieving with eager loading
  var loaded = await table.Documents.GetAsync("doc-123");
  var content = loaded.Content.Value; // Already loaded
  
  // Retrieving with lazy loading
  var lazyLoaded = await table.Documents.GetAsync("doc-123");
  await lazyLoaded.Thumbnail.LoadAsync(); // Explicit load
  var thumbnail = lazyLoaded.Thumbnail.Value;
  ```

### Changed
- **DynamoDbTableBase Removed** - Table classes are now fully source-generated without inheritance
  - Generated table classes no longer inherit from `DynamoDbTableBase`
  - All functionality (Client, Name, Options, Query<T>, Get<T>, Put<T>, Update<T>, Delete<T>) is now generated directly
  - Entity accessor visibility controlled by `[GenerateAccessors]` attribute
  - Index accessors generated for all defined GSIs and LSIs
  - **Breaking Change**: Code that directly references `DynamoDbTableBase` will no longer compile
  - **No Migration Required**: Code using generated table classes continues to work unchanged
  - _Requirements: 1.1-1.6_
  
  **Migration (only if directly referencing DynamoDbTableBase):**
  ```csharp
  // Before: Direct reference to base class (rare)
  public void ProcessTable(DynamoDbTableBase table) { ... }
  
  // After: Use the generated table class directly
  public void ProcessTable(UsersTable table) { ... }
  // Or use duck typing / interfaces if needed
  ```

- **Logging Runtime Configuration** - Removed `DISABLE_DYNAMODB_LOGGING` conditional compilation in favor of runtime configuration
  - All `#if !DISABLE_DYNAMODB_LOGGING` preprocessor directives removed from source code
  - Logging is now controlled entirely via `FluentDynamoDbOptions.WithLogger()` at runtime
  - `NoOpLogger.Instance` provides zero-overhead logging when disabled (via `IsEnabled()` check pattern)
  - Users no longer need to recompile to enable/disable logging
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_
  
  **Migration:**
  ```csharp
  // Before: Compile-time conditional (no longer supported)
  // <DefineConstants>DISABLE_DYNAMODB_LOGGING</DefineConstants>
  
  // After: Runtime configuration
  // Logging disabled (default - zero overhead)
  var table = new UsersTable(client, "users");
  
  // Logging enabled
  var options = new FluentDynamoDbOptions()
      .WithLogger(loggerFactory.ToDynamoDbLogger<UsersTable>());
  var table = new UsersTable(client, "users", options);
  ```

- **Documentation Reorganization** - Split `STSIntegration.md` into two focused documents for better discoverability
  - New `docs/advanced-topics/ClientConfiguration.md` covering development environments (DynamoDB Local, LocalStack), custom client settings, multi-region deployments, and proxy configuration
  - New `docs/advanced-topics/ScopedSecurity.md` covering `WithClient()` method for per-request client customization, STS-scoped credentials, and multi-tenancy patterns
  - Deleted `docs/advanced-topics/STSIntegration.md` after content migration
  - Updated `docs/advanced-topics/README.md` to reflect new document structure
- **Example Projects Configuration Pattern** - Refactored all example projects (TodoList, TransactionDemo, InvoiceManager, StoreLocator) to follow the recommended configuration pattern
  - Configuration now built once at application level in Program.cs
  - Table names passed explicitly to constructors for runtime configurability
  - Removed redundant custom table classes that only contained constructors
  - StoreLocator retains utility methods (`SelectS2Level`, `SelectH3Resolution`) while removing constructor boilerplate
  - Updated README files to reflect new project structure and patterns

- **Encrypted Properties Automatically Marked as Sensitive** - Properties with `[Encrypted]` attribute now automatically have `IsSensitive = true`
  - No need to apply both `[Encrypted]` and `[Sensitive]` attributes
  - Encrypted property values are automatically redacted in logs
  - Existing entities with both attributes continue to work without changes
  - _Requirements: 1.1, 1.2, 1.3_

- **Query Capabilities Derived from Key Attributes** - `SupportedOperations` now automatically derived from key metadata
  - Partition key properties: Support `Equals` operation
  - Sort key properties: Support `Equals`, `BeginsWith`, `Between`, `GreaterThan`, `LessThan` operations
  - No longer need to manually specify operations via `[Queryable]` attribute
  - _Requirements: 3.2, 3.3, 3.4_

- **Write Request Builder Type Constraints** - `PutItemRequestBuilder<TEntity>`, `UpdateItemRequestBuilder<TEntity>`, and `DeleteItemRequestBuilder<TEntity>` now require `TEntity : class, IDynamoDbEntity`
  - Enables AOT-safe transaction validation via static interface members
  - Existing code using entities that implement `IDynamoDbEntity` (all source-generated entities) is unaffected
  - _Requirements: 4.2, 4.3, 4.4_

### Deprecated

- **`[Queryable]` Attribute** - Deprecated in favor of automatic derivation from key attributes
  - Compiler warning `DYNDB103` emitted when `[Queryable]` is used
  - Query capabilities are now derived from `[PartitionKey]` and `[SortKey]` attributes
  - Will be removed in v1.0
  - _Requirements: 3.1_
  
  **Migration:**
  ```csharp
  // Before (deprecated):
  [Queryable(SupportedOperations = new[] { DynamoDbOperation.Equals })]
  [PartitionKey]
  [DynamoDbAttribute("pk")]
  public string UserId { get; set; }
  
  // After (automatic):
  [PartitionKey]  // Automatically supports Equals
  [DynamoDbAttribute("pk")]
  public string UserId { get; set; }
  ```

- **`[BlobReference]` Attribute** - Deprecated in favor of `[BlobStorage]` with `BlobData<T>` wrapper type
  - Compiler warning `DYNDB104` emitted when `[BlobReference]` is used
  - New `[BlobStorage]` attribute provides clearer semantics and lazy/eager loading control
  - New `BlobData<T>` wrapper type encapsulates blob storage behavior
  - Will be removed in v1.0
  - _Requirements: 1.2_
  
  **Migration:**
  ```csharp
  // Before (deprecated):
  [BlobReference(BlobProvider.S3)]
  [DynamoDbAttribute("data")]
  public byte[] Data { get; set; }
  
  // After (new pattern):
  [BlobStorage]
  [DynamoDbAttribute("data")]
  public BlobData<byte[]> Data { get; set; } = default!;
  
  // Creating data:
  // Before: entity.Data = bytes;
  // After:  entity.Data = BlobData<byte[]>.Create(bytes);
  
  // Accessing data:
  // Before: var bytes = entity.Data;
  // After:  var bytes = entity.Data.Value;
  ```

### Removed

### Fixed
- **GeoHash Query Bug** - Fixed interpolated string bug in GeoHash BETWEEN queries
  - Changed `$"geohash_cell BETWEEN {0} AND {1}"` to `"geohash_cell BETWEEN {0} AND {1}"` in StoreLocator example
  - The `$` prefix caused `{0}` and `{1}` to be treated as literal text instead of format placeholders
  - This resulted in "Invalid KeyConditionExpression" errors when executing GeoHash spatial queries
  - _Requirements: 6.1, 6.2, 6.3, 6.4_

- **Documentation API Pattern Corrections** - Fixed incorrect batch and transaction operation examples across documentation
  - Corrected batch operation examples to use `DynamoDbBatch.Write` and `DynamoDbBatch.Get` static entry points instead of constructor-based patterns
  - Corrected transaction operation examples to use `DynamoDbTransactions.Write` and `DynamoDbTransactions.Get` static entry points instead of constructor-based patterns
  - Corrected `CommitAsync()` to `ExecuteAsync()` for transaction execution
  - Files corrected: `BasicOperations.md`, `PerformanceOptimization.md`, `GlobalSecondaryIndexes.md`, `CompositeEntities.md`, `MultiEntityTables.md`, `QUICK_REFERENCE.md`, `DeveloperGuide.md`
  - See `docs/DOCUMENTATION_CHANGELOG.md` for detailed before/after patterns
- **Documentation API Style Corrections** - Fixed incorrect API method names and patterns
  - Corrected `ExecuteAsync<T>()` to `GetItemAsync<T>()` on GetItemRequestBuilder in multiple files
  - Corrected non-existent `DynamoDbTableBase<T>` generic type to use concrete typed table classes with entity accessors
  - Updated migration examples to use lambda expressions and entity accessor patterns
  - Files corrected: `AdvancedTypesMigration.md`, `CodeExamples.md`, `PerformanceOptimization.md`, `AdoptionGuide.md`
  - See `docs/DOCUMENTATION_CHANGELOG.md` Part 6 for detailed before/after patterns

- **NuGet Package Contents** - Fixed packaging to exclude test and example projects
  - Added `<IsPackable>false</IsPackable>` to all unit test projects
  - Added `<IsPackable>false</IsPackable>` to integration test project
  - Added `<IsPackable>false</IsPackable>` to example projects
  - Added `<IsPackable>false</IsPackable>` to AOT and API consistency test projects
  - Removed duplicate icon files from test/example project directories
  - _Requirements: 6.1, 6.2, 6.3, 6.4_

- **HydratorGenerator Parameter Mismatch** - Fixed source generator bug causing compilation errors for entities with `[BlobReference]` attributes
  - `HydratorGenerator` was generating calls to `FromDynamoDbAsync` and `ToDynamoDbAsync` with incorrect parameter order
  - Named parameters were incorrectly used, causing type mismatch errors (e.g., `FluentDynamoDbOptions` passed where `IFieldEncryptor?` expected)
  - Fixed by using positional parameters matching the generated method signatures
  - Affected file: `Oproto.FluentDynamoDb.SourceGenerator/Generators/HydratorGenerator.cs`

### Security

## [0.8.0] - 2025-12-05 (Preview Release)

> **Note:** This is the first public preview release of Oproto.FluentDynamoDb. While the core features are production-ready, some experimental features are still evolving.

### Feature Maturity

**Production-ready (core focus)**
- Strongly-typed entity modeling and repositories
- Single-table, multi-entity patterns (e.g., Invoice + Lines)
- Query and scan builders (LINQ-style expressions)
- Batch operations and transactional helpers
- Source generation (no reflection, AOT-friendly)

**Experimental / evolving**
- Geospatial indexing (GeoHash, S2, H3)
- S3-backed blob storage
- KMS-based field encryption
- Attributes for defining Entities *may* evolve over time

These experimental features are available for early testing and feedback, but may change shape before 1.0 and do not yet have full demo coverage or documentation.

### Breaking Changes
- **Namespace Reorganization** - Internal types moved from `Oproto.FluentDynamoDb.Storage` to dedicated namespaces for better separation of concerns
  - `IDynamoDbEntity`, `IProjectionModel`, `IDiscriminatedProjection` moved to `Oproto.FluentDynamoDb.Entities`
  - `EntityMetadata`, `PropertyMetadata`, `RelationshipMetadata`, `IndexMetadata`, `IEntityMetadataProvider` moved to `Oproto.FluentDynamoDb.Metadata`
  - `IAsyncEntityHydrator`, `IEntityHydratorRegistry`, `DefaultEntityHydratorRegistry` moved to `Oproto.FluentDynamoDb.Hydration`
  - `IFieldEncryptor`, `FieldEncryptionContext` moved to `Oproto.FluentDynamoDb.Providers.Encryption`
  - `IBlobStorageProvider`, `IJsonBlobSerializer` moved to `Oproto.FluentDynamoDb.Providers.BlobStorage`
  - `MappingErrorHandler`, `DynamoDbMappingException`, `DiscriminatorMismatchException`, `ProjectionValidationException`, `FieldEncryptionException` moved to `Oproto.FluentDynamoDb.Mapping`
  - `DynamoDbOperationContext`, `DynamoDbOperationContextDiagnostics`, `OperationContextData` moved to `Oproto.FluentDynamoDb.Context`
  - `Oproto.FluentDynamoDb.Storage` namespace now contains only physical storage abstractions: `DynamoDbTableBase`, `DynamoDbIndex`, `IDynamoDbTable`
  - _Requirements: 1.1, 2.1-2.3, 3.1-3.3, 4.1-4.3, 5.1-5.3, 6.1-6.3, 7.1-7.3, 8.1-8.3, 9.1-9.4, 10.1-10.3, 11.1_
  
  **Migration:**
  ```csharp
  // Before: All types in Storage namespace
  using Oproto.FluentDynamoDb.Storage;
  
  // After: Import specific namespaces as needed
  using Oproto.FluentDynamoDb.Entities;           // IDynamoDbEntity, IProjectionModel, IDiscriminatedProjection
  using Oproto.FluentDynamoDb.Metadata;           // EntityMetadata, PropertyMetadata, etc.
  using Oproto.FluentDynamoDb.Hydration;          // IAsyncEntityHydrator, IEntityHydratorRegistry
  using Oproto.FluentDynamoDb.Providers.Encryption; // IFieldEncryptor, FieldEncryptionContext
  using Oproto.FluentDynamoDb.Providers.BlobStorage; // IBlobStorageProvider, IJsonBlobSerializer
  using Oproto.FluentDynamoDb.Mapping;            // MappingErrorHandler, exceptions
  using Oproto.FluentDynamoDb.Context;            // DynamoDbOperationContext
  using Oproto.FluentDynamoDb.Storage;            // DynamoDbTableBase, DynamoDbIndex (unchanged)
  ```

- **JSON Serializer Runtime Configuration** - `[JsonBlob]` properties now require runtime configuration instead of compile-time assembly attributes
  - `IDynamoDbEntity` interface methods `ToDynamoDb` and `FromDynamoDb` now accept `FluentDynamoDbOptions?` instead of `IDynamoDbLogger?`
  - Removed `[assembly: DynamoDbJsonSerializer]` attribute - no longer supported
  - Removed `JsonSerializerType` enum - no longer needed
  - `[JsonBlob]` properties now require `.WithSystemTextJson()` or `.WithNewtonsoftJson()` on `FluentDynamoDbOptions` at runtime
  - Clear runtime exception thrown when `[JsonBlob]` property is used without configured serializer
  - _Requirements: 1.1, 1.2, 6.1, 6.2_
  
  **Migration:**
  ```csharp
  // Before: Compile-time assembly attribute (no longer supported)
  [assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]
  
  // After: Runtime configuration via FluentDynamoDbOptions
  var options = new FluentDynamoDbOptions()
      .WithSystemTextJson();  // Or .WithNewtonsoftJson()
  
  var table = new DocumentTable(client, "Documents", options);
  ```
  
  **Custom Serializer Options:**
  ```csharp
  // System.Text.Json with custom options
  var options = new FluentDynamoDbOptions()
      .WithSystemTextJson(new JsonSerializerOptions 
      { 
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          WriteIndented = false
      });
  
  // Newtonsoft.Json with custom settings
  var options = new FluentDynamoDbOptions()
      .WithNewtonsoftJson(new JsonSerializerSettings 
      { 
          NullValueHandling = NullValueHandling.Include,
          DateFormatString = "yyyy-MM-dd"
      });
  
  // System.Text.Json with AOT-compatible JsonSerializerContext
  var options = new FluentDynamoDbOptions()
      .WithSystemTextJson(MyJsonContext.Default);
  ```

- **Scan Opt-In Pattern** - Scan operations now require explicit opt-in via `[Scannable]` attribute
  - Removed generic `Scan<TEntity>()` method from `DynamoDbTableBase` to prevent accidental table scans
  - Scan operations are expensive and not a recommended DynamoDB access pattern
  - Entities must now have `[Scannable]` attribute to enable Scan operations
  - Use entity accessor `table.Entitys.Scan()` or `table.Scan()` for default entity
  - Generic `table.Scan<TEntity>()` method is still available when entity has `[Scannable]` attribute
  - _Requirements: 1.1_
  
  **Migration:**
  ```csharp
  // Before: Scan was always available
  var allOrders = await table.Scan<Order>().ToListAsync();
  
  // After: Add [Scannable] attribute to entity
  [DynamoDbEntity]
  [Scannable]  // Required for Scan operations
  public partial class Order : IDynamoDbEntity { ... }
  
  // Then use entity accessor
  var allOrders = await table.Orders.Scan().ToListAsync();
  ```


### Added
- **IJsonBlobSerializer Interface** - New abstraction for JSON serialization of `[JsonBlob]` properties with runtime configuration
  - `IJsonBlobSerializer` interface in core library with `Serialize<T>` and `Deserialize<T>` methods
  - `SystemTextJsonBlobSerializer` implementation in `Oproto.FluentDynamoDb.SystemTextJson` package
  - `NewtonsoftJsonBlobSerializer` implementation in `Oproto.FluentDynamoDb.NewtonsoftJson` package
  - `WithSystemTextJson()` extension method on `FluentDynamoDbOptions` with overloads for default, custom `JsonSerializerOptions`, and AOT-compatible `JsonSerializerContext`
  - `WithNewtonsoftJson()` extension method on `FluentDynamoDbOptions` with overloads for default and custom `JsonSerializerSettings`
  - `JsonSerializer` property on `FluentDynamoDbOptions` for accessing configured serializer
  - `WithJsonSerializer(IJsonBlobSerializer?)` builder method for custom serializer implementations
  - Full AOT compatibility when using `JsonSerializerContext` overload
  - Customizable serializer options (camelCase, null handling, date formats, etc.)
  - Clear runtime exception with guidance when serializer not configured
  - Source generator emits diagnostic warning (DYNDB102) when `[JsonBlob]` used without JSON package reference
  - _Requirements: 2.1, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 5.1, 5.2_
  
  **Usage Examples:**
  ```csharp
  // Default System.Text.Json configuration
  var options = new FluentDynamoDbOptions()
      .WithSystemTextJson();
  
  // Custom System.Text.Json options
  var options = new FluentDynamoDbOptions()
      .WithSystemTextJson(new JsonSerializerOptions 
      { 
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
      });
  
  // AOT-compatible with JsonSerializerContext
  var options = new FluentDynamoDbOptions()
      .WithSystemTextJson(MyJsonContext.Default);
  
  // Newtonsoft.Json with custom settings
  var options = new FluentDynamoDbOptions()
      .WithNewtonsoftJson(new JsonSerializerSettings 
      { 
          NullValueHandling = NullValueHandling.Include 
      });
  
  // Use with table
  var table = new DocumentTable(client, "Documents", options);
  ```

- **Lambda Expression Where() for Put and Delete** - Type-safe condition expressions for Put and Delete operations
  - Added `Where<TEntity>(Expression<Func<TEntity, bool>>)` extension method for `PutItemRequestBuilder<TEntity>`
  - Added `Where<TEntity>(Expression<Func<TEntity, bool>>)` extension method for `DeleteItemRequestBuilder<TEntity>`
  - Supports `AttributeExists()` and `AttributeNotExists()` extension methods in lambda expressions
  - Supports comparison operators (`==`, `!=`, `<`, `>`, `<=`, `>=`) in lambda expressions
  - Consistent API across all request builders (Query, Update, Put, Delete)
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 4.1, 4.2_
  
  **Usage Examples:**
  ```csharp
  // Conditional put - only if item doesn't exist
  await table.Orders.Put(order)
      .Where(x => x.Pk.AttributeNotExists())
      .PutAsync();
  
  // Conditional delete - only if item exists
  await table.Orders.Delete(pk, sk)
      .Where(x => x.Pk.AttributeExists())
      .DeleteAsync();
  
  // Conditional delete with comparison
  await table.Orders.Delete(pk, sk)
      .Where(x => x.Status == "pending")
      .DeleteAsync();
  ```

- **Scan() Method on Entity Accessors** - Type-safe Scan operations via entity accessors
  - Generated `Scan()` method on entity accessors for entities with `[Scannable]` attribute
  - Parameterless `Scan()` returning `ScanRequestBuilder<TEntity>`
  - `Scan(string, params object[])` with filter expression
  - `Scan(Expression<Func<TEntity, bool>>)` with lambda filter
  - _Requirements: 1.1, 1.2, 4.3_
  
  **Usage Examples:**
  ```csharp
  // Simple scan
  var allOrders = await table.Orders.Scan().ToListAsync();
  
  // Scan with lambda filter
  var activeOrders = await table.Orders.Scan(x => x.Status == "active").ToListAsync();
  
  // Scan with format string filter
  var recentOrders = await table.Orders.Scan("CreatedAt > {0}", cutoffDate).ToListAsync();
  ```

- **StoreLocator Adaptive Precision** - Multi-precision spatial indexing for the StoreLocator example application
  - Automatic precision selection based on search radius for optimal query performance
  - S2 precision levels: Level 14 (~284m) for ≤2km, Level 12 (~1.1km) for 2-10km, Level 10 (~4.5km) for >10km
  - H3 precision levels: Resolution 9 (~174m) for ≤2km, Resolution 7 (~1.2km) for 2-10km, Resolution 5 (~8.5km) for >10km
  - Multi-precision storage: stores now indexed at three precision levels simultaneously
  - New GSIs for each precision level (s2-index-fine/medium/coarse, h3-index-fine/medium/coarse)
  - Display of precision level and cell size in search results
  - Eliminates cell limit errors for searches up to 50km radius
  - Property-based tests validating precision selection and multi-precision storage
  - _Requirements: 1.1-1.4, 2.1-2.3, 3.1-3.4_

- **Documentation Overhaul** - Comprehensive documentation improvements for accuracy, organization, and maintainability
  - New `docs/advanced-topics/InternalArchitecture.md` documenting internal interfaces, source generator pipeline, and component relationships
  - New `docs/reference/ApiReference.md` with express list of all request builders, entity accessors, and direct async methods
  - New `.kiro/steering/documentation.md` establishing documentation standards for API style priority, method verification, and attribution
  - H3 third-party attribution added to `THIRD-PARTY-NOTICES.md` following S2 format with Apache License 2.0 notice
  - Organization attribution (Oproto Inc, oproto.com, oproto.io, fluentdynamodb.dev, Dan Guisinger) added to README.md and docs/README.md
  - Updated `docs/INDEX.md` and `docs/README.md` navigation with links to new documentation pages
  - New "Repository Pattern with Table Class" section in `docs/CodeExamples.md` demonstrating how to use table classes as repositories with controlled access using `[GenerateAccessors]` attribute

- **FluentDynamoDbOptions Configuration Pattern** - New centralized configuration object for AOT-compatible service registration
  - `FluentDynamoDbOptions` class with immutable `With*` methods for fluent configuration
  - `WithLogger(IDynamoDbLogger)` for logging configuration
  - `WithBlobStorage(IBlobStorageProvider)` for blob storage integration
  - `WithEncryption(IFieldEncryptor)` for field-level encryption
  - `AddGeospatial()` extension method in Geospatial package for spatial query support
  - Internal `IEntityHydratorRegistry` for async entity hydration without reflection
  - Internal `ICollectionFormatterRegistry` for type-safe collection formatting
  - All services registered at startup, eliminating runtime reflection
  - Thread-safe and immutable - safe for concurrent use across multiple tables
  - Configuration isolation - each table instance maintains independent configuration
  - Default options work for core operations without optional packages
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 5.1, 5.2, 5.3, 7.2, 7.3, 7.4, 8.1, 8.2, 8.3, 8.5_
  
  **Usage Examples:**
  ```csharp
  // Configure options with all services
  var options = new FluentDynamoDbOptions()
      .WithLogger(new ConsoleLogger())
      .WithBlobStorage(new S3BlobStorageProvider(s3Client))
      .WithEncryption(new KmsFieldEncryptor(kmsClient))
      .AddGeospatial();  // Extension from Geospatial package
  
  // Create table with options
  var table = new UserTable(dynamoDbClient, "users", options);
  
  // Or use default options for basic operations
  var simpleTable = new UserTable(dynamoDbClient, "users", new FluentDynamoDbOptions());
  ```
  
  **Migration Notes:**
  - Old constructors accepting individual parameters are deprecated but still work
  - Migrate to `FluentDynamoDbOptions` pattern for AOT compatibility
  - See [Configuration Guide](docs/core-features/configuration-guide.md) for migration examples

- **IGeospatialProvider Interface** - Abstraction for geospatial operations enabling AOT-compatible spatial queries
  - `IGeospatialProvider` interface in main library for geospatial operations
  - `CreateBoundingBox()` methods for radius and coordinate-based bounding boxes
  - `GetGeoHashRange()`, `GetS2CellCovering()`, `GetH3CellCovering()` for cell calculations
  - `GeoBoundingBoxResult` struct for bounding box results
  - `DefaultGeospatialProvider` implementation in Geospatial package
  - Eliminates reflection-based geospatial method discovery
  - Clear exception messages when geospatial provider not configured
  - _Requirements: 2.1, 2.2, 2.3, 2.4_

- **IAsyncEntityHydrator Interface** - Type-safe async entity hydration without reflection
  - `IAsyncEntityHydrator<T>` interface for async entity hydration
  - `IEntityHydratorRegistry` for hydrator registration and lookup
  - Source generator emits hydrator implementations for entities with blob references
  - Eliminates `GetMethod()` reflection for `FromDynamoDbAsync` discovery
  - Automatic fallback to synchronous `FromDynamoDb` when no hydrator registered
  - _Requirements: 3.1, 3.2, 3.4_

- **ICollectionFormatterRegistry Interface** - Type-safe collection formatting without Activator.CreateInstance
  - `ICollectionFormatterRegistry` interface for collection formatter registration
  - Source generator emits type-specific formatters for collection properties with format strings
  - Eliminates `Activator.CreateInstance` for generic collection creation
  - Preserves collection types (HashSet, List, etc.) during formatting
  - _Requirements: 4.1, 4.2, 4.3, 4.4_

- **WithClient Method on Request Builders** - Direct `WithClient(IAmazonDynamoDB client)` method on all request builders for AOT-compatible client swapping
  - Available on `QueryRequestBuilder`, `GetItemRequestBuilder`, `PutItemRequestBuilder`, `UpdateItemRequestBuilder`, `DeleteItemRequestBuilder`, and `ScanRequestBuilder`
  - Returns the same builder instance for fluent chaining
  - Replaces reflection-based `WithClientExtensions` for AOT/trimmer compatibility
  - _Requirements: 5.1, 5.2, 5.4_

- **S2 and H3 Geospatial Indexing** - Advanced spatial indexing support using Google S2 and Uber H3 hierarchical cell systems
  - S2 cell encoding/decoding with configurable precision levels (0-30)
  - H3 hexagonal cell encoding/decoding with configurable resolutions (0-15)
  - Cell covering algorithms for radius and bounding box queries
  - Spiral-ordered results (closest cells first) for optimal pagination
  - Neighbor cell calculation for comprehensive spatial coverage
  - Parent/child cell navigation for hierarchical queries
  - International Date Line crossing support with automatic bounding box splitting
  - Polar region handling with latitude clamping
  - Cell count estimation for query cost prediction
  - Hard limits on cell counts to prevent expensive queries (default: 100, max: 500)
  - `S2Cell` and `H3Cell` value types with bounds, neighbors, parent, and children
  - `S2CellCovering` and `H3CellCovering` for computing cell coverings
  - `S2Encoder` and `H3Encoder` for coordinate-to-cell conversion
  - Extension methods: `ToS2Cell()`, `ToS2Token()`, `ToH3Cell()`, `ToH3Index()`
  - GSI-based spatial query support for efficient DynamoDB queries
  - Fluent query extensions: `WithinRadiusS2()`, `WithinRadiusH3()`, `WithinBoundingBoxS2()`, `WithinBoundingBoxH3()`
  - Continuation token support for paginated spatial queries
  - Full AOT compatibility for Native AOT deployments
  - Comprehensive property-based tests using FsCheck

- **Geospatial Query Support** - New `Oproto.FluentDynamoDb.Geospatial` package for location-based queries using GeoHash
  - GeoHash encoding/decoding with configurable precision (1-12 characters)
  - Efficient proximity queries using GeoHash prefixes
  - Bounding box queries for rectangular regions
  - Radius-based queries (kilometers, miles, meters)
  - `GeoLocation` value type for latitude/longitude coordinates with validation
  - `GeoBoundingBox` for defining rectangular search areas
  - `GeoHashCell` representing GeoHash grid cells with bounds
  - Extension methods for `GeoLocation`: `ToGeoHash()`, `ToGeoHashCell()`, `FromGeoHash()`
  - Extension methods for `GeoBoundingBox`: `GetGeoHashCells()`, `GetGeoHashPrefixes()`
  - Query builder extensions: `WithinRadius()`, `WithinBoundingBox()`
  - Automatic neighbor cell calculation for boundary queries
  - Support for polar regions and international date line
  - Precision guide for accuracy vs. performance tradeoffs
  - Comprehensive documentation with examples and limitations
  - Full AOT compatibility for Native AOT deployments
  - Performance optimized: <1 microsecond encoding/decoding

- **Transaction and Batch API Redesign** - Reusable request builders for composing transaction and batch operations
  - New `DynamoDbTransactions.Write` and `DynamoDbTransactions.Get` entry points for transaction operations
  - New `DynamoDbBatch.Write` and `DynamoDbBatch.Get` entry points for batch operations
  - Reuse existing request builders (Put, Update, Delete, Get, ConditionCheck) within transaction/batch contexts
  - Access to all string formatting, lambda expressions, and source-generated key methods
  - Marker interfaces (`ITransactablePutBuilder`, `ITransactableUpdateBuilder`, etc.) for type-safe builder composition
  - New `ConditionCheckBuilder<TEntity>` for transaction condition checks without data modification
  - Automatic client inference from request builders with validation for consistent client usage
  - `WithClient()` pattern for explicit client specification supporting scoped IAM credentials
  - Transaction-level and batch-level configuration (ReturnConsumedCapacity, ClientRequestToken, ReturnItemCollectionMetrics)
  - Field encryption support in transaction and batch operations with automatic parameter encryption
  - Type-safe response deserialization with `GetItem<TEntity>(index)`, `GetItems<TEntity>(indices)`, and `ExecuteAndMapAsync<T1, T2, ...>()`
  - Comprehensive validation with clear error messages for operation limits and configuration issues
  - Logging and diagnostics for transaction/batch operations with operation counts and consumed capacity
  - Full support for string formatting with placeholders (e.g., `Where("pk = {0}", value)`)
  - Full support for lambda expressions (e.g., `Set(x => new UpdateModel { Value = "123" })`)
  - Source-generated key methods work seamlessly (e.g., `table.Update(pk, sk).Set(...)`)
  - Automatic extraction and preservation of expression attribute names and values
  - Ignores transaction/batch-incompatible settings (item-level ReturnValues, ReturnConsumedCapacity, etc.)

- **Entity-Specific Update Builders** - Simplified update operations with entity-specific builders that eliminate verbose generic parameters
  - Generated entity-specific update builder classes (e.g., `UserUpdateBuilder`) for each entity
  - Automatic type inference - entity type and update expressions type inferred from accessor
  - Simplified `Set()` method requiring only `TUpdateModel` generic parameter instead of three
  - Fluent chaining maintains proper return types throughout the builder chain
  - All extension methods automatically wrapped with entity-specific return types
  - Covariant return types for base class methods (e.g., `ReturnAllNewValues()`)
  - Full backward compatibility - existing base builders continue to work unchanged
  - Better IntelliSense support with cleaner method signatures
  - Reduced cognitive load when writing update operations

- **Convenience Methods** - Simplified methods that combine builder creation and execution in a single call
  - `GetAsync()` - Simple get operations returning entity directly
  - `PutAsync()` - Simple put operations without return values
  - `DeleteAsync()` - Simple delete operations without return values
  - `UpdateAsync()` - Update operations with configuration action
  - Support for both partition key only and composite key operations
  - Overloads for entity objects and raw `Dictionary<string, AttributeValue>`
  - Base class methods on `DynamoDbTableBase` for generic operations
  - Zero performance overhead - thin wrappers around existing extension methods
  - Reduced boilerplate for common CRUD operations
  - Improved code readability for simple operations

- **Raw Dictionary Support** - Direct support for `Dictionary<string, AttributeValue>` in all operations
  - Convenience methods accept raw attribute dictionaries
  - Builder pattern methods accept raw attribute dictionaries
  - Useful for testing, debugging, and migration scenarios
  - Enables dynamic schema scenarios without entity classes
  - Works with all operations: Get, Put, Update, Delete
  - Full support for conditions and return values with raw dictionaries

- **DateTime Kind Preservation** - Explicit timezone handling for DateTime properties
  - New `DateTimeKind` parameter in `[DynamoDbAttribute]` to specify timezone behavior
  - Support for `DateTimeKind.Utc`, `DateTimeKind.Local`, and `DateTimeKind.Unspecified`
  - Automatic conversion to specified kind during serialization (ToDynamoDb)
  - Automatic kind assignment during deserialization (FromDynamoDb)
  - Preserves timezone information across round-trip operations
  - Defaults to `DateTimeKind.Unspecified` for backward compatibility
  - Works seamlessly with format strings for combined timezone and formatting control

- **Format String Application in Serialization** - Consistent format string handling across all operations
  - Format strings from `[DynamoDbAttribute]` now applied during ToDynamoDb serialization
  - Format-aware parsing during FromDynamoDb deserialization
  - Support for DateTime formats (e.g., "yyyy-MM-dd", "o", custom patterns)
  - Support for numeric formats (e.g., "F2" for decimals, "D5" for integers)
  - Support for all IFormattable types with CultureInfo.InvariantCulture
  - Comprehensive error handling with clear messages for invalid formats
  - Backward compatible - properties without format strings use default serialization

- **Encryption Support in Update Expressions** - Field-level encryption now works in expression-based updates
  - Encrypted properties automatically encrypted in update expressions
  - Deferred encryption architecture - encryption happens at request builder layer
  - Async encryption support without blocking calls
  - Parameter metadata tracking for encryption requirements
  - Clear error messages when encryption is required but not configured
  - Support for multiple encrypted properties in single update
  - Works with format strings for encrypted formatted values
  - Consistent encryption behavior across PutItem, UpdateItem, and TransactWrite operations

- **Expression-Based Update Operations** - Type-safe update operations with compile-time validation and IntelliSense support
  - Source-generated `{Entity}UpdateExpressions` and `{Entity}UpdateModel` classes for type-safe updates
  - `UpdateExpressionProperty<T>` wrapper type enabling type-scoped extension methods
  - Extension methods for update operations: `Add()`, `Remove()`, `Delete()`, `IfNotExists()`, `ListAppend()`, `ListPrepend()`
  - Type constraints ensure operations are only available for compatible property types
  - Automatic translation of C# lambda expressions to DynamoDB update expression syntax
  - Support for SET, ADD, REMOVE, and DELETE operations in a single expression
  - Nullable type support - Extension methods work with nullable properties (`int?`, `HashSet<T>?`, `List<T>?`, etc.)
  - Arithmetic operations - Support for arithmetic in SET clauses (e.g., `x.Score + 10`, `x.Total = x.A + x.B`)
  - Format string application - Automatic application of format strings from entity metadata (DateTime, numeric formatting)
  - DynamoDB function support: `if_not_exists()`, `list_append()`, `list_prepend()`
  - Comprehensive error handling with descriptive exception messages
  - Full IntelliSense support with operation discovery based on property types
  - AOT-compatible with no runtime code generation
  - Backward compatible with existing string-based update expressions
  - **Breaking Change**: Cannot mix expression-based and string-based Set() methods in the same builder (throws `InvalidOperationException` with clear guidance)
  - Comprehensive XML documentation with examples for all APIs

- **DynamoDB Streams Support** - New `Oproto.FluentDynamoDb.Streams` package for processing DynamoDB stream events
  - Fluent API for type-safe stream record processing with `Process<TEntity>()` extension method
  - Separate package to avoid bundling Lambda dependencies in non-stream applications
  - `[GenerateStreamConversion]` attribute for opt-in stream conversion code generation
  - Generated `FromDynamoDbStream()` and `FromStreamImage()` methods for Lambda AttributeValue deserialization
  - Event-specific handlers: `OnInsert()`, `OnUpdate()`, `OnDelete()`, `OnTtlDelete()`, `OnNonTtlDelete()`
  - TTL-aware delete handling to distinguish manual vs. automatic deletions
  - LINQ-style entity filtering with `Where()` for post-deserialization filtering
  - Key-based pre-filtering with `WhereKey()` for performance optimization
  - Discriminator-based routing for single-table designs with `WithDiscriminator()`
  - Pattern matching support for discriminators (prefix, suffix, contains, exact)
  - Table-integrated stream processors with generated `OnStream()` methods
  - Automatic discriminator registry generation for table-level stream configuration
  - Comprehensive exception types: `StreamProcessingException`, `StreamDeserializationException`, `StreamFilterException`, `DiscriminatorMismatchException`
  - Full AOT compatibility for Native AOT Lambda deployments
  - Support for encrypted field deserialization in stream records


### Changed
- **API Documentation Style** - All documentation now consistently shows three API styles in priority order
  - Lambda expressions shown first (preferred) with type-safety benefits highlighted
  - Format strings shown second as alternative approach
  - Manual WithValue approach shown third for explicit control scenarios
  - Updated `docs/core-features/BasicOperations.md` and `docs/core-features/QueryingData.md` with consistent ordering
  - Updated `docs/advanced-topics/ManualPatterns.md` to reference preferred lambda approach

- **AOT/Trimmer Compatibility Improvements** - Removed all reflection usage from main library code
  - `MetadataResolver` now uses `IEntityMetadataProvider` interface constraint instead of reflection-based method lookup
  - `ProjectionExtensions` now uses `IProjectionModel` and `IDiscriminatedProjection` interfaces instead of reflection-based property discovery
  - All source-generated entity classes implement the new interfaces automatically
  - Test projects refactored to use `InternalsVisibleTo` instead of reflection for internal member access
  - `DynamicCompilationHelper` centralizes unavoidable reflection in source generator tests with proper suppressions
  - _Requirements: 2.1, 2.2, 2.4, 3.1, 4.1, 4.3_

- **Complete Reflection Elimination in Main Library** - All AOT-unsafe reflection patterns removed
  - `ExpressionTranslator` now uses `IGeospatialProvider` instead of `Assembly.GetType()` and `GetMethod()` for geospatial operations
  - `ExpressionTranslator` uses `MemberExpression.Member` directly instead of `GetProperty()` for property access
  - `UpdateExpressionTranslator` uses `ICollectionFormatterRegistry` instead of `Activator.CreateInstance` for collection formatting
  - `DynamoDbResponseExtensions` uses `IEntityHydratorRegistry` instead of `GetMethod()` for async hydration
  - `EnhancedExecuteAsyncExtensions` uses `IEntityHydratorRegistry` instead of `GetMethod()` for entity conversion
  - All main library files now pass AOT-safety analysis with no reflection warnings
  - _Requirements: 6.1, 6.2, 6.3, 6.4_

- **DynamoDbTableBase Constructor Changes** - Updated to accept FluentDynamoDbOptions
  - Old constructors accepting individual logger/encryptor parameters are deprecated
  - New constructor accepts `FluentDynamoDbOptions` for centralized configuration
  - Request builders receive options for proper service access
  - Logger and encryptor extracted from options internally
  - _Requirements: 7.2, 5.3_

- **IWithDynamoDbClient Interface Extended** - Now exposes FluentDynamoDbOptions
  - Added `Options` property to `IWithDynamoDbClient` interface
  - All request builders (`QueryRequestBuilder`, `ScanRequestBuilder`, `UpdateItemRequestBuilder`) implement the new property
  - Extension methods can access options for proper service configuration
  - _Requirements: 5.3_

- Source generation now uses nested classes to avoid namespace collisions
- Enhanced source generator to support Lambda AttributeValue types alongside SDK AttributeValue types

### Improved
- **Source Code Comment Cleanup** - Removed requirement/fix/issue references from source code comments
  - Cleaned comments in `Oproto.FluentDynamoDb/` and `Oproto.FluentDynamoDb.SourceGenerator/` projects
  - Preserved XML documentation for public APIs and comments explaining complex logic
  - Removed TODO comments referencing completed work
- **Documentation Accuracy** - Verified and updated code examples across all documentation
  - Verified examples in `docs/getting-started/`, `docs/core-features/`, and `docs/advanced-topics/`
  - Updated outdated API references to match current implementation
  - Added documentation for entity accessors, Keys.Pk()/Keys.Sk() usage, and lambda SET operations
- **Example Entity Cleanup** - Cleaned up all example project entities to follow correct attribute patterns
  - Removed incorrect `[DynamoDbEntity]` attribute from table entities (only needed for nested map types)
  - Removed manual `: IDynamoDbEntity` interface implementations (auto-generated by source generator)
  - Removed redundant `CreatePk()` and `CreateSk()` methods that duplicate source-generated `Keys` class functionality
  - Added `Prefix` configuration to `[PartitionKey]` and `[SortKey]` attributes for proper key formatting
  - Updated example code to use source-generated `Entity.Keys.Pk()` and `Entity.Keys.Sk()` methods
  - Affected projects: OperationSamples, InvoiceManager, StoreLocator, TransactionDemo, TodoList
  - Fixed documentation examples in `docs/DOCUMENTATION_CHANGELOG.md`, `docs/examples/ProjectionModelsExamples.md`, `docs/core-features/ProjectionModels.md`, and `docs/advanced-topics/FieldLevelSecurity.md`

### Deprecated

### Removed
- **WithClientExtensions** - Removed `Oproto.FluentDynamoDb/Requests/Extensions/WithClientExtensions.cs`
  - Extension methods replaced with instance methods of the same name and signature
  - No code changes required - existing `builder.WithClient(client)` calls work unchanged
  - _Requirements: 2.1, 2.3_

### Fixed
- **Documentation API Corrections** - Comprehensive fix for incorrect API method references across all documentation
  - Replaced `ExecuteAsync()` with correct method names throughout documentation:
    - `GetItemAsync()` for GetItemRequestBuilder
    - `PutAsync()` for PutItemRequestBuilder
    - `UpdateAsync()` for UpdateItemRequestBuilder
    - `DeleteAsync()` for DeleteItemRequestBuilder
    - `ToListAsync()` for QueryRequestBuilder and ScanRequestBuilder
    - `CommitAsync()` for transaction builders
  - Fixed return value access patterns to use `ToDynamoDbResponseAsync()` when accessing `response.Attributes`
  - Added alternative examples using `DynamoDbOperationContext.Current` for context-based access
  - Updated XML documentation comments in source files (DeleteItemRequestBuilder, UpdateItemRequestBuilder, PutItemRequestBuilder)
  - Corrected examples in 20+ documentation files across getting-started, core-features, advanced-topics, examples, and reference sections
  - Created `docs/DOCUMENTATION_CHANGELOG.md` for tracking documentation corrections separately from code changes
  - Updated `.kiro/steering/documentation.md` with documentation changelog requirements

- **Options Propagation in Batch and Transaction Get Operations** - Fixed `FluentDynamoDbOptions` not being passed to entity deserialization
  - `BatchGetResponse` now accepts and propagates `FluentDynamoDbOptions` to `FromDynamoDb()` calls
  - `TransactionGetResponse` now accepts and propagates `FluentDynamoDbOptions` to `FromDynamoDb()` calls
  - `BatchGetBuilder` captures options from request builders and passes to response
  - `TransactionGetBuilder` captures options from request builders and passes to response
  - Enables JSON deserialization of `[JsonBlob]` properties in batch/transaction get results
  - Enables logging during entity hydration in batch/transaction operations

- **Options Propagation in Source-Generated Hydrators** - Fixed `IAsyncEntityHydrator` not receiving options
  - Added `FluentDynamoDbOptions? options = null` parameter to `IAsyncEntityHydrator<T>` interface methods
  - Updated `HydratorGenerator` to generate code that accepts and passes options to `FromDynamoDbAsync()`
  - Updated `EnhancedExecuteAsyncExtensions` to pass options to `HydrateAsync()` and `SerializeAsync()` calls
  - Enables field encryption and JSON serialization in async hydration scenarios

- **Composite Entity Assembly (ToCompositeEntityAsync)** - Fixed `ToCompositeEntityAsync()` not populating related entity collections
  - Fixed `IsMultiItemEntity` flag not being set for entities with `[RelatedEntity]` attributes
    - `EntityAnalyzer` now sets `IsMultiItemEntity = true` when entity has relationships
    - Enables proper multi-item `FromDynamoDb` code generation for composite entities
  - Fixed wildcard pattern matching for multi-segment sort key patterns
    - Patterns like `"INVOICE#*#LINE#*"` now correctly match sort keys like `"INVOICE#INV-001#LINE#1"`
    - Implemented regex-based pattern matching with proper delimiter handling
    - Supports `#`, `_`, and `:` delimiters with automatic detection
  - Fixed primary entity identification in multi-item queries
    - `FromDynamoDb` now identifies the primary entity item using the entity's sort key pattern
    - Non-collection properties are populated from the primary entity, not the first item
    - Returns `null` when no primary entity item is found in the result set
  - Added comprehensive property-based tests for all correctness properties
  - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 5.1, 5.2, 5.3_

- **Compile Warning Reduction** - Eliminated all 1,182 compile-time warnings across the solution
  - Reduced warning count from 1,182 to 0 (target was < 100)
  - Fixed Roslyn analyzer warnings (RS2008, RS1032) in Source Generator project
  - Added analyzer release tracking files (AnalyzerReleases.Shipped.md, AnalyzerReleases.Unshipped.md)
  - Disabled AOT/trimming analysis for Source Generator project (runs at compile time, not in user binaries)
  - Fixed nullable reference type warnings (CS8604, CS8601, CS8602, CS8625, CS8618) in main library
  - Added pragma suppressions for AOT warnings (IL2026, IL3050) in DynamoDbMappingException (debugging only)
  - Added pragma suppressions for example code files (CS0618, CS1998) - documentation examples
  - Suppressed test project warnings appropriately (nullable, async, xUnit, AOT/trimming)
  - Fixed nullable warning in SpatialQueryExtensions for pagination token handling
  - Fixed nullable warning in DynamoDbIndex constructor
  - Fixed nullable warning in UpdateExpressionTranslator for list item handling
  - Improved logger call patterns in BatchGetBuilder, QueryRequestBuilder, and ScanRequestBuilder
  - All test projects now have appropriate NoWarn settings for test-specific warnings
  - Build now completes with 0 warnings and 0 errors on clean build

- **GetItemRequestBuilder** - Fixed bug where empty `ExpressionAttributeNames` dictionary was being set when no attribute names were used, causing DynamoDB to reject requests with "ExpressionAttributeNames can only be specified when using expressions" error

- **GitHub Actions** - Fixed parallel build issues causing file locking conflicts with source generator by adding `/maxcpucount:1` flag to build commands

- **Sensitive Data Redaction** - Fixed sensitive data not being redacted in log messages for properties marked with `[Sensitive]` attribute
  - Added `IsSensitive` property to `PropertyMetadata` for runtime sensitivity detection
  - Updated `ExpressionTranslator.CaptureValue()` to check `PropertyMetadata.IsSensitive` for redaction decisions
  - Updated `MapperGenerator` to populate `IsSensitive = true` in generated `PropertyMetadata` for sensitive properties
  - Sensitive property values now correctly show as `[REDACTED]` in debug logs while actual values are used in DynamoDB queries

- **Source Generator Missing Using Directive** - Fixed compilation errors in generated code when using `FluentDynamoDbOptions`
  - Added missing `using Oproto.FluentDynamoDb;` directive to `TableGenerator.cs` and `EntitySpecificUpdateBuilderGenerator.cs`
  - Generated table classes and update builders now correctly resolve `FluentDynamoDbOptions` type

- Fixed duplicate index generation on tables
- Fixed fluent chaining in `TypeHandlerRegistration` to allow multiple `.For<T>()` calls in discriminator-based routing
- **Format String Application in Update Expressions** - Format strings now consistently applied in all update expression operations
  - Format strings from entity metadata now applied in SET operations
  - Format strings applied in arithmetic operations (e.g., `x.Score + 10`)
  - Format strings applied in DynamoDB functions (IfNotExists, ListAppend, ListPrepend)
  - Ensures data consistency across PutItem, UpdateItem, and TransactWrite operations
  - Previously, format strings were only applied in some contexts, leading to inconsistent data formats
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

### Security


## [0.5.0] - 2025-11-01

### Added
- Source generation for automatic entity mapping, field constants, and key builders
- Fluent API for all DynamoDB operations (Get, Put, Query, Scan, Update, Delete)
- Expression formatting with String.Format-style syntax for concise queries
- LINQ expression support for type-safe queries with lambda expressions
- Composite entities support for complex data models and multi-item patterns
- Custom client support via `.WithClient()` for STS credentials and multi-region setups
- Batch operations (BatchGet, BatchWrite) with expression formatting
- Transaction support (TransactWrite, TransactGet) for multi-table operations
- Stream processing with fluent pattern matching for Lambda functions
- Field-level security with `[Sensitive]` attribute for logging redaction
- Field-level encryption with `[Encrypted]` attribute and KMS integration
- Multi-tenant encryption support with per-context keys
- Comprehensive logging and diagnostics system with `IDynamoDbLogger` interface
- Microsoft.Extensions.Logging adapter package (`Oproto.FluentDynamoDb.Logging.Extensions`)
- Conditional compilation support to disable logging in production builds
- Structured logging with event IDs and log levels
- Operation context (`DynamoDbOperationContext`) for accessing metadata
- Global Secondary Index (GSI) support with dedicated query builders
- Pagination support with `IPaginationRequest` interface
- AOT (Ahead-of-Time) compilation compatibility
- Trimmer-safe implementation for Native AOT scenarios
- S3 blob storage integration (`Oproto.FluentDynamoDb.BlobStorage.S3`)
- KMS encryption integration (`Oproto.FluentDynamoDb.Encryption.Kms`)
- FluentResults integration (`Oproto.FluentDynamoDb.FluentResults`)
- Newtonsoft.Json serialization support (`Oproto.FluentDynamoDb.NewtonsoftJson`)
- System.Text.Json serialization support (`Oproto.FluentDynamoDb.SystemTextJson`)
- Comprehensive documentation with guides for getting started, core features, and advanced topics
- Integration test infrastructure with DynamoDB Local support
- Unit test coverage across all major components
- Format specifiers for DateTime (`:o`), numeric (`:F2`), and other types
- Sensitive data redaction in logs to protect PII
- Diagnostic utilities for debugging and troubleshooting
- Performance metrics collection for operation monitoring

### Changed
- N/A (Initial release)

### Deprecated
- N/A (Initial release)

### Removed
- N/A (Initial release)

### Fixed
- N/A (Initial release)

### Security
- Field-level encryption with AWS KMS for protecting sensitive data at rest
- Automatic redaction of sensitive fields in logs to prevent PII exposure
- Multi-tenant encryption key isolation for secure multi-tenancy scenarios

[Unreleased]: https://github.com/oproto/fluent-dynamodb/compare/v0.8.0...HEAD
[0.8.0]: https://github.com/oproto/fluent-dynamodb/compare/v0.5.0...v0.8.0
[0.5.0]: https://github.com/oproto/fluent-dynamodb/releases/tag/v0.5.0
