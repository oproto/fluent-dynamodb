# Implementation Plan: Named Property Placeholders

## Overview

Implement `{PropertyName}` and `{PropertyName:format}` named placeholder syntax for `[Computed]` format strings and `[RelatedEntity]` sort key patterns. A new `NamedPlaceholderNormalizer` utility class handles all parsing and rewriting to positional `{N}` form. Integration into `EntityAnalyzer` inserts normalization between attribute extraction and format validation. Six new diagnostics (FDDB091–FDDB096) provide actionable compile-time error messages. Zero changes to downstream pipeline, runtime attributes, or data models.

## Tasks

- [x] 1. Add diagnostic descriptors and NamedPlaceholderNormalizer foundation
  - [x] 1.1 Add FDDB091–FDDB096 diagnostic descriptors to `DiagnosticDescriptors.cs`
    - Add six new `DiagnosticDescriptor` fields following the existing pattern in `Oproto.FluentDynamoDb.SourceGenerator/Diagnostics/DiagnosticDescriptors.cs`
    - FDDB091 (Error): Ambiguous named placeholder usage — multiple positional args containing `{`, or named-placeholder `Format` combined with explicit source properties
    - FDDB092 (Error): Unresolved named placeholder — `{Name}` where Name is not a declared property on the entity; message includes available property names
    - FDDB093 (Error): Mixed named and positional placeholders — both `{Name}` and `{N}` in same format string
    - FDDB094 (Error): Malformed placeholder — unclosed brace `{Name` without `}`; message includes character offset
    - FDDB095 (Error): Empty placeholder — `{}` in format string; message includes character offset
    - FDDB096 (Warning): Ambiguous placeholder name/index — `{0}` where entity has property named `"0"`; resolved as property name
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 1.2 Create `NamedPlaceholderNormalizer` utility class
    - Create new file `Oproto.FluentDynamoDb.SourceGenerator/Utilities/NamedPlaceholderNormalizer.cs`
    - Implement `NormalizationResult` readonly struct with `NormalizedFormat`, `SourceProperties`, `WasNormalized`, and `Diagnostics` fields
    - Implement `NormalizationDiagnostic` readonly struct with `Kind`, `Message`, `Token`, and `CharOffset` fields
    - Implement `NormalizationDiagnosticKind` enum mapping to FDDB091–FDDB096
    - Implement `ContainsNamedPlaceholders(string formatString)` — detects `{Identifier}` or `{Identifier:format}` where Identifier is a valid C# identifier that is not a non-negative integer
    - Implement `Normalize(string formatString, IReadOnlySet<string> knownPropertyNames)`:
      - Left-to-right single-pass parsing of `{...}` tokens
      - Distinguish named (`{Name}`, `{Name:spec}`) from positional (`{N}`, `{N:spec}`) placeholders
      - Assign zero-based indices by first-appearance order; reuse index for duplicate property names
      - Preserve format specifiers character-for-character (including embedded colons like `HH:mm:ss`)
      - Resolve each identifier against `knownPropertyNames` (ordinal case-sensitive)
      - Emit appropriate `NormalizationDiagnostic` for: unresolved names (FDDB092), mixed named/positional (FDDB093), malformed `{Name` (FDDB094), empty `{}` (FDDB095), ambiguous name/index `{0}` matching property (FDDB096)
      - Preserve `*` wildcards as literal text (for RelatedEntity patterns)
    - _Requirements: 1.1, 1.2, 1.3, 1.6, 1.7, 2.1, 2.2, 2.3, 2.4, 2.6, 3.1, 3.2, 3.3, 3.5, 3.6, 4.1, 4.3, 5.1, 5.5, 5.6, 7.2, 7.3, 7.4, 7.5_

  - [x] 1.3 Write unit tests for `NamedPlaceholderNormalizer`
    - Create `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Utilities/NamedPlaceholderNormalizerTests.cs`
    - Test basic normalization: single property, two properties, three+ properties
    - Test format specifiers: `{Date:yyyy-MM-dd}`, `{Sequence:D4}`, `{Time:HH:mm:ss}` (embedded colons), mixed specifier/no-specifier
    - Test deduplication: property appearing 2x, 3x in same format string
    - Test wildcard preservation: `{Name}#*`, `*#{Name}#*`
    - Test diagnostic emission: FDDB092 unresolved, FDDB093 mixed, FDDB094 malformed, FDDB095 empty, FDDB096 ambiguous
    - Test edge cases: no placeholders, single placeholder, empty format string, format string with only literals
    - Test `ContainsNamedPlaceholders` detection for various inputs
    - _Requirements: 1.1, 1.2, 1.3, 1.6, 1.7, 2.1, 2.3, 2.4, 3.1, 3.2, 3.5, 3.6, 4.1, 4.3, 5.1, 7.2, 7.3, 7.4, 7.5_

