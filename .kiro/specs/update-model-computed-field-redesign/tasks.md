# Implementation Plan: Update Model Computed Field Redesign

## Overview

This plan implements the redesign of source-generated update model classes to exclude non-updatable properties (keys, extracted-of-keys, source-of-key-computed) and adds computed field awareness with validation diagnostics (FDDB071/072/073) and automatic recomputation. The implementation follows the dependency order from the design: runtime metadata classes first, then source generator modifications, then expression translator enhancements.

## Tasks

- [x] 1. Add runtime metadata classes to the library
  - [x] 1.1 Create `ComputedFieldMetadata` class
    - Create file `Oproto.FluentDynamoDb/Metadata/ComputedFieldMetadata.cs`
    - Properties: `string[] SourceProperties`, `string Separator`, `string? Prefix`, `string? PrefixSeparator`
    - _Requirements: 7.1, 7.6_

  - [x] 1.2 Create `ExtractedFieldMetadata` class
    - Create file `Oproto.FluentDynamoDb/Metadata/ExtractedFieldMetadata.cs`
    - Properties: `string SourceProperty`, `int Index`
    - _Requirements: 2.1, 3.3_

  - [x] 1.3 Extend `PropertyMetadata` with computed field properties
    - Add `ComputedFieldMetadata? ComputedField` property
    - Add `string? ComputedFieldTarget` property
    - Add `ExtractedFieldMetadata? ExtractedField` property
    - _Requirements: 3.1, 3.2, 3.3, 7.1_

