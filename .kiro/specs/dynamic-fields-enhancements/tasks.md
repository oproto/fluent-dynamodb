# Implementation Tasks

## Task 1: Add Prefix-Based Field Name Discovery
- [ ] Add `GetFieldNamesByPrefix(string prefix)` method to `DynamicFieldCollection`
- [ ] Use ordinal string comparison for prefix matching
- [ ] Return `IEnumerable<string>` of full field names
- [ ] Add XML documentation

## Task 2: Add Prefix-Based Field Retrieval
- [ ] Add `GetByPrefix(string prefix)` method returning `Dictionary<string, AttributeValue>`
- [ ] Add `GetByPrefixWithStrippedKeys(string prefix)` method with prefix stripped from keys
- [ ] Use ordinal string comparison and `StringComparer.Ordinal` for dictionaries
- [ ] Add XML documentation for both methods

## Task 3: Add Prefix-Based Field Removal
- [ ] Add `RemoveByPrefix(string prefix)` method returning count of removed fields
- [ ] Reuse existing `Remove()` method to ensure change tracking works correctly
- [ ] Add XML documentation

## Task 4: Add Typed Map Getter
- [ ] Add `GetMap<T>(string fieldName, FluentDynamoDbOptions? options = null)` method
- [ ] Add generic constraint `where T : IReadOnlyEntity`
- [ ] Call `T.FromDynamoDb<T>(value.M, options)` for deserialization
- [ ] Return null for missing fields, throw `DynamicFieldTypeException` for non-Map types
- [ ] Add `TryGetMap<T>` variant
- [ ] Add XML documentation

## Task 5: Add Typed Map Setter
- [ ] Add `SetMap<T>(string fieldName, T? entity, FluentDynamoDbOptions? options = null)` method
- [ ] Add generic constraint `where T : IDynamoDbEntity`
- [ ] Call `T.ToDynamoDb(entity, options)` for serialization
- [ ] Store result as `new AttributeValue { M = attributes }`
- [ ] Handle null by calling `Remove()`
- [ ] Call `TrackModification()` for change tracking
- [ ] Add XML documentation

## Task 6: Add Bulk Set Operations
- [ ] Add `SetMany(Dictionary<string, AttributeValue> fields)` method
- [ ] Add `SetManyWithPrefix(string prefix, Dictionary<string, AttributeValue> fields)` method
- [ ] Add `SetMapsWithPrefix<T>(string prefix, Dictionary<string, T> entities, FluentDynamoDbOptions? options = null)` method with `where T : IDynamoDbEntity`
- [ ] Call `TrackModification()` for each field added
- [ ] Handle null/empty dictionaries gracefully
- [ ] Add XML documentation for all methods

## Task 7: Add Bulk Remove Operations
- [ ] Add `RemoveMany(IEnumerable<string> fieldNames)` method returning count of removed fields
- [ ] Reuse existing `Remove()` method to ensure change tracking works correctly
- [ ] Add XML documentation

