# Requirements Document

## Introduction

This feature comprehensively refactors and enhances the FluentDynamoDb API to address multiple usability, consistency, and functionality gaps. The changes include: removing redundant type parameters from chained methods, adding missing LINQ expression overloads to source-generated code, ensuring sensitive data protection in expression logging, supporting format strings in LINQ expressions, handling field-level encryption in expressions, and updating documentation to reflect current API patterns.

These changes represent a breaking API improvement but are acceptable since the codebase is still in a greenfield state. The goal is to create a more ergonomic, type-safe, and feature-complete API that aligns with modern .NET patterns.

## Glossary

- **Builder**: A fluent interface class that constructs DynamoDB request objects (QueryRequestBuilder, ScanRequestBuilder, etc.)
- **Type Parameter**: A generic type argument specified in angle brackets (e.g., `<TEntity>`)
- **Fluent Chain**: A sequence of method calls where each method returns an object that supports further chaining
- **Terminal Method**: The final method in a fluent chain that executes the operation (e.g., ExecuteAsync, ToListAsync)
- **Request Builder**: Classes like QueryRequestBuilder, ScanRequestBuilder, GetItemRequestBuilder that build DynamoDB requests
- **Source Generator**: Code generation tool that creates table classes and mapping code from entity attributes
- **LINQ Expression**: Language Integrated Query expressions using lambda syntax (e.g., `x => x.Id == id`)
- **Expression Translation**: The process of converting C# lambda expressions to DynamoDB query syntax
- **Sensitive Attribute**: Marker indicating a property value should be redacted from logs
- **Encrypted Attribute**: Marker indicating a property should be encrypted at rest
- **Format String**: A string template with placeholders for dynamic values (e.g., `"PK = {0}"`)
- **DynamoDbAttribute**: Attribute that configures how a property maps to DynamoDB, including format specifications

## Requirements

### Requirement 1: Simplified Query Builder Chain

**User Story:** As a developer, I want to specify the entity type only once at the start of a query chain, so that my code is less verbose and more readable.

#### Acceptance Criteria

1. WHEN calling Query&lt;TEntity&gt;() on a table, THE System SHALL return a QueryRequestBuilder that remembers TEntity for all subsequent operations
2. WHEN calling Where() on a QueryRequestBuilder, THE System SHALL not require a type parameter
3. WHEN calling WithFilter() on a QueryRequestBuilder, THE System SHALL not require a type parameter
4. WHEN calling ToListAsync() on a QueryRequestBuilder, THE System SHALL not require a type parameter and SHALL return List&lt;TEntity&gt;
5. WHEN calling ExecuteAsync() on a QueryRequestBuilder, THE System SHALL not require a type parameter and SHALL return QueryResponse&lt;TEntity&gt;

### Requirement 2: Simplified Scan Builder Chain

**User Story:** As a developer, I want to specify the entity type only once when scanning, so that scan operations are as concise as query operations.

#### Acceptance Criteria

1. WHEN calling Scan&lt;TEntity&gt;() on a table, THE System SHALL return a ScanRequestBuilder that remembers TEntity for all subsequent operations
2. WHEN calling WithFilter() on a ScanRequestBuilder, THE System SHALL not require a type parameter
3. WHEN calling ToListAsync() on a ScanRequestBuilder, THE System SHALL not require a type parameter and SHALL return List&lt;TEntity&gt;
4. WHEN calling ExecuteAsync() on a ScanRequestBuilder, THE System SHALL not require a type parameter and SHALL return ScanResponse&lt;TEntity&gt;
5. WHEN calling Take() or other configuration methods, THE System SHALL not require a type parameter

### Requirement 3: Simplified Get Operation

**User Story:** As a developer, I want Get operations to infer the return type from the initial call, so that I don't repeat type parameters unnecessarily.

#### Acceptance Criteria

1. WHEN calling Get&lt;TEntity&gt;(key) on a table, THE System SHALL return a GetItemRequestBuilder that remembers TEntity
2. WHEN calling WithConsistentRead() or other configuration methods, THE System SHALL not require a type parameter
3. WHEN calling ExecuteAsync() on a GetItemRequestBuilder, THE System SHALL not require a type parameter and SHALL return GetItemResponse&lt;TEntity&gt;
4. WHEN calling ToRequest() on a GetItemRequestBuilder, THE System SHALL not require a type parameter
5. WHERE Get() is called without type parameter on source-generated tables, THE System SHALL infer the entity type from the table definition

### Requirement 4: Simplified Update Builder Chain

**User Story:** As a developer, I want Update operations to work without repeated type parameters, so that update chains are cleaner.

