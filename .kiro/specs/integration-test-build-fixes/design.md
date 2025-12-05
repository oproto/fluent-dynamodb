# Design Document

## Overview

This design addresses remaining compilation errors in the integration test project after initial fixes reduced errors from 500+ to 404. The integration tests fell behind during refactorings and need to be brought up to date with API changes, renamed methods, and new patterns. The approach focuses on updating test code to use current library APIs correctly, not modifying the library to match old test expectations.

**Progress Update**: Initial tasks (1-7) reduced errors significantly. Remaining errors fall into new categories that require additional fixes.

## Architecture

### Error Classification System

**Remaining 404 errors** after initial fixes fall into these categories:

1. **Generated Code Issues** (~120 errors): Missing table accessor properties (Orders: 86, OrderLines: 26, Payments: 2, Item: 8)
   - Source generator not producing expected accessors for test tables
   - Multi-entity tables missing entity accessor properties
   
2. **Method Not Found Issues** (~60 errors): Methods don't exist on builders
   - PutAsync: 34 errors - wrong builder type or method doesn't exist
   - Where: 6 errors - ScanRequestBuilder missing Where method
   - BatchGet/BatchWrite/TransactGet/TransactWrite: 8 errors - missing on DynamoDbTableBase
   - ReturnValues: 2 errors - missing on DeleteItemRequestBuilder
   
3. **Type Inference Issues** (~60 errors): Generic type parameters cannot be inferred
   - CS0411: 30 errors - Scan<TEntity>() cannot infer type
   - CS0305: 30 errors - ScanRequestBuilder requires type argument
   
4. **Lambda/Type Conversion Issues** (~72 errors): Wrong parameter types
   - CS1660: 32 errors - Cannot convert lambda to string
   - CS1503: 40 errors - Wrong argument types (AttributeValue vs string, EntityMetadata vs object)
   
5. **Method Signature Issues** (~48 errors): Wrong number/type of parameters
   - CS7036: 26 errors - Missing required parameters
   - CS1501: 16 errors - No overload matches
   - CS0119: 6 errors - Method used as property
   
6. **Type Resolution Issues** (~26 errors): Types not found
   - CS0246: 26 errors - ProductEntity and other types not found
   
7. **FluentAssertions Issues** (~4 errors): String comparison methods
   - BeGreaterThanOrEqualTo/BeLessThanOrEqualTo not available for strings
   
8. **Operator Issues** (~2 errors): Invalid operators
   - CS0019: String >= comparison not supported
   
9. **Miscellaneous** (~12 errors): Various other issues
   - NoDeletes, MixedEntities, InternalOps, InternalEntities properties missing

### Fix Strategy Priority

**Phase 2 Fixes** (after completing tasks 1-7):

1. **Critical Priority - Source Generator Issues** (120 errors): Fix missing table accessors
   - Highest impact on error count
   - May be root cause for cascading errors
   - Must investigate why generator isn't producing expected output
   
2. **High Priority - Type Inference** (60 errors): Add explicit type parameters
   - Straightforward fixes with clear patterns
   - Quick wins to reduce error count
   
3. **High Priority - Method Not Found** (60 errors): Fix incorrect method calls
   - Requires understanding current API
   - May reveal API changes that affect multiple tests
   
4. **Medium Priority - Type Conversions** (72 errors): Fix parameter type mismatches
   - Requires understanding method signatures
   - May indicate API changes in how methods accept parameters
   
5. **Medium Priority - Method Signatures** (48 errors): Fix parameter counts and types
   - Similar to type conversions but more about overload selection
   
6. **Low Priority - Type Resolution** (26 errors): Find missing types
   - May be test-specific types that were removed
   - Could be quick fixes if types just moved
   
7. **Low Priority - Assertions & Operators** (6 errors): Fix test assertions
   - Test-specific issues, not library API issues
   
8. **Low Priority - Miscellaneous** (12 errors): Fix remaining property issues
   - Likely test infrastructure issues

## Components and Interfaces

### 1. Source Generator Verification Component

**Purpose**: Ensure the source generator produces all expected table accessor properties

**Key Files**:
- `Oproto.FluentDynamoDb.SourceGenerator/Generators/TableGenerator.cs`
- Integration test table definitions in `TestEntities/`

**Design Decisions**:
- Verify generator is triggered for all test entity types
- Check that entity attributes are correctly recognized
- Ensure accessor property naming matches test expectations
- Validate that multi-entity tables generate all accessors

