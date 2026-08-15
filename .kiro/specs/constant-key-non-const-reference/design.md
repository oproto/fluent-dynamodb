# Constant Key Non-Const Reference Bugfix Design

## Overview

When a key property (`[PartitionKey]` or `[SortKey]`) uses expression-body (`=>`) or read-only auto-property (`{ get; }`) syntax with a reference to a `static readonly` field (or other non-compile-time-constant expression), the `DetectConstantKeyValue` method in `EntityAnalyzer` silently fails because `SemanticModel.GetConstantValue()` returns null for runtime-only values. The generator then falls through to normal code generation, emitting `entity.Property = value.S;` in `FromDynamoDb()` for a property that has no setter — producing uncompilable output. This fix introduces diagnostic FDDB126 to explicitly inform the user and guards against generating assignments to read-only properties.

## Glossary

- **Bug_Condition (C)**: A key property uses expression-body or read-only auto-property syntax, `GetConstantValue()` returns null (value is not a compile-time constant), and the property has no setter — the generator silently falls through to normal assignment codegen
- **Property (P)**: The system shall emit diagnostic FDDB126 (Error) indicating the key value is not a compile-time constant, and shall not generate property assignment or convenience methods for that property
- **Preservation**: All existing behavior for properties that DO resolve to compile-time constants (string literals, `const` fields), properties with setters (normal mutable keys), and non-key properties must remain unchanged
- **DetectConstantKeyValue**: The method in `Analysis/EntityAnalyzer.cs` that checks expression-body and read-only auto-property patterns for constant key values
- **PropertyModel.IsReadOnlyKeyProperty**: A new flag indicating the property is a key property that is syntactically read-only (expression-body or get-only auto-property) but whose value could not be resolved as a compile-time constant
- **FDDB126**: The new diagnostic emitted when a read-only key property references a non-compile-time-constant value

## Bug Details

### Bug Condition

The bug manifests when a key property uses expression-body or read-only auto-property syntax but references a value that `SemanticModel.GetConstantValue()` cannot resolve (e.g., `static readonly` field, property access, method call). The `DetectConstantKeyValue` method sets `ConstantKeyValue = null` and returns without any diagnostic. The generator then treats the property as a normal mutable key, generating `entity.Sk = attrValue.S;` in `FromDynamoDb()` — but the property has no setter, causing CS0200 at compile time.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type (PropertyDeclarationSyntax, SemanticModel, PropertyModel)
  OUTPUT: boolean
  
  LET propertyDecl = input.PropertyDeclarationSyntax
  LET semanticModel = input.SemanticModel
  LET propertyModel = input.PropertyModel
  
  RETURN (propertyModel.IsPartitionKey OR propertyModel.IsSortKey)
         AND (propertyDecl.HasExpressionBody OR propertyDecl.IsGetOnlyAutoProperty)
         AND semanticModel.GetConstantValue(expression) DOES NOT resolve to string
         AND propertyModel.ConstantKeyValue IS NULL
