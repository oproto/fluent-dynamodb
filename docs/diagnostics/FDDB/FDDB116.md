# FDDB116: Duplicate attribute error

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB116` |
| Severity | Error |

## Message

`Multiple [FluentDynamoDbSchemaVersion] attributes detected. Remove duplicate declarations.`

## Description

Multiple `[FluentDynamoDbSchemaVersion]` attributes were found on the assembly. Since the attribute is defined with `AllowMultiple = false`, this can only happen through IL manipulation or other non-standard compilation techniques. When this error is emitted, all code generation is halted — no entity sources will be produced.

## Example

The following scenario triggers this diagnostic (requires IL manipulation since C# prevents this at compile time):

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Only one of these should exist — duplicates detected via IL manipulation
[assembly: FluentDynamoDbSchemaVersion(1, 0)]
[assembly: FluentDynamoDbSchemaVersion(2, 0)]  // Duplicate — would be caught by C# normally

[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}
```

## Fix

Remove duplicate declarations and keep only a single `[FluentDynamoDbSchemaVersion]` attribute:

```csharp
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]
```
