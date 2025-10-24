# Design Document

## Overview

This design consolidates multiple API improvements into a cohesive refactoring of the FluentDynamoDb library. The changes focus on improving developer ergonomics, type safety, and feature completeness while maintaining AOT compatibility and performance. The design addresses type parameter verbosity, missing LINQ expression support, security concerns in logging, and comprehensive testing strategies.

The implementation will be phased to minimize risk and allow for iterative validation, with each phase building on the previous one.

## Architecture

### High-Level Component Changes

```
┌─────────────────────────────────────────────────────────────┐
│                    DynamoDbTableBase                        │
│  - Query<T>() → QueryRequestBuilder<T>                     │
│  - Scan<T>() → ScanRequestBuilder<T>                       │
│  - Get<T>(key) → GetItemRequestBuilder<T>                  │
│  - Update<T>(key) → UpdateItemRequestBuilder<T>            │
│  + Query<T>(Expression<Func<T,bool>>)                      │
│  + Scan<T>(Expression<Func<T,bool>>)                       │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│              Generic Request Builders                       │
│  QueryRequestBuilder<TEntity>                              │
│  - Where(...) → QueryRequestBuilder<TEntity>               │
│  - WithFilter(...) → QueryRequestBuilder<TEntity>          │
│  - ToListAsync() → Task<List<TEntity>>                     │
│  - ExecuteAsync() → Task<QueryResponse<TEntity>>           │
│                                                             │
│  ScanRequestBuilder<TEntity>                               │
│  GetItemRequestBuilder<TEntity>                            │
│  UpdateItemRequestBuilder<TEntity>                         │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│              Expression Translation Layer                   │
│  ExpressionTranslator                                      │
│  - Translate(Expression, EntityMetadata)                   │
│  - ApplyFormatting(PropertyMetadata, value)                │
│  - HandleEncryption(PropertyMetadata, value)               │
│  - RedactSensitiveData(PropertyMetadata, value)            │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                  Source Generator                           │
│  - Generate Query<T>(Expression) overloads                 │
│  - Generate Scan<T>(Expression) overloads                  │
│  - Generate index Query<T>(Expression) overloads           │
│  - Emit metadata for format/encryption/sensitive           │
└─────────────────────────────────────────────────────────────┘
```

### Key Design Principles

1. **Type Parameter Flow**: Type parameters specified once at the entry point flow through the entire chain
2. **Expression Integration**: LINQ expressions integrate seamlessly with existing text and format string expressions
3. **Security by Default**: Sensitive data redaction happens automatically based on attributes
4. **Format Consistency**: Format specifications apply uniformly across all expression types
5. **Encryption Transparency**: Encrypted field queries work transparently when deterministic encryption is used

## Components and Interfaces

### 1. Generic Request Builders

All request builders will be made generic to carry the entity type through the fluent chain.

```csharp
// Before (non-generic)
public class QueryRequestBuilder
{
    public QueryRequestBuilder Where(string expression, params object[] values);
    public QueryRequestBuilder WithFilter(string expression, params object[] values);
}

// After (generic)
public class QueryRequestBuilder<TEntity> where TEntity : class
{
    private readonly EntityMetadata<TEntity> _metadata;
    
    public QueryRequestBuilder<TEntity> Where(string expression, params object[] values);
    public QueryRequestBuilder<TEntity> Where(Expression<Func<TEntity, bool>> predicate);
    public QueryRequestBuilder<TEntity> WithFilter(string expression, params object[] values);
    public QueryRequestBuilder<TEntity> WithFilter(Expression<Func<TEntity, bool>> predicate);
    
    public Task<QueryResponse<TEntity>> ExecuteAsync(CancellationToken ct = default);
    public Task<List<TEntity>> ToListAsync(CancellationToken ct = default);
}
```

### 2. Expression Translator Enhancement

The existing `ExpressionTranslator` will be enhanced to handle format strings, encryption, and sensitive data.

