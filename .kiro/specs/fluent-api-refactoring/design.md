# Design Document

## Overview

This design refactors the Oproto.FluentDynamoDb library to eliminate code duplication and improve developer experience through two major architectural changes:

1. **Extension Method Architecture**: Move shared functionality from interface method implementations to extension methods, reducing maintenance burden from O(n*m) to O(m) where n is the number of builders and m is the number of shared methods.

2. **Enhanced Parameter Handling**: Introduce string.Format-style parameter syntax with optional formatting support, eliminating the ceremony of manual parameter naming and ToString() calls.

The refactoring maintains full backward compatibility while providing a more maintainable codebase and improved developer experience.

## Architecture

### Current Architecture Problems

The current architecture requires each request builder to implement interface methods that simply forward calls to internal helper classes:

```csharp
// Current: Each builder must implement this boilerplate
public class QueryRequestBuilder : IWithAttributeValues<QueryRequestBuilder>
{
    private readonly AttributeValueInternal _attrV = new();
    
    // Repeated across 15+ builders
    public QueryRequestBuilder WithValue(string name, string value) 
    {
        _attrV.WithValue(name, value);
        return this;
    }
    // ... 6 more WithValue overloads repeated in every builder
}
```

### New Extension-Based Architecture

The new architecture moves shared functionality to extension methods:

```csharp
// New: Minimal interface implementation
public class QueryRequestBuilder : IWithAttributeValues<QueryRequestBuilder>
{
    private readonly AttributeValueInternal _attrV = new();
    
    // Only these two methods needed per interface
    public AttributeValueInternal GetAttributeValueHelper() => _attrV;
    public QueryRequestBuilder Self => this;
}

// Shared functionality implemented once
public static class WithAttributeValuesExtensions 
{
    public static T WithValue<T>(this IWithAttributeValues<T> builder, string name, string value) 
        where T : IWithAttributeValues<T>
    {
        builder.GetAttributeValueHelper().WithValue(name, value);
        return builder.Self;
    }
    // All other overloads implemented once here
}
```

## Components and Interfaces

### Modified Interface Contracts

Each interface will be modified to expose minimal required functionality for extension methods:

#### IWithAttributeValues<T>
```csharp
public interface IWithAttributeValues<out TBuilder>
{
    AttributeValueInternal GetAttributeValueHelper();
    TBuilder Self { get; }
}
```

#### IWithAttributeNames<T>
```csharp
public interface IWithAttributeNames<out TBuilder>
{
    AttributeNameInternal GetAttributeNameHelper();
    TBuilder Self { get; }
}
```

#### IWithConditionExpression<T>
```csharp
public interface IWithConditionExpression<out TBuilder>
{
    // Enhanced to support both old and new parameter styles
    TBuilder Self { get; }
    // Builders will need to expose their condition setting mechanism
}
```

#### IWithKey<T>
```csharp
public interface IWithKey<out TBuilder>
{
    // Will need access to key setting mechanism
    TBuilder Self { get; }
}
```

#### IWithUpdateExpression<T> (New)
```csharp
public interface IWithUpdateExpression<out TBuilder>
{
    AttributeValueInternal GetAttributeValueHelper(); // For parameter generation
    TBuilder SetUpdateExpression(string expression); // For setting the processed expression
    TBuilder Self { get; }
}
```

### Extension Method Classes

#### WithAttributeValuesExtensions
```csharp
public static class WithAttributeValuesExtensions 
{
    // Existing API (moved from interfaces, signatures unchanged)
    public static T WithValue<T>(this IWithAttributeValues<T> builder, string attributeName, string? attributeValue, bool conditionalUse = true)
    public static T WithValue<T>(this IWithAttributeValues<T> builder, string attributeName, bool? attributeValue, bool conditionalUse = true)
    public static T WithValue<T>(this IWithAttributeValues<T> builder, string attributeName, decimal? attributeValue, bool conditionalUse = true)
    public static T WithValue<T>(this IWithAttributeValues<T> builder, string attributeName, Dictionary<string, string> attributeValue, bool conditionalUse = true)
    public static T WithValue<T>(this IWithAttributeValues<T> builder, string attributeName, Dictionary<string, AttributeValue> attributeValue, bool conditionalUse = true)
    
    public static T WithValues<T>(this IWithAttributeValues<T> builder, Dictionary<string, AttributeValue> attributeValues)
    public static T WithValues<T>(this IWithAttributeValues<T> builder, Action<Dictionary<string, AttributeValue>> attributeValueFunc)
    
    // FUTURE: Additional type overloads (not in initial implementation)
    // public static T WithValue<T>(this IWithAttributeValues<T> builder, string attributeName, DateTime? attributeValue, bool conditionalUse = true)
    // public static T WithValue<T>(this IWithAttributeValues<T> builder, string attributeName, int? attributeValue, bool conditionalUse = true)
}
```

