# Design Document: Lambda Expression API Gaps

## Overview

This feature addresses missing lambda expression support in FluentDynamoDb request builders and entity accessors. Currently, lambda expressions work for Query and Update operations but are missing from Put, Delete, and Scan operations on entity accessors. This creates an inconsistent API where some operations support type-safe lambda conditions while others require format strings.

The goal is to provide consistent lambda expression support across all DynamoDB operations, enabling developers to write type-safe conditions like:
- `table.Orders.Put(order).Where(x => x.Pk.AttributeNotExists())`
- `table.Orders.Delete(pk, sk).Where(x => x.Pk.AttributeExists())`
- `table.Orders.Scan().WithFilter(x => x.Status == "active")`

## Architecture

The fix involves four areas:

1. **Source Generator (TableGenerator.cs)**: Add missing `GenerateAccessorScanMethods` implementation
2. **Extension Methods (WithConditionExpressionExtensions.cs)**: Add lambda `Where()` overloads for `PutItemRequestBuilder<TEntity>` and `DeleteItemRequestBuilder<TEntity>`
3. **Validation**: Ensure the new extension methods use `ExpressionValidationMode.None` (not `KeysOnly`) since condition expressions can reference any property
4. **Scan Opt-In Pattern**: Remove generic `Scan<TEntity>()` from `DynamoDbTableBase` and enforce opt-in via `[Scannable]` attribute

### Scan Opt-In Pattern

Scan operations are expensive and not a recommended DynamoDB access pattern. To prevent accidental table scans, the library enforces an opt-in pattern:

- **Before**: `table.Scan<Order>()` was always available via `DynamoDbTableBase`
- **After**: Scan is only available when the entity has `[Scannable]` attribute

This change:
1. Removes the generic `Scan<TEntity>()` method from `DynamoDbTableBase`
2. Generates `Scan()` methods only for entities marked with `[Scannable]`
3. Requires developers to explicitly opt-in to scanning by adding `[Scannable]` to their entity

**Usage after change:**
```csharp
// Entity must have [Scannable] attribute
[DynamoDbEntity]
[DynamoDbTable("Orders")]
[Scannable]  // Required for Scan operations
public partial class Order : IDynamoDbEntity { ... }

// Then Scan is available via entity accessor or table (if default entity)
await table.Orders.Scan().WithFilter(x => x.Status == "active").ToListAsync();
await table.Scan().WithFilter(x => x.Status == "active").ToListAsync(); // If Order is default entity
```

```
Oproto.FluentDynamoDb/
├── Requests/
│   └── Extensions/
│       └── WithConditionExpressionExtensions.cs  # Add Put/Delete lambda Where()
└── ...

Oproto.FluentDynamoDb.SourceGenerator/
└── Generators/
    └── TableGenerator.cs  # Add GenerateAccessorScanMethods()
```

## Components and Interfaces

### 1. Extension Methods for PutItemRequestBuilder

Add to `WithConditionExpressionExtensions.cs`:

```csharp
/// <summary>
/// Specifies the condition expression using a C# lambda expression for PutItemRequestBuilder.
/// </summary>
public static PutItemRequestBuilder<TEntity> Where<TEntity>(
    this PutItemRequestBuilder<TEntity> builder,
    Expression<Func<TEntity, bool>> expression,
    EntityMetadata? metadata = null)
    where TEntity : class, IEntityMetadataProvider
{
    metadata ??= MetadataResolver.GetEntityMetadata<TEntity>();
    
    var context = new ExpressionContext(
        builder.GetAttributeValueHelper(),
        builder.GetAttributeNameHelper(),
        metadata,
        ExpressionValidationMode.None); // Condition expressions can reference any property

    var translator = new ExpressionTranslator(builder.GetOptions());
    var expressionString = translator.Translate(expression, context);

    return builder.SetConditionExpression(expressionString);
}
```

### 2. Extension Methods for DeleteItemRequestBuilder

Add to `WithConditionExpressionExtensions.cs`:

```csharp
/// <summary>
/// Specifies the condition expression using a C# lambda expression for DeleteItemRequestBuilder.
/// </summary>
public static DeleteItemRequestBuilder<TEntity> Where<TEntity>(
    this DeleteItemRequestBuilder<TEntity> builder,
    Expression<Func<TEntity, bool>> expression,
    EntityMetadata? metadata = null)
    where TEntity : class, IEntityMetadataProvider
{
    metadata ??= MetadataResolver.GetEntityMetadata<TEntity>();
    
    var context = new ExpressionContext(
        builder.GetAttributeValueHelper(),
        builder.GetAttributeNameHelper(),
        metadata,
        ExpressionValidationMode.None); // Condition expressions can reference any property

    var translator = new ExpressionTranslator(builder.GetOptions());
    var expressionString = translator.Translate(expression, context);

    return builder.SetConditionExpression(expressionString);
}
```

### 3. Source Generator - Scan Methods for Entity Accessors

Add `GenerateAccessorScanMethods` to `TableGenerator.cs`:

