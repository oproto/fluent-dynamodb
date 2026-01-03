# Design Document

## Overview

This design extends the existing `DynamicFieldCollection` class with prefix-based accessors, typed Map operations using entity interfaces, and bulk Set/Remove operations. The design maintains AOT compatibility by leveraging static abstract interface methods from `IReadOnlyEntity` and `IDynamoDbEntity`.

## Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                      DynamicFieldCollection                          │
├─────────────────────────────────────────────────────────────────────┤
│ Existing:                                                            │
│   - _fields: Dictionary<string, AttributeValue>                      │
│   - _addedOrModified: HashSet<string>                               │
│   - _removed: HashSet<string>                                        │
│   - Typed getters/setters (GetString, SetInt, etc.)                 │
│   - Change tracking (StartTrackingChanges, ChangesOnly)             │
├─────────────────────────────────────────────────────────────────────┤
│ New - Prefix Operations:                                             │
│   + GetFieldNamesByPrefix(prefix) → IEnumerable<string>             │
│   + GetByPrefix(prefix) → Dictionary<string, AttributeValue>        │
│   + GetByPrefixWithStrippedKeys(prefix) → Dictionary<string, AV>    │
│   + RemoveByPrefix(prefix) → int                                     │
├─────────────────────────────────────────────────────────────────────┤
│ New - Typed Map Operations:                                          │
│   + GetMap<T>(fieldName, options?) → T?                             │
│   + TryGetMap<T>(fieldName, out T?, options?) → bool                │
│   + SetMap<T>(fieldName, entity, options?)                          │
│   + GetMapsByPrefix<T>(prefix, options?) → Dictionary<string, T>    │
│   + GetMapsByPrefixWithStrippedKeys<T>(prefix, options?) → Dict     │
├─────────────────────────────────────────────────────────────────────┤
│ New - Bulk Operations:                                               │
│   + SetMany(fields: Dictionary<string, AttributeValue>)             │
│   + SetManyWithPrefix(prefix, fields: Dictionary<string, AV>)       │
│   + SetMapsWithPrefix<T>(prefix, entities: Dictionary<string, T>)   │
│   + RemoveMany(fieldNames: IEnumerable<string>) → int               │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ Uses
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                         Entity Interfaces                            │
├─────────────────────────────────────────────────────────────────────┤
│ IReadOnlyEntity:                                                     │
│   static abstract T FromDynamoDb<T>(Dictionary<string, AV>, opts)   │
├─────────────────────────────────────────────────────────────────────┤
│ IDynamoDbEntity : IReadOnlyEntity:                                   │
│   static abstract Dictionary<string, AV> ToDynamoDb<T>(T, opts)     │
└─────────────────────────────────────────────────────────────────────┘
```

### Type Constraints

The typed Map operations use generic constraints to ensure AOT compatibility:

```csharp
// For reading Maps - requires FromDynamoDb
public T? GetMap<T>(string fieldName, FluentDynamoDbOptions? options = null)
    where T : IReadOnlyEntity

// For writing Maps - requires ToDynamoDb
public void SetMap<T>(string fieldName, T? entity, FluentDynamoDbOptions? options = null)
    where T : IDynamoDbEntity
```

This design leverages the static abstract interface methods that the source generator already creates for `[DynamoDbEntity]` types.

## Detailed Design

### 1. Prefix-Based Field Name Discovery

```csharp
/// <summary>
/// Gets all field names that start with the specified prefix.
/// </summary>
/// <param name="prefix">The prefix to match (e.g., "c_" for children).</param>
/// <returns>An enumerable of field names matching the prefix.</returns>
public IEnumerable<string> GetFieldNamesByPrefix(string prefix)
{
    return _fields.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal));
}
```

### 2. Prefix-Based Field Retrieval

```csharp
/// <summary>
/// Gets all fields whose names start with the specified prefix.
/// </summary>
/// <param name="prefix">The prefix to match.</param>
/// <returns>A dictionary of matching fields with full attribute names as keys.</returns>
public Dictionary<string, AttributeValue> GetByPrefix(string prefix)
{
    return _fields
        .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
}

