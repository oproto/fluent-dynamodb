# Extracted + DynamoDbAttribute Conflict Detection Bugfix Design

## Overview

The source generator's `ValidateExtractedProperty()` method fails to detect when a property has both `[Extracted]` and `[DynamoDbAttribute]` applied simultaneously. These attributes are semantically conflicting: `[Extracted]` means the value is derived from a composite key at read time, while `[DynamoDbAttribute]` maps the property to its own independent DynamoDB attribute. The fix adds a new FDDB124 error diagnostic that checks `HasAttributeMapping` on each extracted property before continuing validation, causing the build to fail early and preventing generation of conflicting serialization/extraction code.

## Glossary

- **Bug_Condition (C)**: A property has both `[Extracted]` and `[DynamoDbAttribute]` applied — `IsExtracted == true && HasAttributeMapping == true`
- **Property (P)**: The system emits FDDB124 error diagnostic at the property's identifier location, halting code generation
- **Preservation**: All existing validation behavior in `ValidateExtractedProperty()` for properties that do NOT have both attributes applied simultaneously
- **ValidateExtractedProperty()**: The method in `EntityAnalyzer.cs` (~line 2145) that validates extracted property configuration (source existence, constant key conflicts, index bounds)
- **HasAttributeMapping**: Computed property on `PropertyModel` — `!string.IsNullOrEmpty(AttributeName)` — true when `[DynamoDbAttribute]` is applied
- **IsExtracted**: Computed property on `PropertyModel` — `ExtractedKey != null` — true when `[Extracted]` is applied

## Bug Details

### Bug Condition

The bug manifests when a property has both `[Extracted(sourceProperty, index)]` and `[DynamoDbAttribute("name")]` attributes applied. The `ValidateExtractedProperty()` method currently checks source property existence, constant key conflicts, and index bounds — but never checks whether the extracted property also has an independent DynamoDB attribute mapping.

**Formal Specification:**
```
FUNCTION isBugCondition(property)
  INPUT: property of type PropertyModel
  OUTPUT: boolean
  
  RETURN property.IsExtracted == true
         AND property.HasAttributeMapping == true
END FUNCTION
```

### Examples

- Property `[Extracted("Pk", 0)] [DynamoDbAttribute("year")] public int Year { get; set; }` — **Expected**: FDDB124 error. **Actual**: No diagnostic emitted; generated code writes `year` attribute AND extracts from `Pk`.
- Property `[Extracted("Pk", 1)] [DynamoDbAttribute("month")] public int Month { get; set; }` — **Expected**: FDDB124 error. **Actual**: Silent generation of redundant serialization and extraction paths.
- Property `[Extracted("Pk", 0)] public int Year { get; set; }` (no `[DynamoDbAttribute]`) — **Expected**: No error (valid extracted property). **Actual**: Correct behavior, no diagnostic.
- Property `[DynamoDbAttribute("status")] public string Status { get; set; }` (no `[Extracted]`) — **Expected**: No error (standard mapped property). **Actual**: Correct behavior, no diagnostic.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Properties with only `[Extracted]` (no `[DynamoDbAttribute]`) must continue to generate extraction-only code without error
- Properties with only `[DynamoDbAttribute]` (no `[Extracted]`) must continue to generate standard serialization/deserialization code without error
- Extracted properties referencing valid computed source properties must continue through source property existence, constant key conflict (FDDB122), and index bounds validation
- Extracted properties referencing constant key properties must continue to emit FDDB122
- Extracted properties with negative indices must continue to emit the invalid index diagnostic

**Scope:**
All properties that do NOT have both `[Extracted]` and `[DynamoDbAttribute]` applied simultaneously should be completely unaffected by this fix. This includes:
- Standard mapped properties with only `[DynamoDbAttribute]`
- Pure extracted properties with only `[Extracted]`
- Computed properties with `[Computed]`
- Properties with other attribute combinations (GSI keys, LSI keys, etc.)

## Hypothesized Root Cause

Based on the bug description, the issue is straightforward:

1. **Missing Validation Check**: `ValidateExtractedProperty()` never checks `HasAttributeMapping` on the extracted property. It validates the *source* property and *index*, but not whether the extracted property itself has a conflicting attribute mapping.

2. **No DiagnosticDescriptor Exists**: There is no `FDDB124` diagnostic descriptor defined in `DiagnosticDescriptors.cs` — the file currently ends at FDDB123 (`ConstantKeyEmptyValue`). The check cannot exist without the descriptor.

3. **Attribute System Allows Combination**: Both `[Extracted]` and `[DynamoDbAttribute]` use `AttributeTargets.Property` without `AllowMultiple = false` constraints that would prevent co-application, so the C# compiler accepts the combination silently.

## Correctness Properties

Property 1: Bug Condition - Extracted Property With Attribute Mapping Emits FDDB124

_For any_ property where `IsExtracted == true` AND `HasAttributeMapping == true`, the fixed `ValidateExtractedProperty()` function SHALL emit a diagnostic with code "FDDB124", severity Error, at the property's identifier location, with a message containing the property name.

**Validates: Requirements 2.1, 2.2, 2.3**

Property 2: Preservation - Extracted Properties Without Attribute Mapping Unchanged

_For any_ property where `IsExtracted == true` AND `HasAttributeMapping == false`, the fixed `ValidateExtractedProperty()` function SHALL produce the same validation results as the original function, preserving source property existence checks, constant key conflict detection (FDDB122), and index bounds validation.

