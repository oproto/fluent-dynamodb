# Design Document: Schema Validation

## Overview

This feature provides runtime schema validation for DynamoDB tables, enabling developers to verify that actual DynamoDB table configurations match the entity metadata defined through source generation. The validation is designed for startup-time execution (e.g., Lambda cold start) to provide fail-fast behavior without impacting per-request performance.

The feature also introduces Local Secondary Index (LSI) support to the library, enhancing the metadata model to distinguish between GSIs and LSIs for accurate schema validation and future tooling (e.g., CDK construct generation).

## Architecture

The schema validation feature follows a layered architecture:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Generated Table Class                         │
│  (e.g., UsersTable.ValidateSchemaAsync(client, options?))       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   SchemaValidator                                │
│  - Orchestrates validation                                       │
│  - Compares EntityMetadata with DescribeTable response          │
│  - Produces SchemaValidationResult                              │
└─────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ KeyValidator    │ │ IndexValidator  │ │ TtlValidator    │
│ - PK/SK names   │ │ - GSI/LSI       │ │ - TTL attribute │
│ - PK/SK types   │ │ - Projections   │ │ - TTL enabled   │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

## Components and Interfaces

### New Types

#### SchemaValidationResult

```csharp
namespace Oproto.FluentDynamoDb.Validation;

/// <summary>
/// Result of schema validation containing errors and warnings.
/// </summary>
public class SchemaValidationResult
{
    /// <summary>
    /// Gets whether the validation passed (no errors).
    /// </summary>
    public bool IsValid => Errors.Count == 0;
    
    /// <summary>
    /// Gets the collection of validation errors (critical mismatches).
    /// </summary>
    public IReadOnlyList<SchemaValidationError> Errors { get; }
    
    /// <summary>
    /// Gets the collection of validation warnings (non-critical differences).
    /// </summary>
    public IReadOnlyList<SchemaValidationWarning> Warnings { get; }
    
    /// <summary>
    /// Throws SchemaValidationException if there are any errors.
    /// </summary>
    public void ThrowOnError();
    
    /// <summary>
    /// Logs all errors and warnings using the provided logger.
    /// </summary>
    public void LogResults(IDynamoDbLogger logger);
}
```

#### SchemaValidationError / SchemaValidationWarning

```csharp
/// <summary>
/// Represents a critical schema validation error.
/// </summary>
public class SchemaValidationError
{
    /// <summary>
    /// Gets the error code for programmatic handling.
    /// </summary>
    public SchemaValidationErrorCode Code { get; }
    
    /// <summary>
    /// Gets the element that has the mismatch (table name, index name, attribute name).
    /// </summary>
    public string Element { get; }
    
    /// <summary>
    /// Gets the expected value from entity metadata.
    /// </summary>
    public string Expected { get; }
    
    /// <summary>
    /// Gets the actual value from DynamoDB table.
    /// </summary>
    public string Actual { get; }
    
    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    public string Message { get; }
}

/// <summary>
/// Represents a non-critical schema validation warning.
/// </summary>
public class SchemaValidationWarning
{
    /// <summary>
    /// Gets the warning code for programmatic handling.
    /// </summary>
    public SchemaValidationWarningCode Code { get; }
    
    /// <summary>
    /// Gets the element that has the difference.
    /// </summary>
    public string Element { get; }
    
    /// <summary>
    /// Gets the human-readable warning message explaining why this may be acceptable.
    /// </summary>
    public string Message { get; }
}
```

#### SchemaValidationOptions

```csharp
/// <summary>
/// Options for schema validation behavior.
/// </summary>
public class SchemaValidationOptions
{
    /// <summary>
    /// Gets or sets the validation strictness level. Default is Relaxed.
    /// </summary>
    public ValidationStrictness Strictness { get; set; } = ValidationStrictness.Relaxed;
}

/// <summary>
/// Validation strictness levels.
/// </summary>
public enum ValidationStrictness
{
    /// <summary>
    /// Missing projection models for non-ALL indexes are warnings.
    /// </summary>
    Relaxed,
    
    /// <summary>
    /// Missing projection models for non-ALL indexes are errors.
    /// </summary>
    Strict
}
```

