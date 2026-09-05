# Typed Overload Constant SK Not Omitted — Bugfix Design

## Overview

The source generator emits uncompilable typed convenience overloads when an entity has a `[Computed]` partition key and a constant sort key (expression-body or read-only auto-property). The raw Get/Delete/Update/ConditionCheck methods correctly detect `IsConstantKey` and emit single-parameter signatures that auto-fill the SK value. However, the typed overload generation path — both the parameter resolver and the delegation-argument builders — unconditionally includes the constant SK, producing method signatures and delegation calls that reference raw methods that don't exist.

The fix adds `IsConstantKey` guards in two layers:
1. **Parameter resolution** — `OverloadParameterResolver.GetTypedOverloadParameters()` and `GetStandardOverloadParameters()` skip constant keys when building parameter lists.
2. **Delegation argument construction** — The four `GenerateTyped*Overload` methods in `TableGenerator.cs` skip constant keys when building the delegation call to the raw method.

Because both layers independently contribute to the bug (parameter list includes the extra `sK` parameter AND the delegation call forwards it), both must be fixed.

## Glossary

- **Bug_Condition (C)**: An entity has at least one `[Computed]` key AND a non-computed key that is constant (`IsConstantKey == true`). The typed overload path is entered (because `QualifiesForTypedOverload` returns true due to the computed key) but incorrectly includes the constant key as a parameter and delegation argument.
- **Property (P)**: The typed overload SHALL omit constant keys from both the parameter list and the delegation arguments, producing signatures that match the raw methods.
- **Preservation**: Entities with computed PK + normal (non-constant) SK, computed PK + computed SK, and entities without computed keys continue to generate exactly the same code as before.
- **`OverloadParameterResolver`**: Static class in `Generators/OverloadParameterResolver.cs` that resolves parameter lists for typed and standard overloads.
- **`GetTypedOverloadParameters()`**: Builds the combined PK + SK parameter list used by typed convenience overloads. Currently unconditionally includes non-computed keys.
- **`GetStandardOverloadParameters()`**: Builds the standard string-based parameter list used by `WouldBeAmbiguous()`. Currently unconditionally includes any non-null key.
- **`GenerateTyped*Overload`**: Four methods in `TableGenerator.cs` (Get, Delete, Update, ConditionCheck) that each build delegation arguments and emit the typed overload method body.
- **`IsConstantKey`**: Property on `PropertyModel` — returns `true` when `ConstantKeyValue != null`. Indicates the key value is fixed at compile time and should not appear as a user-supplied parameter.
- **Constant Key**: A key property whose value is determined at compile time via expression-body (`=> "VALUE"`) or read-only auto-property (`{ get; } = "VALUE"`). The raw methods auto-fill these values and omit them from signatures.

## Bug Details

### Bug Condition

The bug manifests when the typed overload generation path is entered for an entity that has at least one `[Computed]` key AND a non-computed key that is constant. The `OverloadParameterResolver` and `GenerateTyped*Overload` methods include the constant key without checking `IsConstantKey`, producing a typed overload that:
1. Has an extra `string sK` (or `string pK`) parameter the caller must supply
2. Delegates to a raw method with that extra argument — but the raw method omits constant keys, so no matching overload exists

**Formal Specification:**
```
FUNCTION isBugCondition(entity)
  INPUT: entity of type EntityModel
  OUTPUT: boolean
  
  LET pk = entity.PartitionKeyProperty
  LET sk = entity.SortKeyProperty
  LET hasComputedKey = (pk != null AND pk.IsComputed) OR (sk != null AND sk.IsComputed)
  LET hasConstantKey = (pk != null AND pk.IsConstantKey AND NOT pk.IsComputed)
                    OR (sk != null AND sk.IsConstantKey AND NOT sk.IsComputed)
  
  RETURN hasComputedKey AND hasConstantKey
END FUNCTION
```

### Examples

- **Computed PK + Constant SK (primary trigger)**: Entity with `[Computed("PkTenantId", "PkCompanyId")]` on PK and `public string Sk { get; } = "COUNTER#CUSTOMER_NUMBER"` on SK. Raw `Get(string pk)` omits SK. Typed overload incorrectly emits `Get(Ulid pkTenantId, Ulid pkCompanyId, string sK)` and delegates to `Get(computedPk, sK)` → CS7036.
- **Computed PK + Constant SK (Update)**: Same entity, typed `Update(Ulid, Ulid, string)` delegates to `Update(computedPk, sK)` which resolves to `Update(string, KeyCondition)` → CS1503 because `string` cannot convert to `KeyCondition`.
- **Computed PK + Normal SK (NOT affected)**: Entity with `[Computed]` PK and a normal settable SK. The SK is not constant, so the typed overload correctly includes `sK` and the raw method also accepts it.
- **Constant PK + Computed SK (defensive)**: Currently blocked by FDDB120 diagnostic, but `GetTypedOverloadParameters` should still guard for defensive correctness.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Entities with `[Computed]` PK and a normal (non-constant, non-computed) SK continue to generate typed overloads with the `sK` parameter and 2-argument delegation
- Entities with `[Computed]` PK and `[Computed]` SK continue to generate typed overloads with all source property parameters from both keys
- Entities with no `[Computed]` keys continue to not generate typed overloads
- `WouldBeAmbiguous()` continues to suppress typed overloads when parameter lists match the standard overload
- Raw Get/Delete/Update/ConditionCheck methods continue to emit single-parameter signatures with auto-filled constant SK values
- Typed async convenience methods (GetAsync, DeleteAsync) and FluentResults methods (GetAsyncResult, DeleteAsyncResult) continue to correctly delegate using the parameter list from `GetTypedOverloadParameters()`

