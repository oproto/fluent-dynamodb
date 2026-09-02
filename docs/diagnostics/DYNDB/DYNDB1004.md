# DYNDB1004: Interface not implemented

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB1004` |
| Severity | Warning |

## Message

`Builder '{0}' does not implement interface '{1}' required by extension methods marked with [GenerateWrapper]`

## Description

The builder class must implement all interfaces that are extended by methods marked with [GenerateWrapper]. Add the interface implementation to the builder class or remove the [GenerateWrapper] attribute from methods extending this interface.

## Example

The following code triggers this diagnostic:

```csharp
public interface IMyCustomBehavior
{
    void DoSomething();
}

public static class MyExtensions
{
    [GenerateWrapper]
    public static IMyCustomBehavior WithCustom(
        this IMyCustomBehavior builder)
    {
        // PutItemRequestBuilder does not implement IMyCustomBehavior
        return builder;
    }
}
```

## Fix

The corrected version:

```csharp
// Use an interface that the builder actually implements
public static class MyExtensions
{
    [GenerateWrapper]
    public static IWithConditionExpression<T> WithCustomCondition<T>(
        this IWithConditionExpression<T> builder)
    {
        return builder;
    }
}
```
