# DYNDB1003: Interface not found

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB1003` |
| Severity | Warning |

## Message

`Interface '{0}' required by extension methods could not be found for builder '{1}'`

## Description

The interface extended by marked extension methods could not be found in the compilation. This may indicate a missing reference or incorrect interface name. Verify that the interface type exists and is accessible from the current project.

## Example

The following code triggers this diagnostic:

```csharp
public static class MyExtensions
{
    [GenerateWrapper]
    public static ICustomInterface WithCustomBehavior(
        this ICustomInterface builder)
    {
        // ICustomInterface is not found in the compilation
        return builder;
    }
}
```

## Fix

The corrected version:

```csharp
// Ensure the interface exists and is referenced
public static class MyExtensions
{
    [GenerateWrapper]
    public static IWithConditionExpression<T> WithAuditCondition<T>(
        this IWithConditionExpression<T> builder)
    {
        return builder;
    }
}
```
