# Implementation Plan: Discriminator Pattern Overlap Analysis Improvement

## Overview

Improve the `PatternOverlapAnalyzer` to perform structural analysis of literal segments for Contains and Complex patterns, eliminating false-positive DISC004 errors for common multi-entity table designs. All code changes are localized to `PatternOverlapAnalyzer.cs` with corresponding unit tests, property-based tests, and integration tests.

## Tasks

- [x] 1. Add helper methods to PatternOverlapAnalyzer
  - [x] 1.1 Implement `GetLiteralSegments` helper method
    - Add private static method `GetLiteralSegments(string pattern)` that splits on `*` and returns non-empty segments
    - Example: `"EMPLOYEE#*#DEDUCTION#*"` → `["EMPLOYEE#", "#DEDUCTION#"]`
    - Example: `"*#DEDUCTION#*"` → `["#DEDUCTION#"]`
    - _Requirements: 8.1, 8.2, 8.3_

  - [x] 1.2 Implement `HasSameWildcardStructure` helper method
    - Add private static method `HasSameWildcardStructure(string patternA, string patternB)`
    - Returns true when both patterns agree on starts-with-wildcard AND ends-with-wildcard
    - Example: `"EMPLOYEE#*#DEDUCTION#*"` and `"EMPLOYEE#*#GARNISHMENT#*"` → true
    - Example: `"EMPLOYEE#*#DEDUCTION#*"` and `"*#DEDUCTION#*"` → false
    - _Requirements: 9.1, 9.2, 9.3_

  - [x] 1.3 Implement `SegmentsCanMatch` helper method
    - Add private static method `SegmentsCanMatch(string segmentA, string segmentB)`
    - Returns true if one segment is a substring of the other (using ordinal comparison)
    - Example: `"#DEDUCTION#"` vs `"#GARNISHMENT#"` → false (distinguishing)
    - Example: `"#LINE#"` vs `"#LINE#ITEM#"` → true (substring relationship)
    - _Requirements: 2.1_

  - [x] 1.4 Implement `ComplexPatternsOverlap` method
    - Add private static method `ComplexPatternsOverlap(DiscriminatorConfig a, DiscriminatorConfig b)`
    - For same-structure patterns: return false if any corresponding segment pair is distinguishing
    - For different-structure patterns: check if all segments of shorter appear in longer pattern text
    - Conservatively return true when analysis is inconclusive
    - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.2, 7.1, 7.2_

- [x] 2. Modify existing overlap detection methods
  - [x] 2.1 Modify `SameStrategyOverlap` Contains case
    - Change the Contains case from `=> true` to perform substring check
    - Return true only if `literalA.IndexOf(literalB, StringComparison.Ordinal) >= 0` OR `literalB.IndexOf(literalA, StringComparison.Ordinal) >= 0`
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 2.2 Modify `WildcardPatternsOverlap` to route Complex patterns
    - Replace the early-return `true` for Complex patterns with a call to `ComplexPatternsOverlap(a, b)`
    - Ensure the routing handles both Complex-vs-Complex and Complex-vs-simple cases
    - _Requirements: 2.3, 3.1, 3.2_

- [x] 3. Checkpoint - Verify core logic compiles and existing tests still pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Unit tests for new helper methods
  - [x] 4.1 Write unit tests for `GetLiteralSegments`
    - Create `PatternOverlapAnalyzerTests.cs` in `Analysis/` folder
    - Test `EMPLOYEE#*#DEDUCTION#*` → `["EMPLOYEE#", "#DEDUCTION#"]`
    - Test `*#DEDUCTION#*` → `["#DEDUCTION#"]`
    - Test `EMPLOYEE#*` → `["EMPLOYEE#"]`
    - Test edge case: all-wildcards pattern → empty array
    - _Requirements: 8.1, 8.2, 8.3_

  - [x] 4.2 Write unit tests for `HasSameWildcardStructure`
    - Test same structure: `"EMPLOYEE#*#DEDUCTION#*"` and `"EMPLOYEE#*#GARNISHMENT#*"` → true
    - Test different structure: `"EMPLOYEE#*#DEDUCTION#*"` and `"*#DEDUCTION#*"` → false
    - Test both start with wildcard: `"*#A#*"` and `"*#B#*"` → true
    - _Requirements: 9.1, 9.2, 9.3_

  - [x] 4.3 Write unit tests for `SegmentsCanMatch`
    - Test distinguishing segments: `"#DEDUCTION#"` vs `"#GARNISHMENT#"` → false
    - Test substring relationship: `"#LINE#"` vs `"#LINE#ITEM#"` → true
    - Test identical segments: `"EMPLOYEE#"` vs `"EMPLOYEE#"` → true
    - _Requirements: 2.1_

  - [x] 4.4 Write unit tests for `ComplexPatternsOverlap`
    - Test same-structure non-overlap: `"EMPLOYEE#*#DEDUCTION#*"` vs `"EMPLOYEE#*#GARNISHMENT#*"` → false
    - Test same-structure overlap: `"EMPLOYEE#*#LINE#*"` vs `"EMPLOYEE#*#LINE#ITEM#*"` → true
    - Test different-structure overlap (subsumption): `"EMPLOYEE#*"` (StartsWith) vs `"EMPLOYEE#*#DEDUCTION#*"` (Complex) → true
    - Test different-structure non-overlap: `"EMPLOYEE#*#PAYRATE#*"` (Complex) vs `"*#DEDUCTION#*"` (Contains) → false
    - Test conservative fallback: empty segments → true
    - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.2, 7.1, 7.2_

  - [x] 4.5 Write unit tests for modified `SameStrategyOverlap` Contains behavior
    - Test non-overlapping: `"*#DEDUCTION#*"` vs `"*#GARNISHMENT#*"` → false
    - Test overlapping (substring): `"*ORDER*"` vs `"*ORD*"` → true
    - Test identical: `"*#PAYRATE#*"` vs `"*#PAYRATE#*"` → true
    - Verify StartsWith/EndsWith behavior unchanged
    - _Requirements: 1.1, 1.2, 1.3, 5.1, 5.2_

