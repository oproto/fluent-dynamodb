# Requirements Document

## Introduction

This feature adds support for unsigned integer types (`ulong`, `uint`, `ushort`, `byte`, and `sbyte`) to the FluentDynamoDb source generator. Currently, the source generator reports DYNDB009 ("Unsupported property type") when entities contain properties of these types, preventing their use in DynamoDB entities.

## Glossary

- **Source_Generator**: The Roslyn-based code generator that analyzes entity classes and generates mapping code
- **EntityAnalyzer**: The component that validates entity properties and reports diagnostics
- **MapperGenerator**: The component that generates ToDynamoDb and FromDynamoDb mapping code
- **AttributeValue**: The AWS SDK type representing a DynamoDB attribute value
- **Unsigned_Integer_Types**: The .NET numeric types `ulong` (UInt64), `uint` (UInt32), `ushort` (UInt16), and `byte` (Byte)
- **Signed_Byte**: The .NET type `sbyte` (SByte), an 8-bit signed integer

## Requirements

### Requirement 1: Support ulong Properties

**User Story:** As a developer, I want to use `ulong` properties in my DynamoDB entities, so that I can store unsigned 64-bit integer values like version numbers or large counters.

#### Acceptance Criteria

1. WHEN an entity has a property of type `ulong`, THE Source_Generator SHALL accept it without reporting DYNDB009
2. WHEN serializing a `ulong` property to DynamoDB, THE MapperGenerator SHALL generate code that stores it as a Number (N) attribute
3. WHEN deserializing a `ulong` property from DynamoDB, THE MapperGenerator SHALL generate code that parses the Number (N) attribute using `ulong.Parse()`
4. WHEN a `ulong?` (nullable) property is null, THE MapperGenerator SHALL skip the attribute during serialization
5. WHEN a `ulong` property is used in a collection (List, HashSet), THE MapperGenerator SHALL correctly serialize and deserialize each element

### Requirement 2: Support uint Properties

**User Story:** As a developer, I want to use `uint` properties in my DynamoDB entities, so that I can store unsigned 32-bit integer values.

#### Acceptance Criteria

1. WHEN an entity has a property of type `uint`, THE Source_Generator SHALL accept it without reporting DYNDB009
2. WHEN serializing a `uint` property to DynamoDB, THE MapperGenerator SHALL generate code that stores it as a Number (N) attribute
3. WHEN deserializing a `uint` property from DynamoDB, THE MapperGenerator SHALL generate code that parses the Number (N) attribute using `uint.Parse()`
4. WHEN a `uint?` (nullable) property is null, THE MapperGenerator SHALL skip the attribute during serialization
5. WHEN a `uint` property is used in a collection (List, HashSet), THE MapperGenerator SHALL correctly serialize and deserialize each element

### Requirement 3: Support ushort Properties

**User Story:** As a developer, I want to use `ushort` properties in my DynamoDB entities, so that I can store unsigned 16-bit integer values.

#### Acceptance Criteria

1. WHEN an entity has a property of type `ushort`, THE Source_Generator SHALL accept it without reporting DYNDB009
2. WHEN serializing a `ushort` property to DynamoDB, THE MapperGenerator SHALL generate code that stores it as a Number (N) attribute
3. WHEN deserializing a `ushort` property from DynamoDB, THE MapperGenerator SHALL generate code that parses the Number (N) attribute using `ushort.Parse()`
4. WHEN a `ushort?` (nullable) property is null, THE MapperGenerator SHALL skip the attribute during serialization
5. WHEN a `ushort` property is used in a collection (List, HashSet), THE MapperGenerator SHALL correctly serialize and deserialize each element

### Requirement 4: Support byte Properties

**User Story:** As a developer, I want to use `byte` properties in my DynamoDB entities, so that I can store unsigned 8-bit integer values (distinct from `byte[]` binary data).

#### Acceptance Criteria

1. WHEN an entity has a property of type `byte`, THE Source_Generator SHALL accept it without reporting DYNDB009
2. WHEN serializing a `byte` property to DynamoDB, THE MapperGenerator SHALL generate code that stores it as a Number (N) attribute
3. WHEN deserializing a `byte` property from DynamoDB, THE MapperGenerator SHALL generate code that parses the Number (N) attribute using `byte.Parse()`
4. WHEN a `byte?` (nullable) property is null, THE MapperGenerator SHALL skip the attribute during serialization
5. WHEN a `byte` property is used in a collection (List, HashSet), THE MapperGenerator SHALL correctly serialize and deserialize each element

### Requirement 5: Support sbyte Properties

**User Story:** As a developer, I want to use `sbyte` properties in my DynamoDB entities, so that I can store signed 8-bit integer values.

#### Acceptance Criteria

1. WHEN an entity has a property of type `sbyte`, THE Source_Generator SHALL accept it without reporting DYNDB009
2. WHEN serializing an `sbyte` property to DynamoDB, THE MapperGenerator SHALL generate code that stores it as a Number (N) attribute
3. WHEN deserializing an `sbyte` property from DynamoDB, THE MapperGenerator SHALL generate code that parses the Number (N) attribute using `sbyte.Parse()`
4. WHEN an `sbyte?` (nullable) property is null, THE MapperGenerator SHALL skip the attribute during serialization
5. WHEN an `sbyte` property is used in a collection (List, HashSet), THE MapperGenerator SHALL correctly serialize and deserialize each element

### Requirement 6: Support short Properties

**User Story:** As a developer, I want to use `short` properties in my DynamoDB entities, so that I can store signed 16-bit integer values.

#### Acceptance Criteria

1. WHEN an entity has a property of type `short`, THE Source_Generator SHALL accept it without reporting DYNDB009
2. WHEN serializing a `short` property to DynamoDB, THE MapperGenerator SHALL generate code that stores it as a Number (N) attribute
3. WHEN deserializing a `short` property from DynamoDB, THE MapperGenerator SHALL generate code that parses the Number (N) attribute using `short.Parse()`
4. WHEN a `short?` (nullable) property is null, THE MapperGenerator SHALL skip the attribute during serialization
5. WHEN a `short` property is used in a collection (List, HashSet), THE MapperGenerator SHALL correctly serialize and deserialize each element

### Requirement 7: Serialization Round-Trip Consistency

**User Story:** As a developer, I want serialization and deserialization of unsigned integer types to be lossless, so that I can trust my data integrity.

#### Acceptance Criteria

1. FOR ALL valid values of each unsigned integer type, serializing then deserializing SHALL produce the original value (round-trip property)
2. FOR ALL boundary values (0, max value for each type), serializing then deserializing SHALL produce the original value
