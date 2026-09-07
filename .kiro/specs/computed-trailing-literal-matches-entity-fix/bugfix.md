# Bugfix Requirements Document

## Introduction

When a `[Computed]` format string ends with a trailing literal suffix after the last `{N}` placeholder (e.g., `"EMPLOYEE#{0}#"`), the auto-derived `MatchesEntity` discrimination check incorrectly rejects items whose sort key value ends with that trailing literal. The generated `IndexOf` check uses `< Length - 1`, which enforces that at least one character must exist after the found separator. This is wrong when the pattern ends with a literal — the separator IS the terminal character and the value is valid. This is a silent data loss bug: items that should be returned from `Query<TEntity>()` and `Scan<TEntity>()` are dropped with no error or indication.

The bug exists in three code generation paths within `MapperGenerator.cs`:
- `GenerateComplexPatternCheck` in "return" mode
- `GenerateComplexPatternCheck` in "negated" mode
- `GenerateComplexExclusionCheck`

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a Complex discriminator pattern ends with a literal after the last wildcard (e.g., `"EMPLOYEE#*#"`) AND `GenerateComplexPatternCheck` is invoked in "return" mode THEN the system generates a trailing-position check `IndexOf(separator, offset) < discriminatorValue.S.Length - 1` that rejects values where the separator is the terminal character, causing valid items to be silently excluded

1.2 WHEN a Complex discriminator pattern ends with a literal after the last wildcard (e.g., `"EMPLOYEE#*#"`) AND `GenerateComplexPatternCheck` is invoked in "negated" mode THEN the system generates a negated trailing-position check `IndexOf(separator, offset) >= discriminatorValue.S.Length - 1` that incorrectly flags values where the separator is the terminal character as non-matching, causing valid items to be silently excluded

1.3 WHEN a Complex discriminator pattern ends with a literal after the last wildcard (e.g., `"EMPLOYEE#*#"`) AND `GenerateComplexExclusionCheck` is invoked THEN the system generates an exclusion guard with `IndexOf(separator, offset) < discriminatorValue.S.Length - 1` that fails to match values where the trailing literal is the terminal character, causing the exclusion guard to not fire when it should

1.4 WHEN an entity with format `"EMPLOYEE#{0}#"` is used in a multi-entity table query AND a DynamoDB item has sort key value `"EMPLOYEE#abc123#"` THEN the system's `MatchesEntity` check returns false and the item is silently dropped from query results

### Expected Behavior (Correct)

2.1 WHEN a Complex discriminator pattern ends with a literal after the last wildcard (e.g., `"EMPLOYEE#*#"`) AND `GenerateComplexPatternCheck` is invoked in "return" mode THEN the system SHALL generate a trailing bare-separator check that only requires `IndexOf(separator, offset) >= 0` without the `< Length - 1` constraint, accepting values where the separator is the terminal character

2.2 WHEN a Complex discriminator pattern ends with a literal after the last wildcard (e.g., `"EMPLOYEE#*#"`) AND `GenerateComplexPatternCheck` is invoked in "negated" mode THEN the system SHALL generate a negated trailing bare-separator check that only requires `IndexOf(separator, offset) < 0` without the `>= Length - 1` branch, correctly accepting values where the separator is the terminal character

2.3 WHEN a Complex discriminator pattern ends with a literal after the last wildcard (e.g., `"EMPLOYEE#*#"`) AND `GenerateComplexExclusionCheck` is invoked THEN the system SHALL generate an exclusion guard with a trailing bare-separator check that only requires `IndexOf(separator, offset) >= 0` without the `< Length - 1` constraint, correctly matching values where the trailing literal is the terminal character

2.4 WHEN an entity with format `"EMPLOYEE#{0}#"` is used in a multi-entity table query AND a DynamoDB item has sort key value `"EMPLOYEE#abc123#"` THEN the system's `MatchesEntity` check SHALL return true, correctly including the item in query results

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a Complex discriminator pattern ends with a wildcard (e.g., `"INVOICE#*#LINE#*"`) AND `GenerateComplexPatternCheck` is invoked in "return" mode THEN the system SHALL CONTINUE TO generate Contains-based checks for meaningful internal segments and positional `IndexOf` checks with `< Length - 1` for non-trailing bare-separator segments

3.2 WHEN a Complex discriminator pattern ends with a wildcard (e.g., `"ORDER#*"`) AND a simple StartsWith strategy is used THEN the system SHALL CONTINUE TO generate only a `StartsWith` check without any trailing-position logic

3.3 WHEN a Complex discriminator pattern has a non-trailing bare-separator segment (e.g., `"PREFIX#*#*#"` where the first `#` after the prefix is not the last segment) THEN the system SHALL CONTINUE TO enforce `< Length - 1` for non-trailing bare-separator segments, ensuring at least one character follows the separator

3.4 WHEN a Complex discriminator pattern ends with a wildcard (e.g., `"INVOICE#*#LINE#*"`) AND `GenerateComplexPatternCheck` is invoked in "negated" mode THEN the system SHALL CONTINUE TO generate negated Contains-based checks for meaningful internal segments and negated positional `IndexOf` checks with `>= Length - 1` for non-trailing bare-separator segments

3.5 WHEN a Complex discriminator pattern ends with a wildcard (e.g., `"INVOICE#*#LINE#*"`) AND `GenerateComplexExclusionCheck` is invoked THEN the system SHALL CONTINUE TO generate exclusion guards using Contains-based checks for meaningful internal segments and positional `IndexOf` checks with `< Length - 1` for non-trailing bare-separator segments

3.6 WHEN a pattern starts with a wildcard (e.g., `"*#SUFFIX#*"`) THEN the system SHALL CONTINUE TO use Contains-based checks for all segments without positional IndexOf logic
