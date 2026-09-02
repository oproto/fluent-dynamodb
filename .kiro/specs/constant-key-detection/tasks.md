# Implementation Plan: Constant Key Detection

## Overview

This plan implements constant key detection for the Roslyn incremental source generator. The feature detects key properties (`[PartitionKey]`/`[SortKey]`) returning a fixed compile-time string value via expression-body or read-only auto-property syntax, stores the value in `PropertyModel.ConstantKeyValue`, and propagates that through discriminator derivation, Keys class generation, convenience methods, serialization, deserialization, update model exclusion, and diagnostics.

Implementation is in C# targeting .NET 8 with FsCheck for property-based testing. All changes are within the `Oproto.FluentDynamoDb.SourceGenerator` and `Oproto.FluentDynamoDb.SourceGenerator.UnitTests` projects.

## Tasks

- [x] 1. Extend PropertyModel and add diagnostic descriptors
  - [x] 1.1 Add ConstantKeyValue field and IsConstantKey property to PropertyModel
    - Add `public string? ConstantKeyValue { get; set; }` to `Models/PropertyModel.cs`
    - Add computed `public bool IsConstantKey => ConstantKeyValue != null;`
    - Ensure equatable implementation includes the new field for incremental generator caching
    - _Requirements: 1.1, 2.1_

  - [x] 1.2 Add diagnostic descriptors FDDB120–FDDB123
    - Add `ConstantKeyComputedConflict` (FDDB120, Error) to `Diagnostics/DiagnosticDescriptors.cs`
    - Add `ConstantKeyPrefixConflict` (FDDB121, Error)
    - Add `ConstantKeyExtractedConflict` (FDDB122, Error)
    - Add `ConstantKeyEmptyValue` (FDDB123, Error)
    - Use message format strings matching the design document
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [x] 2. Implement constant key detection in EntityAnalyzer
  - [x] 2.1 Implement DetectConstantKeyValue method in EntityAnalyzer
    - Add private method `DetectConstantKeyValue(PropertyDeclarationSyntax, SemanticModel, PropertyModel)` in `Analysis/EntityAnalyzer.cs`
    - Handle Case 1: expression-body property — check `propertyDecl.ExpressionBody`, use `SemanticModel.GetConstantValue()` on the expression
    - Handle Case 2: read-only auto-property — check for single get accessor, no set/init, with initializer, use `SemanticModel.GetConstantValue()` on initializer value
    - Only apply to properties with `IsPartitionKey` or `IsSortKey` set
    - Call from `AnalyzeProperty` after key attribute extraction
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4_

  - [x] 2.2 Write property test for expression-body constant key detection
    - **Property 1: Expression-body constant key detection**
    - **Validates: Requirements 1.1**
    - Generate random non-empty strings, verify `ConstantKeyValue` equals the literal for expression-body properties with `[PartitionKey]` or `[SortKey]`
    - Test file: `Analysis/ConstantKeyDetectionPropertyTests.cs`

  - [x] 2.3 Write property test for read-only auto-property constant key detection
    - **Property 2: Read-only auto-property constant key detection**
    - **Validates: Requirements 2.1**
    - Generate random non-empty strings, verify `ConstantKeyValue` equals the literal for get-only auto-properties with `[PartitionKey]` or `[SortKey]`
    - Test file: `Analysis/ConstantKeyDetectionPropertyTests.cs`

  - [x] 2.4 Write property test for set/init accessor prevention
    - **Property 3: Set/init accessor prevents constant key detection**
    - **Validates: Requirements 2.3**
    - Generate properties with set or init accessors, verify `ConstantKeyValue` remains null regardless of initializer
    - Test file: `Analysis/ConstantKeyDetectionPropertyTests.cs`

