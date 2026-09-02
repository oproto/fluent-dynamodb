# FDDB114: Invalid major version error

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB114` |
| Severity | Error |

## Message

`FluentDynamoDbSchemaVersion major version must be at least 1, but was {0}.`

## Description

The major component of the schema version must be at least 1. A major version of 0 or negative is not a valid schema version. When this error is emitted, all code generation is halted — no entity sources will be produced.

This typically occurs when the attribute is constructed with invalid literal values, or when IL manipulation bypasses the attribute constructor's runtime validation.

## Example

The following code triggers this diagnostic:

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Major version 0 is invalid — must be at least 1
[assembly: FluentDynamoDbSchemaVersion(0, 0)]

[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}
```

## Fix

Provide a valid major version (1 or higher):

```csharp
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]
```
