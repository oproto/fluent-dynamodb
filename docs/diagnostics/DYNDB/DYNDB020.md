# DYNDB020: Circular reference detected

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB020` |
| Severity | Error |

## Message

`Entity '{0}' has circular references that cannot be serialized to DynamoDB`

## Description

Entities with circular references cannot be properly serialized to DynamoDB format. DynamoDB stores items as flat attribute maps and nested maps, which cannot represent circular object graphs.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbEntity]
public partial class TreeNode
{
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbMap]
    [DynamoDbAttribute("parent")]
    public TreeNode Parent { get; set; } = null!;
}

[DynamoDbTable("Trees")]
public partial class Tree
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbMap]
    [DynamoDbAttribute("root")]
    public TreeNode Root { get; set; } = new();
}
```

## Fix

The corrected version:

```csharp
[DynamoDbEntity]
public partial class TreeNode
{
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("parentId")]
    public string ParentId { get; set; } = string.Empty;
}

[DynamoDbTable("Trees")]
public partial class Tree
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbMap]
    [DynamoDbAttribute("root")]
    public TreeNode Root { get; set; } = new();
}
```
