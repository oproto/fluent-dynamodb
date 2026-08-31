# Implementation Plan

## Overview

Fix the generated `Extract{Property}Components()` methods and `FromDynamoDb` hydration code for entities using `[Computed(..., Format = "...")]` with constant literal segments. The current code uses the `[Extracted]` attribute's `Index` (placeholder position) directly as the `parts[]` array index after splitting, which is only correct when there are no constant segments. The fix introduces a shared utility that maps placeholder indices to split indices by parsing the format string, then both `KeysGenerator` and `MapperGenerator` use this mapping when `HasCustomFormat == true`.

## Tasks

- [x] 1. Write bug condition exploration tests
  - **Property 1: Bug Condition** — Format-string extraction uses placeholder index as split index
  - **CRITICAL**: These tests MUST FAIL on unfixed code — failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: These tests encode the expected behavior — they will validate the fix when they pass after implementation
  - Create test file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/ExtractComponentsNaiveSplitIndexingBugConditionTests.cs`
  - Follow the pattern in `ExtractedPropertyTypeConversionBugExplorationTests.cs` — construct `EntityModel` objects programmatically and call `KeysGenerator.GenerateKeysClass()` / `MapperGenerator.GenerateEntityImplementation()`
  - **KeysGenerator single variable with leading constant**: Entity with `Format = "TENANT#{0}#EXTERNAL_ACCESS"` and `[Extracted("Pk", 0)]` — assert generated code contains `parts[1]` (split index), NOT `parts[0]` (placeholder index)
  - **KeysGenerator multiple variables with interspersed constants**: Entity with `Format = "TENANT#{0}#SHARE#RESOURCE#{1}#{2}"` and three extracted properties — assert generated code contains `parts[1]`, `parts[4]`, `parts[5]`
  - **KeysGenerator format specifier**: Entity with `Format = "SEQ#{0:D4}"` and `[Extracted("Sk", 0)]` — assert generated code contains `parts[1]`, NOT `parts[0]`
  - **MapperGenerator single variable with leading constant**: Same entity as above — assert `FromDynamoDb` hydration code contains `pkParts[1]`, NOT `pkParts[0]`
  - **MapperGenerator multiple variables**: Same multi-variable entity — assert hydration code uses `pkParts[1]`, `pkParts[4]`, `pkParts[5]`
  - **KeysGenerator bounds check uses max split index**: For the multi-variable entity, assert bounds check is `parts.Length <= 5`, NOT `parts.Length <= 2`
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests FAIL (confirms bug exists — current code uses placeholder index directly)
  - Mark task complete when tests are written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Write preservation tests (BEFORE implementing fix)
  - **Property 3: Preservation** — Separator-based extraction is unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Create test file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/ExtractComponentsNaiveSplitIndexingPreservationTests.cs`
  - Follow the pattern in `ExtractedPropertyTypeConversionPreservationTests.cs`
  - Observe: separator-based entity with `Separator = "#"` and sources `["TenantId", "UserId"]` — `KeysGenerator.GenerateKeysClass()` produces `parts[0]` and `parts[1]` on unfixed code
  - Observe: separator-based entity — `MapperGenerator.GenerateEntityImplementation()` produces `pkParts[0]` and `pkParts[1]` on unfixed code
  - Observe: separator-based entity with three sources `["Year", "Month", "Label"]` — extraction uses `parts[0]`, `parts[1]`, `parts[2]` on unfixed code
  - Observe: string extracted properties from separator-based computed keys use direct assignment (no Parse)
  - Observe: int extracted properties from separator-based computed keys use `int.Parse(parts[N])`
  - Observe: enum extracted properties use `Enum.Parse<T>(parts[N])`
  - Write tests asserting all the above observations hold
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 3. Implement the fix

  - [x] 3.1 Add `FormatPlaceholderMapper` shared utility
    - Create file: `Oproto.FluentDynamoDb.SourceGenerator/Utilities/FormatPlaceholderMapper.cs`
    - Implement `BuildPlaceholderToSplitIndexMap(string format, char separator)` returning `Dictionary<int, int>`
    - Split the format string on the separator character
    - For each segment, match against regex `^\{(\d+)(?::.*?)?\}$` to detect `{N}` and `{N:format}` patterns
    - Build dictionary mapping placeholder index → split position
    - Implement convenience method `GetSplitIndex(string format, char separator, int placeholderIndex)` that calls `BuildPlaceholderToSplitIndexMap` and looks up the given index
    - _Requirements: 2.3, 2.5_

  - [x] 3.2 Fix `KeysGenerator.GenerateExtractionHelper()` to use mapped split indices
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/KeysGenerator.cs`
    - In `GenerateExtractionHelper()`, after resolving the source property, check `sourceProperty.ComputedKey?.HasCustomFormat == true`
    - If true: call `FormatPlaceholderMapper.BuildPlaceholderToSplitIndexMap()` with the format string and separator character
    - For single-property return: use `mapping[extractedProperty.ExtractedKey.Index]` instead of `extractedProperty.ExtractedKey.Index` for `parts[]` access and bounds check
    - For tuple return: use `mapping[p.ExtractedKey.Index]` for each property's `parts[]` access, and use `returnProperties.Max(p => mapping[p.ExtractedKey.Index])` for the max index bounds check
    - If false (separator-based): leave existing code path unchanged
    - _Bug_Condition: parts[placeholderIndex] returns constant literal instead of variable value_
    - _Expected_Behavior: parts[mapping[placeholderIndex]] returns the correct variable value_
    - _Preservation: Separator-based keys use unchanged code path_
    - _Requirements: 2.1, 2.4, 3.1, 3.2_

  - [x] 3.3 Fix `MapperGenerator.GenerateExtractedKeyLogic()` to use mapped split indices
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - In `GenerateExtractedKeyLogic()`, the method currently only receives the `extractedProperty` — it needs access to the source property's `ComputedKey` to check `HasCustomFormat`
    - Option A: Pass the `EntityModel` or source `PropertyModel` as an additional parameter
    - Option B: Look up the source property from the entity context already available in the calling code
    - Once the source property's `ComputedKey` is available, check `HasCustomFormat == true`
    - If true: call `FormatPlaceholderMapper.GetSplitIndex()` to get the correct split index and use it instead of `extractedKey.Index`
    - Update the bounds check `{partsVariable}.Length > {index}` to use the mapped split index
    - If false (separator-based): leave existing code path unchanged
    - _Bug_Condition: pkParts[placeholderIndex] assigns constant literal to extracted property_
    - _Expected_Behavior: pkParts[mapping[placeholderIndex]] assigns the correct variable value_
    - _Preservation: Separator-based keys use unchanged code path_
    - _Requirements: 2.2, 2.4, 3.3_

  - [x] 3.4 Verify bug condition exploration tests now pass
    - **Property 1: Expected Behavior** — format-string extraction uses correct split index
    - **IMPORTANT**: Re-run the SAME tests from task 1 — do NOT write new tests
    - Run `dotnet build-server shutdown` then `dotnet test --filter "FullyQualifiedName~ExtractComponentsNaiveSplitIndexingBugCondition"`
    - **EXPECTED OUTCOME**: Tests PASS (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 3: Preservation** — Separator-based extraction is unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 — do NOT write new tests
    - Run `dotnet test --filter "FullyQualifiedName~ExtractComponentsNaiveSplitIndexingPreservation"`
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 4. Write unit tests for the shared utility
  - [x] 4.1 Write unit tests for `FormatPlaceholderMapper`
    - Create test file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Utilities/FormatPlaceholderMapperTests.cs`
    - **Property 2: Placeholder-to-split-index mapping correctness**
    - **Validates: Requirements 2.1, 2.2, 2.3**
    - Test `BuildPlaceholderToSplitIndexMap("TENANT#{0}#EXTERNAL_ACCESS", '#')` → `{0: 1}`
    - Test `BuildPlaceholderToSplitIndexMap("TENANT#{0}#ROLE#{1}", '#')` → `{0: 1, 1: 3}`
    - Test `BuildPlaceholderToSplitIndexMap("TENANT#{0}#SHARE#RESOURCE#{1}#{2}", '#')` → `{0: 1, 1: 4, 2: 5}`
    - Test `BuildPlaceholderToSplitIndexMap("CAP#{0}#{1}", '#')` → `{0: 1, 1: 2}`
    - Test `BuildPlaceholderToSplitIndexMap("SEQ#{0:D4}", '#')` → `{0: 1}` (format specifier)
    - Test `BuildPlaceholderToSplitIndexMap("ENTRY#{0:yyyy-MM-dd}", '#')` → `{0: 1}` (date format specifier)
    - Test `BuildPlaceholderToSplitIndexMap("{0}#{1}#{2}", '#')` → `{0: 0, 1: 1, 2: 2}` (no constants — identical to separator-based)
    - Test `GetSplitIndex("TENANT#{0}#EXTERNAL_ACCESS", '#', 0)` → `1`
    - Test `GetSplitIndex("TENANT#{0}#SHARE#RESOURCE#{1}#{2}", '#', 2)` → `5`

