# FDDB104: Compound discrimination resolved overlap

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB104` |
| Severity | Info |

## Message

`Entity '{0}' promoted to compound discrimination ({1}: '{2}' + {3}: '{4}') to resolve overlap with '{5}'`

## Description

Two entities on the same table have identical (same-score) auto-derived discriminator patterns on the same key property. The source generator detected that their cross-key patterns differ and automatically promoted both entities to compound discrimination — the generated `MatchesEntity` method now checks both the primary discriminator pattern AND the cross-key pattern.

This diagnostic is informational. No action is required — the generated code correctly discriminates items using the combined key check.

## When This Fires

This diagnostic is emitted when:
1. Two entities share the same table
2. Both have the same auto-derived discriminator pattern on the same key property (e.g., both derive `CAP#*` on SK)
3. Their cross-key patterns differ (e.g., one has PK `PLATFORM#*`, the other has PK `TENANT#*`)
4. The generator promotes to compound discrimination to resolve the ambiguity

When FDDB104 is emitted, the FDDB102 and DISC004 diagnostics for that pair are **suppressed** — the overlap is fully resolved.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("capabilities", IsDefault = true)]
public partial class PlatformCapability
{
    [PartitionKey(Prefix = "PLATFORM")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "CAP")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

[DynamoDbTable("capabilities")]
public partial class TenantCapability
{
    [PartitionKey(Prefix = "TENANT")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "CAP")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```

**Diagnostic output:**
```
Info FDDB104: Entity 'PlatformCapability' promoted to compound discrimination (sk: 'CAP#*' + pk: 'PLATFORM#*') to resolve overlap with 'TenantCapability'
Info FDDB104: Entity 'TenantCapability' promoted to compound discrimination (sk: 'CAP#*' + pk: 'TENANT#*') to resolve overlap with 'PlatformCapability'
```

## Generated Behavior

The generated `MatchesEntity` for each entity checks both keys:

```csharp
// PlatformCapability.MatchesEntity checks:
// 1. sk starts with "CAP#" (primary discriminator)
// 2. pk starts with "PLATFORM#" (compound constraint)

// TenantCapability.MatchesEntity checks:
// 1. sk starts with "CAP#" (primary discriminator)
// 2. pk starts with "TENANT#" (compound constraint)
```

This ensures mutual exclusivity — any DynamoDB item with both keys present matches at most one entity.

## Fix

No fix is required. This is an informational diagnostic confirming that the source generator automatically resolved a same-score discriminator overlap using compound key discrimination. The generated `MatchesEntity` methods correctly check both the primary key pattern and the cross-key pattern.

If you want to suppress this diagnostic, you can use standard Roslyn diagnostic suppression:

```csharp
#pragma warning disable FDDB104
[DynamoDbTable("capabilities", IsDefault = true)]
public partial class PlatformCapability { /* ... */ }
#pragma warning restore FDDB104
```

## See Also

- [Discriminators — Compound Key Discrimination](../../advanced-topics/Discriminators.md#compound-key-discrimination)
