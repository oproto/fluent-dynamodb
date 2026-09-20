# Implementation Plan: Named-Placeholder Runtime Smoke Tests

## Overview

Create 12 runtime smoke tests in a single test class that exercise the full source generator pipeline (generate → compile → load → invoke) for named-placeholder entities. All tests go in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/NamedPlaceholderRuntimeSmokeTests.cs` using existing `DynamicCompilationHelper` infrastructure.

## Tasks

- [x] 1. Create test class with helper methods
  - [x] 1.1 Create `NamedPlaceholderRuntimeSmokeTests.cs` with class declaration, using statements, traits, and suppressions
    - File: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/NamedPlaceholderRuntimeSmokeTests.cs`
    - Add `[Trait("Category", "Integration")]` and `[SuppressMessage("Trimming", "IL2026:...")]`
    - Add private helper methods: `CompileAndLoadEntity`, `InvokeToDynamoDb`, `InvokeFromDynamoDb`, `InvokeMatchesEntity`, `GetKeysMethod`, `CreateItem`
    - Follow patterns from `NamedPlaceholderCodeGenerationTests.cs`, `NonOverlappingPatternsBackwardCompatibilityIntegrationTests.cs`, and `GeneratedLoggingIntegrationTests.cs`
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

- [x] 2. Implement FromDynamoDb round-trip tests (R1)
  - [x] 2.1 Implement `FromDynamoDb_NamedPlaceholder_PopulatesExtractedProperties`
    - Entity: InvoiceLine with `[Computed("INVOICE#{InvoiceNumber}#LINE#{LineNumber}")]` on SK, two `[Extracted]` properties
    - Steps: CompileAndLoad → create instance → ToDynamoDb → FromDynamoDb → assert Extracted properties match
    - _Requirements: 1.1_
  - [x] 2.2 Implement `FromDynamoDb_NamedPlaceholderWithFormatSpecifiers_PopulatesExtractedProperties`
    - Entity: TimeEntry with `[Computed("ENTRY#{Date:yyyy-MM-dd}#{Sequence:D4}")]` on PK, DateOnly + int Extracted
    - Steps: CompileAndLoad → create instance → ToDynamoDb → verify pk value → FromDynamoDb → assert Date and Sequence round-trip
    - _Requirements: 1.2_

- [x] 3. Implement ExtractComponents tests (R2)
  - [x] 3.1 Implement `ExtractSkComponents_NamedPlaceholder_ReturnsCorrectValues`
    - Reuse InvoiceLine entity from R1.1
    - Steps: CompileAndLoad → Keys.Sk("INV-001", 42) → assert "INVOICE#INV-001#LINE#42" → ExtractSkComponents → assert tuple values
    - _Requirements: 2.1_
  - [x] 3.2 Implement `ExtractPkComponents_NamedPlaceholderWithFormatSpecifier_ReturnsCorrectValues`
    - Entity with `[Computed("ENTRY#{Date:yyyy-MM-dd}")]` on PK, single DateOnly Extracted
    - Steps: CompileAndLoad → Keys.Pk(DateOnly) → assert string → ExtractPkComponents → assert DateOnly value
    - _Requirements: 2.2_

- [x] 4. Implement MatchesEntity discrimination tests (R3, R4)
  - [x] 4.1 Implement `MatchesEntity_NamedPlaceholderRelatedEntity_CorrectlyDiscriminatesChildItems`
    - Two entities: Invoice parent (SK prefix "INVOICE") + InvoiceLine child (SK prefix "LINE") with `[RelatedEntity("{InvoiceNumber}#LINE#*")]`
    - Steps: CompileAndLoad → construct parent/child items → MatchesEntity on child type → assert matches children only
    - _Requirements: 3.1_
  - [x] 4.2 Implement `MatchesEntity_TwoNamedPlaceholderEntities_CorrectlyDiscriminates`
    - Two entities: OrderItem `[Computed("ORDER#{OrderId}")]` + ShipmentItem `[Computed("SHIPMENT#{ShipmentId}")]`
    - Steps: CompileAndLoad → create items → cross-check MatchesEntity for each type
    - _Requirements: 4.1_

- [x] 5. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implement three-plus property computed key tests (R5)
  - [x] 6.1 Implement `ThreePropertyComputedKey_NamedPlaceholder_KeysAndExtractWork`
    - Entity: DateEvent with `[Computed("{Year}#{Month}#{Day}")]`, three int Extracted properties
    - Steps: Keys.Pk(2024, 12, 25) → assert "2024#12#25" → ExtractPkComponents → assert (2024, 12, 25)
    - _Requirements: 5.1_
  - [x] 6.2 Implement `ThreePropertyComputedKey_NamedPlaceholderWithFormatSpecifiers_KeysAndExtractWork`
    - Entity: same shape with `[Computed("{Year:D4}#{Month:D2}#{Day:D2}")]`
    - Steps: Keys.Pk(2024, 1, 5) → assert "2024#01#05" → ExtractPkComponents → assert (2024, 1, 5)
    - _Requirements: 5.2_

- [x] 7. Implement GSI with named-placeholder tests (R6)
  - [x] 7.1 Implement `GsiWithNamedPlaceholderComputedKey_KeysMethodWorks`
    - Entity: ProductItem with `[GsiPartitionKey("status-index")]` + `[Computed("{Status}#{Category}")]` on Gsi1Pk
    - Steps: CompileAndLoad → GetKeysMethod for Gsi1Pk → invoke("active", "electronics") → assert "active#electronics"
    - _Requirements: 6.1_
  - [x] 7.2 Implement `GsiWithNamedPlaceholderComputedKey_ToDynamoDbIncludesGsiAttribute`
    - Reuse ProductItem entity from R6.1
    - Steps: CompileAndLoad → create instance → ToDynamoDb → assert "gsi1pk" attribute = "active#electronics"
    - _Requirements: 6.2_

- [x] 8. Implement backward compatibility tests (R7)
  - [x] 8.1 Implement `BackwardCompat_PositionalSyntax_FullRoundTrip`
    - Entity: PositionalEntity with `[Computed("Year", "Month", Format = "{0}#{1}")]` and Extracted
    - Steps: CompileAndLoad → create instance → ToDynamoDb → FromDynamoDb → assert all properties including Extracted
    - _Requirements: 7.1_
  - [x] 8.2 Implement `BackwardCompat_SeparatorSyntax_ExtractComponentsWork`
    - Entity: SeparatorEntity with `[Computed("TenantId", "UserId", Separator = "#")]` and Extracted
    - Steps: CompileAndLoad → Keys.Pk("T1", "U1") → assert "T1#U1" → ExtractPkComponents → assert values
    - _Requirements: 7.2_

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All 12 tests go in a single file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/NamedPlaceholderRuntimeSmokeTests.cs`
- Entity source code is defined as inline C# strings in each test (or shared as class-level constants where reused)
- All entity sources must include `[assembly: FluentDynamoDbSchemaVersion(1, 0)]` to suppress FDDB110
- Helper methods follow exact patterns from existing integration tests (see design document for pattern sources)
- `ExtractPkComponents`/`ExtractSkComponents` return `ValueTuple` — access members via reflection (`Item1`, `Item2`, etc.)
- Multi-entity tests (R3, R4) define both entities in the same source string
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "2.2", "3.1", "3.2"] },
    { "id": 2, "tasks": ["4.1", "4.2", "6.1", "6.2"] },
    { "id": 3, "tasks": ["7.1", "7.2", "8.1", "8.2"] }
  ]
}
```
