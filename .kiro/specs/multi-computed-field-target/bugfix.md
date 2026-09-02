# Bugfix Requirements Document

## Introduction

`PropertyMetadata.ComputedFieldTarget` is typed as `string?`, limiting it to a single computed field target. When a source property contributes to multiple computed fields (e.g., `Status` is a source of both `Gsi1Pk` and `Gsi2Pk`), the MetadataGenerator only records whichever computed field `FirstOrDefault` encounters first. The other computed field's relationship is silently lost. This must become `string[]?` (`ComputedFieldTargets`) to correctly model all source-to-computed-field relationships.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a source property is listed in the `SourceProperties` of multiple non-key computed fields THEN the MetadataGenerator emits only the first computed field found via `FirstOrDefault` as the `ComputedFieldTarget` value

1.2 WHEN `PropertyMetadata.ComputedFieldTarget` is read by any code expecting the complete set of targets THEN only one target name is available, making the metadata incomplete and incorrect for multi-target scenarios

1.3 WHEN downstream code or tooling inspects `ComputedFieldTarget` to determine which computed fields depend on a source property THEN it sees only one target and cannot discover the additional computed fields that also depend on that source

### Expected Behavior (Correct)

2.1 WHEN a source property is listed in the `SourceProperties` of multiple non-key computed fields THEN the MetadataGenerator SHALL emit all matching computed field names into a `ComputedFieldTargets` array property

2.2 WHEN `PropertyMetadata.ComputedFieldTargets` is read THEN it SHALL contain the complete list of all non-key computed fields that list this property as a source

2.3 WHEN `IsComputedSourceProperty` checks whether a property is a source of a computed field THEN it SHALL return true if `ComputedFieldTargets` is non-null and has length greater than zero

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a source property contributes to only one non-key computed field THEN the system SHALL CONTINUE TO correctly identify it as a computed source property and emit its single target in the `ComputedFieldTargets` array

3.2 WHEN a source property does not contribute to any non-key computed field THEN the system SHALL CONTINUE TO leave `ComputedFieldTargets` as null and `IsComputedSourceProperty` SHALL CONTINUE TO return false

3.3 WHEN `ValidateAndProcessComputedFields` iterates all computed fields to validate source assignments and generate recomputation expressions THEN the system SHALL CONTINUE TO correctly validate and recompute each computed field independently (this loop is already correct and must not regress)

3.4 WHEN FDDB072 validation checks that all source properties of a computed field are assigned THEN the system SHALL CONTINUE TO fire FDDB072 independently for each computed field that has missing sources

3.5 WHEN `IsComputedSourceProperty` detects a property that is an extracted property targeting a non-key computed field THEN the system SHALL CONTINUE TO return true via the existing extracted-field path