#### IndexType Enum

```csharp
namespace Oproto.FluentDynamoDb.Metadata;

/// <summary>
/// Type of DynamoDB secondary index.
/// </summary>
public enum IndexType
{
    /// <summary>
    /// Global Secondary Index - can have different partition and sort keys.
    /// </summary>
    GlobalSecondaryIndex,
    
    /// <summary>
    /// Local Secondary Index - shares partition key with base table.
    /// </summary>
    LocalSecondaryIndex
}
```

#### ProjectionType Enum

```csharp
namespace Oproto.FluentDynamoDb.Metadata;

/// <summary>
/// DynamoDB index projection type.
/// </summary>
public enum ProjectionType
{
    /// <summary>
    /// All attributes are projected into the index.
    /// </summary>
    All,
    
    /// <summary>
    /// Only key attributes are projected into the index.
    /// </summary>
    KeysOnly,
    
    /// <summary>
    /// Specific non-key attributes are projected into the index.
    /// </summary>
    Include
}
```

#### LocalSecondaryIndexAttribute

```csharp
namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks a property as the sort key for a Local Secondary Index (LSI).
/// LSIs share the same partition key as the base table.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class LocalSecondaryIndexAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the Local Secondary Index.
    /// </summary>
    public string IndexName { get; }
    
    /// <summary>
    /// Initializes a new instance of the LocalSecondaryIndexAttribute class.
    /// </summary>
    /// <param name="indexName">The name of the Local Secondary Index.</param>
    public LocalSecondaryIndexAttribute(string indexName)
    {
        IndexName = indexName;
    }
}
```

### Modified Types

#### IndexMetadata (Enhanced)

```csharp
namespace Oproto.FluentDynamoDb.Metadata;

public class IndexMetadata
{
    // Existing properties
    public string IndexName { get; set; } = string.Empty;
    public string PartitionKeyProperty { get; set; } = string.Empty;
    public string? SortKeyProperty { get; set; }
    public string[] ProjectedProperties { get; set; } = Array.Empty<string>();
    public string? KeyFormat { get; set; }
    
    // New properties
    /// <summary>
    /// Gets or sets the type of index (GSI or LSI).
    /// </summary>
    public IndexType IndexType { get; set; } = IndexType.GlobalSecondaryIndex;
    
    /// <summary>
    /// Gets or sets the DynamoDB attribute name for the partition key.
    /// </summary>
    public string PartitionKeyAttributeName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the DynamoDB attribute name for the sort key.
    /// </summary>
    public string? SortKeyAttributeName { get; set; }
    
    /// <summary>
    /// Gets or sets the expected attribute type for the partition key (S, N, B).
    /// </summary>
    public string PartitionKeyAttributeType { get; set; } = "S";
    
    /// <summary>
    /// Gets or sets the expected attribute type for the sort key (S, N, B).
    /// </summary>
    public string? SortKeyAttributeType { get; set; }
    
    /// <summary>
    /// Gets or sets the projection type for this index.
    /// </summary>
    public ProjectionType ProjectionType { get; set; } = ProjectionType.All;
    
    /// <summary>
    /// Gets or sets whether a projection model is defined for this index.
    /// </summary>
    public bool HasProjectionModel { get; set; }
}
```

#### EntityMetadata (Enhanced)

```csharp
namespace Oproto.FluentDynamoDb.Metadata;

public class EntityMetadata
{
    // Existing properties remain unchanged
    
    // New properties
    /// <summary>
    /// Gets or sets the DynamoDB attribute name for the partition key.
    /// </summary>
    public string PartitionKeyAttributeName { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the expected attribute type for the partition key (S, N, B).
    /// </summary>
    public string PartitionKeyAttributeType { get; set; } = "S";
    
    /// <summary>
    /// Gets or sets the DynamoDB attribute name for the sort key.
    /// </summary>
    public string? SortKeyAttributeName { get; set; }
    
    /// <summary>
    /// Gets or sets the expected attribute type for the sort key (S, N, B).
    /// </summary>
    public string? SortKeyAttributeType { get; set; }
    
    /// <summary>
    /// Gets or sets the TTL attribute name if TTL is configured.
    /// </summary>
    public string? TtlAttributeName { get; set; }
}
```

