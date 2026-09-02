# Implementation Plan: Compound Discrimination Internal Segment

## Overview

This plan implements a fallback resolution path in `CompoundPromotionPass.Analyze` for same-score entity pairs where prefix-based disambiguation fails because both entities reduce to the same effective cross-key prefix. When one entity's original Complex pattern contains an internal segment (e.g., `#ROLE#` in `TENANT#*#ROLE#*`) that the other entity's simpler pattern lacks, the pair becomes disambiguable via positional `IndexOf`-based compound constraints. All internal-segment constraints use `Strategy=None` with `OffsetIndex=prefixLength` to generate `IndexOf(literal, offset)` checks, preventing false matches from coincidental substring presence. The implementation modifies three files: `CompoundConstraint.cs` (add `OffsetIndex`), `CompoundPromotionPass.cs` (add `ExtractInternalSegment` and fallback logic), and `MapperGenerator.cs` (add `OffsetIndex > 0` code generation).

## Tasks

- [x] 1. Add OffsetIndex property to CompoundConstraint model
  - [x] 1.1 Add `OffsetIndex` property to `CompoundConstraint.cs`
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator/Models/CompoundConstraint.cs`
    - Add an `int OffsetIndex { get; set; }` property with default value 0
    - Add XML doc comment mirroring `ExclusionPattern.OffsetIndex`: "When greater than 0, the code generator emits `IndexOf(LiteralText, OffsetIndex) >= 0` instead of `Contains(LiteralText)`. Used for all internal-segment compound constraints to prevent false matches from coincidental substring presence in wildcard values within the prefix portion. A value of 0 (default) preserves existing Contains/StartsWith behavior for prefix-based compound constraints."
    - This is a non-breaking additive change — all existing constraints default to `OffsetIndex = 0`
    - _Requirements: 5.6_

- [x] 2. Implement internal segment extraction and fallback resolution in CompoundPromotionPass
  - [x] 2.1 Add `ExtractInternalSegment` private static method to `CompoundPromotionPass.cs`
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/CompoundPromotionPass.cs`
    - Add a new private static method with signature: `static (string LiteralText, DiscriminatorStrategy Strategy, int OffsetIndex)? ExtractInternalSegment(string complexPattern, string reducedPrefix)`
    - Algorithm (mirrors `PatternOverlapAnalyzer.CreateExclusionPattern`):
      1. Split `complexPattern` on `*`
      2. Collect non-empty segments, skip the first (the prefix segment)
      3. If no internal segments remain, return `null`
      4. Iterate from last to first internal segment; select the first segment NOT contained within `reducedPrefix` → return `(segment, None, reducedPrefix.Length)` — all segments use positional IndexOf with prefix offset
      5. If all internal segments are contained within the prefix (bare separators), return `(internalSegments[0], None, reducedPrefix.Length)`
    - Add XML doc comment describing the algorithm and its relationship to `PatternOverlapAnalyzer.CreateExclusionPattern`
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 2.2 Add `AssignInternalSegmentConstraint` private static helper method
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/CompoundPromotionPass.cs`
    - Add a helper that creates a `CompoundConstraint` from an extracted internal segment tuple, reusing the existing multi-overlap accumulation logic (positive takes precedence, exclusions accumulate in `AdditionalExclusions`)
    - For positive constraints: set `IsExclusion = false`, `Strategy` and `LiteralText` and `OffsetIndex` from the segment tuple, `Pattern` from the entity's original `DerivedDiscriminatorPattern`
    - For exclusion constraints: set `IsExclusion = true`, same strategy/literal/offset, `ExclusionSourceEntity` = source entity class name
    - Respect existing idempotency rules: if entity already has a positive constraint, skip; if entity already has an exclusion, accumulate in `AdditionalExclusions`
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 7.1, 7.2, 7.3_

  - [x] 2.3 Add internal-segment fallback logic in `Analyze` method
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/CompoundPromotionPass.cs`
    - After the existing `if (!AreDisambiguable(...)) { continue; }` check, insert the fallback path:
      1. Check if both effective patterns are non-null and identical (same prefix)
      2. Retrieve original `DerivedDiscriminatorPattern` for each entity from the cross-key `PropertyModel`
      3. Check if original pattern is Complex via `DiscriminatorAnalyzer.DeterminePatternStrategy`
      4. Extract internal segments from Complex patterns using `ExtractInternalSegment`
      5. Handle three cases: (a) one has segment, other doesn't → positive positional to complex, exclusion positional to simple; (b) both have different segments → positive positional to each; (c) same/no segments → not disambiguable
      6. On resolution: add to `ResolvedPairs`, emit FDDB104 diagnostics for both entities
    - The fallback must NOT fire when both patterns are null (already handled), when patterns already differ (resolved by existing path), or when `AreDisambiguable` already returned true
    - Restructure the `continue` after `AreDisambiguable` returns false to allow the fallback path to execute before continuing
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.7_

  - [x] 2.4 Build and verify compilation
    - Run `dotnet build` from workspace root to ensure all changes compile cleanly
    - Run `dotnet build-server shutdown` first to clear cached source generator
    - Verify no new warnings or errors introduced
    - _Requirements: 5.4, 5.5_

