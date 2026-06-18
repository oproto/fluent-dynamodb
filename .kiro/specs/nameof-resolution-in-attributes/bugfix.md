# Bugfix Requirements Document

## Introduction

The source generator fails to resolve `nameof()` expressions (and other compile-time constant expressions) when used as positional arguments in `[Computed]` and `[Extracted]` attributes. This produces incorrect warnings, generates broken code with missing arguments, and prevents users from using `nameof()` — a pattern the documentation teaches as the primary approach. The workaround is to use raw string literals, but that loses the refactoring safety that `nameof()` provides.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN `nameof()` is used as a positional argument in `[Computed]` attribute (e.g., `[Computed(nameof(UserId), Format = "USER#{0}")]`) THEN the system emits a spurious warning "Computed property has format that may produce invalid keys: Format requires N parameters but only 0 source properties provided"

1.2 WHEN `nameof()` is used as a positional argument in `[Computed]` attribute THEN the system generates broken code with an empty argument list in `string.Format()` (e.g., `string.Format("USER#{0}", )`)

1.3 WHEN `nameof()` is used as the first positional argument in `[Extracted]` attribute (e.g., `[Extracted(nameof(Pk), 0)]`) THEN the system emits an error "Extracted property references non-existent source property ''"

1.4 WHEN a `const string` variable is used as a positional argument in `[Computed]` or `[Extracted]` attributes THEN the system fails to resolve its value, producing the same defective behavior as `nameof()`

### Expected Behavior (Correct)

2.1 WHEN `nameof()` is used as a positional argument in `[Computed]` attribute THEN the system SHALL resolve it to its compile-time string value and include it in the SourceProperties array, behaving identically to a string literal

2.2 WHEN `nameof()` is used as a positional argument in `[Computed]` attribute THEN the system SHALL generate correct code with the resolved property name passed as the argument to `string.Format()`

2.3 WHEN `nameof()` is used as the first positional argument in `[Extracted]` attribute THEN the system SHALL resolve it to its compile-time string value and assign it to SourceProperty, behaving identically to a string literal

2.4 WHEN a `const string` variable is used as a positional argument in `[Computed]` or `[Extracted]` attributes THEN the system SHALL resolve it to its compile-time value, behaving identically to a string literal

### Unchanged Behavior (Regression Prevention)

3.1 WHEN string literals are used as positional arguments in `[Computed]` attribute (e.g., `[Computed("UserId", Format = "USER#{0}")]`) THEN the system SHALL CONTINUE TO correctly populate the SourceProperties array and generate valid code

3.2 WHEN string literals are used as the first positional argument in `[Extracted]` attribute (e.g., `[Extracted("Pk", 0)]`) THEN the system SHALL CONTINUE TO correctly assign the SourceProperty value

3.3 WHEN integer literals are used as the second positional argument in `[Extracted]` attribute (e.g., `[Extracted("Pk", 0)]`) THEN the system SHALL CONTINUE TO correctly assign the index value

3.4 WHEN named arguments (e.g., `Format = "USER#{0}"`, `Separator = "#"`) are used in `[Computed]` attribute THEN the system SHALL CONTINUE TO correctly extract their values
