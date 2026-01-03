# Implementation Plan: Dynamic Fields Enhancements

## Overview

This implementation enhances the existing `DynamicFieldCollection` class with prefix-based accessors, typed Map operations using entity interfaces, and bulk Set/Remove operations. These enhancements enable efficient handling of sparse attribute patterns like the BalanceTreeNode scenario where dynamic attributes use naming conventions (e.g., `c_{nodeId}` for children, `t_{txnId}` for transactions).

## Tasks

- [x] 1. Implement prefix-based operations
  - [x] 1.1 Add `GetFieldNamesByPrefix(string prefix)` method
    - Return `IEnumerable<string>` of field names matching prefix
    - Use `StringComparison.Ordinal` for prefix matching
    - Add XML documentation with usage example
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 1.2 Add `GetByPrefix(string prefix)` method
    - Return `Dictionary<string, AttributeValue>` with full keys
    - Use `StringComparer.Ordinal` for result dictionary
    - Add XML documentation
    - _Requirements: 2.1, 2.2, 2.5_

  - [x] 1.3 Add `GetByPrefixWithStrippedKeys(string prefix)` method
    - Return `Dictionary<string, AttributeValue>` with prefix stripped from keys
    - Use `StringComparer.Ordinal` for result dictionary
    - Add XML documentation
    - _Requirements: 2.3, 2.4, 2.5_

  - [x] 1.4 Add `RemoveByPrefix(string prefix)` method
    - Return `int` count of removed fields
    - Reuse existing `Remove()` method for change tracking
    - Add XML documentation
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 2. Implement typed Map operations
  - [x] 2.1 Add `GetMap<T>(string fieldName, FluentDynamoDbOptions? options = null)` method
    - Add generic constraint `where T : IReadOnlyEntity`
    - Call `T.FromDynamoDb<T>(value.M, options)` for deserialization
    - Return null for missing fields
    - Throw `DynamicFieldTypeException` for non-Map types
    - Add XML documentation with usage example
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 2.2 Add `TryGetMap<T>(string fieldName, out T? value, FluentDynamoDbOptions? options = null)` method
    - Add generic constraint `where T : IReadOnlyEntity`
    - Return true/false based on success
    - Add XML documentation
    - _Requirements: 4.5_

  - [x] 2.3 Add `SetMap<T>(string fieldName, T? entity, FluentDynamoDbOptions? options = null)` method
    - Add generic constraint `where T : IDynamoDbEntity`
    - Call `T.ToDynamoDb(entity, options)` for serialization
    - Store as `new AttributeValue { M = attributes }`
    - Handle null by calling `Remove()`
    - Call `TrackModification()` for change tracking
    - Add XML documentation with usage example
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