**Scope:**
All entities where `isBugCondition(entity)` returns `false` should produce exactly the same generated code as before. This includes:
- Entities with only normal keys (no computed, no constant)
- Entities with computed keys and no constant keys
- Entities where typed overloads are suppressed by ambiguity

## Hypothesized Root Cause

Based on the bug description and code analysis, the issues are in two layers:

1. **`OverloadParameterResolver.GetTypedOverloadParameters()` (lines 82-88)**: The `else if (sk != null)` branch unconditionally adds a `"sK"` parameter for any non-computed sort key. It does not check `sk.IsConstantKey`. The same pattern exists for PK (lines 72-79) — the `else if (pk != null)` branch does not check `pk.IsConstantKey`.

2. **`OverloadParameterResolver.GetStandardOverloadParameters()` (lines 101-106)**: Unconditionally adds `"sK"` for any non-null sort key and `"pK"` for any non-null partition key, without checking `IsConstantKey`. While this doesn't directly cause the compilation error (the count mismatch happens to prevent ambiguity suppression), it is logically incorrect and could cause subtle bugs if parameter counts align differently in the future.

3. **`TableGenerator.cs` — Four `GenerateTyped*Overload` methods**: Each has an `if (sortKey != null)` block that builds delegation arguments. The `else` branch (non-computed SK) unconditionally adds `"sK"` to `delegationArgs` without checking `sortKey.IsConstantKey`. The same pattern exists for the PK branch, though the `else` branch (non-computed PK) is the less common case.

4. **No test entity exists** that combines `[Computed]` PK with a constant SK, so the compilation failure was never caught by existing API surface tests.

## Correctness Properties

Property 1: Bug Condition - Constant Keys Omitted from Typed Overloads

_For any_ entity where a key property has `IsConstantKey == true` AND at least one other key has `IsComputed == true`, the typed convenience overload generation SHALL omit the constant key from both the parameter list (via `GetTypedOverloadParameters`) and the delegation arguments (via `GenerateTyped*Overload`), producing a method signature and delegation call that match the raw method's signature which also omits constant keys.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7**

Property 2: Preservation - Non-Constant Key Entities Unchanged

_For any_ entity where no key property has `IsConstantKey == true` (normal SK, computed SK, or no SK), the typed convenience overload generation SHALL produce exactly the same parameter list, method signature, and delegation call as the unfixed code, preserving all existing typed overload behavior.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/OverloadParameterResolver.cs`

**Method**: `GetTypedOverloadParameters()`

**Specific Changes**:
1. **Add `!sk.IsConstantKey` guard on SK branch (line 87)**: Change `else if (sk != null)` to `else if (sk != null && !sk.IsConstantKey)`. This prevents the constant SK from being added to the typed parameter list.
2. **Add `!pk.IsConstantKey` guard on PK branch (line 78)**: Change `else if (pk != null)` to `else if (pk != null && !pk.IsConstantKey)`. Defensive fix for the symmetric PK case.

**Method**: `GetStandardOverloadParameters()`

**Specific Changes**:
3. **Add `!IsConstantKey` guard on PK (line 102)**: Change `if (entity.PartitionKeyProperty != null)` to `if (entity.PartitionKeyProperty != null && !entity.PartitionKeyProperty.IsConstantKey)`.
4. **Add `!IsConstantKey` guard on SK (line 104)**: Change `if (entity.SortKeyProperty != null)` to `if (entity.SortKeyProperty != null && !entity.SortKeyProperty.IsConstantKey)`.

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/TableGenerator.cs`

**Methods**: `GenerateTypedGetOverload`, `GenerateTypedDeleteOverload`, `GenerateTypedUpdateOverload`, `GenerateTypedConditionCheckOverload`

**Specific Changes** (identical pattern in all four methods):
5. **Wrap SK delegation block with `!sortKey.IsConstantKey`**: Change `if (sortKey != null)` to `if (sortKey != null && !sortKey.IsConstantKey)`. When the SK is constant, the entire SK delegation block is skipped — no `computedSk` computation, no `"sK"` argument. The raw method auto-fills the constant SK value.
6. **Wrap PK delegation block with `!partitionKey.IsConstantKey`** (defensive): In the `else` branch for non-computed PK, add a guard `!partitionKey.IsConstantKey` so constant PKs are not added to delegation args. Currently a defensive-only change since constant PK + computed SK is blocked by FDDB120.

---

**File**: `Oproto.FluentDynamoDb.ApiConsistencyTests/Entities/` (new file)