- [x] 5. Write MapperGenerator hydration path tests for format-string entities
  - [x] 5.1 Write tests verifying MapperGenerator produces correct hydration code for format-string entities
    - Create test file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/ExtractComponentsFormatStringHydrationTests.cs`
    - Single variable with leading constant: Entity with `Format = "TENANT#{0}#EXTERNAL_ACCESS"` — assert `FromDynamoDb` contains `pkParts[1]` with direct string assignment
    - Multiple variables with interspersed constants: Entity with `Format = "TENANT#{0}#ROLE#{1}"` — assert `FromDynamoDb` contains `pkParts[1]` and `pkParts[3]`
    - Sort key with leading constant: Entity with SK `Format = "CAP#{0}#{1}"` — assert `FromDynamoDb` contains `skParts[1]` and `skParts[2]`
    - Format specifier: Entity with SK `Format = "SEQ#{0:D4}"` with int extracted property — assert `FromDynamoDb` contains `int.Parse(skParts[1])`
    - _Requirements: 2.2, 2.3, 2.4, 3.5_

- [x] 6. Update CHANGELOG.md
  - Add entry under the appropriate version section (or `[Unreleased]`)
  - Section: **Fixed**
  - Entry: `Extract{Property}Components()` and `FromDynamoDb` hydration now use the correct split index for entities with `[Computed(..., Format = "...")]` format strings containing constant literal segments — previously used the placeholder index `{N}` directly as the `parts[]` array index, which returned constant segments instead of variable values
  - Reference the issue/spec: `extract-components-naive-split-indexing`
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 7. Checkpoint - Ensure all tests pass
  - Run `dotnet build-server shutdown` then `dotnet build` to verify no compilation errors with fresh generator
  - Run `dotnet test` to verify all tests pass
  - Verify bug condition tests from task 1 now pass (confirms fix works)
  - Verify preservation tests from task 2 still pass (confirms separator-based extraction unchanged)
  - Verify existing tests from the `extracted-property-type-conversion` spec still pass (previous fixes unbroken)
  - Ensure no existing tests in the project are broken by the changes
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- The source generator caches in memory; run `dotnet build-server shutdown` before rebuilding after generator changes
- Tests verify GENERATED code output (string assertions on generated C# source), not runtime behavior
- The fix is separator-agnostic: works for any separator character (#, _, :, -, etc.)
- Separator-based computed keys (no `Format` property) must NOT be affected — they have no constant segments so placeholder index == split index already
- Both `KeysGenerator.GenerateExtractionHelper()` and `MapperGenerator.GenerateExtractedKeyLogic()` need the fix — use the shared `FormatPlaceholderMapper` utility
- `MapperGenerator.GenerateExtractedKeyLogic()` currently only takes a `PropertyModel` parameter — it needs access to the source property's `ComputedKey` to check `HasCustomFormat`, so the calling code may need to pass additional context
- The `FormatPlaceholderMapper` utility should be placed in `Oproto.FluentDynamoDb.SourceGenerator/Utilities/` alongside the existing `FormatSpecifierHelper.cs`
- Test projects: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests` for generator unit tests

## Task Dependency Graph

```json
{
  "waves": [
    { "wave": 1, "tasks": ["1", "2"] },
    { "wave": 2, "tasks": ["3.1"] },
    { "wave": 3, "tasks": ["3.2", "3.3"] },
    { "wave": 4, "tasks": ["3.4", "3.5"] },
    { "wave": 5, "tasks": ["4.1", "5.1"] },
    { "wave": 6, "tasks": ["6"] },
    { "wave": 7, "tasks": ["7"] }
  ]
}
```
