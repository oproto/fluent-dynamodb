# Requirements Document

## Introduction

Named property placeholders enable users to write `{PropertyName}` and `{PropertyName:format}` tokens directly in `[Computed]` format strings and `[RelatedEntity]` sort key patterns, instead of the current positional `{0}`, `{1}` syntax with separately listed source property names. The source generator normalizes named placeholders to the existing positional representation early in the analysis pipeline, preserving full backward compatibility with positional syntax and requiring no downstream pipeline changes.

## Glossary

- **Source_Generator**: The Roslyn incremental source generator in `Oproto.FluentDynamoDb.SourceGenerator` that analyzes entity class declarations and emits implementation code at compile time.
- **Named_Placeholder**: A token of the form `{PropertyName}` or `{PropertyName:format}` embedded in a format string, where `PropertyName` references a declared property on the same entity class.
- **Positional_Placeholder**: A token of the form `{N}` or `{N:format}` where `N` is a zero-based integer index corresponding to the order of source properties in the `[Computed]` attribute constructor.
- **Normalization**: The process of rewriting named placeholders to positional placeholders and populating the `SourceProperties` array, performed during entity analysis before the downstream pipeline runs.
- **ComputedAttribute**: The `[Computed]` attribute that specifies how a property value is computed from other properties, accepting `params string[] sourceProperties` in its constructor and optional `Format` and `Separator` named parameters.
- **RelatedEntityAttribute**: The `[RelatedEntity]` attribute that marks a property as a related entity collection, accepting a `sortKeyPattern` string with wildcard (`*`) support.
- **EntityAnalyzer**: The class in the source generator that parses entity declarations, extracts attribute metadata, validates configurations, and feeds normalized models into code generation.
- **FormatSpecifierHelper**: A utility class in the source generator that detects and extracts format specifier information from composite format strings.
- **NormalizedKeyFormat_Pipeline**: The sequence of `ComputeNormalizedKeyFormats` → `DeriveDiscriminatorPatterns` → `ApplyAutoDerivedDiscriminator` that operates on positional format strings to produce discriminator patterns for multi-entity tables.
- **Format_Specifier**: A .NET format specifier appended after a colon inside a placeholder (e.g., `yyyy-MM-dd` in `{Date:yyyy-MM-dd}` or `D4` in `{Sequence:D4}`).

## Requirements

### Requirement 1: Detect Named Placeholders in Computed Format Strings

**User Story:** As a developer, I want to write `[Computed("INVOICE#{InvoiceNumber}#LINE#{LineNumber}")]` using property names directly in the format string, so that I do not need to separately list source property names and correlate them with positional indices.

#### Acceptance Criteria

1. WHEN a `[Computed]` attribute has exactly one positional constructor argument and that argument contains at least one `{` character followed by a valid C# identifier (a letter or underscore followed by zero or more letters, digits, or underscores) that is not a non-negative integer, THE Source_Generator SHALL treat the argument as a format string containing Named_Placeholders and SHALL extract the property names in order of first appearance as the inferred SourceProperties array.
2. WHEN a `[Computed]` attribute has a `Format` named parameter whose value contains tokens matching the pattern `{Identifier}` or `{Identifier:FormatSpecifier}` where `Identifier` is a valid C# identifier that is not a non-negative integer, THE Source_Generator SHALL treat those tokens as Named_Placeholders and SHALL extract the property names in order of first appearance as the inferred SourceProperties array.
3. WHEN the Source_Generator detects Named_Placeholders in a `[Computed]` attribute (via criterion 1 or criterion 2), THE Source_Generator SHALL normalize the format string by replacing each `{Identifier}` with `{N}` and each `{Identifier:FormatSpecifier}` with `{N:FormatSpecifier}`, where N is the zero-based index assigned to that Identifier based on its order of first appearance, and SHALL populate the ComputedKeyModel with the normalized positional format string and the inferred SourceProperties array so that all downstream pipeline stages (NormalizedKeyFormat, discriminator derivation, KeysGenerator, code emission) receive positional format strings unchanged.
4. WHEN a `[Computed]` attribute has more than one positional constructor argument and any argument contains a `{` character, THE Source_Generator SHALL emit diagnostic error FDDB091 with severity Error indicating that named placeholders in format strings cannot be combined with multiple positional source property arguments, and SHALL halt code generation for the containing entity.
5. WHEN a `[Computed]` attribute has positional constructor arguments that do not contain any `{` character, THE Source_Generator SHALL continue to treat them as source property names using the existing positional behavior, with no change to current functionality.
6. WHEN the Source_Generator extracts a Named_Placeholder whose Identifier does not match any declared property name on the containing entity (compared using ordinal case-sensitive matching), THE Source_Generator SHALL emit diagnostic error FDDB092 with severity Error identifying the unresolved property name and the containing entity, and SHALL halt code generation for that entity.
7. WHEN a Named_Placeholder Identifier appears more than once in the same format string, THE Source_Generator SHALL assign it the same positional index as its first appearance and SHALL include the property name only once in the inferred SourceProperties array.