- [x] 2. Checkpoint — Ensure normalizer foundation builds and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Integrate normalization into EntityAnalyzer
  - [x] 3.1 Modify `EntityAnalyzer.ExtractComputedKeyAttributes()` to call normalizer
    - In `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`, after extracting `sourceProperties` and `Format` named parameter:
    - If `sourceProperties.Length == 1` and `sourceProperties[0].Contains('{')`: call `NamedPlaceholderNormalizer.Normalize()`, set `computedModel.Format` and `computedModel.SourceProperties` from result
    - If `sourceProperties.Length > 1` and any `sourceProperties[i].Contains('{')`: emit FDDB091
    - If `Format != null` and `ContainsNamedPlaceholders(Format)`: if `sourceProperties.Length > 0` emit FDDB091, else normalize `Format` and set model fields from result
    - Collect entity property names (with `[DynamoDbAttribute]`) to pass as `knownPropertyNames`
    - Emit each `NormalizationDiagnostic` as a Roslyn `Diagnostic` located on the `[Computed]` attribute syntax node
    - On error-severity diagnostics, skip `ComputedKeyModel` population and halt entity code generation
    - Existing positional path (no `{` in property names) unchanged
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.5, 2.7, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 6.1, 6.2, 6.4, 7.1, 7.6_

  - [x] 3.2 Modify `EntityAnalyzer.ExtractRelationships()` to call normalizer for `[RelatedEntity]` sort key patterns
    - After extracting `sortKeyPattern` from the `[RelatedEntity]` constructor argument:
    - If pattern contains `{` followed by a C# identifier (not just `*` wildcards): call `NamedPlaceholderNormalizer.Normalize(sortKeyPattern, entityPropertyNames)`
    - Replace `sortKeyPattern` in the relationship model with `result.NormalizedFormat`
    - Preserve `*` wildcards at their original positions (normalizer treats them as literals)
    - Emit diagnostics located on the `[RelatedEntity]` attribute syntax node
    - Patterns with only literals and `*` wildcards (no `{...}`) continue existing behavior unchanged
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 6.3_

  - [x] 3.3 Verify `[Extracted]` attribute handling is excluded from normalization
    - Confirm `EntityAnalyzer.ExtractExtractedKeyAttributes()` stores `sourceProperty` verbatim with no named-placeholder scanning
    - Add a guard comment if not already present documenting the exclusion per Requirement 8
    - No code changes expected — this is a verification task
    - _Requirements: 8.1, 8.2, 8.3_

  - [x] 3.4 Write diagnostic emission unit tests for FDDB091–FDDB096
    - Create `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Diagnostics/NamedPlaceholderDiagnosticsTests.cs`
    - Test FDDB091: `[Computed("{A}", "{B}")]` (multiple positional args with `{`), `[Computed("A", Format = "PFX#{A}")]` (named Format with explicit source properties)
    - Test FDDB092: `{NonExistentProp}` in format string — verify message includes unresolved name and available property list
    - Test FDDB093: `{Name}` mixed with `{0}` in same format string
    - Test FDDB094: `{Name` without closing `}`— verify character offset in message
    - Test FDDB095: `{}` — verify character offset in message
    - Test FDDB096: Entity with property named `"0"`, `{0}` in format string — verify warning severity and resolution as property name
    - Test diagnostic location: verify Location points to attribute syntax node for IDE underline
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 3.5 Write `[Extracted]` attribute exclusion unit tests
    - Create `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Diagnostics/ExtractedAttributeExclusionTests.cs`
    - Test `[Extracted("Pk", 0)]` stores `"Pk"` verbatim
    - Test `[Extracted("{Pk}", 0)]` stores `"{Pk}"` as literal string — no normalization
    - Test `[Extracted("{Pk:D4}", 0)]` stores `"{Pk:D4}"` as literal string — no normalization
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 4. Checkpoint — Ensure EntityAnalyzer integration builds and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Integration tests and property-based tests
  - [x] 5.1 Write end-to-end code generation integration tests
    - Create `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/NamedPlaceholderCodeGenerationTests.cs`
    - Test: entity with `[Computed("INVOICE#{InvoiceNumber}#LINE#{LineNumber}")]` compiles and generates identical `Keys` class to `[Computed("InvoiceNumber", "LineNumber", Format = "INVOICE#{0}#LINE#{1}")]`
    - Test: entity with `[Computed("ENTRY#{Date:yyyy-MM-dd}")]` generates identical mapper to `[Computed("Date", Format = "ENTRY#{0:yyyy-MM-dd}")]`
    - Test: `[RelatedEntity("{OrderId}#LINE#*")]` generates identical discriminator and composite entity assembly to `[RelatedEntity("*#LINE#*")]` equivalent
    - Test: existing positional-syntax entities compile with zero new diagnostics
    - Test: named Format parameter `[Computed(Format = "PFX#{Name}")]` produces correct output
    - _Requirements: 1.3, 2.2, 2.7, 3.4, 4.2, 5.4, 5.7, 6.1, 6.2, 6.3, 6.4, 6.5_

  - [x] 5.2 Write property-based test: P1 Named-to-Positional Equivalence
    - Create `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Utilities/NamedPlaceholderNormalizerPropertyTests.cs`
    - **Property 1: Named-to-Positional Equivalence**
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.7, 2.1, 2.2, 2.4, 2.7, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3**
    - Generate N random valid C# property names, build named format string with random literal segments, build equivalent positional format string, normalize named, compare `(SourceProperties, Format)` tuples

  - [x] 5.3 Write property-based test: P2 Backward Compatibility
    - Add to `NamedPlaceholderNormalizerPropertyTests.cs`
    - **Property 2: Backward Compatibility — Positional Syntax Unchanged**
    - **Validates: Requirements 1.5, 6.1, 6.2, 6.4**
    - Generate valid positional `[Computed]` declarations (no `{` in property names), verify `ContainsNamedPlaceholders` returns false and no normalization occurs

  - [x] 5.4 Write property-based test: P3 Wildcard Preservation
    - Add to `NamedPlaceholderNormalizerPropertyTests.cs`
    - **Property 3: Wildcard Preservation in RelatedEntity Patterns**
    - **Validates: Requirements 5.1, 5.3, 5.4, 5.7, 6.3**
    - Generate patterns with mix of `{Name}` tokens and `*` at random positions, verify `*` positions unchanged after normalization

  - [x] 5.5 Write property-based tests: P4–P7 Diagnostic properties
    - Add to `NamedPlaceholderNormalizerPropertyTests.cs`
    - **Property 4: FDDB091 Rejection of Ambiguous Multi-Argument Input** — **Validates: Requirements 1.4, 2.5, 7.1**
    - **Property 5: FDDB092 Rejection of Unresolved Property Names** — **Validates: Requirements 1.6, 2.3, 3.5, 5.2, 7.2**
    - **Property 6: FDDB093 Rejection of Mixed Named and Positional** — **Validates: Requirements 3.6, 5.6**
    - **Property 7: Extracted Attribute Exclusion** — **Validates: Requirements 8.1, 8.2, 8.3**

