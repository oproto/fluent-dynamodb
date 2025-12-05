# Implementation Plan

- [x] 1. Fix IsMultiItemEntity flag in EntityAnalyzer
  - [x] 1.1 Update EntityAnalyzer to set IsMultiItemEntity based on relationships
    - Modify `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`
    - Change line 944 from `entityModel.IsMultiItemEntity = false;` to `entityModel.IsMultiItemEntity = entityModel.Relationships.Length > 0;`
    - _Requirements: 1.1_

  - [x] 1.2 Write property test for IsMultiItemEntity flag
    - **Property 1: IsMultiItemEntity Flag for Entities with Relationships**
    - **Validates: Requirements 1.1**
    - Create test in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests`
    - Generate random entity definitions with/without [RelatedEntity] attributes
    - Verify IsMultiItemEntity matches presence of relationships
    - _Requirements: 1.1_

  - [x] 1.3 Write unit tests for IsMultiItemEntity flag
    - Add test case: entity with [RelatedEntity] has IsMultiItemEntity = true
    - Add test case: entity without [RelatedEntity] has IsMultiItemEntity = false
    - _Requirements: 1.1_

- [x] 2. Fix wildcard pattern matching in MapperGenerator
  - [x] 2.1 Update GenerateSortKeyPatternMatching for multi-segment wildcards
    - Modify `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - Update `GenerateSortKeyPatternMatching` method to handle patterns like `"INVOICE#*#LINE#*"`
    - Convert wildcard pattern to regex or implement segment-by-segment matching
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 2.2 Write property test for wildcard pattern matching
    - **Property 4: Wildcard Pattern Matching**
    - **Validates: Requirements 4.1, 4.2, 4.3**
    - Create test in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests`
    - Generate random sort key patterns with wildcards
    - Generate random sort keys
    - Verify matching behavior is correct
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 2.3 Write unit tests for pattern matching edge cases
    - Test pattern `"INVOICE#*#LINE#*"` matches `"INVOICE#INV-001#LINE#1"`
    - Test pattern `"INVOICE#*#LINE#*"` does not match `"INVOICE#INV-001"`
    - Test pattern `"INVOICE#*"` matches `"INVOICE#INV-001#LINE#1"`
    - Test exact pattern matching (no wildcards)
    - _Requirements: 4.1, 4.2, 4.3_

- [x] 3. Fix primary entity identification in GenerateMultiItemFromDynamoDb
  - [x] 3.1 Update GenerateMultiItemFromDynamoDb to identify primary entity item
    - Modify `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - Add logic to find the item that matches the entity's own sort key pattern
    - Populate non-collection properties from the primary entity item, not the first item
    - _Requirements: 2.1, 2.2_

  - [x] 3.2 Write property test for primary entity identification
    - **Property 3: Primary Entity Identification**
    - **Validates: Requirements 2.1, 2.2**
    - Create test that generates random primary entity with random property values
    - Generate random related entities
    - Verify primary entity properties are correctly populated
    - _Requirements: 2.1, 2.2_

- [x] 4. Checkpoint - Verify source generator changes
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Update InvoiceManager property tests to use ToCompositeEntityAsync directly
  - [x] 5.1 Update GetCompleteInvoiceAsync to use ToCompositeEntityAsync
    - Modify `examples/Examples.Tests/InvoiceManager/InvoicePropertyTests.cs`
    - Remove manual assembly code in `GetCompleteInvoiceAsync` method
    - Use `ToCompositeEntityAsync<Invoice>()` directly
    - _Requirements: 3.1, 3.2, 3.3_

  - [x] 5.2 Write property test for composite entity assembly
    - **Property 2: Composite Entity Assembly Preserves Item Count**
    - **Validates: Requirements 1.2, 3.1, 3.3, 5.3**
    - Update existing `ComplexEntityAssembly_PopulatesLinesCorrectly` test
    - Verify that for any invoice with N line items, Lines.Count equals N
    - _Requirements: 1.2, 3.1, 3.3, 5.3_

- [x] 6. Add edge case handling
  - [x] 6.1 Handle case when no primary entity item is found
    - Ensure `FromDynamoDb` returns null or throws appropriate exception when no primary entity item matches
    - _Requirements: 2.3_

  - [x] 6.2 Write unit test for null return when no primary entity
    - Test that when only related items exist (no primary entity), the method returns null
    - _Requirements: 2.3_

- [x] 7. Final Checkpoint - Verify all tests pass
  - Ensure all tests pass, ask the user if questions arise.
