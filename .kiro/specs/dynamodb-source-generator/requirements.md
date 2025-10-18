# DynamoDB Source Generator Requirements

## Introduction

This feature adds source code generation capabilities to the Oproto.FluentDynamoDb library to dramatically reduce boilerplate code while maintaining AOT compatibility. The source generator will create entity mapping code, field name constants, key builders, and enhanced ExecuteAsync methods that provide a more EF/LINQ-like experience while preserving the existing fluent API for complex scenarios.

### Example Scenarios

**Multi-Item Entity Example:**
A `TransactionEntry` entity that contains multiple `LedgerEntry` objects. In DynamoDB, this is stored as:
- PK: `tenant123#txn#txn456` SK: `ledger789#line001` (first ledger entry)  
- PK: `tenant123#txn#txn456` SK: `ledger789#line002` (second ledger entry)
- PK: `tenant123#txn#txn456` SK: `ledger999#line003` (third ledger entry)

When querying with `ExecuteAsync<TransactionEntry>()`, all items with the same partition key are grouped and mapped into a single `TransactionEntry` object with a collection of `LedgerEntry` objects.

**Multi-Type Query Example:**
A table containing both `TransactionEntry` and `LedgerSummary` entities:
- PK: `tenant123#txn#txn456` SK: `ledger789#line001` (TransactionEntry data)
- PK: `tenant123#timeseries` SK: `ledger789` (LedgerSummary data)

When querying with `ExecuteAsync<TransactionEntry>()`, only items matching the TransactionEntry pattern are returned and mapped. The LedgerSummary items are filtered out automatically.

**Related Entity Example:**
A transaction entity with related child entities defined directly on it:
```csharp
[DynamoDbTable("transactions")]
public partial class TransactionEntry
{
    // Primary entity properties
    public Ulid TenantId { get; set; }
    public Ulid TransactionId { get; set; }
    public List<TransactionLedgerEntry> LedgerEntries { get; set; } // Multi-item entity
    
    // Related entities (populated based on what data is returned)
    [RelatedEntity(SortKeyPattern = "audit#*")]
    public List<AuditEntry>? AuditEntries { get; set; }
    
    [RelatedEntity(SortKeyPattern = "summary")]
    public LedgerSummary? Summary { get; set; }
}
```

When querying with `ExecuteAsync<TransactionEntry>()`, whatever items are returned by the query expression are automatically mapped to the appropriate properties based on their sort key patterns. If your query brings back audit entries, they'll be populated in `AuditEntries`. If not, the property remains null. This gives you full control through your query expression while automatically handling the complex mapping.

**Future LINQ Support Consideration:**
The attribute metadata design must be comprehensive enough to support future LINQ-style expressions like:
```csharp
// Future LINQ support - not implemented in this spec
var activeTransactions = await table
    .Where(t => t.TenantId == tenantId && t.Status == TransactionStatus.Active)
    .Include(t => t.AuditEntries)
    .OrderByDescending(t => t.CreatedDate)
    .Take(50)
    .ToListAsync<TransactionEntry>();
```

The attributes must capture sufficient metadata about types, relationships, indexes, and constraints to enable AOT-compatible LINQ expression translation in future versions.

## Glossary

- **Entity**: A C# class that represents data stored in DynamoDB, decorated with DynamoDB attributes
- **Source Generator**: A compile-time code generation tool that analyzes source code and generates additional C# code
- **Mapper**: Generated code that converts between C# objects and DynamoDB AttributeValue dictionaries
- **Field Constants**: Generated static string constants representing DynamoDB attribute names
- **Key Builders**: Generated static methods that construct DynamoDB partition and sort keys
- **Enhanced ExecuteAsync**: Generic versions of ExecuteAsync methods that automatically handle entity mapping
- **Multi-Item Entity**: A single logical entity that spans multiple DynamoDB items with the same partition key (e.g., Transaction with multiple LedgerEntries stored as separate items)
- **Single-Item Entity**: An entity that maps to exactly one DynamoDB item
- **Multi-Type Query**: A query that returns multiple different entity types from the same table (e.g., both Transaction and LedgerSummary entities)
- **Related Entity**: Secondary entities that can be optionally loaded and nested within a primary entity based on sort key patterns

- **IDynamoDbEntity**: Generated interface that entities implement to provide mapping capabilities
- **FluentResults Extension**: Optional package that wraps ExecuteAsync methods to return Result<T> instead of throwing exceptions
- **Source Generator**: A .NET Standard 2.0 analyzer project that generates code at compile time
- **Analyzer Package**: A NuGet package that includes both the main library and the source generator as an analyzer
- **LINQ Expression Metadata**: Comprehensive attribute information needed to translate LINQ expressions to DynamoDB queries in future versions

## Requirements

### Requirement 1: Entity Definition and Mapping

**User Story:** As a developer, I want to define DynamoDB entities using attributes so that mapping code is generated automatically.

#### Acceptance Criteria