**Interface**:
```csharp
// Expected generated output for test tables
public partial class MultiEntityTestTable
{
    public EntityAccessor<MultiEntityOrderTestEntity> Orders { get; }
    public EntityAccessor<MultiEntityOrderLineTestEntity> OrderLines { get; }
}

public partial class TransactionTestTable
{
    public EntityAccessor<TransactionOrderTestEntity> Orders { get; }
    public EntityAccessor<TransactionOrderLineTestEntity> OrderLines { get; }
    public EntityAccessor<TransactionPaymentTestEntity> Payments { get; }
}
```

### 2. Request Builder API Discovery Component

**Purpose**: Discover current API patterns in the library and update tests to match

**Key Files**:
- `Oproto.FluentDynamoDb/Requests/TransactWriteItemsRequestBuilder.cs`
- `Oproto.FluentDynamoDb/Requests/TransactGetItemsRequestBuilder.cs`
- `Oproto.FluentDynamoDb/Requests/BatchWriteItemRequestBuilder.cs`
- `Oproto.FluentDynamoDb/Requests/BatchGetItemRequestBuilder.cs`
- `Oproto.FluentDynamoDb/Storage/DynamoDbTableBase.cs`
- Unit tests showing correct usage patterns

**Design Decisions**:
- Review current library code to find correct method names and signatures
- Check unit tests for examples of correct API usage
- Update integration test code to use current API patterns
- Replace old method calls (AddPut, AddGet, etc.) with current equivalents

**Discovery Process**:
1. Examine current TransactWriteItemsRequestBuilder for available methods
2. Check how unit tests build transaction operations
3. Update integration tests to match current patterns
4. Repeat for batch operations, scan, and other operations

### 3. Operation Context Resolution Component

**Purpose**: Replace EncryptionContext references with DynamoDbOperationContext pattern

**Key Files**:
- Integration test files in `Security/` folder
- `Oproto.FluentDynamoDb/Storage/DynamoDbOperationContext.cs`
- Unit test examples showing diagnostic adapter pattern for xUnit

**Design Decisions**:
- EncryptionContext was replaced by DynamoDbOperationContext
- Use diagnostic adapter pattern for xUnit compatibility (AsyncLocal issues)
- Follow patterns from existing unit tests that successfully use operation context
- Update all EncryptionContext references to use new pattern

**Resolution Strategy**:
1. Replace `EncryptionContext` with `DynamoDbOperationContext`
2. Implement diagnostic adapter pattern from unit tests for xUnit compatibility
3. Update test setup to use operation context correctly
4. Ensure encryption-related tests use proper context flow

### 4. Type Inference Resolution Component

**Purpose**: Add explicit type parameters where the library requires them

**Key Files**:
- Integration test files with type inference errors
- `Oproto.FluentDynamoDb/Storage/DynamoDbTableBase.cs`
- `Oproto.FluentDynamoDb/Requests/*RequestBuilder.cs`

**Design Decisions**:
- Review current library API to understand which methods require explicit type parameters
- Update test code to provide explicit type parameters where needed
- Follow patterns from unit tests that successfully use these APIs
- Do not modify library code - only update test code

**Fix Pattern**:
```csharp
// Before (incorrect - missing type parameter)
var result = await table.Query()
    .Where("pk = :pk")
    .ExecuteAsync();

// After (correct - explicit type parameter)
var result = await table.Query<MyEntity>()
    .Where("pk = :pk")
    .ExecuteAsync();
```

### 5. Lambda Expression Compatibility Component

**Purpose**: Ensure lambda expressions match expected delegate types

**Key Files**:
- `Oproto.FluentDynamoDb.IntegrationTests/RealWorld/SensitiveDataRedactionIntegrationTests.cs`
- `Oproto.FluentDynamoDb.IntegrationTests/RealWorld/FormatApplicationIntegrationTests.cs`

**Design Decisions**:
- Identify methods that changed from string to Expression<Func<T, bool>>
- Update test code to use correct syntax
- Ensure expression-based APIs are consistent

**Fix Pattern**:
```csharp
// Before (incorrect - lambda where string expected)
.Where(x => x.Status == "Active")

// After (correct - string expression)
.Where("Status = :status")
.WithValue(":status", "Active")

// Or if expression-based API exists
.Where(x => x.Status == "Active")  // With proper Expression<Func<T, bool>> parameter
```

