# FDDB111: Unsupported old version error

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB111` |
| Severity | Error |

## Message

`Declared schema version {0} is no longer supported. Minimum supported version is {1}. See {2} for migration guidance.`

## Description

The declared schema version is older than the minimum version this generator supports. The source generator cannot emit code for a version it no longer supports. When this error is emitted, all code generation is halted — no entity sources will be produced regardless of how many entities are declared in the assembly.

You must either update your schema version declaration to match a supported version (and adopt the corresponding code shape changes), or pin to an older version of the Oproto.FluentDynamoDb NuGet package that still supports your declared schema version.

## Example

The following code triggers this diagnostic when the minimum supported version is 2.0:

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Schema version 1.0 is no longer supported by this package version
[assembly: FluentDynamoDbSchemaVersion(1, 0)]

[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}
```

## Fix

Update the schema version to the minimum supported version (or higher) and apply any necessary migration steps:

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Updated to a supported schema version
[assembly: FluentDynamoDbSchemaVersion(2, 0)]
```

Alternatively, pin to an older package version that still supports schema version 1.0. Refer to the migration guide URL provided in the diagnostic message for step-by-step instructions.
