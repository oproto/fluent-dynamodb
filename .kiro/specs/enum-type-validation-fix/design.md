# Enum Type Validation Fix - Bugfix Design

## Overview

The `EntityAnalyzer.IsSupportedPropertyType()` method uses a hardcoded allowlist of known type names to validate property types. User-defined enum types are not in this list, causing a false DYNDB009 error that blocks compilation. Meanwhile, the downstream `MapperGenerator` already handles enum serialization/deserialization correctly via its `IsEnumType()` heuristic. The fix adds enum detection to the validator so it no longer rejects types the mapper already supports.

## Glossary

- **Bug_Condition (C)**: A property whose type is a user-defined enum (or nullable/collection of enum) triggers DYNDB009 during `EntityAnalyzer` validation
- **Property (P)**: Enum properties are accepted by the validator and flow through to the mapper for correct serialization
- **Preservation**: All existing type validation behavior for primitives, collections, complex types, and genuinely unsupported types remains unchanged
- **IsSupportedPropertyType**: The method in `EntityAnalyzer.cs` (line 1678) that validates property types against a hardcoded allowlist
- **IsEnumType**: The heuristic in `MapperGenerator.cs` (line 4334) that identifies enum types by exclusion from known primitives
- **DYNDB009**: The diagnostic code for "Unsupported property type" emitted by EntityAnalyzer

## Bug Details

### Bug Condition

The bug manifests when a user defines an entity property with an enum type. The `EntityAnalyzer.IsSupportedPropertyType()` method checks the property type string against a hardcoded array of known type names. Since user-defined enums are not in this list and no enum-detection logic exists, the method returns `false`, causing the analyzer to emit DYNDB009.

**Formal Specification:**
```
FUNCTION isBugCondition(property)
  INPUT: property of type PropertyModel (from EntityAnalyzer)
  OUTPUT: boolean
  
  RETURN property.PropertyType resolves to an enum (TypeKind.Enum)
         AND property is NOT a complex type (Map, Set, List with [DynamoDbMap], TTL, JsonBlob, BlobStorage)
         AND IsSupportedPropertyType(property.PropertyType) returns false
END FUNCTION
```

### Examples

- `public Status EntityStatus { get; set; }` where `Status` is `enum Status { Pending, Success, Failure }` — Expected: accepted, stored as `"Success"`. Actual: DYNDB009 error.
- `public Status? OptionalStatus { get; set; }` — Expected: accepted, nullable enum handled. Actual: DYNDB009 error.
- `public List<Status> Statuses { get; set; }` — Expected: accepted, each element stored as string in List. Actual: DYNDB009 error (collection validation also hits unsupported element type).
- `[DynamoDbAttribute("status", Format = "D")] public Status EntityStatus { get; set; }` — Expected: accepted, stored as numeric value `"200"` in N attribute. Actual: DYNDB009 error.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- All currently-supported primitive types (string, int, long, double, float, decimal, bool, DateTime, DateTimeOffset, Guid, byte[], Ulid, DateOnly, TimeOnly, unsigned integers) continue to pass validation
- Genuinely unsupported types (delegates, Span<T>, arbitrary classes without `[DynamoDbEntity]` or `[DynamoDbMap]`) continue to emit DYNDB009
- Complex types with `[DynamoDbMap]`, `[JsonBlob]`, `[BlobStorage]`, TTL attributes continue to bypass type validation via the `isComplexType` check
- The `MapperGenerator` serialization path (`GetToAttributeValueExpression`, `GetFromAttributeValueExpression`) continues to use `.ToString()` / `Enum.Parse<T>()` for enums
- Collection types (List, HashSet, Dictionary) continue to be handled by their respective checks

**Scope:**
All inputs that do NOT involve enum-typed properties should be completely unaffected by this fix. This includes:
- Properties with primitive types
- Properties with collection types of primitives
- Properties with complex/nested entity types
- Properties with genuinely unsupported types (should still error)

