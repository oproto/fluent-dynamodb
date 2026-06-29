# Requirements Document

## Introduction

This feature adds support for .NET format specifiers in computed field format strings. Currently, placeholders like `{0:yyyy-MM-dd}` or `{0:D4}` break multiple code paths in the source generator and runtime because the system assumes all placeholders are simple `{N}` patterns. This feature fixes the broken paths (discriminator regex, placeholder validation, keys builder pre-stringification, update recomputation pre-stringification) and adds an enhancement for source property Format attribute fallback.

## Glossary

- **Source_Generator**: The Roslyn incremental source generator in `Oproto.FluentDynamoDb.SourceGenerator` that analyzes entity attributes at compile time and emits mapper, keys, and metadata code
- **Format_Specifier**: The portion of a .NET composite format placeholder after the colon (e.g., `yyyy-MM-dd` in `{0:yyyy-MM-dd}`) that controls how `string.Format` renders the corresponding argument via `IFormattable`
- **Placeholder**: A positional substitution token in a .NET composite format string, either simple (`{0}`) or with a format specifier (`{0:yyyy-MM-dd}`)
- **ComputedAttribute**: The user-facing attribute (`[Computed(...)]`) applied to entity properties to declare computed field configurations including source properties and Format
- **DynamoDbAttribute_Format**: The `Format` named property on `[DynamoDbAttribute]` that specifies a formatting pattern for individual property serialization (e.g., `[DynamoDbAttribute("date", Format = "yyyy-MM-dd")]`)
- **EntityAnalyzer**: The source generator component that analyzes entity definitions, derives discriminator patterns, and validates computed key format strings
- **KeysGenerator**: The source generator component that emits key builder methods (`Keys.BuildPk()`, etc.) using format strings
- **MapperGenerator**: The source generator component that emits `ToDynamoDb` and `FromDynamoDb` mapping code including computed key logic
- **UpdateExpressionTranslator**: The runtime component that translates lambda-based update expressions into DynamoDB update expressions, including computed field recomputation
- **ComputedFieldMetadata**: The runtime metadata class describing a computed field's source properties and format string for recomputation
- **Discriminator_Pattern**: A glob-style pattern (using `*` wildcards) derived from a computed field format string, used to identify entity types by matching key values against known patterns
- **Pre_Stringification**: The practice of converting typed values to strings (via `.ToString()` or custom format) before passing them to `string.Format`, which prevents format specifiers from being applied by `string.Format`
- **IFormattable**: The .NET interface (implemented by DateTime, DateOnly, int, decimal, enums, etc.) that enables `string.Format` to apply format specifiers to typed values

## Requirements

### Requirement 1: Discriminator Pattern Derivation with Format Specifiers

**User Story:** As a library consumer, I want computed fields with format specifiers in their format strings to produce correct discriminator patterns, so that entity type detection works regardless of placeholder complexity.

#### Acceptance Criteria

1. WHEN the EntityAnalyzer derives a discriminator pattern from a format string containing placeholders with format specifiers (e.g., `{0:yyyy-MM-dd}#{1}`), THE EntityAnalyzer SHALL replace each placeholder including its format specifier with a wildcard character `*` (producing `*#*`)
2. WHEN the EntityAnalyzer derives a discriminator pattern from a format string containing a mix of simple placeholders and placeholders with format specifiers (e.g., `{0:D4}#{1}`), THE EntityAnalyzer SHALL replace all placeholders with wildcard characters regardless of whether they contain format specifiers
3. THE EntityAnalyzer discriminator pattern derivation regex SHALL match placeholders of the form `{N}` and `{N:format}` where N is one or more decimal digits (`\d+`) and format is zero or more characters that are not `}` (`[^}]*`)
4. WHEN the EntityAnalyzer derives a discriminator pattern from a format string containing only simple placeholders (e.g., `{0}#{1}`), THE EntityAnalyzer SHALL produce the same result as the current behavior (replacing each `{N}` with `*`)
5. WHEN the derived discriminator pattern starts with `*` (indicating the first segment is variable), THE EntityAnalyzer SHALL return null for the derived pattern to indicate that discrimination by prefix is not possible

### Requirement 2: Placeholder Count Validation with Format Specifiers

**User Story:** As a library consumer, I want the source generator to correctly count placeholders in format strings that use format specifiers, so that I do not receive false diagnostic errors when using valid format strings.

#### Acceptance Criteria

1. WHEN the EntityAnalyzer validates a computed key format string containing placeholders with format specifiers (e.g., `{0:yyyy-MM-dd}#{1}`), THE EntityAnalyzer SHALL extract the numeric index portion before the first colon to determine the placeholder index
2. WHEN the EntityAnalyzer validates a format string `{0:yyyy-MM-dd}#{1}` with 2 source properties, THE EntityAnalyzer SHALL determine the placeholder count as 2 and SHALL NOT emit diagnostic FDDB090
3. WHEN the EntityAnalyzer validates a format string with mismatched placeholder count (e.g., `{0:D4}` with 2 source properties), THE EntityAnalyzer SHALL emit diagnostic FDDB090 indicating 1 placeholder but 2 source properties
4. WHEN a placeholder text contains a colon (e.g., `0:yyyy-MM-dd`), THE EntityAnalyzer SHALL parse the substring before the first colon as the placeholder index, supporting format specifiers that themselves contain colons (e.g., `{0:HH:mm:ss}` where the index is `0`)
5. WHEN a placeholder text before the first colon is not a valid non-negative integer (e.g., `{abc:format}`), THE EntityAnalyzer SHALL treat it as an invalid placeholder and emit a diagnostic indicating an invalid placeholder format