### 6. Test Infrastructure Component

**Purpose**: Update test infrastructure to match current library APIs

**Key Files**:
- `Oproto.FluentDynamoDb.IntegrationTests/Infrastructure/MockFieldEncryptor.cs`
- Test files using FluentAssertions

**Design Decisions**:
- Add missing `EncryptCalls` property to MockFieldEncryptor
- Update FluentAssertions method calls to match installed version
- Ensure test table classes expose expected properties

**Interface**:
```csharp
public class MockFieldEncryptor : IFieldEncryptor
{
    public List<EncryptCall> EncryptCalls { get; } = new();
    
    // Existing interface implementation
    public Task<string> EncryptAsync(string plaintext, FieldEncryptionContext context);
    public Task<string> DecryptAsync(string ciphertext, FieldEncryptionContext context);
}

// FluentAssertions update
// Old: .Should().BeGreaterOrEqualTo(1)
// New: .Should().BeGreaterThanOrEqualTo(1)
```

### 7. Method Signature Consistency Component

**Purpose**: Ensure all method signatures match how tests invoke them

**Key Files**:
- `Oproto.FluentDynamoDb/Storage/DynamoDbTableBase.cs`
- Various request builder classes

**Design Decisions**:
- Verify Update is a method, not a property
- Ensure Put/Get/Delete have correct parameter counts
- Check that all method overloads exist as expected

**Verification**:
```csharp
// Update must be a method
public UpdateItemRequestBuilder<TEntity> Update<TEntity>() where TEntity : class;

// Put with entity parameter
public PutItemRequestBuilder<TEntity> Put<TEntity>(TEntity entity) where TEntity : class;

// Get with no parameters (keys specified via fluent API)
public GetItemRequestBuilder<TEntity> Get<TEntity>() where TEntity : class;
```

## Data Models

### Error Categorization Model

```csharp
public enum ErrorCategory
{
    GeneratedCode,      // Missing table accessors
    ApiMethod,          // Missing request builder methods
    TypeResolution,     // EncryptionContext not found
    TypeInference,      // Cannot infer generic types
    LambdaExpression,   // Lambda to string conversion
    TestInfrastructure, // Mock/assertion issues
    MethodSignature     // Parameter count/type mismatches
}

public class CompilationError
{
    public string FilePath { get; set; }
    public int LineNumber { get; set; }
    public string ErrorCode { get; set; }
    public string Message { get; set; }
    public ErrorCategory Category { get; set; }
    public string SuggestedFix { get; set; }
}
```

## Error Handling

### Compilation Error Recovery

1. **Incremental Fixing**: Fix errors by category, starting with root causes
2. **Verification After Each Category**: Rebuild after fixing each category to verify progress
3. **Regression Prevention**: Run unit tests after each fix to ensure no breakage
4. **Documentation**: Document any intentional API changes that require test updates

### Fix Validation Strategy

1. Fix source generator issues first (affects ~150 errors)
2. Add missing API methods (affects ~100 errors)
3. Resolve type resolution issues (affects ~50 errors)
4. Address type inference (affects ~40 errors)
5. Fix lambda expressions (affects ~30 errors)
6. Update test infrastructure (affects ~20 errors)
7. Fix method signatures (affects ~14 errors)

After each category, run:
```bash
dotnet build Oproto.FluentDynamoDb.IntegrationTests/Oproto.FluentDynamoDb.IntegrationTests.csproj
```

## Testing Strategy

### Verification Approach

1. **Compilation Success**: Primary goal is zero compilation errors
2. **Test Execution**: After compilation succeeds, run integration tests to verify functionality
3. **Regression Testing**: Ensure main library unit tests still pass
4. **API Compatibility**: Verify no breaking changes to public APIs

### Test Categories

1. **Build Verification**: `dotnet build` succeeds with zero errors
2. **Unit Test Verification**: `dotnet test Oproto.FluentDynamoDb.UnitTests`
3. **Integration Test Execution**: `dotnet test Oproto.FluentDynamoDb.IntegrationTests` (after build succeeds)

## Phase 2 Fix Patterns

### Pattern 1: Missing Table Accessors (Orders, OrderLines, etc.)

**Problem**: `table.Orders` produces CS1061 error - property not found

