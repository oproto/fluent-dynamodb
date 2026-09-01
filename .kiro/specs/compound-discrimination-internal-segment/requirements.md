# Requirements Document

## Introduction

This feature enhances `CompoundPromotionPass` to resolve same-score discriminator overlaps when two entities share the same reduced cross-key prefix but one entity's original Complex pattern contains a distinguishing internal segment that the other entity's pattern lacks. After the `compound-discrimination-complex-pattern-fix`, Complex patterns like `TENANT#*#ROLE#*` are reduced to the prefix `TENANT#*` for compound promotion. When both entities reduce to the same prefix (e.g., `TENANT#*#ROLE#*` → `TENANT#*` and `TENANT#*` → `TENANT#*`), the pair is currently classified as "not disambiguable." However, the entity with the Complex pattern has an internal segment (`#ROLE#`) that can be used as a `Contains`-based compound constraint, making the pair distinguishable. This enhancement adds a fallback resolution path in `CompoundPromotionPass.Analyze` for same-prefix pairs where internal-segment extraction enables disambiguation.

## Glossary

- **Compound_Promotion_Pass**: The analysis pass in `CompoundPromotionPass.cs` that runs after `PatternOverlapAnalyzer.Analyze` to resolve same-score discriminator overlaps using cross-key pattern disambiguation
- **Internal_Segment**: A non-empty literal text segment between wildcards in a Complex cross-key pattern, excluding the leading prefix segment. For `TENANT#*#ROLE#*`, the internal segment is `#ROLE#`. Extracted by splitting the pattern on `*`, skipping the first non-empty segment (the prefix), and selecting a remaining non-empty segment
- **Reduced_Prefix**: The effective cross-key pattern produced by `GetEffectiveCrossKeyPattern` for Complex patterns — the text before the first `*` plus `*` (e.g., `TENANT#*#ROLE#*` → `TENANT#*`). Introduced by the `compound-discrimination-complex-pattern-fix`
- **Same_Prefix_Pair**: Two entities in a same-score overlap where both entities' effective cross-key patterns (after prefix extraction) are identical, causing `AreDisambiguable` to return false
- **Compound_Constraint**: A secondary cross-key constraint (`CompoundConstraint` model) assigned to an entity's `DiscriminatorConfig`, representing an additional AND condition on the cross-key property. Already supports `Contains` strategy
- **More_Specific_Entity**: The entity in a same-prefix pair whose original Complex cross-key pattern contains an internal segment that the other entity's pattern does not. This entity receives a positive `Contains` compound constraint
- **Less_Specific_Entity**: The entity in a same-prefix pair whose cross-key pattern does not contain the distinguishing internal segment. This entity receives an exclusion guard based on the more-specific entity's internal segment
- **Source_Generator**: The Roslyn-based C# source generator (`DynamoDbSourceGenerator`) that analyzes entity attributes at compile time and emits mapper code
- **PatternOverlapAnalyzer**: The existing analysis pass that detects pattern overlaps, computes specificity scores, and populates exclusion guards. Contains `CreateExclusionPattern` which already implements internal segment extraction logic for Complex patterns
- **MatchesEntity_Method**: The generated static method `MatchesEntity(Dictionary<string, AttributeValue> item)` on each entity mapper that determines whether a DynamoDB item belongs to that entity type

## Requirements

### Requirement 1: Internal Segment Detection for Same-Prefix Pairs

**User Story:** As a developer using single-table DynamoDB designs where entities share the same partition key prefix but differ by internal key segments (e.g., `TENANT#*#ROLE#*` vs `TENANT#*`), I want the source generator to detect that the internal segment can disambiguate the entities, so that I do not receive unresolvable FDDB102 warnings.

#### Acceptance Criteria

