# Multi-Computed-Field-Target Bugfix Design

## Overview

`PropertyMetadata.ComputedFieldTarget` is typed as `string?`, limiting it to a single computed field target. When a source property contributes to multiple non-key computed fields (e.g., `Status` feeds both `Gsi1Pk` and `Gsi2Pk`), the MetadataGenerator only records the first match via `FirstOrDefault`. The fix renames and widens this property to `string[]? ComputedFieldTargets`, updates the source generator to emit all targets, and updates `IsComputedSourceProperty` to check the new array form. The `ValidateAndProcessComputedFields` loop already handles multi-target correctly and requires no changes.

## Glossary

- **Bug_Condition (C)**: A source property is listed in `SourceProperties` of more than one non-key computed field, causing the MetadataGenerator to emit only the first match
- **Property (P)**: `PropertyMetadata.ComputedFieldTargets` SHALL contain all non-key computed fields that list this property as a source
- **Preservation**: Single-target source properties, non-source properties, `IsComputedSourceProperty` boolean behavior, `ValidateAndProcessComputedFields` recomputation logic, and FDDB072 validation must remain unchanged
- **PropertyMetadata**: The runtime metadata class in `Oproto.FluentDynamoDb/Metadata/PropertyMetadata.cs` describing a DynamoDB entity property
- **MapperGenerator**: The source generator in `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` that emits `PropertyMetadata` initialization code
- **IsComputedSourceProperty**: A static method in `UpdateExpressionTranslator.cs` (~line 722) that determines if a property is a source of a computed field
- **ValidateAndProcessComputedFields**: The method that iterates all computed fields, validates all sources are assigned, and emits recomputation SET operations

## Bug Details

### Bug Condition

The bug manifests when a source property is listed in the `SourceProperties` of two or more non-key computed fields. The `MapperGenerator` uses `FirstOrDefault` to find the computed field targeting this source and emits only that single name as `ComputedFieldTarget`. Any additional computed fields referencing the same source are silently lost from the metadata.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type { entityProperties: PropertyModel[], sourceProperty: PropertyModel }
  OUTPUT: boolean
  
  LET matchingComputedFields = entityProperties
    .WHERE(p => p.IsComputed AND NOT p.IsPartitionKey AND NOT p.IsSortKey
                AND p.ComputedKey.SourceProperties.Contains(sourceProperty.PropertyName))
  
  RETURN matchingComputedFields.Count > 1
