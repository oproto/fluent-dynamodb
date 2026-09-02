# Implementation Plan: Diagnostics Reference

## Overview

This plan implements a centralized diagnostics reference for all 99 diagnostic codes in the Oproto.FluentDynamoDb source generator. It adds `helpLinkUri` to every `DiagnosticDescriptor`, creates structured per-code documentation pages under `docs/diagnostics/`, builds an index README with grouped tables and numbering conventions, records the changes in both changelogs, and adds unit/property-based/integration tests to validate correctness.

## Tasks

- [x] 1. Set up DiagnosticHelpLinks and modify DiagnosticDescriptors
  - [x] 1.1 Create DiagnosticHelpLinks.cs with centralized URL format constant
    - Create file `Oproto.FluentDynamoDb.SourceGenerator/Diagnostics/DiagnosticHelpLinks.cs`
    - Define `internal static class DiagnosticHelpLinks` in namespace `Oproto.FluentDynamoDb.SourceGenerator.Diagnostics`
    - Add `internal const string BaseUrlFormat = "https://fluentdynamodb.dev/diagnostics/{0}"`
    - Include XML documentation comments on the class and constant
    - _Requirements: 3.1, 3.2_

  - [x] 1.2 Add helpLinkUri parameter to all DiagnosticDescriptor definitions
    - Modify `Oproto.FluentDynamoDb.SourceGenerator/Diagnostics/DiagnosticDescriptors.cs`
    - Add `helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "CODE")` to each of the 99 DiagnosticDescriptor constructor calls
    - Use named parameter syntax: `helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "DYNDB001")`
    - Ensure no descriptor is missed — all 99 must have the parameter
    - _Requirements: 3.3, 3.4, 3.5_

- [x] 2. Create documentation directory structure and index
  - [x] 2.1 Create docs/diagnostics/ directory with prefix subdirectories
    - Create `docs/diagnostics/` root directory
    - Create subdirectories: `docs/diagnostics/DYNDB/`, `docs/diagnostics/FDDB/`, `docs/diagnostics/PROJ/`, `docs/diagnostics/DISC/`, `docs/diagnostics/SEC/`
    - _Requirements: 1.1, 1.2_

  - [x] 2.2 Create README.md index page with grouped tables and numbering conventions
    - Create `docs/diagnostics/README.md`
    - Add header, introduction, and total count (99 documented diagnostics)
    - Add "Numbering Conventions" section documenting each prefix with code ranges and domain description
    - Acknowledge FDDB0020-0021 four-digit numbering inconsistency (backward compatibility)
    - Explain DYNDB vs FDDB prefix distinction (core entity validation vs table/index generation)
    - Document DYNDB range bands (001–036 core, 101–127 advanced, 1001–1004 extension)
    - Add tables grouped alphabetically by prefix (DISC, DYNDB, FDDB, PROJ, SEC)
    - Each row: linked code `[CODE](PREFIX/CODE.md)`, severity, title
    - Codes within each prefix in ascending numeric order
    - _Requirements: 1.1, 1.4, 4.1, 4.2, 4.3, 4.4, 7.2_