### Requirement 2: Parse and Resolve Named Placeholders

**User Story:** As a developer, I want the source generator to resolve `{PropertyName}` tokens against my entity's declared properties at compile time, so that I get immediate feedback on typos or missing properties.

#### Acceptance Criteria

1. WHEN the Source_Generator encounters a `[Computed]` attribute where `SourceProperties` has exactly one element and that element contains a `{` character, THE Source_Generator SHALL treat that element as a named-placeholder format string rather than a property name, and extract each property name token by parsing `{Name}` and `{Name:specifier}` patterns from the string.
2. WHEN a Named_Placeholder references a property name that exists as a declared property on the same entity class, THE Source_Generator SHALL resolve the reference, assign each distinct property name a zero-based positional index in order of first appearance, rewrite the format string to positional `{N}` or `{N:specifier}` form, and populate the computed key's source properties array from the resolved names in index order so that the downstream NormalizedKeyFormat pipeline receives positional format strings unchanged.
3. WHEN a Named_Placeholder references a property name that does not match any declared property on the entity class, THE Source_Generator SHALL emit a diagnostic error with severity Error, identifying the unresolved property name and the source location of the `[Computed]` or `[RelatedEntity]` attribute that contains it.
4. WHEN the same property name appears in multiple Named_Placeholders within a single format string, THE Source_Generator SHALL assign the property a single positional index based on the position of its first occurrence and reuse that index for every subsequent occurrence of the same name.
5. WHEN a `[Computed]` attribute has more than one element in `SourceProperties` and any element contains a `{` character, THE Source_Generator SHALL emit a diagnostic error with severity Error indicating that named-placeholder format strings cannot be combined with explicit source property names.
6. IF a Named_Placeholder token such as `{0}` is ambiguous because it matches both a declared property name on the entity and a valid positional index, THEN THE Source_Generator SHALL resolve it as a property name reference, emit a warning diagnostic identifying the ambiguity, and include the property in the computed key's source properties.
7. WHEN a `[Computed]` attribute uses a named-placeholder format string with no explicit `SourceProperties` parameter, THE Source_Generator SHALL infer the source properties entirely from the `{Name}` tokens in the format string, producing behavior identical to the equivalent positional `[Computed]` declaration with explicit source property names and a `{N}`-based `Format`.

### Requirement 3: Normalize Named Placeholders to Positional Format

**User Story:** As a maintainer of the source generator, I want named placeholders to be normalized to positional `{N}` format early in the analysis pipeline, so that all downstream code generation (NormalizedKeyFormat_Pipeline, KeysGenerator, MapperGenerator) continues to work without modification.

#### Acceptance Criteria

1. WHEN the `[Computed]` attribute receives exactly one positional constructor argument and that argument contains at least one `{` character, THE Source_Generator SHALL treat the argument as a named-placeholder format string and extract each `{PropertyName}` and `{PropertyName:specifier}` token, assigning each unique property name a zero-based positional index in the order of its first appearance in the format string. A second occurrence of the same property name SHALL receive the same index as the first occurrence.
2. WHEN Normalization completes, THE Source_Generator SHALL rewrite the format string replacing each `{PropertyName}` with `{N}` and each `{PropertyName:specifier}` with `{N:specifier}`, where `N` is the assigned positional index.
3. WHEN Normalization completes, THE Source_Generator SHALL populate the `ComputedKeyModel.SourceProperties` array with the resolved property names in positional index order, and set `ComputedKeyModel.Format` to the rewritten positional format string.
4. THE Source_Generator SHALL produce identical `ComputedKeyModel` state (`SourceProperties` array contents and order, and `Format` string value) when given `[Computed(Format = "INVOICE#{InvoiceNumber}#LINE#{LineNumber}")]` as when given `[Computed("InvoiceNumber", "LineNumber", Format = "INVOICE#{0}#LINE#{1}")]` on the same entity.
5. IF a named placeholder references a property name that does not exist on the entity, THEN THE Source_Generator SHALL emit a diagnostic error identifying the unresolved property name and the format string that contains it, and SHALL not generate code for that computed key.
6. IF a named-placeholder format string contains both named tokens (e.g., `{PropertyName}`) and positional tokens (e.g., `{0}`), THEN THE Source_Generator SHALL emit a diagnostic error indicating that mixed named and positional placeholders are not permitted in the same format string.

