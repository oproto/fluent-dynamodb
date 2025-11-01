# Requirements Document

## Introduction

This spec addresses the consolidation of the separate `Oproto.FluentDynamoDb.Attributes` project back into the main `Oproto.FluentDynamoDb` library. The attributes were originally separated to avoid circular dependencies with the source generator, but with the recent refactoring to use syntax-based attribute matching instead of semantic model resolution, this separation is no longer necessary. Consolidating will simplify the project structure, reduce build complexity, and improve maintainability.

## Glossary

- **Attributes Project**: The `Oproto.FluentDynamoDb.Attributes` project containing attribute definitions
- **Main Library**: The `Oproto.FluentDynamoDb` project containing the core library code
- **Source Generator**: The `Oproto.FluentDynamoDb.SourceGenerator` project that generates code based on attributes
- **Unit Tests**: The `Oproto.FluentDynamoDb.UnitTests` project containing test code
- **Syntax-Based Matching**: The approach where the source generator identifies attributes by their string names in the syntax tree rather than loading type information

## Requirements

### Requirement 1: Consolidate Attributes into Main Library

**User Story:** As a library maintainer, I want all attribute definitions in the main library project, so that the project structure is simpler and easier to maintain.

#### Acceptance Criteria

1. WHEN the consolidation is complete, THE Main Library SHALL contain all attribute class files previously in the Attributes Project
2. WHEN the consolidation is complete, THE Attributes Project SHALL be removed from the solution
3. WHEN attributes are moved, THE Main Library SHALL maintain the same namespace `Oproto.FluentDynamoDb.Attributes` for all attribute classes
4. WHEN the consolidation is complete, THE Main Library project file SHALL not reference the Attributes Project

### Requirement 2: Update Source Generator References

**User Story:** As a library maintainer, I want the source generator to work without the separate attributes assembly, so that the build process is simplified.

#### Acceptance Criteria

1. WHEN the source generator project is updated, THE Source Generator SHALL not have a project reference to the Attributes Project
2. WHEN the source generator builds, THE Source Generator SHALL not attempt to copy or package the Attributes assembly
3. WHEN the source generator runs, THE Source Generator SHALL successfully identify attributes using syntax-based matching
4. WHEN the source generator project file is examined, THE Source Generator SHALL not contain MSBuild targets related to copying the Attributes assembly

### Requirement 3: Consolidate and Update Unit Tests

**User Story:** As a library maintainer, I want attribute-related tests in the main test project, so that all tests are organized consistently.

#### Acceptance Criteria

1. WHEN attribute tests are moved, THE Unit Tests project SHALL contain all tests previously in the Attributes test project
2. WHEN the consolidation is complete, THE Attributes test project SHALL be removed from the solution
3. WHEN tests reference attributes, THE Unit Tests SHALL reference attributes from the Main Library
4. WHEN the source generator tests run, THE Unit Tests SHALL not include attribute source files in the compilation context

### Requirement 4: Update Solution and Project References

**User Story:** As a developer, I want all project references updated correctly, so that the solution builds without errors.

#### Acceptance Criteria

1. WHEN the solution file is updated, THE solution SHALL not contain the Attributes Project or Attributes test project
2. WHEN projects are examined, THE Main Library SHALL not reference the Attributes Project
3. WHEN the BasicUsage example is examined, THE BasicUsage project SHALL reference only the Main Library and Source Generator
4. WHEN all projects build, THE build SHALL complete successfully without assembly loading errors

### Requirement 5: Maintain Backward Compatibility

**User Story:** As a library user, I want existing code to continue working, so that I don't need to make changes when upgrading.

#### Acceptance Criteria

1. WHEN user code references attributes, THE attributes SHALL be available in the same namespace `Oproto.FluentDynamoDb.Attributes`
2. WHEN the NuGet package is built, THE package SHALL contain the attributes in the main assembly
3. WHEN existing code is compiled against the new version, THE code SHALL compile without changes
4. WHEN the source generator processes user code, THE Source Generator SHALL generate the same output as before

### Requirement 6: Clean Up Build Artifacts

**User Story:** As a library maintainer, I want all build configuration related to the separate attributes project removed, so that the build system is clean and maintainable.

#### Acceptance Criteria

1. WHEN MSBuild targets are examined, THE Source Generator project SHALL not contain custom targets for copying the Attributes assembly
2. WHEN the solution is built, THE build output SHALL not contain the separate Oproto.FluentDynamoDb.Attributes.dll
3. WHEN NuGet packages are examined, THE Source Generator package SHALL not include the Attributes assembly as an analyzer dependency
4. WHEN build directories are examined, THE output SHALL not contain orphaned Attributes assembly files
