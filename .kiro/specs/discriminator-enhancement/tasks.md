# Implementation Plan: Discriminator Enhancement — Most-Specific Pattern Matching

## Overview

This implementation adds compile-time pattern overlap analysis to the source generator so that overlapping discriminator patterns on multi-entity tables produce mutually exclusive `MatchesEntity` methods. The work proceeds from pure functions (scoring, overlap detection) through the analyzer, into code generation modifications, diagnostics, and finally integration tests and example updates.

## Tasks

- [x] 1. Add ExclusionPattern model and extend DiscriminatorConfig
  - [x] 1.1 Create `ExclusionPattern` class in `Oproto.FluentDynamoDb.SourceGenerator/Models/ExclusionPattern.cs`
    - Define `EntityName`, `Pattern`, `Strategy`, and `LiteralText` properties as specified in the design
    - _Requirements: 3.1, 3.4_

  - [x] 1.2 Add `OverlappingPatterns` property to `DiscriminatorConfig`
    - Add `public List<ExclusionPattern> OverlappingPatterns { get; set; } = new();` to the existing class
    - _Requirements: 1.4, 1.7_

- [x] 2. Implement PatternOverlapAnalyzer with property-based tests
  - [x] 2.1 Create `PatternOverlapAnalyzer` static class in `Oproto.FluentDynamoDb.SourceGenerator/Analysis/PatternOverlapAnalyzer.cs`
    - Implement `ComputeSpecificityScore(DiscriminatorConfig config)` — split pattern on `*`, count non-empty segments; ExactMatch returns `int.MaxValue`
    - Implement `PatternsOverlap(DiscriminatorConfig a, DiscriminatorConfig b)` — structural overlap detection; different properties never overlap; conservative approach for ambiguous cases
    - Implement `Analyze(List<EntityModel> tableEntities)` — iterate entity pairs within a table group, compute scores, detect overlaps, populate `OverlappingPatterns` on less-specific entities, return diagnostics
    - _Requirements: 1.3, 1.6, 1.7, 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 2.2 Write property test for specificity scoring
    - **Property 1: Specificity score equals non-empty literal segment count**
    - **Validates: Requirements 1.3, 2.2**
    - Generate arbitrary pattern strings containing `*` characters, verify score equals `pattern.Split('*').Count(s => s.Length > 0)`
    - Use FsCheck with xUnit, minimum 100 iterations

  - [x] 2.3 Write property test for ExactMatch precedence
    - **Property 2: ExactMatch always scores higher than any wildcard pattern**
    - **Validates: Requirements 2.4**
    - Generate arbitrary wildcard patterns, verify `ComputeSpecificityScore` for ExactMatch is strictly greater

  - [x] 2.4 Write property test for overlap symmetry and property scoping
    - **Property 3: Overlap detection is symmetric and property-scoped**
    - **Validates: Requirements 1.6, 2.1**
    - Generate arbitrary pairs of `DiscriminatorConfig`, verify `PatternsOverlap(A, B) == PatternsOverlap(B, A)` and that different properties always return false

  - [x] 2.5 Write property test for exclusion list correctness
    - **Property 5: Exclusion list contains all and only higher-scoring overlapping patterns**
    - **Validates: Requirements 1.7, 3.4**
    - Generate table groups with known overlapping patterns, run `Analyze`, verify each entity's `OverlappingPatterns` contains exactly the higher-score overlapping entries

  - [x] 2.6 Write property test for ambiguous same-score diagnostics
    - **Property 8: Ambiguous same-score overlaps produce an error diagnostic**
    - **Validates: Requirements 2.3**
    - Generate entity pairs with overlapping patterns of equal score, verify `Analyze` returns at least one Error-severity diagnostic mentioning both entity names

  - [x] 2.7 Write property test for resolved overlap diagnostics
    - **Property 9: Resolved overlaps produce an informational diagnostic**
    - **Validates: Requirements 2.5**
    - Generate entity pairs with overlapping patterns of different scores, verify `Analyze` returns an Info-severity diagnostic mentioning the less-specific entity, the more-specific entity, and the excluded pattern

- [x] 3. Checkpoint — Verify analyzer builds and property tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Add diagnostic descriptors for overlap analysis
  - [x] 4.1 Add `DISC004` and `DISC005` diagnostic descriptors to the existing `DiagnosticDescriptors` class
    - `DISC004`: Error severity — "Ambiguous overlapping discriminator patterns: '{0}' on {1} and '{2}' on {3} have the same specificity score on property '{4}'"
    - `DISC005`: Info severity — "Overlapping discriminator pattern resolved: {0} excludes pattern '{1}' from more-specific entity {2}"
    - _Requirements: 2.3, 2.5_

- [x] 5. Integrate PatternOverlapAnalyzer into DynamoDbSourceGenerator.Execute
  - [x] 5.1 Add overlap analysis pass in `DynamoDbSourceGenerator.Execute`
    - After `TableEntityCount` population and before the per-entity generation loop, group entities by table name, call `PatternOverlapAnalyzer.Analyze` for each group, and report returned diagnostics via `context.ReportDiagnostic`
    - Skip single-entity tables and entities without valid discriminators
    - _Requirements: 2.1, 2.3, 2.5, 4.3_

