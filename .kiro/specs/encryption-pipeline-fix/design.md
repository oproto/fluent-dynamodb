# Encryption Pipeline Fix — Bugfix Design

## Overview

Entities with `[Encrypted]` properties (but no blob storage) cannot complete CRUD operations because three interconnected gaps in the source generator and request builder pipeline prevent the async serialization/deserialization methods from being invoked. The fix targets three components: (1) the `HydratorGenerator` to also generate hydrators for encryption-only entities, (2) the `MapperGenerator` to make `blobProvider` nullable in `ToDynamoDbAsync` for encryption-only entities, and (3) the `PutItemRequestBuilder` to defer serialization to execution time when the entity requires async methods. The read path, update path, and delete path require minimal or no changes beyond the hydrator generation fix.

## Glossary

- **Bug_Condition (C)**: An entity has one or more `[Encrypted]` properties but no blob storage properties, and a CRUD operation (Put, Get, Query, Scan) is attempted through the standard builder pipeline
- **Property (P)**: The entity is serialized/deserialized using the generated async methods (`ToDynamoDbAsync`/`FromDynamoDbAsync`) with the `IFieldEncryptor` from `FluentDynamoDbOptions`, and the operation completes successfully
- **Preservation**: All existing behavior for non-encrypted entities (sync path), blob-storage-only entities (existing async path), and blob+encrypted entities (existing combined path) remains unchanged
- **`HydratorGenerator`**: Source generator in `Oproto.FluentDynamoDb.SourceGenerator/Generators/HydratorGenerator.cs` that decides whether to emit an `IAsyncEntityHydrator<T>` implementation for an entity
- **`MapperGenerator`**: Source generator in `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` that emits `ToDynamoDb`, `ToDynamoDbAsync`, `FromDynamoDb`, and `FromDynamoDbAsync` methods on entity partial classes
- **`PutItemRequestBuilder<T>`**: Runtime builder in `Oproto.FluentDynamoDb/Requests/PutItemRequestBuilder.cs` whose `WithItem(TEntity)` method currently calls the synchronous `TEntity.ToDynamoDb()` at configuration time
- **`IAsyncEntityHydrator<T>`**: Interface in `Oproto.FluentDynamoDb/Hydration/` with `HydrateAsync` (read) and `SerializeAsync` (write) methods, looked up via `IEntityHydratorRegistry`
- **`FluentDynamoDbOptions`**: Configuration object carrying `FieldEncryptor`, `BlobStorageProvider`, `HydratorRegistry`, and other settings

## Bug Details

### Bug Condition

The bug manifests when a user defines an entity with `[Encrypted]` properties but no `[BlobStorage]` properties, and attempts any standard CRUD operation (Put, Get, Query, Scan) through the fluent builder pipeline. Three separate failures occur depending on the operation path.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type { entity: EntityModel, operation: CrudOperation }
  OUTPUT: boolean

  hasEncrypted := entity.Properties.Any(p => p.Security?.IsEncrypted == true)
  hasBlobStorage := entity.Properties.Any(p => p.ComplexType?.IsBlobStorage == true)

  RETURN hasEncrypted AND NOT hasBlobStorage
         AND operation IN [Put, Get, Query, Scan]