#### WithAttributeNamesExtensions
```csharp
public static class WithAttributeNamesExtensions 
{
    // Existing API (moved from interfaces, signatures unchanged)
    public static T WithAttribute<T>(this IWithAttributeNames<T> builder, string parameterName, string attributeName)
    public static T WithAttributes<T>(this IWithAttributeNames<T> builder, Dictionary<string, string> attributeNames)
    public static T WithAttributes<T>(this IWithAttributeNames<T> builder, Action<Dictionary<string, string>> attributeNameFunc)
}
```

#### WithConditionExpressionExtensions
```csharp
public static class WithConditionExpressionExtensions 
{
    // Existing API (moved from interface)
    public static T Where<T>(this IWithConditionExpression<T> builder, string conditionExpression)
    
    // NEW: Format string support
    public static T Where<T>(this IWithConditionExpression<T> builder, string format, params object[] args)
    
    // FUTURE: Convenience methods (not in initial implementation)
    // public static T WhereExists<T>(this IWithConditionExpression<T> builder, string attributeName)
    // public static T WhereEquals<T>(this IWithConditionExpression<T> builder, string attributeName, object value)
}
```

#### WithKeyExtensions
```csharp
public static class WithKeyExtensions 
{
    // Existing API (moved from interfaces, signatures unchanged)
    public static T WithKey<T>(this IWithKey<T> builder, string primaryKeyName, AttributeValue primaryKeyValue, string? sortKeyName = null, AttributeValue? sortKeyValue = null)
    public static T WithKey<T>(this IWithKey<T> builder, string keyName, string keyValue)
    public static T WithKey<T>(this IWithKey<T> builder, string primaryKeyName, string primaryKeyValue, string sortKeyName, string sortKeyValue)
}
```

#### WithUpdateExpressionExtensions (New)
```csharp
public static class WithUpdateExpressionExtensions 
{
    // Existing API (moved from builders)
    public static T Set<T>(this IWithUpdateExpression<T> builder, string updateExpression)
    
    // NEW: Format string support for update expressions
    public static T Set<T>(this IWithUpdateExpression<T> builder, string format, params object[] args)
    
    // FUTURE: Convenience methods (not in initial implementation)
    // public static T SetValue<T>(this IWithUpdateExpression<T> builder, string attributeName, object value)
    // public static T AddValue<T>(this IWithUpdateExpression<T> builder, string attributeName, object value)
    // public static T RemoveAttribute<T>(this IWithUpdateExpression<T> builder, string attributeName)
}
```

### Enhanced Parameter Handling

#### New Where Method Overload
We'll add a new Where method overload that accepts format strings:

#### New Set Method Overload
We'll add a new Set method overload that accepts format strings for update expressions:

```csharp
public static class WithConditionExpressionExtensions 
{
    // Existing method (unchanged)
    public static T Where<T>(this IWithConditionExpression<T> builder, string conditionExpression)
        where T : IWithConditionExpression<T>
    {
        // Calls existing builder.SetConditionExpression(conditionExpression)
        return builder.Self;
    }
    
    // NEW: Format string overload
    public static T Where<T>(this IWithConditionExpression<T> builder, string format, params object[] args)
        where T : IWithConditionExpression<T>
    {
        var (expression, parameters) = ProcessFormatString(format, args, builder.GetAttributeValueHelper());
        // Set the processed expression and parameters on the builder
        return builder.Self;
    }
}
```

#### Format String Processing Details
```csharp
// Input:  .Where("pk = {0} AND created > {1:o}", "USER#123", DateTime.Now)
// Step 1: Parse format string, find {0} and {1:o}
// Step 2: Generate parameter names :p0, :p1 using builder's parameter generator
// Step 3: Convert values: "USER#123" -> AttributeValue{S="USER#123"}, DateTime -> AttributeValue{S="2024-01-01T12:00:00.000Z"}
// Step 4: Replace placeholders: "pk = :p0 AND created > :p1"
// Step 5: Add parameters to builder's AttributeValueInternal
// Result: Expression set to "pk = :p0 AND created > :p1", parameters added automatically
```

#### Interface Requirements
For this to work, IWithConditionExpression needs to expose:
```csharp
public interface IWithConditionExpression<out TBuilder>
{
    TBuilder Self { get; }
    AttributeValueInternal GetAttributeValueHelper(); // For parameter generation
    TBuilder SetConditionExpression(string expression); // For setting the processed expression
}
```

#### Update Expression Format String Processing
```csharp
// Input:  .Set("SET #name = {0}, updated_time = {1:o}", "John Doe", DateTime.Now)
// Step 1: Parse format string, find {0} and {1:o}
// Step 2: Generate parameter names :p0, :p1 using builder's parameter generator
// Step 3: Convert values: "John Doe" -> AttributeValue{S="John Doe"}, DateTime -> AttributeValue{S="2024-01-01T12:00:00.000Z"}
// Step 4: Replace placeholders: "SET #name = :p0, updated_time = :p1"
// Step 5: Add parameters to builder's AttributeValueInternal
// Result: Expression set to "SET #name = :p0, updated_time = :p1", parameters added automatically
```

#### Type Conversion and Formatting
Enhanced type conversion supporting:
- Standard .NET format strings ({0:o}, {0:F2}, etc.)
- Automatic enum to string conversion
- DateTime formatting with ISO default
- Null handling with conditional parameter generation

