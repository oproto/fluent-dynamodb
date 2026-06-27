# FDDB115: Invalid minor version error

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB115` |
| Severity | Error |

## Message

`FluentDynamoDbSchemaVersion minor version must be at least 0, but was {0}.`

## Description

The minor component of the schema version must be at least 0. A negative minor version is not a valid schema version. When this error is emitted, all code generation is halted — no entity sources will be produced.

This typically occurs when IL manipulation bypasses the attribute constructor's runtime validation, since the C# compiler would normally catch negative literal values at the constructor level.

## Example

The following code triggers this diagnostic (via IL manipulation, since the constructor would throw at runtime):

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Minor version -1 is invalid — must be at least 0
[assembly: FluentDynamoDbSchemaVersion(1, -1)]

[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}
```

## Fix

Provide a valid minor version (0 or higher):

```csharp
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]
```
