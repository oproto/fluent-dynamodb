# Design Document: DYNDB023 False Positive Fix

## Overview

Fix false positive DYNDB023 diagnostics ("Complex nested objects may cause performance issues") that incorrectly fire on enum properties, `[Extracted]` properties, and unmapped properties. Also remove a duplicate call to `ValidatePropertyPerformance` that produces duplicate diagnostics.

## Main Algorithm/Workflow

```mermaid
sequenceDiagram
    participant Loop as Outer Validation Loop
    participant VPM as ValidatePropertyModel
    participant VPP as ValidatePropertyPerformance

    Loop->>VPM: ValidatePropertyModel(property)
    VPM->>VPP: ValidatePropertyPerformance(property)
    VPP-->>VPM: (diagnostics reported)
    VPM-->>Loop: done
    Loop->>VPP: ValidatePropertyPerformance(property) ← DUPLICATE
    VPP-->>Loop: (duplicate diagnostics reported)
```

**After fix:**

```mermaid
sequenceDiagram
    participant Loop as Outer Validation Loop
    participant VPM as ValidatePropertyModel
    participant VPP as ValidatePropertyPerformance

    Loop->>VPM: ValidatePropertyModel(property)
    VPM->>VPP: ValidatePropertyPerformance(property)
    VPP->>VPP: Check early-exit guards
    VPP-->>VPM: (skipped or reported)
    VPM-->>Loop: done
```

## Core Interfaces/Types

```csharp
// PropertyModel already has the necessary properties:
internal class PropertyModel
{
    public string AttributeName { get; set; }       // Empty = not mapped to DynamoDB
    public bool HasAttributeMapping => !string.IsNullOrEmpty(AttributeName);
    public bool IsExtracted => ExtractedKey != null; // Source-only, never serialized
    public bool IsEnum { get; set; }                 // Roslyn TypeKind.Enum detection
    public bool IsRelatedEntity { get; set; }        // Composite entity pattern
}
```

## Key Functions with Formal Specifications

### Function: ValidatePropertyPerformance (after fix)

```csharp
private void ValidatePropertyPerformance(PropertyModel propertyModel)
```

**Preconditions:**
- `propertyModel` is non-null
- `propertyModel.PropertyType` is a non-empty string

**Postconditions:**
- No diagnostic is reported if `propertyModel.HasAttributeMapping` is false
- No diagnostic is reported if `propertyModel.IsExtracted` is true
- No diagnostic is reported if `propertyModel.IsEnum` is true
- No diagnostic is reported if `propertyModel.IsRelatedEntity` is true
- Diagnostic DYNDB023 is reported only for properties that are: mapped to DynamoDB, not extracted, not enum, not related entity, AND have a complex/binary type

**Loop Invariants:** N/A (no loops in this method)

## Algorithmic Pseudocode

### ValidatePropertyPerformance (Fixed)

```csharp
private void ValidatePropertyPerformance(PropertyModel propertyModel)
{
    // Guard 1: Skip properties not mapped to DynamoDB
    // No DynamoDB warning is relevant for unmapped properties
    if (!propertyModel.HasAttributeMapping)
        return;

    // Guard 2: Skip extracted properties
    // Source-only properties populated from computed keys at read time, never serialized
    if (propertyModel.IsExtracted)
        return;

    // Guard 3: Skip enum properties
    // Simple value types stored as string/int, not complex objects
    if (propertyModel.IsEnum)
        return;

    // Guard 4: Skip RelatedEntity properties (existing)
    // Intentionally designed for composite entity patterns
    if (propertyModel.IsRelatedEntity)
        return;

    // Binary data check
    if (propertyModel.PropertyType == "byte[]" || propertyModel.PropertyType == "System.Byte[]")
    {
        ReportDiagnostic(DiagnosticDescriptors.PerformanceWarning,
            propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
            propertyModel.PropertyName, propertyModel.PropertyType,
            "Binary data properties may cause performance issues...");
    }

    // Complex collection check
    if (propertyModel.IsCollection && IsComplexCollectionType(propertyModel.PropertyType))
    {
        ReportDiagnostic(DiagnosticDescriptors.PerformanceWarning,
            propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
            propertyModel.PropertyName, propertyModel.PropertyType,
            "Complex collection types may cause performance issues...");
    }

    // Complex nested object check
    if (!propertyModel.IsCollection && !IsPrimitiveType(propertyModel.PropertyType) &&
        propertyModel.PropertyType != "object" && !propertyModel.PropertyType.EndsWith("?"))
    {
        if (IsComplexNestedType(propertyModel.PropertyType))
        {
            ReportDiagnostic(DiagnosticDescriptors.PerformanceWarning,
                propertyModel.PropertyDeclaration?.Identifier.GetLocation(),
                propertyModel.PropertyName, propertyModel.PropertyType,
                "Complex nested objects may cause performance issues...");
        }
    }
}
```

