# Requirements Document

## Introduction

This document specifies the requirements for fixing false positive DYNDB023 diagnostics in the EntityAnalyzer. The DYNDB023 diagnostic ("Complex nested objects may cause performance issues") incorrectly fires on enum properties, `[Extracted]` properties, and unmapped properties. Additionally, a duplicate call to `ValidatePropertyPerformance` produces duplicate diagnostics for every property. This fix adds early-exit guards and removes the duplicate call site.

## Glossary

- **EntityAnalyzer**: The Roslyn source generator component that validates entity property models and reports diagnostics
- **ValidatePropertyPerformance**: The method within EntityAnalyzer responsible for detecting complex types that may cause DynamoDB performance issues
- **PropertyModel**: The internal representation of a C# property being analyzed, containing metadata about its DynamoDB mapping
- **DYNDB023**: The diagnostic code for "Complex nested objects may cause performance issues"
- **Unmapped_Property**: A property that has no `[DynamoDbAttribute]` and therefore is not persisted to DynamoDB (`HasAttributeMapping` is false)
- **Extracted_Property**: A property decorated with `[Extracted]` that is populated from a computed key at read time and never serialized to DynamoDB (`IsExtracted` is true)
- **Enum_Property**: A property whose type is a C# enum, stored as a simple string or integer value (`IsEnum` is true)
- **Complex_Type**: A non-primitive, non-collection type that is not an enum and not a system type, which may cause serialization performance issues when stored in DynamoDB

## Requirements

### Requirement 1: Suppress DYNDB023 for Non-Applicable Property Types

**User Story:** As a developer using FluentDynamoDb, I want the DYNDB023 diagnostic to only fire on properties that are actually persisted as complex objects to DynamoDB, so that I do not receive false positive warnings on enums, extracted properties, or unmapped properties.

#### Acceptance Criteria

1. WHEN ValidatePropertyPerformance receives a PropertyModel where HasAttributeMapping is false, THE EntityAnalyzer SHALL produce zero DYNDB023 diagnostics for that property
2. WHEN ValidatePropertyPerformance receives a PropertyModel where IsExtracted is true, THE EntityAnalyzer SHALL produce zero DYNDB023 diagnostics for that property
3. WHEN ValidatePropertyPerformance receives a PropertyModel where IsEnum is true, THE EntityAnalyzer SHALL produce zero DYNDB023 diagnostics for that property
4. WHEN ValidatePropertyPerformance receives a PropertyModel where HasAttributeMapping is true, IsExtracted is false, IsEnum is false, IsRelatedEntity is false, and the property type is a Complex_Type, THE EntityAnalyzer SHALL report exactly one DYNDB023 diagnostic for that property

### Requirement 2: Eliminate Duplicate Validation Calls

**User Story:** As a developer using FluentDynamoDb, I want each property to be validated for performance exactly once, so that I do not see duplicate DYNDB023 diagnostics in my IDE.

#### Acceptance Criteria

1. WHEN the EntityAnalyzer processes an entity model, THE EntityAnalyzer SHALL invoke ValidatePropertyPerformance exactly once per property during the validation phase
2. WHEN ValidatePropertyPerformance produces a diagnostic for a legitimate complex type, THE EntityAnalyzer SHALL report that diagnostic exactly once per property