- [x] 3. Extend MapperGenerator to handle OffsetIndex on CompoundConstraint
  - [x] 3.1 Update `GeneratePositiveCompoundConstraintCheck` for `OffsetIndex > 0`
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - In `GeneratePositiveCompoundConstraintCheck`, add a case for `OffsetIndex > 0`:
      - When `OffsetIndex > 0`: emit `if (compoundValue.S.IndexOf("{literal}", {offset}) < 0) return false;`
      - When `OffsetIndex == 0`: preserve existing strategy-based code generation (StartsWith, Contains, etc.)
    - _Requirements: 3.5, 6.1, 6.2_

  - [x] 3.2 Update `GenerateSingleExclusionCheck` for `OffsetIndex > 0`
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - In `GenerateSingleExclusionCheck`, add handling for `Strategy == None && OffsetIndex > 0`:
      - Emit `if (item.TryGetValue("{prop}", out var {varName}) && {varName}.S != null && {varName}.S.IndexOf("{literal}", {offset}) >= 0) return false;`
    - _Requirements: 3.6, 6.1, 6.2_

  - [x] 3.3 Build and verify compilation
    - Run `dotnet build-server shutdown` then `dotnet build` from workspace root
    - Verify all existing tests still pass: `dotnet test` from `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/`
    - _Requirements: 5.4, 5.5_