END FUNCTION
```

### Examples

- **Status feeds Gsi1Pk and Gsi2Pk**: `Status` is listed in `[Computed("Status", "Region")]` on `Gsi1Pk` and `[Computed("Status", "Priority")]` on `Gsi2Pk`. Current code emits `ComputedFieldTarget = "Gsi1Pk"` only; `Gsi2Pk` is lost.
- **SharedField feeds three GSIs**: A property contributing to three computed GSI keys. Current code emits only the first one found by `FirstOrDefault`.
- **Single-target source (non-bug)**: A source property contributing to exactly one computed field — `FirstOrDefault` returns the correct single result and behavior is correct.
- **Non-source property (non-bug)**: A property not listed in any computed field's `SourceProperties` — `FirstOrDefault` returns null, `ComputedFieldTarget` is not emitted.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Single-target source properties must continue to be identified as computed source properties and have their target emitted (now as a single-element array)
- Non-source properties must continue to have `ComputedFieldTargets` as null
- `IsComputedSourceProperty` must continue to return true for direct sources and for extracted properties targeting non-key computed fields
- `ValidateAndProcessComputedFields` must continue to iterate all computed fields independently, validating sources and emitting recomputation expressions — it does NOT use `ComputedFieldTarget(s)` internally
- FDDB072 validation must continue to fire independently per computed field when sources are missing
- Mouse/non-keyboard interactions: N/A (this is a source generator / metadata issue)

**Scope:**
All source properties contributing to exactly zero or one non-key computed field should be completely unaffected by this fix (beyond the property rename from `ComputedFieldTarget` to `ComputedFieldTargets` and the type change from `string?` to `string[]?`). The extracted-field path in `IsComputedSourceProperty` is unchanged.

## Hypothesized Root Cause

Based on the bug description, the root cause is:

1. **`FirstOrDefault` in MapperGenerator (~line 4872)**: The generator uses `entity.Properties.FirstOrDefault(...)` to find ONE computed field that lists the current property as a source. When multiple computed fields share the same source, only the first match is emitted. The fix is to use `.Where(...)` and emit all matches as an array.

2. **`string?` type on PropertyMetadata**: The property type `string? ComputedFieldTarget` structurally cannot hold multiple targets. Changing to `string[]? ComputedFieldTargets` enables correct modeling.

3. **`!= null` check in IsComputedSourceProperty (~line 730)**: The check `propertyMetadata.ComputedFieldTarget != null` must be updated to `propertyMetadata.ComputedFieldTargets?.Length > 0` to match the new type.

4. **No issue in ValidateAndProcessComputedFields**: This method already iterates all computed fields independently using `cf.SourceProperties.Contains(sourceName)`, so it correctly handles multi-target regardless of metadata property shape.

## Correctness Properties

Property 1: Bug Condition - Multi-Target Source Emits All Targets

_For any_ entity configuration where a source property is listed in the `SourceProperties` of N non-key computed fields (N > 1), the generated `PropertyMetadata.ComputedFieldTargets` array SHALL contain exactly those N computed field names.

**Validates: Requirements 2.1, 2.2**

Property 2: Preservation - Single-Target and Non-Source Behavior

_For any_ source property contributing to exactly one non-key computed field, `ComputedFieldTargets` SHALL be an array containing that single target name. _For any_ property contributing to zero non-key computed fields, `ComputedFieldTargets` SHALL remain null. `IsComputedSourceProperty` SHALL continue to return the same boolean result as before the fix for all inputs.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb/Metadata/PropertyMetadata.cs`

**Property**: `ComputedFieldTarget`

**Specific Changes**:
1. **Rename and retype**: Change `public string? ComputedFieldTarget { get; set; }` to `public string[]? ComputedFieldTargets { get; set; }`
2. **Update XML doc**: Update the summary to reflect it now holds all target computed field names

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` (~line 4872)

**Function**: Property metadata emission block

**Specific Changes**:
1. **Replace `FirstOrDefault` with `Where`**: Change from finding a single matching computed field to collecting all matches
2. **Emit array initializer**: Instead of `ComputedFieldTarget = "name"`, emit `ComputedFieldTargets = new[] { "name1", "name2" }`

```csharp
// Before:
var targetComputedField = entity.Properties.FirstOrDefault(p =>
    p.IsComputed && !p.IsPartitionKey && !p.IsSortKey &&
    p.ComputedKey!.SourceProperties.Contains(property.PropertyName));
if (targetComputedField != null)
{
    sb.AppendLine($"                        ComputedFieldTarget = \"{EscapeString(targetComputedField.PropertyName)}\",");
}

// After:
var targetComputedFields = entity.Properties
    .Where(p => p.IsComputed && !p.IsPartitionKey && !p.IsSortKey &&
                p.ComputedKey!.SourceProperties.Contains(property.PropertyName))
    .Select(p => p.PropertyName)
    .ToArray();
