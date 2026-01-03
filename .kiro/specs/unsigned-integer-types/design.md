# Design Document: Unsigned Integer Types Support

## Overview

This design adds support for unsigned integer types (`ulong`, `uint`, `ushort`, `byte`, `sbyte`) and `short` to the FluentDynamoDb source generator. These types will be stored as DynamoDB Number (N) attributes, consistent with how other numeric types (`int`, `long`, `double`, `float`, `decimal`) are handled.

## Architecture

The implementation requires changes to two main components:

1. **EntityAnalyzer** (`Analysis/EntityAnalyzer.cs`): Update the `IsSupportedPropertyType` method to recognize the new numeric types
2. **MapperGenerator** (`Generators/MapperGenerator.cs`): Update the type conversion methods to handle serialization and deserialization of the new types

No new classes or interfaces are needed. The existing architecture already supports numeric types via the DynamoDB Number (N) attribute type.

## Components and Interfaces

### EntityAnalyzer Changes

The `IsSupportedPropertyType` method needs to be updated to include the new types in its supported types list:

```csharp
private bool IsSupportedPropertyType(string typeName)
{
    var supportedTypes = new[]
    {
        // Existing types...
        "string", "int", "long", "double", "float", "decimal", "bool", 
        "DateTime", "DateTimeOffset", "Guid", "byte[]",
        // ... fully qualified versions ...
        
        // NEW: Unsigned integer types
        "ulong", "uint", "ushort", "byte", "sbyte", "short",
        "System.UInt64", "System.UInt32", "System.UInt16", 
        "System.Byte", "System.SByte", "System.Int16"
    };
    // ... rest of method
}
```

### MapperGenerator Changes

#### GetToAttributeValueExpression

Add cases for the new types in the switch expression:

```csharp
private static string GetToAttributeValueExpression(PropertyModel property, string valueExpression)
{
    var baseType = GetBaseType(property.PropertyType);
    
    return baseType switch
    {
        // Existing cases...
        "int" or "System.Int32" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        "long" or "System.Int64" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        
        // NEW: Unsigned integer types
        "ulong" or "System.UInt64" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        "uint" or "System.UInt32" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        "ushort" or "System.UInt16" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        "byte" or "System.Byte" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        "sbyte" or "System.SByte" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        "short" or "System.Int16" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        
        // ... rest of cases
    };
}
```

#### GetFromAttributeValueExpression

Add cases for parsing the new types:

```csharp
private static string GetFromAttributeValueExpression(PropertyModel property, string valueExpression)
{
    var baseType = GetBaseType(property.PropertyType);
    
    return baseType switch
    {
        // Existing cases...
        "int" or "System.Int32" => $"int.Parse({valueExpression}.N)",
        "long" or "System.Int64" => $"long.Parse({valueExpression}.N)",
        
        // NEW: Unsigned integer types
        "ulong" or "System.UInt64" => $"ulong.Parse({valueExpression}.N)",
        "uint" or "System.UInt32" => $"uint.Parse({valueExpression}.N)",
        "ushort" or "System.UInt16" => $"ushort.Parse({valueExpression}.N)",
        "byte" or "System.Byte" => $"byte.Parse({valueExpression}.N)",
        "sbyte" or "System.SByte" => $"sbyte.Parse({valueExpression}.N)",
        "short" or "System.Int16" => $"short.Parse({valueExpression}.N)",
        
        // ... rest of cases
    };
}
```

#### GetFromAttributeValueExpressionForCollectionElement

Add cases for collection element conversion:

```csharp
private static string GetFromAttributeValueExpressionForCollectionElement(string elementType)
{
    var baseType = GetBaseType(elementType);
    
    return baseType switch
    {
        // Existing cases...
        "int" or "System.Int32" => "x => int.Parse(x.N)",
        "long" or "System.Int64" => "x => long.Parse(x.N)",
        
        // NEW: Unsigned integer types
        "ulong" or "System.UInt64" => "x => ulong.Parse(x.N)",
        "uint" or "System.UInt32" => "x => uint.Parse(x.N)",
        "ushort" or "System.UInt16" => "x => ushort.Parse(x.N)",
        "byte" or "System.Byte" => "x => byte.Parse(x.N)",
        "sbyte" or "System.SByte" => "x => sbyte.Parse(x.N)",
        "short" or "System.Int16" => "x => short.Parse(x.N)",
        
        // ... rest of cases
    };
}
```

