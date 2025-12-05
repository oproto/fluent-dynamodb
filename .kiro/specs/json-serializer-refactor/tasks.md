# JSON Serializer Refactor Tasks

## Task 1: Core Library Interface Changes
- [x] Create `IJsonBlobSerializer` interface in `Oproto.FluentDynamoDb/Storage/`
  - [x] Define `Serialize<T>(T value): string` method
  - [x] Define `Deserialize<T>(string json): T?` method
  - [x] Add XML documentation
  - _Requirements: 2.1_

- [x] Update `FluentDynamoDbOptions` class
  - [x] Add `JsonSerializer` property of type `IJsonBlobSerializer?`
  - [x] Add `WithJsonSerializer(IJsonBlobSerializer?)` builder method
  - [x] Update all existing `With*` methods to preserve `JsonSerializer`
  - _Requirements: 1.2_

- [x] Update `IDynamoDbEntity` interface
  - [x] Change `ToDynamoDb` signature from `IDynamoDbLogger?` to `FluentDynamoDbOptions?`
  - [x] Change `FromDynamoDb` (single item) signature from `IDynamoDbLogger?` to `FluentDynamoDbOptions?`
  - [x] Change `FromDynamoDb` (multi item) signature from `IDynamoDbLogger?` to `FluentDynamoDbOptions?`
  - _Requirements: 1.1_

## Task 2: Delete Obsolete Files
- [x] Delete `Oproto.FluentDynamoDb/Attributes/DynamoDbJsonSerializerAttribute.cs`
  - _Requirements: 6.1_

- [x] Delete `Oproto.FluentDynamoDb/Attributes/JsonSerializerType.cs`
  - _Requirements: 6.2_

- [x] Delete `Oproto.FluentDynamoDb.SystemTextJson/SystemTextJsonSerializer.cs`
  - _Requirements: 3.1 (replaced by new implementation)_

- [x] Delete `Oproto.FluentDynamoDb.NewtonsoftJson/NewtonsoftJsonSerializer.cs`
  - _Requirements: 4.1 (replaced by new implementation)_

## Task 3: SystemTextJson Package Implementation
- [x] Create `SystemTextJsonBlobSerializer` class
  - [x] Implement `IJsonBlobSerializer` interface
  - [x] Add constructor with no parameters (default options)
  - [x] Add constructor accepting `JsonSerializerOptions`
  - [x] Add constructor accepting `JsonSerializerContext` for AOT
  - [x] Implement `Serialize<T>` method
  - [x] Implement `Deserialize<T>` method
  - [x] Add XML documentation
  - _Requirements: 3.1, 3.2, 3.3_

- [x] Create `SystemTextJsonOptionsExtensions` class
  - [x] Add `WithSystemTextJson(this FluentDynamoDbOptions)` extension method
  - [x] Add `WithSystemTextJson(this FluentDynamoDbOptions, JsonSerializerOptions)` overload
  - [x] Add `WithSystemTextJson(this FluentDynamoDbOptions, JsonSerializerContext)` overload
  - [x] Add XML documentation
  - _Requirements: 3.4_

- [x] Update `Oproto.FluentDynamoDb.SystemTextJson.csproj`
  - [x] Add reference to core library for `IJsonBlobSerializer` and `FluentDynamoDbOptions`
  - _Requirements: 3.1_

## Task 4: NewtonsoftJson Package Implementation
- [x] Create `NewtonsoftJsonBlobSerializer` class
  - [x] Implement `IJsonBlobSerializer` interface
  - [x] Add constructor with no parameters (default settings)
  - [x] Add constructor accepting `JsonSerializerSettings`
  - [x] Define default settings (TypeNameHandling.None, NullValueHandling.Ignore, etc.)
  - [x] Implement `Serialize<T>` method
  - [x] Implement `Deserialize<T>` method
  - [x] Add XML documentation
  - _Requirements: 4.1, 4.2_