1. WHEN two entities have a same-score discriminator overlap AND both entities' effective cross-key patterns (after prefix extraction) are identical AND at least one entity's original cross-key pattern is Complex with a distinguishing internal segment (a non-empty internal segment that is not contained within the prefix segment), THE Compound_Promotion_Pass SHALL classify the pair as disambiguable via internal-segment discrimination
2. WHEN both entities' effective cross-key patterns are identical AND neither entity's original cross-key pattern is Complex, THE Compound_Promotion_Pass SHALL NOT classify the pair as disambiguable (existing behavior preserved)
3. WHEN both entities' effective cross-key patterns are identical AND both entities' original cross-key patterns are Complex with identical distinguishing internal segments (string-equal extracted segment values), THE Compound_Promotion_Pass SHALL NOT classify the pair as disambiguable
4. WHEN the effective cross-key patterns already differ (different prefixes, or one null and one non-null), THE Compound_Promotion_Pass SHALL continue to resolve the pair using the existing prefix-based disambiguation logic without invoking internal-segment detection
5. WHEN both entities' effective cross-key patterns are identical AND at least one entity's original cross-key pattern is Complex but no entity yields a distinguishing internal segment (all internal segments are bare separators contained within the prefix), THE Compound_Promotion_Pass SHALL NOT classify the pair as disambiguable via internal-segment discrimination

### Requirement 2: Internal Segment Extraction from Complex Patterns

**User Story:** As a developer, I want the source generator to correctly extract distinguishing internal segments from Complex cross-key patterns, so that the compound constraint uses the right literal text for the `Contains` check.

#### Acceptance Criteria

1. WHEN a Complex cross-key pattern has one or more non-empty internal segments after splitting on `*` and skipping the first non-empty segment (the prefix), THE Compound_Promotion_Pass SHALL extract an internal segment by iterating from the last internal segment to the first and selecting the first segment that is not contained within the prefix segment, producing a Compound_Constraint with `None` strategy, the selected segment as `LiteralText`, and `OffsetIndex` equal to the prefix length, to generate a positional `IndexOf` check
2. WHEN multiple internal segments exist in a Complex pattern (e.g., `A#*#BC#*#DEF#*`), THE Compound_Promotion_Pass SHALL select the last non-empty internal segment that is not contained within the prefix segment, to avoid tautological checks and to match the selection order used by `PatternOverlapAnalyzer.CreateExclusionPattern`, producing a Compound_Constraint with `None` strategy and `OffsetIndex` equal to the prefix length
3. WHEN all internal segments of a Complex pattern are contained within the prefix segment (bare separators, e.g., `CAP#*#*` where the only internal segment is `#`), THE Compound_Promotion_Pass SHALL produce a Compound_Constraint with `None` strategy, the bare separator as `LiteralText`, and an offset index equal to the length of the prefix segment (e.g., 4 for prefix `CAP#`), matching the positional approach used by `PatternOverlapAnalyzer.CreateExclusionPattern`
4. WHEN a non-Complex cross-key pattern has the same reduced prefix as a Complex entity's pattern (e.g., `TENANT#*` as a simple StartsWith pattern vs `TENANT#*#ROLE#*` as a Complex pattern), THE Compound_Promotion_Pass SHALL treat the non-Complex entity as having no internal segment

### Requirement 3: Compound Constraint Assignment for Internal-Segment Pairs

**User Story:** As a developer, I want entities disambiguated via internal segments to receive the correct compound constraints, so that the generated `MatchesEntity` methods produce mutually exclusive results.

#### Acceptance Criteria

