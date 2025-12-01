# Requirements Document

## Introduction

This specification addresses documentation accuracy issues discovered during the process of distilling project documentation for the website. The documentation contains outdated API patterns and incorrect method names that no longer exist in the codebase. This is an ongoing effort that will grow as additional issues are discovered.

## Glossary

- **Primary API**: The recommended extension methods (`GetItemAsync`, `PutAsync`, `UpdateAsync`, `DeleteAsync`, `ToListAsync`) that populate `DynamoDbOperationContext` with operation metadata
- **Advanced API**: The `ToDynamoDbResponseAsync()` method that returns raw AWS SDK responses without populating context
- **DynamoDbOperationContext**: An `AsyncLocal`-based context that stores operation metadata including `PreOperationValues` and `PostOperationValues`
- **Return Values**: DynamoDB feature allowing retrieval of item attributes before or after an operation (e.g., `ReturnAllOldValues()`)
- **Entity Accessor**: Source-generated properties on table classes that provide type-safe access to entity operations (e.g., `table.Users.GetAsync()`)

## Requirements

### Requirement 1

**User Story:** As a developer reading the documentation, I want accurate API method names, so that I can write working code without trial and error.

#### Acceptance Criteria

1. WHEN documentation references `ExecuteAsync()` for Get operations THEN the Documentation System SHALL replace it with `GetItemAsync()`
2. WHEN documentation references `ExecuteAsync()` for Put operations THEN the Documentation System SHALL replace it with `PutAsync()`
3. WHEN documentation references `ExecuteAsync()` for Update operations THEN the Documentation System SHALL replace it with `UpdateAsync()`
4. WHEN documentation references `ExecuteAsync()` for Delete operations THEN the Documentation System SHALL replace it with `DeleteAsync()`
5. WHEN documentation references `ExecuteAsync()` for Query operations THEN the Documentation System SHALL replace it with `ToListAsync()` or `ToCompositeEntityAsync()` as appropriate
6. WHEN documentation references `ExecuteAsync()` for Scan operations THEN the Documentation System SHALL replace it with `ToListAsync()`

### Requirement 2

**User Story:** As a developer using return values, I want accurate documentation on how to access old/new item values, so that I can implement audit trails and optimistic locking correctly.

#### Acceptance Criteria

1. WHEN documentation shows accessing `response.Attributes` from `PutAsync()`, `UpdateAsync()`, or `DeleteAsync()` THEN the Documentation System SHALL correct this to show using `ToDynamoDbResponseAsync()` instead
2. WHEN documentation shows return value patterns THEN the Documentation System SHALL explain that Primary API methods (`PutAsync`, `UpdateAsync`, `DeleteAsync`) return `Task` (void) and populate `DynamoDbOperationContext.Current` with `PreOperationValues` or `PostOperationValues`
3. WHEN documentation shows accessing old values THEN the Documentation System SHALL provide examples using both `ToDynamoDbResponseAsync()` (for direct response access) and `DynamoDbOperationContext.Current.PreOperationValues` (for context-based access)
4. WHEN documentation mentions `DynamoDbOperationContext` THEN the Documentation System SHALL note that it uses `AsyncLocal` and may not be suitable for unit testing scenarios

### Requirement 3

**User Story:** As a documentation maintainer, I want a separate documentation-specific changelog (distinct from the repository CHANGELOG.md), so that I can share it with the team maintaining derived documentation without including unrelated code changes.

#### Acceptance Criteria

1. WHEN a documentation correction is made THEN the Documentation System SHALL record the change in a dedicated documentation changelog file at `docs/DOCUMENTATION_CHANGELOG.md`
2. WHEN recording a changelog entry THEN the Documentation System SHALL include the file path, a description of the incorrect pattern, and the corrected pattern
3. WHEN recording a changelog entry THEN the Documentation System SHALL include the date of the correction
4. WHEN the documentation changelog is created THEN the Documentation System SHALL include a header explaining its purpose for synchronizing derived documentation

### Requirement 4

**User Story:** As a developer, I want XML documentation comments in source code to be accurate, so that IntelliSense provides correct guidance.

#### Acceptance Criteria

1. WHEN XML documentation in source code references `ExecuteAsync()` THEN the Documentation System SHALL update it to reference the correct method name
2. WHEN XML documentation shows incorrect return value access patterns THEN the Documentation System SHALL correct them to match the actual API behavior

### Requirement 5

**User Story:** As a documentation maintainer, I want the steering documentation updated to enforce documentation changelog requirements, so that future documentation changes are tracked separately from code changes.

#### Acceptance Criteria

1. WHEN the documentation steering file is updated THEN the Documentation System SHALL add a requirement to maintain a separate documentation changelog at `docs/DOCUMENTATION_CHANGELOG.md`
2. WHEN the steering file defines changelog requirements THEN the Documentation System SHALL specify that this changelog is distinct from the repository CHANGELOG.md and is intended for synchronizing derived documentation
3. WHEN the steering file defines changelog requirements THEN the Documentation System SHALL specify the entry format including date, file path, and before/after patterns

