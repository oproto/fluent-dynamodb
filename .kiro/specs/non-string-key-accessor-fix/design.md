# Non-String Key Accessor Fix - Bugfix Design

## Overview

The source generator produces uncompilable code when entity key properties (partition key or sort key) have non-string .NET types (e.g., enums like `SnsSubscriptionTopic`, numeric types like `int`, `DateTime`, `Guid`) and do NOT have a prefix or computed configuration. The generated accessor methods (Get, Delete, Update, ConditionCheck) pass the raw non-string value to `.WithKey()` which only accepts `string` or `AttributeValue` overloads. The fix introduces a decision branch in the code generation: when a key is non-string with no prefix and not computed, the generator will emit `.SetKey(k => { ... })` with inline `AttributeValue` construction using the same serialization logic as `MapperGenerator.GetToAttributeValueExpression`.

## Glossary

- **Bug_Condition (C)**: A key property has a non-string .NET type AND has no prefix (`KeyFormat?.Prefix` is null/empty) AND is not computed (`IsComputed == false`)
- **Property (P)**: The generated code should use `.SetKey(k => { ... })` with proper `AttributeValue` construction instead of `.WithKey()` with raw non-string values
- **Preservation**: All entities with string keys, prefixed keys, or computed keys must continue to generate identical code using `.WithKey()` as they do today
- **TableGenerator**: The class in `Oproto.FluentDynamoDb.SourceGenerator/Generators/TableGenerator.cs` that generates entity accessor methods and table-level key overloads
- **MapperGenerator.GetToAttributeValueExpression**: Static method that returns the correct C# expression to construct an `AttributeValue` from a given `PropertyModel` and value expression
- **WithKey**: Extension methods on `IWithKey<T>` that accept `string` or `AttributeValue` parameters for key specification
- **SetKey**: Method on `IWithKey<T>` that accepts `Action<Dictionary<string, AttributeValue>>` for direct key dictionary manipulation
- **PropertyModel**: Model representing a source-analyzed property with `PropertyType`, `KeyFormat`, `IsComputed`, `Format`, `DateTimeKind`
- **KeyFormatModel**: Model with `Prefix` and `Separator` that determines key formatting

## Bug Details

### Bug Condition

The bug manifests when a key property (partition key or sort key) is defined with a non-string .NET type and has no prefix and is not computed. The `TableGenerator` methods (`GenerateAccessorGetMethod`, `GenerateAccessorUpdateMethod`, `GenerateAccessorDeleteMethod`, `GenerateAccessorConditionCheckMethod`, `GenerateSingleKeyOverloads`, `GenerateCompositeKeyOverloads`) all emit `.WithKey("attributeName", paramName)` or `.WithKey("pkName", pkParam, "skName", skParam)` where the value parameters are non-string types, causing CS1503 compilation errors because `WithKeyExtensions` only has overloads for `(string, string)`, `(string, string, string, string)`, and `(string, AttributeValue, string?, AttributeValue?)`.

**Formal Specification:**
```
FUNCTION isBugCondition(keyProperty)
  INPUT: keyProperty of type PropertyModel (a partition key or sort key)
  OUTPUT: boolean
  
  LET baseType = GetCSharpType(keyProperty.PropertyType)
  LET isStringType = baseType IN ["string", "System.String"]
  LET hasPrefix = keyProperty.KeyFormat != null AND NOT String.IsNullOrEmpty(keyProperty.KeyFormat.Prefix)
  LET isComputed = keyProperty.IsComputed
  
  RETURN NOT isStringType
         AND NOT hasPrefix
         AND NOT isComputed
END FUNCTION
```

### Examples

- **Enum sort key (compilation failure)**: Entity `UserSubscription` with `[SortKey] SnsSubscriptionTopic Topic` generates `Get(string pK, SnsSubscriptionTopic sK) => _table.Get<UserSubscription>().WithKey("PK", pK, "SK", sK);` — fails because `sK` is an enum, not a string
- **Int partition key (compilation failure)**: Entity with `[PartitionKey] int UserId` generates `Get(int userId) => _table.Get<Entity>().WithKey("pk", userId);` — fails because `userId` is an int, not a string
- **Guid partition key (compilation failure)**: Entity with `[PartitionKey] Guid Id` generates `Get(Guid id) => _table.Get<Entity>().WithKey("id", id);` — fails because `id` is a Guid, not a string
- **DateTime sort key (compilation failure)**: Entity with `[SortKey] DateTime CreatedAt` generates composite `.WithKey("PK", pk, "SK", createdAt)` — fails because `createdAt` is DateTime
- **String key with prefix (works correctly today)**: Entity with `[PartitionKey(Prefix = "USER")] string Pk` generates `Get(string pK) => ... .WithKey("pk", pK);` — works because parameter is string (caller passes the prefixed value)
- **String key no prefix (works correctly today)**: Entity with `[PartitionKey] string Id` generates `Get(string id) => ... .WithKey("id", id);` — works because parameter is string

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- All entities with `string` typed keys (with or without prefix) must continue to generate `.WithKey()` calls with string parameters exactly as today
- All entities with prefixed keys (regardless of underlying .NET type) must continue to generate `string` parameter types using `.WithKey()` because the caller supplies the fully-formed prefixed string value
- All entities with computed keys (`[Computed(...)]`) must continue to generate `string` parameter types using `.WithKey()` because computed keys are always string-typed in the generated code
- Express-route async methods (GetAsync, DeleteAsync, UpdateAsync) must continue to delegate to their builder counterparts without change
- FluentResults methods (GetAsyncResult, DeleteAsyncResult) must continue to delegate correctly
- XML documentation comments on accessor methods must remain accurate

