# FDDB112: Unrecognized future version error

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB112` |
| Severity | Error |

## Message

`Declared schema version {0} is not recognized. Maximum supported version is {1}. Update the Oproto.FluentDynamoDb package to a version that supports schema {0}.`

## Description

The declared schema version is newer than the maximum version this generator supports. The source generator cannot emit code for a future version whose code shape is not yet defined. When this error is emitted, all code generation is halted — no entity sources will be produced regardless of how many entities are declared in the assembly.

This typically happens when you've updated the schema version attribute in anticipation of a newer package version, or when you've downgraded the NuGet package without adjusting the schema version declaration.

## Example

The following code triggers this diagnostic when the current (maximum) supported version is 1.0:

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Schema version 2.0 is not yet supported by this package version
[assembly: FluentDynamoDbSchemaVersion(2, 0)]

[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}
```

## Fix

Either update the Oproto.FluentDynamoDb NuGet package to a version that supports the declared schema version, or lower your declared version to match what's supported:

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Use the maximum supported schema version
[assembly: FluentDynamoDbSchemaVersion(1, 0)]
```