- [x] Create `NewtonsoftJsonOptionsExtensions` class
  - [x] Add `WithNewtonsoftJson(this FluentDynamoDbOptions)` extension method
  - [x] Add `WithNewtonsoftJson(this FluentDynamoDbOptions, JsonSerializerSettings)` overload
  - [x] Add XML documentation
  - _Requirements: 4.3_

- [x] Update `Oproto.FluentDynamoDb.NewtonsoftJson.csproj`
  - [x] Add reference to core library for `IJsonBlobSerializer` and `FluentDynamoDbOptions`
  - _Requirements: 4.1_

## Task 5: Source Generator Updates
- [x] Update `MapperGenerator.cs`
  - [x] Update method signature generation to use `FluentDynamoDbOptions?` instead of `IDynamoDbLogger?`
  - [x] Update `GenerateJsonBlobPropertyToAttributeValue` to call `options?.JsonSerializer?.Serialize()`
  - [x] Update `GenerateJsonBlobPropertyFromAttributeValue` to call `options?.JsonSerializer?.Deserialize()`
  - [x] Add null check with clear exception message when serializer is not configured
  - [x] Update logger access to use `options?.Logger`
  - _Requirements: 5.1, 5.3, 2.2_

- [x] Update `JsonSerializerDetector.cs`
  - [x] Remove code generation logic (no longer needed)
  - [x] Keep package detection for diagnostic purposes only
  - [x] Remove `AssemblyLevelSerializer` detection
  - _Requirements: 6.3_

- [x] Add diagnostic for missing JSON package
  - [x] Use existing `DYNDB102` diagnostic descriptor (MissingJsonSerializer)
  - [x] Emit warning when `[JsonBlob]` is used but no JSON package is referenced (already implemented in AdvancedTypeValidator)
  - _Requirements: 5.2_

- [x] Update all other generators that reference `IDynamoDbLogger` parameter
  - [x] `HydratorGenerator.cs` - updated to pass `options: null` instead of `logger: null`
  - [x] `TableGenerator.cs` - already uses `FluentDynamoDbOptions`
  - [x] `AdvancedTypeAnalyzer.cs` - removed compile-time JSON serializer detection
  - [x] `JsonSerializerContextGenerator.cs` - updated to check package reference directly
  - _Requirements: 5.3_

## Task 6: Update Request Builders and Extensions
- [x] 6 Update all callers of `ToDynamoDb`/`FromDynamoDb` in request builders
  - [x] 6.1 `PutItemRequestBuilder.cs` - pass options instead of logger
  - [x] 6.2 `EnhancedExecuteAsyncExtensions.cs` - pass options instead of logger
  - [x] 6.3 Any other files calling these methods
  - _Requirements: 5.3_

- [x] Update `DynamoDbTableBase.cs` if needed
  - [x] Ensure options are passed through to entity mapping calls (verified - already passes Options to all request builders)
  - _Requirements: 5.3_

## Task 7: Update Unit Tests
- [x] Update `Oproto.FluentDynamoDb.UnitTests`
  - [x] Update all test entity `ToDynamoDb`/`FromDynamoDb` implementations
  - [x] Update all tests that call these methods
  - [x] Add tests for `IJsonBlobSerializer` null check exception
  - _Requirements: 8.1_

- [x] Update `Oproto.FluentDynamoDb.SystemTextJson.UnitTests`
  - [x] Delete tests for old `SystemTextJsonSerializer` class
  - [x] Add tests for `SystemTextJsonBlobSerializer`
  - [x] Add tests for `WithSystemTextJson` extension methods
  - [x] Add tests for AOT path with `JsonSerializerContext`
  - _Requirements: 8.2_

- [x] Update `Oproto.FluentDynamoDb.NewtonsoftJson.UnitTests`
  - [x] Delete tests for old `NewtonsoftJsonSerializer` class
  - [x] Add tests for `NewtonsoftJsonBlobSerializer`
  - [x] Add tests for `WithNewtonsoftJson` extension methods
  - _Requirements: 8.2_

