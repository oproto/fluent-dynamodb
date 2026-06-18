# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Async Multi-Item FromDynamoDbAsync Discards Related Items
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Scope the property to composite entities with `[Encrypted]` properties and `[RelatedEntity]` collections — the async multi-item `FromDynamoDbAsync(IList<...>)` must populate related collections from matching items
  - Create a test entity with `[Encrypted]` property and `[RelatedEntity]` collection in the source generator test project (`Oproto.FluentDynamoDb.SourceGenerator.UnitTests/`)
  - Verify the generated `FromDynamoDbAsync(IList<...>)` method output contains composite assembly logic (primary entity identification via regex exclusion, related entity pattern matching, collection population)
  - Alternatively, test at runtime: construct a list of DynamoDB items (one primary item + related items matching sort key patterns), call the generated async multi-item method, assert that related entity collections are populated (not empty)
  - Bug condition: `isBugCondition(entity) = entity.HasEncryptedProperties AND entity.HasRelatedEntityCollections`
  - Expected behavior: `FromDynamoDbAsync(items)` identifies primary entity (item NOT matching any `[RelatedEntity]` pattern), deserializes it, pattern-matches remaining items to `[RelatedEntity]` collections, and populates them
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (related collections are empty because stub only processes `items[0]`)
  - Document counterexamples found: e.g., "FromDynamoDbAsync([primaryItem, relatedItem1, relatedItem2]) returns entity with empty related collections"
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.4_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Non-Encrypted Composite Assembly and Single-Item Paths Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Observe: entities WITHOUT `[Encrypted]` properties routing through sync `FromDynamoDb(IList<...>)` correctly populate related collections (mirrors InvoiceManager behavior)
  - Observe: `FromDynamoDbAsync(IList<...>)` called with a single item (no related items) returns the entity with empty collections — no error
  - Observe: entities with `[Encrypted]` but no `[RelatedEntity]` properties still work with single-item delegation
  - Write property-based tests:
    - For all non-encrypted composite entities, `ToCompositeEntityAsync` populates related collections via sync path (preservation of requirement 3.1)
    - For all composite entities with only one item in the list, result has empty related collections regardless of encryption status (preservation of requirement 3.2)
    - For all entities with `[Encrypted]` but no `[RelatedEntity]`, single-item `FromDynamoDbAsync` delegation continues to work (preservation of requirement 3.4)
  - Verify generated sync multi-item `FromDynamoDb(IList<...>)` code is unchanged after fix (preservation of requirement 3.1, 3.5)
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 3. Fix composite entity async multi-item assembly

  - [x] 3.1 Add `FromDynamoDbAsync` multi-item static abstract to `IDynamoDbEntity` interface
    - Add the following signature to the `IDynamoDbEntity` interface:
      ```csharp
      static abstract Task<TSelf> FromDynamoDbAsync<TSelf>(
          IList<Dictionary<string, AttributeValue>> items,
          IBlobStorageProvider? blobProvider,
          IFieldEncryptor? fieldEncryptor,
          FluentDynamoDbOptions? options,
          CancellationToken cancellationToken) where TSelf : IDynamoDbEntity;
      ```
    - This enables `ToCompositeEntityAsync` to call the async multi-item method directly through the interface without hydrator routing
    - _Bug_Condition: isBugCondition(entity) = entity.HasEncryptedProperties AND entity.HasRelatedEntityCollections — async stub only processes items[0]_
    - _Expected_Behavior: FromDynamoDbAsync(IList items) performs full composite assembly: primary identification, pattern matching, collection population_
    - _Preservation: Sync FromDynamoDb(IList items) unchanged; non-encrypted entities unaffected_
    - _Requirements: 2.1, 2.2_

  - [x] 3.2 Implement full composite assembly in generated `FromDynamoDbAsync(IList<...>)` in `MapperGenerator.cs`
    - Replace the current stub (`return await FromDynamoDbAsync<TSelf>(items[0], ...)`) with full composite assembly logic
    - Generated code must:
      1. Short-circuit to single-item path when `items.Count == 1`
      2. Identify primary entity item by excluding items matching `[RelatedEntity]` sort key patterns (regex exclusion, same as sync path)
      3. Deserialize primary entity using property-by-property mapping with `await` for encrypted fields
      4. For each `[RelatedEntity]` collection: pattern-match items by sort key regex, deserialize each with `await ChildEntity.FromDynamoDbAsync(item, blobProvider, fieldEncryptor, options, cancellationToken).ConfigureAwait(false)`
      5. Populate related entity collection properties on the parent entity
      6. Pass `blobProvider`, `fieldEncryptor`, `options`, and `cancellationToken` through to all child deserialization calls
    - Mirror the logic from `GenerateFromDynamoDbMultiItemMethod` (sync path) but use async deserialization throughout
    - Always use `await ChildEntity.FromDynamoDbAsync(...)` for related entity deserialization — no need for `ChildEntityRequiresAsync` flag
    - All `await` calls in generated code must use `.ConfigureAwait(false)` per project conventions
    - _Bug_Condition: Generated FromDynamoDbAsync(IList items) is a stub delegating to items[0]_
    - _Expected_Behavior: Generated method performs regex-based primary identification + related entity pattern matching + async collection population_
    - _Preservation: Sync multi-item FromDynamoDb unchanged; entities without RelatedEntity still use items[0] fast-path_
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.3 Update `ToCompositeEntityAsync` in `EntityExecuteAsyncExtensions.cs` to always use async path
    - Replace the current hydrator-check routing with a direct call to `T.FromDynamoDbAsync<T>(items, blobProvider, fieldEncryptor, options, cancellationToken)`
    - Remove or bypass the hydrator routing for composite entity assembly (hydrators remain for `ToListAsync` single-item deserialization)
    - This ensures both encrypted and non-encrypted parent entities go through the same async composite assembly path
    - Non-encrypted entities passing `null` for `fieldEncryptor` is fine — the generated code handles it gracefully
    - _Bug_Condition: ToCompositeEntityAsync routes encrypted entities through hydrator which calls the stub_
    - _Expected_Behavior: ToCompositeEntityAsync always calls T.FromDynamoDbAsync<T>(IList items, ...) for full composite assembly_
    - _Preservation: ToListAsync continues to use hydrator for single-item paths (requirement 3.3)_
    - _Requirements: 2.1, 2.2, 3.1_

  - [x] 3.4 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Async Multi-Item FromDynamoDbAsync Populates Related Collections
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior
    - When this test passes, it confirms: `FromDynamoDbAsync(IList items)` correctly identifies the primary entity, pattern-matches related items, and populates related collections
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** - Non-Encrypted Composite Assembly and Single-Item Paths Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm: non-encrypted entities still work via sync path, single-item lists return entity with empty collections, encrypted entities without relationships still delegate correctly
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 4. Checkpoint - Ensure all tests pass
  - Run full test suite: `dotnet test` from solution root
  - Ensure all existing tests continue to pass (no regressions)
  - Ensure exploration test (Property 1) passes — bug is fixed
  - Ensure preservation tests (Property 2) pass — no behavior regressions
  - Shut down build server (`dotnet build-server shutdown`) if source generator changes aren't picked up
  - Ask the user if questions arise