- [x] 4. Checkpoint — Verify existing tests pass
  - Run `dotnet build-server shutdown` then `dotnet test` from `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/`
  - All existing tests in `CompoundPromotionPassTests.cs`, `CompoundPromotionPassPropertyTests.cs`, and `CompoundPromotionPassComplexPatternTests.cs` must pass without modification
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Write unit tests for internal-segment resolution
  - [x] 5.1 Create `CompoundPromotionPassInternalSegmentTests.cs` with example-based tests
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/CompoundPromotionPassInternalSegmentTests.cs`
    - Add `[Trait("Feature", "compound-discrimination-internal-segment")]` and `[Trait("Category", "Unit")]`
    - Use xUnit `[Fact]` tests with AwesomeAssertions
    - **Test 1: Complex-vs-StartsWith same prefix** — `TENANT#*#ROLE#*` vs `TENANT#*`. Verify: pair resolved, complex entity gets positive positional constraint with `Strategy=None`, `LiteralText="#ROLE#"`, `OffsetIndex=7`, simple entity gets exclusion positional constraint with same parameters, `IsExclusion=true`, `ExclusionSourceEntity` set correctly
    - **Test 2: Both Complex, same prefix, different segments** — `TENANT#*#ROLE#*` vs `TENANT#*#DEPT#*`. Verify: pair resolved, each entity gets positive positional constraint with `Strategy=None`, its respective internal segment, and `OffsetIndex=7`
    - **Test 3: Both Complex, same prefix, same segment** — `TENANT#*#ROLE#*` vs `TENANT#*#ROLE#*`. Verify: pair NOT resolved, no `CompoundConstraint` assigned
    - **Test 4: Bare-separator positional** — `CAP#*#*` vs `CAP#*`. Verify: pair resolved, complex entity gets `Strategy=None`, `LiteralText="#"`, `OffsetIndex=4`, simple entity gets exclusion with same parameters (same approach as meaningful segments — all use positional IndexOf)
    - **Test 5: Three-entity multi-overlap** — Entity A (`TENANT#*#ROLE#*`) overlaps with both B (`TENANT#*`) and C (`TENANT#*`). Verify: A gets positive positional constraint, B and C each get exclusion; C's exclusion accumulated in B's `AdditionalExclusions` or assigned directly
    - **Test 6: Mixed resolution** — Entity A resolved with B via prefix (different prefixes) and with C via internal segment (same prefix). Verify: A retains `StartsWith` positive constraint from (A,B) pair; C gets exclusion positional constraint from (A,C) pair
    - Reuse the `CreateEntity` helper pattern from existing `CompoundPromotionPassTests.cs`
    - _Requirements: 1.1, 1.3, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 3.5, 3.6, 7.1, 7.2, 7.3, 7.4_

  - [x] 5.2 Write property test for Complex-vs-Non-Complex same-prefix resolution (Property 1)
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/CompoundPromotionPassInternalSegmentTests.cs`
    - **Property 1: Complex-vs-Non-Complex Same-Prefix Resolution**
    - **Validates: Requirements 1.1, 3.1, 3.2, 3.4**
    - Use `[Property(MaxTest = 100)]` with FsCheck, `[Trait("Feature", "compound-discrimination-internal-segment")]`, `[Trait("Category", "Property")]`
    - Generate pairs where entity A has Complex PK pattern `{PREFIX}#*#{SUFFIX}#*` and entity B has simple PK pattern `{PREFIX}#*` (same prefix, suffix not contained in prefix)
    - Assert: pair resolved, entity A gets positive positional constraint with `Strategy=None`, `LiteralText` = `#{SUFFIX}#`, `OffsetIndex` = prefix length, entity B gets exclusion positional constraint with same parameters

  - [x] 5.3 Write property test for dual-Complex same-prefix different-segment resolution (Property 2)
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/CompoundPromotionPassInternalSegmentTests.cs`
    - **Property 2: Dual-Complex Same-Prefix Different-Segment Resolution**
    - **Validates: Requirements 1.1, 3.3**
    - Generate pairs where both entities have Complex PK patterns with the same prefix but different internal segments (e.g., `{PREFIX}#*#{SUFFIX_A}#*` vs `{PREFIX}#*#{SUFFIX_B}#*` where `SUFFIX_A != SUFFIX_B`)
    - Assert: pair resolved, each entity gets positive positional constraint with `Strategy=None`, its respective internal segment, and `OffsetIndex` = prefix length

  - [x] 5.4 Write property test for same-prefix identical-segment non-resolution (Property 3)
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/CompoundPromotionPassInternalSegmentTests.cs`
    - **Property 3: Same-Prefix Identical-Segment Non-Resolution**
    - **Validates: Requirements 1.3**
    - Generate pairs where both entities have Complex PK patterns with the same prefix AND the same internal segment
    - Assert: pair NOT resolved, no `CompoundConstraint` assigned to either entity

  - [x] 5.5 Write property test for internal segment extraction correctness (Property 4)
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/CompoundPromotionPassInternalSegmentTests.cs`
    - **Property 4: Internal Segment Extraction Correctness**
    - **Validates: Requirements 2.1, 2.2**
    - Generate Complex patterns with multiple internal segments (e.g., `{PREFIX}#*#{SEG_A}#*#{SEG_B}#*`)
    - Assert: the extraction selects the last meaningful segment (iterating from end), matching `PatternOverlapAnalyzer.CreateExclusionPattern` selection order

  - [x] 5.6 Write property test for bare-separator positional constraint (Property 5)
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/CompoundPromotionPassInternalSegmentTests.cs`
    - **Property 5: Bare-Separator Positional Constraint**
    - **Validates: Requirements 2.3, 3.5, 3.6**
    - Generate pairs with bare-separator Complex patterns (e.g., `{PREFIX}#*#*`) vs same-prefix simple patterns
    - Assert: pair resolved, constraints use `Strategy=None`, `OffsetIndex` = prefix length, `LiteralText` = bare separator

  - [x] 5.7 Write property test for diagnostic behavior (Property 6)
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/CompoundPromotionPassInternalSegmentTests.cs`
    - **Property 6: Diagnostic Behavior for Internally-Resolved Pairs**
    - **Validates: Requirements 4.1, 4.2**
    - Generate internally-resolved pairs
    - Assert: pair in `ResolvedPairs`, exactly 2 FDDB104 diagnostics emitted (one per entity), no FDDB102/DISC004 for resolved pairs

  - [x] 5.8 Write property test for preservation of existing behavior (Property 8)
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/CompoundPromotionPassInternalSegmentTests.cs`
    - **Property 8: Preservation of Existing Behavior**
    - **Validates: Requirements 1.2, 1.4, 4.3, 5.1, 5.2, 5.3, 5.7**
    - Generate pairs that do NOT trigger internal-segment fallback (both null, different prefixes, one null one non-null, neither Complex)
    - Assert: same resolution behavior as before (same `ResolvedPairs`, same constraint assignments)
    - Run existing tests from `CompoundPromotionPassPropertyTests.cs` and `CompoundPromotionPassComplexPatternTests.cs` as additional verification

- [x] 6. Checkpoint — Run full test suite
  - Run `dotnet build-server shutdown` then `dotnet test` from `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/`
  - Verify all new unit tests pass, all new property tests pass, all existing tests still pass
  - Run filtered: `dotnet test --filter "Feature=compound-discrimination-internal-segment"` to confirm new tests
  - Run existing: `dotnet test --filter "Feature=compound-key-discrimination"` to confirm preservation
  - Run existing: `dotnet test --filter "Feature=compound-discrimination-complex-pattern-fix"` to confirm no regressions
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The `ExtractInternalSegment` algorithm mirrors `PatternOverlapAnalyzer.CreateExclusionPattern` — split on `*`, skip prefix, iterate last-to-first, select first segment not in prefix
- The code generator already handles `Contains` for compound constraints — `OffsetIndex > 0` handling uses `IndexOf(literal, offset)` for all internal-segment constraints to prevent false substring matches
- All existing tests must continue to pass without modification (preservation guarantee)
- Use `dotnet build-server shutdown` before builds when modifying the source generator to clear cached versions

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "3.1", "3.2"] },
    { "id": 2, "tasks": ["2.2", "2.3", "3.3"] },
    { "id": 3, "tasks": ["2.4"] },
    { "id": 4, "tasks": ["5.1"] },
    { "id": 5, "tasks": ["5.2", "5.3", "5.4", "5.5", "5.6", "5.7", "5.8"] }
  ]
}
```