### Generated Code

The source generator will generate a `ValidateSchemaAsync` method on each table class:

```csharp
// Generated in UsersTable.g.cs
public partial class UsersTable
{
    /// <summary>
    /// Validates that the DynamoDB table schema matches the entity metadata.
    /// </summary>
    /// <param name="client">The DynamoDB client to use for DescribeTable.</param>
    /// <param name="options">Optional validation options.</param>
    /// <returns>The validation result containing any errors and warnings.</returns>
    public static async Task<SchemaValidationResult> ValidateSchemaAsync(
        IAmazonDynamoDB client,
        SchemaValidationOptions? options = null)
    {
        var validator = new SchemaValidator();
        return await validator.ValidateAsync(
            client,
            "users",  // Table name
            User.GetEntityMetadata(),
            options ?? new SchemaValidationOptions());
    }
}
```

## Data Models

### Error and Warning Codes

```csharp
public enum SchemaValidationErrorCode
{
    // Primary Key Errors
    PartitionKeyNameMismatch,
    PartitionKeyTypeMismatch,
    SortKeyMissing,
    SortKeyUnexpected,
    SortKeyNameMismatch,
    SortKeyTypeMismatch,
    
    // GSI Errors
    GsiNotFound,
    GsiPartitionKeyNameMismatch,
    GsiPartitionKeyTypeMismatch,
    GsiSortKeyMismatch,
    
    // LSI Errors
    LsiNotFound,
    LsiSortKeyNameMismatch,
    LsiSortKeyTypeMismatch,
    
    // TTL Errors
    TtlNotEnabled,
    TtlAttributeNameMismatch,
    
    // Projection Errors (Strict mode)
    ProjectionModelRequired
}

public enum SchemaValidationWarningCode
{
    // Extra items in DynamoDB
    UnexpectedGsi,
    UnexpectedLsi,
    UnexpectedTtl,
    
    // Projection warnings (Relaxed mode)
    ProjectionModelRecommended
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Matching schemas produce valid results
*For any* entity metadata and DynamoDB table description that have identical primary keys, indexes, and TTL configuration, the validation result SHALL have `IsValid = true` and zero errors.
**Validates: Requirements 1.3**

### Property 2: Primary key mismatches produce errors
*For any* entity metadata and DynamoDB table description where the partition key name, partition key type, sort key name, sort key type, or sort key presence differs, the validation result SHALL contain at least one error identifying the mismatch.
**Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6**

### Property 3: Missing GSIs produce errors
*For any* entity metadata defining a GSI that does not exist in the DynamoDB table description, the validation result SHALL contain an error with code `GsiNotFound`.
**Validates: Requirements 3.1**

### Property 4: GSI key mismatches produce errors
*For any* GSI where the partition key name, partition key type, sort key name, sort key type, or sort key presence differs between entity metadata and DynamoDB table description, the validation result SHALL contain at least one error identifying the mismatch.
**Validates: Requirements 3.2, 3.3, 3.4**

### Property 5: Missing LSIs produce errors
*For any* entity metadata defining an LSI that does not exist in the DynamoDB table description, the validation result SHALL contain an error with code `LsiNotFound`.
**Validates: Requirements 4.2**

### Property 6: LSI key mismatches produce errors
*For any* LSI where the sort key name or sort key type differs between entity metadata and DynamoDB table description, the validation result SHALL contain at least one error identifying the mismatch.
**Validates: Requirements 4.3, 4.4**

### Property 7: TTL mismatches produce errors
*For any* entity metadata defining a TTL attribute where the DynamoDB table either has TTL disabled or has a different TTL attribute name, the validation result SHALL contain at least one error.
**Validates: Requirements 5.1, 5.2**

### Property 8: Extra DynamoDB items produce warnings
*For any* DynamoDB table description containing GSIs, LSIs, or TTL configuration not defined in the entity metadata, the validation result SHALL contain warnings (not errors) for each extra item.
**Validates: Requirements 3.5, 4.5, 5.3**

### Property 9: Strictness controls projection model enforcement
*For any* index with projection type KEYS_ONLY or INCLUDE without a defined projection model, the validation result SHALL contain an error when strictness is Strict, and a warning when strictness is Relaxed.
**Validates: Requirements 6.1, 6.3, 6.4**

### Property 10: Error messages contain required information
*For any* validation error, the error message SHALL contain the expected value, actual value, and element identification (table name, index name, or attribute name).
**Validates: Requirements 8.1, 8.2, 8.3**

### Property 11: ThrowOnError throws only when errors exist
*For any* validation result, calling `ThrowOnError()` SHALL throw `SchemaValidationException` if and only if `IsValid = false`.
**Validates: Requirements 9.2, 9.3**

### Property 12: IndexType correctly identifies GSI vs LSI
*For any* entity with `[GlobalSecondaryIndex]` attributes, the generated IndexMetadata SHALL have `IndexType = GlobalSecondaryIndex`. *For any* entity with `[LocalSecondaryIndex]` attributes, the generated IndexMetadata SHALL have `IndexType = LocalSecondaryIndex`.
**Validates: Requirements 7.1, 7.2**

### Property 13: Default projection type is ALL
*For any* index without a defined projection model, the generated IndexMetadata SHALL have `ProjectionType = All`.
**Validates: Requirements 10.3**

## Error Handling

### Exception Types

```csharp
namespace Oproto.FluentDynamoDb.Validation;