- [x] 5. Property-based tests for correctness properties
  - [x] 5.1 Write property test for symmetry (Property 1)
    - **Property 1: Overlap detection remains symmetric**
    - Generate arbitrary DiscriminatorConfig pairs (including new Contains and Complex scenarios)
    - Assert `PatternsOverlap(A, B) == PatternsOverlap(B, A)` for all generated pairs
    - **Validates: Requirements 4.1**

  - [x] 5.2 Write property test for Contains non-overlap (Property 2)
    - **Property 2: Contains patterns with no substring relationship are non-overlapping**
    - Generate pairs of Contains patterns where neither literal is a substring of the other
    - Assert `PatternsOverlap` returns false for all generated pairs
    - **Validates: Requirements 1.1**

  - [x] 5.3 Write property test for Complex non-overlap (Property 3)
    - **Property 3: Complex patterns with same structure and a distinguishing segment are non-overlapping**
    - Generate pairs of Complex patterns with identical wildcard structure and at least one distinguishing segment pair
    - Assert `PatternsOverlap` returns false for all generated pairs
    - **Validates: Requirements 2.1**

  - [x] 5.4 Write property test for Contains substring overlap (Property 4)
    - **Property 4: Substring relationship implies overlap for Contains patterns**
    - Generate pairs of Contains patterns where one literal is a substring of the other
    - Assert `PatternsOverlap` returns true for all generated pairs
    - **Validates: Requirements 1.2**

- [x] 6. Integration tests for real-world scenarios
  - [x] 6.1 Write integration test for employee payroll Complex patterns
    - Test 4 entities: Employee (`EMPLOYEE#*`), PayRate (`EMPLOYEE#*#PAYRATE#*`), Deduction (`EMPLOYEE#*#DEDUCTION#*`), Garnishment (`EMPLOYEE#*#GARNISHMENT#*`)
    - Assert zero DISC004 diagnostics between the three Complex sibling patterns
    - Assert DISC005 diagnostics emitted for Employee overlapping with the three children
    - Assert exclusion guards assigned to Employee entity for each child
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 6.2 Write integration test for Contains-strategy variants
    - Test 3 entities with Contains patterns: `*#DEDUCTION#*`, `*#GARNISHMENT#*`, `*#PAYRATE#*`
    - Assert zero DISC004 diagnostics between any pair
    - Assert zero DISC005 diagnostics (no overlaps at all)
    - _Requirements: 6.4_

  - [x] 6.3 Update existing `AmbiguousSameScoreDiagnosticIntegrationTests`
    - Update `Analyze_OverlappingSameScoreContainsPatterns_EmitsDISC004Diagnostic` test
    - This test uses `*#DATA#*` vs `*#INFO#*` which are no longer overlapping under the new logic
    - Change assertion to expect zero DISC004 diagnostics (neither `#DATA#` nor `#INFO#` is a substring of the other)
    - _Requirements: 1.1_

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- All code changes are in a single file: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/PatternOverlapAnalyzer.cs`
- Unit tests go in: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/PatternOverlapAnalyzerTests.cs`
- Integration tests go in: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/`
- Property tests use FsCheck with xUnit integration (existing pattern in the project)
- The helper methods need `internal` visibility for direct unit testing, or use `[InternalsVisibleTo]` (already configured in the project)
- Remember to run `dotnet build-server shutdown` before testing source generator changes
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["1.4", "2.1"] },
    { "id": 2, "tasks": ["2.2"] },
    { "id": 3, "tasks": ["4.1", "4.2", "4.3", "4.5"] },
    { "id": 4, "tasks": ["4.4", "5.1", "5.2", "5.3", "5.4"] },
    { "id": 5, "tasks": ["6.1", "6.2", "6.3"] }
  ]
}
```
