# Requirements Document

## Introduction

The Oproto.FluentDynamoDb source generator produces typed parameter overloads for builder-returning methods (Get, Delete, Update, ConditionCheck) when an entity has computed keys with 2+ source properties. However, the corresponding async convenience methods (GetAsync, DeleteAsync) are not generated for these typed overloads. This feature adds the missing typed async convenience methods so users can perform one-shot operations without manually chaining terminal methods on the builder.

## Glossary

- **Source_Generator**: The Oproto.FluentDynamoDb.SourceGenerator project that produces C# code at compile time based on entity attribute metadata.
- **Typed_Overload**: A method variant that accepts individual source property component values (e.g., year, month, day) instead of a pre-built composite key string.
- **Async_Convenience_Method**: A one-shot method that combines builder creation with terminal method execution in a single call (e.g., `GetAsync`, `DeleteAsync`).
- **Computed_Key_Entity**: An entity where at least one key property uses the `[Computed]` attribute with 2 or more source properties.
- **Entity_Accessor**: The generated typed accessor property on the table class that provides entity-specific CRUD operations (e.g., `table.ScheduledEvents`).
- **Table_Class**: The generated table class (e.g., `MyTableTable`) that aggregates entity accessors and table-level convenience methods.
- **KeyCondition**: An enum parameter (`KeyCondition.None`, `KeyCondition.MustExist`, `KeyCondition.MustNotExist`) that applies existence preconditions to delete operations.
- **CancellationToken**: A .NET token used to signal cancellation of asynchronous operations.
- **FluentResults_Variant**: A method suffixed with `Result` (e.g., `GetAsyncResult`, `DeleteAsyncResult`) that returns `Result<T>` or `Result` instead of throwing exceptions, generated when `[UseFluentResults]` is present on the entity.

## Requirements

### Requirement 1: Typed GetAsync on Entity Accessor

**User Story:** As a developer using a computed key entity, I want to call `GetAsync` with typed source property parameters on the entity accessor, so that I can retrieve an item in a single call without manually chaining `.GetItemAsync()` on the builder.

#### Acceptance Criteria

1. WHEN a Computed_Key_Entity qualifies for typed overloads, THE Source_Generator SHALL emit a `GetAsync` method on the Entity_Accessor that accepts the same typed parameters as the typed `Get` overload plus a `CancellationToken` parameter with default value `default`.
2. WHEN the typed `GetAsync` method is invoked, THE Entity_Accessor SHALL delegate to the typed `Get` builder method and call `.GetItemAsync(cancellationToken)` on the resulting builder.
3. IF the typed `GetAsync` method is emitted, THEN it SHALL return `Task<T?>` where T is the entity type.
4. WHEN the entity does not qualify for typed overloads, THE Source_Generator SHALL not emit a typed `GetAsync` method.
5. WHEN the typed overload would be ambiguous with the standard overload, THE Source_Generator SHALL not emit a typed `GetAsync` method.

### Requirement 2: Typed DeleteAsync on Entity Accessor

**User Story:** As a developer using a computed key entity, I want to call `DeleteAsync` with typed source property parameters on the entity accessor, so that I can delete an item in a single call without manually chaining `.DeleteAsync()` on the builder.

#### Acceptance Criteria

1. WHEN a Computed_Key_Entity qualifies for typed overloads, THE Source_Generator SHALL emit a `DeleteAsync` method on the Entity_Accessor that accepts the same typed parameters as the typed `Delete` overload followed by a `KeyCondition` parameter with default value `KeyCondition.None` and a `CancellationToken` parameter with default value `default`.
2. WHEN the typed `DeleteAsync` method is invoked, THE Entity_Accessor SHALL delegate to the typed `Delete` builder method using the typed source property parameters, apply `.WithKeyCondition(keyCondition)` on the builder if the KeyCondition is not `KeyCondition.None`, and call `.DeleteAsync(cancellationToken)` on the resulting builder.
3. THE typed `DeleteAsync` method SHALL return `Task`.
4. WHEN the entity does not qualify for typed overloads, THE Source_Generator SHALL not emit a typed `DeleteAsync` method.
5. WHEN the typed overload would be ambiguous with the standard overload, THE Source_Generator SHALL not emit a typed `DeleteAsync` method.

