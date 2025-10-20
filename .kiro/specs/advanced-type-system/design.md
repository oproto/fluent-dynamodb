# Design Document

## Overview

This design extends the Oproto.FluentDynamoDb library to support advanced DynamoDB data types and storage patterns through three main areas:

1. **Native Collection Types**: Maps (M), Sets (SS/NS/BS), and Lists (L) with proper conversion and validation
2. **Specialized Storage**: TTL fields, JSON blob serialization, and external blob references (S3)
3. **Enhanced Format String Support**: Integration of advanced types into the existing format string parameter system

The design maintains AOT compatibility through source generation and avoids runtime reflection. Optional features are provided through separate NuGet packages to keep the core library lightweight.

## Architecture

### Package Structure

```
Oproto.FluentDynamoDb.Attributes/                # Attributes (.NET Standard 2.0)
├── TimeToLiveAttribute.cs                       # TTL field marker
├── DynamoDbMapAttribute.cs                      # Explicit map conversion
├── JsonBlobAttribute.cs                         # JSON serialization marker
├── BlobReferenceAttribute.cs                    # External blob marker
└── DynamoDbJsonSerializerAttribute.cs           # Assembly-level serializer selection

Oproto.FluentDynamoDb/                           # Core library (.NET 8)
├── Storage/
│   └── IBlobStorageProvider.cs                  # Blob provider interface
└── Utility/
    └── AttributeValueConverter.cs               # Enhanced type conversion

Oproto.FluentDynamoDb.SourceGenerator/           # Source generator (.NET Standard 2.0)
├── Generators/
│   ├── AdvancedTypeMapper.cs                    # Map/Set/List generation
│   ├── TtlConverter.cs                          # TTL conversion generation
│   ├── JsonBlobMapper.cs                        # JSON serialization generation
│   └── BlobReferenceMapper.cs                   # Blob storage generation
└── Analyzers/
    └── AdvancedTypeAnalyzer.cs                  # Validation and diagnostics

Oproto.FluentDynamoDb.SystemTextJson/            # System.Text.Json support (.NET 8)
└── SystemTextJsonSerializer.cs                  # AOT-compatible via source generation

Oproto.FluentDynamoDb.NewtonsoftJson/            # Newtonsoft.Json support (.NET 8)
└── NewtonsoftJsonSerializer.cs                  # Runtime serialization (limited AOT)

Oproto.FluentDynamoDb.BlobStorage.S3/            # S3 blob provider (.NET 8)
└── S3BlobProvider.cs
```


### Design Principles

1. **Zero Runtime Reflection**: All type conversions generated at compile-time (System.Text.Json fully AOT-compatible)
2. **Fail Fast**: Validate at compile-time where possible, runtime with clear errors otherwise
3. **Optional Dependencies**: Core library remains lightweight, advanced features in separate packages
4. **Empty Collection Safety**: Automatically handle DynamoDB's empty collection restriction
5. **Consistent API**: Advanced types work seamlessly with existing format string support
6. **Attributes in .NET Standard 2.0**: Separate attributes project for source generator compatibility

### AOT Compatibility Matrix

| Feature | System.Text.Json | Newtonsoft.Json |
|---------|------------------|-----------------|
| Basic Serialization | ✅ Full AOT support via source generation | ⚠️ Limited - uses runtime reflection |
| Trim-safe | ✅ Yes | ⚠️ Requires careful configuration |
| Performance | ✅ Optimized for AOT | ⚠️ Runtime overhead |
| Recommendation | **Recommended for AOT** | Use for compatibility only |

## Components and Interfaces

### 1. Attribute Definitions

#### TimeToLiveAttribute
```csharp
[AttributeUsage(AttributeTargets.Property)]
public class TimeToLiveAttribute : Attribute
{
    // Marker attribute - no properties needed
    // Source generator validates only one per entity
}
```

#### DynamoDbMapAttribute
```csharp
[AttributeUsage(AttributeTargets.Property)]
public class DynamoDbMapAttribute : Attribute
{
    // Explicit marker for complex object -> Map conversion
    // Without this, Dictionary<string, string> uses default conversion
    // With this, custom objects are recursively mapped
}
```

#### JsonBlobAttribute
```csharp
[AttributeUsage(AttributeTargets.Property)]
public class JsonBlobAttribute : Attribute
{
    public int? InlineThreshold { get; set; } // Max size before using external blob
}
```

#### BlobReferenceAttribute
```csharp
[AttributeUsage(AttributeTargets.Property)]
public class BlobReferenceAttribute : Attribute
{
    public BlobProvider Provider { get; }
    public string? BucketName { get; set; }
    public string? KeyPrefix { get; set; }
    public Type? ProviderType { get; set; } // For custom providers
    
    public BlobReferenceAttribute(BlobProvider provider)
    {
        Provider = provider;
    }
}

public enum BlobProvider
{
    S3,
    Custom
}
```

#### DynamoDbJsonSerializerAttribute
```csharp
[AttributeUsage(AttributeTargets.Assembly)]
public class DynamoDbJsonSerializerAttribute : Attribute
{
    public JsonSerializerType SerializerType { get; }
    
    public DynamoDbJsonSerializerAttribute(JsonSerializerType serializerType)
    {
        SerializerType = serializerType;
    }
}

public enum JsonSerializerType
{
    SystemTextJson,
    NewtonsoftJson
}
```


### 2. Blob Storage Provider Interface