## Task 8: Add Prefix-Based Typed Map Retrieval
- [ ] Add `GetMapsByPrefix<T>(string prefix, FluentDynamoDbOptions? options = null)` method
- [ ] Add `GetMapsByPrefixWithStrippedKeys<T>(string prefix, FluentDynamoDbOptions? options = null)` method
- [ ] Add generic constraint `where T : IReadOnlyEntity`
- [ ] Skip non-Map fields (don't throw)
- [ ] Use `StringComparer.Ordinal` for result dictionaries
- [ ] Add XML documentation for both methods

## Task 9: Add Unit Tests for Prefix Operations
- [ ] Create `DynamicFieldCollectionPrefixTests.cs` in `Oproto.FluentDynamoDb.UnitTests/Entities/`
- [ ] Test `GetFieldNamesByPrefix` with matching and non-matching prefixes
- [ ] Test `GetByPrefix` returns correct fields with full keys
- [ ] Test `GetByPrefixWithStrippedKeys` returns correct fields with stripped keys
- [ ] Test `RemoveByPrefix` removes correct fields and returns count
- [ ] Test `RemoveByPrefix` tracks removals when change tracking is enabled

## Task 10: Add Unit Tests for Typed Map Operations
- [ ] Create `DynamicFieldCollectionMapTests.cs` in `Oproto.FluentDynamoDb.UnitTests/Entities/`
- [ ] Create test entity `TestNestedEntity` with `[DynamoDbEntity]` attribute
- [ ] Test `GetMap<T>` with valid Map field
- [ ] Test `GetMap<T>` returns null for missing field
- [ ] Test `GetMap<T>` throws `DynamicFieldTypeException` for non-Map field
- [ ] Test `TryGetMap<T>` returns true/false appropriately
- [ ] Test `SetMap<T>` serializes entity correctly
- [ ] Test `SetMap<T>` with null removes field
- [ ] Test `SetMap<T>` tracks modification

## Task 11: Add Unit Tests for Bulk Operations
- [ ] Create `DynamicFieldCollectionBulkTests.cs` in `Oproto.FluentDynamoDb.UnitTests/Entities/`
- [ ] Test `SetMany` adds all fields
- [ ] Test `SetMany` tracks all modifications
- [ ] Test `SetManyWithPrefix` prepends prefix correctly
- [ ] Test `SetMapsWithPrefix<T>` serializes all entities with prefix
- [ ] Test `RemoveMany` removes specified fields and returns count
- [ ] Test `RemoveMany` ignores non-existent fields
- [ ] Test `GetMapsByPrefix<T>` returns typed entities
- [ ] Test `GetMapsByPrefixWithStrippedKeys<T>` strips prefix from keys
- [ ] Test `GetMapsByPrefix<T>` skips non-Map fields

## Task 12: Add Integration Test for Update Expression
- [ ] Add test in `DynamicFieldsIntegrationTests.cs` for bulk operations with update expression
- [ ] Verify SET clauses generated for `SetMany` changes
- [ ] Verify REMOVE clauses generated for `RemoveMany` changes
- [ ] Verify mixed SET/REMOVE in single update works correctly

## Task 13: Update Steering File (fluentdynamodb.md)
- [ ] Add new "Dynamic Fields Enhancements" section to `.kiro/steering/fluentdynamodb.md`
- [ ] Document prefix-based accessor methods (`GetFieldNamesByPrefix`, `GetByPrefix`, `GetByPrefixWithStrippedKeys`, `RemoveByPrefix`)
- [ ] Document typed Map operations (`GetMap<T>`, `TryGetMap<T>`, `SetMap<T>`, `GetMapsByPrefix<T>`, `GetMapsByPrefixWithStrippedKeys<T>`)
- [ ] Document bulk operations (`SetMany`, `SetManyWithPrefix`, `SetMapsWithPrefix<T>`, `RemoveMany`)
- [ ] Add usage examples showing BalanceTreeNode-style patterns

## Task 14: Update CHANGELOG.md
- [ ] Add "Dynamic Fields Enhancements" entry to `[Unreleased]` section under `### Added`
- [ ] Document prefix-based accessor methods
- [ ] Document typed Map getter/setter using entity interfaces
- [ ] Document bulk Set/Remove operations
- [ ] Include usage examples for sparse attribute patterns
- [ ] Reference requirements (1.1-1.4, 2.1-2.5, 3.1-3.4, etc.)

## Task 15: Update Documentation Changelog
- [ ] Add entry to `docs/DOCUMENTATION_CHANGELOG.md` for steering file updates
- [ ] Document the new DynamicFieldCollection methods added
- [ ] Include before/after examples if applicable

## Task 16: Create/Update Core Documentation
- [ ] Update or create `docs/core-features/DynamicFields.md` with enhanced capabilities
- [ ] Add section on prefix-based operations for sparse attribute patterns
- [ ] Add section on typed Map operations with `[DynamoDbEntity]` nested types
- [ ] Add section on bulk operations for efficient batch modifications
- [ ] Include complete BalanceTreeNode-style example showing children and transactions pattern
