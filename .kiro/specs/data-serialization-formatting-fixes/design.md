# Design Document

## Overview

This design addresses critical gaps in data serialization, formatting, and encryption within the Oproto.FluentDynamoDb library. The solution involves changes to three main components:

1. **Source Generator (MapperGenerator)** - Enhance ToDynamoDb/FromDynamoDb generation to apply format strings and preserve DateTime Kind
2. **UpdateExpressionTranslator** - Apply format strings and handle encryption in update expressions
3. **DynamoDbAttribute** - Add DateTimeKind parameter for timezone preservation

The design prioritizes backward compatibility, performance, and security while maintaining the library's AOT-compatibility guarantees.

## Architecture

### Component Interaction Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        User Entity Class                         │
│  [DynamoDbAttribute("date", Format="yyyy-MM-dd", Kind=Utc)]     │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Source Generator                             │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ EntityAnalyzer: Extract Format & DateTimeKind metadata  │   │
│  └──────────────────────┬───────────────────────────────────┘   │
│                         ▼                                        │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ MapperGenerator: Generate ToDynamoDb/FromDynamoDb        │   │
│  │  - Apply format strings during serialization            │   │
│  │  - Parse formatted values during deserialization         │   │
│  │  - Convert DateTime to specified Kind                    │   │
│  └──────────────────────────────────────────────────────────┘   │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                  Generated Entity Code                           │
│  - ToDynamoDb: Formats values, converts DateTime Kind           │
│  - FromDynamoDb: Parses formatted values, sets DateTime Kind    │
│  - Metadata: Includes Format and DateTimeKind info              │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│              UpdateExpressionTranslator (Runtime)                │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ TranslateSimpleSet: Apply format from metadata          │   │
│  │ TranslateBinaryOperation: Format operands                │   │
│  │ TranslateIfNotExists: Format default values              │   │
│  │ ApplyEncryption: Encrypt sensitive values (NEW)          │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## Detailed Design

### 1. DateTime Kind Support

#### 1.1 DynamoDbAttribute Enhancement

Add a new `DateTimeKind` property to the `DynamoDbAttributeAttribute` class:

```csharp
public class DynamoDbAttributeAttribute : Attribute
{
    public string AttributeName { get; }
    public string? Format { get; set; }
    
    // NEW: DateTime Kind specification
    public DateTimeKind DateTimeKind { get; set; } = DateTimeKind.Unspecified;
    
    public DynamoDbAttributeAttribute(string attributeName)
    {
        AttributeName = attributeName;
    }
}
```

**Rationale**: Using the existing `DateTimeKind` enum provides type safety and is familiar to .NET developers.

#### 1.2 PropertyModel Enhancement

Add DateTimeKind to the PropertyModel in the source generator:

```csharp
internal class PropertyModel
{
    // ... existing properties ...
    public string? Format { get; set; }
    
    // NEW: DateTime Kind for timezone handling
    public DateTimeKind? DateTimeKind { get; set; }
}
```

#### 1.3 EntityAnalyzer Changes

Extract DateTimeKind from the attribute during analysis:

```csharp
// In EntityAnalyzer.AnalyzeProperty()
if (attributeData.NamedArguments.TryGetValue("DateTimeKind", out var kindValue))
{
    property.DateTimeKind = (DateTimeKind)kindValue.Value;
}
```


#### 1.4 MapperGenerator Changes for DateTime Kind

**ToDynamoDb Generation:**

```csharp
// For DateTime properties with DateTimeKind specified
if (property.PropertyType == "DateTime" && property.DateTimeKind.HasValue)
{
    sb.AppendLine($"            if (typedEntity.{propertyName}.HasValue)");
    sb.AppendLine("            {");
    
    // Convert to specified kind before serialization
    switch (property.DateTimeKind.Value)
    {
        case DateTimeKind.Utc:
            sb.AppendLine($"                var {propertyName}Value = typedEntity.{propertyName}.Value.ToUniversalTime();");
            break;
        case DateTimeKind.Local:
            sb.AppendLine($"                var {propertyName}Value = typedEntity.{propertyName}.Value.ToLocalTime();");
            break;
        default:
            sb.AppendLine($"                var {propertyName}Value = typedEntity.{propertyName}.Value;");
            break;
    }
    
    // Apply format if specified
    if (!string.IsNullOrEmpty(property.Format))
    {
        sb.AppendLine($"                var {propertyName}Formatted = {propertyName}Value.ToString(\"{property.Format}\", CultureInfo.InvariantCulture);");
        sb.AppendLine($"                item[\"{property.AttributeName}\"] = new AttributeValue {{ S = {propertyName}Formatted }};");
    }
    else
    {
        sb.AppendLine($"                item[\"{property.AttributeName}\"] = new AttributeValue {{ S = {propertyName}Value.ToString(\"o\", CultureInfo.InvariantCulture) }};");
    }
    
    sb.AppendLine("            }");
}
```