### Requirement 3: Table-Level Typed GetAsync

**User Story:** As a developer using a single-entity computed key table, I want to call `GetAsync` with typed source property parameters at the table level, so that I can use the same concise syntax available on the entity accessor directly from the table instance.

#### Acceptance Criteria

1. WHEN a Computed_Key_Entity qualifies for typed overloads, the typed overload would not be ambiguous with the standard overload, and the table is a single-entity table, THE Source_Generator SHALL emit a typed `GetAsync` method on the Table_Class that delegates to the Entity_Accessor's typed `GetAsync` method.
2. THE table-level typed `GetAsync` method SHALL accept the same parameters as the Entity_Accessor typed `GetAsync` method, including the `CancellationToken` with default value `default`.
3. THE table-level typed `GetAsync` method SHALL return `Task<T?>` where T is the entity type.
4. IF the entity does not qualify for typed overloads OR the table is not a single-entity table, THEN THE Source_Generator SHALL not emit a typed `GetAsync` method on the Table_Class.

### Requirement 4: Table-Level Typed DeleteAsync

**User Story:** As a developer using a single-entity computed key table, I want to call `DeleteAsync` with typed source property parameters at the table level, so that I can use the same concise syntax available on the entity accessor directly from the table instance.

#### Acceptance Criteria

1. WHEN a Computed_Key_Entity qualifies for typed overloads and the table is a single-entity table, THE Source_Generator SHALL emit a typed `DeleteAsync` method on the Table_Class that forwards all parameters to the Entity_Accessor's typed `DeleteAsync` method.
2. THE table-level typed `DeleteAsync` method SHALL accept the same typed source property parameters as the Entity_Accessor typed `DeleteAsync` method, followed by a `KeyCondition` parameter with default value `KeyCondition.None` and a `CancellationToken` parameter with default value `default`.
3. THE table-level typed `DeleteAsync` method SHALL return `Task`.
4. IF the entity does not qualify for typed overloads OR the table is not a single-entity table, THEN THE Source_Generator SHALL not emit a typed `DeleteAsync` method on the Table_Class.

### Requirement 5: Typed GetAsyncResult (FluentResults Variant)

**User Story:** As a developer using a computed key entity with `[UseFluentResults]`, I want to call `GetAsyncResult` with typed source property parameters, so that I can retrieve an item as a `Result<T?>` without exception handling.

#### Acceptance Criteria

1. WHEN a Computed_Key_Entity qualifies for typed overloads AND the entity has the `[UseFluentResults]` attribute, THE Source_Generator SHALL emit a `GetAsyncResult` method on the Entity_Accessor that accepts the same typed parameters as the typed `Get` overload plus a `CancellationToken` parameter with default value `default`.
2. WHEN the typed `GetAsyncResult` method is invoked, THE Entity_Accessor SHALL delegate to the typed `Get` builder method and call `.GetItemAsyncResult(cancellationToken)` on the resulting builder.
3. THE typed `GetAsyncResult` method SHALL return `Task<Result<T?>>` where T is the entity type.
4. WHEN the `[UseFluentResults]` attribute is not present on the entity, THE Source_Generator SHALL not emit the typed `GetAsyncResult` method.
5. WHEN the typed `GetAsyncResult` overload would be ambiguous with the standard `GetAsyncResult` overload, THE Source_Generator SHALL not emit the typed `GetAsyncResult` method.

### Requirement 6: Typed DeleteAsyncResult (FluentResults Variant)

**User Story:** As a developer using a computed key entity with `[UseFluentResults]`, I want to call `DeleteAsyncResult` with typed source property parameters, so that I can delete an item and receive a `Result` without exception handling.

#### Acceptance Criteria

