# DYNDB1002: Invalid extension method

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB1002` |
| Severity | Error |

## Message

`Extension method '{0}' marked with [GenerateWrapper] does not extend a valid interface`

## Description

Extension methods marked with [GenerateWrapper] must extend an interface that is implemented by the builder class. The first parameter must be an interface type. The source generator uses the interface to determine which builder classes should receive the generated wrapper method.

## Example

The following code triggers this diagnostic:

```csharp
public static class MyExtensions
{
    [GenerateWrapper]
    public static string DoSomething(this string value)
    {
        return value.ToUpper();
    }
}
```

## Fix

The corrected version:

```csharp
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
