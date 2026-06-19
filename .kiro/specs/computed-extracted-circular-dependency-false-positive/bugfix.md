# Bugfix Requirements Document

## Introduction

DYNDB033 "Circular dependency detected between computed properties" is incorrectly triggered when `[Computed]` and `[Extracted]` are used on the same pair of properties in the intended round-trip (bidirectional mapping) pattern. This is a false positive because `[Computed]` operates on the write path (ToDynamoDb) and `[Extracted]` operates on the read path (FromDynamoDb) — they never execute in the same direction and therefore cannot form a circular dependency.

The false positive prevents users from implementing the standard bidirectional mapping pattern where a computed key is built on write and decomposed back into component properties on read.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a property has `[Extracted(nameof(SourceProp), index)]` AND the source property has `[Computed(nameof(ExtractedProp), ...)]` (i.e., the extracted property is listed as a source of the computed property) THEN the system reports DYNDB033 "Circular dependency detected between computed properties"

1.2 WHEN the `[Computed]` and `[Extracted]` attributes form a valid bidirectional mapping (write-path composition, read-path decomposition) THEN the system incorrectly treats this as an unresolvable circular dependency

### Expected Behavior (Correct)

2.1 WHEN a property has `[Extracted(nameof(SourceProp), index)]` AND the source property has `[Computed(nameof(ExtractedProp), ...)]` forming a bidirectional mapping THEN the system SHALL NOT report DYNDB033

2.2 WHEN `[Computed]` and `[Extracted]` form a valid round-trip pattern (write-path: source properties → computed target; read-path: computed target → extracted back into source properties) THEN the system SHALL allow this without diagnostics, recognizing that these operate in opposite data-flow directions

### Unchanged Behavior (Regression Prevention)

3.1 WHEN property A has `[Computed(nameof(B), ...)]` AND property B has `[Computed(nameof(A), ...)]` (a genuine Computed→Computed cycle) THEN the system SHALL CONTINUE TO report DYNDB033 via the existing DFS-based `ValidateComputedKeyCircularDependencies` check

3.2 WHEN a property has `[Extracted]` referencing a source property that is NOT marked as `[Computed]` THEN the system SHALL CONTINUE TO validate `[Extracted]` properties normally (no change to other Extracted validations)

3.3 WHEN a property has `[Computed]` with multiple source properties forming a chain (A→B→C→A all via `[Computed]`) THEN the system SHALL CONTINUE TO report DYNDB033 for the genuine circular dependency

3.4 WHEN a property has `[Extracted]` referencing a non-existent source property THEN the system SHALL CONTINUE TO report the appropriate diagnostic error
