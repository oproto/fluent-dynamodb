# Requirements Document

## Introduction

This specification addresses an inconsistency in how `null` values are handled in lambda-based update expressions. Currently, direct `null` assignment sets the DynamoDB NULL type, but `null` in a conditional expression's false branch causes the property to be skipped. This creates confusing dual behavior where `null` means different things in different contexts.

The fix introduces a `.NoUpdate()` extension method to explicitly skip property updates, making `null` consistently mean "set to DynamoDB NULL" in all contexts.

## Glossary

- **Update_Expression_Translator**: The component that converts C# lambda expressions into DynamoDB update expression syntax
- **UpdateExpressionProperty**: A marker type used in update expressions to provide type-safe access to entity properties
- **DynamoDB_NULL**: The DynamoDB attribute value type representing null (`{ NULL: true }`)
- **NoUpdate**: A new extension method that signals a property should not be updated
- **Skip_Operation**: An internal operation type indicating no update should be generated for a property

## Requirements

### Requirement 1: Consistent Null Semantics

**User Story:** As a developer, I want `null` to consistently mean "set to DynamoDB NULL" in all update expression contexts, so that I can predictably set attributes to null.

#### Acceptance Criteria

1. WHEN a property is assigned `null` directly in an update expression, THE Update_Expression_Translator SHALL generate a SET operation with DynamoDB_NULL
2. WHEN a property is assigned `null` via a conditional expression's true branch, THE Update_Expression_Translator SHALL generate a SET operation with DynamoDB_NULL
3. WHEN a property is assigned `null` via a conditional expression's false branch, THE Update_Expression_Translator SHALL generate a SET operation with DynamoDB_NULL
4. FOR ALL update expressions containing `null` assignments, THE Update_Expression_Translator SHALL treat `null` identically regardless of expression structure

### Requirement 2: NoUpdate Extension Method

**User Story:** As a developer, I want an explicit way to skip updating a property in conditional expressions, so that I can selectively update fields based on runtime conditions.

#### Acceptance Criteria

1. THE UpdateExpressionPropertyExtensions class SHALL provide a `NoUpdate<T>()` extension method for UpdateExpressionProperty<T>
2. WHEN a property is assigned `x.Property.NoUpdate()` in an update expression, THE Update_Expression_Translator SHALL generate no operation for that property
3. WHEN `NoUpdate()` is used in a conditional expression's false branch, THE Update_Expression_Translator SHALL skip the property when the condition is false
4. WHEN `NoUpdate()` is used in a conditional expression's true branch, THE Update_Expression_Translator SHALL skip the property when the condition is true
5. THE `NoUpdate()` method SHALL be available for all property types via generic type parameter
6. IF `NoUpdate()` is called directly outside an expression context, THEN THE method SHALL throw InvalidOperationException

### Requirement 3: Breaking Change - Remove Implicit Skip Behavior

**User Story:** As a developer, I want the conditional null-skip behavior removed, so that null handling is consistent and predictable.

#### Acceptance Criteria

1. THE Update_Expression_Translator SHALL NOT treat `null` in conditional false branches as a skip signal
2. WHEN migrating existing code that relied on `flag ? value : null` for skipping, THE developer SHALL replace `null` with `x.Property.NoUpdate()`
3. THE library documentation SHALL clearly document this breaking change and migration path

### Requirement 4: Expression Validation

**User Story:** As a developer, I want clear error messages when I misuse update expression methods, so that I can quickly fix issues.

#### Acceptance Criteria

1. IF `NoUpdate()` is called directly (not in an expression), THEN THE method SHALL throw InvalidOperationException with a descriptive message
2. THE error message SHALL indicate that the method is only for use in update expressions