```csharp
public class ExpressionTranslator
{
    private readonly EntityMetadata _metadata;
    private readonly IFieldEncryptor? _encryptor;
    private readonly IDynamoDbLogger? _logger;
    
    public TranslationResult Translate(
        Expression expression,
        ExpressionContext context)
    {
        // Traverse expression tree
        // Apply formatting from DynamoDbAttribute
        // Handle encryption for encrypted fields
        // Redact sensitive values in logging
        // Generate DynamoDB expression syntax
    }
    
    private object ProcessValue(
        PropertyMetadata property,
        object value)
    {
        // Apply format string if specified
        if (property.Format != null)
        {
            value = ApplyFormat(value, property.Format);
        }
        
        // Encrypt if needed
        if (property.IsEncrypted && _encryptor != null)
        {
            value = _encryptor.Encrypt(value, property.EncryptionContext);
        }
        
        // Redact for logging if sensitive
        if (property.IsSensitive && _logger != null)
        {
            _logger.Debug("Property {PropertyName} value: {Value}", 
                property.Name, 
                "[REDACTED]");
        }
        
        return value;
    }
}
```

### 3. Source Generator Updates

The source generator will emit additional overloads and metadata.

```csharp
// Generated table class
public partial class TransactionTable : DynamoDbTableBase
{
    // Existing method-based API
    public QueryRequestBuilder<Transaction> Query() => 
        new QueryRequestBuilder<Transaction>(DynamoDbClient, this)
            .ForTable(Name);
    
    // NEW: LINQ expression overload
    public QueryRequestBuilder<Transaction> Query(
        Expression<Func<Transaction, bool>> keyCondition)
    {
        var builder = Query();
        return builder.Where(keyCondition);
    }
    
    // NEW: LINQ expression with filter
    public QueryRequestBuilder<Transaction> Query(
        Expression<Func<Transaction, bool>> keyCondition,
        Expression<Func<Transaction, bool>> filterCondition)
    {
        var builder = Query();
        return builder.Where(keyCondition).WithFilter(filterCondition);
    }
    
    // Similar for Scan
    public ScanRequestBuilder<Transaction> Scan() => 
        new ScanRequestBuilder<Transaction>(DynamoDbClient, this)
            .ForTable(Name);
    
    public ScanRequestBuilder<Transaction> Scan(
        Expression<Func<Transaction, bool>> filterCondition)
    {
        var builder = Scan();
        return builder.WithFilter(filterCondition);
    }
    
    // Index properties with LINQ support
    public TransactionGsi1Index Gsi1 => new TransactionGsi1Index(this);
}

// Generated index class
public class TransactionGsi1Index
{
    private readonly TransactionTable _table;
    
    public TransactionGsi1Index(TransactionTable table)
    {
        _table = table;
    }
    
    public QueryRequestBuilder<Transaction> Query() =>
        new QueryRequestBuilder<Transaction>(_table.DynamoDbClient, _table)
            .ForTable(_table.Name)
            .UsingIndex("Gsi1");
    
    // NEW: LINQ expression overloads
    public QueryRequestBuilder<Transaction> Query(
        Expression<Func<Transaction, bool>> keyCondition) =>
        Query().Where(keyCondition);
    
    public QueryRequestBuilder<Transaction> Query(
        Expression<Func<Transaction, bool>> keyCondition,
        Expression<Func<Transaction, bool>> filterCondition) =>
        Query().Where(keyCondition).WithFilter(filterCondition);
}
```

### 4. Property Metadata Enhancement

```csharp
public class PropertyMetadata
{
    public string PropertyName { get; init; }
    public string AttributeName { get; init; }
    public Type PropertyType { get; init; }
    
    // NEW: Format support
    public string? Format { get; init; }
    
    // NEW: Security flags
    public bool IsSensitive { get; init; }
    public bool IsEncrypted { get; init; }
    public EncryptionContext? EncryptionContext { get; init; }
    
    // Existing
    public bool IsComputed { get; init; }
    public bool IsExtracted { get; init; }
    public bool IsQueryable { get; init; }
}
```

### 5. DynamoDbAttribute Enhancement

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class DynamoDbAttributeAttribute : Attribute
{
    public string? AttributeName { get; set; }
    
    // NEW: Format specification
    public string? Format { get; set; }
    
    // Existing properties...
}

// Usage example
public class Transaction
{
    [DynamoDbAttribute(Format = "yyyy-MM-dd")]
    public DateTime CreatedDate { get; set; }
    