#### Acceptance Criteria

1. WHEN calling Update&lt;TEntity&gt;(key) on a table, THE System SHALL return an UpdateItemRequestBuilder that remembers TEntity
2. WHEN calling Set(), Remove(), Add(), or Delete() methods, THE System SHALL not require a type parameter
3. WHEN calling WithCondition() on an UpdateItemRequestBuilder, THE System SHALL not require a type parameter
4. WHEN calling ExecuteAsync() on an UpdateItemRequestBuilder, THE System SHALL not require a type parameter
5. WHEN using LINQ expressions in update operations (future feature), THE System SHALL infer property types from TEntity

### Requirement 5: Simplified Batch Operations

**User Story:** As a developer, I want batch operations to specify types only when adding items, so that batch builders are less cluttered.

#### Acceptance Criteria

1. WHEN calling BatchGet() on a table, THE System SHALL return a BatchGetItemRequestBuilder
2. WHEN calling AddGet&lt;TEntity&gt;(key) on a batch builder, THE System SHALL remember TEntity for that specific get operation
3. WHEN calling ExecuteAsync() on a batch builder, THE System SHALL not require type parameters
4. WHEN calling BatchWrite() on a table, THE System SHALL return a BatchWriteItemRequestBuilder
5. WHEN calling AddPut&lt;TEntity&gt;(item) or AddDelete&lt;TEntity&gt;(key), THE System SHALL infer TEntity from the parameter type where possible

### Requirement 6: Simplified Transaction Operations

**User Story:** As a developer, I want transaction builders to infer types from the items being added, so that transaction code is more concise.

#### Acceptance Criteria

1. WHEN calling TransactWrite() on a table, THE System SHALL return a TransactWriteItemsRequestBuilder
2. WHEN calling AddPut&lt;TEntity&gt;(item), THE System SHALL remember TEntity for that transaction item
3. WHEN calling AddUpdate&lt;TEntity&gt;(key), THE System SHALL return a builder that remembers TEntity for subsequent Set/Remove calls
4. WHEN calling AddConditionCheck&lt;TEntity&gt;(key), THE System SHALL remember TEntity for condition expressions
5. WHEN calling ExecuteAsync() on a transaction builder, THE System SHALL not require type parameters

### Requirement 7: Type-Safe Builder Interfaces

**User Story:** As a library maintainer, I want builder interfaces to be properly generic, so that type safety is maintained throughout the chain.

#### Acceptance Criteria

1. WHEN defining QueryRequestBuilder, THE Class SHALL be generic QueryRequestBuilder&lt;TEntity&gt;
2. WHEN defining ScanRequestBuilder, THE Class SHALL be generic ScanRequestBuilder&lt;TEntity&gt;
3. WHEN defining GetItemRequestBuilder, THE Class SHALL be generic GetItemRequestBuilder&lt;TEntity&gt;
4. WHEN defining UpdateItemRequestBuilder, THE Class SHALL be generic UpdateItemRequestBuilder&lt;TEntity&gt;
5. WHEN builder methods return the builder for chaining, THE Methods SHALL return the correctly typed builder instance

### Requirement 8: Source Generator Updates

**User Story:** As a developer using source-generated tables, I want the generator to create methods with the simplified API, so that I automatically benefit from the cleaner syntax.

#### Acceptance Criteria

1. WHEN the source generator creates Query() methods, THE Generated_Code SHALL return QueryRequestBuilder&lt;TEntity&gt; where TEntity is the table's entity type
2. WHEN the source generator creates Scan() methods, THE Generated_Code SHALL return ScanRequestBuilder&lt;TEntity&gt;
3. WHEN the source generator creates Get() methods, THE Generated_Code SHALL return GetItemRequestBuilder&lt;TEntity&gt;
4. WHEN the source generator creates Update() methods, THE Generated_Code SHALL return UpdateItemRequestBuilder&lt;TEntity&gt;
5. WHEN the source generator creates index Query() methods, THE Generated_Code SHALL return properly typed builders

### Requirement 9: Backward Compatibility Considerations

**User Story:** As a library maintainer, I want to understand the breaking changes, so that I can document migration paths clearly.

#### Acceptance Criteria

1. WHEN this feature is implemented, THE System SHALL remove all type parameters from chained methods (breaking change)
2. WHEN developers upgrade, THE Compiler SHALL produce clear errors indicating where type parameters should be removed
3. WHEN migration documentation is created, THE Documentation SHALL provide before/after examples for all common patterns
4. WHEN migration documentation is created, THE Documentation SHALL include regex patterns for automated refactoring
5. THE System SHALL not provide obsolete overloads since the codebase is greenfield