**FromDynamoDb Generation:**

```csharp
// For DateTime properties with DateTimeKind specified
if (property.PropertyType == "DateTime" && property.DateTimeKind.HasValue)
{
    sb.AppendLine($"            if (item.TryGetValue(\"{property.AttributeName}\", out var {propertyName}Attr) && {propertyName}Attr.S != null)");
    sb.AppendLine("            {");
    
    // Parse with format if specified
    if (!string.IsNullOrEmpty(property.Format))
    {
        sb.AppendLine($"                if (DateTime.TryParseExact({propertyName}Attr.S, \"{property.Format}\", CultureInfo.InvariantCulture, DateTimeStyles.None, out var {propertyName}Parsed))");
    }
    else
    {
        sb.AppendLine($"                if (DateTime.TryParse({propertyName}Attr.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var {propertyName}Parsed))");
    }
    
    sb.AppendLine("                {");
    
    // Set the DateTime Kind
    switch (property.DateTimeKind.Value)
    {
        case DateTimeKind.Utc:
            sb.AppendLine($"                    entity.{propertyName} = DateTime.SpecifyKind({propertyName}Parsed, DateTimeKind.Utc);");
            break;
        case DateTimeKind.Local:
            sb.AppendLine($"                    entity.{propertyName} = DateTime.SpecifyKind({propertyName}Parsed, DateTimeKind.Local);");
            break;
        default:
            sb.AppendLine($"                    entity.{propertyName} = {propertyName}Parsed;");
            break;
    }
    
    sb.AppendLine("                }");
    sb.AppendLine("                else");
    sb.AppendLine("                {");
    sb.AppendLine($"                    throw new DynamoDbMappingException($\"Failed to parse DateTime value '{{propertyName}Attr.S}}' for property '{propertyName}' using format '{property.Format ?? "default"}'\");");
    sb.AppendLine("                }");
    sb.AppendLine("            }");
}
```

### 2. Format String Application in Serialization

#### 2.1 Current State Analysis

The `Format` property already exists in `DynamoDbAttributeAttribute` and is extracted into `PropertyModel.Format`. However, it's **not currently applied** during ToDynamoDb/FromDynamoDb generation.

#### 2.2 MapperGenerator Enhancement for Format Strings

**ToDynamoDb Generation:**

For each property with a format string, generate code that applies the format before creating the AttributeValue:

```csharp
private static void GeneratePropertyToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
{
    // ... existing null checks ...
    
    if (!string.IsNullOrEmpty(property.Format))
    {
        // Generate format application code
        GenerateFormattedPropertySerialization(sb, property);
    }
    else
    {
        // Generate default serialization code
        GenerateDefaultPropertySerialization(sb, property);
    }
}

private static void GenerateFormattedPropertySerialization(StringBuilder sb, PropertyModel property)
{
    var propertyName = property.PropertyName;
    var attributeName = property.AttributeName;
    var format = property.Format;
    
    sb.AppendLine($"            if (typedEntity.{propertyName}.HasValue)");
    sb.AppendLine("            {");
    sb.AppendLine("                try");
    sb.AppendLine("                {");
    
    // Determine the formatting approach based on property type
    if (IsDateTimeType(property.PropertyType))
    {
        sb.AppendLine($"                    var formatted = typedEntity.{propertyName}.Value.ToString(\"{format}\", CultureInfo.InvariantCulture);");
    }
    else if (IsNumericType(property.PropertyType))
    {
        sb.AppendLine($"                    var formatted = typedEntity.{propertyName}.Value.ToString(\"{format}\", CultureInfo.InvariantCulture);");
    }
    else if (IsFormattableType(property.PropertyType))
    {
        sb.AppendLine($"                    var formatted = ((IFormattable)typedEntity.{propertyName}.Value).ToString(\"{format}\", CultureInfo.InvariantCulture);");
    }
    
    sb.AppendLine($"                    item[\"{attributeName}\"] = new AttributeValue {{ S = formatted }};");
    sb.AppendLine("                }");
    sb.AppendLine("                catch (FormatException ex)");
    sb.AppendLine("                {");
    sb.AppendLine($"                    throw new FormatException($\"Invalid format string '{format}' for property '{propertyName}' of type '{property.PropertyType}'. Error: {{ex.Message}}\", ex);");
    sb.AppendLine("                }");
    sb.AppendLine("            }");
}
```

