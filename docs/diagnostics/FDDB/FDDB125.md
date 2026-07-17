# FDDB125: Computed key property has redundant Prefix

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB125` |
| Severity | Error |

## Message

`Property '{0}' is a computed key with Prefix = "{1}" configured on its key attribute. Prefixes are not applied to computed keys — remove the Prefix and embed it in the [Computed] Format if the prefix should appear in the stored value`

## Description

A computed key derives its value entirely from `[Computed]` configuration. The `Prefix` on `[PartitionKey]` or `[SortKey]` is silently ignored at runtime because the source generator's key prefix application logic excludes computed keys. This creates a confusing situation where a developer declares a Prefix expecting it to appear in the stored DynamoDB value, but it never does.

Remove the Prefix to avoid confusion, or use `Format = "PREFIX#{0}"` on `[Computed]` if the prefix should appear in the stored value.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey(Prefix = "EVT")]       // ❌ Prefix is ignored on computed keys
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
    public int Month { get; set; }
}
```

## Fix

Remove the `Prefix` from the key attribute. If the prefix should appear in the stored value, embed it in the `[Computed]` Format instead:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]                        // ✅ No prefix on computed key
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", Format = "EVT#{0}#{1}")]  // Prefix embedded in Format
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
    public int Month { get; set; }
}
```

Or if no prefix is desired:

```csharp
[DynamoDbTable("events")]
public partial class Event
{
    [PartitionKey]                        // ✅ No prefix
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
    public int Month { get; set; }
}
```