- [x] 3. Implement prefix-based typed Map retrieval
  - [x] 3.1 Add `GetMapsByPrefix<T>(string prefix, FluentDynamoDbOptions? options = null)` method
    - Add generic constraint `where T : IReadOnlyEntity`
    - Return `Dictionary<string, T>` with full keys
    - Skip non-Map fields (don't throw)
    - Use `StringComparer.Ordinal` for result dictionary
    - Add XML documentation
    - _Requirements: 8.1, 8.2, 8.4, 8.5_

  - [x] 3.2 Add `GetMapsByPrefixWithStrippedKeys<T>(string prefix, FluentDynamoDbOptions? options = null)` method
    - Add generic constraint `where T : IReadOnlyEntity`
    - Return `Dictionary<string, T>` with prefix stripped from keys
    - Skip non-Map fields (don't throw)
    - Use `StringComparer.Ordinal` for result dictionary
    - Add XML documentation
    - _Requirements: 8.3, 8.4, 8.5_

- [x] 4. Implement bulk Set operations
  - [x] 4.1 Add `SetMany(Dictionary<string, AttributeValue> fields)` method
    - Call `TrackModification()` for each field
    - Handle null/empty dictionaries gracefully (no-op)
    - Add XML documentation
    - _Requirements: 6.1, 6.2, 6.6_

  - [x] 4.2 Add `SetManyWithPrefix(string prefix, Dictionary<string, AttributeValue> fields)` method
    - Prepend prefix to each key before adding
    - Call `TrackModification()` for each field
    - Handle null/empty dictionaries gracefully (no-op)
    - Add XML documentation
    - _Requirements: 6.3, 6.4, 6.6_

  - [x] 4.3 Add `SetMapsWithPrefix<T>(string prefix, Dictionary<string, T> entities, FluentDynamoDbOptions? options = null)` method
    - Add generic constraint `where T : IDynamoDbEntity`
    - Serialize each entity and store with prefixed key
    - Call `TrackModification()` for each field
    - Handle null/empty dictionaries gracefully (no-op)
    - Add XML documentation
    - _Requirements: 6.5, 6.6_

- [x] 5. Implement bulk Remove operations
  - [x] 5.1 Add `RemoveMany(IEnumerable<string> fieldNames)` method
    - Return `int` count of fields actually removed
    - Reuse existing `Remove()` method for change tracking
    - Add XML documentation
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

- [x] 6. Checkpoint - Verify core implementation compiles
  - Run `dotnet build` to verify compilation
  - Ensure no errors in `DynamicFieldCollection.cs`

- [x] 7. Write unit tests for prefix operations
  - [x] 7.1 Create `Oproto.FluentDynamoDb.UnitTests/Entities/DynamicFieldCollectionPrefixTests.cs`
    - Test `GetFieldNamesByPrefix` with matching prefixes
    - Test `GetFieldNamesByPrefix` with non-matching prefixes (empty result)
    - Test `GetByPrefix` returns correct fields with full keys
    - Test `GetByPrefixWithStrippedKeys` returns correct fields with stripped keys
    - Test `RemoveByPrefix` removes correct fields and returns count
    - Test `RemoveByPrefix` tracks removals when change tracking is enabled
    - _Requirements: 1.1-1.4, 2.1-2.5, 3.1-3.4_

- [x] 8. Write unit tests for typed Map operations
  - [x] 8.1 Create test entity `TestNestedEntity` with `[DynamoDbEntity]` attribute
    - Define simple properties for testing serialization/deserialization

  - [x] 8.2 Create `Oproto.FluentDynamoDb.UnitTests/Entities/DynamicFieldCollectionMapTests.cs`
    - Test `GetMap<T>` with valid Map field
    - Test `GetMap<T>` returns null for missing field
    - Test `GetMap<T>` throws `DynamicFieldTypeException` for non-Map field
    - Test `TryGetMap<T>` returns true and populates value for valid Map
    - Test `TryGetMap<T>` returns false for missing field
    - Test `SetMap<T>` serializes entity correctly
    - Test `SetMap<T>` with null removes field
    - Test `SetMap<T>` tracks modification
    - _Requirements: 4.1-4.5, 5.1-5.5_

- [x] 9. Write unit tests for bulk operations
  - [x] 9.1 Create `Oproto.FluentDynamoDb.UnitTests/Entities/DynamicFieldCollectionBulkTests.cs`
    - Test `SetMany` adds all fields
    - Test `SetMany` tracks all modifications
    - Test `SetManyWithPrefix` prepends prefix correctly
    - Test `SetMapsWithPrefix<T>` serializes all entities with prefix
    - Test `RemoveMany` removes specified fields and returns count
    - Test `RemoveMany` ignores non-existent fields
    - Test `GetMapsByPrefix<T>` returns typed entities
    - Test `GetMapsByPrefixWithStrippedKeys<T>` strips prefix from keys
    - Test `GetMapsByPrefix<T>` skips non-Map fields
    - _Requirements: 6.1-6.6, 7.1-7.4, 8.1-8.5_

- [x] 10. Write integration test for update expression
  - [x] 10.1 Add test in `DynamicFieldsIntegrationTests.cs`
    - Test bulk operations with update expression translator
    - Verify SET clauses generated for `SetMany` changes
    - Verify REMOVE clauses generated for `RemoveMany` changes
    - Verify mixed SET/REMOVE in single update works correctly
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

- [x] 11. Checkpoint - Ensure all tests pass
  - Run `dotnet test` to verify all tests pass
  - Ask the user if questions arise

- [x] 12. Update steering file
  - [x] 12.1 Update `.kiro/steering/fluentdynamodb.md`
    - Add "Dynamic Fields Enhancements" subsection after existing Dynamic Fields section
    - Document prefix-based accessor methods with examples
    - Document typed Map operations with `[DynamoDbEntity]` constraint examples
    - Document bulk operations with examples
    - Add BalanceTreeNode-style usage pattern example
    - _Requirements: All_

- [x] 13. Update CHANGELOG.md
  - [x] 13.1 Add entry to `[Unreleased]` section under `### Added`
    - Document "Dynamic Fields Enhancements" feature
    - List prefix-based accessor methods
    - List typed Map getter/setter using entity interfaces
    - List bulk Set/Remove operations
    - Include usage examples for sparse attribute patterns
    - Reference requirements
    - _Requirements: All_

- [x] 14. Update documentation changelog
  - [x] 14.1 Add entry to `docs/DOCUMENTATION_CHANGELOG.md`
    - Add entry with current date
    - Document steering file updates
    - Include category as "New Feature Documentation"
    - _Requirements: All_

- [x] 15. Create core documentation
  - [x] 15.1 Create or update `docs/core-features/DynamicFields.md`
    - Add section on prefix-based operations for sparse attribute patterns
    - Add section on typed Map operations with `[DynamoDbEntity]` nested types
    - Add section on bulk operations for efficient batch modifications
    - Include complete BalanceTreeNode-style example
    - Update `docs/INDEX.md` with link if new file created
    - _Requirements: All_

- [ ] 16. Final checkpoint - All tests pass and documentation complete
  - Ensure all tests pass
  - Verify documentation is complete
  - Ask the user if questions arise

## Notes

- All tasks are required for comprehensive implementation
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Typed Map operations use static abstract interface methods for AOT compatibility
- Bulk operations integrate with existing change tracking for update expression support
- No source generator changes required - all enhancements are to runtime `DynamicFieldCollection` class