- [x] 6. Checkpoint — Ensure all implementation tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Documentation updates
  - [x] 7.1 Update `docs/core-features/EntityDefinition.md` with named placeholder syntax
    - Add section documenting `{PropertyName}` syntax for `[Computed]` format strings
    - Include complete entity class example using named placeholder syntax
    - Include side-by-side comparison of named vs positional producing same key output
    - Add note that existing positional syntax remains supported and is not deprecated
    - _Requirements: 9.1_

  - [x] 7.2 Update `docs/core-features/ComputedFieldFormatSpecifiers.md` with named placeholder format specifier examples
    - Add at least two examples: `{Date:yyyy-MM-dd}` and `{Sequence:D4}` using named syntax
    - Pair each with positional equivalent (`{0:yyyy-MM-dd}`, `{0:D4}`)
    - _Requirements: 9.2_

  - [x] 7.3 Update `docs/advanced-topics/CompositeEntities.md` with named placeholder RelatedEntity example
    - Add `[RelatedEntity("{OrderId}#LINE#*")]` example alongside existing wildcard-only patterns
    - _Requirements: 9.3_

  - [x] 7.4 Update `docs/reference/AttributeReference.md` with Computed and RelatedEntity named placeholder docs
    - Update `[Computed]` section: named placeholder format string syntax, detection rule (single `params` arg with `{`), FDDB091 diagnostic code
    - Update `[RelatedEntity]` section: `{PropertyName}` token resolution, SourceProperties inference
    - _Requirements: 9.4, 9.5_

  - [x] 7.5 Update `docs/core-features/format-strings-guide.md` with named syntax as preferred
    - Add section with named placeholder syntax listed before positional
    - Label named syntax as "Recommended" or "Preferred"
    - Label positional syntax as "Alternative" or "Also supported"
    - _Requirements: 9.7_

  - [x] 7.6 Create diagnostic reference pages `docs/diagnostics/FDDB/FDDB091.md` through `FDDB096.md`
    - Create six new files following the existing FDDB diagnostic page format
    - Each page: Code & Severity table, Message section, Description, Example triggering the diagnostic, Fix section with corrected code
    - _Requirements: 9.6_

  - [x] 7.7 Update `.kiro/steering/fluentdynamodb.md` with named placeholder examples
    - Update "Computed Keys with Format String" section: show `{PropertyName}` as first example, retain `{N}` positional examples below
    - Update `[RelatedEntity]` section with named placeholder pattern examples
    - _Requirements: 9.8_

