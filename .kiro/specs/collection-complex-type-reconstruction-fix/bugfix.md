# Bugfix Requirements Document

## Introduction

The source generator's `GenerateCollectionPropertyFromItems` method in `MapperGenerator.cs` contains a TODO stub that fails to deserialize complex-type collection elements during multi-item `FromDynamoDb` reconstruction. When a composite entity has a collection property with a complex element type (e.g., `List<Address>` where `Address` is a `[DynamoDbEntity]` type), the multi-item path creates empty default instances instead of deserializing the `AttributeValue` data from DynamoDB Maps. This affects entities retrieved via `ToCompositeEntityAsync` that have both `[RelatedEntity]` properties and `List<T>` properties where `T` is a complex type.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a composite entity has a `List<T>` property where `T` is a complex type (marked with `[DynamoDbEntity]` or `[DynamoDbMap]`) AND the entity is reconstructed via the multi-item `FromDynamoDb` path THEN the system creates empty default instances of `T` with `new T()` instead of deserializing the `AttributeValue` data

1.2 WHEN the `GenerateCollectionPropertyFromItems` method encounters a complex element type (where `IsComplexType(elementType)` returns true) THEN the system emits a TODO comment and a `new {elementType}()` call, discarding the `AttributeValue` containing the serialized Map data

1.3 WHEN the multi-item async path (`FromDynamoDbAsync`) encounters a collection property with complex elements THEN the system produces the same defective output as the sync path, since both call the same `GenerateCollectionPropertyFromItems` method

### Expected Behavior (Correct)

2.1 WHEN a composite entity has a `List<T>` property where `T` is a complex type AND the `AttributeValue` is a Map (has `.M` property) THEN the system SHALL deserialize the Map by calling `T.FromDynamoDb<T>(mapAttributeValue, options)` to reconstruct the complex object with all its properties populated

2.2 WHEN the `GenerateCollectionPropertyFromItems` method encounters a complex element type THEN the system SHALL generate code that extracts the Map from the `AttributeValue` and invokes the element type's generated `FromDynamoDb` method to reconstruct the object

2.3 WHEN the `AttributeValue` for a collection property is a List type (has `.L` property containing Maps) THEN the system SHALL iterate the List entries and deserialize each Map entry by calling `T.FromDynamoDb<T>(entry, options)` for each element

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a composite entity has a `List<T>` property where `T` is a primitive type (string, int, decimal, etc.) THEN the system SHALL CONTINUE TO convert the `AttributeValue` directly using the existing primitive conversion logic

3.2 WHEN a composite entity is retrieved via the single-item path (only one DynamoDB item returned) THEN the system SHALL CONTINUE TO short-circuit to the single-item `FromDynamoDb` deserialization which already handles complex collections correctly

3.3 WHEN the `GenerateCollectionPropertyFromItems` method processes collection properties that have no matching attribute in the DynamoDB items THEN the system SHALL CONTINUE TO produce an empty list without errors

3.4 WHEN the sync multi-item `FromDynamoDb` path processes non-collection properties (scalar properties, related entities) THEN the system SHALL CONTINUE TO use the existing `GeneratePrimaryEntityIdentification` and `GenerateRelatedEntityMapping` logic unchanged
