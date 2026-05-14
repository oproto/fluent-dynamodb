# Implementation Plan: Hydration Architecture Consolidation

## Overview

This implementation plan addresses the composite entity assembly bug where `[RelatedEntity]` collections fail to populate when child entities have `[DynamoDbMap]` properties. The fix involves removing the `MatchesEntity()` check, consolidating hydration code paths, and implementing recursive assembly.

## Tasks

- [x] 1. Remove MatchesEntity() check from related entity mapping
  - [x] 1.1 Update GenerateRelatedEntityCollectionMapping to remove MatchesEntity() filter
    - Modify `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - Remove the `if ({relationship.EntityType}.MatchesEntity(item))` condition
    - Wrap `FromDynamoDb` call in try/catch for graceful error handling
    - Log warning on deserialization failure and continue processing
    - _Requirements: 1.2, 3.1, 3.2, 3.3_

  - [x] 1.2 Update GenerateRelatedEntitySingleMapping to remove MatchesEntity() filter
    - Apply same changes to single-entity relationship mapping
    - _Requirements: 3.1, 3.2_

  - [x] 1.3 Write unit test verifying generated code does not contain MatchesEntity() in related mapping
    - Create test entity with [RelatedEntity] attribute
    - Verify generated code structure
    - _Requirements: 3.1_

- [x] 2. Checkpoint - Verify MatchesEntity removal
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Extract shared property deserialization logic
  - [x] 3.1 Create GeneratePropertyDeserialization helper method
    - Extract property deserialization logic from GenerateFromDynamoDbSingleMethod
    - Handle all property types: primitives, enums, nullable, DynamoDbMap, JsonBlob, List<DynamoDbMap>, encrypted, blob references
    - Accept parameters: StringBuilder, PropertyModel, itemVariableName, entityVariableName, indentation
    - _Requirements: 2.1_

  - [x] 3.2 Refactor GenerateFromDynamoDbSingleMethod to use shared helper
    - Replace inline property deserialization with calls to GeneratePropertyDeserialization
    - Verify generated code is functionally identical
    - _Requirements: 2.2_

  - [x] 3.3 Refactor GeneratePrimaryEntityIdentification to use shared helper
    - Replace inline property deserialization with calls to GeneratePropertyDeserialization
    - This fixes the DynamoDbMap bug in multi-item deserialization
    - _Requirements: 2.3, 5.1, 5.2, 5.3_

  - [x] 3.4 Refactor GenerateFromDynamoDbAsyncMethod to use shared helper
    - Replace inline property deserialization with calls to GeneratePropertyDeserialization
    - _Requirements: 2.4_

  - [x] 3.5 Write property test for hydration path consistency
    - **Property 2: Hydration Path Consistency**
    - **Validates: Requirements 2.5, 5.4**

- [x] 4. Checkpoint - Verify shared deserialization
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement recursive composite entity assembly
  - [x] 5.1 Detect nested [RelatedEntity] attributes in child entity types
    - In GenerateRelatedEntityCollectionMapping, check if the EntityType has relationships
    - Store this information for recursive assembly generation
    - _Requirements: 7.1_

  - [x] 5.2 Generate recursive assembly code for nested relationships
    - After deserializing a child entity, check if it has [RelatedEntity] properties
    - If so, filter remaining items by child's sort key patterns
    - Call child's multi-item FromDynamoDb with filtered items
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 5.3 Handle arbitrary nesting depth
    - Ensure recursive assembly works for 3+ level hierarchies
    - No explicit depth limit; bounded by query result size
    - _Requirements: 7.4, 7.5_

  - [x] 5.4 Write property test for recursive assembly
    - **Property 4: Recursive Composite Entity Assembly**
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5**

- [x] 6. Checkpoint - Verify recursive assembly
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Add comprehensive error handling and logging
  - [x] 7.1 Add warning logging for related entity deserialization failures
    - Log sort key value and entity type on failure
    - Continue processing remaining items
    - _Requirements: 6.1, 6.4_

  - [x] 7.2 Add debug logging when no primary entity found
    - Log which patterns were checked
    - _Requirements: 6.2_

  - [x] 7.3 Enhance DynamoDbMap deserialization error messages
    - Include property name, expected type, actual DynamoDB attribute type
    - _Requirements: 6.3_

  - [x] 7.4 Write property test for graceful error handling
    - **Property 5: Graceful Error Handling During Related Entity Mapping**
    - **Validates: Requirements 3.3, 6.1, 6.4**

- [x] 8. Checkpoint - Verify error handling
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Write core property tests
  - [x] 9.1 Write property test for overlapping discriminator patterns
    - **Property 1: Composite Entity Assembly with Overlapping Discriminator Patterns**
    - **Validates: Requirements 1.1, 1.3, 4.1, 4.2**
    - Generate random parent/child entity pairs with overlapping patterns
    - Create DynamoDB items and verify child collection is populated

  - [x] 9.2 Write property test for DynamoDbMap in child entities
    - **Property 3: DynamoDbMap Deserialization in Child Entities**
    - **Validates: Requirements 1.4, 5.1, 5.2**
    - Generate random child entities with [DynamoDbMap] properties
    - Verify round-trip serialization/deserialization

- [x] 10. Verify backward compatibility
  - [x] 10.1 Test entity using [JsonBlob] instead of [DynamoDbMap]
    - Verify composite entity assembly still works
    - _Requirements: 8.1_

  - [x] 10.2 Test entity with no [DynamoDbMap] properties
    - Verify behavior is unchanged
    - _Requirements: 8.2_

  - [x] 10.3 Test existing [RelatedEntity] patterns
    - Verify existing patterns continue to work
    - _Requirements: 8.3, 8.4_

- [x] 11. Final checkpoint - Run full test suite
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet test` across all test projects
  - Verify no regressions in existing functionality
  - Update changelog

## Notes

- All tasks are required for comprehensive testing
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- The key fix is in task 1.1 - removing MatchesEntity() from related entity mapping
- Task 3 consolidates duplicate code paths to prevent future bugs