**FromDynamoDb Generation:**

For deserialization, parse the formatted string back to the original type:

```csharp
private static void GenerateFormattedPropertyDeserialization(StringBuilder sb, PropertyModel property)
{
    var propertyName = property.PropertyName;
    var attributeName = property.AttributeName;
    var format = property.Format;
    
    sb.AppendLine($"            if (item.TryGetValue(\"{attributeName}\", out var {propertyName}Attr) && {propertyName}Attr.S != null)");
    sb.AppendLine("            {");
    sb.AppendLine("                try");
    sb.AppendLine("                {");
    
    // Parse based on property type
    if (IsDateTimeType(property.PropertyType))
    {
        sb.AppendLine($"                    if (DateTime.TryParseExact({propertyName}Attr.S, \"{format}\", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))");
        sb.AppendLine($"                        entity.{propertyName} = parsed;");
        sb.AppendLine("                    else");
        sb.AppendLine($"                        throw new DynamoDbMappingException($\"Failed to parse DateTime value '{{propertyName}Attr.S}}' using format '{format}'\");");
    }
    else if (property.PropertyType == "decimal")
    {
        sb.AppendLine($"                    if (decimal.TryParse({propertyName}Attr.S, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))");
        sb.AppendLine($"                        entity.{propertyName} = parsed;");
        sb.AppendLine("                    else");
        sb.AppendLine($"                        throw new DynamoDbMappingException($\"Failed to parse decimal value '{{propertyName}Attr.S}}'\");");
    }
    else if (IsNumericType(property.PropertyType))
    {
        var parseMethod = GetParseMethod(property.PropertyType);
        sb.AppendLine($"                    if ({parseMethod}({propertyName}Attr.S, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))");
        sb.AppendLine($"                        entity.{propertyName} = parsed;");
        sb.AppendLine("                    else");
        sb.AppendLine($"                        throw new DynamoDbMappingException($\"Failed to parse {property.PropertyType} value '{{propertyName}Attr.S}}'\");");
    }
    
    sb.AppendLine("                }");
    sb.AppendLine("                catch (Exception ex) when (ex is not DynamoDbMappingException)");
    sb.AppendLine("                {");
    sb.AppendLine($"                    throw new DynamoDbMappingException($\"Failed to deserialize property '{propertyName}' from attribute '{attributeName}'. Value: '{{propertyName}Attr.S}}'. Error: {{ex.Message}}\", ex);");
    sb.AppendLine("                }");
    sb.AppendLine("            }");
}
```


### 3. Format String Application in Update Expressions

#### 3.1 Current State

The `UpdateExpressionTranslator` already has an `ApplyFormat` method (lines 1156-1179 in UpdateExpressionTranslator.cs), but it's **already implemented and working**. However, we need to ensure it's consistently called for all operation types.

#### 3.2 Enhancement Strategy

Review and ensure format application in all translation methods:

1. **TranslateSimpleSet** - ✅ Already applies format (line 456)
2. **TranslateBinaryOperation** - ✅ Already applies format (line 577)
3. **TranslateAddOperation** - ✅ Already applies format (line 651)
4. **TranslateIfNotExistsFunction** - ✅ Already applies format (line 677)
5. **TranslateListAppendFunction** - ⚠️ Applies format to list elements (line 783)
6. **TranslateListPrependFunction** - ⚠️ Applies format to list elements (line 867)

**Conclusion**: Format string application is already implemented in UpdateExpressionTranslator. The issue mentioned in PHASE2_LIMITATIONS.md may have been resolved. We need to verify with integration tests.

### 4. Encryption Support in Update Expressions

#### 4.1 Problem Analysis

The `IFieldEncryptor` interface is **async**, but `UpdateExpressionTranslator.TranslateUpdateExpression` is **synchronous**. This creates an architectural mismatch.

