# Bugfix Requirements Document

## Introduction

`PatternOverlapAnalyzer.CreateExclusionPattern()` generates a tautological `Contains("<separator>")` exclusion guard when a Complex-strategy pattern like `"CAP#*#*"` overlaps with a simpler `"CAP#*"` StartsWith pattern. Any string that passes `StartsWith("CAP#")` inherently contains `"#"`, so the exclusion is always true — causing `MatchesEntity` to return `false` for all matching items. This makes the less-specific entity completely invisible to all Query, Scan, and Get operations with no error or warning at runtime.

A secondary issue exists in `GenerateComplexPatternCheck()` where the positive check for the more-specific entity also produces a non-discriminating `Contains("<separator>")` clause that adds zero filtering power.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a Complex pattern like `"CAP#*#*"` overlaps with a StartsWith pattern like `"CAP#*"` on the same discriminator attribute THEN the system generates an exclusion pattern with `Strategy = Contains` and `LiteralText = "#"` (the bare separator character between adjacent wildcards)

1.2 WHEN the generated `MatchesEntity` method evaluates an item whose discriminator value passes the positive `StartsWith("CAP#")` check THEN the exclusion `Contains("#")` is always true, causing the method to return `false` for ALL items — making the entity invisible to queries

1.3 WHEN `IsTautologicalExclusion` evaluates the generated exclusion THEN it fails to detect the semantic subsumption because it only checks for identity (same Strategy AND same LiteralText) rather than checking whether the exclusion literal is inherently contained within the positive match prefix

1.4 WHEN `GenerateComplexPatternCheck()` generates a positive match for pattern `"CAP#*#*"` THEN it produces `StartsWith("CAP#") && Contains("#")` where the `Contains("#")` adds zero discrimination power since `StartsWith("CAP#")` already implies the string contains `"#"`

1.5 WHEN the internal segment between adjacent wildcards is only the separator character (e.g., `"#"`, `"_"`, `":"`) THEN the system treats it as a meaningful distinguishing literal when it carries no discriminating information

### Expected Behavior (Correct)

2.1 WHEN a Complex pattern like `"CAP#*#*"` overlaps with a StartsWith pattern like `"CAP#*"` THEN the system SHALL generate an exclusion that checks for the separator character at a position AFTER the shared prefix length (e.g., `IndexOf('#', prefixLength) >= 0`) rather than a bare `Contains("#")` check

2.2 WHEN evaluating `MatchesEntity` for the less-specific entity with a value like `"CAP#capability1"` (single segment after prefix) THEN the exclusion check SHALL return false (not excluded), allowing the item to be correctly matched to the less-specific entity

2.3 WHEN evaluating `MatchesEntity` for the less-specific entity with a value like `"CAP#svc1#cap1"` (multiple segments after prefix) THEN the exclusion check SHALL return true (excluded), correctly identifying the item as belonging to the more-specific entity

2.4 WHEN `IsTautologicalExclusion` evaluates an exclusion THEN it SHALL detect semantic subsumption where the exclusion's literal text is guaranteed to be present in any value that passes the positive match check (i.e., the separator character appears within the prefix)

2.5 WHEN `GenerateComplexPatternCheck()` generates a positive match for patterns with bare-separator internal segments THEN it SHALL either omit the non-discriminating `Contains` clause or replace it with a positional check that provides actual discrimination power

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a Complex pattern has a meaningful internal segment (e.g., `"INVOICE#*#LINE#*"` where internal segment is `"#LINE#"`) THEN the system SHALL CONTINUE TO generate a `Contains("#LINE#")` exclusion that correctly discriminates between entity types

3.2 WHEN `CreateExclusionPattern()` processes an ExactMatch strategy pattern THEN the system SHALL CONTINUE TO return an exclusion with `Strategy = ExactMatch` and the exact value as `LiteralText`

3.3 WHEN `CreateExclusionPattern()` processes a non-Complex strategy pattern (StartsWith, EndsWith, Contains) THEN the system SHALL CONTINUE TO delegate to `DiscriminatorAnalyzer.GetPatternText()` for literal extraction

3.4 WHEN two entities have overlapping patterns where the more-specific pattern has meaningful distinguishing literals between wildcards THEN the system SHALL CONTINUE TO correctly exclude items belonging to the more-specific entity from the less-specific entity's results

3.5 WHEN entities have non-overlapping discriminator patterns THEN the system SHALL CONTINUE TO generate independent `MatchesEntity` checks without exclusion guards

3.6 WHEN `GenerateComplexPatternCheck()` generates checks for patterns with meaningful internal segments (e.g., `"INVOICE#*#LINE#*"`) THEN the system SHALL CONTINUE TO produce `StartsWith("INVOICE#") && Contains("#LINE#")` which provides genuine discrimination
