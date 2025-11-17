# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed
- **GetItemRequestBuilder** - Fixed bug where empty `ExpressionAttributeNames` dictionary was being set when no attribute names were used, causing DynamoDB to reject requests with "ExpressionAttributeNames can only be specified when using expressions" error
- **GitHub Actions** - Fixed parallel build issues causing file locking conflicts with source generator by adding `/maxcpucount:1` flag to build commands

### Added
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
  
  **Usage Examples:**
  ```csharp
  // Encode location to GeoHash
  var location = new GeoLocation(37.7749, -122.4194); // San Francisco
  var geohash = location.ToGeoHash(precision: 6); // "9q8yyk"
  
  // Query stores within 5km radius
  var stores = await storeTable.Query
      .WithinRadius(userLocation, radiusKm: 5.0, precision: 6)
      .ToListAsync();
  
  // Query within bounding box
  var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 10.0);
  var results = await table.Query
      .WithinBoundingBox(bbox, precision: 5)
      .ToListAsync();
  
  // Get GeoHash cell with bounds
  var cell = location.ToGeoHashCell(precision: 6);
  Console.WriteLine($"Cell covers: {cell.Bounds}");
  ```
  
  **Key Features:**
  - Precision levels 1-12 with accuracy from ~5000km to ~3.7cm
  - Efficient prefix-based queries using DynamoDB's native capabilities
  - Automatic handling of boundary cases (poles, date line)
  - Neighbor cell calculation for comprehensive coverage
  - Value types for zero-allocation performance
  - Comprehensive validation and error handling
  
  **Use Cases:**
  - Store locators and proximity search
  - Geofencing and location-based notifications
  - Delivery zone management
  - Real estate and property search
  - Event discovery by location
  - Fleet tracking and logistics
  
  **Documentation:**
  - [README](Oproto.FluentDynamoDb.Geospatial/README.md) - Overview and quick start
  - [EXAMPLES](Oproto.FluentDynamoDb.Geospatial/EXAMPLES.md) - Real-world usage patterns
  - [PRECISION_GUIDE](Oproto.FluentDynamoDb.Geospatial/PRECISION_GUIDE.md) - Choosing the right precision
  - [LIMITATIONS](Oproto.FluentDynamoDb.Geospatial/LIMITATIONS.md) - Known constraints and workarounds
  
  **Migration Notes:**
  - New package - no breaking changes to existing code
  - Install `Oproto.FluentDynamoDb.Geospatial` NuGet package
  - Add GeoHash attribute to your entity for location storage
  - Use query extensions for location-based queries
  - See examples for integration with existing tables
  - _Requirements: 1.1-1.5, 2.1-2.4, 3.1-3.4, 4.1-4.3, 5.1-5.3, 6.1-6.4, 7.1-7.3, 8.1-8.3, 9.1-9.3, 10.1-10.3, 11.1-11.3, 12.1-12.3, 13.1-13.3, 14.1_


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
  
  **Usage Examples:**
  ```csharp
  // Transaction Write - Compose operations using existing builders
  await DynamoDbTransactions.Write
      .Add(userTable.Put(newUser))
      .Add(orderTable.Update(orderId).Set(x => new OrderUpdateModel { Status = "confirmed" }))
      .Add(inventoryTable.ConditionCheck(productId).Where("quantity > {0}", 0))
      .WithClientRequestToken(idempotencyToken)
      .ReturnConsumedCapacity()
      .ExecuteAsync();
  
  // Transaction Get - Retrieve multiple items atomically
  var (user, order, product) = await DynamoDbTransactions.Get
      .Add(userTable.Get(userId))
      .Add(orderTable.Get(orderId))
      .Add(productTable.Get(productId))
      .ExecuteAndMapAsync<User, Order, Product>();
  
  // Batch Write - Efficient multi-item writes
  await DynamoDbBatch.Write
      .Add(userTable.Put(user1))
      .Add(userTable.Put(user2))
      .Add(orderTable.Delete(oldOrderId))
      .ReturnConsumedCapacity()
      .ExecuteAsync();
  
  // Batch Get - Efficient multi-item reads
  var response = await DynamoDbBatch.Get
      .Add(userTable.Get(userId1))
      .Add(userTable.Get(userId2))
      .Add(orderTable.Get(orderId))
      .ExecuteAsync();
  
  var users = response.GetItems<User>(0, 1);
  var order = response.GetItem<Order>(2);
  ```
  
  **Key Benefits:**
  - Eliminates code duplication - reuse existing request builders and their fluent methods
  - Access to all string formatting features (e.g., `Where("pk = {0}", value)`)
  - Access to all lambda expression features (e.g., `Set(x => new UpdateModel { Value = "123" })`)
  - Source-generated key methods work seamlessly (e.g., `table.Update(pk, sk)`)
  - Field encryption works automatically in transactions and batches
  - Type-safe response deserialization with compile-time checking
  - Consistent API across individual operations, transactions, and batches
  - Automatic client management with validation
  - Clear error messages for validation failures
  - Full logging and diagnostics support
  
  **Migration Notes:**
  - Replaces previous action-based transaction/batch APIs
  - Old APIs remain for backward compatibility but are deprecated
  - New APIs provide better type safety and code reuse
  - See [Transaction and Batch Operations Guide](docs/core-features/TransactionAndBatchOperations.md) for migration examples
  - _Requirements: 1.1-1.8, 2.1-2.5, 3.1-3.7, 4.1-4.7, 5.1-5.5, 6.1-6.6, 7.1-7.5, 8.1-8.5, 9.1-9.5, 10.1-10.5, 11.1-11.5, 12.1-12.5, 13.1-13.7, 14.1-14.7, 15.1-15.7, 16.1-16.8, 17.1-17.8, 18.1-18.5_


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
  
  **Usage Example:**
  ```csharp
  // Before: Required 3 generic type parameters
  await table.Update<User>()
      .WithKey(User.Fields.UserId, "user123")
      .Set<User, UserUpdateExpressions, UserUpdateModel>(x => new UserUpdateModel 
      { 
          Status = "active" 
      })
      .UpdateAsync();
  
  // After: Entity-specific builder infers types automatically
  await table.Users.Update("user123")
      .Set(x => new UserUpdateModel { Status = "active" })
      .UpdateAsync();
  ```
  
  **Key Benefits:**
  - Only one generic parameter (`TUpdateModel`) instead of three
  - Entity type (`User`) inferred from accessor (`table.Users`)
  - Update expressions type (`UserUpdateExpressions`) inferred automatically
  - Cleaner, more readable code with less boilerplate
  - Maintains full type safety and compile-time checking
  - All condition expression methods work with inferred types
  
  **Migration Notes:**
  - No breaking changes - existing code continues to work
  - Adopt incrementally by using entity accessors (e.g., `table.Users.Update()`)
  - See [API Patterns Migration Guide](docs/migration/ApiPatternsMigration.md) for detailed examples
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4_

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
  
  **Usage Examples:**
  ```csharp
  // Simple get - returns entity directly
  var user = await table.Users.GetAsync("user123");
  
  // Simple put - no return value
  await table.Users.PutAsync(user);
  
  // Simple delete
  await table.Users.DeleteAsync("user123");
  
  // Update with configuration action
  await table.Users.UpdateAsync("user123", update => 
      update.Set(x => new UserUpdateModel { Status = "active" }));
  
  // Composite key operations
  var order = await table.Orders.GetAsync("customer123", "order456");
  await table.Orders.DeleteAsync("customer123", "order456");
  ```
  
  **When to Use:**
  - Simple CRUD operations without conditions
  - No need for return values or response metadata
  - Eventually consistent reads are acceptable
  - Quick prototyping or testing
  - Code readability is priority
  
  **When to Use Builder Pattern:**
  - Conditional expressions required
  - Need return values (old/new attributes)
  - Projection expressions to limit data transfer
  - Strongly consistent reads required
  - Custom capacity or retry settings
  
  **Migration Notes:**
  - No breaking changes - additive enhancement
  - Use for new simple operations to reduce boilerplate
  - Keep builder pattern for complex operations
  - Both patterns can be mixed in the same codebase
  - See [API Patterns Migration Guide](docs/migration/ApiPatternsMigration.md) for guidance
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 4.3_