```csharp
public interface IBlobStorageProvider
{
    /// <summary>
    /// Stores blob data and returns a reference key
    /// </summary>
    Task<string> StoreAsync(
        Stream data, 
        string? suggestedKey = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves blob data by reference key
    /// </summary>
    Task<Stream> RetrieveAsync(
        string referenceKey, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes blob data by reference key
    /// </summary>
    Task DeleteAsync(
        string referenceKey, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a blob exists
    /// </summary>
    Task<bool> ExistsAsync(
        string referenceKey, 
        CancellationToken cancellationToken = default);
}
```

### 3. Enhanced AttributeValueConverter

The core library's `AttributeValueConverter` utility class will be enhanced to support advanced types:

```csharp
public static class AttributeValueConverter
{
    // Existing methods remain unchanged
    
    // New: Map conversion
    public static AttributeValue ToMap(Dictionary<string, string> dict)
    {
        if (dict == null || dict.Count == 0)
            return null; // Caller should omit attribute
        
        var map = new Dictionary<string, AttributeValue>();
        foreach (var kvp in dict)
        {
            map[kvp.Key] = new AttributeValue { S = kvp.Value };
        }
        return new AttributeValue { M = map };
    }
    
    public static AttributeValue ToMap(Dictionary<string, AttributeValue> dict)
    {
        if (dict == null || dict.Count == 0)
            return null;
        
        return new AttributeValue { M = dict };
    }
    
    // New: Set conversions
    public static AttributeValue ToStringSet(HashSet<string> set)
    {
        if (set == null || set.Count == 0)
            return null;
        
        return new AttributeValue { SS = set.ToList() };
    }
    
    public static AttributeValue ToNumberSet(HashSet<int> set)
    {
        if (set == null || set.Count == 0)
            return null;
        
        return new AttributeValue { NS = set.Select(n => n.ToString()).ToList() };
    }
    
    public static AttributeValue ToNumberSet(HashSet<decimal> set)
    {
        if (set == null || set.Count == 0)
            return null;
        
        return new AttributeValue { NS = set.Select(n => n.ToString()).ToList() };
    }
    
    public static AttributeValue ToBinarySet(HashSet<byte[]> set)
    {
        if (set == null || set.Count == 0)
            return null;
        
        return new AttributeValue { BS = set.Select(b => new MemoryStream(b)).ToList() };
    }
    
    // New: List conversion
    public static AttributeValue ToList<T>(List<T> list, Func<T, AttributeValue> converter)
    {
        if (list == null || list.Count == 0)
            return null;
        
        return new AttributeValue { L = list.Select(converter).ToList() };
    }
    
    // New: TTL conversion
    public static AttributeValue ToTtl(DateTime? dateTime)
    {
        if (!dateTime.HasValue)
            return null;
        
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var seconds = (long)(dateTime.Value.ToUniversalTime() - epoch).TotalSeconds;
        return new AttributeValue { N = seconds.ToString() };
    }
    
    public static AttributeValue ToTtl(DateTimeOffset? dateTimeOffset)
    {
        if (!dateTimeOffset.HasValue)
            return null;
        
        var seconds = dateTimeOffset.Value.ToUnixTimeSeconds();
        return new AttributeValue { N = seconds.ToString() };
    }
    
    // New: From conversions
    public static Dictionary<string, string> FromMap(AttributeValue av)
    {
        if (av?.M == null)
            return null;
        
        return av.M.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.S);
    }
    
    public static HashSet<string> FromStringSet(AttributeValue av)
    {
        if (av?.SS == null || av.SS.Count == 0)
            return null;
        
        return new HashSet<string>(av.SS);
    }
    
    public static DateTime? FromTtl(AttributeValue av)
    {
        if (av?.N == null)
            return null;
        
        var seconds = long.Parse(av.N);
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddSeconds(seconds);
    }
}
```


### 4. Enhanced Format String Support

The existing `AttributeValueInternal.AddFormattedValue()` method will be enhanced to detect and convert advanced types:

```csharp
internal class AttributeValueInternal
{
    // Existing code...
    
    public string AddFormattedValue(object value, string? format = null)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value), "Cannot use null value in format string");
        
        var paramName = _parameterGenerator.GenerateParameterName();
        var attributeValue = ConvertToAttributeValue(value, format);
        
        if (attributeValue == null)
        {
            throw new ArgumentException(
                $"Cannot use empty collection in format string. " +
                $"DynamoDB does not support empty Maps, Sets, or Lists. " +
                $"Parameter: {paramName}, Type: {value.GetType().Name}");
        }
        
        AttributeValues.Add(paramName, attributeValue);
        return paramName;
    }
    
    private AttributeValue ConvertToAttributeValue(object value, string? format)
    {
        // Handle advanced types
        return value switch
        {
            // Maps
            Dictionary<string, string> dict => AttributeValueConverter.ToMap(dict),
            Dictionary<string, AttributeValue> dict => AttributeValueConverter.ToMap(dict),
            
            // Sets
            HashSet<string> set => AttributeValueConverter.ToStringSet(set),
            HashSet<int> set => AttributeValueConverter.ToNumberSet(set),
            HashSet<decimal> set => AttributeValueConverter.ToNumberSet(set),
            HashSet<byte[]> set => AttributeValueConverter.ToBinarySet(set),
            
            // Lists
            List<string> list => AttributeValueConverter.ToList(list, s => new AttributeValue { S = s }),
            List<int> list => AttributeValueConverter.ToList(list, n => new AttributeValue { N = n.ToString() }),
            
            // TTL (when format suggests it)
            DateTime dt when format?.Contains("ttl") == true => AttributeValueConverter.ToTtl(dt),
            DateTimeOffset dto when format?.Contains("ttl") == true => AttributeValueConverter.ToTtl(dto),
            
            // Existing basic types
            string s => new AttributeValue { S = s },
            int n => new AttributeValue { N = n.ToString() },
            decimal d => new AttributeValue { N = d.ToString() },
            bool b => new AttributeValue { BOOL = b },
            
            // Formatted strings
            _ when format != null => new AttributeValue { S = string.Format($"{{0:{format}}}", value) },
            
            // Default string conversion
            _ => new AttributeValue { S = value.ToString() }
        };
    }
}
```


