# Bugfix Requirements Document

## Introduction

The source generator's `MapperGenerator.cs` fails to properly deserialize related entities when the `[RelatedEntity]` attribute does not explicitly specify `EntityType`. Instead of inferring the entity type from the collection's generic type parameter (or the property type for single entities) and calling `FromDynamoDb`, the generated code creates empty instances with a TODO comment. This results in related entity properties being populated with default/empty objects that contain none of the actual DynamoDB item data.

This bug affects three code generation paths: sync collection mapping, async collection mapping, and single entity mapping.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a `[RelatedEntity]` attribute is used on a collection property (e.g., `List<UserSubscription>`) without specifying `EntityType` THEN the sync generated code creates an empty instance via `new ElementType()` and adds it to the collection without deserializing the DynamoDB item data

1.2 WHEN a `[RelatedEntity]` attribute is used on a collection property without specifying `EntityType` THEN the async generated code creates an empty instance via `new ElementType()` and adds it to the collection without deserializing the DynamoDB item data

1.3 WHEN a `[RelatedEntity]` attribute is used on a single (non-collection) property without specifying `EntityType` THEN the generated code assigns `new PropertyType()` to the property without deserializing the DynamoDB item data

### Expected Behavior (Correct)

2.1 WHEN a `[RelatedEntity]` attribute is used on a collection property without specifying `EntityType` THEN the sync generated code SHALL infer the element type from the collection's generic type parameter and call `ElementType.FromDynamoDb<ElementType>(item, options)` to properly deserialize the DynamoDB item

2.2 WHEN a `[RelatedEntity]` attribute is used on a collection property without specifying `EntityType` THEN the async generated code SHALL infer the element type from the collection's generic type parameter and call `await ElementType.FromDynamoDbAsync<ElementType>(item, blobProvider, fieldEncryptor, options, cancellationToken).ConfigureAwait(false)` to properly deserialize the DynamoDB item

2.3 WHEN a `[RelatedEntity]` attribute is used on a single (non-collection) property without specifying `EntityType` THEN the generated code SHALL infer the property type and call `PropertyType.FromDynamoDb<PropertyType>(item, options)` to properly deserialize the DynamoDB item

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a `[RelatedEntity]` attribute explicitly specifies `EntityType` on a collection property THEN the system SHALL CONTINUE TO use the specified entity type for `FromDynamoDb` deserialization with try/catch error handling

3.2 WHEN a `[RelatedEntity]` attribute explicitly specifies `EntityType` on a single property THEN the system SHALL CONTINUE TO use the specified entity type for `FromDynamoDb` deserialization with try/catch error handling

3.3 WHEN a `[RelatedEntity]` attribute explicitly specifies `EntityType` on an entity with child relationships THEN the system SHALL CONTINUE TO perform recursive assembly via item grouping and multi-item `FromDynamoDb`

3.4 WHEN a `[RelatedEntity]` attribute is used without `EntityType` THEN the generated code SHALL CONTINUE TO use try/catch with warning logging for graceful error handling, consistent with the explicit `EntityType` code path