    [DynamoDbAttribute(Format = "F2")] // Two decimal places
    public decimal Amount { get; set; }
}
```

## Data Models

### Expression Translation Result

```csharp
public class TranslationResult
{
    public string Expression { get; init; }
    public Dictionary<string, string> AttributeNames { get; init; }
    public Dictionary<string, AttributeValue> AttributeValues { get; init; }
    public List<string> Warnings { get; init; }
}
```

### Expression Context

```csharp
public enum ExpressionContextType
{
    KeyCondition,    // Query WHERE clause
    FilterExpression, // Query/Scan filter
    ConditionExpression, // Put/Update/Delete condition
    UpdateExpression  // Update SET/REMOVE/ADD/DELETE
}

public class ExpressionContext
{
    public ExpressionContextType Type { get; init; }
    public EntityMetadata Metadata { get; init; }
    public IFieldEncryptor? Encryptor { get; init; }
    public IDynamoDbLogger? Logger { get; init; }
    public ExpressionValidationMode ValidationMode { get; init; }
}
```

## Error Handling

### Type Inference Errors

The compiler will naturally produce errors when type parameters are missing or incorrect:

```csharp
// Error: Cannot infer type
var builder = table.Query(); // Missing <T>

// Correct
var builder = table.Query<Transaction>();
```

### Expression Translation Errors

```csharp
public class ExpressionTranslationException : Exception
{
    public Expression OriginalExpression { get; }
    public string PropertyName { get; }
    public string Reason { get; }
}

// Example error messages:
// "Property 'InternalField' cannot be used in queries. It is not mapped to a DynamoDB attribute."
// "Property 'EncryptedField' cannot be used in equality comparisons because it uses non-deterministic encryption."
// "Expression contains unsupported method call 'String.ToUpper()'. Use simpler expressions or text-based queries."
```

### Encryption Errors

```csharp
// When querying encrypted fields with non-deterministic encryption
throw new ExpressionTranslationException(
    expression,
    "SensitiveData",
    "Cannot query encrypted field 'SensitiveData' because it uses non-deterministic encryption. " +
    "Only deterministic encryption supports equality queries.");
```

## Testing Strategy

### Phase 1: Type Parameter Simplification Tests

```csharp
[Fact]
public async Task Query_WithoutTypeParametersOnChain_InfersTypeCorrectly()
{
    // Arrange
    var table = new TransactionTable(client);
    
    // Act - No <Transaction> on Where, WithFilter, ToListAsync
    var results = await table.Query<Transaction>()
        .Where(x => x.PartitionKey == "PK#123")
        .WithFilter(x => x.Status == "ACTIVE")
        .ToListAsync();
    
    // Assert
    results.Should().BeOfType<List<Transaction>>();
}

[Fact]
public async Task Scan_WithoutTypeParametersOnChain_InfersTypeCorrectly()
{
    var results = await table.Scan<Transaction>()
        .WithFilter(x => x.Amount > 100)
        .ToListAsync();
    
    results.Should().BeOfType<List<Transaction>>();
}
```

### Phase 2: LINQ Expression Overload Tests

```csharp
[Fact]
public async Task Query_WithLinqExpression_ConfiguresKeyCondition()
{
    // Act
    var results = await table.Query<Transaction>(x => x.PartitionKey == "PK#123")
        .ToListAsync();
    
    // Assert - verify key condition was set correctly
}

[Fact]
public async Task Query_WithLinqExpressionAndFilter_ConfiguresBoth()
{
    var results = await table.Query<Transaction>(
        x => x.PartitionKey == "PK#123",
        x => x.Status == "ACTIVE")
        .ToListAsync();
}

[Fact]
public async Task IndexQuery_WithLinqExpression_SetsIndexName()
{
    var results = await table.Gsi1.Query<Transaction>(x => x.Gsi1Pk == "STATUS#ACTIVE")
        .ToListAsync();
    
    // Verify IndexName was set to "Gsi1"
}
```

### Phase 3: Sensitive Data Redaction Tests

```csharp
[Fact]
public void ExpressionTranslator_WithSensitiveProperty_RedactsInLogs()
{
    // Arrange
    var logger = new TestLogger();
    var translator = new ExpressionTranslator(metadata, null, logger);
    
    // Act
    translator.Translate(x => x.SensitiveField == "secret", context);
    
    // Assert
    logger.Messages.Should().Contain(m => m.Contains("[REDACTED]"));
    logger.Messages.Should().NotContain(m => m.Contains("secret"));
}
```

### Phase 4: Format String in LINQ Tests

```csharp
[Fact]
public async Task Query_WithFormattedDateProperty_AppliesFormat()
{
    // Arrange - CreatedDate has Format = "yyyy-MM-dd"
    var date = new DateTime(2024, 10, 24);
    
    // Act
    var results = await table.Query<Transaction>(x => x.CreatedDate == date)
        .ToListAsync();
    
    // Assert - verify DynamoDB received "2024-10-24" not full DateTime
}