## Source Generator Enhancements

### 1. Advanced Type Detection

The source generator will analyze properties and detect advanced types:

```csharp
public class AdvancedTypeAnalyzer
{
    public AdvancedTypeInfo AnalyzeProperty(PropertyModel property)
    {
        var info = new AdvancedTypeInfo
        {
            PropertyName = property.PropertyName,
            IsMap = IsMapType(property),
            IsSet = IsSetType(property),
            IsList = IsListType(property),
            IsTtl = property.Attributes.Any(a => a.Name == "TimeToLive"),
            IsJsonBlob = property.Attributes.Any(a => a.Name == "JsonBlob"),
            IsBlobReference = property.Attributes.Any(a => a.Name == "BlobReference")
        };
        
        ValidateTypeConfiguration(info, property);
        return info;
    }
    
    private bool IsMapType(PropertyModel property)
    {
        // Dictionary<string, string> or Dictionary<string, AttributeValue>
        if (property.PropertyType.StartsWith("Dictionary<"))
            return true;
        
        // Custom class with [DynamoDbMap]
        if (property.Attributes.Any(a => a.Name == "DynamoDbMap"))
            return true;
        
        return false;
    }
    
    private bool IsSetType(PropertyModel property)
    {
        return property.PropertyType.StartsWith("HashSet<");
    }
    
    private bool IsListType(PropertyModel property)
    {
        return property.PropertyType.StartsWith("List<");
    }
    
    private void ValidateTypeConfiguration(AdvancedTypeInfo info, PropertyModel property)
    {
        // TTL validation
        if (info.IsTtl && !property.PropertyType.Contains("DateTime"))
        {
            ReportError("DYNDB101", 
                $"[TimeToLive] can only be used on DateTime or DateTimeOffset properties. " +
                $"Property '{property.PropertyName}' is type '{property.PropertyType}'");
        }
        
        // JSON blob validation
        if (info.IsJsonBlob && !HasJsonSerializerPackage())
        {
            ReportError("DYNDB102",
                $"[JsonBlob] requires referencing either Oproto.FluentDynamoDb.SystemTextJson " +
                $"or Oproto.FluentDynamoDb.NewtonsoftJson package");
        }
        
        // Blob reference validation
        if (info.IsBlobReference && !HasBlobProviderPackage())
        {
            ReportError("DYNDB103",
                $"[BlobReference] requires referencing a blob provider package like " +
                $"Oproto.FluentDynamoDb.BlobStorage.S3");
        }
        
        // Incompatible combinations
        if (info.IsTtl && (info.IsJsonBlob || info.IsBlobReference))
        {
            ReportError("DYNDB104",
                $"[TimeToLive] cannot be combined with [JsonBlob] or [BlobReference]");
        }
    }
}

public class AdvancedTypeInfo
{
    public string PropertyName { get; set; }
    public bool IsMap { get; set; }
    public bool IsSet { get; set; }
    public bool IsList { get; set; }
    public bool IsTtl { get; set; }
    public bool IsJsonBlob { get; set; }
    public bool IsBlobReference { get; set; }
}
```


### 2. Generated Mapping Code

#### Map Property Example

```csharp
// Entity definition
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string Id { get; set; }
    
    [DynamoDbAttribute("metadata")]
    public Dictionary<string, string> Metadata { get; set; }
    
    [DynamoDbAttribute("attributes")]
    [DynamoDbMap]
    public ProductAttributes Attributes { get; set; }
}

// Generated ToDynamoDb
public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity)
    where TSelf : IDynamoDbEntity
{
    var typedEntity = (Product)(object)entity;
    var item = new Dictionary<string, AttributeValue>();
    
    item["pk"] = new AttributeValue { S = typedEntity.Id };
    
    // Simple dictionary map
    if (typedEntity.Metadata != null && typedEntity.Metadata.Count > 0)
    {
        var metadataMap = new Dictionary<string, AttributeValue>();
        foreach (var kvp in typedEntity.Metadata)
        {
            metadataMap[kvp.Key] = new AttributeValue { S = kvp.Value };
        }
        item["metadata"] = new AttributeValue { M = metadataMap };
    }
    
    // Complex object map
    if (typedEntity.Attributes != null)
    {
        var attributesMap = new Dictionary<string, AttributeValue>();
        if (!string.IsNullOrEmpty(typedEntity.Attributes.Color))
            attributesMap["color"] = new AttributeValue { S = typedEntity.Attributes.Color };
        if (typedEntity.Attributes.Size.HasValue)
            attributesMap["size"] = new AttributeValue { N = typedEntity.Attributes.Size.Value.ToString() };
        
        if (attributesMap.Count > 0)
            item["attributes"] = new AttributeValue { M = attributesMap };
    }
    
    return item;
}

// Generated FromDynamoDb
public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item)
    where TSelf : IDynamoDbEntity
{
    var entity = new Product();
    
    if (item.TryGetValue("pk", out var pkValue))
        entity.Id = pkValue.S;
    
    // Simple dictionary map
    if (item.TryGetValue("metadata", out var metadataValue) && metadataValue.M != null)
    {
        entity.Metadata = metadataValue.M.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.S);
    }
    
    // Complex object map
    if (item.TryGetValue("attributes", out var attributesValue) && attributesValue.M != null)
    {
        entity.Attributes = new ProductAttributes();
        if (attributesValue.M.TryGetValue("color", out var colorValue))
            entity.Attributes.Color = colorValue.S;
        if (attributesValue.M.TryGetValue("size", out var sizeValue))
            entity.Attributes.Size = int.Parse(sizeValue.N);
    }
    
    return (TSelf)(object)entity;
}
```


