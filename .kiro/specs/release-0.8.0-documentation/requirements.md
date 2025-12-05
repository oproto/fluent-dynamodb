# Requirements Document

## Introduction

This feature addresses documentation corrections and release preparation for version 0.8.0 of Oproto.FluentDynamoDb. The documentation contains outdated API patterns, incorrect installation instructions, and needs release notes for the first public preview release.

## Glossary

- **FluentDynamoDb**: The Oproto.FluentDynamoDb library for DynamoDB operations
- **Source Generator**: The compile-time code generator bundled with the main package
- **Property-based API**: Old pattern using `table.Query.Where()` (deprecated)
- **Method-based API**: Current pattern using `table.Query().Where()` (correct)
- **Entity Accessor**: Generated type-safe accessor like `table.Users.GetAsync()`

## Requirements

### Requirement 1: Correct Installation Instructions

**User Story:** As a developer, I want accurate installation instructions, so that I can correctly set up the library without confusion.

#### Acceptance Criteria

1. WHEN a developer reads the README.md Quick Start section THEN the system SHALL show only `dotnet add package Oproto.FluentDynamoDb` as the required package (source generator is bundled)
2. WHEN a developer reads the Installation.md guide THEN the system SHALL NOT reference `Oproto.FluentDynamoDb.SourceGenerator` as a separate package
3. WHEN a developer reads the Installation.md guide THEN the system SHALL NOT reference `Oproto.FluentDynamoDb.Attributes` as a separate package (attributes are in main package)
4. WHEN a developer reads the QuickStart.md guide THEN the system SHALL show the correct single-package installation

### Requirement 2: Correct API Patterns in Documentation

**User Story:** As a developer, I want documentation that shows the current API patterns, so that I can write correct code.

#### Acceptance Criteria

1. WHEN documentation shows builder access THEN the system SHALL use method-based patterns `table.Query()` instead of property-based patterns `table.Query.`
2. WHEN documentation shows Put operations THEN the system SHALL use `table.Put()` or `table.Users.Put()` instead of `table.Put.`
3. WHEN documentation shows Get operations THEN the system SHALL use `table.Get()` or `table.Users.Get()` instead of `table.Get.`
4. WHEN documentation shows Update operations THEN the system SHALL use `table.Update()` or `table.Users.Update()` instead of `table.Update.`
5. WHEN documentation shows Delete operations THEN the system SHALL use `table.Delete()` or `table.Users.Delete()` instead of `table.Delete.`
6. WHEN documentation shows Scan operations THEN the system SHALL use `table.Scan()` or `table.Users.Scan()` instead of `table.Scan.`

### Requirement 3: Update CHANGELOG.md for 0.8.0 Release

**User Story:** As a developer, I want a clear changelog for the 0.8.0 release, so that I can understand what's included in this version.

#### Acceptance Criteria

1. WHEN a developer reads CHANGELOG.md THEN the system SHALL show version 0.8.0 as a released version with the current date
2. WHEN a developer reads CHANGELOG.md THEN the system SHALL include the experimental features note from README.md in the release notes
3. WHEN a developer reads CHANGELOG.md THEN the system SHALL have an empty [Unreleased] section ready for future changes
4. WHEN a developer reads CHANGELOG.md THEN the system SHALL clearly indicate this is a preview release

### Requirement 4: Create Release Notes

**User Story:** As a developer, I want release notes for 0.8.0, so that I can understand the scope and maturity of this first public release.

#### Acceptance Criteria

1. WHEN a developer reads the release notes THEN the system SHALL indicate this is the first public preview release
2. WHEN a developer reads the release notes THEN the system SHALL list production-ready features
3. WHEN a developer reads the release notes THEN the system SHALL list experimental features with appropriate warnings
4. WHEN a developer reads the release notes THEN the system SHALL provide migration guidance for users coming from pre-release versions

### Requirement 5: Update Documentation Changelog

**User Story:** As a documentation maintainer, I want the documentation changelog updated, so that derived documentation can be synchronized.

#### Acceptance Criteria

1. WHEN documentation is corrected THEN the system SHALL add entries to docs/DOCUMENTATION_CHANGELOG.md
2. WHEN entries are added THEN the system SHALL include the date, file path, before/after patterns, and reason
3. WHEN API patterns are corrected THEN the system SHALL document the change from property-based to method-based access