/// <summary>
/// Gets all fields whose names start with the specified prefix, with the prefix stripped from keys.
/// </summary>
/// <param name="prefix">The prefix to match and strip.</param>
/// <returns>A dictionary of matching fields with prefix-stripped keys.</returns>
public Dictionary<string, AttributeValue> GetByPrefixWithStrippedKeys(string prefix)
{
    return _fields
        .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
        .ToDictionary(kvp => kvp.Key.Substring(prefix.Length), kvp => kvp.Value, StringComparer.Ordinal);
}
```

### 3. Prefix-Based Field Removal

```csharp
/// <summary>
/// Removes all fields whose names start with the specified prefix.
/// </summary>
/// <param name="prefix">The prefix to match.</param>
/// <returns>The number of fields removed.</returns>
public int RemoveByPrefix(string prefix)
{
    var keysToRemove = _fields.Keys
        .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
        .ToList();
    
    foreach (var key in keysToRemove)
    {
        Remove(key); // Uses existing Remove which handles change tracking
    }
    
    return keysToRemove.Count;
}
```

### 4. Typed Map Getter

```csharp
/// <summary>
/// Gets a Map field as a typed entity using the entity's FromDynamoDb method.
/// </summary>
/// <typeparam name="T">The entity type implementing IReadOnlyEntity.</typeparam>
/// <param name="fieldName">The name of the field.</param>
/// <param name="options">Optional FluentDynamoDb options.</param>
/// <returns>The deserialized entity, or null if the field does not exist.</returns>
/// <exception cref="DynamicFieldTypeException">Thrown when the field is not a Map type.</exception>
public T? GetMap<T>(string fieldName, FluentDynamoDbOptions? options = null)
    where T : IReadOnlyEntity
{
    if (!_fields.TryGetValue(fieldName, out var value))
        return default;

    if (value.NULL == true)
        return default;

    if (!value.IsMSet)
        throw new DynamicFieldTypeException(fieldName, typeof(T), GetDynamoDbTypeName(value));

    return T.FromDynamoDb<T>(value.M, options);
}

/// <summary>
/// Tries to get a Map field as a typed entity.
/// </summary>
public bool TryGetMap<T>(string fieldName, out T? value, FluentDynamoDbOptions? options = null)
    where T : IReadOnlyEntity
{
    value = default;
    
    if (!_fields.TryGetValue(fieldName, out var av))
        return false;

    if (av.NULL == true)
        return true;

    if (!av.IsMSet)
        return false;

    value = T.FromDynamoDb<T>(av.M, options);
    return true;
}
```

### 5. Typed Map Setter

```csharp
/// <summary>
/// Sets a Map field from a typed entity using the entity's ToDynamoDb method.
/// </summary>
/// <typeparam name="T">The entity type implementing IDynamoDbEntity.</typeparam>
/// <param name="fieldName">The name of the field.</param>
/// <param name="entity">The entity to serialize, or null to remove the field.</param>
/// <param name="options">Optional FluentDynamoDb options.</param>
public void SetMap<T>(string fieldName, T? entity, FluentDynamoDbOptions? options = null)
    where T : IDynamoDbEntity
{
    if (entity == null)
    {
        Remove(fieldName);
        return;
    }

    var attributes = T.ToDynamoDb(entity, options);
    _fields[fieldName] = new AttributeValue { M = attributes };
    TrackModification(fieldName);
}
```

### 6. Bulk Set Operations

```csharp
/// <summary>
/// Sets multiple fields from a dictionary of AttributeValues.
/// </summary>
/// <param name="fields">The fields to set.</param>
public void SetMany(Dictionary<string, AttributeValue> fields)
{
    if (fields == null || fields.Count == 0)
        return;

    foreach (var kvp in fields)
    {
        _fields[kvp.Key] = kvp.Value;
        TrackModification(kvp.Key);
    }
}

/// <summary>
/// Sets multiple fields with a prefix prepended to each key.
/// </summary>
/// <param name="prefix">The prefix to prepend to each key.</param>
/// <param name="fields">The fields to set (keys without prefix).</param>
public void SetManyWithPrefix(string prefix, Dictionary<string, AttributeValue> fields)
{
    if (fields == null || fields.Count == 0)
        return;

    foreach (var kvp in fields)
    {
        var fullKey = prefix + kvp.Key;
        _fields[fullKey] = kvp.Value;
        TrackModification(fullKey);
    }
}

