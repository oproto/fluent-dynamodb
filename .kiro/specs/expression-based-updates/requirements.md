# Requirements Document

## Introduction

This specification defines requirements for adding type-safe, expression-based update operations to DynamoDB UpdateItem requests. Currently, the library supports expression-based syntax for Query().Where(), WithFilter(), and WithCondition() operations. This feature extends that capability to update operations, enabling developers to write type-safe updates with compile-time validation, IntelliSense support, and automatic parameter generation.

The implementation uses source-generated helper classes to provide a clean API that works within C# expression tree limitations while maintaining full type safety and AOT compatibility.

## Glossary

- **UpdateExpression**: A DynamoDB expression that specifies how to modify attributes in an item (e.g., "SET #name = :value, ADD #count :inc")
- **UpdateExpressionProperty<T>**: A wrapper type used in expressions to represent a property that can be updated, enabling extension methods for update operations
- **UpdateModel**: A source-generated class with nullable properties representing the values to set in an update operation
- **UpdateExpressions**: A source-generated class with UpdateExpressionProperty<T> properties used as the parameter in update expressions
- **SET Action**: A DynamoDB update action that sets attribute values (supports arithmetic, functions like if_not_exists, list_append)
- **ADD Action**: A DynamoDB update action that atomically increments numbers or adds elements to sets
- **REMOVE Action**: A DynamoDB update action that deletes entire attributes from an item
- **DELETE Action**: A DynamoDB update action that removes specific elements from sets
- **ExpressionTranslator**: The component that converts C# lambda expressions to DynamoDB expression syntax
- **UpdateItemRequestBuilder**: The fluent builder for constructing DynamoDB UpdateItem requests
- **EntityMetadata**: Runtime metadata about entity properties, including attribute names, formats, and encryption settings
- **SourceGenerator**: The Roslyn-based code generator that produces helper classes and metadata at compile time
- **AOT Compatibility**: Ahead-of-Time compilation compatibility, requiring no runtime code generation
- **MemberInitExpression**: A C# expression tree node representing object initialization syntax (new T { Prop = value })

## Requirements

### Requirement 1: Source-Generated Update Helper Classes

**User Story:** As a developer, I want the source generator to create helper classes for my entities, so that I can write type-safe update expressions with IntelliSense support.

#### Acceptance Criteria

1. WHEN THE SourceGenerator processes an entity with the DynamoDbTable attribute, THE Generator SHALL create a {EntityName}UpdateExpressions class with UpdateExpressionProperty<T> properties for each entity property
2. WHEN THE SourceGenerator processes an entity, THE Generator SHALL create a {EntityName}UpdateModel class with nullable versions of all entity properties
3. WHEN THE Generated classes are created, THE Classes SHALL be in the same namespace as the entity
4. WHEN THE Entity has properties with different types, THE UpdateExpressionProperty<T> SHALL use the appropriate generic type parameter
5. WHERE THE Entity has key properties (partition key, sort key), THE UpdateExpressions class SHALL still include them but validation will prevent their update

### Requirement 2: Expression-Based SET Operations

**User Story:** As a developer, I want to use C# lambda expressions to specify SET operations in update expressions, so that I can write type-safe updates with compile-time checking.

#### Acceptance Criteria

1. WHEN THE Developer calls Set() with a lambda expression returning an UpdateModel, THE UpdateItemRequestBuilder SHALL translate the expression to DynamoDB SET syntax
2. WHEN THE Developer assigns simple values in the UpdateModel initializer, THE ExpressionTranslator SHALL generate SET clauses with attribute names and value parameters
3. WHEN THE Developer assigns multiple properties in the UpdateModel, THE UpdateItemRequestBuilder SHALL combine them into a single SET clause separated by commas
4. WHEN THE EntityMetadata contains a format string for a property, THE ExpressionTranslator SHALL apply the format to the assigned value before creating the AttributeValue
5. WHERE THE Developer uses captured variables or constants as values, THE ExpressionTranslator SHALL evaluate them and generate parameter placeholders

### Requirement 3: Arithmetic Operations in SET

**User Story:** As a developer, I want to use arithmetic operators (+, -) in SET operations, so that I can increment or decrement values using DynamoDB's SET arithmetic syntax.

#### Acceptance Criteria

1. WHEN THE Developer uses the + operator with UpdateExpressionProperty in the expression, THE ExpressionTranslator SHALL generate SET syntax with addition (e.g., "SET #attr = #attr + :val")
2. WHEN THE Developer uses the - operator with UpdateExpressionProperty in the expression, THE ExpressionTranslator SHALL generate SET syntax with subtraction (e.g., "SET #attr = #attr - :val")
3. WHEN THE Arithmetic operation is used, THE ExpressionTranslator SHALL validate that the property type is numeric
4. WHEN THE Developer combines arithmetic with other SET operations, THE ExpressionTranslator SHALL generate a single SET clause with comma-separated operations
5. WHERE THE Property type is not numeric, THE ExpressionTranslator SHALL throw an UnsupportedExpressionException with a descriptive error message

### Requirement 4: Atomic ADD Operations