**Investigation Steps**:
1. Check if MultiEntityTestTable is marked with `[DynamoDbTable]` attribute
2. Check if entity classes have `[DynamoDbEntity]` attribute
3. Look at obj/Generated folder to see what's actually generated
4. Compare with working unit test tables

**Possible Solutions**:
- Fix entity/table attributes if misconfigured
- If generator pattern changed, use new accessor pattern (e.g., `table.GetAccessor<OrderEntity>()`)
- If accessors aren't generated, use direct table methods instead

### Pattern 2: PutAsync on Wrong Builder

**Problem**: `QueryRequestBuilder.PutAsync()` produces CS1061 error

**Root Cause**: Test code is calling PutAsync on a query builder, which doesn't make sense

**Solution**: Replace with correct method
```csharp
// Wrong
await table.Query().PutAsync(entity);

// Correct - separate operations
await table.Put(entity).PutAsync();
var results = await table.Query().ToListAsync();
```

### Pattern 3: Scan Type Inference

**Problem**: `table.Scan()` produces CS0411 - cannot infer type

**Solution**: Add explicit type parameter
```csharp
// Wrong
var result = await table.Scan()
    .FilterExpression("Status = :status")
    .ExecuteAsync();

// Correct
var result = await table.Scan<MyEntity>()
    .FilterExpression("Status = :status")
    .ExecuteAsync();
```

### Pattern 4: Lambda to String Conversion

**Problem**: Cannot convert lambda expression to type 'string'

**Solution**: Use string-based expression syntax
```csharp
// Wrong
.Where(x => x.Status == "Active")

// Correct
.Where("Status = :status")
.WithValue(":status", "Active")
```

### Pattern 5: AttributeValue to String Conversion

**Problem**: Cannot convert from AttributeValue to string

**Solution**: Extract string value from AttributeValue
```csharp
// Wrong
SomeMethod(partitionKey, sortKey, attributeValue);

// Correct
SomeMethod(partitionKey, sortKey, attributeValue.S);
```

### Pattern 6: Missing Required Parameters

**Problem**: CS7036 - Missing required parameter (often with Remove method)

**Solution**: Check method signature and add missing parameters
```csharp
// Wrong (old signature)
dictionary.Remove(key);

// Correct (new signature requires out parameter)
dictionary.Remove(key, out var value);
```

### Pattern 7: String Comparison Operators

**Problem**: Operator '>=' cannot be applied to strings

**Solution**: Use CompareTo method
```csharp
// Wrong
.Where(x => x.Name >= "M")

// Correct
.Where(x => x.Name.CompareTo("M") >= 0)
```

### Pattern 8: FluentAssertions String Comparisons

**Problem**: BeGreaterThanOrEqualTo not available for StringAssertions

**Solution**: Use string-specific comparison methods
```csharp
// Wrong
result.Should().BeGreaterThanOrEqualTo("M");

// Correct
result.CompareTo("M").Should().BeGreaterOrEqualTo(0);
// Or
result.Should().Match(s => s.CompareTo("M") >= 0);
```

## Implementation Notes

### Source Generator Debugging

If table accessors are not generated:
1. Check that entity classes have `[DynamoDbEntity]` attribute
2. Verify table classes have `[DynamoDbTable]` attribute
3. Ensure source generator is referenced in test project
4. Check generated files in `obj/Generated/` folder

### API Method Discovery

To find if a method exists but with different name:
1. Search for similar method names in request builder classes
2. Check git history for renamed methods
3. Look for extension methods that might provide functionality

### Operation Context Resolution

For EncryptionContext/DynamoDbOperationContext issues:
1. EncryptionContext was replaced by DynamoDbOperationContext
2. Use diagnostic adapter pattern for xUnit (AsyncLocal compatibility)
3. Reference unit tests for correct usage patterns
4. Ensure proper context flow in integration tests

## Performance Considerations

- Fixing errors by category minimizes rebuild time
- Source generator fixes have highest impact (150 errors)
- Test infrastructure fixes can be done in parallel with other fixes
- Lambda expression fixes are localized and quick to implement

## Security Considerations

- Ensure encryption tests still validate security properly after fixes
- Verify MockFieldEncryptor doesn't accidentally expose sensitive data
- Confirm field-level security tests compile and execute correctly

## Deployment Considerations

- All fixes should be in main library or test code, no deployment changes needed
- Integration tests should remain isolated from production code
- No changes to public API surface area (only additions, no breaking changes)