**Scope:**
All inputs where the key property is of type `string`, has a non-empty prefix, or is computed should produce completely identical generated code. This includes:
- String partition keys with or without prefix
- String sort keys with or without prefix
- Computed keys (always string)
- All existing entities that currently compile successfully

## Hypothesized Root Cause

Based on the source code analysis, the root cause is straightforward:

1. **Missing type check in code generation**: The six affected methods in `TableGenerator.cs` unconditionally emit `.WithKey("attrName", paramValue)` for all key properties. They use `GetCSharpType(property.PropertyType)` to determine the parameter type (correctly producing the native .NET type), but then pass that parameter directly to `.WithKey()` which only accepts `string` values. There is no conditional branch to handle the case where the parameter type is not `string`.

2. **No alternative code path exists**: The generator has no logic to detect whether a key is non-string and emit `.SetKey(k => { ... })` instead. The `IWithKey<T>.SetKey(Action<Dictionary<string, AttributeValue>>)` method exists on all builders and is what `WithKey` itself delegates to, but the generator never directly emits `SetKey` calls.

3. **GetToAttributeValueExpression is available but unused by TableGenerator**: `MapperGenerator.GetToAttributeValueExpression` already knows how to convert any `PropertyModel` type to the correct `AttributeValue` construction expression, but `TableGenerator` does not reference it. The fix needs to either call this method (if accessible) or replicate its logic for key types.

4. **Update method uses builder.WithKey() directly (not extension)**: The `GenerateAccessorUpdateMethod` calls `builder.WithKey(...)` directly on the builder instance (not as an expression-bodied member via extension method), but the same type mismatch applies since the builder's `WithKey` is the same extension method.

## Correctness Properties

Property 1: Bug Condition - Non-String Key SetKey Generation

_For any_ entity where at least one key property (partition key or sort key) satisfies the bug condition (non-string type, no prefix, not computed), the fixed generator SHALL emit accessor methods that use `.SetKey(k => { k["attributeName"] = <AttributeValue expression>; })` with the correct `AttributeValue` construction for that type, producing compilable C# code.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

Property 2: Preservation - String/Prefixed/Computed Key Behavior

_For any_ entity where all key properties are either string-typed, have a non-empty prefix, or are computed, the fixed generator SHALL produce exactly the same generated code as the original generator, preserving `.WithKey()` usage with string parameters unchanged.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/TableGenerator.cs`

**Functions**: `GenerateAccessorGetMethod`, `GenerateAccessorUpdateMethod`, `GenerateAccessorDeleteMethod`, `GenerateAccessorConditionCheckMethod`, `GenerateSingleKeyOverloads`, `GenerateCompositeKeyOverloads`

**Specific Changes**:

1. **Add a helper method `NeedsSetKeyApproach(PropertyModel key)`**: Create a private static method that returns `true` when the key's C# type is not `string` AND has no prefix AND is not computed. This encapsulates the bug condition check.

2. **Add a helper method to generate AttributeValue expression**: Either make `MapperGenerator.GetToAttributeValueExpression` accessible to `TableGenerator` (change visibility to `internal`) or create a simplified version in `TableGenerator` that handles the types relevant to key properties (string, int, long, enum, DateTime, Guid, etc.).

3. **Modify single-key accessor generation**: In each of the six methods, when the single partition key satisfies `NeedsSetKeyApproach`, emit:
   ```
   .SetKey(k => { k["attributeName"] = <AttributeValue expression for pkParam>; })
   ```
   instead of `.WithKey("attributeName", pkParam)`.

4. **Modify composite-key accessor generation**: When either or both keys satisfy `NeedsSetKeyApproach`, emit:
   ```
   .SetKey(k => { k["pkAttr"] = <AV expr for pk>; k["skAttr"] = <AV expr for sk>; })
   ```
   instead of `.WithKey("pkAttr", pkParam, "skAttr", skParam)`. Note: if one key is string (no prefix, not computed) and the other is non-string, both must go through `SetKey` since we can't mix approaches in a single call.

5. **Handle Update method's builder pattern**: The Update accessor uses `builder.WithKey(...)` in a multi-statement body (not expression-bodied). Change this to `builder.SetKey(k => { ... })` when the bug condition applies.

6. **Ensure correct `using` for AttributeValue**: The generated code already emits `using Amazon.DynamoDBv2.Model;` in its file header, so `AttributeValue` is available.

### Decision Logic Summary

```
IF key.PropertyType is "string" OR key.KeyFormat?.Prefix is not null/empty OR key.IsComputed:
    → Use existing .WithKey() approach (string parameter)
