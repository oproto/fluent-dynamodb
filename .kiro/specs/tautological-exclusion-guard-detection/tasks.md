# Implementation Plan: Tautological Exclusion Guard Detection

## Overview

Fix the source generator's `PatternOverlapAnalyzer` to detect when a computed exclusion guard would be tautological (identical to the entity's own positive match criterion) and emit a DISC006 diagnostic error instead of silently generating contradictory `MatchesEntity` code. Includes comprehensive test coverage for detection, preservation of valid hierarchies, and hydration correctness.

## Tasks

- [x] 1. Add DISC006 diagnostic descriptor and `IsTautologicalExclusion` helper method to the source generator
  - Add `TautologicalExclusionGuard` DiagnosticDescriptor to `DiagnosticDescriptors.cs` with ID "DISC006", severity Error
  - Add `IsTautologicalExclusion(DiscriminatorConfig lessSpecificConfig, ExclusionPattern exclusion)` private static method to `PatternOverlapAnalyzer.cs`
  - Helper computes positive match literal via `DiscriminatorAnalyzer.GetPatternText()` for StartsWith/EndsWith/Contains, `ExactValue` for ExactMatch, first non-empty segment for Complex
  - Returns true when exclusion.Strategy == positiveStrategy AND exclusion.LiteralText == positiveLiteral (ordinal)
  - For Complex positive strategy, normalize to StartsWith using first non-empty segment before comparison
  - Verify build: `dotnet build Oproto.FluentDynamoDb.SourceGenerator`

- [x] 2. Integrate tautology check into `PatternOverlapAnalyzer.Analyze()` method
  - In the else branch (different scores), after `CreateExclusionPattern` call, invoke `IsTautologicalExclusion`
  - When tautological: emit DISC006 diagnostic with lessSpecific.ClassName, both patterns, moreSpecific.ClassName, exclusion strategy, exclusion.LiteralText
  - When tautological: do NOT add exclusion to `lessSpecific.Discriminator!.OverlappingPatterns`
  - When not tautological: preserve existing behavior (add to OverlappingPatterns, emit DISC005)
  - Verify build: `dotnet build Oproto.FluentDynamoDb.SourceGenerator`

- [x] 3. Create exploratory unit tests confirming tautological detection works
  - Create `Analysis/TautologicalExclusionDetectionTests.cs` with xUnit tests
  - Test `ContainsVsComplex_SameSegment_IsTautological`: `*#ROLE#*` vs `USER#*#ROLE#*` → DISC006 emitted, OverlappingPatterns empty
  - Test `ContainsVsComplex_DeductionVariant_IsTautological`: `*#DEDUCTION#*` vs `EMPLOYEE#*#DEDUCTION#*` → DISC006 emitted
  - Test `StartsWithVsComplex_ValidHierarchy_NoTautology`: `USER#*` vs `USER#*#ROLE#*` → DISC005 emitted (not DISC006), exclusion populated
  - Test `StartsWithVsComplex_InvoiceHierarchy_NoTautology`: `INVOICE#*` vs `INVOICE#*#LINE#*` → DISC005, valid exclusion
  - Test `ContainsVsComplex_DifferentSegment_NoOverlap`: `*#AUDIT#*` vs `USER#*#ROLE#*` → no overlap detected at all
  - Verify: `dotnet test Oproto.FluentDynamoDb.SourceGenerator.UnitTests --filter "FullyQualifiedName~TautologicalExclusionDetection"`

- [x] 4. Create preservation tests for existing valid discriminator behaviors
  - Create `Analysis/TautologicalExclusionPreservationTests.cs`
  - Test `DISC004_SameScore_StillEmitted`: `*#ROLE#*` vs `*#AUDIT#*` → DISC004 (same score, not DISC006)
  - Test `DISC005_ValidResolution_StillEmitted`: `ORDER#*` vs `ORDER#*#LINE#*` → DISC005, exclusion Contains("#LINE#")
  - Test `NonOverlapping_NoExclusions_NoDiagnostics`: `USER#*` vs `ORDER#*` → zero diagnostics, empty OverlappingPatterns
  - Test `ExactMatchExclusion_NeverTautological`: Contains `*#ROLE#*` vs ExactMatch "ADMIN_ROLE" → strategies differ, no DISC006
  - Test `ThreeEntityHierarchy_ValidExclusions`: `ORDER#*`, `ORDER#*#LINE#*`, `ORDER#*#LINE#*#ADJ#*` → correct exclusion chain, no DISC006
  - Test `MultipleOverlaps_AllTautological`: `*#TAG#*` overlaps both `USER#*#TAG#*` and `ORDER#*#TAG#*` → DISC006 for each
  - Verify: `dotnet test Oproto.FluentDynamoDb.SourceGenerator.UnitTests --filter "FullyQualifiedName~TautologicalExclusionPreservation"`