1. WHEN a Computed_Key_Entity qualifies for typed overloads AND the entity has the `[UseFluentResults]` attribute, THE Source_Generator SHALL emit a `DeleteAsyncResult` method on the Entity_Accessor that accepts the same typed parameters as the typed `Delete` overload plus a `KeyCondition` parameter with default value `KeyCondition.None` and a `CancellationToken` parameter with default value `default`.
2. WHEN the typed `DeleteAsyncResult` method is invoked with a non-`None` KeyCondition, THE Entity_Accessor SHALL apply the key condition using `.WithKeyCondition(keyCondition)` on the builder before calling `.DeleteAsyncResult(cancellationToken)` on the resulting builder.
3. WHEN the typed `DeleteAsyncResult` method is invoked with `KeyCondition.None`, THE Entity_Accessor SHALL delegate to the typed `Delete` builder method and call `.DeleteAsyncResult(cancellationToken)` on the resulting builder without applying a key condition.
4. THE typed `DeleteAsyncResult` method SHALL return `Task<Result>`.
5. WHEN the `[UseFluentResults]` attribute is not present on the entity, THE Source_Generator SHALL not emit the typed `DeleteAsyncResult` method.
6. WHEN the typed overload would be ambiguous with the standard overload, THE Source_Generator SHALL not emit a typed `DeleteAsyncResult` method.

### Requirement 7: Table-Level FluentResults Variants

**User Story:** As a developer using a single-entity computed key table with `[UseFluentResults]`, I want to call `GetAsyncResult` and `DeleteAsyncResult` with typed source property parameters at the table level, so that table-level and entity-accessor-level APIs remain symmetric.

#### Acceptance Criteria

1. WHEN a Computed_Key_Entity qualifies for typed overloads (at least one key is computed, the overload would not be ambiguous with the standard string-parameter overload), the table is a single-entity table, AND the entity has the `[UseFluentResults]` attribute, THE Source_Generator SHALL emit a typed `GetAsyncResult` method on the Table_Class that accepts the resolved source property parameters plus an optional `CancellationToken` and returns `Task<Result<T?>>`.
2. WHEN a Computed_Key_Entity qualifies for typed overloads, the table is a single-entity table, AND the entity has the `[UseFluentResults]` attribute, THE Source_Generator SHALL emit a typed `DeleteAsyncResult` method on the Table_Class that accepts the resolved source property parameters, an optional `KeyCondition` defaulting to `None`, and an optional `CancellationToken`, and returns `Task<Result>`.
3. THE table-level typed `GetAsyncResult` method SHALL delegate to the Entity_Accessor's typed `GetAsyncResult` method, passing all source property parameters and the cancellation token unchanged.
4. THE table-level typed `DeleteAsyncResult` method SHALL delegate to the Entity_Accessor's typed `DeleteAsyncResult` method, passing all source property parameters, the key condition, and the cancellation token unchanged.
5. IF the `[UseFluentResults]` attribute is not present on the entity, THEN THE Source_Generator SHALL NOT emit typed `GetAsyncResult` or `DeleteAsyncResult` FluentResults_Variant methods at the table level.
6. IF the entity has the `[UseFluentResults]` attribute but does not qualify for typed overloads (no computed key, or the typed overload would be ambiguous), THEN THE Source_Generator SHALL NOT emit the typed source-property-parameter `GetAsyncResult` or `DeleteAsyncResult` methods at the table level.

### Requirement 8: Eligibility Guards

**User Story:** As a developer, I want the typed async convenience methods to follow the same eligibility and ambiguity rules as the existing typed builder overloads, so that the generated API surface remains consistent and free of compilation errors.

#### Acceptance Criteria

1. THE Source_Generator SHALL apply the same `ComputedOverloadEligibility.QualifiesForTypedOverload` check to typed async methods as to typed builder overloads.
2. THE Source_Generator SHALL apply the same `ComputedOverloadEligibility.WouldBeAmbiguous` check to typed async methods as to typed builder overloads.
3. IF the `OverloadParameterResolver.GetTypedOverloadParameters` call returns null for an entity, THEN THE Source_Generator SHALL not emit typed async methods for that entity and SHALL emit the same diagnostic as for typed builder overloads.
