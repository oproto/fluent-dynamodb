# Bugfix Requirements Document

## Introduction

The compound discrimination feature (FDDB104) resolves same-score discriminator overlaps by inspecting cross-key `DerivedDiscriminatorPattern` values. However, when a cross-key pattern is classified as `Complex` (contains 2+ wildcards, e.g., `TENANT#*#ROLE#*`), the `GetEffectiveCrossKeyPattern` method in `CompoundPromotionPass.cs` returns `null`, preventing the entity from participating in compound promotion. This causes FDDB102 warnings to persist for entity pairs that could be disambiguated using the leading prefix segment of Complex patterns. The fix should extract the leading prefix from Complex patterns and use a `StartsWith` strategy for compound constraints, enabling disambiguation where the prefixes differ.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN two entities share a same-score SK discriminator overlap AND one entity's cross-key PK pattern is classified as `Complex` (e.g., `TENANT#*#ROLE#*` with 2+ wildcards) THEN the system treats that Complex pattern as `null` in `GetEffectiveCrossKeyPattern`, preventing compound promotion even when the leading prefix differs from the other entity's PK pattern

1.2 WHEN both entities have Complex cross-key PK patterns with different leading prefixes (e.g., `TENANT#*#ROLE#*` vs `SERVICE#*#REGION#*`) THEN the system treats both as `null` and classifies the pair as not disambiguable, leaving FDDB102 warnings unresolved

1.3 WHEN an entity with a Complex PK pattern like `TENANT#*#ROLE#*` overlaps with an entity whose PK pattern is `SERVICE#*` (StartsWith strategy) THEN the system assigns an exclusion guard to the Complex entity instead of recognizing both have distinguishable prefixes (`TENANT#` vs `SERVICE#`) and assigning positive constraints to both

### Expected Behavior (Correct)

2.1 WHEN two entities share a same-score SK discriminator overlap AND one entity's cross-key PK pattern is `Complex` with a non-empty leading prefix (text before the first wildcard) THEN the system SHALL extract that leading prefix, use `StartsWith` strategy, and treat it as a valid (non-null) effective cross-key pattern for compound promotion

2.2 WHEN both entities have Complex cross-key PK patterns with different leading prefixes (e.g., `TENANT#` from `TENANT#*#ROLE#*` vs `SERVICE#` from `SERVICE#*#REGION#*`) THEN the system SHALL treat both as valid effective patterns and resolve the pair via dual positive compound constraints using `StartsWith` with their respective prefixes

2.3 WHEN an entity with a Complex PK pattern like `TENANT#*#ROLE#*` overlaps with an entity whose PK pattern is `SERVICE#*` THEN the system SHALL assign positive compound constraints to both entities — `StartsWith("TENANT#")` for the Complex entity and `StartsWith("SERVICE#")` for the simple entity — rather than treating the Complex entity as having a null pattern

2.4 WHEN a Complex cross-key pattern has no leading prefix (starts with a wildcard, e.g., `*#ROLE#*#TENANT#*`) THEN the system SHALL continue to treat it as `null` since no `StartsWith` prefix can be extracted

### Unchanged Behavior (Regression Prevention)

3.1 WHEN both entities have non-Complex cross-key patterns (StartsWith, ExactMatch, EndsWith, Contains) THEN the system SHALL CONTINUE TO resolve compound promotion exactly as before, using the full pattern and its native strategy

3.2 WHEN both entities have `null` cross-key patterns (no `DerivedDiscriminatorPattern`) THEN the system SHALL CONTINUE TO classify the pair as not disambiguable

3.3 WHEN both entities have identical cross-key patterns (whether Complex or non-Complex) THEN the system SHALL CONTINUE TO classify the pair as not disambiguable

3.4 WHEN entities have different specificity scores (not same-score overlap) THEN the system SHALL CONTINUE TO resolve via the existing exclusion guard mechanism in `PatternOverlapAnalyzer` without any change

3.5 WHEN assigning a positive `CompoundConstraint`, the `Strategy` and `LiteralText` fields SHALL CONTINUE TO be derived from `DiscriminatorAnalyzer.DeterminePatternStrategy` and `DiscriminatorAnalyzer.GetPatternText` for non-Complex patterns

3.6 WHEN a Complex cross-key pattern's extracted leading prefix is identical to the other entity's effective prefix (e.g., both start with `TENANT#`) THEN the system SHALL CONTINUE TO classify the pair as not disambiguable (identical effective patterns)