### Requirement 10: LINQ Expression Integration

**User Story:** As a developer, I want the simplified API to work seamlessly with LINQ expressions, so that type inference flows naturally through expression-based queries.

#### Acceptance Criteria

1. WHEN calling Query&lt;TEntity&gt;(x => x.Pk == value), THE System SHALL infer TEntity for the entire chain
2. WHEN calling WithFilter(x => x.Status == "ACTIVE"), THE System SHALL infer the lambda parameter type from TEntity
3. WHEN using LINQ expressions in Where() or WithFilter(), THE System SHALL not require explicit type parameters on the lambda
4. WHEN chaining LINQ expression methods, THE System SHALL maintain type information through the entire chain
5. WHEN calling ToListAsync() after LINQ expressions, THE System SHALL return List&lt;TEntity&gt; without requiring a type parameter

### Requirement 11: Index Query Simplification

**User Story:** As a developer, I want index queries to use the simplified API, so that querying indexes is as clean as querying tables.

#### Acceptance Criteria

1. WHEN calling Query&lt;TEntity&gt;() on a DynamoDbIndex, THE System SHALL return a QueryRequestBuilder&lt;TEntity&gt;
2. WHEN calling Query&lt;TEntity&gt;(key) on an index, THE System SHALL configure the key condition and return a typed builder
3. WHEN chaining methods on index query builders, THE System SHALL not require type parameters
4. WHEN calling ExecuteAsync() on an index query, THE System SHALL return QueryResponse&lt;TEntity&gt; without type parameter
5. WHERE an index is defined with DynamoDbIndex&lt;TEntity&gt;, THE System SHALL use TEntity as the default type for Query() calls

### Requirement 12: Extension Method Compatibility

**User Story:** As a developer, I want extension methods to work with the simplified API, so that custom extensions integrate seamlessly.

#### Acceptance Criteria

1. WHEN defining extension methods on QueryRequestBuilder&lt;TEntity&gt;, THE Extensions SHALL have access to TEntity
2. WHEN extension methods return the builder for chaining, THE Methods SHALL return QueryRequestBuilder&lt;TEntity&gt;
3. WHEN extension methods are used in a chain, THE Type SHALL flow through correctly to subsequent methods
4. WHEN creating custom terminal methods, THE Methods SHALL have access to TEntity for return type specification
5. THE System SHALL document patterns for creating type-safe extensions

### Requirement 13: Error Message Clarity

**User Story:** As a developer, I want clear error messages when type inference fails, so that I can quickly resolve issues.

#### Acceptance Criteria

1. WHEN type inference fails in a query chain, THE Compiler SHALL produce an error indicating where the type should be specified
2. WHEN mixing typed and untyped builders incorrectly, THE Compiler SHALL produce an error explaining the mismatch
3. WHEN using var with builder methods, THE System SHALL infer types correctly without requiring explicit declarations
4. WHEN IntelliSense is used, THE IDE SHALL show the inferred type at each step in the chain
5. THE System SHALL not produce ambiguous overload errors due to type parameter removal

### Requirement 14: Performance Considerations

**User Story:** As a developer, I want the simplified API to have no performance overhead, so that cleaner code doesn't sacrifice efficiency.

#### Acceptance Criteria

1. WHEN using the simplified API, THE System SHALL produce identical IL code to the previous type-parameter-heavy version
2. WHEN the JIT compiler optimizes the code, THE Performance SHALL be identical to previous versions
3. WHEN using AOT compilation, THE Generated_Code SHALL be identical in size and performance
4. THE System SHALL not introduce additional allocations due to generic type handling
5. THE System SHALL not introduce additional virtual method calls

### Requirement 15: Source-Generated LINQ Expression Overloads

**User Story:** As a developer using source-generated tables, I want Query() and Scan() methods with LINQ expression parameters, so that I can write concise queries without chaining Where() calls.

#### Acceptance Criteria

1. WHEN the source generator creates a table class, THE Generated_Code SHALL include Query(Expression&lt;Func&lt;TEntity, bool&gt;&gt; keyCondition) overload
2. WHEN the source generator creates a table class, THE Generated_Code SHALL include Query(Expression&lt;Func&lt;TEntity, bool&gt;&gt; keyCondition, Expression&lt;Func&lt;TEntity, bool&gt;&gt; filterCondition) overload
3. WHEN the source generator creates a table class, THE Generated_Code SHALL include Scan(Expression&lt;Func&lt;TEntity, bool&gt;&gt; filterCondition) overload
4. WHEN calling Query(x => x.Pk == id), THE System SHALL configure the key condition and return a QueryRequestBuilder&lt;TEntity&gt;
5. WHEN calling Query(x => x.Pk == id, x => x.Status == "ACTIVE"), THE System SHALL configure both key condition and filter, returning a QueryRequestBuilder&lt;TEntity&gt;