ELSE:
    → Use .SetKey(k => { k["attr"] = new AttributeValue { ... }; }) approach (native type parameter)
```

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code (compilation errors), then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write source generator unit tests that define entities with non-string key types (enum, int, Guid, DateTime) without prefixes, run the generator, and inspect the generated code for compilation errors. Run these tests on the UNFIXED code to observe failures.

**Test Cases**:
1. **Enum Sort Key Test**: Define entity with `[SortKey] SnsSubscriptionTopic Topic` (no prefix) → verify generated code calls `.WithKey()` with non-string value (will fail to compile on unfixed code)
2. **Int Partition Key Test**: Define entity with `[PartitionKey] int UserId` (no prefix) → verify generated code passes int to `.WithKey()` (will fail to compile on unfixed code)
3. **Guid Partition Key Test**: Define entity with `[PartitionKey] Guid Id` (no prefix) → verify generated code passes Guid to `.WithKey()` (will fail to compile on unfixed code)
4. **Mixed Key Types Test**: Define entity with string PK (prefixed) and enum SK (no prefix) → verify composite `.WithKey()` call has type mismatch (will fail to compile on unfixed code)

**Expected Counterexamples**:
- Generated C# code that passes non-string typed parameters to `.WithKey()` methods which only accept `string` or `AttributeValue`
- CS1503 compilation errors when attempting to build the generated code

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL entity WHERE any key satisfies isBugCondition(key) DO
  generatedCode := TableGenerator_fixed.Generate(entity)
  ASSERT generatedCode contains ".SetKey(k =>"
  ASSERT generatedCode contains correct AttributeValue construction for key type
  ASSERT generatedCode compiles successfully
  ASSERT generated SetKey lambda sets correct attribute names
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL entity WHERE NO key satisfies isBugCondition(key) DO
  ASSERT TableGenerator_original.Generate(entity) == TableGenerator_fixed.Generate(entity)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many entity configurations (varying key types, prefixes, computed flags) automatically
- It catches edge cases like nullable types, custom format strings, and DateTimeKind that manual tests might miss
- It provides strong guarantees that behavior is unchanged for all entities with string/prefixed/computed keys

**Test Plan**: Capture generated output from UNFIXED code for entities with string keys, prefixed keys, and computed keys. Then verify the FIXED code produces byte-for-byte identical output for those same entities.

**Test Cases**:
1. **String Key Preservation**: Verify entities with `string` typed keys (no prefix) produce identical generated code before and after fix
2. **Prefixed Key Preservation**: Verify entities with prefixed keys (any underlying type) produce identical generated code before and after fix
3. **Computed Key Preservation**: Verify entities with computed keys produce identical generated code before and after fix
4. **Multi-Entity Table Preservation**: Verify tables with multiple entities (some with string keys) still generate correctly

### Unit Tests

- Test `NeedsSetKeyApproach` helper returns `true` for non-string, no-prefix, non-computed keys
- Test `NeedsSetKeyApproach` helper returns `false` for string keys, prefixed keys, and computed keys
- Test generated `SetKey` lambda contains correct `AttributeValue` construction for: int, long, enum, DateTime, Guid, bool, DateOnly, TimeOnly
- Test generated `SetKey` lambda respects `Format` property (custom date formats)
- Test generated `SetKey` lambda respects `DateTimeKind` property
- Test composite key where both keys are non-string
- Test composite key where one key is string and the other is non-string (both go through SetKey)
- Test single key that is non-string
- Test Update method body-style generation with SetKey

### Property-Based Tests

- Generate random `PropertyModel` configurations varying type, prefix, computed flag → verify decision logic always produces compilable code
- Generate random entity configurations with composite keys → verify either `.WithKey()` or `.SetKey()` is emitted correctly based on key properties
- Generate entities with all supported non-string types (int, long, enum, DateTime, Guid, bool, etc.) → verify correct `AttributeValue` construction expression for each

### Integration Tests

- Define a complete entity with non-string keys, run the full source generator pipeline, and verify the generated file compiles
- Define a multi-entity table where one entity has string keys and another has non-string keys, verify both generate correctly
- Build the test project with entities exercising the fixed code path and verify no compilation errors
- Execute a Get/Update/Delete/ConditionCheck against DynamoDB LocalStack with non-string key entities to verify runtime correctness
