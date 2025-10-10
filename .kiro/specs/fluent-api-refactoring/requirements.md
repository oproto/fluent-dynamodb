# Requirements Document

## Introduction

This feature refactors the Oproto.FluentDynamoDb library's architecture to eliminate code duplication across request builders and introduce more efficient parameter handling. Currently, each request builder (QueryRequestBuilder, PutItemRequestBuilder, etc.) must implement identical interface methods that forward calls to shared internal helpers, creating a maintenance burden where every new method must be implemented across multiple builders. Additionally, the current parameter handling requires verbose `:parameter` naming and separate `.WithValue()` calls that add ceremony without value.

The refactoring will move shared functionality to extension methods on interfaces and introduce string.Format-style parameter handling with optional formatting support, significantly reducing maintenance overhead while improving the developer experience.

## Requirements

### Requirement 1

**User Story:** As a library maintainer, I want to add new functionality once and have it automatically available on all applicable request builders, so that I don't have to implement the same method across multiple classes.

#### Acceptance Criteria

1. WHEN a new method is added to an interface extension THEN all request builders implementing that interface SHALL automatically have access to the method
2. WHEN adding a new WithValue overload THEN the implementation SHALL only need to be written once in the extension method
3. WHEN adding a new condition expression method THEN it SHALL be available on all builders that support condition expressions without individual implementation

### Requirement 2

**User Story:** As a library consumer, I want existing code to continue working without changes, so that I can upgrade the library without breaking my application.

#### Acceptance Criteria

1. WHEN upgrading to the refactored version THEN existing method calls SHALL continue to work with identical behavior
2. WHEN using fluent method chaining THEN the syntax and return types SHALL remain unchanged
3. WHEN calling any existing public method THEN the method signature and behavior SHALL be preserved
4. IF extension methods require additional using statements THEN this SHALL be the only change required for consumers

### Requirement 3

**User Story:** As a developer using the library, I want to write DynamoDB conditions without the ceremony of parameter naming and separate value calls, so that my code is more concise and readable.

#### Acceptance Criteria

1. WHEN writing a condition expression THEN I SHALL be able to use `{0}`, `{1}` syntax instead of `:param` naming
2. WHEN providing multiple parameters THEN they SHALL be automatically mapped to generated parameter names
3. WHEN using the new parameter syntax THEN values SHALL be automatically converted to appropriate AttributeValue types
4. WHEN mixing new and old parameter syntax THEN both approaches SHALL work together without conflict

### Requirement 4

**User Story:** As a developer, I want to format values inline within condition expressions, so that I don't need separate ToString() calls for dates, enums, and other formatted types.

#### Acceptance Criteria

1. WHEN using `{0:o}` format syntax THEN DateTime values SHALL be automatically formatted as ISO strings
2. WHEN using `{0}` without format THEN enum values SHALL be automatically converted to strings
3. WHEN using format specifiers THEN standard .NET formatting rules SHALL be applied
4. WHEN a value doesn't support the specified format THEN a clear error message SHALL be provided

### Requirement 5

**User Story:** As a library maintainer, I want to identify and consolidate all shared functionality into extension methods, so that the codebase has minimal duplication and maximum maintainability.

#### Acceptance Criteria

1. WHEN analyzing existing interfaces THEN all methods that are duplicated across builders SHALL be identified
2. WHEN refactoring interfaces THEN they SHALL expose only the minimal required properties for extension methods to function
3. WHEN implementing extension methods THEN they SHALL handle all existing overloads and variations
4. WHEN builders implement interfaces THEN they SHALL only need to provide access to internal helpers and return Self property

### Requirement 6

**User Story:** As a library consumer, I want the refactored library to maintain AOT compatibility, so that I can continue using it in Native AOT applications.

#### Acceptance Criteria

1. WHEN using extension methods THEN they SHALL not rely on reflection or dynamic code generation
2. WHEN using format string parsing THEN it SHALL use only AOT-safe string manipulation
3. WHEN converting values to AttributeValue types THEN the conversion SHALL be statically analyzable
4. WHEN the library is used in AOT applications THEN it SHALL compile and run without warnings or errors

### Requirement 7

**User Story:** As a developer, I want comprehensive IntelliSense support for the new extension methods, so that I can discover and use functionality easily.

#### Acceptance Criteria

1. WHEN typing method names THEN extension methods SHALL appear in IntelliSense with appropriate documentation
2. WHEN using extension methods THEN parameter hints and overload information SHALL be displayed
3. WHEN extension methods are not available THEN appropriate using statement suggestions SHALL be provided by the IDE
4. WHEN viewing method documentation THEN examples and usage patterns SHALL be clearly described