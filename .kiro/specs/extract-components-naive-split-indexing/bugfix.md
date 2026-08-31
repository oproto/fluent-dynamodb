# Bugfix Requirements Document

## Introduction

For entities using `[Computed(..., Format = "...")]` with constant literal segments in the format string, the generated `Extract{Property}Components()` methods and `FromDynamoDb` hydration code return wrong values. They use the `[Extracted]` attribute's `Index` (the placeholder position like `{0}`) directly as the array index into `parts[]` after splitting on the separator. This is only correct when there are no constant segments — when constant segments exist, the split array contains both constant and variable segments, and the placeholder index no longer corresponds to the correct split position.

The bug affects two code paths:
1. `KeysGenerator.GenerateExtractionHelper()` — generates `Extract{Property}Components()` static methods
2. `MapperGenerator.GenerateExtractedKeyLogic()` — generates `FromDynamoDb` property hydration

The forward path (`Keys.Pk()` / `Keys.Sk()`) works correctly because it uses `string.Format()` which places values at the right placeholder positions. Only the reverse path (extraction) is broken, meaning values can be built correctly but not round-tripped.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN an entity has a `[Computed]` property with `Format = "TENANT#{0}#EXTERNAL_ACCESS"` and an `[Extracted("Pk", 0)]` property THEN the `Keys.ExtractPkComponents()` method generates `parts[0]` which returns the constant `"TENANT"` instead of the actual value at placeholder `{0}` (which is at split index 1)

1.2 WHEN an entity has a `[Computed]` property with a custom format containing constant segments THEN the `FromDynamoDb` hydration code generates `pkParts[{placeholderIndex}]` which assigns constant literal text to the extracted property instead of the variable value

1.3 WHEN a format string has multiple variables with interspersed constants (e.g., `"TENANT#{0}#SHARE#RESOURCE#{1}#{2}"`) THEN all extracted properties after the first constant segment receive wrong values because placeholder indices 0, 1, 2 map to split indices 1, 4, 5 respectively

1.4 WHEN a format string has format specifiers (e.g., `"SEQ#{0:D4}"`) THEN the extraction also uses the wrong index because the format specifier does not change the constant-segment problem — `parts[0]` still returns the constant `"SEQ"` instead of `"0007"`

### Expected Behavior (Correct)

2.1 WHEN an entity has a `[Computed]` property with `HasCustomFormat == true` and an `[Extracted]` property THEN the `Keys.ExtractPkComponents()` method SHALL use the correct split index derived from parsing the format string, mapping placeholder `{N}` to its actual position in the split array

2.2 WHEN an entity has a `[Computed]` property with `HasCustomFormat == true` THEN the `FromDynamoDb` hydration code SHALL use the correct split index derived from parsing the format string, ensuring extracted properties receive the variable values, not constant literals

2.3 WHEN the format string is parsed to build the placeholder-to-split-index mapping THEN the parser SHALL recognize both `{N}` and `{N:format}` placeholder patterns, correctly handling format specifiers

2.4 WHEN a format string has multiple variables with interspersed constants THEN the bounds validation check (`parts.Length <= N`) SHALL use the maximum split index from the mapping, not the maximum placeholder index, to correctly guard array access

2.5 WHEN both `KeysGenerator` and `MapperGenerator` need the placeholder-to-split-index mapping THEN the mapping logic SHALL be implemented in a shared utility method to ensure consistency and avoid duplication

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a `[Computed]` property uses separator-based concatenation (no `Format` property, `HasCustomFormat == false`) THEN the generated extraction code SHALL CONTINUE TO use the placeholder index directly as the split index, producing identical output to the current code

3.2 WHEN a separator-based `[Computed]` property has multiple source properties THEN the `Keys.ExtractPkComponents()` method SHALL CONTINUE TO return the correct tuple with values at their placeholder indices

3.3 WHEN a separator-based `[Computed]` property is hydrated in `FromDynamoDb` THEN the extracted properties SHALL CONTINUE TO receive the correct values from the split array

3.4 WHEN a `[Computed]` property has string-typed `[Extracted]` properties THEN the type conversion behavior (direct assignment for strings) SHALL CONTINUE TO work correctly regardless of whether format-string or separator-based

3.5 WHEN a `[Computed]` property has non-string-typed `[Extracted]` properties (int, enum, etc.) THEN the type conversion behavior (`int.Parse`, `Enum.Parse<T>`, etc.) SHALL CONTINUE TO work correctly, applied to the value at the corrected split index

3.6 WHEN an entity has `[Extracted]` properties from a non-key `[Computed]` property THEN the fix SHALL apply equally — the bug and fix are not restricted to partition/sort keys