- [x] 3. Implement validation rules for constant key conflicts
  - [x] 3.1 Add constant key validation checks to EntityAnalyzer validation phase
    - In `ValidatePropertyModel`: emit FDDB120 when `IsConstantKey && IsComputed`
    - In `ValidatePropertyModel`: emit FDDB121 when `IsConstantKey && KeyFormat?.Prefix != null`
    - In `ValidatePropertyModel`: emit FDDB123 when `IsConstantKey && string.IsNullOrWhiteSpace(ConstantKeyValue)`
    - In `ValidateExtractedProperty`: emit FDDB122 when source property `IsConstantKey`
    - All diagnostics halt code generation for the affected entity
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [x] 3.2 Write property test for empty/whitespace constant key diagnostic
    - **Property 11: Empty or whitespace constant key value produces error diagnostic**
    - **Validates: Requirements 9.4**
    - Generate strings composed entirely of whitespace (and empty), verify FDDB123 emitted
    - Test file: `Diagnostics/ConstantKeyDiagnosticPropertyTests.cs`

  - [x] 3.3 Write unit tests for FDDB120–FDDB123 conflict diagnostics
    - Test Constant+Computed emits FDDB120
    - Test Constant+Prefix emits FDDB121
    - Test Extracted-from-Constant emits FDDB122
    - Test empty/whitespace constant emits FDDB123
    - Test that each diagnostic halts code generation for the entity
    - Test file: `Diagnostics/ConstantKeyDiagnosticTests.cs`
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [x] 4. Checkpoint - Ensure detection and diagnostics pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement auto-discriminator derivation from constant keys
  - [x] 5.1 Extend discriminator derivation to handle constant keys
    - In `ComputeNormalizedKeyFormats`: set `NormalizedKeyFormat = ConstantKeyValue` when `IsConstantKey`
    - In `DeriveDiscriminatorPatterns`: set `DerivedDiscriminatorPattern = ConstantKeyValue` when `IsConstantKey`
    - In `ApplyAutoDerivedDiscriminator`: create `DiscriminatorConfig` with `Strategy = ExactMatch`, `ExactValue = constantValue`, `IsAutoDerived = true`
    - Ensure constant-key entities do not require manual `DiscriminatorProperty`/`DiscriminatorValue` on `[DynamoDbTable]`
    - Sort key pattern preferred as primary discriminator when entity has both constant SK and prefix PK
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 5.2 Write property test for discriminator derivation produces ExactMatch
    - **Property 4: Discriminator derivation produces ExactMatch**
    - **Validates: Requirements 3.1**
    - For any non-whitespace constant key value V, verify derived DiscriminatorConfig has Strategy==ExactMatch, ExactValue==V, IsAutoDerived==true
    - Test file: `Analysis/ConstantKeyDetectionPropertyTests.cs`

- [x] 6. Implement KeysGenerator changes for constant keys
  - [x] 6.1 Modify KeysGenerator to emit parameterless accessors for constant keys
    - In `GeneratePartitionKeyBuilder`/`GenerateSortKeyBuilder`: skip parameterized method for constant keys, emit parameterless static property returning constant value
    - In `GenerateCompositeKeyBuilder`: determine constant vs variable keys, generate `Key()` accepting only variable parameters, inject constant values in tuple return
    - Handle all-keys-constant scenario: parameterless `Key()` returning tuple of constants
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 6.2 Write property test for parameterless accessor generation
    - **Property 5: Keys class provides parameterless accessor for constant keys**
    - **Validates: Requirements 4.1, 4.4**
    - For any constant key with value V, verify Keys class contains parameterless property returning V and no parameterized method
    - Test file: `Generators/ConstantKeyKeysGeneratorPropertyTests.cs`

  - [x] 6.3 Write property test for composite Key() method
    - **Property 6: Composite Key() method accepts only variable parameters**
    - **Validates: Requirements 4.2**
    - For entity with one constant key (C) and one variable key, verify Key() accepts one param and returns tuple with both values
    - Test file: `Generators/ConstantKeyKeysGeneratorPropertyTests.cs`

- [x] 7. Implement MapperGenerator changes for serialization and deserialization
  - [x] 7.1 Modify MapperGenerator ToDynamoDb for constant key serialization
    - In `GeneratePropertyToAttributeValue`: when `IsConstantKey`, emit constant value directly as `new AttributeValue { S = "value" }` using the attribute name as dict key
    - Do not read from entity instance — property may lack a setter
    - Skip prefix/KeyInputMode logic for constant keys with no prefix
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 7.2 Modify MapperGenerator FromDynamoDb for constant key deserialization validation
    - In `GeneratePropertyFromAttributeValue`: when `IsConstantKey`, generate validation code
    - If attribute present but value differs (ordinal comparison), log warning via `options?.Logger?.LogWarning`
    - If attribute missing, log warning indicating expected attribute was absent
    - Skip property assignment — no setter for expression-body, read-only for auto-property
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 7.3 Write property test for serialization emits constant value
    - **Property 8: Serialization emits constant value directly**
    - **Validates: Requirements 6.1, 6.2, 6.3**
    - For any constant key with attribute name A and value V, verify ToDynamoDb emits `[A] = new AttributeValue { S = V }` directly
    - Test file: `Generators/ConstantKeyMapperPropertyTests.cs`

  - [x] 7.4 Write property test for deserialization validates constant key
    - **Property 9: Deserialization validates constant key value**
    - **Validates: Requirements 7.1**
    - For any constant key with expected V and incoming W≠V, verify LogWarning is invoked with expected and actual values
    - Test file: `Generators/ConstantKeyMapperPropertyTests.cs`

