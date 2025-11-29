# Requirements Document

## Introduction

This specification addresses the remaining AOT-unsafe reflection in the Oproto.FluentDynamoDb library by introducing a service registration pattern. Currently, the library uses reflection to discover optional features (geospatial support, blob storage async methods) and to handle generic collection formatting at runtime. This breaks AOT compilation and trimming. The solution introduces a centralized configuration system where optional packages register their handlers at startup, and source-generated code handles type-specific operations.

## Glossary

- **AOT (Ahead-of-Time)**: Compilation strategy where code is compiled to native code before runtime, incompatible with reflection-based dynamic code
- **Service Registration**: Pattern where services are registered at application startup and resolved without reflection
- **Source Generator**: Compile-time code generation that produces type-specific implementations
- **FluentDynamoDbOptions**: Central configuration object for the library
- **IGeospatialProvider**: Interface for geospatial operations (bounding box, cell covering)
- **IAsyncEntityHydrator**: Interface for async entity hydration with blob storage support
- **ICollectionFormatter**: Interface for type-specific collection formatting

## Requirements

### Requirement 1

**User Story:** As a library consumer, I want to configure FluentDynamoDb services at application startup, so that optional features are registered without runtime reflection.

#### Acceptance Criteria

1. WHEN configuring FluentDynamoDb THEN the Library SHALL provide a `FluentDynamoDbOptions` class for centralized configuration
2. WHEN registering optional packages THEN the Library SHALL provide extension methods like `AddFluentDynamoDbGeospatial()` and `AddFluentDynamoDbBlobStorage()`
3. WHEN no optional packages are registered THEN the Library SHALL function correctly with core features only
4. WHEN multiple optional packages are registered THEN the Library SHALL compose their functionality without conflicts
5. WHEN configuration is complete THEN the Library SHALL validate that all required dependencies are satisfied

### Requirement 2

**User Story:** As a library consumer using geospatial features, I want to register geospatial support at startup, so that expression translation works without reflection.

#### Acceptance Criteria

1. WHEN `AddFluentDynamoDbGeospatial()` is called THEN the Geospatial_Package SHALL register an `IGeospatialProvider` implementation
2. WHEN ExpressionTranslator encounters geospatial methods THEN the Translator SHALL use the registered `IGeospatialProvider` instead of reflection
3. WHEN geospatial features are used without registration THEN the Library SHALL throw a descriptive exception explaining how to register
4. WHEN `IGeospatialProvider` is registered THEN the Provider SHALL expose methods for bounding box creation and cell covering without reflection

### Requirement 3

**User Story:** As a library consumer using blob storage, I want async entity hydration to work without reflection, so that the library is fully AOT-compatible.

#### Acceptance Criteria

1. WHEN entities have blob references THEN the Source_Generator SHALL generate an `IAsyncEntityHydrator<TEntity>` implementation
2. WHEN `ToListAsync` or `ToEntityAsync` is called with blob storage THEN the Library SHALL use the generated hydrator instead of reflection-based method discovery
3. WHEN `AddFluentDynamoDbBlobStorage()` is called THEN the BlobStorage_Package SHALL register the `IBlobStorageProvider` implementation
4. WHEN blob storage is used without registration THEN the Library SHALL throw a descriptive exception explaining how to register

### Requirement 4

**User Story:** As a library consumer, I want collection formatting to work without reflection, so that update expressions with HashSet<T> properties are AOT-compatible.

#### Acceptance Criteria

1. WHEN an entity has collection properties with format strings THEN the Source_Generator SHALL generate type-specific formatting methods
2. WHEN UpdateExpressionTranslator formats a collection THEN the Translator SHALL use the generated formatter instead of `Activator.CreateInstance`
3. WHEN a collection type is not pre-registered THEN the Library SHALL fall back to a generic implementation with appropriate trimmer warnings
4. WHEN formatting is applied THEN the Formatter SHALL preserve the original collection type (HashSet<T> remains HashSet<T>)

### Requirement 5

**User Story:** As a library consumer, I want logging to be configured through the same service registration pattern, so that configuration is consistent across all features.

#### Acceptance Criteria

1. WHEN configuring FluentDynamoDb THEN the Library SHALL accept an `IDynamoDbLogger` through `FluentDynamoDbOptions`
2. WHEN no logger is configured THEN the Library SHALL use `NoOpLogger.Instance` as the default
3. WHEN a logger is configured THEN the Library SHALL propagate it to all request builders and translators automatically
4. WHEN using Microsoft.Extensions.Logging THEN the Logging_Extensions_Package SHALL provide `AddFluentDynamoDbLogging(ILoggerFactory)` extension

### Requirement 6

**User Story:** As a library maintainer, I want the service registration to be AOT-compatible, so that the entire library works in trimmed and AOT-compiled applications.

#### Acceptance Criteria

1. WHEN the library is trimmed THEN the Library SHALL produce zero IL2026, IL2060, IL2070, IL2072, IL2075, or IL3050 warnings from service registration code
2. WHEN the library is AOT-compiled THEN the Library SHALL function correctly without runtime code generation
3. WHEN optional packages are not referenced THEN the Trimmer SHALL remove their code completely
4. WHEN service registration is complete THEN the Library SHALL not use `Assembly.GetType()`, `Type.GetMethod()`, or `Activator.CreateInstance()` for registered services

### Requirement 7

**User Story:** As a library consumer not using DI, I want to configure FluentDynamoDb without a DI container, so that the library works in simple console applications.

#### Acceptance Criteria

1. WHEN not using a DI container THEN the Library SHALL provide instance-based configuration through `FluentDynamoDbOptions`
2. WHEN creating table instances THEN the Library SHALL accept `FluentDynamoDbOptions` as a constructor parameter
3. WHEN options are not provided THEN the Library SHALL use sensible defaults (NoOpLogger, no optional features)
4. WHEN multiple table instances exist THEN each Instance SHALL use its own configuration independently

### Requirement 8

**User Story:** As a test author, I want each test to have isolated configuration, so that parallel test execution works correctly without interference.

#### Acceptance Criteria

1. WHEN running tests in parallel THEN the Library SHALL NOT use AsyncLocal or static mutable state for configuration
2. WHEN a test creates a table instance with specific options THEN the Options SHALL be scoped to that instance only
3. WHEN multiple tests run concurrently with different configurations THEN each Test SHALL see only its own configuration
4. WHEN using DI in tests THEN the Library SHALL support scoped service lifetimes for test isolation
5. WHEN a test completes THEN the Library SHALL NOT retain any test-specific configuration in global state