/// <summary>
/// Sets multiple Map fields from typed entities with a prefix prepended to each key.
/// </summary>
/// <typeparam name="T">The entity type implementing IDynamoDbEntity.</typeparam>
/// <param name="prefix">The prefix to prepend to each key.</param>
/// <param name="entities">The entities to set (keys without prefix).</param>
/// <param name="options">Optional FluentDynamoDb options.</param>
public void SetMapsWithPrefix<T>(string prefix, Dictionary<string, T> entities, FluentDynamoDbOptions? options = null)
    where T : IDynamoDbEntity
{
    if (entities == null || entities.Count == 0)
        return;

    foreach (var kvp in entities)
    {
        var fullKey = prefix + kvp.Key;
        var attributes = T.ToDynamoDb(kvp.Value, options);
        _fields[fullKey] = new AttributeValue { M = attributes };
        TrackModification(fullKey);
    }
}
```

### 7. Bulk Remove Operations

```csharp
/// <summary>
/// Removes multiple fields by name.
/// </summary>
/// <param name="fieldNames">The names of the fields to remove.</param>
/// <returns>The number of fields actually removed.</returns>
public int RemoveMany(IEnumerable<string> fieldNames)
{
    var count = 0;
    foreach (var fieldName in fieldNames)
    {
        if (Remove(fieldName)) // Uses existing Remove which handles change tracking
            count++;
    }
    return count;
}
```

### 8. Prefix-Based Typed Map Retrieval

```csharp
/// <summary>
/// Gets all Map fields matching a prefix as typed entities.
/// </summary>
/// <typeparam name="T">The entity type implementing IReadOnlyEntity.</typeparam>
/// <param name="prefix">The prefix to match.</param>
/// <param name="options">Optional FluentDynamoDb options.</param>
/// <returns>A dictionary of entities with full attribute names as keys.</returns>
public Dictionary<string, T> GetMapsByPrefix<T>(string prefix, FluentDynamoDbOptions? options = null)
    where T : IReadOnlyEntity
{
    var result = new Dictionary<string, T>(StringComparer.Ordinal);
    
    foreach (var kvp in _fields)
    {
        if (!kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
            continue;
        
        if (!kvp.Value.IsMSet)
            continue; // Skip non-Map fields
        
        var entity = T.FromDynamoDb<T>(kvp.Value.M, options);
        result[kvp.Key] = entity;
    }
    
    return result;
}

/// <summary>
/// Gets all Map fields matching a prefix as typed entities, with prefix stripped from keys.
/// </summary>
public Dictionary<string, T> GetMapsByPrefixWithStrippedKeys<T>(string prefix, FluentDynamoDbOptions? options = null)
    where T : IReadOnlyEntity
{
    var result = new Dictionary<string, T>(StringComparer.Ordinal);
    
    foreach (var kvp in _fields)
    {
        if (!kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
            continue;
        
        if (!kvp.Value.IsMSet)
            continue; // Skip non-Map fields
        
        var entity = T.FromDynamoDb<T>(kvp.Value.M, options);
        result[kvp.Key.Substring(prefix.Length)] = entity;
    }
    
    return result;
}
```

## Usage Examples

### BalanceTreeNode Pattern

```csharp
// Entity definition
[DynamoDbTable("BalanceTree")]
[EnableDynamicFields]
public partial class BalanceTreeNode
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    // Fixed attributes
    [DynamoDbAttribute("v")]
    public int Version { get; set; }

    // Dynamic fields captured automatically: c_{nodeId}, t_{txnId}
}

// Nested entity for child references
[DynamoDbEntity]
public partial class ChildReference
{
    [DynamoDbAttribute("cst")]
    public decimal CurrentSubtreeTo { get; set; }

    [DynamoDbAttribute("csf")]
    public decimal CurrentSubtreeFrom { get; set; }

    [DynamoDbAttribute("fst")]
    public decimal FinalSubtreeTo { get; set; }

    [DynamoDbAttribute("fsf")]
    public decimal FinalSubtreeFrom { get; set; }
}
```

### Reading Children

```csharp
// Get a node from DynamoDB
var node = await table.BalanceTreeNodes.Get(pk, sk).GetItemAsync();

// Get all child IDs
var childIds = node.DynamicFields.GetFieldNamesByPrefix("c_")
    .Select(name => name.Substring(2)) // Strip "c_" prefix
    .ToList();

// Get all children as typed entities (with stripped keys = nodeIds)
var children = node.DynamicFields.GetMapsByPrefixWithStrippedKeys<ChildReference>("c_");
foreach (var (nodeId, child) in children)
{
    Console.WriteLine($"Child {nodeId}: To={child.CurrentSubtreeTo}, From={child.CurrentSubtreeFrom}");
}

// Get a specific child
var specificChild = node.DynamicFields.GetMap<ChildReference>("c_01ARZ3NDEKTSV4RRFFQ69G5FA1");
```

### Updating Children

```csharp
// Load node and start tracking changes
var node = await table.BalanceTreeNodes.Get(pk, sk).GetItemAsync();

// Add a new child
node.DynamicFields.SetMap("c_" + newChildId, new ChildReference
{
    CurrentSubtreeTo = 1000m,
    CurrentSubtreeFrom = 500m,
    FinalSubtreeTo = 800m,
    FinalSubtreeFrom = 400m
});

// Update an existing child
var existingChild = node.DynamicFields.GetMap<ChildReference>("c_" + existingChildId);
existingChild.CurrentSubtreeTo += 100m;
node.DynamicFields.SetMap("c_" + existingChildId, existingChild);

// Remove a child
node.DynamicFields.Remove("c_" + removedChildId);

// Update with only changes
await table.BalanceTreeNodes.Update(pk, sk)
    .Set(x => new BalanceTreeNodeUpdateModel
    {
        Version = x.Version + 1,
        DynamicFields = node.DynamicFields.ChangesOnly()
    })
    .Where(x => x.Version == node.Version)
    .UpdateAsync();
```

### Bulk Operations

```csharp
// Add multiple children at once
var newChildren = new Dictionary<string, ChildReference>
{
    ["01ARZ3NDEKTSV4RRFFQ69G5FA1"] = new ChildReference { CurrentSubtreeTo = 1000m, ... },
    ["01ARZ3NDEKTSV4RRFFQ69G5FA2"] = new ChildReference { CurrentSubtreeTo = 2000m, ... },
    ["01ARZ3NDEKTSV4RRFFQ69G5FA3"] = new ChildReference { CurrentSubtreeTo = 500m, ... }
};
node.DynamicFields.SetMapsWithPrefix("c_", newChildren);

// Remove multiple children
var childrenToRemove = new[] { "c_01ARZ3NDEKTSV4RRFFQ69G5FA4", "c_01ARZ3NDEKTSV4RRFFQ69G5FA5" };
node.DynamicFields.RemoveMany(childrenToRemove);

// Remove all children
node.DynamicFields.RemoveByPrefix("c_");
```

### Transaction String Fields

For transaction fields that use encoded strings (not Maps), use existing methods:

```csharp
// Get all transaction field names
var txnFieldNames = node.DynamicFields.GetFieldNamesByPrefix("t_");

// Get all transactions as raw strings
var txnFields = node.DynamicFields.GetByPrefixWithStrippedKeys("t_");
foreach (var (txnId, av) in txnFields)
{
    var encodedValue = av.S;
    var (state, amount) = TransactionValueEncoder.Decode(encodedValue);
    // Process transaction...
}

// Set a transaction
node.DynamicFields.SetString("t_" + txnId, TransactionValueEncoder.Encode(state, amount));

// Bulk set transactions
var txnValues = new Dictionary<string, AttributeValue>
{
    ["TXN001"] = new AttributeValue { S = "C1000.0000" },
    ["TXN002"] = new AttributeValue { S = "D500.0000" }
};
node.DynamicFields.SetManyWithPrefix("t_", txnValues);
```

## Testing Strategy

### Unit Tests

1. **Prefix Operations**
   - GetFieldNamesByPrefix with matching/non-matching prefixes
   - GetByPrefix and GetByPrefixWithStrippedKeys
   - RemoveByPrefix with change tracking verification

2. **Typed Map Operations**
   - GetMap/TryGetMap with valid Map, missing field, wrong type
   - SetMap with entity, null, change tracking
   - GetMapsByPrefix with mixed Map/non-Map fields

3. **Bulk Operations**
   - SetMany/SetManyWithPrefix with change tracking
   - SetMapsWithPrefix with entity serialization
   - RemoveMany with partial matches

4. **Integration with Update Expression Translator**
   - Verify SET clauses generated for bulk additions
   - Verify REMOVE clauses generated for bulk removals
   - Verify mixed SET/REMOVE in single update

### Integration Tests

1. Round-trip test: Create node with children → Save → Load → Verify children
2. Incremental update test: Load → Add/Remove children → Update → Verify
3. Transaction test: Multiple operations in TransactWrite

## Migration Notes

This is a purely additive change. Existing code using `DynamicFieldCollection` will continue to work unchanged. The new methods provide additional convenience for prefix-based patterns.