Current code (lines 1119-1138):
```csharp
private object? ApplyEncryption(object? value, string propertyName, string attributeName, Expression expression)
{
    if (value == null)
        return null;

    if (_fieldEncryptor == null)
    {
        throw new EncryptionRequiredException(...);
    }

    // Note: Encryption is async, but update expression translation is sync
    // This is a limitation that will need to be addressed in the design
    throw new NotSupportedException(
        $"Synchronous encryption is not supported in update expressions. " +
        $"Property '{propertyName}' (DynamoDB attribute: '{attributeName}') is marked as encrypted. " +
        $"Consider using string-based update expressions with pre-encrypted values, " +
        $"or encrypt the value before passing it to the expression.");
}
```

#### 4.2 Architectural Options

**Option A: Make UpdateExpressionTranslator Async (Breaking Change)**

Pros:
- Clean architecture - async all the way
- Consistent with other async operations
- No performance compromises

Cons:
- **Breaking change** - All callers must be updated
- Affects UpdateItemRequestBuilder.Set() method signature
- Ripple effect through the codebase

**Option B: Synchronous Encryption Wrapper**

Pros:
- No breaking changes
- Simple to implement
- Works with existing code

Cons:
- **Performance impact** - Blocking async calls
- Anti-pattern in async codebases
- Potential deadlock risks in some contexts

Implementation:
```csharp
private object? ApplyEncryption(object? value, string propertyName, string attributeName, Expression expression)
{
    if (value == null || _fieldEncryptor == null)
        return value;

    try
    {
        // Convert value to bytes
        var plaintext = Encoding.UTF8.GetBytes(value.ToString() ?? string.Empty);
        
        // Create encryption context
        var context = new FieldEncryptionContext
        {
            EntityType = attributeName, // Use attribute name as entity type
            FieldName = propertyName
        };
        
        // BLOCKING ASYNC CALL - Not ideal but necessary for sync context
        var ciphertext = _fieldEncryptor.EncryptAsync(plaintext, propertyName, context, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        
        // Convert to base64 for storage
        return Convert.ToBase64String(ciphertext);
    }
    catch (Exception ex)
    {
        throw new FieldEncryptionException($"Failed to encrypt property '{propertyName}': {ex.Message}", ex);
    }
}
```

**Option C: Defer Encryption to Request Builder (Architectural Change)**

Pros:
- No breaking changes to translator
- Encryption happens at the right layer
- Can be async naturally

Cons:
- More complex implementation
- Requires request builder to understand encryption
- Delayed encryption (happens after expression translation)

Implementation approach:
1. Translator marks parameters as "needs encryption" in metadata
2. Request builder encrypts marked parameters before sending to DynamoDB
3. Requires new parameter metadata structure

**Option D: Hybrid Approach - Sync Wrapper with Async Alternative**

Pros:
- No breaking changes (sync wrapper for existing code)
- Provides async path for new code
- Gradual migration path

Cons:
- Two code paths to maintain
- More complex API surface

Implementation:
```csharp
// Existing sync method uses wrapper
public string TranslateUpdateExpression<TUpdateExpressions, TUpdateModel>(...)
{
    // Uses sync wrapper for encryption
}

// New async method for encryption support
public async Task<string> TranslateUpdateExpressionAsync<TUpdateExpressions, TUpdateModel>(
    Expression<Func<TUpdateExpressions, TUpdateModel>> expression,
    ExpressionContext context,
    CancellationToken cancellationToken = default)
{
    // Can properly await encryption
}
```

#### 4.3 Recommended Approach: Option C (Defer Encryption to Request Builder)

**Rationale:**
1. **No breaking changes** - Translator remains synchronous
2. **Proper async handling** - Encryption happens at the right layer where async is natural
3. **Better architecture** - Separation of concerns (translator builds expressions, request builder handles I/O)
4. **No performance compromises** - True async encryption without blocking
5. **Consistent with blob references** - Similar pattern already used for blob storage

**Architecture:**

The UpdateExpressionTranslator will mark parameters that need encryption, and the UpdateItemRequestBuilder will encrypt them before sending to DynamoDB.

**Implementation plan:**

1. **Extend AttributeValue metadata** - Add encryption marker to parameter tracking
2. **Translator changes** - Mark encrypted parameters instead of encrypting inline
3. **Request builder changes** - Encrypt marked parameters before ExecuteAsync
4. **Context propagation** - Pass encryption context through the request pipeline

**Detailed Implementation:**