#### Set Property Example

```csharp
// Entity definition
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags { get; set; }
    
    [DynamoDbAttribute("category_ids")]
    public HashSet<int> CategoryIds { get; set; }
}

// Generated ToDynamoDb
if (typedEntity.Tags != null && typedEntity.Tags.Count > 0)
{
    item["tags"] = new AttributeValue { SS = typedEntity.Tags.ToList() };
}

if (typedEntity.CategoryIds != null && typedEntity.CategoryIds.Count > 0)
{
    item["category_ids"] = new AttributeValue 
    { 
        NS = typedEntity.CategoryIds.Select(id => id.ToString()).ToList() 
    };
}

// Generated FromDynamoDb
if (item.TryGetValue("tags", out var tagsValue) && tagsValue.SS != null)
{
    entity.Tags = new HashSet<string>(tagsValue.SS);
}

if (item.TryGetValue("category_ids", out var categoryIdsValue) && categoryIdsValue.NS != null)
{
    entity.CategoryIds = new HashSet<int>(categoryIdsValue.NS.Select(int.Parse));
}
```

#### List Property Example

```csharp
// Entity definition
[DynamoDbTable("orders")]
public partial class Order
{
    [DynamoDbAttribute("item_ids")]
    public List<string> ItemIds { get; set; }
    
    [DynamoDbAttribute("prices")]
    public List<decimal> Prices { get; set; }
}

// Generated ToDynamoDb
if (typedEntity.ItemIds != null && typedEntity.ItemIds.Count > 0)
{
    item["item_ids"] = new AttributeValue 
    { 
        L = typedEntity.ItemIds.Select(id => new AttributeValue { S = id }).ToList() 
    };
}

if (typedEntity.Prices != null && typedEntity.Prices.Count > 0)
{
    item["prices"] = new AttributeValue 
    { 
        L = typedEntity.Prices.Select(p => new AttributeValue { N = p.ToString() }).ToList() 
    };
}

// Generated FromDynamoDb
if (item.TryGetValue("item_ids", out var itemIdsValue) && itemIdsValue.L != null)
{
    entity.ItemIds = itemIdsValue.L.Select(av => av.S).ToList();
}

if (item.TryGetValue("prices", out var pricesValue) && pricesValue.L != null)
{
    entity.Prices = pricesValue.L.Select(av => decimal.Parse(av.N)).ToList();
}
```


#### TTL Property Example

```csharp
// Entity definition
[DynamoDbTable("sessions")]
public partial class Session
{
    [DynamoDbAttribute("session_id")]
    public string SessionId { get; set; }
    
    [DynamoDbAttribute("ttl")]
    [TimeToLive]
    public DateTime? ExpiresAt { get; set; }
}

// Generated ToDynamoDb
if (typedEntity.ExpiresAt.HasValue)
{
    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var seconds = (long)(typedEntity.ExpiresAt.Value.ToUniversalTime() - epoch).TotalSeconds;
    item["ttl"] = new AttributeValue { N = seconds.ToString() };
}

// Generated FromDynamoDb
if (item.TryGetValue("ttl", out var ttlValue) && ttlValue.N != null)
{
    var seconds = long.Parse(ttlValue.N);
    var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    entity.ExpiresAt = epoch.AddSeconds(seconds);
}
```

#### JSON Blob Property Example

```csharp
// Entity definition
[assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]

[DynamoDbTable("documents")]
public partial class Document
{
    [DynamoDbAttribute("doc_id")]
    public string DocumentId { get; set; }
    
    [DynamoDbAttribute("content")]
    [JsonBlob]
    public DocumentContent Content { get; set; }
}

// Generated ToDynamoDb (System.Text.Json - AOT-compatible)
if (typedEntity.Content != null)
{
    var json = System.Text.Json.JsonSerializer.Serialize(
        typedEntity.Content, 
        DocumentJsonContext.Default.DocumentContent);
    item["content"] = new AttributeValue { S = json };
}

// Generated FromDynamoDb (System.Text.Json - AOT-compatible)
if (item.TryGetValue("content", out var contentValue) && contentValue.S != null)
{
    entity.Content = System.Text.Json.JsonSerializer.Deserialize(
        contentValue.S,
        DocumentJsonContext.Default.DocumentContent);
}

// Generated JsonSerializerContext for AOT compatibility
[JsonSerializable(typeof(DocumentContent))]
internal partial class DocumentJsonContext : JsonSerializerContext
{
}

// Alternative: Generated code for Newtonsoft.Json (runtime serialization)
// [assembly: DynamoDbJsonSerializer(JsonSerializerType.NewtonsoftJson)]
// 
// if (typedEntity.Content != null)
// {
//     var json = Newtonsoft.Json.JsonConvert.SerializeObject(typedEntity.Content);
//     item["content"] = new AttributeValue { S = json };
// }
// 
// Note: Newtonsoft.Json uses runtime reflection and has limited AOT support
```


#### Blob Reference Property Example