- [x] 2. Modify source generator to filter update model properties
  - [x] 2.1 Add helper methods for property classification in `UpdateExpressionsGenerator`
    - Implement `IsExtractedFromKeyProperty(PropertyModel, EntityModel)` — checks if an extracted property's source is a key
    - Implement `IsSourcePropertyOfKeyComputed(PropertyModel, EntityModel)` — checks if property is a source of a key-based computed field
    - Implement `IsExtractedPropertyOfKeyComputed(PropertyModel, EntityModel)` — checks if property is extracted from a key-based computed field
    - Implement `GetNonKeyComputedProperties(EntityModel)` — returns computed fields that are not keys
    - _Requirements: 1.1, 1.2, 2.1, 2.2, 2.3, 3.5_

  - [x] 2.2 Modify `GenerateUpdateModelClass` to apply property filtering
    - Exclude properties decorated with `[PartitionKey]` or `[SortKey]`
    - Exclude extracted properties whose source is a key property
    - Exclude source properties and extracted properties of key-based computed fields
    - Include non-key computed fields and their source properties with deduplication
    - Handle `[Extracted]` referencing non-existent property by emitting diagnostic (Req 2.4)
    - All included properties generated as nullable types (`T?` for reference, `Nullable<T>` for value types)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 2.3 Modify `GenerateUpdateExpressionsClass` to apply same filtering
    - Apply identical exclusion/inclusion logic to the `{Entity}UpdateExpressions` class
    - Ensure `x.PropertyName` accessors match the update model properties
    - _Requirements: 1.1, 1.2, 2.1, 3.1, 3.2_

  - [x] 2.4 Write property tests for source generator filtering (Properties 1-5, 12)
    - **Property 1: Key Properties Excluded from Update Model**
    - **Property 2: Extracted Properties of Keys Excluded from Update Model**
    - **Property 3: Non-Key Computed Field Inclusion**
    - **Property 4: Update Model Property Deduplication**
    - **Property 5: Key-Based Computed Field Cascade Exclusion**
    - **Property 12: Nullable Type Generation Convention**
    - Use FsCheck with `[Property(MaxTest = 100)]`
    - Place in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/`
    - **Validates: Requirements 1.1-1.5, 2.1-2.3, 3.1-3.5**

  - [x] 2.5 Write unit tests for source generator filtering edge cases
    - Entity with PK only → update model excludes PK (Req 1.4)
    - Entity with PK+SK → update model excludes both (Req 1.3)
    - Entity with `[Extracted("Pk", 0)]` → excluded (Req 2.1)
    - Entity with `[Extracted("NonExistentProp", 0)]` → diagnostic emitted (Req 2.4)
    - _Requirements: 1.3, 1.4, 2.1, 2.4_

- [x] 3. Modify source generator to emit computed field metadata
  - [x] 3.1 Update MetadataGenerator to emit `ComputedFieldMetadata` for non-key computed properties
    - Populate `SourceProperties` from `ComputedKeyModel.SourceProperties`
    - Populate `Separator` from `ComputedKeyModel.Separator`
    - Populate `Prefix` and `PrefixSeparator` from key attribute configuration
    - _Requirements: 7.1, 7.2, 7.6_

  - [x] 3.2 Update MetadataGenerator to emit `ComputedFieldTarget` for source properties
    - For each source property of a non-key computed field, set `ComputedFieldTarget` to the computed property name
    - _Requirements: 3.2, 7.1_

  - [x] 3.3 Update MetadataGenerator to emit `ExtractedFieldMetadata` for extracted properties
    - Populate `SourceProperty` and `Index` from `ExtractedKeyModel`
    - _Requirements: 3.3, 4.4_

- [x] 4. Checkpoint - Ensure source generator changes build and existing tests pass
  - Shut down build server: `dotnet build-server shutdown`
  - Run `dotnet build` to verify source generator compiles
  - Run `dotnet test` to verify no regressions
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Enhance UpdateExpressionTranslator for computed field validation
  - [x] 5.1 Add computed field diagnostic constants
    - Define `EntityParameterReferenceMessage` (FDDB071)
    - Define `PartialSourceAssignmentMessage` (FDDB072)
    - Define `MixedAssignmentMessage` (FDDB073)
    - All throw `InvalidOperationException` with the specified message templates
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [x] 5.2 Implement entity parameter reference detection (FDDB071)
    - Add `ReferencesEntityParameter(Expression, ParameterExpression)` method
    - Walk the expression tree to detect transitive references to the entity lambda parameter
    - Covers direct property access, arithmetic on entity properties, and method calls passing entity properties
    - Throw immediately upon detection during binding processing
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 5.3 Implement computed source property interception in main binding loop
    - Add `IsComputedSourceProperty(string propertyName, ExpressionContext)` method
    - When a source/extracted property of a computed field is assigned, validate FDDB071 and store value for later recomputation instead of generating a SET
    - _Requirements: 6.1, 7.4_

  - [x] 5.4 Implement `ValidateAndProcessComputedFields` post-processing method
    - Detect mixed direct + source assignment → throw FDDB073
    - Detect partial source assignment → throw FDDB072 with missing property names
    - When all sources assigned: concatenate values in order with separator, apply prefix if configured, generate SET for computed field's DynamoDB attribute
    - Validate each computed field independently (Req 5.5)
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 5.4, 5.5, 7.1, 7.2, 7.3, 7.4, 7.6_

  - [x] 5.5 Ensure backwards compatibility for non-computed property updates
    - Direct assignment to non-key computed field → standard SET expression
    - Existing features (NoUpdate, Remove, Add, IfNotExists, null assignment, arithmetic) unchanged on non-key non-computed properties
    - No FDDB071/072/073 diagnostics emitted for previously valid expressions
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x] 5.6 Write property tests for expression translator (Properties 6-11)
    - **Property 6: Partial Source Assignment Validation (FDDB072)**
    - **Property 7: Mixed Direct and Source Assignment Validation (FDDB073)**
    - **Property 8: Independent Computed Field Validation**
    - **Property 9: Entity Parameter Reference Detection (FDDB071)**
    - **Property 10: Recomputation Correctness**
    - **Property 11: Backwards Compatibility for Non-Computed Properties**
    - Use FsCheck with `[Property(MaxTest = 100)]`
    - Place in `Oproto.FluentDynamoDb.UnitTests/Expressions/`
    - **Validates: Requirements 4.1-4.4, 5.1-5.5, 6.1-6.4, 7.1-7.4, 7.6, 9.1-9.5**

  - [x] 5.7 Write unit tests for expression translator validation
    - FDDB071 with `x.Prop + 1` pattern → correct message (Req 6.1)
    - FDDB072 with 1 of 3 sources assigned → message lists 2 missing (Req 4.2)
    - FDDB073 with direct + source → correct message (Req 5.2)
    - Recomputation with prefix ("ORDER" + "#" + "val1#val2") (Req 7.6)
    - Multiple computed fields: FDDB073 on one, other valid → only one throws (Req 5.5)
    - Direct assignment to non-key computed field → standard SET (Req 7.5, 9.3)
    - Existing features (NoUpdate, Remove, Add, arithmetic) → unchanged (Req 9.4)
    - _Requirements: 4.2, 5.2, 5.5, 6.1, 7.5, 7.6, 9.3, 9.4_

- [x] 6. Checkpoint - Verify all unit tests pass
  - Shut down build server: `dotnet build-server shutdown`
  - Run `dotnet build` and `dotnet test`
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Integration tests and final wiring
  - [x] 7.1 Write integration test: full entity with computed GSI key → update via sources → verify DynamoDB expression
    - Create test entity with computed non-key GSI field
    - Test update via source properties produces correct SET expression for the computed field
    - Place in integration test project
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 7.2 Verify existing update integration tests pass without modification
    - Run existing integration test suite
    - Confirm no regressions in non-computed property updates
    - _Requirements: 9.1, 9.2, 9.4_

- [x] 8. Update documentation
  - [x] 8.1 Update documentation files
    - Update relevant docs in `docs/` folder with computed field update patterns
    - Document the three diagnostics (FDDB071, FDDB072, FDDB073) and their meaning
    - Document source-property-based update syntax
    - Update `DOCUMENTATION_CHANGELOG.md` with the documentation additions
    - Update `CHANGELOG.md` with the feature addition
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 9. Final checkpoint - Ensure full build and test suite passes
  - Shut down build server: `dotnet build-server shutdown`
  - Run `dotnet build` and `dotnet test`
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The source generator cache must be cleared (`dotnet build-server shutdown`) before rebuilding after source generator changes
- Runtime diagnostics (FDDB071/072/073) are `InvalidOperationException` thrown from expression translator, not Roslyn compile-time diagnostics
- FsCheck is already available in the project for property-based testing

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3"] },
    { "id": 2, "tasks": ["2.1"] },
    { "id": 3, "tasks": ["2.2", "2.3"] },
    { "id": 4, "tasks": ["2.4", "2.5", "3.1"] },
    { "id": 5, "tasks": ["3.2", "3.3"] },
    { "id": 6, "tasks": ["5.1"] },
    { "id": 7, "tasks": ["5.2", "5.3"] },
    { "id": 8, "tasks": ["5.4", "5.5"] },
    { "id": 9, "tasks": ["5.6", "5.7"] },
    { "id": 10, "tasks": ["7.1", "7.2"] },
    { "id": 11, "tasks": ["8.1"] }
  ]
}
```