#### GetToAttributeValueExpressionForCollectionElement

Add cases for collection element serialization:

```csharp
private static string GetToAttributeValueExpressionForCollectionElement(string elementType, string valueExpression)
{
    var baseType = GetBaseType(elementType);

    return baseType switch
    {
        // Existing cases...
        "int" or "System.Int32" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        
        // NEW: Unsigned integer types
        "ulong" or "System.UInt64" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        "uint" or "System.UInt32" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        "ushort" or "System.UInt16" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        "byte" or "System.Byte" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        "sbyte" or "System.SByte" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        "short" or "System.Int16" => $"new AttributeValue {{ N = {valueExpression}.ToString() }}",
        
        // ... rest of cases
    };
}
```

#### GetNumericConversionExpression

This method already includes the unsigned types (found during analysis), so no changes needed here.

#### GenerateFormattedPropertySerialization and GenerateFormattedPropertyDeserialization

Add cases for format string support (optional, for consistency):

```csharp
// In GenerateFormattedPropertySerialization
else if (baseType is "ulong" or "System.UInt64" or "uint" or "System.UInt32" or 
         "ushort" or "System.UInt16" or "byte" or "System.Byte" or 
         "sbyte" or "System.SByte" or "short" or "System.Int16")
{
    sb.AppendLine($"var formatted = {valueExpression}.ToString(\"{format}\", System.Globalization.CultureInfo.InvariantCulture);");
}
```

## Data Models

No new data models are required. The existing `PropertyModel` class already captures all necessary information about property types.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: All new numeric types are accepted by the source generator

*For any* entity class containing properties of type `ulong`, `uint`, `ushort`, `byte`, `sbyte`, or `short`, the source generator SHALL NOT produce a DYNDB009 diagnostic.

**Validates: Requirements 1.1, 2.1, 3.1, 4.1, 5.1, 6.1**

### Property 2: Serialization round-trip consistency

*For any* valid value of type `ulong`, `uint`, `ushort`, `byte`, `sbyte`, or `short`, serializing to an AttributeValue and then deserializing back SHALL produce the original value.

**Validates: Requirements 7.1**

### Property 3: Collection serialization round-trip

*For any* List or HashSet containing values of type `ulong`, `uint`, `ushort`, `byte`, `sbyte`, or `short`, serializing to DynamoDB format and deserializing back SHALL produce a collection with the same elements.

**Validates: Requirements 1.5, 2.5, 3.5, 4.5, 5.5, 6.5**

## Error Handling

The implementation follows existing error handling patterns:

1. **Parse Errors**: If a DynamoDB Number attribute contains a value that cannot be parsed to the target type (e.g., overflow), the standard `Parse` method will throw a `FormatException` or `OverflowException`. This is consistent with existing numeric type handling.

2. **Null Handling**: Nullable types (`ulong?`, `uint?`, etc.) follow the existing pattern where null values are skipped during serialization and result in default values during deserialization if the attribute is missing.

## Testing Strategy

### Unit Tests

Unit tests will verify:
- Code generation produces correct serialization expressions
- Code generation produces correct deserialization expressions
- Nullable type handling generates correct conditional logic
- Collection type handling generates correct LINQ expressions

### Property-Based Tests

Property-based tests using FsCheck will verify:
- Round-trip consistency for all new numeric types
- Collection round-trip consistency
- Boundary value handling (0, max values)

**Property-Based Testing Configuration**:
- Framework: FsCheck with xUnit integration
- Minimum iterations: 100 per property
- Each test tagged with: **Feature: unsigned-integer-types, Property N: {property_text}**
