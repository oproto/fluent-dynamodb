# Requirements Document

## Introduction

This feature enhances the usability of the Oproto.FluentDynamoDb library by introducing entity-specific update builders and convenience method convenience methods. The primary goal is to improve the developer experience when working with DynamoDB operations by reducing boilerplate code and eliminating the need for excessive generic type parameters in common scenarios.

## Glossary

- **FluentDynamoDb**: The Oproto.FluentDynamoDb library that provides a fluent API wrapper for Amazon DynamoDB
- **UpdateItemRequestBuilder**: The existing generic builder class for constructing DynamoDB UpdateItem requests
- **Entity-Specific Update Builder**: A specialized update builder class that inherits from UpdateItemRequestBuilder and is bound to a specific entity type
- **Convenience Method Method**: A convenience method that combines builder creation and execution in a single call (e.g., GetAsync() instead of Get().ExecuteAsync())
- **Accessor Class**: A nested class within generated table classes that provides entity-specific operation methods (e.g., MultiEntityOrderTestEntityAccessor)
- **Generic Type Parameter**: A type parameter in C# generics (e.g., TEntity, TProperty, TValue)
- **LINQ Expression**: A strongly-typed lambda expression used for type-safe property access (e.g., x => x.PropertyName)

## Requirements

### Requirement 1: Entity-Specific Update Builders

**User Story:** As a developer using FluentDynamoDb, I want to use expression-based Set() methods without specifying three generic type parameters, so that my code is more concise and easier to read.

#### Acceptance Criteria

1. WHEN a developer calls the Update() method on an entity accessor (e.g., table.Orders.Update(pk)), THEN FluentDynamoDb SHALL return an entity-specific update builder that is bound to the entity type
2. WHEN a developer calls Set() with a LINQ expression on an entity-specific update builder, THEN FluentDynamoDb SHALL infer the property type and value type without requiring explicit generic type parameters
3. WHEN a developer chains multiple Set() calls on an entity-specific update builder, THEN FluentDynamoDb SHALL maintain the fluent interface pattern and allow method chaining
4. WHEN an entity-specific update builder is used, THEN FluentDynamoDb SHALL provide the same functionality as the base UpdateItemRequestBuilder including encryption support, condition expressions, and return value options

### Requirement 2: Convenience Method Convenience Methods

**User Story:** As a developer using FluentDynamoDb, I want to execute simple operations without chaining builder methods, so that I can write more concise code for straightforward use cases.

#### Acceptance Criteria

1. WHEN a developer needs to execute a Get operation without additional configuration, THEN FluentDynamoDb SHALL provide a GetAsync() method that combines Get() and ExecuteAsync()
2. WHEN a developer needs to execute a Put operation without additional configuration, THEN FluentDynamoDb SHALL provide a PutAsync() method that combines Put() and ExecuteAsync()
3. WHEN a developer needs to execute a Delete operation without additional configuration, THEN FluentDynamoDb SHALL provide a DeleteAsync() method that combines Delete() and ExecuteAsync()
4. WHEN a developer needs to execute an Update operation without additional configuration, THEN FluentDynamoDb SHALL provide an UpdateAsync() method that combines Update() and ExecuteAsync()
5. WHERE convenience method methods are provided, FluentDynamoDb SHALL accept the same parameters as the corresponding builder creation methods

### Requirement 3: Raw Attribute Dictionary Support

**User Story:** As a developer using FluentDynamoDb, I want to work with raw DynamoDB attribute dictionaries directly, so that I can use the library for advanced scenarios without requiring entity classes.

#### Acceptance Criteria

1. WHEN a developer calls Put() with a Dictionary<string, AttributeValue> parameter on an entity accessor, THEN FluentDynamoDb SHALL accept the dictionary and return a configured builder
2. WHEN a developer calls PutAsync() with a Dictionary<string, AttributeValue> parameter on an entity accessor, THEN FluentDynamoDb SHALL store the item in DynamoDB without requiring an entity class
3. WHEN raw attribute dictionary overloads are provided, THEN FluentDynamoDb SHALL maintain the same fluent interface and functionality as entity-based overloads
4. WHEN a developer uses raw attribute dictionaries, THEN FluentDynamoDb SHALL support all standard PutItem options including conditions, return values, and consumed capacity

### Requirement 4: Consistent API Surface

**User Story:** As a developer using FluentDynamoDb, I want all entity accessors to provide a consistent set of operations, so that I can use the same patterns across different entity types.

#### Acceptance Criteria

1. WHEN entity-specific update builders are implemented, THEN FluentDynamoDb SHALL ensure all entity accessor classes provide Update() methods that return the entity-specific builder
2. WHEN convenience method methods are implemented, THEN FluentDynamoDb SHALL ensure all entity accessor classes provide the same set of convenience method methods (GetAsync, PutAsync, DeleteAsync, UpdateAsync)
3. WHEN convenience method methods are implemented, THEN FluentDynamoDb SHALL provide them on both DynamoDbTableBase and entity accessor classes for consistency
4. WHEN new convenience methods are added, THEN FluentDynamoDb SHALL maintain backward compatibility with existing code that uses the builder pattern
5. WHEN a developer uses either the builder pattern or convenience method methods, THEN FluentDynamoDb SHALL provide equivalent functionality and behavior

### Requirement 5: Complete Fluent Chain Preservation

**User Story:** As a developer using FluentDynamoDb, I want all fluent methods on entity-specific builders to maintain the correct return type, so that I can chain methods without losing type safety or simplified method signatures.

#### Acceptance Criteria

1. WHEN a developer calls Where() on an entity-specific update builder, THEN FluentDynamoDb SHALL return the entity-specific builder type to maintain the fluent chain
2. WHEN a developer calls WithValue() on an entity-specific update builder, THEN FluentDynamoDb SHALL return the entity-specific builder type to maintain the fluent chain
3. WHEN a developer calls WithAttribute() on an entity-specific update builder, THEN FluentDynamoDb SHALL return the entity-specific builder type to maintain the fluent chain
4. WHEN a developer calls any fluent method on an entity-specific update builder, THEN FluentDynamoDb SHALL ensure the simplified Set() method remains available after the call
5. WHEN a developer uses LINQ expressions with Where() on an entity-specific update builder, THEN FluentDynamoDb SHALL not require explicit generic type parameters for the entity type

### Requirement 6: Automatic Extension Method Wrapper Generation

**User Story:** As a library maintainer, I want extension methods to be automatically wrapped in entity-specific builders, so that adding new extension methods doesn't require manual updates to the code generator.

#### Acceptance Criteria

1. WHEN an extension method is marked with the GenerateWrapper attribute, THEN FluentDynamoDb SHALL automatically generate a wrapper method in entity-specific builders
2. WHEN an extension method requires generic type specialization, THEN FluentDynamoDb SHALL apply the appropriate specialization rules based on the method signature
3. WHEN a new extension method is added to an interface implemented by UpdateItemRequestBuilder, THEN FluentDynamoDb SHALL generate wrappers for it if marked with GenerateWrapper
4. WHEN the source generator runs, THEN FluentDynamoDb SHALL validate that all marked extension methods exist and report errors for missing methods
5. WHEN an extension method has multiple overloads, THEN FluentDynamoDb SHALL generate wrappers for all overloads marked with GenerateWrapper