### Requirement 4: Support Format Specifiers with Named Placeholders

**User Story:** As a developer, I want to use format specifiers with named placeholders like `{Date:yyyy-MM-dd}` or `{Sequence:D4}`, so that I get the same formatting control as with positional placeholders.

#### Acceptance Criteria

1. WHEN a Named_Placeholder includes a Format_Specifier (e.g., `{Date:yyyy-MM-dd}` or `{Sequence:D4}`), THE Source_Generator SHALL rewrite the placeholder to a positional form `{N:specifier}` where N is the zero-based index assigned by left-to-right order of first appearance, and the specifier text is preserved character-for-character, including format specifiers that contain embedded colons (e.g., `{StartTime:HH:mm:ss}` becomes `{N:HH:mm:ss}`).
2. WHEN the FormatSpecifierHelper methods `HasAnyFormatSpecifier`, `HasFormatSpecifierForIndex`, and `GetIndicesWithFormatSpecifiers` process a format string that was normalized from named to positional placeholders, THE FormatSpecifierHelper SHALL return identical results to processing the equivalent hand-written positional format string (e.g., the normalized output of `"ENTRY#{Date:yyyy-MM-dd}"` SHALL produce the same return values from all three methods as the literal string `"ENTRY#{0:yyyy-MM-dd}"`).
3. WHEN a format string contains multiple Named_Placeholders where some include Format_Specifiers and others do not (e.g., `"INVOICE#{InvoiceId}#LINE#{LineNumber:D3}"`), THE Source_Generator SHALL preserve each placeholder's specifier independently, producing a normalized format string where only the placeholders that had specifiers retain them (e.g., `"INVOICE#{0}#LINE#{1:D3}"`).

### Requirement 5: Named Placeholders in RelatedEntity Sort Key Patterns

**User Story:** As a developer, I want to write `[RelatedEntity("{OrderId}#LINE#*")]` using property names in the sort key pattern, so that the pattern is self-documenting and I do not need a separate `SourceProperties` parameter.

#### Acceptance Criteria