- [x] Update `Oproto.FluentDynamoDb.SourceGenerator.UnitTests`
  - [x] Update `AdvancedTypeGenerationTests.cs` - remove assembly attribute tests
  - [x] Add tests for new generated code pattern
  - [x] Add tests for diagnostic warning
  - _Requirements: 8.1_
  - ✅ **VERIFIED**: All 37 tests pass

## Task 8: Update Package READMEs
- [x] Update `Oproto.FluentDynamoDb.SystemTextJson/README.md`
  - [x] Document `WithSystemTextJson()` extension method
  - [x] Document `WithSystemTextJson(JsonSerializerOptions)` overload
  - [x] Document `WithSystemTextJson(JsonSerializerContext)` for AOT
  - [x] Show complete usage example with `FluentDynamoDbOptions`
L  - [x] Remove documentation for old `SystemTextJsonSerializer` class
  - _Requirements: 7.1_

- [x] Update `Oproto.FluentDynamoDb.NewtonsoftJson/README.md`
  - [x] Document `WithNewtonsoftJson()` extension method
  - [x] Document `WithNewtonsoftJson(JsonSerializerSettings)` overload
  - [x] Show complete usage example with `FluentDynamoDbOptions`
  - [x] Remove documentation for old `NewtonsoftJsonSerializer` class
  - _Requirements: 7.2_

## Task 9: Update Main Documentation
- [x] Update `docs/advanced-topics/AdvancedTypes.md`
  - [x] Update `[JsonBlob]` section with new configuration pattern
  - [x] Remove assembly attribute examples
  - [x] Add `FluentDynamoDbOptions` configuration examples
  - _Requirements: 7.3_

- [x] Update `docs/reference/AttributeReference.md`
  - [x] Update `[JsonBlob]` documentation
  - [x] Remove `[DynamoDbJsonSerializer]` section entirely
  - _Requirements: 7.3_

- [x] Update `docs/reference/AdvancedTypesQuickReference.md`
  - [x] Update JSON blob configuration examples
  - _Requirements: 7.3_

- [x] Update `docs/examples/AdvancedTypesExamples.md`
  - [x] Update all `[JsonBlob]` examples with new pattern
  - _Requirements: 7.3_

- [x] Update `docs/QUICK_REFERENCE.md`
  - [x] Update JSON blob configuration section
  - _Requirements: 7.3_

## Task 10: Update Changelogs
- [x] Update `CHANGELOG.md`
  - [x] Add breaking change entry for `IDynamoDbEntity` interface change
  - [x] Add breaking change entry for removed assembly attribute
  - [x] Add new feature entry for `IJsonBlobSerializer` and options-based configuration
  - [x] Add migration guidance
  - _Requirements: 7.4_

- [x] Update `docs/DOCUMENTATION_CHANGELOG.md`
  - [x] Add entry for SystemTextJson README changes
  - [x] Add entry for NewtonsoftJson README changes
  - [x] Add entry for AdvancedTypes.md changes
  - [x] Add entry for AttributeReference.md changes
  - [x] Add entry for all other documentation updates
  - _Requirements: 7.5_

## Task 11: Integration Testing
- [x] 11 Run full test suite
  - `dotnet test` on entire solution
  -  Verify all tests pass
  - _Requirements: 8.1, 8.2_

- [x] Manual integration test
  - [x] Create test project with `[JsonBlob]` property
  - [x] Verify error message when no serializer configured
  - [x] Verify SystemTextJson works with `WithSystemTextJson()`
  - [x] Verify NewtonsoftJson works with `WithNewtonsoftJson()`
  - [x] Verify custom options are respected
  - _Requirements: 2.2, 3.1, 4.1_

## Task 12: Build Verification
- [x] Verify solution builds without errors
  - [x] `dotnet build` on entire solution
  - _Requirements: All_

- [x] Verify NuGet packages build correctly
  - [x] `dotnet pack` on solution
  - [x] Verify package contents include new files
  - _Requirements: All_
