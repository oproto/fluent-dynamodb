# JSON Serializer Refactor Design

## Architecture Overview

This design refactors JSON serialization for `[JsonBlob]` properties from compile-time assembly attributes to runtime configuration via `FluentDynamoDbOptions`.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         User Code                                        │
├─────────────────────────────────────────────────────────────────────────┤
│  var options = new FluentDynamoDbOptions()                              │
│      .WithSystemTextJson(new JsonSerializerOptions { ... });            │
│  var table = new MyTable(client, "TableName", options);                 │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    FluentDynamoDbOptions                                 │
├─────────────────────────────────────────────────────────────────────────┤
│  + Logger: IDynamoDbLogger                                              │
│  + BlobStorageProvider: IBlobStorageProvider?                           │
│  + FieldEncryptor: IFieldEncryptor?                                     │
│  + GeospatialProvider: IGeospatialProvider?                             │
│  + JsonSerializer: IJsonBlobSerializer?  ◄── NEW                        │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    IJsonBlobSerializer (Core)                            │
├─────────────────────────────────────────────────────────────────────────┤
│  + Serialize<T>(T value): string                                        │
│  + Deserialize<T>(string json): T?                                      │
└─────────────────────────────────────────────────────────────────────────┘
                    ▲                               ▲
                    │                               │
    ┌───────────────┴───────────────┐   ┌──────────┴──────────────────┐
    │  SystemTextJsonBlobSerializer │   │  NewtonsoftJsonBlobSerializer│
    │  (SystemTextJson Package)     │   │  (NewtonsoftJson Package)    │
    ├───────────────────────────────┤   ├─────────────────────────────┤
    │  - _options: JsonSerializerOpt│   │  - _settings: JsonSerializer│
    │  - _context: JsonSerializerCtx│   │                Settings     │
    │  + Serialize<T>()             │   │  + Serialize<T>()           │
    │  + Deserialize<T>()           │   │  + Deserialize<T>()         │
    └───────────────────────────────┘   └─────────────────────────────┘
```

## Component Design

### 1. IJsonBlobSerializer Interface (Core Library)

**File:** `Oproto.FluentDynamoDb/Storage/IJsonBlobSerializer.cs`

```csharp
namespace Oproto.FluentDynamoDb.Storage;

/// <summary>
/// Interface for JSON serialization of [JsonBlob] properties.
/// Implementations are provided by Oproto.FluentDynamoDb.SystemTextJson 
/// and Oproto.FluentDynamoDb.NewtonsoftJson packages.
/// </summary>
public interface IJsonBlobSerializer
{
    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="value">The object to serialize.</param>
    /// <returns>JSON string representation.</returns>
    string Serialize<T>(T value);
    
    /// <summary>
    /// Deserializes a JSON string to an object.
    /// </summary>
    /// <typeparam name="T">The type of object to deserialize.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized object, or null if json is null/empty.</returns>
    T? Deserialize<T>(string json);
}
```

### 2. FluentDynamoDbOptions Updates

**File:** `Oproto.FluentDynamoDb/FluentDynamoDbOptions.cs`

Add new property and builder method:

```csharp
/// <summary>
/// Gets the JSON serializer for [JsonBlob] properties.
/// Null if JSON blob serialization is not configured.
/// Configure using .WithSystemTextJson() or .WithNewtonsoftJson() extension methods.
/// </summary>
public IJsonBlobSerializer? JsonSerializer { get; private init; }

/// <summary>
/// Creates a new options instance with the specified JSON serializer.
/// </summary>
/// <param name="serializer">The JSON serializer to use for [JsonBlob] properties.</param>
/// <returns>A new FluentDynamoDbOptions instance with the specified JSON serializer.</returns>
public FluentDynamoDbOptions WithJsonSerializer(IJsonBlobSerializer? serializer)
    => new() 
    { 
        Logger = Logger,
        GeospatialProvider = GeospatialProvider,
        BlobStorageProvider = BlobStorageProvider,
        FieldEncryptor = FieldEncryptor,
        HydratorRegistry = HydratorRegistry,
        JsonSerializer = serializer
    };