1. WHEN a `[RelatedEntity]` sort key pattern contains tokens matching the regex `\{[A-Za-z_][A-Za-z0-9_]*\}` (a C# identifier enclosed in curly braces), THE Source_Generator SHALL treat those tokens as Named_Placeholders referencing properties on the declaring entity class by name, and resolve each placeholder to the corresponding property during analysis.
2. WHEN a Named_Placeholder in a `[RelatedEntity]` sort key pattern references a property name that does not exist on the declaring entity class, THE Source_Generator SHALL emit a diagnostic error at severity Error that identifies the unresolved property name and the entity class, and SHALL halt code generation for that entity.
3. WHEN a `[RelatedEntity]` sort key pattern contains only literal text and wildcard (`*`) characters with no Named_Placeholders, THE Source_Generator SHALL continue to treat the pattern using the existing wildcard-matching behavior with no change in output.
4. WHEN a `[RelatedEntity]` sort key pattern contains a Named_Placeholder alongside a wildcard (`*`), THE Source_Generator SHALL resolve the Named_Placeholder to the referenced property and preserve the wildcard as a match-any segment, producing a pattern equivalent to the manually specified wildcard pattern (e.g., `{OrderId}#LINE#*` with property value `"INV-001"` behaves identically to `INV-001#LINE#*`).
5. WHEN a `[RelatedEntity]` sort key pattern contains a Named_Placeholder with a format specifier (e.g., `{Sequence:D4}`), THE Source_Generator SHALL apply the format specifier to the resolved property value using `CultureInfo.InvariantCulture`, consistent with the existing `[Computed]` format specifier behavior.
6. WHEN a `[RelatedEntity]` sort key pattern contains positional placeholders (`{0}`, `{1}`) alongside Named_Placeholders (`{PropertyName}`), THE Source_Generator SHALL emit a diagnostic error at severity Error indicating that mixing positional and named placeholders in a single pattern is not permitted.
7. THE Source_Generator SHALL normalize Named_Placeholders in `[RelatedEntity]` sort key patterns to positional format (`{N}`) during analysis, before the pattern enters the discriminator derivation pipeline, so that all downstream processing (NormalizedKeyFormat, DerivedDiscriminatorPattern) operates on positional format strings unchanged.

### Requirement 6: Backward Compatibility

**User Story:** As a developer with existing code using positional `{0}`, `{1}` syntax, I want my existing `[Computed]` and `[RelatedEntity]` declarations to continue working without changes when I upgrade to a version that supports named placeholders.

#### Acceptance Criteria

1. THE Source_Generator SHALL accept `[Computed]` attributes with explicit `params string[] sourceProperties` constructor arguments and an optional `Format` named parameter using positional `{N}` placeholders, and produce generated `Keys.Pk()` and `Keys.Sk()` methods with the same parameter names, parameter types, and key-string output as the version prior to named placeholder support.
2. THE Source_Generator SHALL accept `[Computed]` attributes using the `Separator` named parameter instead of `Format`, and produce generated `Keys.Pk()` and `Keys.Sk()` methods with the same parameter names, parameter types, and key-string output as the version prior to named placeholder support.
3. THE Source_Generator SHALL accept `[RelatedEntity]` sort key patterns containing only literals and `*` wildcards with no `{…}` placeholders, and produce identical `MatchesEntity` discrimination logic and `ToCompositeEntityAsync` assembly behavior as the version prior to named placeholder support.
4. WHEN processing a `[Computed]` attribute that uses positional `{N}` placeholders with explicit `sourceProperties`, THE Source_Generator SHALL produce the same `NormalizedKeyFormat` and `DerivedDiscriminatorPattern` values for the annotated key property as the version prior to named placeholder support.
5. WHEN a project that compiles without diagnostics on the version prior to named placeholder support is compiled with the version that adds named placeholder support, THE Source_Generator SHALL emit no new warnings or errors for any existing `[Computed]` or `[RelatedEntity]` declarations that use positional `{N}` placeholders, `Separator`, or literal-and-wildcard patterns.

### Requirement 7: Diagnostic Messages

**User Story:** As a developer, I want clear, actionable diagnostic messages when I make mistakes with named placeholders, so that I can quickly fix configuration errors.

#### Acceptance Criteria

1. WHEN the Source_Generator analyzes a `[Computed]` attribute where more than one positional argument in the `SourceProperties` array contains a `{` character, THE Source_Generator SHALL emit an error-severity diagnostic whose message identifies the attribute location, states that multiple positional arguments containing `{` are ambiguous, and suggests either using property names without braces as positional arguments with a separate `Format` parameter, or using a single format string with `{PropertyName}` placeholders as the sole positional argument.
2. WHEN a named placeholder in a `[Computed]` or `[RelatedEntity]` format string references a property name that does not exist on the declaring entity, THE Source_Generator SHALL emit an error-severity diagnostic whose message includes the unresolved property name and a comma-separated list of all property names declared on that entity that carry a `[DynamoDbAttribute]`.
3. WHEN a placeholder token such as `{0}` matches both a positional index and a property declared on the entity with the name `"0"`, THE Source_Generator SHALL resolve the token as a property-name reference, SHALL NOT fall back to positional-index interpretation, and SHALL emit a warning-severity diagnostic whose message identifies the token, states it was resolved as a property name, and advises renaming the property to avoid ambiguity.
4. IF the Source_Generator encounters a placeholder with an unclosed brace (e.g., `{Name` without a closing `}`), THEN THE Source_Generator SHALL emit an error-severity diagnostic whose message identifies the malformed token text and the character offset within the format string where the unclosed brace begins.
5. IF the Source_Generator encounters an empty placeholder `{}` in a `[Computed]` or `[RelatedEntity]` format string, THEN THE Source_Generator SHALL emit an error-severity diagnostic whose message identifies the empty placeholder and its character offset within the format string.
6. WHEN the Source_Generator emits any diagnostic defined by criteria 1 through 5, THE Source_Generator SHALL locate the diagnostic on the attribute syntax node in the source file so that the IDE underlines the attribute declaration containing the error.

### Requirement 8: Extracted Attribute Exclusion

**User Story:** As a developer, I want to confirm that `[Extracted]` attributes are unaffected by this feature, so that I do not need to change any extraction logic.

#### Acceptance Criteria

1. THE Source_Generator SHALL parse `[Extracted("SourceProperty", index)]` by storing the first constructor argument as the literal `ExtractedKey.SourceProperty` string and the second constructor argument as the literal `ExtractedKey.Index` integer, with no transformation or token extraction applied to either value.
2. WHEN the Source_Generator encounters an `[Extracted]` attribute whose `sourceProperty` parameter is a plain property name (e.g., `"Pk"`), THE Source_Generator SHALL store it verbatim as `ExtractedKey.SourceProperty` without scanning for `{PropertyName}` Named_Placeholder tokens.
3. WHEN the Source_Generator encounters an `[Extracted]` attribute whose `sourceProperty` parameter coincidentally contains brace characters (e.g., `"{Pk}"`), THE Source_Generator SHALL store the value as the literal string `"{Pk}"` and SHALL NOT invoke the Named_Placeholder normalization pipeline on it.

### Requirement 9: Documentation Updates in /docs Directory

**User Story:** As a developer reading the project documentation, I want the `/docs` directory to include comprehensive documentation for named property placeholder syntax, so that I can learn how to use `{PropertyName}` in `[Computed]` and `[RelatedEntity]` attributes.

#### Acceptance Criteria

1. WHEN the named property placeholder feature is implemented, THE documentation at `docs/core-features/EntityDefinition.md` SHALL include a section documenting the `{PropertyName}` syntax for `[Computed]` format strings that contains: (a) at least one complete entity class example using named placeholder syntax (e.g., `[Computed(Format = "INVOICE#{InvoiceNumber}#LINE#{LineNumber}")]`), (b) a side-by-side comparison showing the named placeholder syntax and its positional `{N}` equivalent producing the same key output, and (c) a note that existing positional syntax remains supported and is not deprecated.
2. WHEN the named property placeholder feature is implemented, THE documentation at `docs/core-features/ComputedFieldFormatSpecifiers.md` SHALL include at least two examples of named placeholders with format specifiers (e.g., `{Date:yyyy-MM-dd}` and `{Sequence:D4}`), each paired with the equivalent positional placeholder form (e.g., `{0:yyyy-MM-dd}`) to demonstrate that named placeholders support the same format specifier behavior.
3. WHEN the named property placeholder feature is implemented, THE documentation at `docs/advanced-topics/CompositeEntities.md` SHALL include at least one `[RelatedEntity]` example using named placeholders in the sort key pattern (e.g., `[RelatedEntity("{OrderId}#LINE#*")]`), placed alongside the existing wildcard-only pattern examples so both styles are visible in the same section.
4. WHEN the named property placeholder feature is implemented, THE documentation at `docs/reference/AttributeReference.md` SHALL update the `[Computed]` attribute section to document: (a) the named placeholder format string syntax as an alternative to the positional constructor arguments, (b) the detection rule that the source generator treats a single `params` argument containing `{` as a named-placeholder format string rather than a property name, and (c) the diagnostic code FDDB091 emitted when positional arguments and named-placeholder format strings are mixed ambiguously.
5. WHEN the named property placeholder feature is implemented, THE documentation at `docs/reference/AttributeReference.md` SHALL update the `[RelatedEntity]` attribute section to document: (a) that `{PropertyName}` tokens in the sort key pattern are resolved against entity properties at compile time, and (b) that when named placeholders are used, the `SourceProperties` parameter is not required because source properties are inferred from the pattern.
6. WHEN new diagnostic codes FDDB091 and FDDB092 are added, THE documentation SHALL include a diagnostic reference page for each at `docs/diagnostics/FDDB/FDDB091.md` and `docs/diagnostics/FDDB/FDDB092.md` respectively, where each page contains the following sections matching the existing format: a "Code & Severity" table, a "Message" section with the compiler message template, a "Description" section explaining the cause, an "Example" section with a code snippet that triggers the diagnostic, and a "Fix" section with the corrected code.
7. WHEN the named property placeholder feature is implemented, THE documentation at `docs/core-features/format-strings-guide.md` SHALL include a section showing named placeholder syntax listed before positional syntax, with the named syntax labeled as "Recommended" or "Preferred" via a heading or callout, and the positional syntax labeled as "Alternative" or "Also supported".
8. THE `.kiro/steering/fluentdynamodb.md` steering file SHALL be updated to include named placeholder syntax examples in the Computed Keys with Format String section and the `[RelatedEntity]` section, with `{PropertyName}` shown as the first example in each section and the existing `{N}` positional examples retained below it.

### Requirement 10: Documentation Changelog Updates

**User Story:** As a documentation maintainer for the fluentdynamodb.dev website, I want the `docs/DOCUMENTATION_CHANGELOG.md` to record all documentation changes made as part of this feature, so that I can synchronize the website documentation with the repository.

#### Acceptance Criteria

1. WHEN a documentation file in the `/docs` directory is modified as part of this feature, THE `docs/DOCUMENTATION_CHANGELOG.md` SHALL include a corresponding entry containing the date in `YYYY-MM-DD` format, the Category, the file path, Before and After code blocks showing the changed content, and a Reason field explaining why the change was made.
2. WHEN a new documentation file in the `/docs` directory is created as part of this feature, THE `docs/DOCUMENTATION_CHANGELOG.md` SHALL include a corresponding entry containing the date in `YYYY-MM-DD` format, the Category, the file path, a Description field summarizing the new content, and representative Before/After code blocks illustrating the behavior change the new page documents.
3. WHEN new diagnostic reference pages are added (e.g., `FDDB091.md`, `FDDB092.md`), THE `docs/DOCUMENTATION_CHANGELOG.md` SHALL include an entry for each new diagnostic page listing the diagnostic code, severity, title in a summary table, and a Before/After code block showing an example that triggers the diagnostic and the corrected form.
4. WHEN the steering file `.kiro/steering/fluentdynamodb.md` is updated with named placeholder examples, THE `docs/DOCUMENTATION_CHANGELOG.md` SHALL include an entry with the Category, the steering file path, Before and After code blocks showing the previous positional-placeholder syntax and the new named-placeholder syntax, and a Reason field.
5. ALL entries in `docs/DOCUMENTATION_CHANGELOG.md` SHALL follow the established entry format with Category, File path, Before/After code blocks (or Description field for new files that have no prior content to contrast), and Reason fields, and SHALL be ordered with the most recent entry at the top of the Changelog Entries section.

### Requirement 11: Repository Changelog Updates

**User Story:** As a user upgrading to a new version of the library, I want the `CHANGELOG.md` to document the named property placeholder feature, so that I understand what new capabilities are available and how to use them.

#### Acceptance Criteria

1. WHEN the named property placeholder feature is implemented, THE `CHANGELOG.md` SHALL include an entry under the `[Unreleased]` section in the `### Added` subsection that documents the named property placeholder syntax for `[Computed]` format strings, containing: a bold title with em-dash separator (e.g., `**Named Property Placeholders for Computed Keys** —`), a description of the feature, and a before/after code example pair showing the positional syntax `[Computed("InvoiceNumber", "LineNumber", Format = "INVOICE#{0}#LINE#{1}")]` alongside the new named syntax `[Computed("INVOICE#{InvoiceNumber}#LINE#{LineNumber}")]`.
2. WHEN the named property placeholder feature is implemented, THE `CHANGELOG.md` entry SHALL document named placeholder support in `[RelatedEntity]` sort key patterns, containing a before/after code example pair showing the positional syntax alongside the new named syntax (e.g., `[RelatedEntity("{OrderId}#LINE#*", EntityType = typeof(OrderLine))]`).
3. WHEN the named property placeholder feature is implemented, THE `CHANGELOG.md` entry SHALL document that named placeholders support .NET format specifiers using `{PropertyName:format}` syntax, with at least one code example showing a format specifier (e.g., `[Computed(Format = "ENTRY#{Date:yyyy-MM-dd}")]` or `[Computed(Format = "SEQ#{Sequence:D4}")]`).
4. WHEN new diagnostic codes are introduced for the named property placeholder feature, THE `CHANGELOG.md` SHALL include a separate `### Added` entry following the existing "**New Compile-Time Diagnostics**" convention, listing each new diagnostic code (e.g., FDDB091, FDDB092) with its severity level and a one-line description of the condition that triggers it.
5. THE `CHANGELOG.md` entry SHALL include an explicit backward-compatibility statement confirming that existing positional `{N}` syntax in `[Computed]` format strings and `[RelatedEntity]` patterns continues to work without changes.
6. THE `CHANGELOG.md` entries SHALL follow the structural conventions of existing entries in the file: bold title with em-dash separator, descriptive paragraph, and fenced `csharp` code blocks for examples, placed within the `[Unreleased]` section under the `### Added` subsection.
