# Implementation Plan: Compound Key Discrimination

## Overview

Implement the CompoundPromotionPass that resolves same-score discriminator overlaps by inspecting cross-key `DerivedDiscriminatorPattern` values. The pass runs after `PatternOverlapAnalyzer.Analyze` and assigns compound constraints or exclusion guards to entities, enabling mutually exclusive `MatchesEntity` code generation without false FDDB102/DISC004 diagnostics.

## Tasks

- [x] 1. Create CompoundConstraint model and extend DiscriminatorConfig
  - [x] 1.1 Create `CompoundConstraint` class in Models/
    - Create `Oproto.FluentDynamoDb.SourceGenerator/Models/CompoundConstraint.cs`
    - Define properties: `PropertyName`, `Pattern`, `Strategy`, `LiteralText`, `IsExclusion`, `ExclusionSourceEntity`
    - Add `AdditionalExclusions` list for multi-overlap exclusion scenarios
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 1.2 Add `CompoundConstraint` property to `DiscriminatorConfig`
    - Add `public CompoundConstraint? CompoundConstraint { get; set; }` to `Oproto.FluentDynamoDb.SourceGenerator/Models/DiscriminatorConfig.cs`
    - _Requirements: 2.1, 2.2_

- [x] 2. Add FDDB104 diagnostic descriptor
  - [x] 2.1 Add `CompoundPromotionResolved` descriptor to `DiagnosticDescriptors.cs`
    - Add info-level diagnostic with id "FDDB104", category "FluentDynamoDb.Discriminator"
    - Message format: "Entity '{0}' promoted to compound discrimination ({1}: '{2}' + {3}: '{4}') to resolve overlap with '{5}'"
    - Add help link entry in `DiagnosticHelpLinks.cs`
    - _Requirements: 3.3_

- [x] 3. Implement CompoundPromotionPass analysis logic
  - [x] 3.1 Create `CompoundPromotionResult` class in Analysis/
    - Create `Oproto.FluentDynamoDb.SourceGenerator/Analysis/CompoundPromotionResult.cs`
    - Define `Diagnostics` list and `ResolvedPairs` HashSet with ordered tuple format `(string, string)`
    - _Requirements: 3.1, 3.3_

  - [x] 3.2 Create `CompoundPromotionPass` static class in Analysis/
    - Create `Oproto.FluentDynamoDb.SourceGenerator/Analysis/CompoundPromotionPass.cs`
    - Implement `public static CompoundPromotionResult Analyze(List<EntityModel> tableEntities, List<Diagnostic> overlapDiagnostics)`
    - Filter to entities with valid discriminators
    - Generate all unique pairwise combinations
    - Identify same-score overlaps on the same discriminator property
    - _Requirements: 1.1, 1.5, 1.6, 5.1, 5.4, 5.5, 5.6_

  - [x] 3.3 Implement cross-key pattern resolution logic
    - For each same-score pair, determine cross-key property (PK if discriminator is SK, SK if discriminator is PK)
    - Retrieve `DerivedDiscriminatorPattern` from cross-key property
    - Treat Complex-strategy patterns as null
    - Check disambiguability: patterns must differ (including one-null-one-non-null)
    - _Requirements: 1.2, 1.3, 1.4, 7.5, 7.6_

  - [x] 3.4 Implement compound constraint assignment
    - When both cross-key patterns are non-null and differ: assign positive CompoundConstraint to both entities
    - When one is non-null and other is null: assign positive CompoundConstraint to non-null entity, Exclusion guard to null entity
    - Derive Strategy and LiteralText using `DiscriminatorAnalyzer.DeterminePatternStrategy` and related methods
    - Handle multiple overlaps on same entity (idempotent positive constraints, accumulated exclusions via `AdditionalExclusions`)
    - Emit FDDB104 diagnostic and mark pair as resolved
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 7.1, 7.2, 7.3, 7.4_

  - [x] 3.5 Write property test: Disambiguability Classification (Property 1)
    - **Property 1: Disambiguability Classification**
    - **Validates: Requirements 1.2, 1.3, 1.4, 7.6**

  - [x] 3.6 Write property test: Symmetric Cross-Key Inspection (Property 2)
    - **Property 2: Symmetric Cross-Key Inspection**
    - **Validates: Requirements 1.5**

  - [x] 3.7 Write property test: Dual Compound Constraint Assignment (Property 3)
    - **Property 3: Dual Compound Constraint Assignment**
    - **Validates: Requirements 2.1, 2.3**

  - [x] 3.8 Write property test: Asymmetric Constraint Assignment (Property 4)
    - **Property 4: Asymmetric Constraint Assignment**
    - **Validates: Requirements 2.2, 2.4**

  - [x] 3.9 Write property test: Strategy Derivation from Pattern (Property 5)
    - **Property 5: Strategy Derivation from Pattern**
    - **Validates: Requirements 2.5, 7.1, 7.2, 7.3, 7.4**

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Integrate CompoundPromotionPass into source generator pipeline
  - [x] 5.1 Insert CompoundPromotionPass call in `DynamoDbSourceGenerator.cs`
    - After `PatternOverlapAnalyzer.Analyze(tableEntities)` call, invoke `CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics)`
    - _Requirements: 5.1, 5.5_

  - [x] 5.2 Implement diagnostic filtering for resolved pairs
    - Filter FDDB102/DISC004 diagnostics using `CompoundPromotionResult.ResolvedPairs`
    - Implement `IsResolvedByCompoundPromotion` helper to extract entity names from diagnostics and check resolved set
    - Only suppress diagnostics for pairs that are fully resolved; pass through all others unchanged
    - Emit FDDB104 diagnostics from `CompoundPromotionResult.Diagnostics`
    - _Requirements: 3.1, 3.2, 3.4, 5.6_

  - [x] 5.3 Write property test: Diagnostic Suppression for Resolved Pairs (Property 6)
    - **Property 6: Diagnostic Suppression for Resolved Pairs**
    - **Validates: Requirements 3.1, 3.3**

  - [x] 5.4 Write property test: Diagnostic Persistence for Unresolved Pairs (Property 7)
    - **Property 7: Diagnostic Persistence for Unresolved Pairs**
    - **Validates: Requirements 3.2, 3.4**

  - [x] 5.5 Write property test: Non-Interference for Non-Overlapping Entities (Property 8)
    - **Property 8: Non-Interference for Non-Overlapping Entities**
    - **Validates: Requirements 5.2, 5.3**

