# Design Document: JsonBlob Composite Entity Fix

## Overview

This design addresses a bug in the FluentDynamoDb source generator where `[JsonBlob]` properties are incorrectly deserialized when processing composite entities via `ToCompositeEntityAsync()`. The root cause is that the generated `FromDynamoDb` method for related entities incorrectly uses `Enum.Parse()` or other incorrect deserialization logic instead of the configured JSON serializer.

The fix involves ensuring that the `GenerateRelatedEntityCollectionMapping` and `GenerateRelatedEntitySingleMapping` methods in `MapperGenerator.cs` correctly delegate to the entity's `FromDynamoDb` method, which already contains the proper JSON deserialization logic for `[JsonBlob]` properties.

## Architecture

The source generator follows this flow for composite entities:

```
ToCompositeEntityAsync<T>()
    │
    ├── Query DynamoDB for all items with matching partition key
    │
    └── T.FromDynamoDb<T>(items, options)
            │
            ├── Map primary entity from first matching item
            │
            └── GenerateRelatedEntityMapping()
                    │
                    ├── For each [RelatedEntity] property:
                    │   └── RelatedEntityType.FromDynamoDb<RelatedEntityType>(item, options)
                    │       └── GenerateJsonBlobPropertyFromAttributeValue() ← Bug is here
                    │
                    └── Populate related entity collections/properties
```

The bug occurs because the generated code for related entity mapping may not correctly invoke the `FromDynamoDb` method with the `options` parameter, or the `FromDynamoDb` method itself may have incorrect deserialization logic for certain property types.

## Components and Interfaces

### Affected Components

1. **MapperGenerator.cs** - The source generator that produces entity mapping code
   - `GenerateRelatedEntityCollectionMapping()` - Maps collections of related entities
   - `GenerateRelatedEntitySingleMapping()` - Maps single related entities
   - `GenerateJsonBlobPropertyFromAttributeValue()` - Deserializes JsonBlob properties

2. **EntityExecuteAsyncExtensions.cs** - Runtime extension methods
   - `ToCompositeEntityAsync<T>()` - Executes query and assembles composite entity

### Key Interfaces

```csharp
// IJsonBlobSerializer - Used for JsonBlob property serialization
public interface IJsonBlobSerializer
{
    string Serialize<T>(T value);
    T Deserialize<T>(string json);
}

// IDynamoDbEntity - Entity interface with FromDynamoDb method
public interface IDynamoDbEntity
{
    static abstract T FromDynamoDb<T>(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null) where T : class, IDynamoDbEntity;
    static abstract T FromDynamoDb<T>(IReadOnlyList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null) where T : class, IDynamoDbEntity;
    // ... other members
}
```

## Data Models

No changes to data models are required. The fix is purely in the code generation logic.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Generated Code Uses JSON Deserialization for JsonBlob Properties

*For any* entity with `[JsonBlob]` properties, the generated `FromDynamoDb` method SHALL contain calls to `options.JsonSerializer.Deserialize<T>()` for those properties, not `Enum.Parse()` or other incorrect deserialization methods.

**Validates: Requirements 1.1**

### Property 2: JsonBlob Round-Trip Consistency

*For any* valid entity instance with `[JsonBlob]` properties (including nullable and collection types), serializing via `ToDynamoDb` then deserializing via `FromDynamoDb` SHALL produce an object where all JsonBlob property values are equivalent to the original.

**Validates: Requirements 1.3, 1.4, 2.1, 4.1**

### Property 3: Composite Entity JsonBlob Round-Trip

*For any* composite entity with related entities containing `[JsonBlob]` properties, querying via `ToCompositeEntityAsync` SHALL correctly deserialize all JsonBlob properties in both the primary entity and all related entities.

**Validates: Requirements 1.2, 4.2**

### Property 4: Loading Path Consistency

*For any* entity that can be loaded both directly (via `GetItemAsync`) and as a related entity (via `ToCompositeEntityAsync`), the JsonBlob property values SHALL be identical regardless of the loading path used.

**Validates: Requirements 2.2**

### Property 5: Single-Item and Multi-Item FromDynamoDb Equivalence

*For any* entity, calling `FromDynamoDb` with a single-item dictionary SHALL produce the same result as calling `FromDynamoDb` with a list containing only that item (for the primary entity properties).

**Validates: Requirements 2.3**

## Error Handling

### Missing JSON Serializer

When a `[JsonBlob]` property is encountered but no JSON serializer is configured:

```csharp
if (options?.JsonSerializer == null)
{
    throw new InvalidOperationException(
        $"Property '{propertyName}' has [JsonBlob] attribute but no JSON serializer is configured. " +
        "Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.");
}
```

**Validates: Requirements 3.1**

### JSON Deserialization Failure

When JSON deserialization fails:

```csharp
catch (Exception ex)
{
    throw DynamoDbMappingException.PropertyConversionFailed(
        typeof(EntityType),
        "PropertyName",
        attributeValue,
        typeof(PropertyType),
        ex)
        .WithContext("SerializerType", "RuntimeConfigured")
        .WithContext("PropertyType", "TargetType")
        .WithContext("Operation", "JsonDeserialization");
}
```

**Validates: Requirements 3.2**

### Related Entity Deserialization Error

When deserialization fails in a related entity, the error message includes the related entity type:

```csharp
throw new DynamoDbMappingException(
    $"Failed to map related entity {relatedEntityType.Name} for property {propertyName}. " +
    $"Error: {ex.Message}", ex);
```

**Validates: Requirements 3.3**

## Testing Strategy

### Unit Tests

1. **Source Generator Output Tests** - Verify generated code contains correct JSON deserialization calls
2. **Error Handling Tests** - Verify correct exceptions are thrown for missing serializer and malformed JSON
3. **Edge Case Tests** - Test null values, empty collections, nested objects

### Property-Based Tests

Using FsCheck or similar property-based testing library:

1. **Property 1**: Generate random entity definitions with JsonBlob properties, run source generator, verify output contains `JsonSerializer.Deserialize` calls
2. **Property 2**: Generate random entity instances with JsonBlob properties, round-trip through ToDynamoDb/FromDynamoDb, verify equivalence
3. **Property 3**: Generate random composite entities with related entities containing JsonBlob properties, verify round-trip preserves all data
4. **Property 4**: Generate random entities, load via both paths, verify identical results
5. **Property 5**: Generate random entities, call FromDynamoDb with single item and list, verify equivalence

### Integration Tests

1. **End-to-End Composite Entity Test** - Create composite entity with JsonBlob properties, save to DynamoDB Local, load via ToCompositeEntityAsync, verify all properties
2. **Mixed Property Types Test** - Test entities with mix of JsonBlob, regular, and collection properties

### Test Configuration

- Property tests: Minimum 100 iterations per property
- Use FsCheck for C# property-based testing
- Tag format: **Feature: jsonblob-composite-entity-fix, Property {number}: {property_text}**
