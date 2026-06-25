# Requirements Document

## Introduction

This feature normalizes all computed field configurations into a single format string at compile time in the source generator, eliminating redundant runtime fields (Separator, Prefix, PrefixSeparator) from `ComputedFieldMetadata`. At runtime, all computed field recomputation paths use `string.Format(format, values)` exclusively, aligning the update path with the existing Put and Key builder paths.

## Glossary

- **Source_Generator**: The Roslyn incremental source generator in `Oproto.FluentDynamoDb.SourceGenerator` that analyzes entity attributes at compile time and emits mapper, keys, and metadata code
- **ComputedFieldMetadata**: The runtime metadata class describing a computed field's source properties and how to reconstruct the computed value
- **ComputedAttribute**: The user-facing attribute (`[Computed(...)]`) applied to entity properties to declare computed field configurations
- **ComputedKeyModel**: The internal source generator model that holds parsed computed key information during code generation
- **MapperGenerator**: The source generator component that emits `PropertyMetadata` initialization code including `ComputedFieldMetadata` instances
- **UpdateExpressionTranslator**: The runtime component that translates lambda-based update expressions into DynamoDB update expressions, including computed field recomputation
- **KeysGenerator**: The source generator component that emits key builder methods (`Keys.BuildPk()`, etc.) using format strings
- **Format_String**: A .NET composite format string (e.g., `"{0}#{1}"`) compatible with `string.Format()`

## Requirements

### Requirement 1: Compile-Time Format String Generation

**User Story:** As a library maintainer, I want the source generator to translate all Separator/Prefix/PrefixSeparator combinations into a single format string at compile time, so that runtime metadata is simpler and all code paths produce identical output.

#### Acceptance Criteria

1. WHEN a ComputedAttribute specifies Separator without Format and the property has more than one source property, THE Source_Generator SHALL produce a format string by interleaving the Separator between positional placeholders for each source property (e.g., Separator="#", 2 sources → "{0}#{1}", Separator="#", 3 sources → "{0}#{1}#{2}")
2. WHEN a ComputedAttribute specifies Separator without Format and the property has exactly one source property, THE Source_Generator SHALL produce the format string "{0}" with no separator characters
3. WHEN a ComputedAttribute specifies Separator and the property has a key prefix configured via its PartitionKey or SortKey attribute, THE Source_Generator SHALL prepend the key attribute's Prefix value followed by the key attribute's Separator value to the generated format string (e.g., PartitionKey Prefix="ORDER" with Separator="#", ComputedAttribute Separator="#", 2 sources → "ORDER#{0}#{1}")
4. WHEN a ComputedAttribute specifies an explicit Format property, THE Source_Generator SHALL emit that Format value unchanged as the format string in ComputedFieldMetadata
5. WHEN a ComputedAttribute specifies both Format and Separator, THE Source_Generator SHALL use the Format value and ignore Separator
6. THE Source_Generator SHALL produce format strings containing exactly N sequential positional placeholders ({0} through {N-1}) where N equals the number of source properties declared in the ComputedAttribute constructor
7. IF a ComputedAttribute specifies an explicit Format property whose placeholder count does not equal the number of source properties, THEN THE Source_Generator SHALL emit a compile-time diagnostic error identifying the mismatch between placeholder count and source property count

### Requirement 2: Simplified Runtime Metadata

**User Story:** As a library maintainer, I want ComputedFieldMetadata to carry only SourceProperties and Format, so that there is a single authoritative representation of how to reconstruct a computed value.

#### Acceptance Criteria

1. THE ComputedFieldMetadata class SHALL contain a SourceProperties property of type string array with a default value of an empty array
2. THE ComputedFieldMetadata class SHALL contain a Format property of type non-nullable string with a default value of "{0}"
3. THE ComputedFieldMetadata class SHALL NOT contain Separator, Prefix, or PrefixSeparator properties
4. WHEN the Source_Generator emits a ComputedFieldMetadata instance, THE Source_Generator SHALL set the Format property to a non-null, non-empty format string containing at least one positional placeholder
5. WHEN the Source_Generator emits a ComputedFieldMetadata instance, THE Source_Generator SHALL set SourceProperties to an array containing at least 1 element

### Requirement 3: Unified Runtime Recomputation

**User Story:** As a library maintainer, I want all runtime computed field recomputation to use `string.Format(format, values)`, so that Put, Update, and Key operations produce identical results for the same computed field configuration.

#### Acceptance Criteria