```csharp
// Entity definition
[DynamoDbTable("files")]
public partial class FileMetadata
{
    [DynamoDbAttribute("file_id")]
    public string FileId { get; set; }
    
    [DynamoDbAttribute("data_ref")]
    [BlobReference(BlobProvider.S3, BucketName = "my-files")]
    public byte[] Data { get; set; }
}

// Generated ToDynamoDb
public static async Task<Dictionary<string, AttributeValue>> ToDynamoDbAsync<TSelf>(
    TSelf entity,
    IBlobStorageProvider blobProvider,
    CancellationToken cancellationToken = default)
    where TSelf : IDynamoDbEntity
{
    var typedEntity = (FileMetadata)(object)entity;
    var item = new Dictionary<string, AttributeValue>();
    
    item["file_id"] = new AttributeValue { S = typedEntity.FileId };
    
    // Store blob and save reference
    if (typedEntity.Data != null && typedEntity.Data.Length > 0)
    {
        using var stream = new MemoryStream(typedEntity.Data);
        var reference = await blobProvider.StoreAsync(
            stream, 
            $"files/{typedEntity.FileId}",
            cancellationToken);
        item["data_ref"] = new AttributeValue { S = reference };
    }
    
    return item;
}

// Generated FromDynamoDb
public static async Task<TSelf> FromDynamoDbAsync<TSelf>(
    Dictionary<string, AttributeValue> item,
    IBlobStorageProvider blobProvider,
    CancellationToken cancellationToken = default)
    where TSelf : IDynamoDbEntity
{
    var entity = new FileMetadata();
    
    if (item.TryGetValue("file_id", out var fileIdValue))
        entity.FileId = fileIdValue.S;
    
    // Retrieve blob using reference
    if (item.TryGetValue("data_ref", out var dataRefValue) && dataRefValue.S != null)
    {
        try
        {
            using var stream = await blobProvider.RetrieveAsync(dataRefValue.S, cancellationToken);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            entity.Data = memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            throw new DynamoDbMappingException(
                $"Failed to retrieve blob reference '{dataRefValue.S}' for property 'Data'", ex);
        }
    }
    
    return (TSelf)(object)entity;
}
```


#### Combined JSON Blob + Blob Reference Example

```csharp
// Entity definition
[DynamoDbTable("documents")]
public partial class LargeDocument
{
    [DynamoDbAttribute("doc_id")]
    public string DocumentId { get; set; }
    
    [DynamoDbAttribute("content_ref")]
    [JsonBlob]
    [BlobReference(BlobProvider.S3, BucketName = "large-docs")]
    public ComplexContent Content { get; set; }
}

// Generated ToDynamoDb
if (typedEntity.Content != null)
{
    // Serialize to JSON
    var json = System.Text.Json.JsonSerializer.Serialize(
        typedEntity.Content,
        LargeDocumentJsonContext.Default.ComplexContent);
    
    // Store JSON as blob
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
    var reference = await blobProvider.StoreAsync(
        stream,
        $"documents/{typedEntity.DocumentId}/content",
        cancellationToken);
    
    item["content_ref"] = new AttributeValue { S = reference };
}

// Generated FromDynamoDb
if (item.TryGetValue("content_ref", out var contentRefValue) && contentRefValue.S != null)
{
    // Retrieve blob
    using var stream = await blobProvider.RetrieveAsync(contentRefValue.S, cancellationToken);
    using var reader = new StreamReader(stream);
    var json = await reader.ReadToEndAsync();
    
    // Deserialize from JSON
    entity.Content = System.Text.Json.JsonSerializer.Deserialize(
        json,
        LargeDocumentJsonContext.Default.ComplexContent);
}
```

## Data Models

### Enhanced PropertyModel

```csharp
public class PropertyModel
{
    // Existing properties...
    public string PropertyName { get; set; }
    public string AttributeName { get; set; }
    public string PropertyType { get; set; }
    
    // New: Advanced type information
    public AdvancedTypeInfo AdvancedType { get; set; }
}

public class AdvancedTypeInfo
{
    public bool IsMap { get; set; }
    public bool IsSet { get; set; }
    public bool IsList { get; set; }
    public bool IsTtl { get; set; }
    public bool IsJsonBlob { get; set; }
    public bool IsBlobReference { get; set; }
    
    // Type-specific details
    public string ElementType { get; set; } // For collections
    public string JsonSerializerType { get; set; } // SystemTextJson or NewtonsoftJson
    public BlobProviderConfig BlobProvider { get; set; }
}

public class BlobProviderConfig
{
    public string ProviderType { get; set; } // S3, Custom
    public string BucketName { get; set; }
    public string KeyPrefix { get; set; }
    public string CustomProviderTypeName { get; set; }
}
```


## Optional Package Implementations

### System.Text.Json Package

```csharp
// Oproto.FluentDynamoDb.SystemTextJson/SystemTextJsonSerializer.cs
namespace Oproto.FluentDynamoDb.SystemTextJson;

public static class SystemTextJsonSerializer
{
    public static string Serialize<T>(T value, JsonSerializerContext context)
    {
        return System.Text.Json.JsonSerializer.Serialize(value, typeof(T), context);
    }
    
    public static T Deserialize<T>(string json, JsonSerializerContext context)
    {
        return (T)System.Text.Json.JsonSerializer.Deserialize(json, typeof(T), context);
    }
}
```

### Newtonsoft.Json Package

```csharp
// Oproto.FluentDynamoDb.NewtonsoftJson/NewtonsoftJsonSerializer.cs
namespace Oproto.FluentDynamoDb.NewtonsoftJson;

/// <summary>
/// Newtonsoft.Json serializer implementation.
/// Note: This uses runtime serialization and has limited AOT compatibility.
/// For full AOT support, use Oproto.FluentDynamoDb.SystemTextJson instead.
/// </summary>
public static class NewtonsoftJsonSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        TypeNameHandling = TypeNameHandling.None, // Avoid reflection-based type handling
        ContractResolver = new DefaultContractResolver(),
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore
    };
    
    public static string Serialize<T>(T value)
    {
        // Uses runtime reflection - not ideal for AOT but works
        return JsonConvert.SerializeObject(value, Settings);
    }
    
    public static T Deserialize<T>(string json)
    {
        // Uses runtime reflection - not ideal for AOT but works
        return JsonConvert.DeserializeObject<T>(json, Settings);
    }
}
```

