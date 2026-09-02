# Requirements Document

## Introduction

The Oproto.FluentDynamoDb source generator emits approximately 85 diagnostic codes across multiple prefix groups (DYNDB, FDDB, PROJ, DISC, SEC). Currently, diagnostic information is scattered across TroubleshootingGuide.md, AdvancedTypesQuickReference.md, MultiEntityTables.md, and other contextual docs. Users encountering an unfamiliar diagnostic have no single place to look it up. This feature establishes a centralized diagnostics reference in the repository, wires up clickable `helpLinkUri` values in all DiagnosticDescriptor definitions, and documents the changes for downstream website generation.

## Glossary

- **Source_Generator**: The Roslyn incremental source generator in the `Oproto.FluentDynamoDb.SourceGenerator` project that emits C# code and diagnostics at compile time
- **DiagnosticDescriptor**: A Roslyn API type that defines the ID, title, message format, category, severity, and optional help link URI for a compiler diagnostic
- **helpLinkUri**: The 8th constructor parameter on `DiagnosticDescriptor` that provides a clickable URL users can follow from the IDE error list to online documentation
- **Diagnostic_Code**: A unique identifier (e.g., DYNDB001, FDDB090) assigned to each diagnostic emitted by the Source_Generator
- **Diagnostics_Reference**: The collection of per-code documentation pages located in `docs/diagnostics/` within the repository
- **Prefix_Group**: A logical grouping of diagnostic codes sharing the same alphabetical prefix (DYNDB, FDDB, PROJ, DISC, SEC)
- **DOCUMENTATION_CHANGELOG**: The file at `docs/DOCUMENTATION_CHANGELOG.md` that tracks documentation changes for downstream website synchronization

## Requirements

### Requirement 1: Diagnostics Reference Directory Structure

**User Story:** As a developer encountering an unfamiliar diagnostic, I want a centralized directory of all diagnostics organized by prefix group, so that I can quickly navigate to the explanation for any code.

#### Acceptance Criteria

1. THE Source_Generator repository SHALL contain a `docs/diagnostics/` directory with a `README.md` index page listing all Diagnostic_Code values grouped by Prefix_Group, where each group is presented in alphabetical order by prefix (DISC, DYNDB, FDDB, PROJ, SEC) and codes within each group are listed in ascending numeric order
2. THE Diagnostics_Reference SHALL organize per-code documentation files into subdirectories named after each Prefix_Group (e.g., `docs/diagnostics/DYNDB/`, `docs/diagnostics/FDDB/`, `docs/diagnostics/PROJ/`, `docs/diagnostics/DISC/`, `docs/diagnostics/SEC/`)
3. WHEN a Diagnostic_Code exists as a DiagnosticDescriptor field in the DiagnosticDescriptors.cs file, THE Diagnostics_Reference SHALL contain a corresponding markdown file at the path `docs/diagnostics/{PREFIX}/{FULL_CODE}.md` where FULL_CODE is the complete diagnostic identifier including prefix and number (e.g., `DYNDB001.md`, `FDDB060.md`)
4. THE `docs/diagnostics/README.md` index page SHALL include for each diagnostic a table row containing the full code, severity level (Error, Warning, or Info), and the diagnostic title (the second constructor argument of the DiagnosticDescriptor), with the code text linking to the corresponding detail page via relative markdown link
5. WHEN a detail page exists at `docs/diagnostics/{PREFIX}/{FULL_CODE}.md`, THE detail page SHALL contain at minimum: the diagnostic code as a heading, the severity level, the title, the description text from the DiagnosticDescriptor, a "Cause" section explaining what triggers the diagnostic, and a "Resolution" section explaining how to fix the issue

### Requirement 2: Per-Code Documentation Content

**User Story:** As a developer reading a diagnostic explanation, I want consistent structured information including what the diagnostic means, how to trigger it, and how to fix it, so that I can resolve the issue without further research.

#### Acceptance Criteria

1. WHEN a per-code documentation file exists, THE file SHALL contain a "Code & Severity" section stating the Diagnostic_Code (matching the ID field from DiagnosticDescriptors.cs, e.g., "DYNDB001") and its severity level as one of: Error, Warning, or Info
2. WHEN a per-code documentation file exists, THE file SHALL contain a "Message" section showing the exact format string from the DiagnosticDescriptor's message parameter (including placeholders such as `{0}`, `{1}`)
3. WHEN a per-code documentation file exists, THE file SHALL contain a "Description" section of no more than 3 paragraphs explaining what condition causes the diagnostic to be emitted and why the flagged code is problematic
4. WHEN a per-code documentation file exists, THE file SHALL contain an "Example" section with a single self-contained C# code snippet of no more than 30 lines that, when compiled with the source generator, triggers the documented diagnostic
5. WHEN a per-code documentation file exists, THE file SHALL contain a "Fix" section with a single self-contained C# code snippet of no more than 30 lines that demonstrates the corrected version of the Example code
6. WHEN a per-code documentation file has a "Fix" section, THE corrected code example SHALL compile without triggering the original diagnostic when processed by the source generator
7. IF a per-code documentation file is missing any of the required sections (Code & Severity, Message, Description, Example, Fix), THEN the file SHALL be considered incomplete and fail documentation validation

### Requirement 3: helpLinkUri on DiagnosticDescriptor Definitions

**User Story:** As a developer seeing a diagnostic in the IDE error list, I want to click the diagnostic code and be taken directly to the relevant documentation page on fluentdynamodb.dev, so that I can quickly understand and resolve the issue.