### Requirement 3: Keys Builder Typed Value Preservation

**User Story:** As a library consumer, I want computed key builders to pass typed values to `string.Format` when the format string contains format specifiers, so that format specifiers like `{0:yyyy-MM-dd}` render correctly using the value's `IFormattable` implementation.

#### Acceptance Criteria

1. WHEN the KeysGenerator emits a key builder method for a computed format containing at least one placeholder with a format specifier (e.g., `{0:yyyy-MM-dd}`), THE KeysGenerator SHALL pass the source property value for that placeholder index to `string.Format` as its original typed value cast to `object`, without calling `.ToString()` or applying any pre-stringification expression
2. WHEN the KeysGenerator emits a key builder method for a computed format where a specific placeholder index has a format specifier (e.g., index 0 in `{0:yyyy-MM-dd}#{1}`), THE KeysGenerator SHALL pass only the source property at that index as its typed value cast to `object`, while source properties at placeholder indices without format specifiers (e.g., index 1 in `{0:yyyy-MM-dd}#{1}`) SHALL continue to use existing pre-stringification value expression logic
3. WHEN the KeysGenerator emits a key builder method for a computed format containing only simple placeholders without format specifiers (e.g., `{0}#{1}`), THE KeysGenerator SHALL apply existing value expression logic (pre-stringification via `GetValueExpression`) to all arguments for backwards compatibility
4. WHEN a source property of type `DateOnly` is used in a format string with specifier `{0:yyyy-MM-dd}`, THE KeysGenerator SHALL emit code that passes the DateOnly value to `string.Format` so that the DateOnly.IFormattable implementation applies the format specifier
5. IF the computed format string contains a format specifier at a placeholder index (e.g., `{0:yyyy-MM-dd}`) and the source property at that index also has a `DynamoDbAttribute.Format` value defined, THEN THE KeysGenerator SHALL use the format specifier from the computed format string and ignore the source property's `DynamoDbAttribute.Format` for that placeholder

### Requirement 4: Update Recomputation Typed Value Preservation

**User Story:** As a library consumer, I want computed field recomputation during Update operations to pass typed values to `string.Format` when the format string contains format specifiers, so that format specifiers render correctly at runtime.

#### Acceptance Criteria

1. WHEN the UpdateExpressionTranslator recomputes a computed field whose format string contains at least one placeholder matching the pattern `{N:specifier}` (where N is a digit and specifier is one or more characters after the colon), THE UpdateExpressionTranslator SHALL pass each source property value as its original typed value (boxed to object) to `string.Format` without calling `.ToString()` first, regardless of whether that specific positional argument has a format specifier
2. WHEN the UpdateExpressionTranslator recomputes a computed field whose format string contains only simple placeholders matching the pattern `{N}` (no colon or format specifier present in any placeholder), THE UpdateExpressionTranslator SHALL convert source property values to strings via `.ToString()` before passing to `string.Format`
3. IF a source property value is null during recomputation of a computed field whose format string contains at least one placeholder with a format specifier, THEN THE UpdateExpressionTranslator SHALL substitute an empty string for that positional argument
4. WHEN the format string is `{0:yyyy-MM-dd}#{1}` and source property at index 0 is a DateTime with value 2024-03-15 and source property at index 1 is the string "CategoryA", THE UpdateExpressionTranslator SHALL produce the recomputed value `2024-03-15#CategoryA`

### Requirement 5: Cross-Operation Consistency with Format Specifiers

**User Story:** As a library consumer, I want Put operations, Update operations, and Key builder operations to produce identical computed values when the format string contains format specifiers, so that data integrity is maintained regardless of which code path writes the value.

#### Acceptance Criteria

1. WHEN a ComputedAttribute has format `{0:yyyy-MM-dd}#{1}` and source values are (DateOnly 2024-03-15, string "CATEGORY"), THE Keys builder path, Put mapper path, and Update recomputation path SHALL each produce the string `2024-03-15#CATEGORY`
2. WHEN a ComputedAttribute has format `{0:D4}#{1}` and source values are (int 42, string "Name"), THE Keys builder path, Put mapper path, and Update recomputation path SHALL each produce the string `0042#Name`
3. WHEN a ComputedAttribute has format `{0:G}#{1}` and a source property is an enum type with value representing "Active" and a string "id123", THE Keys builder path, Put mapper path, and Update recomputation path SHALL each produce the string `Active#id123`
4. WHEN a computed format contains format specifiers, THE system SHALL invoke `string.Format` using `CultureInfo.InvariantCulture` with typed (non-pre-stringified) source values in all three operation paths (Keys, Put, Update) so that `IFormattable` implementations produce identical output regardless of the host machine's locale
5. IF a source property value used in a computed format is null, THEN THE system SHALL substitute an empty string for that placeholder position before invoking `string.Format`
6. WHEN a ComputedAttribute format contains placeholders with format specifiers (e.g., `{0:yyyy-MM-dd}`), THE discriminator pattern derivation and placeholder count validation SHALL correctly parse the index portion before the colon, treating `{N:format}` as a single placeholder at index N