- [x] 6. Modify GenerateDiscriminatorCheck to emit exclusion guards
  - [x] 6.1 Update `MapperGenerator.GenerateDiscriminatorCheck` to handle `OverlappingPatterns`
    - When `entity.Discriminator.OverlappingPatterns` is non-empty, restructure generated code: emit positive match check first (return false if not matching), then for each exclusion pattern emit a return-false guard using the correct string operation (StartsWith, EndsWith, Contains, or equality), then return true
    - When `OverlappingPatterns` is empty, preserve existing code generation behavior exactly
    - Include a comment in generated code identifying each exclusion pattern's source entity and score
    - _Requirements: 1.1, 1.4, 1.5, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2_

  - [x] 6.2 Write property test for exclusion guard string operation correctness
    - **Property 6: Exclusion guard uses the correct string operation**
    - **Validates: Requirements 3.1, 3.2**
    - Generate exclusion patterns with various strategies, verify the generated code contains the corresponding string method call (StartsWith, EndsWith, Contains)

  - [x] 6.3 Write property test for non-overlapping entities producing no exclusion logic
    - **Property 7: Non-overlapping entities produce no exclusion logic or overlap diagnostics**
    - **Validates: Requirements 1.5, 4.1, 4.3, 4.4**
    - Generate table groups with non-overlapping patterns, verify generated `MatchesEntity` contains no exclusion guards and no DISC004/DISC005 diagnostics are emitted

- [x] 7. Checkpoint — Verify full build succeeds and all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Write integration tests verifying generated code
  - [x] 8.1 Write integration test for two-entity hierarchy (Invoice + InvoiceLine)
    - Define test entities with `INVOICE#*` and `INVOICE#*#LINE#*` patterns on the same table
    - Run the source generator, compile the output
    - Verify `InvoiceLine.MatchesEntity` returns true for `"INVOICE#001#LINE#1"` and false for `"INVOICE#001"`
    - Verify `Invoice.MatchesEntity` returns true for `"INVOICE#001"` and false for `"INVOICE#001#LINE#1"`
    - _Requirements: 1.1, 1.2, 1.4_

  - [x] 8.2 Write integration test for three-entity hierarchy
    - Define entities with `INVOICE#*`, `INVOICE#*#LINE#*`, and `INVOICE#*#LINE#*#ADJUSTMENT#*` patterns
    - Verify each entity claims only its intended items and excludes all more-specific patterns
    - _Requirements: 1.7_

  - [x] 8.3 Write integration test for non-overlapping patterns (backward compatibility)
    - Define entities with `USER#*` and `ORDER#*` patterns on the same table
    - Verify generated code is identical to pre-enhancement behavior (no exclusion guards)
    - Verify no DISC004 or DISC005 diagnostics are emitted
    - _Requirements: 4.1, 4.4_

  - [x] 8.4 Write integration test for ambiguous same-score error diagnostic
    - Define two entities with `*#AUDIT` and `*#LOG` patterns (both score 1, both EndsWith, but non-overlapping) — verify no error
    - Define two entities with patterns that overlap and have same score — verify DISC004 error is emitted
    - _Requirements: 2.3_

  - [x] 8.5 Write property test for mutual exclusivity of MatchesEntity
    - **Property 4: Mutual exclusivity of MatchesEntity across overlapping entities**
    - **Validates: Requirements 1.1, 1.4**
    - Generate random discriminator string values that match at least one entity's pattern in a test hierarchy, verify exactly one entity's MatchesEntity logic claims each value

- [x] 9. Update InvoiceManager example to use sort key pattern discriminators
  - [x] 9.1 Modify InvoiceManager example entities to replace `entity_type` attribute with sort key pattern discriminators
    - Update `Invoice` entity to use `DiscriminatorProperty = "sk", DiscriminatorPattern = "INVOICE#*"` instead of `EntityDiscriminator = "invoice"`
    - Update `InvoiceLine` entity to use `DiscriminatorProperty = "sk", DiscriminatorPattern = "INVOICE#*#LINE#*"`
    - Remove `entity_type` attribute from example entities if it was only used for discrimination
    - Verify the example still compiles and generated code is correct
    - _Requirements: 1.1, 1.2_

- [x] 10. Update CHANGELOG and documentation
  - Add entry to `CHANGELOG.md` under `[Unreleased]` > `### Added` describing the most-specific pattern matching feature
  - Update `docs/advanced-topics/Discriminators.md` with a new section documenting overlapping pattern behavior and examples
  - Update `docs/DOCUMENTATION_CHANGELOG.md` with entries for the Discriminators.md changes
  - Include usage example showing `INVOICE#*` and `INVOICE#*#LINE#*` on the same table working without `entity_type` attribute

- [x] 11. Final checkpoint — Full test suite and build verification
  - Run `dotnet build-server shutdown && dotnet build` and `dotnet test` to ensure everything passes
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit/integration tests validate specific examples and edge cases
- The source generator must be restarted (`dotnet build-server shutdown`) before testing changes
- FsCheck is the PBT library; tests integrate with xUnit and use AwesomeAssertions for readable assertions
- All new files go in the `Oproto.FluentDynamoDb.SourceGenerator` project (implementation) or `Oproto.FluentDynamoDb.SourceGenerator.UnitTests` project (tests)

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "4.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["2.1"] },
    { "id": 3, "tasks": ["2.2", "2.3", "2.4", "2.5", "2.6", "2.7"] },
    { "id": 4, "tasks": ["5.1"] },
    { "id": 5, "tasks": ["6.1"] },
    { "id": 6, "tasks": ["6.2", "6.3"] },
    { "id": 7, "tasks": ["8.1", "8.2", "8.3", "8.4", "8.5"] },
    { "id": 8, "tasks": ["9.1"] },
    { "id": 9, "tasks": ["10"] },
    { "id": 10, "tasks": ["11"] }
  ]
}
```