END FUNCTION
```

### Examples

- `public string Sk => DynamoDB.DefaultSortkeyValue;` where `DefaultSortkeyValue` is `static readonly string` → Bug: generates `entity.Sk = value.S;` which fails with CS0200
- `public string Sk { get; } = DynamoDB.DefaultSortkeyValue;` where `DefaultSortkeyValue` is `static readonly string` → Bug: generates `entity.Sk = value.S;` which fails with CS0200
- `public string Sk => GetSortKey();` (method call) → Bug: same uncompilable assignment generated
- `public string Sk => SomeClass.SomeProperty;` (property access) → Bug: same uncompilable assignment generated
- `public string Sk => "PROFILE";` (string literal) → NOT a bug: detection succeeds, `IsConstantKey = true`
- `public string Sk => Constants.Key;` (const field) → NOT a bug: detection succeeds via `GetConstantValue()`

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- String literal expression-body keys (`=> "PROFILE"`) must continue to be detected as constant keys with `IsConstantKey = true`
- `const` field reference keys (`=> Constants.Key`) must continue to resolve via `GetConstantValue()` and be detected as constant keys
- Read-only auto-property keys with literal initializers (`{ get; } = "PROFILE"`) must continue to be detected as constant keys
- Read-only auto-property keys with `const` field initializers must continue to be detected as constant keys
- Normal mutable key properties (`{ get; set; }`) must continue to work as normal key properties with no constant key detection attempted
- All existing FDDB120–FDDB125 diagnostics must continue to fire in their original conditions
- All non-key properties (regardless of syntax) must be completely unaffected

**Scope:**
All inputs that do NOT involve read-only key properties with unresolvable non-const references should be completely unaffected by this fix. This includes:
- Key properties with string literal values (expression-body or auto-property)
- Key properties with `const` field references
- Key properties with `{ get; set; }` accessor patterns
- Non-key properties of any kind
- Expression-body properties not annotated with `[PartitionKey]` or `[SortKey]`

## Hypothesized Root Cause

Based on the bug description, the most likely issues are:

1. **Missing fallback diagnostic in DetectConstantKeyValue**: When `GetConstantValue()` returns null for an expression-body or get-only auto-property key, the method simply returns without setting `ConstantKeyValue` and without emitting any diagnostic. There is no detection that the property is syntactically read-only (no setter).

2. **No read-only property guard in MapperGenerator.FromDynamoDb**: The `GeneratePropertyFromAttributeValue` method unconditionally generates `entity.PropertyName = value;` for non-constant key properties. It does not check whether the property actually has a setter before generating the assignment.

3. **PropertyModel lacks read-only-key-property awareness**: The `IsReadOnly` flag on `PropertyModel` only checks `IsComputed || IsExtracted`. There is no mechanism to flag a property as "read-only due to syntax" when it's an expression-body or get-only auto-property that failed constant key detection.

4. **No integration test verifying full compilation**: Existing tests check generator diagnostics but not whether the generated code compiles successfully with the input source.

## Correctness Properties

Property 1: Bug Condition - Read-Only Key With Non-Const Reference Emits FDDB126

_For any_ key property (partition or sort) that uses expression-body or read-only auto-property syntax AND whose value expression does not resolve to a compile-time constant string via `GetConstantValue()`, the fixed `DetectConstantKeyValue` method SHALL emit diagnostic FDDB126 with severity Error on the property declaration, and the generator SHALL NOT produce property assignment code for that property in `FromDynamoDb()`.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

Property 2: Preservation - Compile-Time Constant Keys Continue To Resolve

_For any_ key property that uses expression-body or read-only auto-property syntax AND whose value expression DOES resolve to a compile-time constant string (string literal or `const` field reference), the fixed code SHALL produce exactly the same result as the original code — detecting the property as a constant key with `IsConstantKey = true` and `ConstantKeyValue` set to the resolved string value.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Diagnostics/DiagnosticDescriptors.cs`

**Change**: Add FDDB126 descriptor

**Specific Changes**:
1. **Add FDDB126 DiagnosticDescriptor**: Add a new `ConstantKeyNonConstReference` descriptor with id "FDDB126", severity Error, message format indicating the property uses expression-body/read-only syntax but the value is not a compile-time constant, with guidance to use a string literal or `const` field.

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Models/PropertyModel.cs`

**Change**: Add `IsReadOnlyKeyProperty` flag

**Specific Changes**:
1. **Add IsReadOnlyKeyProperty property**: `public bool IsReadOnlyKeyProperty { get; set; }` — set to `true` when a key property is expression-body or get-only auto-property AND `ConstantKeyValue` remains null after detection. This provides a clear signal to downstream generators that they must not generate property assignments.

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`

**Function**: `DetectConstantKeyValue`

**Specific Changes**:
1. **Add read-only detection after constant detection fails**: In both Case 1 (expression-body) and Case 2 (read-only auto-property), after `GetConstantValue()` returns null or non-string, check that the property is indeed read-only (no setter for expression-body; get-only for auto-property). If so, set `propertyModel.IsReadOnlyKeyProperty = true` and emit FDDB126 via `ReportDiagnostic`.
2. **Report diagnostic with property name and location**: Use `propertyDecl.Identifier.GetLocation()` for the diagnostic location and include the property name in the message format argument.

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`

**Function**: `GeneratePropertyFromAttributeValue` (shared deserialization path)

**Specific Changes**:
1. **Guard against assignment to read-only key properties**: After the existing `IsConstantKey` early-return check, add a check for `property.IsReadOnlyKeyProperty`. If true, skip property assignment entirely (no code emitted for this property in `FromDynamoDb()`). This is a safety net — with FDDB126 emitted as Error, code generation should halt for the entity. But if the diagnostic is downgraded to Warning in the future, this guard prevents uncompilable output.

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`

