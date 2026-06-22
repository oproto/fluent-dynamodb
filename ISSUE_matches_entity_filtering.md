# MatchesEntity silently drops items with missing non-nullable attributes

## Summary

The source-generated `MatchesEntity` method uses attribute-presence checks on ALL non-nullable properties as a heuristic for entity type discrimination. This causes items to be silently filtered out of query/scan results when any non-nullable property is missing from the DynamoDB item — even when the item legitimately belongs to that entity type.

## Symptoms

- `Query<TEntity>()` / `Scan<TEntity>()` / `Get<TEntity>()` return zero results or fewer results than expected
- No errors or exceptions — items are silently dropped
- Raw AWS SDK queries against the same table/index return the correct items
- Adding a value to an otherwise-empty collection property "fixes" the query

## Root Cause

`GenerateMatchesEntityMethod` in `MapperGenerator.cs` generates a check like:

```csharp
public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
{
    if (!item.ContainsKey("pk")) return false;
    if (!item.ContainsKey("sk")) return false;
    if (!item.ContainsKey("gsi1pk")) return false;
    if (!item.ContainsKey("emails")) return false;   // empty List<T> may not be stored
    if (!item.ContainsKey("phones")) return false;   // empty List<T> may not be stored
    if (!item.ContainsKey("tenantId")) return false;
    // ... every non-nullable property
    return true;
}
```

This fails when:

1. **Empty collections** — `List<T>` or `Dictionary<K,V>` initialized to `new()` may not be persisted by DynamoDB (empty lists/maps are sometimes omitted)
2. **Schema evolution** — a new non-nullable property is added to the entity class, but existing records in the database don't have that attribute. Those records silently vanish from all queries.
3. **Sparse writes** — an item was written with only key attributes and a subset of data attributes (common in update-heavy patterns)

## The Design Tension

`MatchesEntity` exists because multi-entity tables (single-table design) return mixed entity types from queries and scans. Without filtering:

- `Scan<EmployeeEntity>()` would return Contractor, PayRate, and Deduction items hydrated into `EmployeeEntity` objects
- `FromDynamoDb` would map whatever attributes happen to match by name, producing silent data corruption
- A Contractor item with `pk`, `sk`, `firstName` fields would become an `EmployeeEntity` with the contractor's data

So the check is necessary — but the current implementation is wrong.

## Current Behavior Analysis

**Two failure modes:**

| Scenario | Current Behavior | Correct Behavior |
|----------|-----------------|------------------|
| Item belongs to TEntity, missing empty collection | **Silently dropped** (false negative) | Should be returned |
| Item belongs to TEntity, added new field since write | **Silently dropped** (false negative) | Should be returned |
| Item belongs to different entity type | Correctly filtered out | Correctly filtered out |
| Item belongs to different type with similar schema | **May pass through** (false positive) | Should be filtered out |

The attribute-presence heuristic is both too strict (drops valid items) and too weak (can't reliably distinguish entity types with similar schemas).

## The Two Layers

There are actually two distinct concerns being conflated:

### Layer 1: Entity Type Discrimination
"Is this item an EmployeeEntity or a ContractorEntity?"

This is what discriminators solve. The `DiscriminatorProperty`/`DiscriminatorPattern` system already exists for this:
```csharp
[DynamoDbTable(typeof(EmployeesTable), DiscriminatorProperty = "sk", DiscriminatorPattern = "EMPLOYEE#*")]
```

But `GenerateMatchesEntityMethod` never uses the new discriminator config. It only checks the old deprecated `entity.EntityDiscriminator` property (which is only set for `ExactMatch` strategies, not patterns).

### Layer 2: Hydration Safety
"Can I deserialize this item without crashing or producing garbage?"

This is what `FromDynamoDb` needs to handle internally — using defaults for missing attributes, returning null/empty for unset collections, etc. It should NOT be a pre-filter that drops items.

## Proposed Fix

### For entities WITH discriminator configured (multi-entity tables):

Use the discriminator as the sole check:

```csharp
public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
{
    // Discriminator check: authoritative entity type identification
    if (!item.TryGetValue("sk", out var skValue) || skValue.S == null)
        return false;
    return skValue.S.StartsWith("EMPLOYEE#");
}
```

This is:
- Fast (one dictionary lookup, one StartsWith)
- Correct (discriminator is authoritative)
- Schema-evolution safe (doesn't depend on data attributes)
- Empty-collection safe (doesn't check data attributes)

### For single-entity tables (no discrimination needed):

```csharp
public static bool MatchesEntity(Dictionary<string, AttributeValue> item)
{
    // Single-entity table: no discrimination needed
    return item.ContainsKey("pk");  // minimal structural check
}
```

### For multi-entity tables WITHOUT discriminator (edge case):

This is the hardest case. Options:
1. **Require discriminators** — emit a compiler warning/error if a multi-entity table doesn't have discriminators configured. This is the cleanest long-term approach.
2. **Check key attributes only** — only require pk/sk to exist, accept the risk of wrong-type hydration. At least items don't disappear.
3. **Keep current behavior but exclude collections** — check non-nullable scalar properties only, skip `List<T>`, `Dictionary<K,V>`, and `[DynamoDbMap]` types. Reduces false negatives without fully solving the problem.

## Impact

- **Breaking change**: Entities that previously relied on strict attribute checks to avoid wrong-type hydration in tables without discriminators would need to add discriminator configuration
- **Fixes**: Silent data loss from empty collections, schema evolution, sparse writes
- **Performance**: Discriminator check is faster than 15+ ContainsKey calls

## Affected Code

- `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` — `GenerateMatchesEntityMethod` (line ~3607)
- Every call site of `T.MatchesEntity()` in `EntityExecuteAsyncExtensions.cs`, `CompoundEntityResult.cs`, `PartiQLRequestBuilder.cs`

## Reproduction

```csharp
// Entity with empty collection
[DynamoDbTable(typeof(EmployeesTable), IsDefault = true,
    DiscriminatorProperty = "sk", DiscriminatorPattern = "EMPLOYEE#*")]
public partial class EmployeeEntity
{
    [PartitionKey] [DynamoDbAttribute("pk")] public string Pk { get; set; } = string.Empty;
    [SortKey] [DynamoDbAttribute("sk")] public string Sk { get; set; } = string.Empty;
    [DynamoDbAttribute("name")] public string Name { get; set; } = string.Empty;
    [DynamoDbMap] [DynamoDbAttribute("phones")] public List<PhoneModel> Phones { get; set; } = new();  // empty list
}

// Put without setting Phones (stays as empty list)
var emp = new EmployeeEntity { Pk = "T#1", Sk = "EMPLOYEE#1", Name = "Alice" };
await table.Employees.Put(emp).PutAsync();

// Query — returns 0 items because "phones" attribute not stored
var result = await table.Gsi1.Query<EmployeeEntity>(...).ToListAsync();
// result.Count == 0  (should be 1)

// Raw SDK query — returns 1 item
var raw = await client.QueryAsync(...);
// raw.Items.Count == 1
```

## Related

- `DiscriminatorAnalyzer.cs` — already parses discriminator config
- `entity.Discriminator` — populated but never used in `MatchesEntity`
- `entity.EntityDiscriminator` (deprecated) — the only thing currently checked
