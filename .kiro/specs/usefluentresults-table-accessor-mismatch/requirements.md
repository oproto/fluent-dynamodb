# Requirements Document

## Introduction

This document specifies the requirements for fixing a bug in the FluentDynamoDb source generator where table-level convenience methods (`GetAsync`, `DeleteAsync`, `PutAsync`) are generated that call accessor methods that don't exist when `[UseFluentResults]` is applied to an entity with `HideGeneratedAsyncMethods = true` (the default).

When `[UseFluentResults]` is applied, the entity accessor class generates `GetAsyncResult`, `DeleteAsyncResult`, and `PutAsyncResult` methods instead of the traditional `GetAsync`, `DeleteAsync`, and `PutAsync` methods. However, the table-level operations always generate methods that delegate to the traditional async methods, causing compilation errors in consuming projects.

## Glossary

- **Source Generator**: The Roslyn-based code generator that produces table classes, entity accessors, and related code from entity definitions.
- **Entity Accessor**: A nested class within the generated table class that provides entity-specific operation methods (e.g., `UserAccessor` for `User` entity).
- **Table-Level Operations**: Convenience methods on the table class that delegate to the default entity's accessor methods.
- **UseFluentResults**: An attribute that configures the source generator to produce Result-returning methods instead of exception-throwing methods.
- **HideGeneratedAsyncMethods**: A property on `[UseFluentResults]` that controls whether traditional async methods are suppressed (default: `true`).
- **Traditional Async Methods**: Methods like `GetAsync`, `PutAsync`, `DeleteAsync` that throw exceptions on failure.
- **Result-Returning Methods**: Methods like `GetAsyncResult`, `PutAsyncResult`, `DeleteAsyncResult` that return `FluentResults.Result<T>` instead of throwing exceptions.

## Requirements

### Requirement 1

**User Story:** As a developer using `[UseFluentResults]`, I want the generated table class to compile successfully, so that I can use the FluentResults pattern without build errors.

#### Acceptance Criteria

1. WHEN an entity has `[UseFluentResults]` with `HideGeneratedAsyncMethods = true` (default) THEN the Source Generator SHALL NOT generate table-level `GetAsync` methods that delegate to accessor `GetAsync` methods.
2. WHEN an entity has `[UseFluentResults]` with `HideGeneratedAsyncMethods = true` (default) THEN the Source Generator SHALL NOT generate table-level `DeleteAsync` methods that delegate to accessor `DeleteAsync` methods.
3. WHEN an entity has `[UseFluentResults]` with `HideGeneratedAsyncMethods = false` THEN the Source Generator SHALL generate table-level `GetAsync` methods that delegate to accessor `GetAsync` methods.
4. WHEN an entity has `[UseFluentResults]` with `HideGeneratedAsyncMethods = false` THEN the Source Generator SHALL generate table-level `DeleteAsync` methods that delegate to accessor `DeleteAsync` methods.
5. WHEN an entity does not have `[UseFluentResults]` THEN the Source Generator SHALL generate table-level `GetAsync` and `DeleteAsync` methods that delegate to accessor methods.

### Requirement 2

**User Story:** As a developer using `[UseFluentResults]`, I want table-level Result-returning convenience methods, so that I can use the FluentResults pattern directly from the table class.

#### Acceptance Criteria

1. WHEN an entity has `[UseFluentResults]` THEN the Source Generator SHALL generate table-level `GetAsyncResult` methods that delegate to accessor `GetAsyncResult` methods.
2. WHEN an entity has `[UseFluentResults]` THEN the Source Generator SHALL generate table-level `DeleteAsyncResult` methods that delegate to accessor `DeleteAsyncResult` methods.
3. WHEN an entity has `[UseFluentResults]` THEN the Source Generator SHALL generate table-level `PutAsyncResult` methods that delegate to accessor `PutAsyncResult` methods.
4. WHEN an entity has `[UseFluentResults]` THEN the Source Generator SHALL generate table-level `QueryAsyncResult` methods that delegate to accessor `QueryAsyncResult` methods.

### Requirement 3

**User Story:** As a developer, I want consistent method availability between table-level and accessor-level operations, so that I can use either access pattern interchangeably.

#### Acceptance Criteria

1. WHEN the Source Generator generates a table-level convenience method THEN the corresponding accessor method SHALL exist.
2. WHEN the Source Generator generates a table-level method that delegates to an accessor method THEN the accessor method signature SHALL match the delegation call.
3. WHEN an entity has `[UseFluentResults]` with `HideGeneratedAsyncMethods = true` THEN the table-level operations SHALL only include builder methods and Result-returning convenience methods.
4. WHEN an entity has `[UseFluentResults]` with `HideGeneratedAsyncMethods = false` THEN the table-level operations SHALL include both traditional async methods and Result-returning convenience methods.
