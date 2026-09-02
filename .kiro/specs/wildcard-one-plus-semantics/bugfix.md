# Bugfix Requirements Document

## Introduction

The positional `IndexOf` checks generated for Complex pattern discrimination treat the wildcard `*` as "zero or more characters". This allows values ending with a trailing separator (e.g., `"ORDER#123#"`) to incorrectly pass the structural check for patterns like `"ORDER#*#*"`, because the final wildcard matches the empty string. Wildcards in key patterns should semantically mean "one or more characters" — an empty wildcard portion is not a valid key value in practice. This causes parent entities using trailing separators for query boundaries to be falsely claimed by child entity discriminators.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a discriminator value ends with a trailing separator (e.g., `"ORDER#123#"`) and the pattern contains multiple wildcards (e.g., `"ORDER#*#*"`) THEN the system incorrectly matches the value because `IndexOf("#", prefixLength) >= 0` succeeds even when the final wildcard portion is empty

1.2 WHEN the negated mode check is generated for a complex pattern THEN the system uses `IndexOf("#", prefixLength) < 0` which only rejects values with no separator at all, failing to reject values where the separator is at the terminal position

1.3 WHEN an exclusion check is generated for a complex pattern THEN the system uses `IndexOf("#", prefixLength) >= 0` which incorrectly includes values where the wildcard after the separator is empty (trailing separator)

1.4 WHEN a discriminator value has the separator immediately after the prefix (e.g., `"ORDER##LINE1"` for pattern `"ORDER#*#*"`) THEN the system incorrectly matches because the first wildcard is allowed to be zero characters

### Expected Behavior (Correct)

2.1 WHEN a discriminator value ends with a trailing separator (e.g., `"ORDER#123#"`) and the pattern contains multiple wildcards (e.g., `"ORDER#*#*"`) THEN the system SHALL reject the value by verifying the found separator index is strictly less than `Length - 1`, ensuring content exists after the separator

2.2 WHEN the negated mode check is generated for a complex pattern THEN the system SHALL use `IndexOf("#", prefixLength) < 0 || IndexOf("#", prefixLength) >= Length - 1` to also reject values where the separator is at the terminal position

2.3 WHEN an exclusion check is generated for a complex pattern THEN the system SHALL use `IndexOf("#", prefixLength) >= 0 && IndexOf("#", prefixLength) < Length - 1` to only include values where meaningful content exists after the separator

2.4 WHEN a discriminator value has the separator immediately after the prefix (e.g., `"ORDER##LINE1"` for pattern `"ORDER#*#*"`) THEN the system SHALL reject the value by using `prefixLength + 1` as the search offset, ensuring the first wildcard is also one-or-more characters

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a discriminator value has content in all wildcard positions (e.g., `"ORDER#123#LINE1"` for pattern `"ORDER#*#*"`) THEN the system SHALL CONTINUE TO correctly match the value

3.2 WHEN a pattern uses meaningful segments with `Contains` checks THEN the system SHALL CONTINUE TO operate correctly since `Contains` is unaffected by this change

3.3 WHEN a pattern starts with a wildcard (wildcard-first patterns) THEN the system SHALL CONTINUE TO operate correctly since these patterns do not use positional `IndexOf` checks

3.4 WHEN a simple prefix pattern with a single wildcard is used (e.g., `"ORDER#*"`) THEN the system SHALL CONTINUE TO correctly match values where content exists after the prefix

3.5 WHEN a discriminator value has multiple valid segments (e.g., `"ORDER#123#LINE1#DETAIL"` for pattern `"ORDER#*#*"`) THEN the system SHALL CONTINUE TO correctly match since the separator is found before the end of the string
