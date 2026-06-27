# DYNDB1001: Invalid GenerateWrapper usage

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB1001` |
| Severity | Error |

## Message

`Method '{0}' is marked with [GenerateWrapper] but is not an extension method`

## Description

The [GenerateWrapper] attribute can only be applied to extension methods. Extension methods must be static and have 'this' as the first parameter modifier. The source generator uses this attribute to create fluent wrapper methods on builder classes.

## Example

The following code triggers this diagnostic:

```csharp
public static class MyExtensions
{
    [GenerateWrapper]
    public static void AddLogging(IWithAttributes<PutItemRequestBuilder> builder)
    {
        // Not an extension method - missing 'this' keyword
    }
}
```

## Fix

The corrected version:

```csharp
public static class MyExtensions
{
    [GenerateWrapper]
    public static IWithAttributes<PutItemRequestBuilder> AddLogging(
        this IWithAttributes<PutItemRequestBuilder> builder)
    {
        // Proper extension method with 'this' keyword
        return builder;
    }
}
```