- [x] 3. Create per-code documentation files
  - [x] 3.1 Create DYNDB prefix documentation files (57 files)
    - Create one `.md` file per DYNDB code in `docs/diagnostics/DYNDB/`
    - Codes: DYNDB001–DYNDB036, DYNDB101–DYNDB115, DYNDB120–DYNDB127, DYNDB1001–DYNDB1004
    - Each file follows the template: heading with code and title, Code & Severity table, Message section (exact format string), Description (≤3 paragraphs), Example (C# snippet ≤30 lines), Fix (C# snippet ≤30 lines)
    - Pull title, message format, severity, and description from DiagnosticDescriptors.cs
    - _Requirements: 1.3, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 7.1_

  - [x] 3.2 Create FDDB prefix documentation files (26 files)
    - Create one `.md` file per FDDB code in `docs/diagnostics/FDDB/`
    - Codes: FDDB001–FDDB006, FDDB0020–FDDB0021, FDDB050–FDDB055, FDDB060–FDDB062, FDDB070, FDDB072, FDDB080–FDDB081, FDDB090, FDDB100–FDDB103
    - Follow same template structure as DYNDB files
    - _Requirements: 1.3, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 7.1_

  - [x] 3.3 Create PROJ prefix documentation files (8 files)
    - Create one `.md` file per PROJ code in `docs/diagnostics/PROJ/`
    - Codes: PROJ001–PROJ006, PROJ101–PROJ102
    - Follow same template structure
    - _Requirements: 1.3, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 7.1_

  - [x] 3.4 Create DISC prefix documentation files (6 files)
    - Create one `.md` file per DISC code in `docs/diagnostics/DISC/`
    - Codes: DISC001–DISC006
    - Follow same template structure
    - _Requirements: 1.3, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 7.1_

  - [x] 3.5 Create SEC prefix documentation files (2 files)
    - Create one `.md` file per SEC code in `docs/diagnostics/SEC/`
    - Codes: SEC001–SEC002
    - Follow same template structure
    - _Requirements: 1.3, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 7.1_

- [x] 4. Checkpoint - Verify source generator builds and documentation structure
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Record changelog entries
  - [x] 5.1 Add DOCUMENTATION_CHANGELOG entry
    - Add entry to `docs/DOCUMENTATION_CHANGELOG.md` under a new date heading (YYYY-MM-DD)
    - Category: "New Feature Documentation"
    - Description: state that `docs/diagnostics/` directory was added with all five prefix subdirectories (DYNDB/, FDDB/, PROJ/, DISC/, SEC/)
    - Reason: explain purpose for fluentdynamodb.dev website team
    - Specify URL pattern `https://fluentdynamodb.dev/diagnostics/{CODE}` as the route for per-code pages
    - Follow established entry structure in the file
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 5.2 Add CHANGELOG.md entry under [Unreleased] > ### Added
    - Add single bullet to `CHANGELOG.md` under `[Unreleased]` > `### Added`
    - Format: `- **Bold Title** - Description`
    - Reference both the centralized diagnostics reference and helpLinkUri addition
    - Create `[Unreleased]` and `### Added` sections if they don't exist
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 6. Write unit and property-based tests
  - [x] 6.1 Write unit tests validating helpLinkUri on all descriptors
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/DiagnosticHelpLinkTests.cs`
    - Test: all DiagnosticDescriptor fields have non-null, non-empty HelpLinkUri (use reflection)
    - Test: each HelpLinkUri equals `string.Format(DiagnosticHelpLinks.BaseUrlFormat, descriptor.Id)`
    - Test: BaseUrlFormat contains exactly one `{0}` placeholder and starts with `https://fluentdynamodb.dev/diagnostics/`
    - _Requirements: 3.3, 3.5_

  - [x] 6.2 Write property-based test for URL format invariant (Property 1)
    - Create `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/DiagnosticHelpLinkPropertyTests.cs`
    - **Property 1: helpLinkUri matches URL format for all descriptors**
    - **Validates: Requirements 3.2, 3.3, 3.4, 3.5**
    - Use FsCheck.Xunit with `[Property]` attribute
    - Generate arbitrary diagnostic code strings matching pattern `[A-Z]{2,5}[0-9]{1,4}`
    - Assert formatting with BaseUrlFormat produces URL matching `https://fluentdynamodb.dev/diagnostics/{CODE}`
    - Minimum 100 iterations
    - Tag: `Feature: diagnostics-reference, Property 1: helpLinkUri matches URL format for all descriptors`

  - [x] 6.3 Write property-based test for helpLinkUri correctness across actual descriptors (Property 2 partial)
    - Add to `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/DiagnosticHelpLinkPropertyTests.cs`
    - **Property 2: Documentation file exists for every descriptor (code-side validation)**
    - **Validates: Requirements 3.2, 3.3, 3.4, 3.5**
    - Use FsCheck to select any DiagnosticDescriptor field from the complete set via reflection
    - Assert `HelpLinkUri == string.Format(DiagnosticHelpLinks.BaseUrlFormat, descriptor.Id)`
    - Tag: `Feature: diagnostics-reference, Property 2: Documentation file exists for every descriptor`

- [x] 7. Write integration/validation tests
  - [x] 7.1 Write validation tests for documentation file existence and structure (Properties 2, 3)
    - Create `Oproto.FluentDynamoDb.UnitTests/Documentation/DiagnosticsDocumentationValidationTests.cs`
    - **Property 2: Documentation file exists for every descriptor**
    - **Property 3: Documentation files contain all required sections**
    - **Validates: Requirements 1.3, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 2.7, 7.1**
    - Test: for each DiagnosticDescriptor ID, a corresponding `.md` file exists at `docs/diagnostics/{PREFIX}/{CODE}.md`
    - Test: each `.md` file contains required sections (Code & Severity, Message, Description, Example, Fix)
    - Test: Example and Fix code blocks are ≤30 lines each
    - Test: Message section matches the descriptor's messageFormat string

  - [x] 7.2 Write validation tests for README completeness and changelog entries (Property 4)
    - Create `Oproto.FluentDynamoDb.UnitTests/Documentation/DiagnosticsReadmeValidationTests.cs`
    - **Property 4: README index row exists for every descriptor**
    - **Validates: Requirements 1.4, 7.2**
    - Test: README has all prefixes in alphabetical order (DISC, DYNDB, FDDB, PROJ, SEC)
    - Test: README total count matches actual file count
    - Test: every DiagnosticDescriptor has a corresponding row in README with code, severity, and title
    - Test: CHANGELOG.md has entry under `[Unreleased]` > `### Added`
    - Test: DOCUMENTATION_CHANGELOG has appropriately formatted entry

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The source generator already references FsCheck and FsCheck.Xunit in the test project
- The test project references the source generator with `ReferenceOutputAssembly="true"`, enabling reflection over DiagnosticDescriptors
- Documentation files should pull title, message, severity, and description directly from DiagnosticDescriptors.cs to ensure consistency

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1"] },
    { "id": 1, "tasks": ["1.2", "2.2", "3.1", "3.2", "3.3", "3.4", "3.5"] },
    { "id": 2, "tasks": ["5.1", "5.2"] },
    { "id": 3, "tasks": ["6.1", "6.2", "6.3"] },
    { "id": 4, "tasks": ["7.1", "7.2"] }
  ]
}
```
