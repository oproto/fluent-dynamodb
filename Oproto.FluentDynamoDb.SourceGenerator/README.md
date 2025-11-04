# DynamoDB Source Generator Architecture

This document describes the architecture and design of the DynamoDB Source Generator.

## Overview

The DynamoDB Source Generator is a Roslyn-based code generator that analyzes entity classes decorated with DynamoDB attributes and generates optimized mapping code, field constants, and key builders. The generator runs at compile time and produces AOT-compatible code with zero runtime reflection.

## Core Components

### 1. EntityAnalyzer (`Analysis/EntityAnalyzer.cs`)

**Purpose**: Analyzes class declarations to extract DynamoDB entity information.

**Responsibilities**:
- Parses class declarations with `[DynamoDbTable]` attributes
- Extracts property information including keys, attributes, and relationships
- Validates entity configuration (partition key requirements, conflicting patterns)
- Reports diagnostic errors and warnings for configuration issues
- Produces `EntityModel` data structures for code generation

**Key Validations**:
- Ensures classes are marked as `partial`
- Verifies exactly one partition key exists
- Validates computed and extracted key patterns
- Detects circular dependencies in computed keys
- Checks for conflicting entity type patterns

### 2. MapperGenerator (`Generators/MapperGenerator.cs`)

**Purpose**: Generates entity mapping code for converting between C# objects and DynamoDB AttributeValue dictionaries.

**Responsibilities**:
- Generates `ToDynamoDb<TSelf>()` method for entity-to-DynamoDB conversion
- Generates `FromDynamoDb<TSelf>()` methods (single-item and multi-item overloads)
- Generates `GetPartitionKey()` method for extracting partition keys
- Generates `MatchesEntity()` method for entity type discrimination
- Generates `GetEntityMetadata()` method for future LINQ support

**Performance Optimizations**:

1. **Pre-allocated Dictionaries**
   ```csharp
   // Calculate exact capacity at compile time
   var item = new Dictionary<string, AttributeValue>(propertyCount);
   ```
   *Why*: Dictionary resizing is expensive. Pre-allocating with exact capacity eliminates this cost.

2. **Aggressive Inlining**
   ```csharp
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity)
   ```
   *Why*: Mapping is a hot path in DynamoDB operations. Inlining reduces call overhead.

3. **Direct Property Access**
   ```csharp
   item["pk"] = new AttributeValue { S = typedEntity.PartitionKey };
   ```
   *Why*: No reflection overhead at runtime. All property access is direct and type-safe.

4. **Efficient Type Conversions**
   - Optimized conversion logic for common types (string, int, decimal, DateTime, etc.)
   - Special handling for nullable types
   - Efficient enum conversions

**Generated Code Structure**:
```csharp
public partial class YourEntity
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity)
    {
        // Type check
        if (entity is not YourEntity typedEntity)
            throw new ArgumentException(...);
        
        // Pre-allocate with exact capacity
        var item = new Dictionary<string, AttributeValue>(propertyCount);
        
        // Direct property mappings
        item["pk"] = new AttributeValue { S = typedEntity.PartitionKey };
        // ... more mappings
        
        return item;
    }
    
    // Additional generated methods...
}
```

### 3. KeysGenerator (`Generators/KeysGenerator.cs`)

**Purpose**: Generates static key builder methods for DynamoDB entities as nested classes.

**Responsibilities**:
- Generates partition key and sort key builder methods
- Handles composite keys with multiple components, prefixes, and separators
- Generates separate key builders for each Global Secondary Index
- Creates extraction helper methods for composite keys
- Ensures type safety for all key builder parameters
- Generates Keys class as a nested static partial class within the entity

