# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
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
