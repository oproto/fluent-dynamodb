# Design: Related Entity Generic Mapping Fix

## Overview

Replace the three "generic mapping" TODO stubs in `MapperGenerator.cs` with proper `FromDynamoDb` / `FromDynamoDbAsync` calls using the element type already available from `GetCollectionElementType()` or `GetBaseType()`.

When a `[RelatedEntity]` attribute does not explicitly specify `EntityType`, the source generator currently emits code that creates empty instances (`new ElementType()`) with a TODO comment. This design replaces those stubs with actual deserialization calls that mirror the existing explicit-`EntityType` code path.

## Changes Required

### 1. `GenerateRelatedEntityCollectionMapping` (sync, ~line 5120)

**Current (broken):**
```csharp
sb.AppendLine($"                        // Generic mapping to {elementType}");
sb.AppendLine($"                        var relatedEntity = new {elementType}();");
sb.AppendLine($"                        // TODO: Implement generic property mapping for {elementType}");
sb.AppendLine($"                        {relationship.PropertyName.ToLowerInvariant()}Items.Add(relatedEntity);");
```

**Fixed:**
```csharp
sb.AppendLine($"                        // Map related entity using inferred type: {elementType}");
sb.AppendLine("                        try");
sb.AppendLine("                        {");
sb.AppendLine($"                            var relatedEntity = {elementType}.FromDynamoDb<{elementType}>(item, options);");
sb.AppendLine($"                            {relationship.PropertyName.ToLowerInvariant()}Items.Add(relatedEntity);");
sb.AppendLine("                        }");
sb.AppendLine("                        catch (Exception ex)");
sb.AppendLine("                        {");
sb.AppendLine($"                            options?.Logger?.LogWarning(Oproto.FluentDynamoDb.Logging.LogEventIds.RelatedEntityMappingFailed,");
sb.AppendLine($"                                \"Failed to deserialize related entity {{EntityType}} with sort key {{SortKey}}: {{Error}}\",");
sb.AppendLine($"                                \"{elementType}\", sortKey, ex.Message);");
sb.AppendLine("                        }");
```

### 2. `GenerateRelatedEntityCollectionMappingAsync` (async, ~line 2555)

**Current (broken):**
```csharp
sb.AppendLine($"                            // Generic mapping to {elementType}");
sb.AppendLine($"                            var relatedEntity = new {elementType}();");
sb.AppendLine($"                            {relationship.PropertyName.ToLowerInvariant()}Items.Add(relatedEntity);");
```

**Fixed:**
```csharp
sb.AppendLine($"                            // Map related entity using inferred type: {elementType}");
sb.AppendLine("                            try");
sb.AppendLine("                            {");
sb.AppendLine($"                                var relatedEntity = await {elementType}.FromDynamoDbAsync<{elementType}>(item, blobProvider, fieldEncryptor, options, cancellationToken).ConfigureAwait(false);");
sb.AppendLine($"                                {relationship.PropertyName.ToLowerInvariant()}Items.Add(relatedEntity);");
sb.AppendLine("                            }");
sb.AppendLine("                            catch (Exception ex)");
sb.AppendLine("                            {");
sb.AppendLine($"                                options?.Logger?.LogWarning(Oproto.FluentDynamoDb.Logging.LogEventIds.RelatedEntityMappingFailed,");
sb.AppendLine($"                                    \"Failed to deserialize related entity {{EntityType}} with sort key {{SortKey}}: {{Error}}\",");
sb.AppendLine($"                                    \"{elementType}\", sortKey, ex.Message);");
sb.AppendLine("                            }");
```

### 3. `GenerateRelatedEntitySingleMapping` (sync single-entity, ~line 5219)

**Current (broken):**
```csharp
sb.AppendLine($"                        // Generic mapping to {propertyType}");
sb.AppendLine($"                        entity.{relationship.PropertyName} = new {propertyType}();");
sb.AppendLine($"                        // TODO: Implement generic property mapping for {propertyType}");
sb.AppendLine("                        break; // Found the related entity");
```

**Fixed:**
```csharp
sb.AppendLine($"                        // Map related entity using inferred type: {propertyType}");
sb.AppendLine("                        try");
sb.AppendLine("                        {");
sb.AppendLine($"                            entity.{relationship.PropertyName} = {propertyType}.FromDynamoDb<{propertyType}>(item, options);");
sb.AppendLine("                        }");
sb.AppendLine("                        catch (Exception ex)");
sb.AppendLine("                        {");
sb.AppendLine($"                            options?.Logger?.LogWarning(Oproto.FluentDynamoDb.Logging.LogEventIds.RelatedEntityMappingFailed,");
sb.AppendLine($"                                \"Failed to deserialize related entity {{EntityType}} with sort key {{SortKey}}: {{Error}}\",");
sb.AppendLine($"                                \"{propertyType}\", sortKey, ex.Message);");
sb.AppendLine("                        }");
sb.AppendLine("                        break; // Found the related entity");
```

## Design Decisions

1. **Error handling consistency**: The fix wraps the `FromDynamoDb` call in try/catch with warning logging, matching the pattern used when `EntityType` IS explicitly specified. This ensures graceful degradation if deserialization fails.

2. **No behavioral change to explicit EntityType path**: When `EntityType` is specified, the existing code path is unchanged. The fix only affects the else branch.

3. **Type inference source**: For collections, the type is inferred from `GetCollectionElementType(relationship.PropertyType)` which extracts the generic type argument (e.g., `UserSubscription` from `List<UserSubscription>`). For single properties, `GetBaseType(relationship.PropertyType)` is used.

4. **Async pattern compliance**: The async variant uses `await ... .ConfigureAwait(false)` and passes the full async parameter set (`blobProvider`, `fieldEncryptor`, `options`, `cancellationToken`) consistent with the explicit-EntityType async path.

## Files Modified

- `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` (3 locations)

## Testing Strategy

- Add unit tests in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/RelatedEntityMappingTests.cs` that verify the generated code calls `FromDynamoDb` when `EntityType` is null
- Verify existing tests still pass (regression prevention for explicit EntityType path)

## Traceability

| Design Choice | Requirement |
|---|---|
| Sync collection uses `ElementType.FromDynamoDb<ElementType>(item, options)` | 2.1 — Infer element type and call `FromDynamoDb` for sync collections |
| Async collection uses `await ElementType.FromDynamoDbAsync<ElementType>(item, blobProvider, fieldEncryptor, options, cancellationToken).ConfigureAwait(false)` | 2.2 — Infer element type and call `FromDynamoDbAsync` for async collections |
| Single entity uses `PropertyType.FromDynamoDb<PropertyType>(item, options)` | 2.3 — Infer property type and call `FromDynamoDb` for single entities |
| All three paths wrapped in try/catch with `LogWarning` using `RelatedEntityMappingFailed` event ID | 3.4 — Maintain consistent error handling with the explicit EntityType path |