### S3 Blob Provider Package

```csharp
// Oproto.FluentDynamoDb.BlobStorage.S3/S3BlobProvider.cs
namespace Oproto.FluentDynamoDb.BlobStorage.S3;

public class S3BlobProvider : IBlobStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _keyPrefix;
    
    public S3BlobProvider(IAmazonS3 s3Client, string bucketName, string? keyPrefix = null)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
        _keyPrefix = keyPrefix ?? string.Empty;
    }
    
    public async Task<string> StoreAsync(
        Stream data, 
        string? suggestedKey = null,
        CancellationToken cancellationToken = default)
    {
        var key = suggestedKey ?? Guid.NewGuid().ToString();
        var fullKey = string.IsNullOrEmpty(_keyPrefix) ? key : $"{_keyPrefix}/{key}";
        
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = fullKey,
            InputStream = data
        };
        
        await _s3Client.PutObjectAsync(request, cancellationToken);
        return fullKey;
    }
    
    public async Task<Stream> RetrieveAsync(
        string referenceKey, 
        CancellationToken cancellationToken = default)
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = referenceKey
        };
        
        var response = await _s3Client.GetObjectAsync(request, cancellationToken);
        return response.ResponseStream;
    }
    
    public async Task DeleteAsync(
        string referenceKey, 
        CancellationToken cancellationToken = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = referenceKey
        };
        
        await _s3Client.DeleteObjectAsync(request, cancellationToken);
    }
    
    public async Task<bool> ExistsAsync(
        string referenceKey, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = referenceKey
            };
            
            await _s3Client.GetObjectMetadataAsync(request, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
```


## Error Handling

### Compilation Errors

```csharp
public static class AdvancedTypeDiagnostics
{
    public static readonly DiagnosticDescriptor InvalidTtlType = new(
        "DYNDB101",
        "Invalid TTL property type",
        "[TimeToLive] can only be used on DateTime or DateTimeOffset properties. Property '{0}' is type '{1}'",
        "DynamoDb.AdvancedTypes",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor MissingJsonSerializer = new(
        "DYNDB102",
        "Missing JSON serializer package",
        "[JsonBlob] requires referencing either Oproto.FluentDynamoDb.SystemTextJson or Oproto.FluentDynamoDb.NewtonsoftJson",
        "DynamoDb.AdvancedTypes",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor MissingBlobProvider = new(
        "DYNDB103",
        "Missing blob provider package",
        "[BlobReference] requires referencing a blob provider package like Oproto.FluentDynamoDb.BlobStorage.S3",
        "DynamoDb.AdvancedTypes",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor IncompatibleAttributes = new(
        "DYNDB104",
        "Incompatible attribute combination",
        "[TimeToLive] cannot be combined with [JsonBlob] or [BlobReference] on property '{0}'",
        "DynamoDb.AdvancedTypes",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor MultipleTtlFields = new(
        "DYNDB105",
        "Multiple TTL fields",
        "Entity '{0}' has multiple [TimeToLive] properties. Only one TTL field is allowed per entity",
        "DynamoDb.AdvancedTypes",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor UnsupportedCollectionType = new(
        "DYNDB106",
        "Unsupported collection type",
        "Collection type '{0}' is not supported. Use Dictionary<string, T>, HashSet<T>, or List<T>",
        "DynamoDb.AdvancedTypes",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
```

### Runtime Error Handling

```csharp
public class DynamoDbMappingException : Exception
{
    public string PropertyName { get; }
    public string EntityType { get; }
    
    public DynamoDbMappingException(string message, Exception innerException = null)
        : base(message, innerException)
    {
    }
    
    public DynamoDbMappingException(
        string propertyName, 
        string entityType, 
        string message, 
        Exception innerException = null)
        : base($"Failed to map property '{propertyName}' on entity '{entityType}': {message}", innerException)
    {
        PropertyName = propertyName;
        EntityType = entityType;
    }
}

// Generated error handling example
try
{
    if (item.TryGetValue("tags", out var tagsValue) && tagsValue.SS != null)
    {
        entity.Tags = new HashSet<string>(tagsValue.SS);
    }
}
catch (Exception ex)
{
    throw new DynamoDbMappingException(
        "Tags",
        "Product",
        $"Failed to convert DynamoDB String Set to HashSet<string>",
        ex);
}
```


## Testing Strategy

### Unit Tests

#### Collection Type Tests
```csharp
[Fact]
public void Map_WithNonEmptyDictionary_GeneratesCorrectAttributeValue()
{
    var dict = new Dictionary<string, string>
    {
        ["key1"] = "value1",
        ["key2"] = "value2"
    };
    
    var av = AttributeValueConverter.ToMap(dict);
    
    av.Should().NotBeNull();
    av.M.Should().HaveCount(2);
    av.M["key1"].S.Should().Be("value1");
}

[Fact]
public void Map_WithEmptyDictionary_ReturnsNull()
{
    var dict = new Dictionary<string, string>();
    
    var av = AttributeValueConverter.ToMap(dict);
    
    av.Should().BeNull();
}

[Fact]
public void StringSet_WithNonEmptyHashSet_GeneratesCorrectAttributeValue()
{
    var set = new HashSet<string> { "tag1", "tag2", "tag3" };
    
    var av = AttributeValueConverter.ToStringSet(set);
    
    av.Should().NotBeNull();
    av.SS.Should().HaveCount(3);
    av.SS.Should().Contain("tag1");
}

[Fact]
public void FormatString_WithEmptyCollection_ThrowsArgumentException()
{
    var builder = new QueryRequestBuilder(client, "test-table");
    var emptySet = new HashSet<string>();
    
    var act = () => builder.Where("tags = {0}", emptySet);
    
    act.Should().Throw<ArgumentException>()
        .WithMessage("*empty collection*")
        .WithMessage("*DynamoDB does not support empty*");
}
```