### Requirement 16: Sensitive Data Protection in Expression Logging

**User Story:** As a developer, I want properties marked with [Sensitive] to be redacted when logging LINQ expressions, so that sensitive data doesn't leak into logs.

#### Acceptance Criteria

1. WHEN logging a LINQ expression that references a [Sensitive] property, THE Logging_System SHALL redact the property value with "[REDACTED]"
2. WHEN logging expression translation, THE Logging_System SHALL preserve property names but redact values for sensitive properties
3. WHEN logging DynamoDB parameters generated from expressions, THE Logging_System SHALL redact values for parameters derived from sensitive properties
4. WHEN an expression contains both sensitive and non-sensitive properties, THE Logging_System SHALL redact only the sensitive values
5. THE Logging_System SHALL apply redaction consistently across text expressions, format string expressions, and LINQ expressions

### Requirement 17: Format String Support in LINQ Expressions

**User Story:** As a developer, I want format specifications from DynamoDbAttribute to apply when using LINQ expressions, so that formatting is consistent across all expression types.

#### Acceptance Criteria

1. WHEN a property has DynamoDbAttribute with a Format parameter, THE Expression_Translator SHALL apply the format when generating DynamoDB values
2. WHEN translating x => x.Timestamp == dateValue, THE Expression_Translator SHALL format dateValue according to the Timestamp property's DynamoDbAttribute format
3. WHEN a property uses a custom format string (e.g., "yyyy-MM-dd"), THE Expression_Translator SHALL apply it during value serialization
4. WHEN no format is specified, THE Expression_Translator SHALL use default serialization
5. THE Expression_Translator SHALL support the same format specifications as text-based format string expressions

### Requirement 18: Field-Level Encryption in Expressions

**User Story:** As a developer, I want to query encrypted fields using LINQ expressions, so that encryption is transparent in query operations.

#### Acceptance Criteria

1. WHEN a property is marked with [Encrypted], THE Expression_Translator SHALL generate the encrypted envelope value for comparison
2. WHEN translating x => x.SensitiveField == "value", THE Expression_Translator SHALL encrypt "value" using the same encryption context as storage operations
3. WHEN encryption uses deterministic encryption (if supported), THE Expression_Translator SHALL produce consistent encrypted values for queries
4. WHEN encryption uses non-deterministic encryption, THE Expression_Translator SHALL throw an exception indicating the field cannot be used in equality comparisons
5. THE Expression_Translator SHALL integrate with the IFieldEncryptor interface from the Encryption.Kms package

### Requirement 19: Index Query API Improvements

**User Story:** As a developer querying indexes, I want the same simplified API and LINQ expression support on indexes as on tables, so that index queries are as ergonomic as table queries.

#### Acceptance Criteria

1. WHEN the source generator creates index properties, THE Generated_Code SHALL include Query(Expression&lt;Func&lt;TEntity, bool&gt;&gt; keyCondition) overload
2. WHEN the source generator creates index properties, THE Generated_Code SHALL include Query(Expression&lt;Func&lt;TEntity, bool&gt;&gt; keyCondition, Expression&lt;Func&lt;TEntity, bool&gt;&gt; filterCondition) overload
3. WHEN calling index.Query&lt;TEntity&gt;(), THE System SHALL return a QueryRequestBuilder&lt;TEntity&gt; without requiring type parameters on chained methods
4. WHEN calling index.Query(x => x.Gsi1Pk == value), THE System SHALL configure the key condition and automatically set the IndexName
5. WHEN chaining methods on index queries, THE System SHALL follow the same type parameter simplification as table queries

### Requirement 20: Remove Non-Functional QueryAsync Methods

**User Story:** As a developer, I want to remove the placeholder QueryAsync methods from DynamoDbIndex&lt;TDefault&gt;, so that the API only exposes working functionality.

#### Acceptance Criteria

