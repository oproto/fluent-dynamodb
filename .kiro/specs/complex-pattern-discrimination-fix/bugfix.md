# Bugfix Requirements Document

## Introduction

The source generator's auto-derived discriminator system produces incorrect `MatchesEntity` code when Complex patterns (patterns with multiple wildcards like `"CAP#*#*"`) interact with simpler overlapping patterns (like `"CAP#*"`). This causes two critical failures:

1. **Invisible entities**: The less-specific entity's exclusion guard is tautologically true, making `MatchesEntity` always return false — the entity is invisible to all queries.
2. **False positives**: The more-specific entity's positive Complex check degrades to just a `StartsWith` after the non-discriminating `Contains` is removed, causing it to match items belonging to the less-specific entity.

Both issues stem from the same root cause: when a Complex pattern like `"CAP#*#*"` is split on `*`, the internal segment `"#"` (the separator between adjacent wildcards) is already contained within the prefix `"CAP#"`. A `Contains("#")` check adds zero discrimination after `StartsWith("CAP#")` has passed.

The existing partial fix addresses the exclusion side (OffsetIndex on ExclusionPattern, IndexOf generation in MapperGenerator) but REMOVES the non-discriminating `Contains` from the positive Complex check without REPLACING it with a positional equivalent. This gap must be closed.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a less-specific entity (pattern `"CAP#*"`) needs to exclude items from a more-specific entity (pattern `"CAP#*#*"`) AND all internal segments of the more-specific pattern are bare separators already contained in the prefix THEN the system generates a `Contains("#")` exclusion that is always true after the positive `StartsWith("CAP#")` check passes, causing `MatchesEntity` to always return false

1.2 WHEN a more-specific entity with a Complex pattern (e.g., `"CAP#*#*"`) generates its positive `MatchesEntity` check AND the internal segment (e.g., `"#"`) is already contained in the prefix segment (e.g., `"CAP#"`) THEN the system removes the non-discriminating `Contains` check without replacing it with a positional equivalent, causing the positive check to degrade to just `StartsWith("CAP#")` which also matches items from the less-specific entity

1.3 WHEN a Complex pattern's positive check degrades to only `StartsWith` AND both the less-specific and more-specific entities share the same prefix THEN the system produces false positives in GSI queries, Scan operations, or CompoundEntityResult scenarios where items from both entities appear in the same result set

### Expected Behavior (Correct)

2.1 WHEN a less-specific entity (pattern `"CAP#*"`) needs to exclude items from a more-specific entity (pattern `"CAP#*#*"`) AND all internal segments are bare separators contained in the prefix THEN the system SHALL generate a positional `IndexOf(separator, prefixLength) >= 0` exclusion check that verifies the separator exists BEYOND the prefix boundary

2.2 WHEN a more-specific entity with a Complex pattern (e.g., `"CAP#*#*"`) generates its positive `MatchesEntity` check AND the internal segment is a bare separator already contained in the prefix THEN the system SHALL generate a positional `IndexOf(separator, prefixLength) >= 0` check that verifies the separator exists beyond the prefix, providing structural discrimination between one-segment and multi-segment values

2.3 WHEN entities with custom discriminator configurations (DiscriminatorProperty/DiscriminatorValue/DiscriminatorPattern) are present THEN the system SHALL continue to use the user-specified discriminator logic unchanged, with no impact from changes to auto-derivation

2.4 WHEN a Complex pattern has meaningful (non-bare) internal segments (e.g., `"INVOICE#*#LINE#*"` where `"#LINE#"` is not contained in `"INVOICE#"`) THEN the system SHALL continue to generate `Contains("segment")` checks for those segments as it does today

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a Complex pattern has meaningful internal segments that are NOT contained in the prefix (e.g., `"INVOICE#*#LINE#*"` producing `Contains("#LINE#")`) THEN the system SHALL CONTINUE TO generate standard `Contains` checks for those segments

3.2 WHEN entities use simple (single-wildcard) patterns like `"ORDER#*"` that do not overlap with any Complex pattern THEN the system SHALL CONTINUE TO generate `StartsWith("ORDER#")` checks without any positional logic

3.3 WHEN entities have user-specified DiscriminatorProperty, DiscriminatorValue, or DiscriminatorPattern attributes THEN the system SHALL CONTINUE TO use the custom discriminator logic unmodified

3.4 WHEN the exclusion pattern has an `OffsetIndex > 0` (already using positional IndexOf) THEN the system SHALL CONTINUE TO generate `IndexOf(literal, offset) >= 0` as it does in the existing partial fix

3.5 WHEN two entities have different PK prefixes providing compound discrimination via CompoundPromotionPass THEN the system SHALL CONTINUE TO generate compound constraint checks on the cross-key attribute as it does today

3.6 WHEN a Complex pattern starts with a wildcard (e.g., `"*#SUFFIX#*"`) THEN the system SHALL CONTINUE TO generate `Contains` checks for all non-empty segments as it does today

3.7 WHEN the separator character varies (e.g., `"_"`, `":"`, or any character) THEN the positional fix SHALL work correctly for any separator, not just `"#"`