[Fact]
public async Task Query_WithFormattedDecimal_AppliesFormat()
{
    // Arrange - Amount has Format = "F2"
    var amount = 123.456m;
    
    // Act
    var results = await table.Query<Transaction>(x => x.Amount == amount)
        .ToListAsync();
    
    // Assert - verify DynamoDB received "123.46"
}
```

### Phase 5: Encryption in Expression Tests

```csharp
[Fact]
public async Task Query_WithEncryptedField_EncryptsValue()
{
    // Arrange
    var encryptor = new MockFieldEncryptor();
    var table = new TransactionTable(client, encryptor: encryptor);
    
    // Act
    var results = await table.Query<Transaction>(x => x.EncryptedField == "value")
        .ToListAsync();
    
    // Assert
    encryptor.EncryptCalls.Should().Contain(c => c.Value == "value");
}

[Fact]
public void Query_WithNonDeterministicEncryption_ThrowsException()
{
    // Arrange - EncryptedField uses non-deterministic encryption
    
    // Act & Assert
    var act = () => table.Query<Transaction>(x => x.EncryptedField == "value");
    
    act.Should().Throw<ExpressionTranslationException>()
        .WithMessage("*non-deterministic encryption*");
}
```

### Phase 6: End-to-End Integration Tests

```csharp
[Collection("DynamoDbLocal")]
public class EndToEndExpressionTests
{
    [Theory]
    [InlineData("text")] // Text expression
    [InlineData("format")] // Format string expression
    [InlineData("linq")] // LINQ expression
    public async Task Query_AllExpressionModes_ReturnSameResults(string mode)
    {
        // Arrange - seed data
        await SeedTestData();
        
        // Act
        List<Transaction> results = mode switch
        {
            "text" => await table.Query<Transaction>()
                .Where("pk = :pk", new { pk = "PK#123" })
                .ToListAsync(),
            
            "format" => await table.Query<Transaction>()
                .Where("pk = {0}", "PK#123")
                .ToListAsync(),
            
            "linq" => await table.Query<Transaction>(x => x.PartitionKey == "PK#123")
                .ToListAsync(),
            
            _ => throw new ArgumentException()
        };
        
        // Assert
        results.Should().HaveCount(5);
        results.Should().AllSatisfy(t => t.PartitionKey.Should().Be("PK#123"));
    }
    