1. WHEN I decorate a class with [DynamoDbTable], THE Source_Generator SHALL generate mapping methods for the entity
2. WHEN I decorate properties with [DynamoDbAttribute], THE Source_Generator SHALL map properties to DynamoDB attribute names
3. WHEN I specify [PartitionKey] and [SortKey] attributes, THE Source_Generator SHALL generate appropriate key builder methods
4. WHEN an entity spans multiple DynamoDB items, THE Source_Generator SHALL generate multi-item mapping logic
5. WHEN an entity maps to a single DynamoDB item, THE Source_Generator SHALL generate single-item mapping logic

### Requirement 2: Field Name Constants Generation

**User Story:** As a developer, I want generated field name constants so that I can reference DynamoDB attribute names safely in custom queries.

#### Acceptance Criteria

1. WHEN I define an entity with DynamoDB attributes, THE Source_Generator SHALL create a static Fields class with string constants
2. WHEN I have Global Secondary Indexes defined, THE Source_Generator SHALL create nested GSI field classes
3. WHEN I reference a field constant in code, THE Source_Generator SHALL ensure compile-time safety
4. WHEN field names change in attributes, THE Source_Generator SHALL update all generated constants automatically
5. WHEN I use reserved DynamoDB words as property names, THE Source_Generator SHALL handle attribute name mapping correctly

### Requirement 3: Key Builder Generation

**User Story:** As a developer, I want generated key builder methods so that I can construct DynamoDB keys without manual string concatenation.

#### Acceptance Criteria

1. WHEN I define partition and sort key attributes, THE Source_Generator SHALL create static key builder methods
2. WHEN I have composite keys with multiple components, THE Source_Generator SHALL generate methods that accept all components
3. WHEN I have GSI keys defined, THE Source_Generator SHALL create separate key builders for each GSI
4. WHEN key formats include prefixes or separators, THE Source_Generator SHALL handle the formatting automatically
5. WHEN I call a key builder method, THE Source_Generator SHALL ensure type safety for all parameters

### Requirement 4: Enhanced ExecuteAsync Methods

**User Story:** As a developer, I want generic ExecuteAsync methods so that I can work with strongly-typed entities instead of AttributeValue dictionaries.

#### Acceptance Criteria

1. WHEN I call ExecuteAsync<T> on a GetItemRequestBuilder, THE Enhanced_Method SHALL return GetItemResponse<T> with mapped entity
2. WHEN I call ExecuteAsync<T> on a QueryRequestBuilder, THE Enhanced_Method SHALL return QueryResponse<T> with mapped entities
3. WHEN I call ExecuteAsync<T> on a PutItemRequestBuilder, THE Enhanced_Method SHALL accept the entity and convert to AttributeValues
4. WHEN I use WithItem<T> with an entity object, THE Enhanced_Method SHALL store the object for later conversion
5. WHEN an entity maps to multiple DynamoDB items, THE Enhanced_Method SHALL handle grouping and mapping automatically

### Requirement 5: IDynamoDbEntity Interface Implementation

**User Story:** As a developer, I want entities to implement a common interface so that generic ExecuteAsync methods can work with any mapped entity.

#### Acceptance Criteria

1. WHEN I define a DynamoDB entity, THE Source_Generator SHALL make it implement IDynamoDbEntity interface
2. WHEN ExecuteAsync<T> is called, THE Enhanced_Method SHALL use static abstract interface methods for mapping
3. WHEN I constrain generic methods with IDynamoDbEntity, THE Compiler SHALL ensure only mapped entities are accepted
4. WHEN mapping fails, THE Interface_Implementation SHALL provide clear error messages
5. WHEN multiple entities share the same table, THE Interface_Implementation SHALL handle entity discrimination correctly

### Requirement 6: Multi-Item Entity Support

**User Story:** As a developer, I want to work with entities that span multiple DynamoDB items so that I can model complex relationships naturally.

#### Acceptance Criteria

1. WHEN an entity contains collections that map to separate DynamoDB items with the same partition key, THE Source_Generator SHALL create appropriate mapping logic
2. WHEN I query for a multi-item entity, THE Enhanced_Method SHALL group related items by partition key and reconstruct the complete entity
3. WHEN I save a multi-item entity, THE Enhanced_Method SHALL create multiple DynamoDB items with consistent partition keys
4. WHEN items belong to the same logical entity, THE Source_Generator SHALL use the partition key to group them during mapping
5. WHEN I update a multi-item entity, THE Enhanced_Method SHALL handle item additions, updates, and deletions within the same partition key

### Requirement 7: Multi-Type Query Support

**User Story:** As a developer, I want to query tables that contain multiple entity types so that I can work with heterogeneous data efficiently.

#### Acceptance Criteria

1. WHEN a table contains multiple entity types, THE Source_Generator SHALL support entity discrimination through sort key patterns or explicit discriminator fields
2. WHEN I query with ExecuteAsync<T>, THE Enhanced_Method SHALL filter and map only items that match the specified entity type
3. WHEN I need to query for multiple entity types, THE Enhanced_Method SHALL provide mechanisms to handle mixed results
4. WHEN entity types share the same partition key, THE Source_Generator SHALL use sort key patterns to distinguish between types
5. WHEN I query without specifying a type, THE Enhanced_Method SHALL return raw AttributeValue dictionaries as fallback