**User Story:** As a developer, I want to use the Add() extension method for atomic increment/decrement operations, so that I can perform thread-safe numeric updates and set additions.

#### Acceptance Criteria

1. WHEN THE Developer calls Add() on an UpdateExpressionProperty<int> or other numeric type, THE ExpressionTranslator SHALL generate an ADD action (e.g., "ADD #attr :val")
2. WHEN THE Developer specifies a negative value in Add(), THE ExpressionTranslator SHALL generate an ADD action with the negative value for atomic decrement
3. WHEN THE Developer calls Add() on an UpdateExpressionProperty<HashSet<T>>, THE ExpressionTranslator SHALL generate an ADD action for set union
4. WHEN THE Multiple Add() operations are used, THE ExpressionTranslator SHALL generate a single ADD clause with comma-separated operations
5. WHERE THE Property type does not support ADD operations, THE Extension method SHALL not be available (enforced by generic type constraints)

### Requirement 5: REMOVE Operations

**User Story:** As a developer, I want to use the Remove() extension method to delete attributes, so that I can remove fields with type safety and compile-time validation.

#### Acceptance Criteria

1. WHEN THE Developer calls Remove() on an UpdateExpressionProperty, THE ExpressionTranslator SHALL generate a REMOVE action (e.g., "REMOVE #attr")
2. WHEN THE Developer specifies multiple properties to remove, THE ExpressionTranslator SHALL generate a single REMOVE clause with comma-separated attribute names
3. WHEN THE Developer attempts to remove a partition key or sort key property, THE ExpressionTranslator SHALL throw an InvalidUpdateOperationException with a descriptive error message
4. WHEN THE EntityMetadata is available, THE ExpressionTranslator SHALL validate that the property exists and is mapped to a DynamoDB attribute
5. WHERE THE Developer removes nested attributes, THE ExpressionTranslator SHALL generate the appropriate path expression

### Requirement 6: DELETE Operations for Sets

**User Story:** As a developer, I want to use the Delete() extension method to remove specific elements from sets, so that I can perform partial set updates with type safety.

#### Acceptance Criteria

1. WHEN THE Developer calls Delete() on an UpdateExpressionProperty<HashSet<T>>, THE ExpressionTranslator SHALL generate a DELETE action (e.g., "DELETE #attr :val")
2. WHEN THE Developer specifies multiple elements to delete, THE ExpressionTranslator SHALL include all elements in the value set
3. WHEN THE Property type is not a set type, THE Delete extension method SHALL not be available (enforced by generic type constraints)
4. WHEN THE Multiple Delete() operations are used, THE ExpressionTranslator SHALL generate a single DELETE clause with comma-separated operations
5. WHERE THE Set is empty after deletion, THE Attribute SHALL remain as an empty set (different from REMOVE which deletes the attribute)

### Requirement 7: DynamoDB Functions Support

**User Story:** As a developer, I want to use DynamoDB functions like if_not_exists and list_append in my update expressions, so that I can leverage DynamoDB's built-in functionality.

#### Acceptance Criteria

1. WHEN THE Developer calls IfNotExists() on an UpdateExpressionProperty, THE ExpressionTranslator SHALL generate SET syntax with if_not_exists function (e.g., "SET #attr = if_not_exists(#attr, :val)")
2. WHEN THE Developer calls ListAppend() on an UpdateExpressionProperty<List<T>>, THE ExpressionTranslator SHALL generate SET syntax with list_append function
3. WHEN THE Developer calls ListPrepend() on an UpdateExpressionProperty<List<T>>, THE ExpressionTranslator SHALL generate SET syntax with list_append in reverse order
4. WHEN THE Function is used with an incompatible type, THE Extension method SHALL not be available (enforced by generic type constraints)
5. WHERE THE Function parameters are invalid, THE ExpressionTranslator SHALL throw an UnsupportedExpressionException with a descriptive error message

### Requirement 8: Format String Application

**User Story:** As a developer, I want format strings defined in entity metadata to be automatically applied to update expression values, so that dates, numbers, and other types are consistently formatted.

#### Acceptance Criteria

1. WHEN THE EntityMetadata specifies a format string for a property, THE ExpressionTranslator SHALL apply the format to values assigned to that property
2. WHEN THE Format string is "o" for a DateTime property, THE ExpressionTranslator SHALL format the value as ISO 8601
3. WHEN THE Format string is a numeric format like "F2", THE ExpressionTranslator SHALL format decimal values with the specified precision
4. WHEN THE Format string is invalid for the value type, THE ExpressionTranslator SHALL throw a FormatException with a descriptive error message
5. WHERE THE Property has no format string, THE ExpressionTranslator SHALL use the default conversion to AttributeValue

### Requirement 9: Field-Level Encryption Support

**User Story:** As a developer, I want values assigned to encrypted properties to be automatically encrypted before being sent to DynamoDB, so that sensitive data is protected at rest.

#### Acceptance Criteria

