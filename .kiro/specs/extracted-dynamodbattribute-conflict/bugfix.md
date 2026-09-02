# Bugfix Requirements Document

## Introduction

The source generator silently accepts properties that have both `[Extracted]` and `[DynamoDbAttribute]` applied simultaneously. These attributes are semantically conflicting — `[Extracted]` means the value is derived from a composite key at read time, while `[DynamoDbAttribute]` means the property maps to its own independent DynamoDB attribute. The absence of a diagnostic leads to generated code that both serializes the property as a standalone attribute AND extracts it from the source key during deserialization, causing silent data inconsistency.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a property has both `[Extracted]` and `[DynamoDbAttribute]` attributes THEN the system emits no diagnostic and silently generates code for both serialization and extraction paths

1.2 WHEN the generated code runs for a property with both attributes THEN the system writes the property as a separate DynamoDB attribute during serialization (wasting storage with redundant data) AND attempts to extract the value from the composite key during deserialization, with undefined precedence between the two values

1.3 WHEN a property with both attributes is evaluated for update model inclusion THEN the system may include the property in update models (due to `[DynamoDbAttribute]`) even though extracted properties should be excluded from updates

### Expected Behavior (Correct)

2.1 WHEN a property has both `[Extracted]` and `[DynamoDbAttribute]` attributes THEN the system SHALL emit an error diagnostic (FDDB124) with the message: "Property '{PropertyName}' has both [Extracted] and [DynamoDbAttribute]. Extracted properties derive their value from a composite key and must not have independent DynamoDB attribute mapping. Remove one of the attributes."

2.2 WHEN the FDDB124 diagnostic is emitted THEN the system SHALL report it at Error severity, causing the build to fail and preventing the conflicting code from being generated

2.3 WHEN validation runs in `ValidateExtractedProperty()` THEN the system SHALL check `HasAttributeMapping` on each extracted property and report the diagnostic at the property's identifier location

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a property has only `[Extracted]` without `[DynamoDbAttribute]` THEN the system SHALL CONTINUE TO generate extraction-only code without error

3.2 WHEN a property has only `[DynamoDbAttribute]` without `[Extracted]` THEN the system SHALL CONTINUE TO generate standard serialization/deserialization code without error

3.3 WHEN a property has `[Extracted]` and references a valid computed source property THEN the system SHALL CONTINUE TO validate source property existence, constant key conflicts, and index bounds as before

3.4 WHEN a property has `[Extracted]` referencing a constant key property THEN the system SHALL CONTINUE TO emit the existing FDDB122 diagnostic

3.5 WHEN a property has `[Extracted]` with a negative index THEN the system SHALL CONTINUE TO emit the existing invalid index diagnostic
