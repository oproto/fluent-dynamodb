# List Index API Consistency and Dynamic Index Support

## Overview

This specification addresses API consistency issues in the list index operations and adds support for dynamic (non-constant) indices in lambda expressions. Currently, `SetAt` and `RemoveAt` are builder methods while other list operations (`Append`, `Prepend`, `AppendRange`, `PrependRange`) are expression extension methods. Additionally, list indices must be constant integers, preventing common patterns like using loop variables or method results.

## Problem Statement

### API Inconsistency

The current API has inconsistent patterns for list operations:

**Expression Extension Methods (Consistent Pattern):**
```csharp
// These use extension methods on the list property
.Set(x => x.Tags.Append("new"))
.Set(x => x.Tags.Prepend("priority"))
.Set(x => x.Tags.AppendRange(new[] { "a", "b" }))
```

**Builder Methods (Inconsistent Pattern):**
```csharp
// These use separate builder methods
.SetAt(x => x.Tags[0], "updated")
.RemoveAt(x => x.Tags[2])
```

This inconsistency makes the API harder to learn and use.

### Dynamic Index Limitation

Currently, list indices must be constant integers:

```csharp
// ✅ Works - constant index
.WithFilter(x => x.Tags[0] == "featured")

// ❌ Throws UnsupportedExpressionException
int index = GetIndex();
.WithFilter(x => x.Tags[index] == "featured")

// ❌ Throws UnsupportedExpressionException
.WithFilter(x => x.Tags[GetIndex()] == "featured")
```

This prevents common patterns where the index is determined at runtime.

---

## User Stories

### Story 1: Consistent List Update API

**As a** developer using FluentDynamoDb  
**I want to** use consistent expression extension methods for all list operations  
**So that** the API is intuitive and easy to learn

#### Acceptance Criteria

1. **AC1.1**: Support `SetAt` as an expression extension method
   ```csharp
   // New pattern - consistent with Append/Prepend
   await table.Items.Update(itemId)
       .Set(x => x.Tags.SetAt(0, "updated"))
       .UpdateAsync();
   // Generates: SET #tags[0] = :v0
   ```

2. **AC1.2**: Support `RemoveAt` as an expression extension method
   ```csharp
   // New pattern - consistent with other list operations
   await table.Items.Update(itemId)
       .Set(x => x.Tags.RemoveAt(2))
       .UpdateAsync();
   // Generates: REMOVE #tags[2]
   ```

3. **AC1.3**: Nested list operations work with new extension methods
   ```csharp
   await table.Items.Update(itemId)
       .Set(x => x.Metadata.Keywords.SetAt(0, "updated"))
       .UpdateAsync();
   // Generates: SET #metadata.#keywords[0] = :v0
   
   await table.Items.Update(itemId)
       .Set(x => x.Metadata.Keywords.RemoveAt(1))
       .UpdateAsync();
   // Generates: REMOVE #metadata.#keywords[1]
   ```

4. **AC1.4**: Remove old builder methods (SetAt/RemoveAt on builder)
   ```csharp
   // These should be REMOVED - replaced by extension methods
   .SetAt(x => x.Tags[0], "updated")  // Remove
   .RemoveAt(x => x.Tags[2])          // Remove
   ```

5. **AC1.5**: Extension methods throw InvalidOperationException if called directly
   ```csharp
   // Should throw - these are expression-only methods
   var list = new List<string> { "a", "b" };
   list.SetAt(0, "c");  // Throws InvalidOperationException
   list.RemoveAt(1);    // Throws InvalidOperationException
   ```

---

### Story 2: Dynamic Index Support in Filter/Condition Expressions

**As a** developer using FluentDynamoDb  
**I want to** use variables and method results as list indices  
**So that** I can write dynamic queries without falling back to string expressions

#### Acceptance Criteria

1. **AC2.1**: Support local variable indices in filter expressions
   ```csharp
   int index = 0;
   var items = await table.Items.Query(x => x.Category == category)
       .WithFilter(x => x.Tags[index] == "featured")
       .ToListAsync();
   // Generates: #tags[0] = :v0 (index evaluated at translation time)
   ```

2. **AC2.2**: Support method call indices in filter expressions
   ```csharp
   var items = await table.Items.Query(x => x.Category == category)
       .WithFilter(x => x.Tags[GetPrimaryTagIndex()] == "featured")
       .ToListAsync();
   // Generates: #tags[N] = :v0 (where N is result of GetPrimaryTagIndex())
   ```

3. **AC2.3**: Support property access indices in filter expressions
   ```csharp
   var config = GetConfig();
   var items = await table.Items.Query(x => x.Category == category)
       .WithFilter(x => x.Tags[config.PrimaryIndex] == "featured")
       .ToListAsync();
   // Generates: #tags[N] = :v0 (where N is config.PrimaryIndex)
   ```

4. **AC2.4**: Reject indices that reference the entity parameter
   ```csharp
   // Should throw UnsupportedExpressionException
   .WithFilter(x => x.Tags[x.PrimaryIndex] == "featured")
   // Error: "List index cannot reference the entity parameter"
   ```

5. **AC2.5**: Support dynamic indices in condition expressions
   ```csharp
   int index = 1;
   await table.Items.Put(item)
       .Where(x => x.Tags[index] == "expected")
       .PutAsync();
   ```

---

### Story 3: Dynamic Index Support in Update Expressions

**As a** developer using FluentDynamoDb  
**I want to** use variables as list indices in update operations  
**So that** I can update list elements at runtime-determined positions

#### Acceptance Criteria