- [x] 8. Changelog and documentation changelog updates
  - [x] 8.1 Update `docs/DOCUMENTATION_CHANGELOG.md` with all documentation changes
    - Add entries for each modified/new documentation file from tasks 7.1–7.7
    - Each entry: date (YYYY-MM-DD), Category, file path, Before/After code blocks, Reason
    - New files: Description field summarizing new content
    - Diagnostic pages: diagnostic code, severity, title summary
    - Steering file: Before/After showing positional→named syntax addition
    - Most recent entries at top of Changelog Entries section
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_

  - [x] 8.2 Update `CHANGELOG.md` with feature entry under `[Unreleased]`
    - Add `### Added` entry for named property placeholders in `[Computed]`: bold title with em-dash, description, before/after code example
    - Add named placeholder support in `[RelatedEntity]` sort key patterns with before/after example
    - Add format specifier example (`{Date:yyyy-MM-dd}` or `{Sequence:D4}`)
    - Add separate `### Added` entry for new diagnostics (FDDB091–FDDB096) following "New Compile-Time Diagnostics" convention
    - Include explicit backward-compatibility statement for existing positional `{N}` syntax
    - Follow existing file conventions: bold title with em-dash, descriptive paragraph, fenced `csharp` code blocks
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6_

- [x] 9. Final checkpoint — Ensure everything builds, all tests pass, documentation is complete
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate the seven correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The source generator caches aggressively — run `dotnet build-server shutdown` between iterations if generated output seems stale
- `ConfigureAwait(false)` does NOT apply to source generator code (runs at compile time)
- No changes are needed to `ComputedAttribute`, `RelatedEntityAttribute`, `ComputedKeyModel`, or any downstream pipeline components
- The `NamedPlaceholderNormalizer` is an internal source-generator utility; it does not appear in the public API

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3", "3.1", "3.2", "3.3"] },
    { "id": 3, "tasks": ["3.4", "3.5", "5.1"] },
    { "id": 4, "tasks": ["5.2", "5.3", "5.4", "5.5"] },
    { "id": 5, "tasks": ["7.1", "7.2", "7.3", "7.4", "7.5", "7.6", "7.7"] },
    { "id": 6, "tasks": ["8.1", "8.2"] }
  ]
}
```