#### TTL Conversion Tests
```csharp
[Fact]
public void Ttl_WithDateTime_ConvertsToUnixEpoch()
{
    var dateTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    
    var av = AttributeValueConverter.ToTtl(dateTime);
    
    av.Should().NotBeNull();
    av.N.Should().Be("1704067200"); // Unix timestamp for 2024-01-01
}

[Fact]
public void Ttl_WithNull_ReturnsNull()
{
    DateTime? dateTime = null;
    
    var av = AttributeValueConverter.ToTtl(dateTime);
    
    av.Should().BeNull();
}

[Fact]
public void Ttl_RoundTrip_PreservesValue()
{
    var original = new DateTime(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc);
    
    var av = AttributeValueConverter.ToTtl(original);
    var restored = AttributeValueConverter.FromTtl(av);
    
    restored.Should().BeCloseTo(original, TimeSpan.FromSeconds(1));
}
```


#### JSON Blob Tests
```csharp
[Fact]
public void JsonBlob_SystemTextJson_SerializesAndDeserializes()
{
    var content = new DocumentContent
    {
        Title = "Test",
        Body = "Content",
        Metadata = new Dictionary<string, string> { ["key"] = "value" }
    };
    
    var json = SystemTextJsonSerializer.Serialize(content, TestJsonContext.Default.DocumentContent);
    var restored = SystemTextJsonSerializer.Deserialize<DocumentContent>(json, TestJsonContext.Default.DocumentContent);
    
    restored.Title.Should().Be("Test");
    restored.Body.Should().Be("Content");
    restored.Metadata.Should().ContainKey("key");
}

[Fact]
public void JsonBlob_NewtonsoftJson_SerializesAndDeserializes()
{
    var content = new DocumentContent
    {
        Title = "Test",
        Body = "Content"
    };
    
    var json = NewtonsoftJsonSerializer.Serialize(content);
    var restored = NewtonsoftJsonSerializer.Deserialize<DocumentContent>(json);
    
    restored.Title.Should().Be("Test");
    restored.Body.Should().Be("Content");
}
```

#### Blob Provider Tests
```csharp
[Fact]
public async Task S3BlobProvider_StoreAndRetrieve_WorksCorrectly()
{
    var s3Client = Substitute.For<IAmazonS3>();
    var provider = new S3BlobProvider(s3Client, "test-bucket", "prefix");
    
    var data = Encoding.UTF8.GetBytes("test data");
    using var stream = new MemoryStream(data);
    
    var reference = await provider.StoreAsync(stream, "test-key");
    
    reference.Should().Be("prefix/test-key");
    await s3Client.Received(1).PutObjectAsync(
        Arg.Is<PutObjectRequest>(r => r.BucketName == "test-bucket" && r.Key == "prefix/test-key"),
        Arg.Any<CancellationToken>());
}

[Fact]
public async Task S3BlobProvider_Delete_CallsS3Delete()
{
    var s3Client = Substitute.For<IAmazonS3>();
    var provider = new S3BlobProvider(s3Client, "test-bucket");
    
    await provider.DeleteAsync("test-key");
    
    await s3Client.Received(1).DeleteObjectAsync(
        Arg.Is<DeleteObjectRequest>(r => r.BucketName == "test-bucket" && r.Key == "test-key"),
        Arg.Any<CancellationToken>());
}
```

### Source Generator Tests

```csharp
[Fact]
public void SourceGenerator_MapProperty_GeneratesCorrectCode()
{
    var source = @"
        [DynamoDbTable(""test"")]
        public partial class TestEntity
        {
            [DynamoDbAttribute(""metadata"")]
            public Dictionary<string, string> Metadata { get; set; }
        }";
    
    var result = GenerateCode(source);
    
    result.Diagnostics.Should().BeEmpty();
    var generatedCode = result.GeneratedSources[0].SourceText.ToString();
    generatedCode.Should().Contain("if (typedEntity.Metadata != null && typedEntity.Metadata.Count > 0)");
    generatedCode.Should().Contain("new AttributeValue { M = metadataMap }");
}

[Fact]
public void SourceGenerator_TtlProperty_GeneratesEpochConversion()
{
    var source = @"
        [DynamoDbTable(""test"")]
        public partial class TestEntity
        {
            [DynamoDbAttribute(""ttl"")]
            [TimeToLive]
            public DateTime? ExpiresAt { get; set; }
        }";
    
    var result = GenerateCode(source);
    
    result.Diagnostics.Should().BeEmpty();
    var generatedCode = result.GeneratedSources[0].SourceText.ToString();
    generatedCode.Should().Contain("ToUniversalTime()");
    generatedCode.Should().Contain("TotalSeconds");
}

[Fact]
public void SourceGenerator_MultipleTtlFields_GeneratesError()
{
    var source = @"
        [DynamoDbTable(""test"")]
        public partial class TestEntity
        {
            [TimeToLive]
            public DateTime? ExpiresAt { get; set; }
            
            [TimeToLive]
            public DateTime? DeletedAt { get; set; }
        }";
    
    var result = GenerateCode(source);
    
    result.Diagnostics.Should().ContainSingle(d => d.Id == "DYNDB105");
}
```


