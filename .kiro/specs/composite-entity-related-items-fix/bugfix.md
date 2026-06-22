# Bugfix Requirements Document

## Introduction

`ToCompositeEntityAsync` fails to populate `[RelatedEntity]` collection properties on composite entities that have encrypted or blob properties. The generated multi-item `FromDynamoDbAsync` overload (used for entities with `[Encrypted]` properties) is a stub that only processes `items[0]` and discards all other items — it never performs primary entity identification or related entity pattern matching. This means the composite entity assembly logic that works correctly in the sync `FromDynamoDb` multi-item method (used by InvoiceManager and other non-encrypted entities) is completely absent from the async path.

The root cause is in the source generator: `GenerateFromDynamoDbAsyncMultiItemMethod` (or equivalent) emits a trivial delegation to the single-item `FromDynamoDbAsync(items[0], ...)` rather than replicating the composite assembly logic from `GenerateFromDynamoDbMultiItemMethod`.

This affects any entity with `[Encrypted]` or `[BlobReference]` properties that also uses `[RelatedEntity]` for composite entity queries — the async hydration path never gets the multi-item assembly treatment.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN an entity has `[Encrypted]` properties AND `[RelatedEntity]` collection properties, and `ToCompositeEntityAsync` is called on query results containing both the parent item and related items, THEN the generated `FromDynamoDbAsync(IList<Dictionary<string, AttributeValue>> items, ...)` overload only processes `items[0]` and returns the entity with empty related collections.

1.2 WHEN the multi-item `FromDynamoDbAsync` is called with a list of items, THEN it delegates to `FromDynamoDbAsync(items[0], ...)` discarding all items after index 0, regardless of whether they match `[RelatedEntity]` patterns.

1.3 WHEN the entity has no `[Encrypted]` or `[BlobReference]` properties, THEN `ToCompositeEntityAsync` correctly routes through the sync `FromDynamoDb(IList<...> items, ...)` multi-item overload which performs full composite assembly (primary identification via regex, related entity pattern matching, collection population).

1.4 WHEN the hydrator is registered (due to `[Encrypted]` properties), THEN `ToCompositeEntityAsync` calls `hydrator.HydrateAsync(items, ...)` which eventually calls the async multi-item `FromDynamoDbAsync` stub — bypassing the composite assembly logic entirely.

### Expected Behavior (Correct)

2.1 WHEN an entity has `[Encrypted]` properties AND `[RelatedEntity]` collection properties, and `ToCompositeEntityAsync` is called with multiple items, THEN the generated `FromDynamoDbAsync(IList<...> items, ...)` SHALL identify the primary entity item (by excluding items matching `[RelatedEntity]` sort key patterns) and populate related entity collections from matching items.

2.2 WHEN the multi-item `FromDynamoDbAsync` is called, THEN it SHALL perform the same composite assembly logic as the sync `FromDynamoDb(IList<...> items, ...)`: primary entity identification, related entity pattern matching, collection population, and recursive assembly for nested relationships.

2.3 WHEN populating related entity collections in the async path, THEN each related item SHALL be deserialized using the related entity's `FromDynamoDbAsync` (if the related entity also has encrypted/blob properties) or `FromDynamoDb` (if it does not), preserving correct handling of encrypted fields on child entities.

2.4 WHEN the parent entity's sort key pattern overlaps with related entity patterns, THEN the primary entity identification SHALL use regex exclusion of `[RelatedEntity]` patterns (same as the sync path) to correctly distinguish the parent item from related items.

### Unchanged Behavior (Regression Prevention)

3.1 WHEN entities WITHOUT `[Encrypted]` or `[BlobReference]` properties use `ToCompositeEntityAsync`, THEN the system SHALL CONTINUE TO route through the sync `FromDynamoDb` multi-item path and correctly populate related collections (as InvoiceManager demonstrates).

3.2 WHEN `ToCompositeEntityAsync` is called on query results containing only the parent entity item (no related items), THEN the system SHALL CONTINUE TO return the parent entity with empty related entity collections regardless of whether it has encrypted properties.

3.3 WHEN `ToListAsync` is called (not composite entity assembly) on encrypted entities, THEN the system SHALL CONTINUE TO use the hydrator's single-item `HydrateAsync` path for each item independently.

3.4 WHEN the entity has `[Encrypted]` properties but no `[RelatedEntity]` properties, THEN the multi-item `FromDynamoDbAsync(items[0], ...)` delegation is acceptable (no composite assembly needed) and SHALL CONTINUE TO work.

3.5 WHEN composite entities are queried where the parent's discriminator does NOT overlap with related entity patterns, THEN the system SHALL CONTINUE TO correctly populate related entity collections.

## Root Cause

The source generator generates two variants of multi-item `FromDynamoDb`:

1. **Sync path** (`FromDynamoDb<TSelf>(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options)`): Generated by `GenerateFromDynamoDbMultiItemMethod` in `MapperGenerator.cs`. Contains full composite assembly: `GeneratePrimaryEntityIdentification`, `GenerateRelatedEntityMapping`, regex-based sort key pattern matching.

2. **Async path** (`FromDynamoDbAsync<TSelf>(IList<Dictionary<string, AttributeValue>> items, IBlobStorageProvider?, IFieldEncryptor?, ...)`): Generated as a stub that only calls `FromDynamoDbAsync<TSelf>(items[0], ...)`. Missing all composite assembly logic.

The fix must generate equivalent composite assembly logic in the async multi-item method, accounting for the fact that related entity deserialization may also need async handling (if child entities have encrypted properties).
