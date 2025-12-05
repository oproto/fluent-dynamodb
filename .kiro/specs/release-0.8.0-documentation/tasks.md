# Implementation Plan

- [x] 1. Fix Installation Instructions
  - [x] 1.1 Update README.md Quick Start installation section
    - Remove `dotnet add package Oproto.FluentDynamoDb.SourceGenerator`
    - Remove `dotnet add package Oproto.FluentDynamoDb.Attributes`
    - Keep only `dotnet add package Oproto.FluentDynamoDb`
    - Add note that source generator is bundled
    - _Requirements: 1.1_

  - [x] 1.2 Update docs/getting-started/QuickStart.md installation section
    - Remove separate SourceGenerator and Attributes package references
    - Update to single package installation
    - _Requirements: 1.4_

  - [x] 1.3 Update docs/getting-started/Installation.md
    - Remove all references to `Oproto.FluentDynamoDb.SourceGenerator` as separate package
    - Remove all references to `Oproto.FluentDynamoDb.Attributes` as separate package
    - Update Core Packages section to show only main package
    - Update .csproj example to show only main package
    - _Requirements: 1.2, 1.3_

- [x] 2. Fix API Patterns in Core Documentation
  - [x] 2.1 Update README.md API examples
    - Convert property-based patterns to method-based or convenience methods
    - Use simplest approach for each example (convenience methods where possible)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 2.2 Update docs/getting-started/QuickStart.md examples
    - Convert all `table.Put.`, `table.Query.`, etc. to method-based patterns
    - Simplify examples using convenience methods where appropriate
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 2.3 Update docs/core-features/BasicOperations.md examples
    - Convert property-based patterns to method-based
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 2.4 Update docs/core-features/LinqExpressions.md examples
    - Convert `table.Query.Where<User>` to `table.Query().Where<User>` or `table.Query<User>().Where`
    - _Requirements: 2.1_

- [x] 3. Fix API Patterns in Advanced Documentation
  - [x] 3.1 Update docs/advanced-topics/AdvancedTypes.md examples
    - Convert property-based patterns to method-based
    - _Requirements: 2.1, 2.2_

  - [x] 3.2 Update docs/advanced-topics/Discriminators.md examples
    - Convert property-based patterns to method-based
    - _Requirements: 2.2_

- [x] 4. Fix API Patterns in Reference Documentation
  - [x] 4.1 Update docs/reference/ErrorHandling.md examples
    - Convert property-based patterns to method-based
    - _Requirements: 2.2_

  - [x] 4.2 Update docs/reference/AdoptionGuide.md examples
    - Convert property-based patterns to method-based
    - _Requirements: 2.1, 2.2_

  - [x] 4.3 Update docs/reference/AdvancedTypesQuickReference.md examples
    - Convert property-based patterns to method-based
    - _Requirements: 2.1_

  - [x] 4.4 Update docs/reference/LoggingTroubleshooting.md examples
    - Convert property-based patterns to method-based
    - _Requirements: 2.1_

  - [x] 4.5 Update docs/TroubleshootingGuide.md examples
    - Convert property-based patterns to method-based
    - _Requirements: 2.2_

  - [x] 4.6 Update Oproto.FluentDynamoDb/Expressions/EXPRESSION_EXAMPLES.md
    - Convert property-based patterns to method-based
    - _Requirements: 2.1_

- [x] 5. Prepare CHANGELOG.md for 0.8.0 Release
  - [x] 5.1 Move [Unreleased] content to [0.8.0] section
    - Add release date (2025-12-05)
    - Mark as preview release
    - _Requirements: 3.1, 3.4_

  - [x] 5.2 Add Feature Maturity section to 0.8.0 release notes
    - Include production-ready features list
    - Include experimental features list with warnings
    - Copy from README.md Feature Maturity section
    - _Requirements: 3.2_

  - [x] 5.3 Create empty [Unreleased] section
    - Add placeholder for future changes
    - _Requirements: 3.3_

- [x] 6. Create Release Notes
  - [x] 6.1 Create RELEASE_NOTES.md file
    - Indicate first public preview release
    - List production-ready features
    - List experimental features with warnings
    - Provide migration guidance
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

- [x] 7. Update Documentation Changelog
  - [x] 7.1 Add entries to docs/DOCUMENTATION_CHANGELOG.md
    - Document installation instruction corrections
    - Document API pattern corrections (property-based to method-based)
    - Include date, file paths, before/after patterns, and reasons
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 8. Verification
  - [x] 8.1 Verify no old patterns remain
    - Run grep searches for `table.Put.`, `table.Query.`, etc.
    - Run grep searches for SourceGenerator and Attributes package references in installation docs
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_