```csharp
// 1. New class to track parameter metadata
public class ParameterMetadata
{
    public string ParameterName { get; set; }
    public AttributeValue Value { get; set; }
    public bool RequiresEncryption { get; set; }
    public string? PropertyName { get; set; }
    public string? AttributeName { get; set; }
}

// 2. Update ExpressionContext to track parameter metadata
public class ExpressionContext
{
    // Existing properties...
    public List<ParameterMetadata> ParameterMetadata { get; } = new();
}

// 3. UpdateExpressionTranslator marks encrypted parameters
private string CaptureValue(object? value, ExpressionContext context, PropertyMetadata? propertyMetadata)
{
    var attributeValue = ConvertToAttributeValue(value);
    var parameterName = context.ParameterGenerator.GenerateParameterName();
    
    // Add to attribute values as before
    context.AttributeValues.AttributeValues.Add(parameterName, attributeValue);
    
    // NEW: Track metadata for encryption
    if (propertyMetadata?.IsEncrypted == true)
    {
        context.ParameterMetadata.Add(new ParameterMetadata
        {
            ParameterName = parameterName,
            Value = attributeValue,
            RequiresEncryption = true,
            PropertyName = propertyMetadata.PropertyName,
            AttributeName = propertyMetadata.AttributeName
        });
    }
    
    return parameterName;
}

// 4. UpdateItemRequestBuilder encrypts before sending
public async Task<UpdateItemResponse> UpdateAsync(CancellationToken cancellationToken = default)
{
    // Build the request as usual
    var request = ToRequest();
    
    // NEW: Encrypt marked parameters
    if (_context.ParameterMetadata.Any(p => p.RequiresEncryption))
    {
        if (_fieldEncryptor == null)
        {
            throw new InvalidOperationException(
                "Field encryption is required but no IFieldEncryptor is configured. " +
                "Configure an encryptor in the DynamoDbOperationContext.");
        }
        
        await EncryptParametersAsync(request, cancellationToken);
    }
    
    // Send to DynamoDB
    return await _client.UpdateItemAsync(request, cancellationToken);
}

private async Task EncryptParametersAsync(UpdateItemRequest request, CancellationToken cancellationToken)
{
    foreach (var param in _context.ParameterMetadata.Where(p => p.RequiresEncryption))
    {
        // Get the current value from the request
        if (!request.ExpressionAttributeValues.TryGetValue(param.ParameterName, out var attributeValue))
            continue;
        
        // Extract plaintext (assuming string value)
        var plaintext = Encoding.UTF8.GetBytes(attributeValue.S ?? string.Empty);
        
        // Create encryption context
        var encryptionContext = new FieldEncryptionContext
        {
            EntityType = _tableName,
            FieldName = param.PropertyName ?? param.AttributeName ?? "unknown"
        };
        
        // Encrypt
        var ciphertext = await _fieldEncryptor.EncryptAsync(
            plaintext,
            param.PropertyName ?? param.AttributeName ?? "unknown",
            encryptionContext,
            cancellationToken);
        
        // Replace with encrypted value (base64 encoded)
        request.ExpressionAttributeValues[param.ParameterName] = new AttributeValue
        {
            S = Convert.ToBase64String(ciphertext)
        };
    }
}
```

**Benefits of this approach:**
- Translator stays simple and synchronous
- Encryption happens where async is natural (request builder)
- Consistent with existing patterns (blob references work similarly)
- No blocking async calls
- Clear separation of concerns

**Challenges:**
- More complex implementation (need to track parameter metadata)
- Encryption happens later in the pipeline (after expression translation)
- Need to propagate encryption context through request builder

**Mitigation:**
- Comprehensive testing of parameter metadata tracking
- Clear documentation of the encryption flow
- Integration tests to verify end-to-end encryption

### 5. PropertyMetadata Enhancement

The `PropertyMetadata` class needs to include encryption information for the UpdateExpressionTranslator to detect encrypted properties.

#### 5.1 Current PropertyMetadata

```csharp
public class PropertyMetadata
{
    public string PropertyName { get; set; }
    public string AttributeName { get; set; }
    public Type PropertyType { get; set; }
    public bool IsPartitionKey { get; set; }
    public bool IsSortKey { get; set; }
    public string? Format { get; set; }
    // Missing: IsEncrypted flag
}
```

#### 5.2 Enhanced PropertyMetadata