- [x] 8. Implement TableGenerator changes for convenience methods
  - [x] 8.1 Modify TableGenerator to simplify convenience methods for constant keys
    - In `GenerateAccessorGetMethod`: detect constant SK/PK, generate method accepting only variable key param, inject constant value in `.WithKey()` call
    - In `GenerateAccessorDeleteMethod`/`GenerateAccessorDeleteAsyncMethod`: same simplification
    - In `GenerateAccessorUpdateMethod`: same simplification
    - Handle both constant-SK and constant-PK scenarios
    - Handle all-keys-constant scenario: parameterless methods
    - Preserve optional KeyCondition and KeyInputMode parameters on simplified signatures
    - Also simplify table-level convenience methods (e.g., `table.Get<Entity>(pk)`)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [x] 8.2 Write property test for convenience method simplification
    - **Property 7: Convenience methods omit constant key parameters**
    - **Validates: Requirements 5.1, 5.2, 5.3, 5.4**
    - For entity with one constant and one variable key, verify Get/Delete/Update accept only variable param and inject constant internally
    - Test file: `Generators/ConstantKeyTableGeneratorPropertyTests.cs`

- [x] 9. Implement UpdateExpressionsGenerator exclusion
  - [x] 9.1 Modify UpdateExpressionsGenerator to exclude constant key properties
    - In update model property enumeration: skip property when `IsConstantKey`
    - Ensure exclusion is independent of existing key exclusion logic (belt-and-suspenders)
    - _Requirements: 8.1, 8.2, 8.3_

  - [x] 9.2 Write property test for update model exclusion
    - **Property 10: Update model excludes constant key properties**
    - **Validates: Requirements 8.1, 8.2, 8.3**
    - For any property detected as constant key, verify generated update model does not include it
    - Test file: `Generators/ConstantKeyUpdateModelPropertyTests.cs`

- [x] 10. Checkpoint - Ensure all generator tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Integration tests and end-to-end verification
  - [x] 11.1 Write integration tests for constant key full compilation
    - Define entity with expression-body constant SK and variable PK, compile with source generator, verify:
      - Generated code compiles without errors
      - Keys class has parameterless SK accessor
      - Convenience methods accept only PK
      - ToDynamoDb emits constant directly
      - FromDynamoDb validates incoming value
      - Update model excludes constant key property
    - Define entity with read-only auto-property constant PK
    - Define entity with both keys constant (parameterless everything)
    - Test non-constant entities remain unaffected (regression)
    - Test file: `Integration/ConstantKeyIntegrationTests.cs`
    - _Requirements: 1.1, 2.1, 4.1, 5.1, 6.1, 7.1, 8.1_

  - [x] 11.2 Write unit tests for non-resolvable expression edge cases
    - Test method call returns (`PropertyModel.ConstantKeyValue` remains null)
    - Test interpolated string returns (remains null)
    - Test conditional expression returns (remains null)
    - Test property access returns (remains null)
    - Test `nameof()` expression — should resolve via GetConstantValue
    - Test const field from different class in same compilation
    - Test file: `Analysis/ConstantKeyDetectionTests.cs`
    - _Requirements: 1.3, 1.4, 2.2_

  - [x] 11.3 Write unit tests for discriminator conflict and pattern overlap scenarios
    - Test entity with both explicit DiscriminatorValue and constant key — expect existing FDDB101/FDDB103 diagnostics
    - Test two entities on same table with overlapping constant-key patterns — expect overlap diagnostic and exclusion guards
    - Test file: `Integration/ConstantKeyDiscriminatorIntegrationTests.cs`
    - _Requirements: 3.4, 3.5_

- [x] 12. Documentation and changelog updates
  - [x] 12.1 Create documentation and update changelog for constant key detection
    - Create `docs/core-features/ConstantKeyDetection.md` with:
      - Examples of expression-body and read-only auto-property syntax
      - Keys class behavior changes
      - Convenience method simplification
      - Serialization/deserialization behavior
      - Diagnostic descriptions (FDDB120–FDDB123)
    - Update `CHANGELOG.md` under `[Unreleased]` → Added section with feature description and code examples
    - Update `docs/DOCUMENTATION_CHANGELOG.md` with date, file path, and explanation
    - Update `.kiro/steering/fluentdynamodb.md` Entity Definition section with constant key syntax
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

- [x] 13. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties defined in the design (Properties 1–11)
- Unit tests validate specific examples and edge cases
- FsCheck is already available in the test project — no additional dependency setup needed
- The source generator targets `netstandard2.0`; tests target `net8.0`
- After modifying the source generator, run `dotnet build-server shutdown` before rebuilding

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "3.1"] },
    { "id": 3, "tasks": ["3.2", "3.3", "5.1"] },
    { "id": 4, "tasks": ["5.2", "6.1"] },
    { "id": 5, "tasks": ["6.2", "6.3", "7.1"] },
    { "id": 6, "tasks": ["7.2", "7.3", "8.1"] },
    { "id": 7, "tasks": ["7.4", "8.2", "9.1"] },
    { "id": 8, "tasks": ["9.2", "11.1"] },
    { "id": 9, "tasks": ["11.2", "11.3"] },
    { "id": 10, "tasks": ["12.1"] }
  ]
}
```