### Duplicate Call Removal

```csharp
// BEFORE (outer loop at ~line 72-78):
foreach (var property in entityModel.Properties)
{
    ValidatePropertyModel(property, semanticModel);
    ValidatePropertyPerformance(property); // ← REMOVE: already called inside ValidatePropertyModel
}

// AFTER:
foreach (var property in entityModel.Properties)
{
    ValidatePropertyModel(property, semanticModel);
    // ValidatePropertyPerformance is called internally by ValidatePropertyModel
}
```

## Example Usage

```csharp
// Scenario 1: Enum property with [Extracted] — should NOT trigger DYNDB023
[Extracted(nameof(Topic), 1)]
public SnsSubscriptionTopic TopicType { get; set; }
// PropertyModel: IsExtracted=true, IsEnum=true, AttributeName="" → skipped at Guard 1

// Scenario 2: Mapped enum property — should NOT trigger DYNDB023
[DynamoDbAttribute("status")]
public OrderStatus Status { get; set; }
// PropertyModel: IsExtracted=false, IsEnum=true, AttributeName="status" → skipped at Guard 3

// Scenario 3: Unmapped property — should NOT trigger DYNDB023
public SomeCustomType InternalState { get; set; }
// PropertyModel: IsExtracted=false, IsEnum=false, AttributeName="" → skipped at Guard 1

// Scenario 4: Mapped complex type — SHOULD trigger DYNDB023
[DynamoDbAttribute("nested")]
public SomeComplexClass NestedData { get; set; }
// PropertyModel: IsExtracted=false, IsEnum=false, AttributeName="nested" → reaches complex check → reported
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do.*

### Property 1: Unmapped properties never produce DYNDB023

*For any* property model where `HasAttributeMapping` is false, `ValidatePropertyPerformance` SHALL produce zero DYNDB023 diagnostics regardless of the property's type, extraction status, or enum status.

**Validates: Requirement 1.1**

### Property 2: Extracted properties never produce DYNDB023

*For any* property model where `IsExtracted` is true, `ValidatePropertyPerformance` SHALL produce zero DYNDB023 diagnostics regardless of the property's type or mapping status.

**Validates: Requirement 1.2**

### Property 3: Enum properties never produce DYNDB023

*For any* property model where `IsEnum` is true, `ValidatePropertyPerformance` SHALL produce zero DYNDB023 diagnostics regardless of mapping status or other property attributes.

**Validates: Requirement 1.3**

### Property 4: Legitimate complex types still produce DYNDB023

*For any* property model where `HasAttributeMapping` is true, `IsExtracted` is false, `IsEnum` is false, `IsRelatedEntity` is false, and the property type passes `IsComplexNestedType`, `ValidatePropertyPerformance` SHALL produce exactly one DYNDB023 diagnostic.

**Validates: Requirement 1.4**

### Property 5: No duplicate diagnostics per property

*For any* entity model processed by the analyzer, each property that qualifies for a DYNDB023 diagnostic SHALL have exactly one such diagnostic reported (no duplicates from redundant validation calls).

**Validates: Requirements 2.1, 2.2**