**Function**: `GeneratePropertyToAttributeValue` (shared serialization path)

**Specific Changes**:
1. **Guard against reading from read-only key property**: After the existing `IsConstantKey` early-return, add a check for `property.IsReadOnlyKeyProperty`. If true, skip the property in `ToDynamoDb()` serialization since we cannot read a value that may not be deterministic or accessible.

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write tests that define entity classes with expression-body and read-only auto-property key properties referencing `static readonly` fields. Run the source generator on unfixed code and inspect:
- Whether any diagnostic is emitted (expect: none on unfixed code)
- Whether the generated `FromDynamoDb` code contains `entity.Sk = ...;` assignment (expect: yes, demonstrating the bug)

**Test Cases**:
1. **Expression-body with static readonly field**: Entity with `[SortKey] public string Sk => StaticFields.Value;` where `Value` is `static readonly` (will produce uncompilable code on unfixed generator)
2. **Read-only auto-property with static readonly initializer**: Entity with `[SortKey] public string Sk { get; } = StaticFields.Value;` (will produce uncompilable code on unfixed generator)
3. **Expression-body with method call**: Entity with `[SortKey] public string Sk => GetKey();` (will produce uncompilable code on unfixed generator)
4. **Expression-body with property access**: Entity with `[SortKey] public string Sk => Config.DefaultKey;` where DefaultKey is a property (will produce uncompilable code on unfixed generator)

**Expected Counterexamples**:
- Generated code contains `entity.Sk = ` assignment for properties with no setter
- No diagnostic emitted to warn the user
- Possible causes: `DetectConstantKeyValue` returns silently when `GetConstantValue()` yields null

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := DetectConstantKeyValue_fixed(input)
  diagnostics := getDiagnostics()
  ASSERT diagnostics CONTAINS FDDB126
  ASSERT input.PropertyModel.IsReadOnlyKeyProperty == true
  ASSERT generatedFromDynamoDb DOES NOT CONTAIN "entity.{PropertyName} ="
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT DetectConstantKeyValue_original(input) = DetectConstantKeyValue_fixed(input)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain (random string literals, random const field names)
- It catches edge cases that manual unit tests might miss (empty strings, special characters, unicode)
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Observe behavior on UNFIXED code first for all valid constant key patterns (string literals, const field references), then write property-based tests capturing that behavior.

**Test Cases**:
1. **String literal preservation**: For any random non-empty string V, an expression-body key `=> "V"` must still resolve `ConstantKeyValue = V` and `IsReadOnlyKeyProperty = false`
2. **Const field preservation**: For any `const string` field reference, `GetConstantValue()` must still resolve the value and set `IsConstantKey = true`
3. **Mutable key preservation**: For any property with `{ get; set; }`, constant key detection must not be attempted and `IsReadOnlyKeyProperty` must remain `false`
4. **Non-key property preservation**: For any property without `[PartitionKey]`/`[SortKey]`, neither `IsConstantKey` nor `IsReadOnlyKeyProperty` should be set

### Unit Tests

- Test FDDB126 emitted for expression-body key with `static readonly` field reference
- Test FDDB126 emitted for read-only auto-property key with `static readonly` field initializer
- Test FDDB126 emitted for expression-body key with method call return
- Test FDDB126 emitted for expression-body key with property access return
- Test FDDB126 NOT emitted for expression-body key with string literal
- Test FDDB126 NOT emitted for expression-body key with `const` field reference
- Test FDDB126 NOT emitted for mutable property `{ get; set; }` regardless of type
- Test that generated `FromDynamoDb` does not contain property assignment when `IsReadOnlyKeyProperty = true`
- Test full compilation succeeds when FDDB126 is not emitted (valid constant keys)

### Property-Based Tests

- Generate random non-empty strings and verify expression-body string literal keys always resolve to `IsConstantKey = true` (preservation)
- Generate random `static readonly` field scenarios and verify FDDB126 is always emitted (fix checking)
- Generate random property configurations (key vs non-key, mutable vs read-only) and verify only the specific bug condition triggers FDDB126

### Integration Tests

- Test full compilation of entity with `static readonly` reference key — expect FDDB126, no generated code
- Test full compilation of entity with valid string literal key — expect no diagnostics, generated code compiles
- Test full compilation of entity with `const` field key — expect no diagnostics, generated code compiles
- Test that `outputCompilation.GetDiagnostics()` is checked for CS errors in existing constant key integration tests (test gap from issue)