- **Raw Dictionary Support** - Direct support for `Dictionary<string, AttributeValue>` in all operations
  - Convenience methods accept raw attribute dictionaries
  - Builder pattern methods accept raw attribute dictionaries
  - Useful for testing, debugging, and migration scenarios
  - Enables dynamic schema scenarios without entity classes
  - Works with all operations: Get, Put, Update, Delete
  - Full support for conditions and return values with raw dictionaries
  
  **Usage Examples:**
  ```csharp
  // Put raw dictionary - convenience method
  await table.Users.PutAsync(new Dictionary<string, AttributeValue>
  {
      ["pk"] = new AttributeValue { S = "user123" },
      ["username"] = new AttributeValue { S = "john_doe" },
      ["email"] = new AttributeValue { S = "john@example.com" }
  });
  
  // Put raw dictionary with condition - builder pattern
  await table.Users.Put(rawAttributes)
      .Where("attribute_not_exists(pk)")
      .PutAsync();
  
  // Dynamic attributes based on runtime conditions
  var attributes = new Dictionary<string, AttributeValue>
  {
      ["pk"] = new AttributeValue { S = userId },
      ["username"] = new AttributeValue { S = username }
  };
  
  if (includeMetadata)
  {
      attributes["metadata"] = new AttributeValue { M = metadataMap };
  }
  
  await table.Users.PutAsync(attributes);
  ```
  
  **Use Cases:**
  - Testing and debugging DynamoDB operations
  - Migration from other libraries with existing AttributeValue code
  - Dynamic schema scenarios where entity classes aren't practical
  - Advanced DynamoDB features not yet supported by entity mapping
  - Quick prototyping without defining entity classes
  
  **Migration Notes:**
  - Complements entity-based operations, doesn't replace them
  - Use entity classes for production code when possible
  - Raw dictionaries useful for edge cases and testing
  - See [API Patterns Migration Guide](docs/migration/ApiPatternsMigration.md) for examples
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4_