1. WHEN THE EntityMetadata indicates a property has the Encrypted attribute, THE ExpressionTranslator SHALL encrypt the value before creating the AttributeValue
2. WHEN THE IFieldEncryptor is available in the operation context, THE ExpressionTranslator SHALL use it to encrypt field values
3. WHEN THE Developer assigns a value to an encrypted property in an update expression, THE Value SHALL be encrypted transparently without additional code
4. WHEN THE Encryption operation fails, THE ExpressionTranslator SHALL throw a FieldEncryptionException with details about the failure
5. WHERE THE Property is marked as encrypted but no IFieldEncryptor is configured, THE ExpressionTranslator SHALL throw an EncryptionRequiredException with a descriptive error message

### Requirement 10: Validation and Error Handling

**User Story:** As a developer, I want clear error messages when I use unsupported expression patterns in update operations, so that I can quickly identify and fix issues.

#### Acceptance Criteria

1. WHEN THE Developer uses an unsupported expression pattern, THE ExpressionTranslator SHALL throw an UnsupportedExpressionException with a description of the issue
2. WHEN THE Developer references a property that is not mapped to a DynamoDB attribute, THE ExpressionTranslator SHALL throw an UnmappedPropertyException
3. WHEN THE Developer attempts to update a partition key or sort key, THE ExpressionTranslator SHALL throw an InvalidUpdateOperationException
4. WHEN THE Developer uses method calls that are not recognized update operations, THE ExpressionTranslator SHALL throw an UnsupportedExpressionException
5. WHERE THE Expression contains syntax errors, THE ExpressionTranslator SHALL provide error messages that include the problematic expression text

### Requirement 11: Extension Method Type Safety

**User Story:** As a developer, I want extension methods to only be available for appropriate property types, so that I cannot accidentally use incompatible operations.

#### Acceptance Criteria

1. WHEN THE Property type is numeric, THE Add extension method SHALL be available for atomic increment/decrement
2. WHEN THE Property type is a set (HashSet<T>), THE Delete extension method SHALL be available
3. WHEN THE Property type is a list (List<T>), THE ListAppend and ListPrepend extension methods SHALL be available
4. WHEN THE Property type does not support an operation, THE Extension method SHALL not appear in IntelliSense
5. WHERE THE Developer attempts to use an incompatible operation, THE Code SHALL not compile with a clear error message

### Requirement 12: Backward Compatibility

**User Story:** As a developer, I want the existing string-based update expression methods to continue working unchanged, so that I can adopt the new feature incrementally without breaking existing code.

#### Acceptance Criteria

1. WHEN THE Developer uses the existing Set(string) method, THE Method SHALL continue to work exactly as before
2. WHEN THE Developer uses the existing Set(string, params object[]) method with format strings, THE Method SHALL continue to work exactly as before
3. WHEN THE Developer mixes string-based and expression-based methods in the same builder, THE Builder SHALL combine them correctly into a single update expression
4. WHEN THE Existing tests for string-based updates are run, THE Tests SHALL pass without modification
5. WHERE THE Developer has existing code using update expressions, THE Code SHALL compile and run without changes

### Requirement 13: AOT Compatibility

**User Story:** As a developer, I want the expression-based update feature to work with Native AOT compilation, so that I can use it in AOT-compiled applications.

#### Acceptance Criteria

1. WHEN THE Application is compiled with Native AOT, THE Expression-based update operations SHALL work without runtime code generation
2. WHEN THE Source generator creates helper classes, THE Classes SHALL be fully AOT-compatible
3. WHEN THE ExpressionTranslator analyzes expressions, THE Analysis SHALL use only static code paths without reflection
4. WHEN THE UpdateExpressionProperty<T> type is used, THE Type SHALL not require runtime type information
5. WHERE THE Feature uses generic types, THE Generics SHALL be resolved at compile time

### Requirement 14: IntelliSense and Developer Experience

**User Story:** As a developer, I want comprehensive IntelliSense support for update expressions, so that I can discover available operations and write correct code quickly.

#### Acceptance Criteria

1. WHEN THE Developer types "x." in the update expression, THE IntelliSense SHALL show all available properties from the UpdateExpressions class
2. WHEN THE Developer types "x.PropertyName." on a numeric property, THE IntelliSense SHALL show the Add() extension method
3. WHEN THE Developer types "x.PropertyName." on a set property, THE IntelliSense SHALL show the Delete() extension method
4. WHEN THE Developer hovers over an extension method, THE Tooltip SHALL show comprehensive documentation with examples
5. WHERE THE Property type does not support an operation, THE Operation SHALL not appear in IntelliSense

### Requirement 15: XML Documentation

**User Story:** As a developer, I want comprehensive XML documentation for all expression-based update methods and classes, so that I can understand how to use them and see examples in IntelliSense.

#### Acceptance Criteria

1. WHEN THE Developer views the Set() extension method in IntelliSense, THE Documentation SHALL include a summary, parameter descriptions, return value description, and examples
2. WHEN THE Documentation includes examples, THE Examples SHALL demonstrate common update patterns including SET, ADD, REMOVE, and DELETE operations
3. WHEN THE Documentation describes supported expression patterns, THE Description SHALL list valid operators and method calls
4. WHEN THE Documentation describes error conditions, THE Description SHALL list all possible exceptions with their causes
5. WHERE THE Extension methods are documented, THE Documentation SHALL clearly explain which property types support each operation