```csharp
/// <summary>
/// Generates Scan methods for an entity accessor.
/// </summary>
private static void GenerateAccessorScanMethods(StringBuilder sb, EntityModel entity, string modifier)
{
    // Parameterless Scan() method
    sb.AppendLine($"        /// <summary>");
    sb.AppendLine($"        /// Creates a new Scan operation builder for {entity.ClassName}.");
    sb.AppendLine($"        /// </summary>");
    sb.AppendLine($"        /// <returns>A ScanRequestBuilder&lt;{entity.ClassName}&gt; configured for this table.</returns>");
    sb.AppendLine($"        {modifier} ScanRequestBuilder<{entity.ClassName}> Scan() =>");
    sb.AppendLine($"            _table.Scan<{entity.ClassName}>();");
    sb.AppendLine();
    
    // Expression-based Scan(string, params object[]) method
    sb.AppendLine($"        /// <summary>");
    sb.AppendLine($"        /// Creates a new Scan operation builder with a filter expression.");
    sb.AppendLine($"        /// </summary>");
    sb.AppendLine($"        /// <param name=\"filterExpression\">The filter expression with format placeholders.</param>");
    sb.AppendLine($"        /// <param name=\"values\">The values to substitute into the expression.</param>");
    sb.AppendLine($"        /// <returns>A ScanRequestBuilder&lt;{entity.ClassName}&gt; configured with the filter.</returns>");
    sb.AppendLine($"        {modifier} ScanRequestBuilder<{entity.ClassName}> Scan(string filterExpression, params object[] values) =>");
    sb.AppendLine($"            _table.Scan<{entity.ClassName}>().WithFilter(filterExpression, values);");
    sb.AppendLine();
    
    // LINQ expression Scan(Expression<Func<TEntity, bool>>) method
    sb.AppendLine($"        /// <summary>");
    sb.AppendLine($"        /// Creates a new Scan operation builder with a LINQ expression for the filter.");
    sb.AppendLine($"        /// </summary>");
    sb.AppendLine($"        /// <param name=\"filterCondition\">The LINQ expression representing the filter condition.</param>");
    sb.AppendLine($"        /// <returns>A ScanRequestBuilder&lt;{entity.ClassName}&gt; configured with the filter.</returns>");
    sb.AppendLine($"        {modifier} ScanRequestBuilder<{entity.ClassName}> Scan(Expression<Func<{entity.ClassName}, bool>> filterCondition)");
    sb.AppendLine($"        {{");
    sb.AppendLine($"            return Scan().WithFilter(filterCondition);");
    sb.AppendLine($"        }}");
    sb.AppendLine();
}
```

## Data Models

No new data models are required. The existing `EntityMetadata`, `ExpressionContext`, and `ExpressionTranslator` classes are reused.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: AttributeExists generates correct expression
*For any* entity property, when `x.Property.AttributeExists()` is used in a Where lambda on Put or Delete builders, the System SHALL generate `attribute_exists(attributeName)` where attributeName is the DynamoDB attribute name for that property.

**Validates: Requirements 2.3**

### Property 2: AttributeNotExists generates correct expression
*For any* entity property, when `x.Property.AttributeNotExists()` is used in a Where lambda on Put or Delete builders, the System SHALL generate `attribute_not_exists(attributeName)` where attributeName is the DynamoDB attribute name for that property.

**Validates: Requirements 2.4**

### Property 3: Comparison operators generate correct expressions
*For any* entity property and comparison value, when comparison operators (`==`, `!=`, `<`, `>`, `<=`, `>=`) are used in a Where lambda on Put or Delete builders, the System SHALL generate the equivalent DynamoDB comparison expression.

**Validates: Requirements 3.3**

### Property 4: Generic and entity accessor methods produce identical results
*For any* condition expression, the result of `table.Put<TEntity>().Where(expression)` SHALL be identical to `table.Entitys.Put().Where(expression)` in terms of the generated DynamoDB request.

**Validates: Requirements 5.1, 5.2**

## Error Handling

- If an unsupported expression is used in a lambda, throw `UnsupportedExpressionException` with a clear message
- If a property doesn't map to a DynamoDB attribute, throw `UnmappedPropertyException`
- If the entity doesn't implement `IEntityMetadataProvider`, throw a compile-time error (enforced by generic constraint)

## Testing Strategy

### Unit Tests

1. **Compilation Tests**: Verify that the sample code from TransactionWriteSamples.cs compiles after the fix
2. **Extension Method Tests**: Verify `Where()` extension methods exist on `PutItemRequestBuilder<T>` and `DeleteItemRequestBuilder<T>`
3. **Entity Accessor Tests**: Verify `Scan()` method exists on entity accessors

### Property-Based Tests

**Property-Based Testing Library**: FsCheck with xUnit integration

**Property 1 Implementation**: AttributeExists expression generation
```csharp
// Feature: lambda-expression-api-gaps, Property 1: AttributeExists generates correct expression
// Validates: Requirements 2.3
[Property]
public Property AttributeExistsGeneratesCorrectExpression()
{
    // For any property name, verify attribute_exists() is generated correctly
}
```

**Property 2 Implementation**: AttributeNotExists expression generation
```csharp
// Feature: lambda-expression-api-gaps, Property 2: AttributeNotExists generates correct expression
// Validates: Requirements 2.4
[Property]
public Property AttributeNotExistsGeneratesCorrectExpression()
{
    // For any property name, verify attribute_not_exists() is generated correctly
}
```

### Test Configuration
- Property tests run minimum 100 iterations
- Tests tagged with feature and property references per design requirements