#### Acceptance Criteria

1. THE Source_Generator SHALL define a single `const string` field within the `Diagnostics` namespace that contains the base URL format for diagnostic help links, using a string format placeholder for the diagnostic code
2. THE centralized URL format constant SHALL produce URLs in the pattern `https://fluentdynamodb.dev/diagnostics/{CODE}` where `{CODE}` is the full Diagnostic_Code identifier (e.g., `FDDB053`, `DYNDB001`)
3. WHEN a DiagnosticDescriptor is defined in DiagnosticDescriptors.cs, THE definition SHALL include a `helpLinkUri` parameter whose value is the formatted URL for that descriptor's diagnostic code
4. THE helpLinkUri value for each DiagnosticDescriptor SHALL be derived from the centralized URL format constant combined with the descriptor's diagnostic code, rather than using inline URL string literals
5. WHEN the helpLinkUri is applied to existing DiagnosticDescriptors, THE Source_Generator SHALL include the `helpLinkUri` parameter on all DiagnosticDescriptor definitions in DiagnosticDescriptors.cs without exception

### Requirement 4: Numbering Inconsistency Acknowledgment

**User Story:** As a developer browsing the diagnostics reference, I want clear documentation of the numbering conventions and known inconsistencies, so that I am not confused by varying code formats.

#### Acceptance Criteria

1. THE `docs/diagnostics/README.md` SHALL include a "Numbering Conventions" section that lists each diagnostic prefix (DYNDB, FDDB, PROJ, DISC, SEC) with its associated code ranges and a one-sentence description of its domain
2. THE "Numbering Conventions" section SHALL acknowledge that FDDB0020-0021 uses 4-digit numbering while other FDDB codes use 3-digit numbering, and state that this is a known inconsistency retained for backward compatibility
3. THE "Numbering Conventions" section SHALL explain that both DYNDB and FDDB prefixes relate to the FluentDynamoDb source generator, with DYNDB covering core entity validation and FDDB covering table/index generation
4. THE "Numbering Conventions" section SHALL document that DYNDB uses range bands to group related diagnostics (001–036 for core validation, 101–127 for advanced types, 1001–1004 for extension generation) and that the varying digit widths within DYNDB reflect these logical groupings rather than an inconsistency

### Requirement 5: DOCUMENTATION_CHANGELOG Entry

**User Story:** As a website documentation maintainer, I want a changelog entry describing the new diagnostics reference, so that I can build the corresponding pages on fluentdynamodb.dev.

#### Acceptance Criteria

1. WHEN the Diagnostics_Reference is added to the repository, THE DOCUMENTATION_CHANGELOG SHALL contain a new entry under a date heading in YYYY-MM-DD format that includes a Category of "New Feature Documentation", a Description section stating that the `docs/diagnostics/` directory was added, and a Reason section explaining its purpose for the fluentdynamodb.dev website team
2. THE DOCUMENTATION_CHANGELOG entry SHALL list all five Prefix_Group subdirectories created: DYNDB/, FDDB/, PROJ/, DISC/, and SEC/
3. THE DOCUMENTATION_CHANGELOG entry SHALL specify the URL pattern `https://fluentdynamodb.dev/diagnostics/{CODE}` as the route at which per-code diagnostic pages should be served on the website
4. THE DOCUMENTATION_CHANGELOG entry SHALL follow the established entry structure in `docs/DOCUMENTATION_CHANGELOG.md`, including a level-2 date heading, a Description block, and a Reason block

### Requirement 6: Repository CHANGELOG Entry

**User Story:** As a contributor reviewing the project history, I want the diagnostics reference work tracked in the main CHANGELOG, so that I can see when this documentation was added.

#### Acceptance Criteria

1. WHEN the Diagnostics_Reference and helpLinkUri changes are committed, THE repository CHANGELOG.md SHALL contain a new bullet entry under the `[Unreleased]` section's `### Added` subsection
2. THE CHANGELOG entry SHALL follow the existing entry format of `- **Bold Title** - Description` and SHALL reference both the centralized diagnostics reference document and the helpLinkUri property addition to DiagnosticDescriptor definitions within a single bullet point
3. IF the `[Unreleased]` section or its `### Added` subsection does not yet exist in CHANGELOG.md, THEN THE system SHALL create the missing section headers before inserting the entry

### Requirement 7: Coverage Completeness

**User Story:** As a maintainer adding new diagnostics in the future, I want the reference to be clearly complete at the time of creation, so that any newly added codes are obviously missing from the reference.

#### Acceptance Criteria

1. THE Diagnostics_Reference SHALL contain one documentation file for every `DiagnosticDescriptor` field defined in `DiagnosticDescriptors.cs` at the time of creation, where each documentation file corresponds to exactly one diagnostic code (e.g., DYNDB001, FDDB001, PROJ001, DISC001, SEC001)
2. THE `docs/diagnostics/README.md` SHALL display a total count of documented diagnostics as an integer equal to the number of documentation files present in the diagnostics reference directory
3. IF a `DiagnosticDescriptor` field exists in `DiagnosticDescriptors.cs` without a corresponding documentation file in `docs/diagnostics/`, THEN THE README index SHALL list that diagnostic code in a dedicated "Undocumented" section indicating the code, its title, and its severity
4. WHEN a new documentation file is added to the diagnostics reference, THE `docs/diagnostics/README.md` total count SHALL be updated to reflect the new number of documented diagnostics