- **Comprehensive Documentation Updates**
  - Updated [Basic Operations](docs/core-features/BasicOperations.md) with convenience methods and entity-specific builder examples
  - New [Entity-Specific Builders Examples](docs/examples/EntitySpecificBuildersExamples.md) with real-world patterns
  - Decision guides for choosing between API patterns
  - Quick reference tables comparing convenience methods vs builder patterns
  - Troubleshooting section for common issues
  - Complete code examples for user management, e-commerce, and session management
  - Updated README with API patterns overview and examples


- **DateTime Kind Preservation** - Explicit timezone handling for DateTime properties
  - New `DateTimeKind` parameter in `[DynamoDbAttribute]` to specify timezone behavior
  - Support for `DateTimeKind.Utc`, `DateTimeKind.Local`, and `DateTimeKind.Unspecified`
  - Automatic conversion to specified kind during serialization (ToDynamoDb)
  - Automatic kind assignment during deserialization (FromDynamoDb)
  - Preserves timezone information across round-trip operations
  - Defaults to `DateTimeKind.Unspecified` for backward compatibility
  - Works seamlessly with format strings for combined timezone and formatting control
  
  **Usage Example:**
  ```csharp
  [DynamoDbEntity("users")]
  public partial class User
  {
      // Store as UTC, automatically convert on save
      [DynamoDbAttribute("created_at", DateTimeKind = DateTimeKind.Utc)]
      public DateTime CreatedAt { get; set; }
      
      // Store as local time with custom format
      [DynamoDbAttribute("last_login", Format = "yyyy-MM-dd HH:mm:ss", DateTimeKind = DateTimeKind.Local)]
      public DateTime LastLogin { get; set; }
  }
  ```
  
  **Migration Notes:**
  - Existing code without `DateTimeKind` specified continues to work unchanged
  - Add `DateTimeKind` parameter to attributes where timezone preservation is important
  - Consider using `DateTimeKind.Utc` for most scenarios to avoid timezone ambiguity
  - See [DateTime Kind Guide](docs/core-features/datetime-kind-guide.md) for best practices
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- **Format String Application in Serialization** - Consistent format string handling across all operations
  - Format strings from `[DynamoDbAttribute]` now applied during ToDynamoDb serialization
  - Format-aware parsing during FromDynamoDb deserialization
  - Support for DateTime formats (e.g., "yyyy-MM-dd", "o", custom patterns)
  - Support for numeric formats (e.g., "F2" for decimals, "D5" for integers)
  - Support for all IFormattable types with CultureInfo.InvariantCulture
  - Comprehensive error handling with clear messages for invalid formats
  - Backward compatible - properties without format strings use default serialization
  
  **Usage Example:**
  ```csharp
  [DynamoDbEntity("products")]
  public partial class Product
  {
      // Store date without time component
      [DynamoDbAttribute("release_date", Format = "yyyy-MM-dd")]
      public DateTime ReleaseDate { get; set; }
      
      // Store price with exactly 2 decimal places
      [DynamoDbAttribute("price", Format = "F2")]
      public decimal Price { get; set; }
      
      // Store SKU with zero-padding
      [DynamoDbAttribute("sku", Format = "D8")]
      public int Sku { get; set; }
  }
  ```
  
  **Migration Notes:**
  - Existing format strings now take effect in PutItem and UpdateItem operations
  - Verify format strings match your data requirements before deploying
  - Use consistent formats across your application for data integrity
  - See [Format Strings Guide](docs/core-features/format-strings-guide.md) for examples
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5_