**Validates: Requirements 3.1, 3.3, 3.4, 3.5**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Diagnostics/DiagnosticDescriptors.cs`

**Change**: Add new DiagnosticDescriptor

**Specific Changes**:
1. **Add FDDB124 descriptor**: Add a new `DiagnosticDescriptor` named `ExtractedPropertyHasAttributeMapping` after `ConstantKeyEmptyValue` (FDDB123)
   - Code: `"FDDB124"`
   - Title: `"Extracted property conflicts with DynamoDbAttribute"`
   - Message: `"Property '{0}' has both [Extracted] and [DynamoDbAttribute]. Extracted properties derive their value from a composite key and must not have independent DynamoDB attribute mapping. Remove one of the attributes."`
   - Category: `"DynamoDb"`
   - Severity: `DiagnosticSeverity.Error`
   - Enabled by default: `true`
   - Help link: `string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB124")`

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`

**Function**: `ValidateExtractedProperty()`

**Specific Changes**:
2. **Add HasAttributeMapping check**: Insert a check at the beginning of `ValidateExtractedProperty()`, BEFORE the source property existence check, that tests `extractedProperty.HasAttributeMapping`
   - If true, call `ReportDiagnostic(DiagnosticDescriptors.ExtractedPropertyHasAttributeMapping, extractedProperty.PropertyDeclaration?.Identifier.GetLocation(), extractedProperty.PropertyName)`
   - Early return after reporting (no need to continue validating source/index if the fundamental conflict exists)

3. **Placement**: The check must be the FIRST validation in the method, before accessing `extractedKey.SourceProperty`, to provide the most specific error message and avoid cascading diagnostics

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write source generator unit tests that provide entity source code with both `[Extracted]` and `[DynamoDbAttribute]` on the same property, run the analyzer, and assert that FDDB124 is emitted. Run these tests on the UNFIXED code to observe the absence of the diagnostic.

**Test Cases**:
1. **Basic Conflict Test**: Entity with `[Extracted("Pk", 0)] [DynamoDbAttribute("year")] public int Year` — assert FDDB124 emitted (will fail on unfixed code)
2. **Multiple Conflicting Properties**: Entity with two properties both having `[Extracted]` + `[DynamoDbAttribute]` — assert FDDB124 emitted for each (will fail on unfixed code)
3. **Conflict With Valid Source**: Entity where the extracted source property exists and is computed, but the extracted property also has `[DynamoDbAttribute]` — assert FDDB124 emitted before other checks (will fail on unfixed code)

**Expected Counterexamples**:
- No FDDB124 diagnostic is produced on unfixed code when both attributes are present
- The generated code silently includes both serialization and extraction paths

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL property WHERE isBugCondition(property) DO
  result := ValidateExtractedProperty_fixed(property)
  ASSERT result.diagnostics CONTAINS diagnostic WITH code == "FDDB124"
  ASSERT diagnostic.severity == Error
  ASSERT diagnostic.message CONTAINS property.PropertyName
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL property WHERE NOT isBugCondition(property) DO
  ASSERT ValidateExtractedProperty_original(property) = ValidateExtractedProperty_fixed(property)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many property configurations automatically across the input domain
- It catches edge cases that manual unit tests might miss (e.g., empty string AttributeName vs null)
- It provides strong guarantees that no existing diagnostic behavior is altered

**Test Plan**: Observe behavior on UNFIXED code first for properties with only `[Extracted]`, then write property-based tests capturing that behavior.

**Test Cases**:
1. **Extracted-Only Preservation**: Verify `[Extracted("Pk", 0)]` without `[DynamoDbAttribute]` emits no FDDB124 and continues to validate source/index normally
2. **DynamoDbAttribute-Only Preservation**: Verify `[DynamoDbAttribute("name")]` without `[Extracted]` is completely unaffected by the new check
3. **Existing FDDB122 Preservation**: Verify extracted property referencing a constant key still emits FDDB122, not FDDB124
4. **Invalid Index Preservation**: Verify extracted property with negative index still emits the index diagnostic, not FDDB124

### Unit Tests

- Test that FDDB124 is emitted when both `[Extracted]` and `[DynamoDbAttribute]` are on the same property
- Test that FDDB124 message contains the property name
- Test that FDDB124 has Error severity
- Test that validation halts after FDDB124 (no cascading source/index diagnostics)
- Test that properties with only `[Extracted]` do not trigger FDDB124
- Test that properties with only `[DynamoDbAttribute]` do not trigger FDDB124

### Property-Based Tests

- Generate random `PropertyModel` instances with `IsExtracted == true` and varying `HasAttributeMapping` values — verify FDDB124 is emitted if and only if `HasAttributeMapping == true`
- Generate random entity configurations with mixed property types (computed, extracted, standard) — verify only the conflicting properties emit FDDB124, others are unaffected
- Generate extracted properties with valid/invalid source properties and varying `HasAttributeMapping` — verify FDDB124 takes precedence over source validation when both conditions hold

### Integration Tests

- Full source generator integration test: compile an entity with the conflict and verify build output contains FDDB124
- Full source generator integration test: compile a valid entity with `[Extracted]` only and verify no FDDB124 appears
- Verify that existing test entities in the test suite continue to compile without new diagnostics