## Hypothesized Root Cause

Based on the code analysis, the root cause is confirmed:

1. **Missing Enum Detection in IsSupportedPropertyType**: The method at line 1678 of `EntityAnalyzer.cs` uses a `string[]` of known type names. User-defined enums like `MyNamespace.Status` are never in this list. The method has no logic to detect that a type is an enum.

2. **Semantic Model Available but Unused for Enums**: The `AnalyzeProperty` method has access to `SemanticModel` and `IPropertySymbol` (via `propertySymbol.Type`), which provides `ITypeSymbol.TypeKind`. This allows reliable enum detection via `TypeKind == TypeKind.Enum`, but this is never checked.

3. **Divergence Between Validator and Generator**: The `MapperGenerator.IsEnumType()` uses a heuristic (anything not in the known primitives list and not a collection). The `EntityAnalyzer` lacks equivalent logic, creating a gap where the mapper can handle types that the validator rejects.

4. **No Format="D" Handling for Numeric Enum Storage**: The mapper's current `IsEnumType` branch always serializes via `.ToString()` (string S attribute). There is no path for `Format = "D"` to trigger numeric serialization `((int)value).ToString()` via N attribute.

## Correctness Properties

Property 1: Bug Condition - Enum Properties Accepted by Validator

_For any_ entity property whose type resolves to an enum (including nullable enums and collections of enums), the fixed `IsSupportedPropertyType` logic SHALL return `true`, preventing DYNDB009 from being emitted, and the property SHALL flow through to the mapper for correct serialization.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

Property 2: Preservation - Non-Enum Type Validation Unchanged

_For any_ entity property whose type is NOT an enum (primitives, collections of primitives, complex types, genuinely unsupported types), the fixed validation logic SHALL produce the same result as the original logic, preserving acceptance of supported types and rejection of unsupported types.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

## Fix Implementation

### Changes Required

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`

**Function**: `IsSupportedPropertyType` (line 1678)

**Specific Changes**:

1. **Change method signature to accept ITypeSymbol**: Currently `IsSupportedPropertyType(string typeName)` only receives the type name string. Change to `IsSupportedPropertyType(ITypeSymbol typeSymbol)` (or add the symbol as a second parameter) so it can use `typeSymbol.TypeKind == TypeKind.Enum` for reliable enum detection.

2. **Add enum detection before the allowlist check**: Before checking the `supportedTypes` array, check if the type (or its underlying type for nullables) is an enum. If so, return `true` immediately.

3. **Handle nullable enums**: For `Nullable<T>` types, unwrap to the inner type and check if that is an enum. The existing nullable handling (`baseType.StartsWith("System.Nullable<")`) returns `true` unconditionally — this is acceptable since if the inner type is unsupported, the mapper will fail at generation time. However, for explicit enum validation, unwrap and check `TypeKind`.

4. **Update the call site**: At line 1312, where `IsSupportedPropertyType` is called, pass the `IPropertySymbol.Type` (available from `propertySymbol.Type` in `AnalyzeProperty`) in addition to or instead of the string type name.

5. **Optionally: Add Format="D" support in MapperGenerator**: In `GetToAttributeValueExpression` and `GetFromAttributeValueExpression`, when the property is an enum type AND `property.Format == "D"`, generate numeric serialization: `new AttributeValue { N = ((int){value}).ToString() }` and deserialization: `({EnumType})int.Parse({value}.N)`.

### Alternative Approach (Simpler)

If modifying the method signature is too invasive, the call site at line 1312 can be modified to add a separate `IsEnumType` check using the semantic model before calling `IsSupportedPropertyType`:

```csharp
var isEnum = propertySymbol.Type.TypeKind == TypeKind.Enum ||
             (propertySymbol.Type is INamedTypeSymbol namedType && 
              namedType.IsGenericType && 
              namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
              namedType.TypeArguments[0].TypeKind == TypeKind.Enum);