```csharp
public class PropertyMetadata
{
    public string PropertyName { get; set; }
    public string AttributeName { get; set; }
    public Type PropertyType { get; set; }
    public bool IsPartitionKey { get; set; }
    public bool IsSortKey { get; set; }
    public string? Format { get; set; }
    
    // NEW: Encryption flag
    public bool IsEncrypted { get; set; }
    
    // NEW: DateTime Kind for timezone handling
    public DateTimeKind? DateTimeKind { get; set; }
}
```

#### 5.3 MapperGenerator Changes for Metadata

Update the `GenerateGetEntityMetadataMethod` to include encryption and DateTime Kind information:

```csharp
private static void GenerateGetEntityMetadataMethod(StringBuilder sb, EntityModel entity)
{
    sb.AppendLine("        public static EntityMetadata GetEntityMetadata()");
    sb.AppendLine("        {");
    sb.AppendLine("            return new EntityMetadata");
    sb.AppendLine("            {");
    sb.AppendLine($"                EntityType = typeof({entity.ClassName}),");
    sb.AppendLine($"                TableName = \"{entity.TableName}\",");
    sb.AppendLine("                Properties = new[]");
    sb.AppendLine("                {");
    
    foreach (var property in entity.Properties.Where(p => p.HasAttributeMapping))
    {
        sb.AppendLine("                    new PropertyMetadata");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        PropertyName = \"{property.PropertyName}\",");
        sb.AppendLine($"                        AttributeName = \"{property.AttributeName}\",");
        sb.AppendLine($"                        PropertyType = typeof({property.PropertyType}),");
        sb.AppendLine($"                        IsPartitionKey = {property.IsPartitionKey.ToString().ToLower()},");
        sb.AppendLine($"                        IsSortKey = {property.IsSortKey.ToString().ToLower()},");
        
        if (!string.IsNullOrEmpty(property.Format))
        {
            sb.AppendLine($"                        Format = \"{property.Format}\",");
        }
        
        if (property.Security?.IsEncrypted == true)
        {
            sb.AppendLine($"                        IsEncrypted = true,");
        }
        
        if (property.DateTimeKind.HasValue)
        {
            sb.AppendLine($"                        DateTimeKind = DateTimeKind.{property.DateTimeKind.Value},");
        }
        
        sb.AppendLine("                    },");
    }
    
    sb.AppendLine("                }");
    sb.AppendLine("            };");
    sb.AppendLine("        }");
}
```

#### 5.4 UpdateExpressionTranslator Changes

Update `IsEncryptedProperty` to use the metadata:

```csharp
private bool IsEncryptedProperty(PropertyMetadata propertyMetadata)
{
    return propertyMetadata.IsEncrypted;
}
```


### 6. Error Handling Strategy

#### 6.1 Exception Hierarchy

```
Exception
├── FormatException (existing .NET exception)
│   └── Used for invalid format strings
├── DynamoDbMappingException (existing library exception)
│   └── Used for deserialization failures
└── FieldEncryptionException (existing library exception)
    └── Used for encryption/decryption failures
```

#### 6.2 Error Messages