```

### 3. IDynamoDbEntity Interface Changes

**File:** `Oproto.FluentDynamoDb/Storage/IDynamoDbEntity.cs`

Change method signatures from `IDynamoDbLogger?` to `FluentDynamoDbOptions?`:

```csharp
public interface IDynamoDbEntity : IEntityMetadataProvider
{
    /// <summary>
    /// Converts an entity instance to a DynamoDB AttributeValue dictionary.
    /// </summary>
    static abstract Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(
        TSelf entity, 
        FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity;

    /// <summary>
    /// Creates an entity instance from a single DynamoDB item.
    /// </summary>
    static abstract TSelf FromDynamoDb<TSelf>(
        Dictionary<string, AttributeValue> item, 
        FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity;

    /// <summary>
    /// Creates an entity instance from multiple DynamoDB items.
    /// </summary>
    static abstract TSelf FromDynamoDb<TSelf>(
        IList<Dictionary<string, AttributeValue>> items, 
        FluentDynamoDbOptions? options = null) where TSelf : IDynamoDbEntity;

    // ... other methods unchanged
}
```

### 4. SystemTextJson Package Implementation

**File:** `Oproto.FluentDynamoDb.SystemTextJson/SystemTextJsonBlobSerializer.cs`

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.SystemTextJson;

/// <summary>
/// System.Text.Json implementation of IJsonBlobSerializer.
/// Supports both reflection-based and AOT-compatible serialization.
/// </summary>
public sealed class SystemTextJsonBlobSerializer : IJsonBlobSerializer
{
    private readonly JsonSerializerOptions? _options;
    private readonly JsonSerializerContext? _context;

    /// <summary>
    /// Creates a serializer with default options.
    /// </summary>
    public SystemTextJsonBlobSerializer()
    {
        _options = null;
        _context = null;
    }

    /// <summary>
    /// Creates a serializer with custom options.
    /// </summary>
    /// <param name="options">The JsonSerializerOptions to use.</param>
    public SystemTextJsonBlobSerializer(JsonSerializerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _context = null;
    }

    /// <summary>
    /// Creates an AOT-compatible serializer with a JsonSerializerContext.
    /// </summary>
    /// <param name="context">The JsonSerializerContext for AOT serialization.</param>
    public SystemTextJsonBlobSerializer(JsonSerializerContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = null;
    }

    /// <inheritdoc />
    public string Serialize<T>(T value)
    {
        if (_context != null)
        {
            return JsonSerializer.Serialize(value, typeof(T), _context);
        }
        
        return JsonSerializer.Serialize(value, _options);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(string json)
    {
        if (string.IsNullOrEmpty(json))
            return default;

        if (_context != null)
        {
            return (T?)JsonSerializer.Deserialize(json, typeof(T), _context);
        }
        
        return JsonSerializer.Deserialize<T>(json, _options);
    }
}
```

**File:** `Oproto.FluentDynamoDb.SystemTextJson/SystemTextJsonOptionsExtensions.cs`

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Oproto.FluentDynamoDb;

namespace Oproto.FluentDynamoDb.SystemTextJson;

/// <summary>
/// Extension methods for configuring System.Text.Json serialization.
/// </summary>
public static class SystemTextJsonOptionsExtensions
{
    /// <summary>
    /// Configures System.Text.Json for [JsonBlob] properties with default options.
    /// </summary>
    public static FluentDynamoDbOptions WithSystemTextJson(this FluentDynamoDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.WithJsonSerializer(new SystemTextJsonBlobSerializer());
    }

    /// <summary>
    /// Configures System.Text.Json for [JsonBlob] properties with custom options.
    /// </summary>
    public static FluentDynamoDbOptions WithSystemTextJson(
        this FluentDynamoDbOptions options, 
        JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serializerOptions);
        return options.WithJsonSerializer(new SystemTextJsonBlobSerializer(serializerOptions));
    }

    /// <summary>
    /// Configures System.Text.Json for [JsonBlob] properties with AOT-compatible context.
    /// </summary>
    public static FluentDynamoDbOptions WithSystemTextJson(
        this FluentDynamoDbOptions options, 
        JsonSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);
        return options.WithJsonSerializer(new SystemTextJsonBlobSerializer(context));
    }
}
```

### 5. NewtonsoftJson Package Implementation

**File:** `Oproto.FluentDynamoDb.NewtonsoftJson/NewtonsoftJsonBlobSerializer.cs`

```csharp
using Newtonsoft.Json;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.NewtonsoftJson;

/// <summary>
/// Newtonsoft.Json implementation of IJsonBlobSerializer.
/// </summary>
public sealed class NewtonsoftJsonBlobSerializer : IJsonBlobSerializer
{
    private readonly JsonSerializerSettings _settings;

