# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Deprecated

### Removed

### Fixed

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