### Integration Tests

```csharp
[Fact]
public async Task EndToEnd_MapProperty_RoundTrip()
{
    // Arrange
    var entity = new Product
    {
        Id = "prod-123",
        Metadata = new Dictionary<string, string>
        {
            ["color"] = "blue",
            ["size"] = "large"
        }
    };
    
    // Act - Save
    await table.Put
        .WithItem(entity)
        .ExecuteAsync();
    
    // Act - Load
    var loaded = await table.Get
        .WithKey("pk", "prod-123")
        .ExecuteAsync<Product>();
    
    // Assert
    loaded.Item.Should().NotBeNull();
    loaded.Item.Metadata.Should().HaveCount(2);
    loaded.Item.Metadata["color"].Should().Be("blue");
}

[Fact]
public async Task EndToEnd_TtlProperty_StoresCorrectly()
{
    // Arrange
    var expiresAt = DateTime.UtcNow.AddDays(7);
    var entity = new Session
    {
        SessionId = "sess-123",
        ExpiresAt = expiresAt
    };
    
    // Act
    await table.Put
        .WithItem(entity)
        .ExecuteAsync();
    
    var loaded = await table.Get
        .WithKey("session_id", "sess-123")
        .ExecuteAsync<Session>();
    
    // Assert
    loaded.Item.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
}

[Fact]
public async Task EndToEnd_EmptyCollection_OmitsAttribute()
{
    // Arrange
    var entity = new Product
    {
        Id = "prod-123",
        Tags = new HashSet<string>() // Empty
    };
    
    // Act
    await table.Put
        .WithItem(entity)
        .ExecuteAsync();
    
    // Get raw item to verify attribute is omitted
    var response = await dynamoDbClient.GetItemAsync(new GetItemRequest
    {
        TableName = "products",
        Key = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "prod-123" }
        }
    });
    
    // Assert
    response.Item.Should().NotContainKey("tags");
}

[Fact]
public async Task EndToEnd_BlobReference_StoresInS3()
{
    // Arrange
    var s3Client = new AmazonS3Client();
    var blobProvider = new S3BlobProvider(s3Client, "test-bucket");
    var entity = new FileMetadata
    {
        FileId = "file-123",
        Data = Encoding.UTF8.GetBytes("test file content")
    };
    
    // Act
    var item = await FileMetadata.ToDynamoDbAsync(entity, blobProvider);
    await dynamoDbClient.PutItemAsync(new PutItemRequest
    {
        TableName = "files",
        Item = item
    });
    
    // Assert - Verify reference stored in DynamoDB
    item["data_ref"].S.Should().StartWith("files/file-123");
    
    // Assert - Verify data stored in S3
    var s3Object = await s3Client.GetObjectAsync("test-bucket", item["data_ref"].S);
    using var reader = new StreamReader(s3Object.ResponseStream);
    var content = await reader.ReadToEndAsync();
    content.Should().Be("test file content");
}
```

## Performance Considerations

### Memory Efficiency

1. **Collection Conversion**: Generated code creates new collections only when necessary
2. **Streaming Blobs**: Blob providers support streaming to avoid loading large data into memory
3. **JSON Serialization**: Reuse serializer contexts and settings across operations

### Optimization Strategies

```csharp
// Efficient map conversion - single pass
var metadataMap = new Dictionary<string, AttributeValue>(typedEntity.Metadata.Count);
foreach (var kvp in typedEntity.Metadata)
{
    metadataMap[kvp.Key] = new AttributeValue { S = kvp.Value };
}

// Efficient set conversion - direct list creation
item["tags"] = new AttributeValue { SS = typedEntity.Tags.ToList() };

// Efficient list conversion with pre-sized collection
var itemIds = new List<AttributeValue>(typedEntity.ItemIds.Count);
foreach (var id in typedEntity.ItemIds)
{
    itemIds.Add(new AttributeValue { S = id });
}
item["item_ids"] = new AttributeValue { L = itemIds };
```

### Blob Storage Optimization

```csharp
// Stream large blobs without loading into memory
public async Task<string> StoreAsync(Stream data, string? suggestedKey = null, CancellationToken ct = default)
{
    // S3 supports streaming directly from input stream
    var request = new PutObjectRequest
    {
        BucketName = _bucketName,
        Key = fullKey,
        InputStream = data, // No intermediate buffer
        AutoCloseStream = false // Caller manages stream
    };
    
    await _s3Client.PutObjectAsync(request, ct);
    return fullKey;
}
```

## Migration Guide

### Adding Advanced Types to Existing Entities

```csharp
// Before
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string Id { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
}

// After - Add collections
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string Id { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
    
    // New: Add tags as set
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags { get; set; }
    
    // New: Add metadata as map
    [DynamoDbAttribute("metadata")]
    public Dictionary<string, string> Metadata { get; set; }
    
    // New: Add TTL
    [DynamoDbAttribute("ttl")]
    [TimeToLive]
    public DateTime? ExpiresAt { get; set; }
}
```

### Using Format Strings with Advanced Types

```csharp
// Before - Manual parameter handling
builder
    .Where("tags = :tags")
    .WithValue(":tags", new AttributeValue { SS = tags.ToList() });

// After - Format string with automatic conversion
builder.Where("tags = {0}", tags);

// Update expressions
builder.Set("SET metadata = {0}, tags = {1}, expires_at = {2}",
    metadataDict,
    tagsSet,
    expiryDateTime);
```

This design provides a comprehensive foundation for advanced type support while maintaining the library's core principles of AOT compatibility, zero reflection, and clean API design.
