# Bugfix Requirements Document

## Introduction

The source generator (`MapperGenerator.cs`) emits `System.Text.RegularExpressions.Regex.IsMatch(sortKey, @"...")` calls in generated code for `[RelatedEntity]` composite entity pattern matching. This breaks Native AOT deployment, which the library explicitly targets (`IsAotCompatible = true`, `IsTrimmable = true`). The interpreted regex engine relies on runtime code paths that may be trimmed in AOT scenarios, causing failures. The AOT-safe alternative `[GeneratedRegex]` cannot be used because source generators cannot chain off each other's output in the same compilation pass.

The fix replaces all emitted `Regex.IsMatch` calls with plain string operations (`string.Split` + segment comparison) that the generator can emit directly. The wildcard patterns used by `[RelatedEntity]` are known at compile time and decompose into literal segments separated by a delimiter (typically `#`), where each `*` matches exactly one segment. This makes string-based matching both AOT-safe and faster than regex.

Five emission sites in `MapperGenerator.cs` emit `Regex.IsMatch`, plus a `ConvertWildcardPatternToRegex` helper that constructs the regex patterns:

1. `GenerateAsyncPrimaryEntityIdentification` — regex exclusion of related patterns (~line 2452)
2. `GenerateRelatedEntityCollectionMappingAsync` — pattern matching for collections (~line 2674)
3. `GenerateRelatedEntitySingleMappingAsync` — pattern matching for single entities (~line 2788)
4. `GeneratePrimaryEntityPatternMatching` — sync primary entity identification (~line 4277)
5. `GenerateSortKeyPatternMatching` — shared helper for sync collection and single mapping (~line 5829)

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a `[RelatedEntity]` attribute with a wildcard pattern (e.g., `"INVOICE#*#LINE#*"`) is defined on an entity THEN the source generator emits `System.Text.RegularExpressions.Regex.IsMatch(sortKey, @"^INVOICE\#[^#]*\#LINE\#[^#]*$")` in the generated mapper code

1.2 WHEN the generated code containing `Regex.IsMatch` calls is compiled for Native AOT (`PublishAot = true`) THEN the application may fail at runtime because the interpreted regex engine's code paths can be trimmed by the AOT compiler

1.3 WHEN the generated code containing `Regex.IsMatch` calls is compiled with trimming enabled (`PublishTrimmed = true`) THEN trim analysis warnings may be produced and the regex engine may not function correctly at runtime

1.4 WHEN the `ConvertWildcardPatternToRegex` helper method is invoked at source-generation time THEN it produces regex patterns (e.g., `^INVOICE\#[^#]*\#LINE\#[^#]*$`) that are emitted as string literals into generated code, introducing a runtime dependency on `System.Text.RegularExpressions`

### Expected Behavior (Correct)

2.1 WHEN a `[RelatedEntity]` attribute with a wildcard pattern (e.g., `"INVOICE#*#LINE#*"`) is defined on an entity THEN the source generator SHALL emit plain string operations (e.g., `var _segments = sortKey.Split('#'); if (_segments.Length == 4 && _segments[0] == "INVOICE" && _segments[2] == "LINE")`) instead of `Regex.IsMatch`

2.2 WHEN the generated code with string-based pattern matching is compiled for Native AOT (`PublishAot = true`) THEN the application SHALL function correctly at runtime without any AOT-related failures in composite entity matching

2.3 WHEN the generated code with string-based pattern matching is compiled with trimming enabled (`PublishTrimmed = true`) THEN no trim analysis warnings SHALL be produced for the pattern matching code and all composite entity matching SHALL function correctly

2.4 WHEN the source generator processes wildcard patterns at compile time THEN it SHALL decompose the pattern into literal segments and wildcard positions and emit segment-count and literal-segment equality checks, with no runtime dependency on `System.Text.RegularExpressions`

2.5 WHEN a `[RelatedEntity]` pattern contains multiple wildcards with a custom delimiter (e.g., `"TYPE_*_SUB_*"` using `_` as delimiter) THEN the source generator SHALL correctly infer the delimiter and emit `Split('_')` with the correct segment count and literal segment checks

2.6 WHEN a `[RelatedEntity]` pattern contains no wildcards (exact match pattern) THEN the source generator SHALL CONTINUE TO emit the existing exact-match logic (`sortKey == pattern || sortKey.StartsWith(pattern + delimiter)`) unchanged

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a `[RelatedEntity]` wildcard pattern like `"INVOICE#*#LINE#*"` is matched against a sort key `"INVOICE#INV-001#LINE#1"` THEN the system SHALL CONTINUE TO correctly identify and map the item as a related entity

3.2 WHEN a `[RelatedEntity]` wildcard pattern like `"INVOICE#*#LINE#*"` is matched against a sort key that does not match (e.g., `"ORDER#123"` or `"INVOICE#INV-001"`) THEN the system SHALL CONTINUE TO correctly reject the item as not matching the pattern

3.3 WHEN a composite entity query returns items with mixed sort key patterns (primary entity + related entities) THEN `ToCompositeEntityAsync` SHALL CONTINUE TO correctly assemble the composite entity with related collections populated and the primary entity identified

3.4 WHEN a `[RelatedEntity]` attribute uses an exact pattern (no wildcards) THEN the source generator SHALL CONTINUE TO emit exact-match string comparison logic without any changes

3.5 WHEN the sync and async mapper code paths process the same composite entity data THEN both paths SHALL CONTINUE TO produce identical entity mapping results
