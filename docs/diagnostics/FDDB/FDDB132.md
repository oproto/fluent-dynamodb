# FDDB132: Bare RelatedEntity table mismatch

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB132` |
| Severity | Error |

## Message

`[RelatedEntity] on property '{0}' in entity '{1}' (table '{2}') references child entity '{3}' which is on table '{4}'. Related entities must share the same DynamoDB table.`

## Description

A bare `[RelatedEntity]` attribute resolves the child entity within the same table group. Composite entity assembly (`ToCompositeEntityAsync`) works by querying a single DynamoDB table and distributing items across parent and child entities based on sort key pattern matching. If the child entity is declared on a different table, the relationship cannot be resolved because items from different tables cannot be retrieved in a single query.

This diagnostic is emitted when the inferred child entity type is found but belongs to a different `[DynamoDbTable]` than the parent entity.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [RelatedEntity]  // ❌ FDDB132: Payment is on "payments" table
    public List<Payment> Payments { get; set; } = new();
}

[DynamoDbTable("payments")]  // Different table!
public partial class Payment
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "PAY")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```

## Fix

Either move the child entity to the same table, or use an explicit pattern with a different querying strategy:

```csharp
// Option 1: Move child entity to the same table
[DynamoDbTable("orders")]  // Same table as parent
public partial class Payment
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "PAY")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

// Option 2: Use explicit pattern (if querying separately)
[RelatedEntity("PAY#*", EntityType = typeof(Payment))]
public List<Payment> Payments { get; set; } = new();
```