1. WHEN the UpdateExpressionTranslator recomputes a computed field value, THE UpdateExpressionTranslator SHALL call string.Format with the ComputedFieldMetadata Format property as the first argument and an object array of source property values ordered by their position in SourceProperties as the second argument
2. WHEN the UpdateExpressionTranslator recomputes a computed field value, THE UpdateExpressionTranslator SHALL NOT use string.Join, StringBuilder concatenation, or manual prefix prepend logic
3. WHEN all source properties of a computed field are assigned in an update expression, THE UpdateExpressionTranslator SHALL produce a recomputed string value byte-for-byte identical to what the MapperGenerator Put path produces for the same source values
4. WHEN a source property value is null, THE UpdateExpressionTranslator SHALL substitute string.Empty for that positional argument in the format string before calling string.Format

### Requirement 4: Backwards Compatibility

**User Story:** As a library consumer, I want my existing ComputedAttribute configurations using Separator to continue working without code changes, so that this internal refactoring does not break my entities.

#### Acceptance Criteria

1. THE ComputedAttribute class SHALL continue to expose Separator as a public settable property of type string with default value "#"
2. THE ComputedAttribute class SHALL continue to expose Format as a public settable property of type nullable string with default value null
3. THE ComputedAttribute class SHALL continue to accept a params string[] constructor parameter for source properties without changes to the constructor signature
4. WHEN a library consumer upgrades to the new version without modifying their entity definitions, THE Source_Generator SHALL produce computed values that are byte-for-byte identical to the values produced by the previous version for the same source property inputs
5. THE ComputedAttribute class SHALL NOT introduce any new required constructor parameters or remove any existing public properties

### Requirement 5: Cross-Operation Consistency

**User Story:** As a library consumer, I want Put operations, Update operations, and Key builder operations to produce identical computed values for the same entity configuration, so that data integrity is maintained across all DynamoDB operations.

#### Acceptance Criteria

1. FOR a ComputedAttribute configuration using Separator (without Format), formatting a computed value via the Keys builder path, the Put mapper path, and the Update recomputation path SHALL produce byte-for-byte identical string results given the same ordered source property string values
2. FOR a ComputedAttribute configuration using an explicit Format property, formatting a computed value via the Keys builder path, the Put mapper path, and the Update recomputation path SHALL produce byte-for-byte identical string results given the same ordered source property string values
3. WHEN a ComputedAttribute uses Format = "TENANT#{0}#USER#{1}#" with source values "tenantValue" and "userValue", all three paths (Keys builder, Put mapper, Update recomputation) SHALL produce the string "TENANT#tenantValue#USER#userValue#"
4. WHEN any source property value is null or empty, all three paths SHALL produce identical output by substituting string.Empty for that positional argument

### Requirement 6: Source Generator Format String Emission

**User Story:** As a library maintainer, I want the MapperGenerator to emit the computed format string directly into the ComputedFieldMetadata initialization, so that the runtime metadata is immediately usable without further translation.

#### Acceptance Criteria

1. WHEN the MapperGenerator emits a ComputedFieldMetadata instance in generated code, THE MapperGenerator SHALL emit the Format property assignment with the pre-computed format string as a verbatim or escaped C# string literal
2. WHEN the MapperGenerator emits a ComputedFieldMetadata instance in generated code, THE MapperGenerator SHALL NOT emit assignments for Separator, Prefix, or PrefixSeparator properties
3. WHEN the format string contains characters requiring C# string escaping (backslash, quotes, curly braces as literal text), THE MapperGenerator SHALL emit a properly escaped string literal that compiles without error and evaluates to the intended format string at runtime

### Requirement 7: Format String Parser Round-Trip

**User Story:** As a library maintainer, I want the format string generation to be verifiable by round-trip testing, so that the compile-time translation is provably correct.

#### Acceptance Criteria

1. FOR a ComputedAttribute configuration with N source properties (N ≥ 1) and a Separator, applying string.Format with the generated format string and N source values (converted via ToString()) SHALL produce the same result as string.Join(Separator, sourceValues)
2. FOR a ComputedAttribute configuration with a key Prefix, key Separator (as PrefixSeparator), and ComputedAttribute Separator, applying string.Format with the generated format string and N source values SHALL produce the same result as Prefix + PrefixSeparator + string.Join(Separator, sourceValues)
3. FOR a ComputedAttribute configuration with an explicit Format, applying string.Format with that Format and N source values (converted via ToString()) SHALL produce the same string as calling string.Format(Format, sourceValues) directly
