# Bugfix Requirements Document

## Introduction

Three related bugs in the source generator's discriminator analysis cause incorrect `MatchesEntity` code generation and spurious FDDB102 warnings when entities share a table with overlapping SK patterns and PK patterns that have prefix-subset relationships. The bugs affect `CompoundPromotionPass` and `PatternOverlapAnalyzer` in the source generator project, producing mutual exclusivity violations (Bug 1), unnecessary exclusion patterns and warnings (Bug 2), and misleading diagnostics (Bug 3). Bug 1 is the root cause of 66 test failures in a consuming project.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN two entities share the same SK discriminator score and `CompoundPromotionPass` assigns dual positive `StartsWith` compound constraints where one entity's PK prefix subsumes the other (e.g., `TENANT#` is a prefix of `TENANT#PLATFORM#ROLE#`) THEN the system generates `MatchesEntity` methods where the shorter-prefix entity incorrectly matches items belonging to the longer-prefix entity, violating mutual exclusivity

1.2 WHEN `CompoundPromotionPass.GetEffectiveCrossKeyPattern` processes a Complex PK pattern like `TENANT#*#ROLE#*` THEN the system reduces it to `TENANT#*` (prefix-only), losing the internal segment specificity that would distinguish it from other entities also starting with `TENANT#`

1.3 WHEN `PatternOverlapAnalyzer.ExactValueMatchesPattern` evaluates an ExactMatch value against a Complex pattern (e.g., `"SETTINGS"` vs `"CAP#*#*"`) THEN the system unconditionally returns `true` (assumes overlap) even when the exact value structurally cannot match the Complex pattern

1.4 WHEN `PatternOverlapAnalyzer.Analyze` processes a different-score pair where both discriminators are auto-derived and the overlap is successfully resolved with a non-tautological exclusion pattern THEN the system emits an FDDB102 warning diagnostic before creating the exclusion, misleading users into thinking the overlap is unresolved

### Expected Behavior (Correct)

#### Requirement 2.1: Prefix Subsumption Exclusion Guard

**User Story:** As a developer defining multiple entities on a shared DynamoDB table where PK prefixes have a subset relationship, I want the source generator to detect prefix subsumption and add exclusion guards so that `MatchesEntity` methods remain mutually exclusive.

**Acceptance Criteria:**

2.1.1 WHEN `CompoundPromotionPass` assigns dual positive `StartsWith` compound constraints for a same-score pair and one entity's `StartsWith` literal text is an ordinal string prefix of the other's (e.g., `"TENANT#"` is a prefix of `"TENANT#PLATFORM#ROLE#"`), THEN the system SHALL detect this prefix subsumption relationship

2.1.2 WHEN prefix subsumption is detected, THEN the system SHALL add an exclusion `CompoundConstraint` to the shorter-prefix entity that rejects items starting with the longer prefix, ensuring the shorter-prefix entity's `MatchesEntity` returns `false` for items belonging to the longer-prefix entity

2.1.3 WHEN prefix subsumption is detected, THEN the system SHALL preserve the positive `CompoundConstraint` on both entities (the longer-prefix entity keeps its positive constraint unchanged, and the shorter-prefix entity keeps its positive constraint in addition to receiving the exclusion guard)

2.1.4 WHEN prefix subsumption exclusion guards are applied, THEN for any DynamoDB item, at most one of the two entities' `MatchesEntity` methods SHALL return `true` (mutual exclusivity)

#### Requirement 2.2: Prefix Subsumption Verification After Dual Assignment

**User Story:** As a developer, I want the compound promotion pass to verify prefix relationships after assigning dual positive constraints so that subsumptive prefix pairs are always caught and corrected.

**Acceptance Criteria:**

2.2.1 WHEN `CompoundPromotionPass` assigns dual positive compound constraints for two entities with `StartsWith` strategy on the cross-key, THEN the system SHALL check whether entity A's literal text starts with entity B's literal text OR entity B's literal text starts with entity A's literal text (ordinal comparison)

2.2.2 IF a prefix subsumption exists after dual positive assignment, THEN the system SHALL apply an exclusion guard on the entity whose literal text is the shorter prefix, excluding items that match the longer prefix's `StartsWith` pattern

2.2.3 WHEN two entities have the same `StartsWith` literal text on the cross-key (identical, not subsumptive), THEN the system SHALL NOT treat this as prefix subsumption and SHALL continue to the internal-segment fallback path

#### Requirement 2.3: ExactValueMatchesPattern Complex Pattern Prefix Check

**User Story:** As a developer using multiple entities on a shared DynamoDB table where one entity has an ExactMatch sort key and another has a Complex sort key pattern, I want `PatternOverlapAnalyzer.ExactValueMatchesPattern` to detect that the exact value cannot structurally match the Complex pattern, so that the system does not produce spurious overlap warnings or unnecessary exclusion patterns.

**Acceptance Criteria:**

2.3.1 WHEN `PatternOverlapAnalyzer.ExactValueMatchesPattern` evaluates an ExactMatch value against a Complex pattern whose `Pattern` string contains at least one non-empty segment before the first `*` (leading prefix segment), THEN the system SHALL return `false` if the exact value does not start with that leading prefix segment using ordinal string comparison

