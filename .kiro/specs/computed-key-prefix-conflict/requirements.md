# Requirements Document

## Introduction

This feature adds a new FDDB125 error diagnostic that fires when a property has both a `[Computed]` attribute and a `Prefix` configured on `[PartitionKey]` or `[SortKey]`. Computed keys manage their own value during serialization — the `Prefix` on the key attribute is intentionally NOT applied to the computed value. This creates a confusing situation where users declare a Prefix expecting it to appear in the stored DynamoDB value, but it is silently ignored. A real bug was discovered where an Invoice entity had `[SortKey(Prefix = "INVOICE")] [Computed("InvoiceNumber")]` and the prefix was never stored, causing Get operations to fail.

Two invalid configurations are detected:
1. Key attribute with Prefix + `[Computed]` WITHOUT explicit Format — the prefix is completely ignored
2. Key attribute with Prefix + `[Computed]` WITH explicit Format — the prefix is redundantly declared in both places

The fix for users is to remove the Prefix from the key attribute and either use `Format = "PREFIX#{0}"` on `[Computed]` (if the prefix should appear in the stored value) or omit it entirely (if no prefix is desired).

## Glossary

- **Source_Generator**: The Oproto.FluentDynamoDb source generator that analyzes entity classes and emits code and diagnostics at compile time
- **EntityAnalyzer**: The analysis component within the Source_Generator responsible for validating entity configurations and emitting diagnostics
- **Computed_Property**: A property decorated with `[Computed(...)]` that derives its value from other properties during serialization
- **Key_Property**: A property decorated with `[PartitionKey]` or `[SortKey]` that serves as a DynamoDB table key
- **Prefix**: The `Prefix` named parameter on `[PartitionKey]` or `[SortKey]` attributes (e.g., `Prefix = "ORDER"`)
- **Format**: The `Format` named parameter on `[Computed]` that specifies a custom format string for computing the value
- **FDDB125**: The new diagnostic code for computed key prefix conflict detection
- **DiagnosticDescriptors**: The static class containing all diagnostic descriptor definitions in the Source_Generator

## Requirements

### Requirement 1: Detect Computed Key With Prefix and No Format

**User Story:** As a developer, I want the source generator to emit an error when I configure a Prefix on a key attribute that also has [Computed] without an explicit Format, so that I am alerted to the fact that the prefix will never appear in the stored value.

#### Acceptance Criteria

1. WHEN a Key_Property has a non-empty Prefix AND is a Computed_Property AND the Computed_Property has no explicit Format, THEN THE Source_Generator SHALL emit FDDB125 as an error diagnostic at the property location
2. THE FDDB125 diagnostic message SHALL contain the property name, the configured Prefix value, and guidance that the prefix is ignored for computed keys
3. WHEN FDDB125 is emitted for a property, THE EntityAnalyzer SHALL continue processing remaining properties on the entity without halting analysis

### Requirement 2: Detect Computed Key With Prefix and Redundant Format

**User Story:** As a developer, I want the source generator to emit an error when I configure a Prefix on a key attribute that also has [Computed] with an explicit Format, so that I am alerted to the redundant and confusing configuration.

#### Acceptance Criteria

1. WHEN a Key_Property has a non-empty Prefix AND is a Computed_Property AND the Computed_Property has an explicit Format, THEN THE Source_Generator SHALL emit FDDB125 as an error diagnostic at the property location
2. THE FDDB125 diagnostic message SHALL contain the property name and the configured Prefix value
3. THE FDDB125 diagnostic SHALL fire regardless of whether the Format string contains the Prefix value or not

### Requirement 3: Diagnostic Descriptor Definition

**User Story:** As a developer maintaining the source generator, I want a well-defined FDDB125 diagnostic descriptor, so that the diagnostic integrates consistently with the existing diagnostic infrastructure.

#### Acceptance Criteria

1. THE DiagnosticDescriptors class SHALL define a static readonly DiagnosticDescriptor named `ComputedKeyPrefixConflict` with code "FDDB125"
2. THE FDDB125 descriptor SHALL have DiagnosticSeverity.Error
3. THE FDDB125 descriptor SHALL be enabled by default
4. THE FDDB125 descriptor SHALL use category "DynamoDb"
5. THE FDDB125 descriptor SHALL include a help link URI following the existing format pattern

### Requirement 4: No False Positives

**User Story:** As a developer, I want the diagnostic to only fire on truly invalid configurations, so that valid entity definitions do not produce spurious errors.

#### Acceptance Criteria

1. WHEN a Key_Property has a Prefix but is NOT a Computed_Property, THE Source_Generator SHALL NOT emit FDDB125
2. WHEN a Key_Property is a Computed_Property but has no Prefix configured, THE Source_Generator SHALL NOT emit FDDB125
3. WHEN a Key_Property has an empty or null Prefix AND is a Computed_Property, THE Source_Generator SHALL NOT emit FDDB125
4. WHEN a property has `[Computed]` but is NOT a Key_Property, THE Source_Generator SHALL NOT emit FDDB125

### Requirement 5: Update Existing Test Entities

**User Story:** As a developer maintaining the test suite, I want existing test entities that use the now-invalid Prefix + Computed pattern to be updated, so that the test suite compiles cleanly after the new diagnostic is introduced.

#### Acceptance Criteria

1. THE `ComputedPkWithPrefixTestEntity` in ComputedKeyExclusionPropertyTests SHALL have the Prefix removed from its `[PartitionKey]` attribute
2. THE `NonComputedPkComputedSkTestEntity` in PutComputedAndGsiIntegrationTests SHALL have the Prefix removed from its `[SortKey]` attribute on the computed SK property
3. AFTER Prefix removal, THE existing property-based tests SHALL continue to validate that computed keys do not receive prefix application (the behavioral property remains the same — computed values pass through unchanged)
4. AFTER Prefix removal, THE existing integration tests SHALL continue to validate the correct serialization behavior for computed and non-computed keys
