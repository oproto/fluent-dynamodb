# JSON Serializer Refactor Requirements

## Overview

Refactor the JSON serialization system for `[JsonBlob]` properties to use runtime configuration via `FluentDynamoDbOptions` instead of compile-time assembly attributes. This makes the JSON packages (`Oproto.FluentDynamoDb.SystemTextJson` and `Oproto.FluentDynamoDb.NewtonsoftJson`) provide real value by allowing users to configure serializer options.

## Problem Statement

Currently:
1. The source generator inlines direct calls to `System.Text.Json.JsonSerializer` or `Newtonsoft.Json.JsonConvert`
2. The JSON packages contain wrapper classes (`SystemTextJsonSerializer`, `NewtonsoftJsonSerializer`) that are never used
3. Users cannot customize serializer options (camelCase, null handling, etc.)
4. Configuration is via compile-time `[assembly: DynamoDbJsonSerializer]` attribute

## Requirements

### 1. Interface Changes

1.1. GIVEN the `IDynamoDbEntity` interface, WHEN I review the `ToDynamoDb` and `FromDynamoDb` methods, THEN they SHALL accept `FluentDynamoDbOptions?` instead of `IDynamoDbLogger?`

1.2. GIVEN the `FluentDynamoDbOptions` class, WHEN I configure JSON serialization, THEN there SHALL be an `IJsonBlobSerializer? JsonSerializer` property available

### 2. Core Library Changes

2.1. GIVEN the core library, WHEN I need JSON serialization, THEN there SHALL be an `IJsonBlobSerializer` interface with `Serialize<T>` and `Deserialize<T>` methods

2.2. GIVEN a `[JsonBlob]` property without a configured serializer, WHEN `ToDynamoDb` or `FromDynamoDb` is called, THEN a clear runtime exception SHALL be thrown explaining the requirement

### 3. SystemTextJson Package

3.1. GIVEN the `Oproto.FluentDynamoDb.SystemTextJson` package, WHEN I want to use System.Text.Json, THEN there SHALL be a `SystemTextJsonBlobSerializer` class implementing `IJsonBlobSerializer`

3.2. GIVEN the SystemTextJson package, WHEN I configure options, THEN I SHALL be able to pass `JsonSerializerOptions` for customization

3.3. GIVEN the SystemTextJson package for AOT scenarios, WHEN I configure options, THEN I SHALL be able to pass a `JsonSerializerContext` for AOT-compatible serialization

3.4. GIVEN the SystemTextJson package, WHEN I want simple configuration, THEN there SHALL be a `WithSystemTextJson()` extension method on `FluentDynamoDbOptions`

### 4. NewtonsoftJson Package

4.1. GIVEN the `Oproto.FluentDynamoDb.NewtonsoftJson` package, WHEN I want to use Newtonsoft.Json, THEN there SHALL be a `NewtonsoftJsonBlobSerializer` class implementing `IJsonBlobSerializer`

4.2. GIVEN the NewtonsoftJson package, WHEN I configure options, THEN I SHALL be able to pass `JsonSerializerSettings` for customization

4.3. GIVEN the NewtonsoftJson package, WHEN I want simple configuration, THEN there SHALL be a `WithNewtonsoftJson()` extension method on `FluentDynamoDbOptions`

### 5. Source Generator Changes

5.1. GIVEN the source generator, WHEN generating `ToDynamoDb`/`FromDynamoDb` methods, THEN it SHALL generate code that calls `options?.JsonSerializer?.Serialize()` instead of inlining JSON library calls

5.2. GIVEN the source generator, WHEN a `[JsonBlob]` property is detected but no JSON package is referenced, THEN it SHALL emit a diagnostic warning

5.3. GIVEN the source generator, WHEN generating entity implementations, THEN it SHALL pass `FluentDynamoDbOptions?` to the interface methods

### 6. Cleanup

6.1. GIVEN the `[assembly: DynamoDbJsonSerializer]` attribute, WHEN this refactor is complete, THEN it SHALL be deleted (not deprecated - pre-release)

6.2. GIVEN the `JsonSerializerType` enum, WHEN this refactor is complete, THEN it SHALL be deleted

6.3. GIVEN the `JsonSerializerDetector` in the source generator, WHEN this refactor is complete, THEN it SHALL be updated to only detect package references for diagnostics (not for code generation)

### 7. Documentation

7.1. GIVEN the SystemTextJson package README, WHEN this refactor is complete, THEN it SHALL document the correct usage pattern with `WithSystemTextJson()`

7.2. GIVEN the NewtonsoftJson package README, WHEN this refactor is complete, THEN it SHALL document the correct usage pattern with `WithNewtonsoftJson()`

7.3. GIVEN the main documentation, WHEN this refactor is complete, THEN all `[JsonBlob]` examples SHALL show the options-based configuration

7.4. GIVEN the CHANGELOG.md, WHEN this refactor is complete, THEN it SHALL document this as a breaking change with migration guidance

7.5. GIVEN the docs/DOCUMENTATION_CHANGELOG.md, WHEN documentation is updated, THEN it SHALL include entries for all documentation changes

### 8. Testing

8.1. GIVEN the unit tests, WHEN `ToDynamoDb`/`FromDynamoDb` signatures change, THEN all affected tests SHALL be updated

8.2. GIVEN the JSON serializer packages, WHEN the implementations change, THEN their unit tests SHALL be updated to test the new `IJsonBlobSerializer` implementations

## User Experience

### Before (Current - Incorrect)
```csharp
// Assembly attribute (compile-time)
[assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]

// Entity with JsonBlob
[DynamoDbTable("Documents")]
public partial class Document
{
    [JsonBlob]
    [DynamoDbAttribute("content")]
    public DocumentContent Content { get; set; }
}

// Table usage - no way to customize serializer options
var table = new DocumentTable(client, "Documents");
```

### After (New - Correct)
```csharp
// Entity with JsonBlob (no assembly attribute needed)
[DynamoDbTable("Documents")]
public partial class Document
{
    [JsonBlob]
    [DynamoDbAttribute("content")]
    public DocumentContent Content { get; set; }
}

// Configure at runtime with options
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson()  // Uses defaults
    // OR with custom options
    .WithSystemTextJson(new JsonSerializerOptions 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    })
    // OR for AOT
    .WithSystemTextJson(MyJsonContext.Default);

var table = new DocumentTable(client, "Documents", options);
```

## Out of Scope

- Changes to non-JSON blob serialization
- Changes to the `[DynamoDbMap]` attribute behavior
- Changes to encryption or blob storage providers