1. WHEN one entity has a Complex cross-key pattern with a distinguishing internal segment and the other entity has a non-Complex pattern with the same prefix, THE Compound_Promotion_Pass SHALL assign a positive positional Compound_Constraint to the More_Specific_Entity with `IsExclusion` set to false, `PropertyName` set to the DynamoDB attribute name of the cross-key property, `Strategy` set to `None`, `LiteralText` set to the extracted internal segment, and `OffsetIndex` set to the prefix length
2. WHEN one entity has a Complex cross-key pattern with a distinguishing internal segment and the other entity has a non-Complex pattern with the same prefix, THE Compound_Promotion_Pass SHALL assign an exclusion-guard Compound_Constraint to the Less_Specific_Entity with `IsExclusion` set to true, `PropertyName` set to the same cross-key attribute name, `Strategy` set to `None`, `LiteralText` set to the More_Specific_Entity's extracted internal segment, and `OffsetIndex` set to the prefix length
3. WHEN both entities have Complex cross-key patterns with the same prefix but different distinguishing internal segments (e.g., `TENANT#*#ROLE#*` vs `TENANT#*#DEPT#*`), THE Compound_Promotion_Pass SHALL assign a positive positional Compound_Constraint to each entity using its respective internal segment as the `LiteralText` with `Strategy` set to `None` and `OffsetIndex` set to the prefix length
4. ALL internal-segment Compound_Constraints SHALL use `Strategy=None` with `OffsetIndex` equal to the prefix length, generating `IndexOf(LiteralText, OffsetIndex)` checks instead of `Contains(LiteralText)`, to prevent false matches from coincidental substring presence in wildcard values within the prefix portion
5. WHEN the More_Specific_Entity's internal segment requires a positional check (all internal segments are bare separators contained within the prefix), THE Compound_Constraint for the More_Specific_Entity SHALL use the `None` strategy with an offset index equal to the length of the reduced prefix segment, and `LiteralText` set to the bare separator, matching the approach used by `PatternOverlapAnalyzer.CreateExclusionPattern`
6. WHEN the More_Specific_Entity's internal segment requires a positional check (bare separator contained within the prefix), THE Compound_Promotion_Pass SHALL assign an exclusion-guard Compound_Constraint to the Less_Specific_Entity using the same `None` strategy, offset index, and `LiteralText` as the More_Specific_Entity's positional constraint, with `IsExclusion` set to true

### Requirement 4: Diagnostic Behavior for Internal-Segment Resolution

**User Story:** As a developer, I want the diagnostic behavior for internally-resolved pairs to be consistent with existing compound promotion diagnostics, so that resolved pairs suppress FDDB102 warnings and emit FDDB104 info diagnostics.

#### Acceptance Criteria

1. WHEN the Compound_Promotion_Pass resolves a same-prefix pair via internal-segment discrimination, THE Source_Generator SHALL NOT emit FDDB102 or DISC004 diagnostics for that entity pair
2. WHEN the Compound_Promotion_Pass resolves a same-prefix pair via internal-segment discrimination, THE Source_Generator SHALL emit exactly one FDDB104 diagnostic per entity in the resolved pair with severity Info, including the entity name, the primary discriminator property and pattern, the cross-key attribute name, the internal-segment constraint detail, and the other entity name involved in the resolution
3. WHEN the Compound_Promotion_Pass cannot resolve a same-prefix pair via internal-segment discrimination (neither entity has a distinguishing internal segment, or both entities have identical internal segments), THE Source_Generator SHALL emit FDDB102 or DISC004 diagnostics with the same diagnostic ID, severity, and message content as if the internal-segment analysis did not run
4. WHEN an entity is involved in multiple same-prefix overlaps and some are resolved by internal-segment discrimination while others are not, THE Source_Generator SHALL suppress FDDB102 and DISC004 diagnostics only for the resolved pairs and SHALL continue to emit FDDB102 or DISC004 for the unresolved pairs involving that entity

### Requirement 5: Preservation of Existing Behavior

**User Story:** As a developer, I want the internal-segment enhancement to not change any existing compound promotion behavior for pairs that are already resolvable or explicitly unresolvable, so that the upgrade is non-breaking.

#### Acceptance Criteria