/// <summary>
/// Exception thrown when schema validation fails and ThrowOnError() is called.
/// </summary>
public class SchemaValidationException : Exception
{
    /// <summary>
    /// Gets the validation result containing all errors and warnings.
    /// </summary>
    public SchemaValidationResult ValidationResult { get; }
    
    public SchemaValidationException(SchemaValidationResult result)
        : base($"Schema validation failed with {result.Errors.Count} error(s)")
    {
        ValidationResult = result;
    }
}
```

### Error Scenarios

| Scenario | Behavior |
|----------|----------|
| DescribeTable fails (table doesn't exist) | Throws `ResourceNotFoundException` from AWS SDK |
| DescribeTable fails (permissions) | Throws `AccessDeniedException` from AWS SDK |
| Validation finds errors | Returns result with `IsValid = false`, no exception |
| ThrowOnError called with errors | Throws `SchemaValidationException` |
| ThrowOnError called without errors | No exception, returns normally |

## Testing Strategy

### Dual Testing Approach

The implementation will use both unit tests and property-based tests:

1. **Unit Tests**: Verify specific examples and edge cases
2. **Property-Based Tests**: Verify universal properties across generated inputs

### Property-Based Testing Framework

The implementation will use **FsCheck** for property-based testing, consistent with existing tests in the codebase (e.g., `CellCoveringPropertyTests.cs`, `SpatialQueryPropertyTests.cs`).

### Test Structure

```csharp
// Property tests will be in:
// Oproto.FluentDynamoDb.UnitTests/Validation/SchemaValidationPropertyTests.cs

// Unit tests will be in:
// Oproto.FluentDynamoDb.UnitTests/Validation/SchemaValidatorTests.cs

// Source generator tests will be in:
// Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/SchemaValidationGeneratorTests.cs
```

### Test Generators

Custom FsCheck generators will be created for:
- `EntityMetadata` with valid key configurations
- Mock `DescribeTableResponse` objects
- Index configurations (GSI/LSI)
- TTL configurations

### Property Test Annotations

Each property-based test will be annotated with the correctness property it validates:

```csharp
/// <summary>
/// **Feature: schema-validation, Property 2: Primary key mismatches produce errors**
/// </summary>
[Property]
public Property PrimaryKeyMismatch_ProducesError()
{
    // ...
}
```
