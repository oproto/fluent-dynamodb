# Bugfix Requirements Document

## Introduction

The source generator's `PatternOverlapAnalyzer.CreateExclusionPattern()` method can produce tautological (contradictory) exclusion guards when a Complex pattern's extracted exclusion literal is identical to the less-specific entity's own positive match literal. This causes the generated `MatchesEntity` method to always return `false`, silently preventing the entity from ever matching any items during composite entity assembly or multi-entity queries.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a Contains-strategy entity (e.g., pattern `*#ROLE#*`) overlaps with a Complex-strategy entity (e.g., pattern `USER#*#ROLE#*`) AND CreateExclusionPattern extracts the last internal segment of the Complex pattern (e.g., `#ROLE#`) which is identical to the Contains entity's own positive match literal THEN the system generates a MatchesEntity method where the positive check (`Contains("#ROLE#")`) and the exclusion guard (`Contains("#ROLE#")`) are contradictory, causing the method to always return false

1.2 WHEN the generated MatchesEntity method contains a tautological exclusion guard THEN the system silently produces unreachable code (the `return true` statement is never reached) without emitting any compile-time diagnostic warning or error

### Expected Behavior (Correct)

2.1 WHEN a computed exclusion guard would use the same strategy and literal text as the entity's own positive match criterion (i.e., the exclusion is tautological) THEN the system SHALL emit a diagnostic error (e.g., DISC006) at compile time instead of generating contradictory code

2.2 WHEN a tautological exclusion is detected THEN the system SHALL NOT populate the OverlappingPatterns list for that entity, preventing broken code generation

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a less-specific entity's positive match criterion uses a different strategy or literal text than the computed exclusion guard (e.g., StartsWith "USER#" excluded by Contains "#ROLE#") THEN the system SHALL CONTINUE TO generate valid exclusion guards that correctly carve out more-specific entity subsets

3.2 WHEN two overlapping patterns have the same specificity score THEN the system SHALL CONTINUE TO emit the DISC004 ambiguous overlap diagnostic error

3.3 WHEN overlapping patterns are successfully resolved by specificity ordering with valid (non-tautological) exclusions THEN the system SHALL CONTINUE TO emit the DISC005 informational diagnostic and populate OverlappingPatterns correctly

3.4 WHEN entities have non-overlapping discriminator patterns on the same property THEN the system SHALL CONTINUE TO generate independent MatchesEntity methods without exclusion guards