7. **Add `ComputedPkConstantSkTable.cs`**: Define a new entity with `[Computed]` PK (2+ source properties) and a constant SK (expression-body `=> "CONSTANT_SK"`). This entity will be used by the API surface compile tests.

---

**File**: `Oproto.FluentDynamoDb.ApiConsistencyTests/SingleEntityTables/` (new file)

8. **Add `ComputedPkConstantSkApiSurface.cs`**: Compile-time API surface tests verifying that typed overloads for Get, Delete, Update, ConditionCheck compile correctly with the constant SK omitted. Pattern matches `ComputedKeyTypedOverloadsApiSurface.cs` but typed overloads should have only the PK source parameters (no `sK`). Standard string overloads should also have only the PK parameter (no `sK`).

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Create a test entity combining `[Computed]` PK with a constant SK. Write compile-time API surface tests that call the typed overloads with only PK source parameters (no `sK`). Run these tests on the UNFIXED code to observe compilation failures.

**Test Cases**:
1. **Typed Get Overload**: `table.Entity.Get(param1, param2)` — expect CS7036 on unfixed code because generated signature includes extra `sK`
2. **Typed Delete Overload**: `table.Entity.Delete(param1, param2)` — expect CS7036 on unfixed code
3. **Typed Update Overload**: `table.Entity.Update(param1, param2)` — expect CS1503 on unfixed code
4. **Typed ConditionCheck Overload**: `table.Entity.ConditionCheck(param1, param2)` — expect compilation error on unfixed code
5. **Standard String Overload (single PK)**: `table.Entity.Get("pk_value")` — should compile on unfixed code (raw methods are correct)

**Expected Counterexamples**:
- Compilation errors CS7036 and CS1503 confirming the bug: typed overloads include `sK` parameter but delegate to raw methods that omit it
- Root cause confirmed: `GetTypedOverloadParameters()` returns parameter list including `sK`, and `GenerateTyped*Overload` adds `sK` to delegation args

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL entity WHERE isBugCondition(entity) DO
  typedParams := GetTypedOverloadParameters_fixed(entity)
  standardParams := GetStandardOverloadParameters_fixed(entity)
  
  // Constant keys should be absent from parameter lists
  ASSERT NOT any(p IN typedParams WHERE p.Name = "sK" AND entity.SortKeyProperty.IsConstantKey)
  ASSERT NOT any(p IN typedParams WHERE p.Name = "pK" AND entity.PartitionKeyProperty.IsConstantKey)
  ASSERT NOT any(p IN standardParams WHERE p.Name = "sK" AND entity.SortKeyProperty.IsConstantKey)
  ASSERT NOT any(p IN standardParams WHERE p.Name = "pK" AND entity.PartitionKeyProperty.IsConstantKey)
  
  // Generated typed overloads should compile (verified by API surface tests)
  ASSERT compilesSuccessfully(generatedCode)
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL entity WHERE NOT isBugCondition(entity) DO
  ASSERT GetTypedOverloadParameters_original(entity) = GetTypedOverloadParameters_fixed(entity)
  ASSERT GetStandardOverloadParameters_original(entity) = GetStandardOverloadParameters_fixed(entity)
  ASSERT generatedCode_original(entity) = generatedCode_fixed(entity)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many entity configurations automatically across the input domain
- It catches edge cases that manual unit tests might miss
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Observe behavior on UNFIXED code first for non-constant-key entities, then write property-based tests capturing that behavior.

**Test Cases**:
1. **Computed PK + Normal SK Preservation**: Verify `GetTypedOverloadParameters` returns same parameters (including `sK`) for entities with computed PK and normal SK — both before and after fix
2. **Computed PK + Computed SK Preservation**: Verify `GetTypedOverloadParameters` returns same source property parameters for entities with computed PK and computed SK — both before and after fix
3. **Standard Overload Preservation**: Verify `GetStandardOverloadParameters` returns same parameters for entities without constant keys — both before and after fix
4. **Existing API Surface Preservation**: Verify `ComputedKeyTypedOverloadsApiSurface.cs` tests continue to compile after fix (existing computed key entity with normal SK)

### Unit Tests

- Test `GetTypedOverloadParameters` with entity having computed PK + constant SK → should omit `sK`
- Test `GetTypedOverloadParameters` with entity having computed PK + normal SK → should include `sK`
- Test `GetStandardOverloadParameters` with entity having constant SK → should omit `sK`
- Test `GetStandardOverloadParameters` with entity having normal SK → should include `sK`
- Test defensive case: entity with constant PK (non-computed) → should omit `pK`

### Property-Based Tests

- Generate random entity configurations (computed/constant/normal key combinations) and verify `GetTypedOverloadParameters` only includes non-constant keys
- Generate random entity configurations and verify `GetStandardOverloadParameters` only includes non-constant keys
- For entities where `isBugCondition` is false, verify parameter lists match between fixed and unfixed implementations

### Integration Tests

- Full build of `ApiConsistencyTests` project after fix — verifies all API surface compile tests pass
- New `ComputedPkConstantSkApiSurface.cs` compile tests verifying typed overloads compile with correct signatures
- Existing `ComputedKeyTypedOverloadsApiSurface.cs` continues to compile (regression prevention)