1. WHEN two entities have a same-score discriminator overlap AND both entities' effective cross-key patterns (after prefix extraction) are non-null and non-identical, THE Compound_Promotion_Pass SHALL continue to resolve via dual positive `StartsWith` constraints without invoking internal-segment logic
2. WHEN one entity has a non-null effective cross-key pattern and the other has a null pattern (already disambiguable), THE Compound_Promotion_Pass SHALL continue to assign a positive Compound_Constraint to the entity with the non-null pattern and an exclusion-guard Compound_Constraint to the entity with the null pattern, via the existing asymmetric path
3. WHEN both entities have null effective cross-key patterns (both null or both empty-prefix Complex), THE Compound_Promotion_Pass SHALL continue to classify the pair as not disambiguable and SHALL NOT assign any Compound_Constraint to either entity
4. THE `PatternOverlapAnalyzer` SHALL NOT be modified by this enhancement
5. THE `DiscriminatorAnalyzer` SHALL NOT be modified by this enhancement
6. THE `CompoundConstraint` model SHALL NOT require new properties for this enhancement since the model already supports the `Contains` strategy via the existing `Strategy` and `LiteralText` fields, and positional checks can be represented by adding an offset field if needed
7. IF entities have non-overlapping discriminator patterns or different specificity scores, THEN THE Compound_Promotion_Pass SHALL NOT assign any Compound_Constraint to either entity and SHALL NOT alter any existing field on either entity's `DiscriminatorConfig`

### Requirement 6: Mutual Exclusivity of Generated MatchesEntity Methods

**User Story:** As a developer, I want the generated `MatchesEntity` methods for entities resolved by internal-segment discrimination to be mutually exclusive, so that any given DynamoDB item matches at most one entity type.

#### Acceptance Criteria

1. THE generated MatchesEntity_Method for two entities resolved by internal-segment discrimination SHALL NOT both return true for the same DynamoDB item when both the discriminator property and the cross-key property exist with non-null string values
2. WHEN the More_Specific_Entity has a positive `Contains` constraint on the internal segment and the Less_Specific_Entity has an exclusion guard negating that same `Contains` check, THE generated MatchesEntity_Methods SHALL partition items: items whose cross-key value contains the internal segment match only the More_Specific_Entity, and items whose cross-key value does not contain the internal segment match only the Less_Specific_Entity
3. WHEN both entities have positive `Contains` constraints with different internal segments, THE generated MatchesEntity_Methods SHALL partition items by internal-segment presence: an item whose cross-key value contains only entity A's internal segment matches only entity A, an item whose cross-key value contains only entity B's internal segment matches only entity B, an item whose cross-key value contains both entity A's and entity B's internal segments matches neither entity, and an item whose cross-key value contains neither internal segment matches neither entity
4. IF the cross-key property is missing from a DynamoDB item or its value is null, THEN THE generated MatchesEntity_Method SHALL return false for both entities in the internally-resolved pair

### Requirement 7: Multi-Overlap Interaction

**User Story:** As a developer with three or more entities sharing the same discriminator pattern, I want internal-segment discrimination to work correctly when one entity overlaps with multiple others, so that all resolvable pairs are resolved independently.

#### Acceptance Criteria

1. WHEN entity A has a Complex cross-key pattern with an internal segment and overlaps with both entity B (non-Complex, same prefix) and entity C (non-Complex, same prefix), THE Compound_Promotion_Pass SHALL resolve both the (A, B) pair and the (A, C) pair, assigning entity A a single positive `Contains` constraint using its internal segment, and assigning entity B and entity C each an exclusion-guard Compound_Constraint that negates entity A's internal-segment `Contains` check, accumulating entity C's exclusion in entity B's `AdditionalExclusions` list if entity B already has an exclusion guard from the (A, B) resolution
2. WHEN entity A overlaps with entity B via prefix-based disambiguation (different reduced prefixes) and with entity C via internal-segment disambiguation (same reduced prefix), THE Compound_Promotion_Pass SHALL assign entity A and entity B positive `StartsWith` constraints for the prefix-based pair, and assign entity C an exclusion-guard `Contains` constraint for the internal-segment pair, with entity A retaining its prefix-based positive `StartsWith` constraint unchanged
3. WHEN entity A already has a positive compound constraint from a prior pair resolution, THE Compound_Promotion_Pass SHALL preserve entity A's existing positive constraint and skip assignment of any new positive constraint from subsequent pair resolutions for that entity
4. WHEN entity B and entity C both have non-Complex cross-key patterns with the same reduced prefix and neither has a distinguishing internal segment, THE Compound_Promotion_Pass SHALL NOT resolve the (B, C) pair via internal-segment discrimination, and the (B, C) pair SHALL remain subject to existing FDDB102 or DISC004 diagnostic behavior
