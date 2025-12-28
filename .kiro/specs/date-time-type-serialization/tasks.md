# Implementation Plan: DateOnly and TimeOnly Serialization

## Overview

This implementation plan adds native serialization support for `DateOnly` and `TimeOnly` types to Oproto.FluentDynamoDb. The changes are focused on two components: the source generator (MapperGenerator) and the runtime expression translator (UpdateExpressionTranslator).

## Tasks

- [x] 1. Update MapperGenerator known primitives
  - Add `DateOnly`, `TimeOnly`, `System.DateOnly`, `System.TimeOnly` to the `knownPrimitives` array in `IsEnumType()` method
  - This prevents these types from incorrectly falling through to enum handling
  - _Requirements: 1.1, 2.1_

- [x] 2. Add DateOnly serialization support in MapperGenerator
  - [x] 2.1 Add DateOnly case to `GetToAttributeValueExpression()` method
    - Generate: `new AttributeValue { S = {value}.ToString("O", CultureInfo.InvariantCulture) }`
    - _Requirements: 1.1_
  - [x] 2.2 Add DateOnly case to `GetFromAttributeValueExpression()` method
    - Generate: `DateOnly.ParseExact({value}.S, "O", CultureInfo.InvariantCulture)`
    - _Requirements: 1.2_
  - [x] 2.3 Add DateOnly case to `GetToAttributeValueExpressionForCollectionElement()` method
    - _Requirements: 4.1_
  - [x] 2.4 Add DateOnly case to `GetFromAttributeValueExpressionForCollectionElement()` method
    - _Requirements: 4.1_
  - [x] 2.5 Write property test for DateOnly round-trip
    - **Property 1: DateOnly Round-Trip Consistency**
    - **Validates: Requirements 1.1, 1.2, 1.5**

- [x] 3. Add TimeOnly serialization support in MapperGenerator
  - [x] 3.1 Add TimeOnly case to `GetToAttributeValueExpression()` method
    - Generate: `new AttributeValue { S = {value}.ToString("O", CultureInfo.InvariantCulture) }`
    - _Requirements: 2.1_
  - [x] 3.2 Add TimeOnly case to `GetFromAttributeValueExpression()` method
    - Generate: `TimeOnly.ParseExact({value}.S, "O", CultureInfo.InvariantCulture)`
    - _Requirements: 2.2_
  - [x] 3.3 Add TimeOnly case to `GetToAttributeValueExpressionForCollectionElement()` method
    - _Requirements: 4.2_
  - [x] 3.4 Add TimeOnly case to `GetFromAttributeValueExpressionForCollectionElement()` method
    - _Requirements: 4.2_
  - [x] 3.5 Write property test for TimeOnly round-trip
    - **Property 2: TimeOnly Round-Trip Consistency**
    - **Validates: Requirements 2.1, 2.2, 2.5**

- [x] 4. Add format string support for DateOnly and TimeOnly
  - [x] 4.1 Add DateOnly case to `GenerateFormattedToAttributeValue()` method
    - _Requirements: 5.1_
  - [x] 4.2 Add DateOnly case to `GenerateFormattedFromAttributeValue()` method
    - _Requirements: 5.1_
  - [x] 4.3 Add TimeOnly case to `GenerateFormattedToAttributeValue()` method
    - _Requirements: 5.2_
  - [x] 4.4 Add TimeOnly case to `GenerateFormattedFromAttributeValue()` method
    - _Requirements: 5.2_
  - [x] 4.5 Write unit tests for custom format string handling
    - Test DateOnly with custom format (e.g., "MM/dd/yyyy")
    - Test TimeOnly with custom format (e.g., "h:mm tt")
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 5. Checkpoint - Verify source generator changes
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Update UpdateExpressionTranslator
  - [x] 6.1 Add DateOnly case to `ConvertToAttributeValue()` method
    - Add pattern: `DateOnly d => new AttributeValue { S = d.ToString("O", CultureInfo.InvariantCulture) }`
    - _Requirements: 3.1_
  - [x] 6.2 Add TimeOnly case to `ConvertToAttributeValue()` method
    - Add pattern: `TimeOnly t => new AttributeValue { S = t.ToString("O", CultureInfo.InvariantCulture) }`
    - _Requirements: 3.2_
  - [x] 6.3 Write property test for UpdateExpressionTranslator DateOnly conversion
    - **Property 3: UpdateExpressionTranslator DateOnly Conversion**
    - **Validates: Requirements 3.1**
  - [x] 6.4 Write property test for UpdateExpressionTranslator TimeOnly conversion
    - **Property 4: UpdateExpressionTranslator TimeOnly Conversion**
    - **Validates: Requirements 3.2**

- [x] 7. Add collection round-trip tests
  - [x] 7.1 Write property test for List<DateOnly> round-trip
    - **Property 5: Collection Round-Trip Consistency (DateOnly)**
    - **Validates: Requirements 4.1, 4.4**
  - [x] 7.2 Write property test for List<TimeOnly> round-trip
    - **Property 5: Collection Round-Trip Consistency (TimeOnly)**
    - **Validates: Requirements 4.2, 4.4**

- [x] 8. Verify built-in enum serialization
  - [x] 8.1 Write unit test for DayOfWeek serialization
    - Verify DayOfWeek serializes to string (e.g., "Monday", "Tuesday")
    - Verify DayOfWeek deserializes from string back to enum value
    - Test all seven days of the week
  - [x] 8.2 Write unit test for nullable DayOfWeek? serialization
    - Verify null handling for nullable DayOfWeek?
    - Verify non-null DayOfWeek? serializes correctly
  - [x] 8.3 Write unit test for DayOfWeek in UpdateExpressionTranslator
    - Verify DayOfWeek values convert correctly in update expressions

- [x] 9. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks are required for comprehensive implementation
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
- The "O" format specifier is the ISO 8601 round-trip format for DateOnly and TimeOnly