- **Encryption Support in Update Expressions** - Field-level encryption now works in expression-based updates
  - Encrypted properties automatically encrypted in update expressions
  - Deferred encryption architecture - encryption happens at request builder layer
  - Async encryption support without blocking calls
  - Parameter metadata tracking for encryption requirements
  - Clear error messages when encryption is required but not configured
  - Support for multiple encrypted properties in single update
  - Works with format strings for encrypted formatted values
  - Consistent encryption behavior across PutItem, UpdateItem, and TransactWrite operations
  
  **Usage Example:**
  ```csharp
  [DynamoDbEntity("users")]
  public partial class User
  {
      [DynamoDbAttribute("ssn")]
      [Encrypted]
      public string SocialSecurityNumber { get; set; }
      
      [DynamoDbAttribute("credit_card")]
      [Encrypted]
      public string CreditCard { get; set; }
  }
  
  // Encrypted properties are automatically encrypted in updates
  await table.Update()
      .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
      .Set(x => new UserUpdateModel 
      {
          SocialSecurityNumber = "123-45-6789",  // Automatically encrypted
          CreditCard = "4111-1111-1111-1111"     // Automatically encrypted
      })
      .ExecuteAsync();
  ```
  
  **Architecture:**
  - UpdateExpressionTranslator marks parameters requiring encryption
  - UpdateItemRequestBuilder encrypts marked parameters before sending to DynamoDB
  - No breaking changes - translator remains synchronous
  - Proper async handling at the request builder layer
  - Consistent with blob reference pattern
  
  **Migration Notes:**
  - Encryption now works in expression-based updates (previously threw NotSupportedException)
  - Ensure IFieldEncryptor is configured in DynamoDbOperationContext
  - No code changes required - existing encrypted properties work automatically
  - See [Encryption Guide](docs/core-features/encryption-guide.md) for setup instructions
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

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
  
  **Usage Examples:**
  ```csharp
  // Type-safe update with multiple operations
  await table.Update()
      .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
      .Set(x => new UserUpdateModel 
      {
          Name = "John Doe",              // SET operation
          LoginCount = x.LoginCount.Add(1), // ADD operation (atomic increment)
          Tags = x.Tags.Delete("old-tag"), // DELETE operation (remove from set)
          TempData = x.TempData.Remove()   // REMOVE operation (delete attribute)
      })
      .ExecuteAsync();
  
  // Generates: SET #name = :p0 ADD #login_count :p1 DELETE #tags :p2 REMOVE #temp_data
  ```
  
  **Advanced Features:**
  ```csharp
  // Nullable type support
  public HashSet<int>? CategoryIds { get; set; }  // Nullable property
  CategoryIds = x.CategoryIds.Add(5)  // Works seamlessly
  
  // Arithmetic operations
  Score = x.Score + 10  // Intuitive arithmetic syntax
  TotalScore = x.BaseScore + x.BonusScore  // Property-to-property operations
  
  // Format string application
  [DynamoDbAttribute("created_date", Format = "yyyy-MM-dd")]
  public DateTime CreatedDate { get; set; }
  CreatedDate = DateTime.Now  // Automatically formatted as "2024-03-15"
  ```
  
  **Known Limitations:**
  - Field-level encryption not yet implemented for expression-based updates (use string-based Set() as workaround)
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
- Source generation now uses nested classes to avoid namespace collisions
- Enhanced source generator to support Lambda AttributeValue types alongside SDK AttributeValue types

### Deprecated

### Removed

### Fixed
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

[Unreleased]: https://github.com/oproto/fluent-dynamodb/compare/v0.5.0...HEAD
[0.5.0]: https://github.com/oproto/fluent-dynamodb/releases/tag/v0.5.0