END FUNCTION
```

### Examples

- **Put (Defect 1.3)**: `table.SecureRecords.Put(record).PutAsync()` → `WithItem(record)` calls sync `ToDynamoDb()` → throws `NotSupportedException("...requires async methods. Use ToDynamoDbAsync...")`
- **Get (Defect 1.1)**: `table.SecureRecords.Get(pk).GetItemAsync(blobProvider)` → hydrator registry returns `null` (no hydrator generated) → falls through to sync `FromDynamoDb()` → throws `NotSupportedException`
- **Put with null blob provider (Defect 1.2)**: Even if defect 1.3 were fixed, `ToDynamoDbAsync(entity, blobProvider: null!)` → throws `ArgumentNullException("blobProvider is required...")` because the generated method has a non-nullable `IBlobStorageProvider` parameter with a null guard
- **Edge case — blob+encrypted entity**: `SecureRecordWithBlobs` (has both) → hydrator IS generated, `ToDynamoDbAsync` receives a real blob provider → works correctly today (not a bug condition)

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Entities with no `[Encrypted]` and no blob storage properties continue to use synchronous `ToDynamoDb`/`FromDynamoDb` without hydrators
- Entities with blob storage properties (no encryption) continue to generate hydrators and use `ToDynamoDbAsync`/`FromDynamoDbAsync` with `IBlobStorageProvider` as a required parameter
- Entities with both blob storage and `[Encrypted]` properties continue to generate hydrators and use both providers
- `PutItemRequestBuilder.WithItem(entity)` for non-encrypted entities continues to serialize synchronously at builder-configuration time via `ToDynamoDb`
- `GetItemAsync` for non-encrypted entities without blob storage continues to deserialize synchronously via `FromDynamoDb` without consulting the hydrator registry
- `UpdateItemRequestBuilder` continues to use the expression translator path (which already handles encryption via `EncryptParametersAsync`)
- `DeleteItemRequestBuilder` continues to work unchanged (it only sends keys, never serializes entity bodies)
- Transaction and batch write operations that use `ITransactablePutBuilder.GetItem()` continue to receive the serialized item dictionary

**Scope:**
All inputs that do NOT involve encryption-only entities should be completely unaffected by this fix. This includes:
- All synchronous entity operations (no blob, no encryption)
- All blob-storage-only entity operations
- All blob+encrypted entity operations
- Update operations (expression translator already handles encryption)
- Delete operations (no entity serialization)

## Hypothesized Root Cause

Based on the bug description and code analysis, the three defects have distinct root causes:

1. **HydratorGenerator.RequiresHydrator() only checks IsBlobStorage** (`HydratorGenerator.cs:34`): The method `return entity.Properties.Any(p => p.ComplexType?.IsBlobStorage == true)` was written when hydrators were only needed for blob storage. When encryption was added, the `MapperGenerator` was updated to generate async methods for encrypted entities, but `HydratorGenerator.RequiresHydrator()` was never updated to also trigger on `p.Security?.IsEncrypted == true`. This means the read path (`GetItemAsync` with blob provider overload) looks up the hydrator registry, finds nothing, and falls through to the sync `FromDynamoDb` stub.

2. **Generated ToDynamoDbAsync has non-nullable IBlobStorageProvider** (`MapperGenerator.cs:GenerateToDynamoDbAsyncMethod`): The `ToDynamoDbAsync` method signature always emits `IBlobStorageProvider blobProvider` as a required parameter with a null guard (`if (blobProvider == null) throw new ArgumentNullException`). For encryption-only entities, there is no blob provider to pass. The parameter should be nullable for entities that only have encryption.

3. **PutItemRequestBuilder.WithItem() calls sync ToDynamoDb at configuration time** (`PutItemRequestBuilder.cs:WithItem`): The method `_req.Item = TEntity.ToDynamoDb(entity, _options)` is called synchronously when the builder is configured. For encrypted entities, `ToDynamoDb` is a stub that throws `NotSupportedException`. Serialization must be deferred to execution time (`PutAsync`/`ToDynamoDbResponseAsync`) where an async context is available and the hydrator's `SerializeAsync` can be called.

## Correctness Properties

Property 1: Bug Condition — Encryption-Only Entity CRUD Operations

_For any_ entity type that has `[Encrypted]` properties but no blob storage properties, and _for any_ CRUD operation (Put, Get, Query, Scan) executed through the standard builder pipeline with a configured `IFieldEncryptor`, the fixed pipeline SHALL successfully serialize (write path) and deserialize (read path) the entity using the generated async methods, with encrypted fields correctly encrypted/decrypted.

**Validates: Requirements 2.1, 2.2, 2.3**

Property 2: Preservation — Non-Encrypted and Blob-Only Entity Behavior

_For any_ entity type that does NOT have encryption-only properties (i.e., has no encrypted properties, or has both blob storage and encrypted properties, or has only blob storage), the fixed pipeline SHALL produce exactly the same behavior as the original code, preserving synchronous serialization for plain entities, existing hydrator-based async serialization for blob entities, and the combined path for blob+encrypted entities.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/HydratorGenerator.cs`

**Function**: `RequiresHydrator(EntityModel entity)` and `GenerateHydrator(EntityModel entity)`