- [x] 5. Create integration tests — full source generator end-to-end with tautological patterns
  - Create `Integration/TautologicalExclusionIntegrationTests.cs`
  - Test `TautologicalPattern_EmitsDISC006_NoExclusionGuard`: full generator with `*#ROLE#*` + `USER#*#ROLE#*`, verify DISC006 in diagnostics, no exclusion guard in generated code
  - Test `ValidHierarchy_GeneratesCorrectExclusionGuard`: full generator with `USER#*` + `USER#*#ROLE#*`, compile output, invoke MatchesEntity via reflection
  - Test `ThreeEntityTable_MixedPatterns_CorrectBehavior`: `SVCACCT#*`, `SVCACCT#*#ROLE#*`, `USER#*`, `USER#*#ROLE#*` — no DISC006, verify mutual exclusivity
  - Test `TautologicalPattern_GeneratedCode_StillCompiles`: verify generated code compiles even when DISC006 fires (entity just lacks exclusion)
  - Verify: `dotnet test Oproto.FluentDynamoDb.SourceGenerator.UnitTests --filter "FullyQualifiedName~TautologicalExclusionIntegration"`

- [x] 6. Create hydration correctness integration tests for valid hierarchies
  - Create `Integration/DiscriminatorHydrationCorrectnessTests.cs`
  - Test `FourEntityServiceAccountTable_MutualExclusivity`: ServiceAccount/ServiceAccountRole/User/UserRole — compile, assert MatchesEntity mutually exclusive for all sample keys
  - Test `InvoiceHierarchy_ThreeLevels_MutualExclusivity`: Invoice/InvoiceLine/InvoiceLineAdjustment — verify correct matching across three levels
  - Test `MatchesEntity_MissingDiscriminatorProperty_ReturnsFalse`: item without discriminator attribute → returns false
  - Test `MatchesEntity_NullDiscriminatorValue_ReturnsFalse`: item with null S value → returns false
  - Verify: `dotnet test Oproto.FluentDynamoDb.SourceGenerator.UnitTests --filter "FullyQualifiedName~DiscriminatorHydrationCorrectness"`

- [x] 7. Create property-based tests for tautology detection
  - Create `Analysis/TautologicalExclusionPropertyTests.cs` using FsCheck
  - Property `ContainsParent_ComplexChild_SameSegment_AlwaysTautological`: random prefix+segment where Contains `*#SEG#*` overlaps Complex `PREFIX#*#SEG#*` → DISC006 always
  - Property `StartsWithParent_ComplexChild_NeverTautological`: random prefix+segment for `PREFIX#*` vs `PREFIX#*#SEG#*` → DISC006 never
  - Property `NonOverlappingPatterns_NeverEmitDISC006`: random non-overlapping patterns → zero DISC006
  - Property `ValidHierarchy_ExclusionPopulated_ExactlyOnce`: StartsWith + Complex child → OverlappingPatterns.Count == 1 and literal differs from positive
  - Verify: `dotnet test Oproto.FluentDynamoDb.SourceGenerator.UnitTests --filter "FullyQualifiedName~TautologicalExclusionProperty"`

- [x] 8. Full regression check — verify all existing tests pass unchanged
  - Run full suite: `dotnet test Oproto.FluentDynamoDb.SourceGenerator.UnitTests`
  - Verify `TwoEntityHierarchyIntegrationTests` passes (Invoice/InvoiceLine)
  - Verify `EmployeePayrollComplexPatternIntegrationTests` passes
  - Verify `MutualExclusivityPropertyTests` passes
  - Verify `PatternOverlapAnalyzerTests` passes
  - Verify `ContainsStrategyIntegrationTests` passes
  - Run solution build: `dotnet build Oproto.FluentDynamoDb.sln`

## Task Dependency Graph

```json
{
  "waves": [
    [1],
    [2],
    [3, 4, 5, 6, 7],
    [8]
  ]
}
```

## Notes

- Tasks 3-7 can be parallelized after Task 2 is complete since they are independent test suites
- Task 8 is the final gate — no test should have been broken by the implementation
- Property-based tests (Task 7) use FsCheck and should include `[Trait("Category", "PropertyBased")]` for filtering
- Integration tests compile source via Roslyn and invoke MatchesEntity via reflection using existing `DynamicCompilationHelper` patterns
- The fix is entirely compile-time (source generator). No runtime library changes needed.