2.3.2 WHEN `PatternOverlapAnalyzer.ExactValueMatchesPattern` evaluates an ExactMatch value against a Complex pattern and the exact value starts with the leading prefix segment, THEN the system SHALL return `true` (conservative: assume overlap for remaining wildcard structure)

2.3.3 IF the Complex pattern's `Pattern` string starts with `*` (the leading prefix segment is empty), THEN the system SHALL return `true` (conservative: no leading prefix available to rule out overlap)

2.3.4 WHEN `PatternOverlapAnalyzer.ExactValueMatchesPattern` evaluates an ExactMatch value against a StartsWith, EndsWith, or Contains pattern, THEN the system SHALL continue to use the existing structural matching logic for those strategies without any change

#### Requirement 2.4: Suppress FDDB102 for Resolved Different-Score Pairs

**User Story:** As a developer using multi-entity tables with overlapping auto-derived discriminator patterns, I want the source generator to not emit FDDB102 warnings for different-score pairs that are successfully resolved with non-tautological exclusion patterns, so that I only see warnings for genuinely unresolved overlaps.

**Acceptance Criteria:**

2.4.1 WHEN `PatternOverlapAnalyzer.Analyze` processes a different-score pair where both discriminators have `IsAutoDerived == true` and `CreateExclusionPattern` produces an `ExclusionPattern` for which `IsTautologicalExclusion` returns `false`, THEN the system SHALL NOT add an FDDB102 warning diagnostic for that pair

2.4.2 WHEN `PatternOverlapAnalyzer.Analyze` processes a different-score pair where both discriminators have `IsAutoDerived == true` and `CreateExclusionPattern` produces an `ExclusionPattern` for which `IsTautologicalExclusion` returns `true`, THEN the system SHALL continue to add the FDDB102 warning diagnostic for that pair

2.4.3 WHEN `PatternOverlapAnalyzer.Analyze` suppresses FDDB102 for a non-tautological different-score pair, THEN the system SHALL still add the `ExclusionPattern` to the less-specific entity's `DiscriminatorConfig.OverlappingPatterns` list and SHALL still emit the DISC005 informational diagnostic for that pair

2.4.4 WHEN `PatternOverlapAnalyzer.Analyze` processes a same-score pair where both discriminators have `IsAutoDerived == true`, THEN the system SHALL continue to emit the FDDB102 warning diagnostic regardless of any subsequent resolution by `CompoundPromotionPass`

#### Requirement 2.5: FDDB102 Preserved for Tautological Exclusions

**User Story:** As a library consumer, I want the source generator to warn me when two auto-derived discriminator patterns overlap and the overlap cannot be resolved by exclusion guards, so that I know to redesign my key formats for unambiguous entity identification.

**Acceptance Criteria:**

2.5.1 WHEN `PatternOverlapAnalyzer.Analyze` processes a different-score overlapping pair where both discriminators are auto-derived and `IsTautologicalExclusion` returns `true` for the computed exclusion pattern, THEN the system SHALL emit exactly one FDDB102 diagnostic for that pair with severity Warning

2.5.2 WHEN `PatternOverlapAnalyzer.Analyze` detects a tautological exclusion in a different-score auto-derived pair, THEN the system SHALL also emit the DISC006 diagnostic for that pair, resulting in both FDDB102 and DISC006 being reported

### Unchanged Behavior (Regression Prevention)

3.1 WHEN two entities share the same SK discriminator score and have different non-subsumptive PK prefixes (e.g., `PLATFORM#` and `TENANT#`) THEN the system SHALL CONTINUE TO assign dual positive `StartsWith` compound constraints without exclusion guards

3.2 WHEN two entities share the same SK discriminator score and one has a non-null PK pattern while the other has null THEN the system SHALL CONTINUE TO assign a positive constraint to the non-null entity and an exclusion guard to the null entity

3.3 WHEN two entities have different specificity scores and non-overlapping patterns THEN the system SHALL CONTINUE TO not emit any overlap diagnostics

3.4 WHEN two entities have same-score overlaps that cannot be resolved by cross-key disambiguation (both null or identical PK patterns) THEN the system SHALL CONTINUE TO emit FDDB102 or DISC004 diagnostics

3.5 WHEN `PatternOverlapAnalyzer.ExactValueMatchesPattern` evaluates an ExactMatch value against a StartsWith, EndsWith, or Contains pattern THEN the system SHALL CONTINUE TO use the existing structural matching logic for those strategies

3.6 WHEN `PatternOverlapAnalyzer.Analyze` processes same-score pairs (both auto-derived) THEN the system SHALL CONTINUE TO emit FDDB102 diagnostics for unresolved same-score overlaps

3.7 WHEN `CompoundPromotionPass` resolves a same-score pair via compound promotion THEN the system SHALL CONTINUE TO emit FDDB104 info diagnostics and suppress the corresponding FDDB102/DISC004 diagnostics for that resolved pair

3.8 WHEN `CompoundPromotionPass` processes entities with Complex PK patterns that share the same prefix but have different internal segments THEN the system SHALL CONTINUE TO resolve them via internal-segment positional constraints