    /// <summary>
    /// Default settings optimized for DynamoDB storage.
    /// </summary>
    private static readonly JsonSerializerSettings DefaultSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        NullValueHandling = NullValueHandling.Ignore,
        DateFormatHandling = DateFormatHandling.IsoDateFormat,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore
    };

    /// <summary>
    /// Creates a serializer with default settings.
    /// </summary>
    public NewtonsoftJsonBlobSerializer()
    {
        _settings = DefaultSettings;
    }

    /// <summary>
    /// Creates a serializer with custom settings.
    /// </summary>
    /// <param name="settings">The JsonSerializerSettings to use.</param>
    public NewtonsoftJsonBlobSerializer(JsonSerializerSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public string Serialize<T>(T value)
    {
        return JsonConvert.SerializeObject(value, _settings);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(string json)
    {
        if (string.IsNullOrEmpty(json))
            return default;

        return JsonConvert.DeserializeObject<T>(json, _settings);
    }
}
```

**File:** `Oproto.FluentDynamoDb.NewtonsoftJson/NewtonsoftJsonOptionsExtensions.cs`

```csharp
using Newtonsoft.Json;
using Oproto.FluentDynamoDb;

namespace Oproto.FluentDynamoDb.NewtonsoftJson;

/// <summary>
/// Extension methods for configuring Newtonsoft.Json serialization.
/// </summary>
public static class NewtonsoftJsonOptionsExtensions
{
    /// <summary>
    /// Configures Newtonsoft.Json for [JsonBlob] properties with default settings.
    /// </summary>
    public static FluentDynamoDbOptions WithNewtonsoftJson(this FluentDynamoDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.WithJsonSerializer(new NewtonsoftJsonBlobSerializer());
    }

    /// <summary>
    /// Configures Newtonsoft.Json for [JsonBlob] properties with custom settings.
    /// </summary>
    public static FluentDynamoDbOptions WithNewtonsoftJson(
        this FluentDynamoDbOptions options, 
        JsonSerializerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);
        return options.WithJsonSerializer(new NewtonsoftJsonBlobSerializer(settings));
    }
}
```

### 6. Source Generator Changes

**File:** `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`

Update `GenerateJsonBlobPropertyToAttributeValue` to use options:

```csharp
private static void GenerateJsonBlobPropertyToAttributeValue(StringBuilder sb, PropertyModel property, EntityModel entity)
{
    var attributeName = property.AttributeName;
    var propertyName = property.PropertyName;
    var escapedPropertyName = EscapePropertyName(propertyName);

    sb.AppendLine($"            // Serialize JSON blob property {propertyName}");

    if (property.IsNullable)
    {
        sb.AppendLine($"            if (typedEntity.{escapedPropertyName} != null)");
        sb.AppendLine("            {");
        sb.AppendLine("                if (options?.JsonSerializer == null)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    throw new InvalidOperationException(");
        sb.AppendLine($"                        \"Property '{propertyName}' has [JsonBlob] attribute but no JSON serializer is configured. \" +");
        sb.AppendLine($"                        \"Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.\");");
        sb.AppendLine("                }");
        sb.AppendLine($"                var json = options.JsonSerializer.Serialize(typedEntity.{escapedPropertyName});");
        sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue {{ S = json }};");
        sb.AppendLine("            }");
    }
    else
    {
        sb.AppendLine("            if (options?.JsonSerializer == null)");
        sb.AppendLine("            {");
        sb.AppendLine($"                throw new InvalidOperationException(");
        sb.AppendLine($"                    \"Property '{propertyName}' has [JsonBlob] attribute but no JSON serializer is configured. \" +");
        sb.AppendLine($"                    \"Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.\");");
        sb.AppendLine("            }");
        sb.AppendLine($"            var json = options.JsonSerializer.Serialize(typedEntity.{escapedPropertyName});");
        sb.AppendLine($"            item[\"{attributeName}\"] = new AttributeValue {{ S = json }};");
    }
}
```

### 7. Files to Delete

- `Oproto.FluentDynamoDb/Attributes/DynamoDbJsonSerializerAttribute.cs`
- `Oproto.FluentDynamoDb/Attributes/JsonSerializerType.cs`
- `Oproto.FluentDynamoDb.SystemTextJson/SystemTextJsonSerializer.cs` (replaced by new implementation)
- `Oproto.FluentDynamoDb.NewtonsoftJson/NewtonsoftJsonSerializer.cs` (replaced by new implementation)

### 8. Source Generator Diagnostic

Update `JsonSerializerDetector` to emit a warning when `[JsonBlob]` is used without a JSON package:

```csharp
// Diagnostic: FDDB0020 - JsonBlob requires JSON package
public static readonly DiagnosticDescriptor JsonBlobRequiresPackage = new(
    id: "FDDB0020",
    title: "JsonBlob requires JSON serializer package",
    messageFormat: "Property '{0}' has [JsonBlob] attribute but no JSON serializer package is referenced. " +
                   "Add a reference to Oproto.FluentDynamoDb.SystemTextJson or Oproto.FluentDynamoDb.NewtonsoftJson.",
    category: "Usage",
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true);
```

## Migration Impact

### Breaking Changes

1. **Interface signature change**: `ToDynamoDb`/`FromDynamoDb` now take `FluentDynamoDbOptions?` instead of `IDynamoDbLogger?`
2. **Assembly attribute removed**: `[assembly: DynamoDbJsonSerializer]` no longer exists
3. **Runtime configuration required**: `[JsonBlob]` properties now require `WithSystemTextJson()` or `WithNewtonsoftJson()` at runtime

### Migration Steps

1. Remove `[assembly: DynamoDbJsonSerializer(...)]` from code
2. Add `.WithSystemTextJson()` or `.WithNewtonsoftJson()` to `FluentDynamoDbOptions`
3. Update any code that manually calls `ToDynamoDb`/`FromDynamoDb` with the new signature

## Testing Strategy

1. Update all unit tests that call `ToDynamoDb`/`FromDynamoDb` with new signature
2. Add tests for `IJsonBlobSerializer` implementations
3. Add tests for extension methods
4. Add integration tests for JSON blob round-trip with both serializers
5. Add tests for error case (no serializer configured)
