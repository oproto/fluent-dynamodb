# DYNDB018: Invalid key format syntax

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB018` |
| Severity | Error |

## Message

`Key format '{0}' on property '{1}' contains invalid syntax or placeholders`

## Description

Key formats must use valid placeholder syntax like {0}, {1}, etc. and cannot contain reserved characters. The source generator parses key formats to generate key construction methods, and invalid syntax prevents correct code generation.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", Separator = "#", Format = "{invalid}")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
    public int Month { get; set; }
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed("Year", "Month", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public int Year { get; set; }

    [Extracted("Pk", 1)]
    public int Month { get; set; }
}
```
