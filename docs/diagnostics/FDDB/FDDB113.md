# FDDB113: Older-but-supported version info

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB113` |
| Severity | Info |

## Message

`Schema version {0} is supported but not current. Consider upgrading to {1} for the latest generated code improvements. See {2}.`

## Description

The declared schema version is still supported but a newer version is available with improved generated code. Code generation proceeds normally using the declared version's code shape. This is purely informational — no action is required, and your code will continue to compile and function correctly.

When you're ready to adopt the latest generated code improvements, you can bump your declared schema version and apply any necessary code changes as described in the upgrade guide.

## Example

The following code triggers this diagnostic when the current version is 2.0 and minimum supported is 1.0:

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Version 1.0 is still supported but 2.0 is current
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

No fix is required — this is informational only. When ready to upgrade:

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Upgraded to the current schema version
[assembly: FluentDynamoDbSchemaVersion(2, 0)]
```

Refer to the upgrade guide URL provided in the diagnostic message for details on what changed between versions and any code adjustments needed.