if (targetComputedFields.Length > 0)
{
    var targets = string.Join(", ", targetComputedFields.Select(t => $"\"{EscapeString(t)}\""));
    sb.AppendLine($"                        ComputedFieldTargets = new[] {{ {targets} }},");
}
```

---

**File**: `Oproto.FluentDynamoDb/Expressions/UpdateExpressionTranslator.cs` (~line 730)

**Function**: `IsComputedSourceProperty`

**Specific Changes**:
1. **Update null check**: Change `if (propertyMetadata.ComputedFieldTarget != null)` to `if (propertyMetadata.ComputedFieldTargets?.Length > 0)`
2. **Update XML doc reference**: Update `<see cref="..."/>` to reference `ComputedFieldTargets`

---

**File**: `Oproto.FluentDynamoDb.IntegrationTests/RealWorld/ComputedGsiFieldUpdateIntegrationTests.cs`

**Specific Changes**:
1. **Update all `ComputedFieldTarget = "Gsi1Pk"` assignments**: Change to `ComputedFieldTargets = new[] { "Gsi1Pk" }` (single-element array for existing tests)

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Create an entity model where a source property contributes to two non-key computed fields. Invoke the MetadataGenerator logic (or inspect generated metadata) and verify that `ComputedFieldTarget` only contains one target name. Run on UNFIXED code to observe the data loss.

**Test Cases**:
1. **Dual-target source**: Define `Status` as a source of both `Gsi1Pk` and `Gsi2Pk`. Verify metadata only records one target (will demonstrate bug on unfixed code).
2. **Triple-target source**: Define a property as a source of three computed fields. Verify only the first is emitted (will demonstrate bug on unfixed code).
3. **Single-target control**: Define a property sourcing one computed field. Verify it works correctly (should pass on unfixed code — not a bug case).

**Expected Counterexamples**:
- `ComputedFieldTarget` contains only the first computed field name, missing subsequent targets
- Root cause confirmed: `FirstOrDefault` returns first match, subsequent matches discarded

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL entityConfig WHERE isBugCondition(entityConfig) DO
  metadata := generatePropertyMetadata_fixed(entityConfig)
  ASSERT metadata.ComputedFieldTargets CONTAINS ALL expected target names
  ASSERT metadata.ComputedFieldTargets.Length == expectedTargetCount
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL entityConfig WHERE NOT isBugCondition(entityConfig) DO
  ASSERT IsComputedSourceProperty_fixed(entityConfig) == IsComputedSourceProperty_original(entityConfig)
  IF sourceProperty contributes to one computed field THEN
    ASSERT ComputedFieldTargets == new[] { originalComputedFieldTarget }
  ELSE
    ASSERT ComputedFieldTargets == null
  END IF
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many entity configurations with varying numbers of computed fields and source properties
- It catches edge cases like empty source lists, properties that are both extracted and direct sources
- It provides strong guarantees that single-target and non-source behavior is unchanged

**Test Plan**: Observe behavior on UNFIXED code first for single-target scenarios and non-source scenarios, then write property-based tests capturing that behavior.

**Test Cases**:
1. **Single-target preservation**: Verify a source property contributing to one computed field gets `ComputedFieldTargets = new[] { "target" }` and `IsComputedSourceProperty` returns true
2. **Non-source preservation**: Verify a property not in any computed field's sources gets `ComputedFieldTargets = null` and `IsComputedSourceProperty` returns false
3. **Extracted-field path preservation**: Verify the extracted-field path in `IsComputedSourceProperty` continues to detect extracted properties of non-key computed fields
4. **ValidateAndProcessComputedFields preservation**: Verify recomputation and FDDB072 validation still work correctly for both single and multi-target scenarios

### Unit Tests

- Test `PropertyMetadata.ComputedFieldTargets` is null for non-source properties
- Test `PropertyMetadata.ComputedFieldTargets` contains single target for single-source properties
- Test `PropertyMetadata.ComputedFieldTargets` contains all targets for multi-source properties
- Test `IsComputedSourceProperty` returns true when `ComputedFieldTargets.Length > 0`
- Test `IsComputedSourceProperty` returns false when `ComputedFieldTargets` is null
- Test `IsComputedSourceProperty` still detects extracted-field sources

### Property-Based Tests

- Generate random entity configurations with 1-5 computed fields sharing 0-3 source properties and verify `ComputedFieldTargets` completeness
- Generate random single-target configurations and verify preservation of boolean detection behavior
- Generate random non-source properties and verify `ComputedFieldTargets` remains null

### Integration Tests

- End-to-end test: entity with shared source property, update expression assigns the shared source, verify both computed fields are recomputed in the emitted SET expression
- End-to-end test: entity with shared source property, assign shared source but omit one computed field's other required source, verify FDDB072 fires for the incomplete computed field only
- End-to-end test: entity with single-target source property, verify existing integration tests continue passing with the new array form