1. **AC3.1**: Support variable indices in SetAt extension method
   ```csharp
   int index = GetIndexToUpdate();
   await table.Items.Update(itemId)
       .Set(x => x.Tags.SetAt(index, "updated"))
       .UpdateAsync();
   // Generates: SET #tags[N] = :v0 (where N is evaluated index)
   ```

2. **AC3.2**: Support variable indices in RemoveAt extension method
   ```csharp
   int index = GetIndexToRemove();
   await table.Items.Update(itemId)
       .Set(x => x.Tags.RemoveAt(index))
       .UpdateAsync();
   // Generates: REMOVE #tags[N]
   ```

3. **AC3.3**: Support method call indices in update expressions
   ```csharp
   await table.Items.Update(itemId)
       .Set(x => x.Tags.SetAt(GetTargetIndex(), "updated"))
       .UpdateAsync();
   ```

4. **AC3.4**: Support property access indices in update expressions
   ```csharp
   var config = GetConfig();
   await table.Items.Update(itemId)
       .Set(x => x.Tags.SetAt(config.TargetIndex, "updated"))
       .UpdateAsync();
   ```

5. **AC3.5**: Reject indices that reference the entity parameter
   ```csharp
   // Should throw UnsupportedExpressionException
   .Set(x => x.Tags.SetAt(x.LastIndex, "updated"))
   // Error: "List index cannot reference the entity parameter"
   ```

6. **AC3.6**: Validate index is non-negative at translation time
   ```csharp
   int index = -1;
   // Should throw ArgumentOutOfRangeException
   .Set(x => x.Tags.SetAt(index, "updated"))
   // Error: "List index must be non-negative. Got: -1"
   ```

---

### Story 4: Documentation Updates

**As a** developer learning FluentDynamoDb  
**I want to** have accurate documentation for list operations  
**So that** I can use the API correctly

#### Acceptance Criteria

1. **AC4.1**: Update `.kiro/steering/fluentdynamodb.md` with:
   - New `SetAt` and `RemoveAt` extension method patterns
   - Remove old builder method patterns
   - Dynamic index examples

2. **AC4.2**: Update `docs/core-features/MapsAndLists.md` with:
   - Corrected API patterns using extension methods
   - Remove references to old builder methods
   - Dynamic index support documentation
   - Examples of variable, method call, and property access indices

3. **AC4.3**: Update `CHANGELOG.md` with new features

4. **AC4.4**: Truncate `docs/DOCUMENTATION_CHANGELOG.md` to fresh state (external sources synchronized)

---

## Technical Design Notes

### New Extension Methods

```csharp
namespace Oproto.FluentDynamoDb.Expressions;

public static class ListOperationExtensions
{
    // Existing methods...
    
    /// <summary>
    /// Sets the value at a specific index in a list.
    /// Translates to: SET #attr[index] = :val
    /// </summary>
    [ExpressionOnly]
    public static List<T> SetAt<T>(this List<T> list, int index, T value)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions.");

    /// <summary>
    /// Removes the element at a specific index from a list.
    /// Translates to: REMOVE #attr[index]
    /// </summary>
    [ExpressionOnly]
    public static List<T> RemoveAt<T>(this List<T> list, int index)
        => throw new InvalidOperationException(
            "This method is only for use in update expressions.");
}
```

### Index Evaluation

Dynamic indices are evaluated at expression translation time using the existing `EvaluateConstantExpression<T>` pattern:

1. Check if index expression references entity parameter → throw if yes
2. Evaluate index expression to get integer value
3. Validate index is non-negative
4. Use evaluated value in document path

---

## Out of Scope

1. **Negative indices** (Python-style from-end indexing) - Not supported by DynamoDB
2. **Range indices** (slicing) - Not supported by DynamoDB

---

## DynamoDB Limitations

### Overlapping Document Paths

DynamoDB does NOT allow multiple operations on overlapping document paths in a single update expression. This means:

```csharp
// ✅ ALLOWED - different indices on same list
.Set(x => x.Tags.SetAt(0, "a").SetAt(1, "b"))
// Generates: SET #tags[0] = :v0, #tags[1] = :v1

// ❌ NOT ALLOWED - list_append overlaps with index access
.Set(x => x.Tags.SetAt(0, "a").Append("new"))
// Would generate: SET #tags[0] = :v0, #tags = list_append(#tags, :v1)
// DynamoDB error: "Two document paths overlap with each other"

// ❌ NOT ALLOWED - RemoveAt overlaps with SetAt on same list
.Set(x => x.Tags.SetAt(0, "a").RemoveAt(1))
// Would generate: SET #tags[0] = :v0 REMOVE #tags[1]
// DynamoDB error: "Two document paths overlap with each other"
```

**Design Decision**: We will NOT support chaining of list operations that would result in overlapping paths. The extension methods will return `List<T>` for C# syntax compatibility, but the translator will throw `UnsupportedExpressionException` if chained operations would create overlapping paths.

**Allowed Chaining**:
- Multiple `SetAt` calls with different indices: `x.Tags.SetAt(0, "a").SetAt(1, "b")` ✅

**Disallowed Chaining**:
- `SetAt` + `Append`: overlapping paths
- `SetAt` + `RemoveAt`: overlapping paths (SET + REMOVE on same attribute)
- `Append` + `RemoveAt`: overlapping paths
- Any combination that mixes index operations with whole-list operations

---

## Dependencies

- Existing `ListOperationExtensions` class
- Existing `ExpressionTranslator` infrastructure
- Existing `UpdateExpressionTranslator` infrastructure

---

## Testing Requirements

1. **Unit tests** for new extension method translation
2. **Unit tests** for dynamic index evaluation
3. **Unit tests** for index validation (non-negative, no entity reference)
4. **Integration tests** for end-to-end dynamic index operations
5. **Backward compatibility tests** for existing builder methods