### Requirement 6: Source Property Format Attribute Fallback

**User Story:** As a library consumer, I want computed fields to automatically use the source property's `DynamoDbAttribute.Format` as a fallback format specifier when the computed format placeholder has no explicit specifier, so that I do not need to repeat format information in the computed format string.

#### Acceptance Criteria

1. WHEN a computed format placeholder has no format specifier (e.g., `{0}`) and the corresponding source property has a `DynamoDbAttribute` with a non-null Format property (e.g., `Format = "yyyy-MM-dd"`), THE Source_Generator SHALL inject the source property's Format into the effective format string at compile time (producing `{0:yyyy-MM-dd}`)
2. WHEN a computed format placeholder already has an explicit format specifier (e.g., `{0:MM/dd/yyyy}`), THE Source_Generator SHALL NOT override it with the source property's DynamoDbAttribute.Format
3. WHEN a computed format placeholder has no format specifier and the corresponding source property has no DynamoDbAttribute.Format (or Format is null), THE Source_Generator SHALL leave the placeholder unchanged as `{0}`
4. WHEN the Source_Generator injects a source property Format into a computed format string, THE Source_Generator SHALL use the injected format as the effective format for all operation paths (Keys builder, Put mapper, Update recomputation)
5. WHEN a source property with `[DynamoDbAttribute("date", Format = "yyyy-MM-dd")]` is used in `[Computed("EventDate", "Category")]` without an explicit Format, THE Source_Generator SHALL produce an effective format string of `{0:yyyy-MM-dd}#{1}` using the default separator
6. WHEN the source property's DynamoDbAttribute.Format value is an empty string, THE Source_Generator SHALL treat it the same as null and leave the placeholder unchanged as `{0}`

### Requirement 7: Diagnostic Accuracy

**User Story:** As a library consumer, I want the source generator to emit correct diagnostic messages for format strings with specifiers, so that I can trust compiler warnings and errors.

#### Acceptance Criteria

1. WHEN a format string contains placeholders with format specifiers where each placeholder's index portion (the substring before the first colon) is a non-negative integer and the count of distinct placeholder indices matches the source property count, THE Source_Generator SHALL NOT emit any diagnostic related to placeholder count mismatch
2. WHEN a format string contains a placeholder whose index portion is not a valid non-negative integer (e.g., `{abc:format}` or `{-1:format}`), THE Source_Generator SHALL emit a diagnostic indicating an invalid placeholder format that identifies the malformed placeholder text
3. WHEN a format string with format specifiers has more distinct placeholder indices than source properties, THE Source_Generator SHALL emit diagnostic FDDB090 reporting the number of distinct placeholder indices and the number of source properties
4. WHEN a format string with format specifiers has fewer distinct placeholder indices than source properties, THE Source_Generator SHALL emit diagnostic FDDB090 reporting the number of distinct placeholder indices and the number of source properties
5. WHEN a format string contains repeated placeholder indices with different format specifiers (e.g., `{0:D4}#{0:G}#{1}`), THE Source_Generator SHALL count index 0 once when computing the distinct placeholder index count for FDDB090 validation

### Requirement 8: Documentation Updates

**User Story:** As a library consumer, I want documentation to describe how to use format specifiers in computed field format strings, so that I can format dates, integers, and enums in my composite keys.

#### Acceptance Criteria

1. THE documentation SHALL include a complete example of using format specifiers with DateOnly types showing the entity definition with `[Computed("EventDate", "Category", Format = "{0:yyyy-MM-dd}#{1}")]` and the expected output value (e.g., `2024-03-15#electronics`)
2. THE documentation SHALL include a complete example of using format specifiers with integer types for zero-padding showing the entity definition with `[Computed("Priority", "Name", Format = "{0:D4}#{1}")]` and the expected output value (e.g., `0042#TaskName`)
3. THE documentation SHALL include a complete example of using format specifiers with enum types showing the entity definition with `[Computed("Status", "Id", Format = "{0:G}#{1}")]` and the expected output value (e.g., `Active#id123`)
4. THE documentation SHALL describe the format specifier precedence in a table or ordered list: (1) explicit specifier in computed format string, (2) source property's DynamoDbAttribute.Format, (3) default ToString()
5. THE documentation SHALL be recorded in the DOCUMENTATION_CHANGELOG with entries following the established before/after/reason format for website synchronization
6. THE repository CHANGELOG SHALL include entries under both "Fixed" (for bug fixes A-D) and "Added" (for format fallback enhancement E) sections with usage examples

