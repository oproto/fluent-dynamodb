# FDDB121: Prefix not applicable to constant key

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB121` |
| Severity | Error |

## Message

`Property '{0}' is a constant key but has Prefix configured — prefix is meaningless on a constant value`

## Description

A key property detected as a constant key (via expression-body or read-only auto-property syntax) cannot have a `Prefix` configured on `[PartitionKey]` or `[SortKey]`. The constant value already contains the exact key value that will be stored in DynamoDB — there is no variable portion to prepend a prefix to.

When this diagnostic fires, code generation is halted for the affected entity.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("customers")]
public partial class Customer
{
    [PartitionKey(Prefix = "USER")]
    [DynamoDbAttribute("pk")]
    public string Pk => "CONSTANT_VALUE";  // Constant + Prefix = conflict
}
```

## Fix

Remove the `Prefix` — the constant value already IS the full key value:

```csharp
[DynamoDbTable("customers")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk => "USER#CONSTANT_VALUE";  // Include prefix in the constant itself
}
```

Or, if the key needs a prefix with a variable part, don't use a constant key:

```csharp
[DynamoDbTable("customers")]
public partial class Customer
{
    [PartitionKey(Prefix = "USER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;  // Variable key with prefix
}
```