if (!isComplexType && !isEnum && !IsSupportedPropertyType(propertyModel.PropertyType))
{
    ReportDiagnostic(DiagnosticDescriptors.UnsupportedPropertyType, ...);
}
```

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis.

**Test Plan**: Write source generator unit tests that provide entity source code with enum properties and verify DYNDB009 is emitted. Run these tests on the UNFIXED code to observe the diagnostic being produced, confirming the root cause.

**Test Cases**:
1. **Simple Enum Property**: Entity with `public Status EntityStatus { get; set; }` — verify DYNDB009 is emitted (will fail on unfixed code, confirming bug)
2. **Nullable Enum Property**: Entity with `public Status? EntityStatus { get; set; }` — verify behavior (may or may not trigger due to existing nullable short-circuit)
3. **Enum Collection Property**: Entity with `public List<Status> Statuses { get; set; }` — verify behavior with collection of enums
4. **Enum with Format="D"**: Entity with `[DynamoDbAttribute("status", Format = "D")] public Status EntityStatus { get; set; }` — verify DYNDB009 is emitted

**Expected Counterexamples**:
- DYNDB009 diagnostic produced for every enum property variant
- Root cause confirmed: `IsSupportedPropertyType` returns false for all enum type strings

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL property WHERE property.Type.TypeKind == TypeKind.Enum DO
  result := AnalyzeEntity_fixed(entityWithProperty)
  ASSERT result.Diagnostics NOT CONTAINS "DYNDB009"
  ASSERT result.Properties CONTAINS property
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL property WHERE property.Type.TypeKind != TypeKind.Enum DO
  ASSERT AnalyzeEntity_original(property) = AnalyzeEntity_fixed(property)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain of type names
- It catches edge cases that manual unit tests might miss (e.g., types named similarly to enums)
- It provides strong guarantees that behavior is unchanged for all non-enum inputs

**Test Plan**: Observe behavior on UNFIXED code first for known supported and unsupported types, then write property-based tests capturing that behavior.

**Test Cases**:
1. **Primitive Type Preservation**: Verify string, int, bool, DateTime, Guid, etc. continue to pass validation after fix
2. **Unsigned Integer Preservation**: Verify ulong, uint, ushort, byte, sbyte, short continue to pass
3. **Complex Type Preservation**: Verify `[DynamoDbMap]` nested entities continue to bypass validation
4. **Unsupported Type Rejection Preservation**: Verify types like `Action`, `Span<byte>`, arbitrary classes without proper attributes continue to emit DYNDB009

### Unit Tests

- Test EntityAnalyzer with simple enum property produces no DYNDB009
- Test EntityAnalyzer with nullable enum property produces no DYNDB009
- Test EntityAnalyzer with List<Enum> property produces no DYNDB009
- Test EntityAnalyzer with HashSet<Enum> property produces no DYNDB009
- Test EntityAnalyzer with Format="D" enum property produces no DYNDB009
- Test MapperGenerator generates correct `ToString()` serialization for enum properties
- Test MapperGenerator generates correct `Enum.Parse<T>()` deserialization for enum properties
- Test MapperGenerator generates numeric serialization when Format="D" is specified
- Test that genuinely unsupported types still produce DYNDB009

### Property-Based Tests

- Generate random type names from the supported types list and verify they continue to pass validation
- Generate enum types with various names/namespaces and verify they all pass validation after fix
- Generate properties with nullable wrappers around enums and verify acceptance
- Test that the set of types rejected by the fixed validator is a proper subset of types rejected by the original (only enums removed from rejection set)

### Integration Tests

- Full source generator pipeline test: entity with enum property compiles and generates correct mapper code
- End-to-end: entity with enum property generates `ToDynamoDb` that serializes enum as string
- End-to-end: entity with enum property generates `FromDynamoDb` that deserializes string back to enum
- End-to-end: entity with Format="D" enum generates numeric serialization/deserialization
- End-to-end: entity with mixed properties (enum + primitive + nested) generates correctly