**Generated Code Structure**:
```csharp
public partial class YourEntity
{
    // Nested Keys class for better organization
    public static partial class Keys
    {
        // Main table keys
        public static string Pk(string tenantId, string customerId) 
            => $"{tenantId}#{customerId}";
        
        public static string Sk(DateTime date) 
            => date.ToString("yyyy-MM-dd");
        
        // GSI keys (nested without redundant suffix)
        public static partial class StatusIndex
        {
            public static string Pk(string status) => $"STATUS#{status}";
        }
        
        // Extraction helpers
        public static (string TenantId, string CustomerId) ExtractPkComponents(string pk)
        {
            var parts = pk.Split('#');
            return (parts[0], parts[1]);
        }
    }
}
```

**Access Pattern**:
```csharp
// Access keys through entity class
var pk = YourEntity.Keys.Pk("tenant1", "customer123");
var sk = YourEntity.Keys.Sk(DateTime.UtcNow);

// Access GSI keys without redundant suffix
var gsiPk = YourEntity.Keys.StatusIndex.Pk("ACTIVE");
```

### 4. FieldsGenerator (`Generators/FieldsGenerator.cs`)

**Purpose**: Generates static field name constant classes for DynamoDB entities as nested classes.

**Responsibilities**:
- Generates string constants for all DynamoDB attribute names
- Creates nested classes for Global Secondary Index fields
- Provides compile-time safety when referencing attribute names
- Handles reserved word mapping and special cases
- Generates Fields class as a nested static partial class within the entity

**Generated Code Structure**:
```csharp
public partial class YourEntity
{
    // Nested Fields class for better organization
    public static partial class Fields
    {
        // Main table fields
        public const string PartitionKey = "pk";
        public const string SortKey = "sk";
        public const string Amount = "amount";
        public const string Status = "status";
        
        // GSI fields (nested without redundant suffix)
        public static partial class StatusIndex
        {
            public const string Status = "status";
            public const string CreatedDate = "created_date";
        }
    }
}
```

**Access Pattern**:
```csharp
// Access fields through entity class
var pkField = YourEntity.Fields.PartitionKey;
var amountField = YourEntity.Fields.Amount;

// Access GSI fields without redundant suffix
var gsiStatusField = YourEntity.Fields.StatusIndex.Status;
```

