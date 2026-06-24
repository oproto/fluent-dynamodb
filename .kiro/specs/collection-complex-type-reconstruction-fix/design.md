# Design: Collection Complex Type Reconstruction Fix

## Overview

Replace the TODO stub in `GenerateCollectionPropertyFromItems` with proper deserialization of complex-type collection elements using `FromDynamoDb`.

## Context

The method `GenerateCollectionPropertyFromItems` is used during multi-item `FromDynamoDb` reconstruction. It iterates DynamoDB items looking for the collection's attribute name, then processes each matched `AttributeValue`.

For a complex type collection property like `List<Address>`, the `AttributeValue` found in the item will be one of:
- A **Map** (`.M` contains the entity's attributes) — when the item stores the value as a Map attribute
- A **List** (`.L` contains a list of Maps) — when the item stores the collection as a List of Maps

The most common case for multi-item paths: each DynamoDB item has the attribute as a Map value (since the multi-item path iterates multiple items, each contributing one element to the collection). However, the List case should also be handled for completeness.

## Change Required

In `GenerateCollectionPropertyFromItems` (~line 4097 of `MapperGenerator.cs`), replace:

```csharp
if (IsComplexType(elementType))
{
    // For complex types, we'd need to reconstruct the object
    sb.AppendLine($"                    // TODO: Implement complex type reconstruction for {elementType}");
    sb.AppendLine($"                    // For now, create default instance");
    sb.AppendLine($"                    var {collectionProperty.PropertyName.ToLowerInvariant()}Item = new {elementType}();");
    sb.AppendLine($"                    {collectionProperty.PropertyName.ToLowerInvariant()}List.Add({collectionProperty.PropertyName.ToLowerInvariant()}Item);");
}
```

With:

```csharp
if (IsComplexType(elementType))
{
    var varPrefix = collectionProperty.PropertyName.ToLowerInvariant();
    // Complex type: deserialize from Map AttributeValue
    sb.AppendLine($"                    if ({varPrefix}Value.M != null && {varPrefix}Value.M.Count > 0)");
    sb.AppendLine("                    {");
    sb.AppendLine("                        try");
    sb.AppendLine("                        {");
    sb.AppendLine($"                            var {varPrefix}Item = {elementType}.FromDynamoDb<{elementType}>({varPrefix}Value.M, options);");
    sb.AppendLine($"                            {varPrefix}List.Add({varPrefix}Item);");
    sb.AppendLine("                        }");
    sb.AppendLine("                        catch (Exception ex)");
    sb.AppendLine("                        {");
    sb.AppendLine($"                            options?.Logger?.LogWarning(Oproto.FluentDynamoDb.Logging.LogEventIds.MappingFailed,");
    sb.AppendLine($"                                \"Failed to deserialize collection element {{ElementType}}: {{Error}}\",");
    sb.AppendLine($"                                \"{elementType}\", ex.Message);");
    sb.AppendLine("                        }");
    sb.AppendLine("                    }");
    sb.AppendLine($"                    else if ({varPrefix}Value.L != null && {varPrefix}Value.L.Count > 0)");
    sb.AppendLine("                    {");
    sb.AppendLine($"                        // List of Maps - deserialize each entry");
    sb.AppendLine($"                        foreach (var listEntry in {varPrefix}Value.L)");
    sb.AppendLine("                        {");
    sb.AppendLine("                            if (listEntry.M != null && listEntry.M.Count > 0)");
    sb.AppendLine("                            {");
    sb.AppendLine("                                try");
    sb.AppendLine("                                {");
    sb.AppendLine($"                                    var {varPrefix}Item = {elementType}.FromDynamoDb<{elementType}>(listEntry.M, options);");
    sb.AppendLine($"                                    {varPrefix}List.Add({varPrefix}Item);");
    sb.AppendLine("                                }");
    sb.AppendLine("                                catch (Exception ex)");
    sb.AppendLine("                                {");
    sb.AppendLine($"                                    options?.Logger?.LogWarning(Oproto.FluentDynamoDb.Logging.LogEventIds.MappingFailed,");
    sb.AppendLine($"                                        \"Failed to deserialize collection element {{ElementType}}: {{Error}}\",");
    sb.AppendLine($"                                        \"{elementType}\", ex.Message);");
    sb.AppendLine("                                }");
    sb.AppendLine("                            }");
    sb.AppendLine("                        }");
    sb.AppendLine("                    }");
}
```

## Design Decisions

1. **Map-first check**: The primary case is a single Map per item (each DynamoDB item contributes one collection element). Check `.M` first.

2. **List-of-Maps fallback**: If the attribute value is a List (`.L`), iterate each entry and deserialize Maps within. This handles the case where one item stores the entire collection as a DynamoDB List attribute.

3. **Error handling**: Wrap `FromDynamoDb` calls in try/catch with warning logging, consistent with the related entity mapping pattern. Use `LogEventIds.MappingFailed` since this is a property mapping failure rather than a related entity failure.

4. **Graceful skip**: On deserialization failure, skip the element and continue. Don't fail the entire entity reconstruction.

5. **No behavioral change for primitives**: The `else` branch (primitive types) remains unchanged.

6. **`FromDynamoDb` parameter compatibility**: The generated `FromDynamoDb<T>(Dictionary<string, AttributeValue> item, ...)` expects a `Dictionary<string, AttributeValue>`. The `.M` property on `AttributeValue` is exactly this type, so it can be passed directly.

## Files Modified

- `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` (1 location: `GenerateCollectionPropertyFromItems`)

## Testing Strategy

- Add unit test verifying generated code calls `FromDynamoDb` for complex types
- Verify primitive collection path is unchanged (regression)
- Run full source generator test suite

## Traceability

| Design Choice | Requirement |
|---|---|
| Deserialize from `.M` using `FromDynamoDb` | 2.1 — Deserialize Map to reconstruct complex object |
| Generate `FromDynamoDb` call instead of `new T()` | 2.2 — Use element type's generated `FromDynamoDb` method |
| Handle `.L` list of Maps with iteration | 2.3 — Iterate List entries and deserialize each Map |
| try/catch with `LogWarning` | Consistent error handling (follows related entity pattern) |
| Primitive `else` branch unchanged | 3.1 — Primitive conversion logic unchanged |