**Specific Changes**:
1. **Extend RequiresHydrator condition**: Change the check from `entity.Properties.Any(p => p.ComplexType?.IsBlobStorage == true)` to also include `entity.Properties.Any(p => p.Security?.IsEncrypted == true)`. This ensures encryption-only entities get hydrators generated.
2. **Make blobProvider nullable in generated hydrator for encryption-only entities**: When the entity has encryption but no blob storage, the generated hydrator's `HydrateAsync` and `SerializeAsync` methods should pass `null` (or a no-op provider) for the `blobProvider` parameter when delegating to `FromDynamoDbAsync`/`ToDynamoDbAsync`. This requires the hydrator to be aware of whether the entity actually needs blob storage.

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`

**Function**: `GenerateToDynamoDbAsyncMethod(StringBuilder sb, EntityModel entity)`

**Specific Changes**:
3. **Make blobProvider nullable for encryption-only entities**: When the entity has encrypted properties but no blob storage properties, generate `IBlobStorageProvider? blobProvider` (nullable) instead of `IBlobStorageProvider blobProvider`, and remove the null guard for `blobProvider`. The blob provider is only needed when blob storage properties exist.
4. **Apply same change to FromDynamoDbAsync methods**: The `FromDynamoDbSingleAsyncMethod` and `FromDynamoDbMultiAsyncMethod` should also make `blobProvider` nullable when the entity is encryption-only.

---

**File**: `Oproto.FluentDynamoDb/Requests/PutItemRequestBuilder.cs`

**Function**: `WithItem(TEntity entity)`

**Specific Changes**:
5. **Store entity reference without immediate serialization**: Change `WithItem(TEntity entity)` to store the entity reference (`_entity = entity`) but NOT call `TEntity.ToDynamoDb(entity, _options)` immediately. Instead, defer serialization.
6. **Serialize at execution time**: In `ToPutItemRequest()` (or `ToDynamoDbResponseAsync`), check if `_req.Item` is null (meaning deferred serialization). If so, check the hydrator registry for an `IAsyncEntityHydrator<TEntity>`. If a hydrator exists, use `SerializeAsync` to produce the item dictionary. If no hydrator exists, call the sync `TEntity.ToDynamoDb(entity, _options)` as before.
7. **Handle the async gap in ToPutItemRequest()**: Since `ToPutItemRequest()` is synchronous, the deferred async serialization must happen in `ToDynamoDbResponseAsync()` and `PutAsync()` (the async execution methods). `ToPutItemRequest()` should throw if the item hasn't been serialized yet and the entity requires async serialization.

---

**File**: `Oproto.FluentDynamoDb/Requests/Extensions/EntityExecuteAsyncExtensions.cs`

**Function**: `PutAsync<T>(this PutItemRequestBuilder<T> builder, ...)`

**Specific Changes**:
8. **Add async serialization before building request**: Before calling `builder.ToPutItemRequest()`, check if the builder has a deferred entity that needs async serialization. If so, resolve the hydrator from `builder.GetOptions().HydratorRegistry` and call `SerializeAsync` to populate the item dictionary.

---

**File**: `Oproto.FluentDynamoDb/Hydration/IAsyncEntityHydrator.cs`

**Function**: Interface definition

**Specific Changes**:
9. **Make blobProvider nullable in interface**: Change `IBlobStorageProvider blobProvider` to `IBlobStorageProvider? blobProvider` in all three methods (`HydrateAsync` single, `HydrateAsync` multi, `SerializeAsync`). This allows encryption-only hydrators to be called without a blob provider.

---

### Impact on Other Write Builders

- **UpdateItemRequestBuilder**: No changes needed. Updates use the expression translator path, which already handles encryption via `EncryptParametersAsync`. The `SetFieldEncryptor` method is already wired up.
- **DeleteItemRequestBuilder**: No changes needed. Deletes only send key attributes, never serialize entity bodies.
- **TransactionWriteBuilder.Add(PutItemRequestBuilder)**: Currently calls `ITransactablePutBuilder.GetItem()` synchronously. After the fix, the transaction builder will need to handle deferred serialization. The `GetItem()` call must ensure the item is serialized. This may require making the transaction `Add` method aware of async serialization, or requiring that `PutItemRequestBuilder` exposes an async method to resolve the item before the transaction is built.
- **BatchWriteBuilder.Add(PutItemRequestBuilder)**: Same concern as transactions — `putBuilder.GetItem()` is called synchronously. The batch builder's `ExecuteAsync` method will need to resolve deferred items before building the batch request.

### Impact on Transactions and Batch Operations

The `TransactionWriteBuilder` and `BatchWriteBuilder` both call `ITransactablePutBuilder.GetItem()` synchronously when adding put operations. For encryption-only entities with deferred serialization, this requires one of:

- **Option A**: Make `TransactionWriteBuilder.Add` and `BatchWriteBuilder.Add` async-aware by storing the builder reference and resolving the item in `ExecuteAsync` (similar to how `_updateBuilders` are stored for encryption handling in transactions).
- **Option B**: Add an async `ResolveItemAsync` method to `ITransactablePutBuilder` that the transaction/batch builders call during `ExecuteAsync`.

Option A is consistent with the existing pattern used for `UpdateItemRequestBuilder` encryption in `TransactionWriteBuilder` (which stores `_updateBuilders` and calls `EncryptParametersIfNeededAsync` during `ExecuteAsync`).

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write tests that create encryption-only entities and attempt CRUD operations through the builder pipeline. Run these tests on the UNFIXED code to observe failures and understand the root cause.

**Test Cases**:
1. **HydratorGenerator Test**: Assert that `HydratorGenerator.RequiresHydrator()` returns `true` for an entity with `[Encrypted]` properties but no blob storage (will fail on unfixed code — returns `false`)
2. **ToDynamoDbAsync Null BlobProvider Test**: Call `ToDynamoDbAsync` on an encryption-only entity with `blobProvider: null` (will fail on unfixed code — throws `ArgumentNullException`)
3. **PutItemRequestBuilder.WithItem Test**: Call `WithItem(encryptedEntity)` on a `PutItemRequestBuilder` (will fail on unfixed code — throws `NotSupportedException` from sync `ToDynamoDb` stub)
4. **GetItemAsync Hydrator Lookup Test**: Verify that the hydrator registry has a hydrator for an encryption-only entity type (will fail on unfixed code — no hydrator registered)

**Expected Counterexamples**:
- `HydratorGenerator.RequiresHydrator()` returns `false` for encryption-only entities
- `ToDynamoDbAsync` throws `ArgumentNullException` when `blobProvider` is null
- `PutItemRequestBuilder.WithItem()` throws `NotSupportedException` for encrypted entities
- Possible causes: `RequiresHydrator` only checks `IsBlobStorage`, `ToDynamoDbAsync` has non-nullable `blobProvider`, `WithItem` calls sync `ToDynamoDb`

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := executeOperation_fixed(input)
  ASSERT result.success == true
  ASSERT result.encryptedFieldsAreEncrypted == true (write path)
  ASSERT result.encryptedFieldsAreDecrypted == true (read path)
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT executeOperation_original(input) = executeOperation_fixed(input)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain
- It catches edge cases that manual unit tests might miss
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Observe behavior on UNFIXED code first for non-encrypted entities and blob-only entities, then write property-based tests capturing that behavior.

**Test Cases**:
1. **Plain Entity Preservation**: Verify that entities with no encryption and no blob storage continue to use sync `ToDynamoDb`/`FromDynamoDb` and do not consult the hydrator registry
2. **Blob-Only Entity Preservation**: Verify that entities with blob storage but no encryption continue to generate hydrators and use `ToDynamoDbAsync`/`FromDynamoDbAsync` with a required blob provider
3. **Blob+Encrypted Entity Preservation**: Verify that entities with both blob storage and encryption continue to work as before
4. **Update Operation Preservation**: Verify that `UpdateItemRequestBuilder` with encrypted fields continues to use the expression translator encryption path
5. **Delete Operation Preservation**: Verify that `DeleteItemRequestBuilder` continues to work without entity serialization

### Unit Tests

- Test `HydratorGenerator.RequiresHydrator()` returns `true` for encryption-only, blob-only, and blob+encrypted entities
- Test `HydratorGenerator.RequiresHydrator()` returns `false` for plain entities
- Test generated `ToDynamoDbAsync` accepts null `blobProvider` for encryption-only entities
- Test generated `ToDynamoDbAsync` still requires non-null `blobProvider` for blob-storage entities
- Test `PutItemRequestBuilder.WithItem()` defers serialization for encrypted entities
- Test `PutItemRequestBuilder.WithItem()` serializes immediately for non-encrypted entities
- Test `PutAsync` resolves deferred serialization via hydrator registry
- Test transaction and batch builders resolve deferred serialization during `ExecuteAsync`

### Property-Based Tests

- Generate random entity configurations (varying combinations of encrypted/blob/plain properties) and verify `RequiresHydrator` returns the correct value
- Generate random entity instances with encrypted fields and verify round-trip serialization/deserialization preserves all field values
- Generate random non-encrypted entity instances and verify the fix does not alter their serialization behavior

### Integration Tests

- End-to-end Put + Get of an encryption-only entity with a configured `IFieldEncryptor`
- End-to-end Query returning encryption-only entities with decrypted fields
- Transaction write containing a mix of encrypted and non-encrypted entity puts
- Batch write containing encrypted entity puts
- Verify `DynamoDbOperationContext` is correctly populated after encrypted entity operations
