# FDDB131: Bare RelatedEntity trivial sort key pattern

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB131` |
| Severity | Error |

## Message

`[RelatedEntity] on property '{0}' in entity '{1}' cannot infer a matching pattern because child entity '{2}' has a bare sort key with no distinguishing structure (NormalizedKeyFormat is '{{0}}'). Provide an explicit pattern string.`

## Description

A bare `[RelatedEntity]` attribute infers the sort key matching pattern from the child entity's `DerivedDiscriminatorPattern`. This pattern is derived from the child entity's sort key `NormalizedKeyFormat` by replacing `{N}` placeholders with `*` wildcards.

When the child entity's sort key has no prefix, separator, or computed structure (i.e., `NormalizedKeyFormat` is just `{0}`), the derived pattern would be simply `*`, which matches everything and provides no discriminating value. In this case, the generator cannot safely infer a meaningful pattern and requires an explicit one.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("orders")]
public partial class OrderLine
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]  // Bare sort key — NormalizedKeyFormat is "{0}"
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [RelatedEntity]  // ❌ FDDB131: OrderLine has bare sort key
    public List<OrderLine> Lines { get; set; } = new();
}
```

## Fix

Either add structure to the child entity's sort key (prefix or computed format), or provide an explicit pattern string:

```csharp
// Option 1: Add prefix to child sort key
[SortKey(Prefix = "LINE")]
[DynamoDbAttribute("sk")]
public string Sk { get; set; } = string.Empty;

// Option 2: Use explicit pattern string on the parent
[RelatedEntity("LINE#*")]
public List<OrderLine> Lines { get; set; } = new();
```