1. WHEN this feature is implemented, THE System SHALL remove QueryAsync(Action&lt;QueryRequestBuilder&gt;) from DynamoDbIndex&lt;TDefault&gt;
2. WHEN this feature is implemented, THE System SHALL remove QueryAsync&lt;TResult&gt;(Action&lt;QueryRequestBuilder&gt;) from DynamoDbIndex&lt;TDefault&gt;
3. WHEN developers need to query indexes, THE System SHALL provide Query() methods that return QueryRequestBuilder for fluent chaining
4. WHEN developers need to execute queries, THE System SHALL use the standard .ExecuteAsync() or .ToListAsync() terminal methods
5. THE System SHALL not provide callback-based query configuration methods that don't align with the fluent API pattern

### Requirement 21: Discriminator Usage in ToCompoundEntity

**User Story:** As a developer using discriminators for polymorphic entities, I want ToCompoundEntity() to respect discriminator values, so that entity type resolution works correctly.

#### Acceptance Criteria

1. WHEN calling ToCompoundEntity() on an item with a discriminator attribute, THE System SHALL use the discriminator value to determine the entity type
2. WHEN the discriminator value doesn't match any registered type, THE System SHALL throw a DiscriminatorMismatchException
3. WHEN no discriminator is present but one is expected, THE System SHALL throw an exception indicating missing discriminator
4. WHEN ToCompoundEntity() deserializes the item, THE System SHALL use the correct entity type's mapping logic
5. THE System SHALL validate discriminator configuration at table initialization time

### Requirement 22: Documentation Modernization

**User Story:** As a developer, I want documentation to reflect the current API patterns, so that examples and guides are accurate and helpful.

#### Acceptance Criteria

1. WHEN documentation shows DynamoDB operations, THE Examples SHALL use method-based syntax (Query(), Scan(), Get()) not property-based syntax
2. WHEN documentation shows query examples, THE Examples SHALL demonstrate LINQ expressions, format strings, and text expressions
3. WHEN documentation shows data operations, THE Examples SHALL demonstrate both simple and complex scenarios
4. WHEN documentation references API methods, THE References SHALL use current method signatures with correct type parameters
5. THE Documentation SHALL include migration guides for developers upgrading from older API versions

### Requirement 23: Comprehensive End-to-End Testing Strategy

**User Story:** As a library maintainer, I want comprehensive end-to-end tests for all operations and expression modes, so that I can ensure the library works correctly in real scenarios.

#### Acceptance Criteria

1. THE Test_Suite SHALL include end-to-end tests for Get, Put, Update, Delete, Query, Scan, Batch, and Transaction operations
2. WHEN testing each operation, THE Tests SHALL cover text expressions, format string expressions, and LINQ expressions
3. WHEN testing queries, THE Tests SHALL verify data round-trips correctly to and from DynamoDB
4. WHEN testing pagination, THE Tests SHALL verify all expression modes work with paginated results
5. THE Tests SHALL verify attribute name and value mappings work correctly across all operation types

### Requirement 24: Error Message Clarity

**User Story:** As a developer, I want clear error messages when type inference fails or expressions are invalid, so that I can quickly resolve issues.

#### Acceptance Criteria

1. WHEN type inference fails in a query chain, THE Compiler SHALL produce an error indicating where the type should be specified
2. WHEN mixing typed and untyped builders incorrectly, THE Compiler SHALL produce an error explaining the mismatch
3. WHEN using var with builder methods, THE System SHALL infer types correctly without requiring explicit declarations
4. WHEN IntelliSense is used, THE IDE SHALL show the inferred type at each step in the chain
5. THE System SHALL not produce ambiguous overload errors due to type parameter removal

### Requirement 25: Performance Considerations

**User Story:** As a developer, I want the simplified API to have no performance overhead, so that cleaner code doesn't sacrifice efficiency.

#### Acceptance Criteria

1. WHEN using the simplified API, THE System SHALL produce identical IL code to the previous type-parameter-heavy version
2. WHEN the JIT compiler optimizes the code, THE Performance SHALL be identical to previous versions
3. WHEN using AOT compilation, THE Generated_Code SHALL be identical in size and performance
4. THE System SHALL not introduce additional allocations due to generic type handling
5. THE System SHALL not introduce additional virtual method calls

### Requirement 26: Backward Compatibility Considerations

**User Story:** As a library maintainer, I want to understand the breaking changes, so that I can document migration paths clearly.

#### Acceptance Criteria

1. WHEN this feature is implemented, THE System SHALL remove all type parameters from chained methods (breaking change)
2. WHEN developers upgrade, THE Compiler SHALL produce clear errors indicating where type parameters should be removed
3. WHEN migration documentation is created, THE Documentation SHALL provide before/after examples for all common patterns
4. WHEN migration documentation is created, THE Documentation SHALL include regex patterns for automated refactoring
5. THE System SHALL not provide obsolete overloads since the codebase is greenfield