## Code Generation Pipeline

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. Syntax Analysis                                              │
│    - Roslyn identifies classes with [DynamoDbTable] attributes  │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 2. Entity Analysis (EntityAnalyzer)                             │
│    - Parse class declaration and attributes                     │
│    - Extract property information                               │
│    - Validate configuration                                     │
│    - Create EntityModel                                         │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 3. Validation & Diagnostics                                     │
│    - Check for required attributes                              │
│    - Validate key patterns                                      │
│    - Report errors and warnings                                 │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 4. Code Generation (Consolidated)                               │
│    ┌──────────────────────────────────────────────────────────┐ │
│    │ MapperGenerator → YourEntity.g.cs                        │ │
│    │   - ToDynamoDb method                                    │ │
│    │   - FromDynamoDb methods (single & multi-item)           │ │
│    │   - GetPartitionKey method                               │ │
│    │   - MatchesEntity method                                 │ │
│    │   - GetEntityMetadata method                             │ │
│    │   - Nested Keys class (from KeysGenerator)               │ │
│    │     • Partition key builder                              │ │
│    │     • Sort key builder                                   │ │
│    │     • GSI key builders (nested without suffix)           │ │
│    │     • Extraction helpers                                 │ │
│    │   - Nested Fields class (from FieldsGenerator)           │ │
│    │     • Field name constants                               │ │
│    │     • GSI field constants (nested without suffix)        │ │
│    └──────────────────────────────────────────────────────────┘ │
│    ┌──────────────────────────────────────────────────────────┐ │
│    │ TableGenerator → YourTable.g.cs                          │ │
│    │   - Table class with index properties                    │ │
│    │   - Nested typed index classes (e.g., StatusIndexIndex)  │ │
│    │   - Query and scan methods with projection support       │ │
│    └──────────────────────────────────────────────────────────┘ │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│ 5. Compilation                                                  │
│    - Generated code compiled with user project                  │
│    - Full type safety and IntelliSense support                  │
│    - Nested classes provide better organization                 │
└─────────────────────────────────────────────────────────────────┘
```

## Design Principles

### Single Responsibility

Each component has a focused purpose:
- **EntityAnalyzer**: Parse and validate
- **MapperGenerator**: Generate mapping code
- **KeysGenerator**: Generate key builders
- **FieldsGenerator**: Generate field constants

This separation makes the codebase easy to understand, test, and extend.

### Performance First

All generated code is optimized for production use:
- Pre-allocated collections with exact capacity
- Aggressive inlining for hot paths
- Direct property access (no reflection)
- Efficient string operations
- Minimal memory allocations

### AOT Compatibility

The generator produces AOT-safe code:
- No runtime reflection
- All types resolved at compile time
- Trimmer-safe implementations
- Static abstract interface methods for generic constraints

### Maintainability

Clear architecture enables easy maintenance:
- No circular dependencies between components
- Each generator is self-contained
- Comprehensive XML documentation
- Diagnostic reporting for configuration issues

## Historical Context

### Consolidation (Tasks 40-44)

The current architecture is the result of a consolidation effort that simplified the codebase:

**Before**: Three separate implementations existed:
1. `MapperGenerator.cs` - delegated to OptimizedCodeGenerator
2. `OptimizedCodeGenerator.cs` - generated method bodies only
3. `AdvancedPerformanceOptimizations.cs` - complete alternative implementation

**After**: Single implementation in `MapperGenerator.cs`
- All code generation logic consolidated
- Performance optimizations integrated directly
- Clearer architecture and easier maintenance
- No delegation or multiple code paths

**Deleted Files** (Tasks 40-41):
- `Performance/OptimizedCodeGenerator.cs` - DELETED
- `Performance/AdvancedPerformanceOptimizations.cs` - DELETED

These files were removed because they created confusion about which implementation was being used and made the codebase harder to maintain. All performance optimizations were preserved and integrated into `MapperGenerator.cs`.

## Testing Strategy

### Unit Tests
- **EntityAnalyzer Tests**: Verify correct parsing and validation
- **Generator Tests**: Validate generated code syntax and correctness
- **Mapping Logic Tests**: Test entity to/from DynamoDB conversion
- **Error Handling Tests**: Verify appropriate error messages

### Integration Tests
- **End-to-End Scenarios**: Test complete workflows with real DynamoDB operations
- **Multi-Item Entity Tests**: Verify complex entity mapping scenarios
- **Related Entity Tests**: Test relationship mapping and filtering
- **Performance Tests**: Ensure generated code performs well

## Future Enhancements

### LINQ Expression Support

The attribute metadata design is comprehensive enough to support future LINQ-style query expressions:

```csharp
// Future LINQ support - not yet implemented
var activeTransactions = await table
    .Where(t => t.TenantId == tenantId && t.Status == TransactionStatus.Active)
    .Include(t => t.AuditEntries)
    .OrderByDescending(t => t.CreatedDate)
    .Take(50)
    .ToListAsync<TransactionEntry>();
```

The `GetEntityMetadata()` method provides the foundation for this by capturing:
- Property types and constraints
- Index relationships and projections
- Queryable operations per property
- Relationship metadata

## Contributing

When modifying the source generator:

1. **Maintain Single Responsibility**: Keep each component focused on its purpose
2. **Preserve Performance**: Don't remove optimizations without benchmarking
3. **Update Documentation**: Keep this README and inline docs current
4. **Add Tests**: Cover new functionality with unit and integration tests
5. **Consider AOT**: Ensure generated code remains AOT-compatible
6. **Nested Structure**: Generate support classes as nested classes within entities

## References

- [Roslyn Source Generators](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
- [Static Abstract Interface Members](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-11#generic-math-support)
- [Native AOT Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