### Requirement 8: Related Entity Support

**User Story:** As a developer, I want to define related entities directly on my primary entities so that they are automatically populated based on what data my query returns.

#### Acceptance Criteria

1. WHEN I define properties with [RelatedEntity] attributes, THE Source_Generator SHALL create mapping logic that automatically maps related entity types
2. WHEN I query with ExecuteAsync<T>, THE Enhanced_Method SHALL populate related entity properties based on what data is returned by the query
3. WHEN related entities share the same partition key, THE Source_Generator SHALL group them by partition key and map to appropriate related properties
4. WHEN I define sort key patterns for related entities, THE Source_Generator SHALL use these patterns to identify and map related data
5. WHEN query results don't include related entities, THE Enhanced_Method SHALL leave related properties as null or empty collections

### Requirement 9: FluentResults Integration

**User Story:** As a developer, I want optional FluentResults support so that I can use Result<T> return patterns instead of exceptions.

#### Acceptance Criteria

1. WHEN I reference the FluentResults extension package, THE Extension_Methods SHALL wrap ExecuteAsync calls in Result<T>
2. WHEN DynamoDB operations succeed, THE Extension_Methods SHALL return Result.Ok with the mapped entity
3. WHEN DynamoDB operations fail, THE Extension_Methods SHALL return Result.Fail with error details
4. WHEN I don't reference the FluentResults package, THE Core_Library SHALL remain dependency-free
5. WHEN exceptions occur during mapping, THE Extension_Methods SHALL convert them to failed Results

### Requirement 10: Backward Compatibility

**User Story:** As a developer, I want existing fluent API code to continue working so that I can adopt the source generator incrementally.

#### Acceptance Criteria

1. WHEN I use existing ExecuteAsync methods, THE Core_Library SHALL continue to work without changes
2. WHEN I use existing WithItem methods with AttributeValue dictionaries, THE Core_Library SHALL accept them unchanged
3. WHEN I mix generated and manual approaches, THE Library SHALL support both patterns in the same codebase
4. WHEN I need complex operations not supported by generation, THE Fluent_API SHALL remain fully accessible
5. WHEN I upgrade to use source generation, THE Migration SHALL require minimal code changes

### Requirement 11: AOT Compatibility

**User Story:** As a developer, I want the generated code to work with AOT compilation so that I can deploy to environments that don't support JIT compilation.

#### Acceptance Criteria

1. WHEN I compile with AOT enabled, THE Generated_Code SHALL not use reflection at runtime
2. WHEN I use generic ExecuteAsync methods, THE Generated_Code SHALL resolve types at compile time
3. WHEN mapping occurs, THE Generated_Code SHALL use direct property access instead of reflection
4. WHEN I deploy to AOT environments, THE Generated_Code SHALL execute without runtime code generation
5. WHEN trimming is enabled, THE Generated_Code SHALL not reference unnecessary assemblies

### Requirement 12: Source Generator Packaging and Compatibility

**User Story:** As a developer, I want the source generator to work seamlessly across different .NET versions and be easy to consume.

#### Acceptance Criteria

1. WHEN I reference Oproto.FluentDynamoDb, THE Source_Generator SHALL be automatically included as an analyzer
2. WHEN I use .NET 6, 7, or 8 projects, THE Source_Generator SHALL work consistently across all versions
3. WHEN the source generator runs, THE Generator SHALL target .NET Standard 2.0 for maximum compatibility
4. WHEN I build my project, THE Source_Generator SHALL execute during compilation without additional configuration
5. WHEN I package the library, THE Source_Generator SHALL be included as a separate analyzer project within the main NuGet package

### Requirement 13: Future LINQ Expression Support

**User Story:** As a developer, I want the attribute metadata to be comprehensive enough to support future LINQ-style query expressions.

#### Acceptance Criteria

1. WHEN I define entity attributes, THE Metadata SHALL capture sufficient information for future LINQ expression translation
2. WHEN properties have DynamoDB-specific constraints, THE Attributes SHALL capture type information, key relationships, and query capabilities
3. WHEN GSI relationships are defined, THE Metadata SHALL capture projection information and query patterns for future LINQ support
4. WHEN related entities are defined, THE Attributes SHALL capture relationship metadata that could support future join-style LINQ expressions
5. WHEN the attribute design is complete, THE Metadata SHALL provide a foundation for AOT-compatible LINQ expression trees

### Requirement 14: Error Handling and Diagnostics

**User Story:** As a developer, I want clear error messages and diagnostics so that I can troubleshoot source generation issues easily.

#### Acceptance Criteria

1. WHEN entity attributes are misconfigured, THE Source_Generator SHALL provide clear compilation errors
2. WHEN mapping fails at runtime, THE Generated_Code SHALL provide detailed error messages
3. WHEN key builders receive invalid parameters, THE Generated_Code SHALL validate inputs and provide helpful errors
4. WHEN multiple entities conflict in the same table, THE Source_Generator SHALL detect and report conflicts
5. WHEN debugging generated code, THE Source_Generator SHALL produce readable, well-commented output