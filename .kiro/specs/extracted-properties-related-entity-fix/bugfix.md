# Bugfix Requirements Document

## Introduction

`[Extracted]` properties are silently not populated when an entity has any `[RelatedEntity]` attribute. The presence of a `[RelatedEntity]` property causes the source generator to set `IsMultiItemEntity = true`, which routes `FromDynamoDb` through the multi-item deserialization code path. Both the sync and async multi-item paths in `MapperGenerator` are missing the `GenerateExtractedKeyLogic` call that the single-item paths include. This results in extracted properties retaining their default values (e.g., `DateTime.MinValue`) with no error or warning — silent data loss.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN an entity has both `[Extracted]` properties and a `[RelatedEntity]` property, and the entity is deserialized via the sync multi-item `FromDynamoDb` path THEN the system silently leaves `[Extracted]` properties at their default values (e.g., `DateTime.MinValue` for `DateTime` properties) because `GenerateMultiItemFromDynamoDb` does not call `GenerateExtractedKeyLogic`

1.2 WHEN an entity has both `[Extracted]` properties and a `[RelatedEntity]` property, and the entity is deserialized via the async multi-item `FromDynamoDbAsync` path with multiple items THEN the system silently leaves `[Extracted]` properties at their default values because `GenerateMultiItemFromDynamoDbAsync` does not call `GenerateExtractedKeyLogic`

1.3 WHEN an entity has `[Extracted]` properties and a `[RelatedEntity]` property is removed from the class definition THEN the system correctly populates `[Extracted]` properties, demonstrating the bug is caused specifically by the multi-item code path routing

### Expected Behavior (Correct)

2.1 WHEN an entity has both `[Extracted]` properties and a `[RelatedEntity]` property, and the entity is deserialized via the sync multi-item `FromDynamoDb` path THEN the system SHALL populate all `[Extracted]` properties by calling `GenerateExtractedKeyLogic` after `GeneratePrimaryEntityIdentification` completes property deserialization from `primaryItem`

2.2 WHEN an entity has both `[Extracted]` properties and a `[RelatedEntity]` property, and the entity is deserialized via the async multi-item `FromDynamoDbAsync` path with multiple items THEN the system SHALL populate all `[Extracted]` properties by calling `GenerateExtractedKeyLogic` after `GenerateAsyncPrimaryEntityIdentification` completes property deserialization from `primaryItem`

2.3 WHEN an entity has both `[Extracted]` properties and a `[RelatedEntity]` property THEN the generated code SHALL contain the extraction logic (e.g., `entity.CreationDateTime = DateTime.Parse(orderidParts[actualIndex])`) in both sync and async multi-item code paths

### Unchanged Behavior (Regression Prevention)

3.1 WHEN an entity has `[Extracted]` properties but NO `[RelatedEntity]` property (single-item entity) THEN the system SHALL CONTINUE TO populate `[Extracted]` properties correctly via the single-item sync `FromDynamoDb` path

3.2 WHEN an entity has `[Extracted]` properties but NO `[RelatedEntity]` property (single-item entity) THEN the system SHALL CONTINUE TO populate `[Extracted]` properties correctly via the single-item async `FromDynamoDbAsync` path

3.3 WHEN an entity has `[RelatedEntity]` properties but NO `[Extracted]` properties THEN the system SHALL CONTINUE TO deserialize the entity correctly without generating unnecessary extraction code in the multi-item paths

3.4 WHEN a multi-item entity is deserialized via the async path with exactly one item (short-circuit path) THEN the system SHALL CONTINUE TO delegate to the single-item `FromDynamoDbAsync` path where extraction already works correctly