## Data Models

### Internal Helper Classes

#### AttributeValueInternal (Enhanced)
```csharp
internal class AttributeValueInternal
{
    public Dictionary<string, AttributeValue> AttributeValues { get; init; } = new();
    private readonly ParameterGenerator _parameterGenerator = new();
    
    // Existing methods remain unchanged for backward compatibility
    public void WithValue(string name, string value, bool conditionalUse = true) { }
    // ... other existing overloads
    
    // New method for format string support - handles the formatting internally
    public string AddFormattedValue(object value, string? format = null)
    {
        var paramName = _parameterGenerator.GenerateParameterName();
        var formattedValue = FormatValue(value, format);
        AttributeValues.Add(paramName, formattedValue);
        return paramName;
    }
    
    private static AttributeValue FormatValue(object value, string? format)
    {
        // Handle formatting and conversion to AttributeValue
    }
}
```

#### AttributeNameInternal (Unchanged)
Remains as-is since attribute name handling doesn't need enhancement.

#### ParameterGenerator (New)
```csharp
internal class ParameterGenerator
{
    private int _counter = 0;
    
    public string GenerateParameterName() => $":p{_counter++}";
    public void Reset() => _counter = 0; // For testing
}
```

Each builder instance will have its own ParameterGenerator to ensure predictable, debuggable parameter names within a single expression.

### Request Builder Modifications

Each request builder will be modified to:
1. Remove all interface method implementations
2. Add GetXxxHelper() methods for each interface
3. Add Self property returning the builder instance
4. Maintain all existing builder-specific methods unchanged

#### Update Expression Builders
UpdateItemRequestBuilder and TransactUpdateBuilder will additionally implement IWithUpdateExpression:
```csharp
public class UpdateItemRequestBuilder : 
    IWithKey<UpdateItemRequestBuilder>, 
    IWithConditionExpression<UpdateItemRequestBuilder>, 
    IWithAttributeNames<UpdateItemRequestBuilder>, 
    IWithAttributeValues<UpdateItemRequestBuilder>,
    IWithUpdateExpression<UpdateItemRequestBuilder>  // NEW
{
    // Existing helper methods...
    
    // NEW: Update expression support
    public UpdateItemRequestBuilder SetUpdateExpression(string expression)
    {
        _req.UpdateExpression = expression;
        return this;
    }
}
```

## Error Handling

### Format String Validation
- Invalid format specifiers will throw ArgumentException with clear messages
- Mismatched parameter counts will be detected and reported
- Null format strings will be handled gracefully

### Type Conversion Errors
- Unsupported type conversions will provide helpful error messages
- Format string incompatibilities will be caught early
- AOT-incompatible operations will be avoided

### Backward Compatibility
- All existing method signatures preserved
- Existing behavior maintained exactly
- Extension method conflicts resolved through explicit interface implementation

## Testing Strategy

### Unit Tests for Extension Methods
- Test all WithValue overloads with various data types
- Verify format string parsing with edge cases
- Validate parameter generation and naming
- Test conditional parameter handling

### Integration Tests for Builders
- Verify all builders work with new extension methods
- Test mixed usage of old and new parameter styles
- Validate complex expressions with multiple parameters
- Test error conditions and edge cases

### Backward Compatibility Tests
- Run existing test suite without modifications
- Verify identical behavior for all existing functionality
- Test upgrade scenarios with mixed codebases

### AOT Compatibility Tests
- Verify library works in Native AOT applications
- Test all format string operations are AOT-safe
- Validate no reflection or dynamic code generation

## Migration Strategy

### Phase 1: Interface Modification
1. Modify interfaces to expose helper access methods
2. Update all builders to implement new interface contracts
3. Maintain existing method implementations for compatibility

### Phase 2: Extension Method Implementation
1. Create extension method classes with all existing functionality
2. Add enhanced parameter handling capabilities
3. Comprehensive testing of new functionality

### Phase 3: Enhanced Features
1. Add format string support to Where methods
2. Implement convenience methods for common patterns
3. Add new type conversion overloads

### Phase 4: Documentation and Examples
1. Update XML documentation with new patterns
2. Create migration guide for consumers
3. Add examples showing new capabilities

## Performance Considerations

### Extension Method Overhead
- Extension methods have minimal runtime overhead
- Generic constraints ensure type safety without boxing
- Method inlining should eliminate most call overhead

### Parameter Generation
- Per-builder parameter name generation for predictable debugging
- Minimal string allocation for parameter names  
- Format string parsing optimized for common cases

### Memory Usage
- No additional memory overhead per builder instance
- Shared extension method implementations reduce code size
- Internal helper classes remain lightweight

## Security Considerations

### Parameter Injection Prevention
- Format string parsing prevents SQL-injection-style attacks
- All values properly converted to AttributeValue types
- Parameter names generated safely without user input

### AOT Safety
- No reflection or dynamic code generation
- All type conversions statically analyzable
- Format string operations use only safe string manipulation