    [Fact]
    public async Task CompleteWorkflow_WithAllFeatures_WorksEndToEnd()
    {
        // Test: Query with LINQ, format, encryption, sensitive data, pagination
        var results = await table.Query<Transaction>(
                x => x.PartitionKey == "PK#123" && x.SortKey.StartsWith("TX#"))
            .WithFilter(x => x.Amount > 100 && x.Status == "ACTIVE")
            .Take(10)
            .ToListAsync();
        
        // Verify results
        // Verify sensitive data was redacted in logs
        // Verify encrypted fields were handled correctly
        // Verify formatting was applied
    }
}
```

### Phase 7: Documentation Tests

```csharp
// Verify all code examples in documentation compile and run
[Fact]
public void Documentation_CodeExamples_Compile()
{
    // Extract code examples from markdown
    // Compile them
    // Verify they produce expected results
}
```

## Implementation Phases

### Phase 1: Generic Request Builders (Breaking Change)
- Make all request builders generic
- Update method signatures to remove type parameters from chained methods
- Update source generator to emit generic builders
- Run full test suite to identify breaking changes

### Phase 2: LINQ Expression Overloads
- Add Query(Expression) and Scan(Expression) overloads to source generator
- Add Query(Expression) overloads to index generation
- Integrate with existing ExpressionTranslator
- Add tests for new overloads

### Phase 3: Sensitive Data Redaction in Expressions
- Enhance ExpressionTranslator to check IsSensitive flag
- Update logging calls to redact sensitive values
- Add tests for redaction behavior

### Phase 4: Format String Support in LINQ
- Add Format property to DynamoDbAttribute
- Update source generator to emit Format in metadata
- Enhance ExpressionTranslator to apply formatting
- Add tests for format application

### Phase 5: Encryption in Expressions
- Enhance ExpressionTranslator to handle encrypted fields
- Add validation for deterministic vs non-deterministic encryption
- Integrate with IFieldEncryptor interface
- Add tests for encryption behavior

### Phase 6: Index Improvements
- Remove QueryAsync methods from DynamoDbIndex<TDefault>
- Ensure index queries follow same patterns as table queries
- Add tests for index query behavior

### Phase 7: Discriminator Validation
- Review ToCompoundEntity implementation
- Add discriminator validation tests
- Document discriminator behavior

### Phase 8: Documentation Update
- Update all examples to use method-based API
- Add LINQ expression examples
- Update API reference
- Create migration guide

### Phase 9: End-to-End Testing
- Create comprehensive E2E test suite
- Test all operations with all expression modes
- Test pagination with all modes
- Test error scenarios

## Performance Considerations

### Type Parameter Inference
- No runtime overhead - all type inference happens at compile time
- IL code should be identical to explicit type parameters

### Expression Translation Caching
- Cache translated expressions by expression tree structure
- Use ConcurrentDictionary for thread-safe caching
- Cache key: expression tree hash + entity type

### Format String Application
- Pre-compile format strings where possible
- Cache formatted values for repeated queries

### Encryption Performance
- Encryption happens once per query value
- No additional overhead compared to manual encryption
- Deterministic encryption allows query result caching

## Security Considerations

### Sensitive Data Redaction
- Redaction happens at logging time, not at runtime
- Original values are still used in DynamoDB queries
- Redaction applies to all log levels

### Encryption Transparency
- Encrypted values are never logged (even redacted)
- Encryption context includes property name for auditability
- Non-deterministic encryption prevents equality queries (by design)

### Expression Injection Prevention
- All values are parameterized
- No string concatenation in expression building
- Expression trees are statically analyzed

## Migration Path

### For Existing Code

```csharp
// Before
var results = await table.Query<Transaction>()
    .Where<Transaction>("pk = {0}", "PK#123")
    .WithFilter<Transaction>("status = {0}", "ACTIVE")
    .ToListAsync<Transaction>();

// After (breaking change - remove type parameters)
var results = await table.Query<Transaction>()
    .Where("pk = {0}", "PK#123")
    .WithFilter("status = {0}", "ACTIVE")
    .ToListAsync();

// Or use new LINQ overload
var results = await table.Query<Transaction>(x => x.PartitionKey == "PK#123")
    .WithFilter(x => x.Status == "ACTIVE")
    .ToListAsync();

// Or use shorthand
var results = await table.Query<Transaction>(
    x => x.PartitionKey == "PK#123",
    x => x.Status == "ACTIVE")
    .ToListAsync();
```

### Automated Refactoring

Regex patterns for automated migration:
```regex
// Remove type parameters from Where
\.Where<(\w+)>\(
→ .Where(

// Remove type parameters from WithFilter
\.WithFilter<(\w+)>\(
→ .WithFilter(

// Remove type parameters from ToListAsync
\.ToListAsync<(\w+)>\(\)
→ .ToListAsync()

// Remove type parameters from ExecuteAsync
\.ExecuteAsync<(\w+)>\(\)
→ .ExecuteAsync()
```

## Open Questions

1. **Encryption Determinism**: Should we provide a way to mark encrypted fields as "deterministic" vs "non-deterministic" to enable/disable query support?
   - **Decision**: Add `EncryptionMode` property to `EncryptedAttribute` with values `Deterministic` and `NonDeterministic`

2. **Format String Validation**: Should format strings be validated at compile time or runtime?
   - **Decision**: Runtime validation with clear error messages. Compile-time validation would require Roslyn analyzers (future enhancement)

3. **Index Type Parameters**: Should indexes always require explicit type parameters or can we infer from context?
   - **Decision**: Require explicit type parameters on index Query() calls for clarity

4. **Backward Compatibility**: Should we provide obsolete overloads with warnings?
   - **Decision**: No obsolete overloads since codebase is greenfield. Clean break is acceptable.

5. **Expression Complexity Limits**: Should we limit expression complexity to prevent performance issues?
   - **Decision**: No hard limits initially. Monitor performance and add limits if needed.
