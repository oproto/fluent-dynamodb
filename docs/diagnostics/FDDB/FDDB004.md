# FDDB004: Empty entity property name

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB004` |
| Severity | Error |

## Message

`Entity '{0}' has [GenerateEntityProperty] with empty Name; provide a valid name or omit the Name property to use default naming`

## Description

The Name property in [GenerateEntityProperty] cannot be empty. Either provide a valid custom name or omit the Name property to use the default pluralized entity name.

When the Name property is specified, it must be a valid C# identifier that will be used as the property name on the generated table class for accessing entity operations.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("orders")]
[GenerateEntityProperty(Name = "")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("orders")]
[GenerateEntityProperty(Name = "Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```