- [x] 6. Implement code generation for compound constraints
  - [x] 6.1 Extend `DiscriminatorCodeGenerator` / `MapperGenerator` for positive compound checks
    - When `DiscriminatorConfig.CompoundConstraint` is non-null and `IsExclusion == false`:
    - Generate code to retrieve cross-key attribute from item dictionary
    - Generate null/existence check for cross-key attribute value
    - Generate strategy-specific string operation (StartsWith, ExactMatch, EndsWith, Contains)
    - Return false if cross-key attribute missing or null (compound constraint is mandatory)
    - _Requirements: 4.1, 4.3, 4.4_

  - [x] 6.2 Extend code generation for exclusion guard compound constraints
    - When `DiscriminatorConfig.CompoundConstraint` is non-null and `IsExclusion == true`:
    - Generate code that returns false if cross-key value MATCHES the exclusion pattern
    - Return true (pass through) if cross-key attribute is missing or null
    - Handle `AdditionalExclusions` list by generating multiple exclusion checks
    - _Requirements: 4.2, 4.3, 4.5_

  - [x] 6.3 Write property test: Mutual Exclusivity of Generated MatchesEntity (Property 9)
    - **Property 9: Mutual Exclusivity of Generated MatchesEntity**
    - **Validates: Requirements 6.1, 6.2, 6.3, 6.5**

  - [x] 6.4 Write property test: Pairwise Completeness in Multi-Entity Groups (Property 10)
    - **Property 10: Pairwise Completeness in Multi-Entity Groups**
    - **Validates: Requirements 1.6, 5.7**

- [x] 7. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Unit tests and integration tests
  - [x] 8.1 Write unit tests for CompoundPromotionPass
    - Test two entities with same SK prefix, different PK prefixes → both get positive CompoundConstraint
    - Test two entities with same SK prefix, one PK prefix and one bare PK → positive + exclusion
    - Test two entities with same SK prefix, both null PK patterns → not disambiguable
    - Test two entities with same SK prefix, identical PK patterns → not disambiguable
    - Test three-entity group with mixed resolvability
    - Test Complex cross-key patterns treated as null
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [x] 8.2 Write unit tests for compound constraint code generation
    - Test generated MatchesEntity with positive CompoundConstraint + missing cross-key attr → returns false
    - Test generated MatchesEntity with ExclusionGuard + missing cross-key attr → returns true
    - Test StartsWith, ExactMatch, EndsWith, Contains strategies in generated code
    - Test AdditionalExclusions generates multiple exclusion checks
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 7.1, 7.2, 7.3, 7.4_

  - [x] 8.3 Write integration test for full pipeline
    - Define two entity classes with same SK prefix but different PK prefixes
    - Run full source generation pipeline
    - Verify no FDDB102/DISC004 emitted
    - Verify FDDB104 info diagnostic emitted
    - Verify generated MatchesEntity code compiles via Roslyn in-memory compilation
    - Verify generated code correctly discriminates test items
    - _Requirements: 3.1, 3.3, 6.1, 6.2, 6.3, 6.4_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties using FsCheck with xUnit
- Unit tests validate specific examples and edge cases
- The CompoundPromotionPass reuses `DiscriminatorAnalyzer.DeterminePatternStrategy` for consistent strategy derivation
- Cross-key patterns with Complex strategy are treated as null (cannot be used for compound promotion)
- Diagnostic filtering uses a fail-open approach: if entity names cannot be parsed from a diagnostic, it passes through unchanged

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "3.1"] },
    { "id": 2, "tasks": ["3.2"] },
    { "id": 3, "tasks": ["3.3"] },
    { "id": 4, "tasks": ["3.4"] },
    { "id": 5, "tasks": ["3.5", "3.6", "3.7", "3.8", "3.9"] },
    { "id": 6, "tasks": ["5.1"] },
    { "id": 7, "tasks": ["5.2"] },
    { "id": 8, "tasks": ["5.3", "5.4", "5.5"] },
    { "id": 9, "tasks": ["6.1"] },
    { "id": 10, "tasks": ["6.2"] },
    { "id": 11, "tasks": ["6.3", "6.4"] },
    { "id": 12, "tasks": ["8.1", "8.2"] },
    { "id": 13, "tasks": ["8.3"] }
  ]
}
```