All error messages should include:
1. Property name (C# property)
2. Attribute name (DynamoDB attribute)
3. The problematic value (redacted if sensitive)
4. The format string or operation that failed
5. Guidance on how to fix the issue

Example error messages:

**Format String Error:**
```
Invalid format string 'DD-MM-YYYY' for property 'CreatedDate' (DynamoDB attribute: 'created_date') of type 'DateTime'. 
Error: The format string is not valid. 
Common format strings: 'o' for ISO 8601 dates, 'F2' for 2 decimal places, 'yyyy-MM-dd' for date-only.
```

**Parsing Error:**
```
Failed to parse DateTime value '2024-13-45' for property 'CreatedDate' (DynamoDB attribute: 'created_date') using format 'yyyy-MM-dd'. 
The stored value does not match the expected format. 
Ensure the format string matches the data stored in DynamoDB.
```

**Encryption Error:**
```
Failed to encrypt property 'SocialSecurityNumber' (DynamoDB attribute: 'ssn'): KMS key not found. 
Ensure the IFieldEncryptor is properly configured with valid encryption keys.
```

### 7. Performance Considerations

#### 7.1 Format String Caching

Format strings are compile-time constants, so no runtime caching is needed. The generated code directly embeds the format string.

#### 7.2 CultureInfo Caching

Use `CultureInfo.InvariantCulture` throughout, which is a singleton and doesn't require caching.

#### 7.3 Encryption Performance

Synchronous encryption wrapper will block, but typical encryption operations are fast:
- AWS KMS with caching: 1-5ms
- Local encryption (AES): <1ms

**Mitigation:**
- Document performance characteristics
- Recommend using encryption providers with caching
- Consider batching encrypted updates if possible

#### 7.4 Memory Allocations

Minimize allocations in hot paths:
- Reuse StringBuilder instances in generator
- Avoid unnecessary string concatenations
- Use span-based APIs where appropriate

### 8. Testing Strategy

#### 8.1 Unit Tests (Source Generator)

Test the generated code for various scenarios:

1. **Format String Tests:**
   - DateTime with various formats (yyyy-MM-dd, o, custom)
   - Decimal with precision formats (F2, F4, N2)
   - Integer with zero-padding (D5, D8)
   - Invalid format strings (should generate code that throws)

2. **DateTime Kind Tests:**
   - DateTimeKind.Utc conversion
   - DateTimeKind.Local conversion
   - DateTimeKind.Unspecified (default)
   - Round-trip preservation

3. **Encryption Tests:**
   - Encrypted properties in ToDynamoDb
   - Encrypted properties in FromDynamoDb
   - Multiple encrypted properties
   - Encryption with format strings

#### 8.2 Unit Tests (UpdateExpressionTranslator)

Test expression translation with:

1. **Format String Application:**
   - Simple SET with formatted DateTime
   - Arithmetic operations with formatted decimals
   - IfNotExists with formatted values
   - ListAppend/ListPrepend with formatted elements

2. **Encryption:**
   - Simple SET with encrypted property
   - Multiple encrypted properties in one expression
   - Encryption failure scenarios
   - Missing encryptor scenarios

#### 8.3 Integration Tests

End-to-end tests with DynamoDB Local:

1. **Format String Round-Trip:**
   - Store entity with formatted properties
   - Retrieve and verify values
   - Update with expression-based API
   - Verify format consistency

2. **DateTime Kind Round-Trip:**
   - Store DateTime with Utc kind
   - Retrieve and verify Kind is preserved
   - Store DateTime with Local kind
   - Retrieve and verify Kind is preserved

3. **Encryption Round-Trip:**
   - Store entity with encrypted properties
   - Retrieve and decrypt
   - Update encrypted property via expression
   - Verify encryption is maintained

#### 8.4 Test Coverage Goals

- Source Generator: >95% coverage
- UpdateExpressionTranslator: >90% coverage
- Integration Tests: All critical paths covered

### 9. Documentation Updates

#### 9.1 API Documentation

Update XML documentation for:

1. **DynamoDbAttributeAttribute:**
   - Document DateTimeKind parameter
   - Provide examples of DateTime Kind usage
   - Explain timezone handling behavior

2. **UpdateExpressionTranslator:**
   - Document synchronous encryption behavior
   - Warn about performance implications
   - Provide guidance on encryption provider selection

3. **Generated Methods:**
   - Document format string application
   - Document DateTime Kind conversion
   - Document encryption behavior

#### 9.2 User Guide

Add new sections:

1. **Format Strings Guide:**
   - Common format patterns
   - Type-specific formatting
   - Performance considerations
   - Troubleshooting format errors

2. **DateTime Kind Guide:**
   - When to use Utc vs Local vs Unspecified
   - Timezone handling best practices
   - Migration from existing code

3. **Encryption Guide:**
   - Setting up IFieldEncryptor
   - Performance considerations
   - Security best practices
   - Troubleshooting encryption errors

#### 9.3 Migration Guide

For users upgrading from previous versions:

1. **Format Strings:**
   - No breaking changes
   - Opt-in by adding Format parameter
   - Examples of before/after

2. **DateTime Kind:**
   - No breaking changes (default is Unspecified)
   - Opt-in by adding DateTimeKind parameter
   - Guidance on choosing the right kind

3. **Encryption:**
   - No breaking changes
   - Now works in update expressions
   - Performance notes

### 10. Backward Compatibility

#### 10.1 Breaking Changes: NONE

All changes are additive:
- New DateTimeKind parameter (optional, defaults to Unspecified)
- Format strings only applied when explicitly specified
- Encryption only applied when property is marked as encrypted

#### 10.2 Behavioral Changes

1. **Format Strings:**
   - Previously ignored in ToDynamoDb/FromDynamoDb
   - Now applied when specified
   - Only affects entities with explicit Format parameter

2. **DateTime Kind:**
   - Previously always Unspecified
   - Now can be specified
   - Default remains Unspecified (no change)

3. **Encryption in Update Expressions:**
   - Previously threw NotSupportedException
   - Now works (synchronously)
   - Only affects entities with encrypted properties

#### 10.3 Deprecations: NONE

No existing APIs are deprecated.

### 11. Implementation Phases

#### Phase 1: DateTime Kind Support (1-2 days)
1. Add DateTimeKind to DynamoDbAttributeAttribute
2. Update EntityAnalyzer to extract DateTimeKind
3. Update PropertyModel with DateTimeKind
4. Update MapperGenerator for DateTime Kind handling
5. Add unit tests for DateTime Kind
6. Add integration tests for DateTime Kind

#### Phase 2: Format String Application (2-3 days)
1. Update MapperGenerator to apply format strings in ToDynamoDb
2. Update MapperGenerator to parse format strings in FromDynamoDb
3. Add error handling for invalid formats
4. Add unit tests for format string application
5. Add integration tests for format string round-trips
6. Verify UpdateExpressionTranslator format application (already implemented)

#### Phase 3: Encryption Support (3-4 days)
1. Add IsEncrypted to PropertyMetadata
2. Update MapperGenerator to include IsEncrypted in metadata
3. Create ParameterMetadata class for tracking encryption requirements
4. Update ExpressionContext to track parameter metadata
5. Update UpdateExpressionTranslator to mark encrypted parameters
6. Update UpdateItemRequestBuilder to encrypt marked parameters before sending
7. Add error handling for encryption failures
8. Add unit tests for encryption parameter marking
9. Add integration tests for encryption round-trips

#### Phase 4: Documentation and Testing (2-3 days)
1. Update XML documentation
2. Write user guide sections
3. Write migration guide
4. Add comprehensive integration tests
5. Performance testing and optimization
6. Update CHANGELOG.md

**Total Estimated Effort: 8-12 days**

### 12. Success Criteria

The implementation will be considered successful when:

1. ✅ All unit tests pass (>90% coverage)
2. ✅ All integration tests pass
3. ✅ Format strings are applied in ToDynamoDb/FromDynamoDb
4. ✅ Format strings are applied in update expressions
5. ✅ DateTime Kind is preserved in round-trips
6. ✅ Encryption works in update expressions
7. ✅ No breaking changes to existing code
8. ✅ Documentation is complete and accurate
9. ✅ Performance impact is <5% for format strings
10. ✅ All error messages are clear and actionable

### 13. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Format string parsing failures | Medium | High | Comprehensive error handling, clear error messages |
| DateTime Kind confusion | Low | Medium | Clear documentation, examples |
| Encryption performance issues | Low | Medium | Document performance, recommend caching providers |
| Breaking changes discovered | Low | High | Thorough testing, backward compatibility checks |
| Complex edge cases | Medium | Medium | Comprehensive test suite, integration tests |

### 14. Open Questions

1. **Should we support custom format providers?**
   - Current design uses InvariantCulture only
   - Could add CultureInfo parameter to DynamoDbAttribute
   - Decision: Defer to future version if requested

2. **Should we support encryption in other request builders?**
   - TransactWriteItemsRequestBuilder also uses update expressions
   - Should follow same pattern as UpdateItemRequestBuilder
   - Decision: Yes, apply same pattern to all request builders that support update expressions

3. **Should format strings apply to nested properties?**
   - Current design only applies to top-level properties
   - Nested objects use JSON serialization
   - Decision: Out of scope for this spec

4. **Should we validate format strings at compile time?**
   - Source generator could validate format strings
   - Would catch errors earlier
   - Decision: Nice to have, but not critical for v1

## Conclusion

This design provides a comprehensive solution for format string application, DateTime Kind preservation, and encryption support in update expressions. The approach prioritizes backward compatibility, performance, and security while maintaining the library's AOT-compatibility guarantees.

The deferred encryption approach (Option C) is the recommended approach as it provides proper async handling, better architecture with separation of concerns, and no performance compromises. The translator remains simple and synchronous, while encryption happens naturally at the request builder layer where async operations are expected.

All changes are additive and opt-in, ensuring existing code continues to work without modification.
