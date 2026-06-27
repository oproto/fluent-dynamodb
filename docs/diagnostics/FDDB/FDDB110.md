# FDDB110: Missing schema version attribute

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB110` |
| Severity | Warning |

## Message

`Assembly does not declare [FluentDynamoDbSchemaVersion]. Defaulting to schema version 1.0. Add [assembly: FluentDynamoDbSchemaVersion(1, 0)] to suppress this warning.`

## Description

Every assembly using the FluentDynamoDb source generator should declare a schema version to explicitly state which generated code shape it targets. When no `[FluentDynamoDbSchemaVersion]` attribute is present, the generator defaults to schema version 1.0 and emits this warning once per compilation.

The schema version attribute creates an explicit contract between your code and the source generator, ensuring you're aware of which code shape is being generated and preventing silent breaking changes on package upgrade.

## Example

The following code triggers this diagnostic:

```csharp
// No assembly-level schema version attribute declared anywhere in the project

[DynamoDbTable("Orders")]
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

Add the `[assembly: FluentDynamoDbSchemaVersion]` attribute to any file in your project (commonly `AssemblyInfo.cs` or `GlobalUsings.cs`):

```csharp
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]
```
