# Requirements Document

## Introduction

This feature adds compound key discrimination to the Oproto.FluentDynamoDb source generator. When two entities share the same DynamoDB table and have identical (same-score) discriminator patterns on one key property (e.g., sort key), the source generator automatically attempts to disambiguate by inspecting the other key property's derived pattern (e.g., partition key). If the cross-key patterns differ, the generator promotes to a compound discriminator check (primary key match AND sort key match) and suppresses the existing FDDB102/DISC004 diagnostics. If the cross-key patterns cannot disambiguate, existing diagnostics are emitted unchanged.

## Glossary

- **Source_Generator**: The Roslyn-based C# source generator (`DynamoDbSourceGenerator`) that analyzes entity attributes at compile time and emits mapper code
- **Compound_Promotion_Pass**: A new analysis pass that runs after `PatternOverlapAnalyzer.Analyze` to resolve same-score overlaps using cross-key pattern disambiguation
- **Cross_Key_Pattern**: The `DerivedDiscriminatorPattern` on the key property opposite to the current discriminator property (PK pattern when discriminator is SK, SK pattern when discriminator is PK)
- **Compound_Constraint**: A secondary `DiscriminatorConfig` attached to an entity's primary discriminator, representing an additional AND condition on the cross-key property that must pass for `MatchesEntity` to return true
- **Exclusion_Guard**: A negation check in `MatchesEntity` that returns false when the cross-key value matches another entity's compound constraint, ensuring mutual exclusivity without a positive compound assertion
- **Same_Score_Overlap**: A condition detected by `PatternOverlapAnalyzer` where two entities on the same table have overlapping discriminator patterns with identical specificity scores on the same discriminator property
- **MatchesEntity_Method**: The generated static method `MatchesEntity(Dictionary<string, AttributeValue> item)` on each entity mapper that determines whether a DynamoDB item belongs to that entity type
- **Table_Entity_Group**: The set of all entities sharing the same DynamoDB table name, as determined by `GroupEntitiesByTableName` at compile time
- **DerivedDiscriminatorPattern**: A pattern derived from a key property's `NormalizedKeyFormat` by replacing `{N}` placeholders with `*`. Null when the key has no prefix/format (bare `{0}`)

## Requirements

### Requirement 1: Compound Promotion Pass Detection

**User Story:** As a developer using single-table DynamoDB designs with entities that share the same sort key prefix but differ by partition key structure, I want the source generator to automatically detect that the partition key can disambiguate the entities, so that I do not receive false FDDB102/DISC004 diagnostics and my entities are correctly distinguished at runtime.

#### Acceptance Criteria

1. WHEN `PatternOverlapAnalyzer.Analyze` detects a Same_Score_Overlap between two entities on the same discriminator property, THE Compound_Promotion_Pass SHALL check whether both entities have a `DerivedDiscriminatorPattern` on their Cross_Key_Pattern property
2. WHEN the Cross_Key_Pattern values for two entities in a Same_Score_Overlap differ (including one being null and the other non-null), THE Compound_Promotion_Pass SHALL classify the pair as disambiguable via compound discrimination
3. WHEN the Cross_Key_Pattern values for two entities in a Same_Score_Overlap are both null, THE Compound_Promotion_Pass SHALL NOT classify the pair as disambiguable and existing FDDB102/DISC004 diagnostics SHALL remain
4. WHEN the Cross_Key_Pattern values for two entities in a Same_Score_Overlap are identical non-null values, THE Compound_Promotion_Pass SHALL NOT classify the pair as disambiguable and existing FDDB102/DISC004 diagnostics SHALL remain
5. WHEN the primary discriminator property is the sort key attribute, THE Compound_Promotion_Pass SHALL inspect the partition key attribute's DerivedDiscriminatorPattern as the Cross_Key_Pattern; WHEN the primary discriminator property is the partition key attribute, THE Compound_Promotion_Pass SHALL inspect the sort key attribute's DerivedDiscriminatorPattern as the Cross_Key_Pattern
6. WHEN three or more entities in a Table_Entity_Group share the same Same_Score_Overlap on the same discriminator property, THE Compound_Promotion_Pass SHALL evaluate disambiguation pairwise across all unique pairs, resolving each pair independently where Cross_Key_Patterns differ and leaving unresolved pairs to emit FDDB102/DISC004

### Requirement 2: Compound Constraint Assignment

**User Story:** As a developer, I want entities that are disambiguable via cross-key patterns to receive compound constraint configurations, so that the generated `MatchesEntity` methods produce mutually exclusive results for items on the same table.

#### Acceptance Criteria

1. WHEN both entities in a disambiguable Same_Score_Overlap have non-null Cross_Key_Pattern values that differ, THE Compound_Promotion_Pass SHALL assign a Compound_Constraint to each entity's `DiscriminatorConfig` representing a positive match on that entity's own Cross_Key_Pattern
2. WHEN one entity in a disambiguable Same_Score_Overlap has a non-null Cross_Key_Pattern and the other has a null Cross_Key_Pattern, THE Compound_Promotion_Pass SHALL assign a Compound_Constraint to the entity with the non-null pattern and an Exclusion_Guard to the entity with the null pattern
3. THE Compound_Constraint SHALL reference the DynamoDB attribute name of the cross-key property, the pattern string, and the matching strategy derived from the pattern (StartsWith, ExactMatch, EndsWith, or Contains)
4. THE Exclusion_Guard SHALL reference the other entity's cross-key pattern and negate the match (return false when the cross-key value matches the other entity's compound constraint pattern)
5. WHEN a cross-key pattern contains no wildcard (constant key value), THE Compound_Constraint SHALL use the ExactMatch strategy with the literal constant value

### Requirement 3: Diagnostic Suppression

**User Story:** As a developer, I want the source generator to suppress FDDB102 and DISC004 diagnostics when compound promotion successfully resolves a same-score overlap, so that I only see diagnostics for genuinely ambiguous situations.

#### Acceptance Criteria

1. WHEN the Compound_Promotion_Pass resolves a Same_Score_Overlap via cross-key disambiguation, THE Source_Generator SHALL NOT emit FDDB102 or DISC004 diagnostics for that entity pair
2. WHEN the Compound_Promotion_Pass cannot resolve a Same_Score_Overlap (both Cross_Key_Patterns are null or identical), THE Source_Generator SHALL emit FDDB102 or DISC004 diagnostics with the same severity level and message content as in the pre-existing behavior
3. WHEN the Compound_Promotion_Pass resolves a Same_Score_Overlap, THE Source_Generator SHALL emit exactly one FDDB104 diagnostic per resolved pair with severity Info, including the entity name, the primary discriminator pattern, the compound constraint pattern, and the other entity name involved in the resolution
4. WHEN an entity is involved in multiple Same_Score_Overlaps and some are resolved by compound promotion while others are not, THE Source_Generator SHALL suppress diagnostics only for the resolved pairs and SHALL continue to emit FDDB102/DISC004 for the unresolved pairs involving that entity

### Requirement 4: Code Generation for Compound Constraints

**User Story:** As a developer, I want the generated `MatchesEntity` method to include the compound constraint check after the primary discriminator check, so that items are correctly assigned to the right entity type at runtime.

#### Acceptance Criteria

1. WHEN an entity's `DiscriminatorConfig` has a non-null Compound_Constraint, THE Source_Generator SHALL generate MatchesEntity_Method code that first verifies the primary discriminator match and then verifies the Compound_Constraint match, returning true only if both checks pass
2. WHEN an entity's `DiscriminatorConfig` has an Exclusion_Guard, THE Source_Generator SHALL generate MatchesEntity_Method code that first verifies the primary discriminator match and then returns false if the cross-key value matches the exclusion pattern, returning true only if the exclusion check does not match
3. THE generated compound constraint check SHALL retrieve the cross-key attribute from the DynamoDB item dictionary, verify the attribute exists and has a non-null string value, and apply the strategy-specific string operation: `StartsWith` for prefix patterns, exact string equality for constant patterns, `EndsWith` for suffix patterns, and `Contains` for middle-segment patterns
4. WHEN the cross-key attribute is missing from the item or has a null string value, THE generated MatchesEntity_Method SHALL return false for entities with a Compound_Constraint (the compound constraint is mandatory for a positive match)
5. WHEN the cross-key attribute is missing from the item or has a null string value, THE generated MatchesEntity_Method SHALL return true for entities with an Exclusion_Guard (the exclusion cannot fire if the attribute is absent, so the primary match stands)

### Requirement 5: Pipeline Integration

**User Story:** As a developer, I want the compound promotion pass to integrate into the existing source generator pipeline without altering existing non-overlapping entity behavior, so that the upgrade is transparent for entities that do not have same-score overlaps.

#### Acceptance Criteria

1. THE Compound_Promotion_Pass SHALL execute after `PatternOverlapAnalyzer.Analyze` completes and before the per-entity code generation loop begins
2. WHEN entities in the same Table_Entity_Group have non-overlapping discriminator patterns, THE Compound_Promotion_Pass SHALL not modify any entity's `DiscriminatorConfig`
3. WHEN entities in the same Table_Entity_Group have overlapping patterns with different specificity scores (already resolved by exclusion), THE Compound_Promotion_Pass SHALL not modify any entity's `DiscriminatorConfig`
4. WHEN a Table_Entity_Group contains only a single entity, THE Compound_Promotion_Pass SHALL not execute for that group
5. THE Compound_Promotion_Pass SHALL consume the same-score overlap information produced by `PatternOverlapAnalyzer.Analyze` rather than re-computing overlap relationships
6. THE Compound_Promotion_Pass SHALL NOT mutate the overlap data structures produced by `PatternOverlapAnalyzer.Analyze`; it SHALL only read overlap information and write to entity `DiscriminatorConfig` objects
7. THE Compound_Promotion_Pass SHALL iterate over all unique pairwise combinations within each Same_Score_Overlap group, where a group may contain two or more entities sharing the same pattern on the same discriminator property

### Requirement 6: Mutual Exclusivity of Generated MatchesEntity Methods

**User Story:** As a developer, I want the generated `MatchesEntity` methods for entities resolved by compound promotion to be mutually exclusive, so that any given DynamoDB item matches at most one entity type on the same table.

#### Acceptance Criteria

1. FOR ALL DynamoDB items where both the discriminator property and the cross-key property exist with non-null string values, THE generated MatchesEntity_Method for two entities resolved by compound promotion SHALL NOT both return true for the same item
2. WHEN both entities have Compound_Constraints (both have non-null differing Cross_Key_Patterns), THE generated MatchesEntity_Methods SHALL partition items by cross-key value: an item matching entity A's cross-key pattern matches only entity A, and an item matching entity B's cross-key pattern matches only entity B, and an item matching neither entity's cross-key pattern matches neither entity
3. WHEN one entity has a Compound_Constraint and the other has an Exclusion_Guard, THE generated MatchesEntity_Methods SHALL partition items by cross-key value: an item matching the compound entity's cross-key pattern matches only the compound entity, and all other items (that pass the primary discriminator and have the cross-key property present with a non-null string value) match only the excluded entity
4. FOR ALL DynamoDB items where the discriminator property matches the primary discriminator pattern AND the cross-key property exists with a non-null string value AND at least one entity's cross-key pattern matches the cross-key value, at least one of the two compound-promoted entities' MatchesEntity_Methods SHALL return true (no items that satisfy a cross-key pattern fall through unmatched)
5. WHEN both entities have Compound_Constraints and a DynamoDB item's cross-key value matches neither entity's cross-key pattern, THE generated MatchesEntity_Methods for both entities SHALL return false for that item

### Requirement 7: Support for All Discriminator Strategy Types

**User Story:** As a developer, I want compound promotion to work with all existing discriminator pattern strategies including StartsWith (prefix patterns), ExactMatch (constant keys), EndsWith, and Contains patterns, so that the feature applies broadly to different key design patterns.

#### Acceptance Criteria

1. WHEN a Cross_Key_Pattern uses a prefix format (e.g., "PLATFORM#*"), THE Compound_Constraint SHALL use the StartsWith strategy with the literal prefix text derived by removing the trailing wildcard (e.g., "PLATFORM#")
2. WHEN a Cross_Key_Pattern is a constant value with no wildcard (e.g., "PROFILE"), THE Compound_Constraint SHALL use the ExactMatch strategy with the full constant value
3. WHEN a Cross_Key_Pattern uses an EndsWith format (e.g., "*#SUFFIX"), THE Compound_Constraint SHALL use the EndsWith strategy with the literal suffix text derived by removing the leading wildcard (e.g., "#SUFFIX")
4. WHEN a Cross_Key_Pattern uses a Contains format (e.g., "*#MIDDLE#*"), THE Compound_Constraint SHALL use the Contains strategy with the literal middle text derived by removing the leading and trailing wildcards (e.g., "#MIDDLE#")
5. THE Compound_Promotion_Pass SHALL derive the strategy from the Cross_Key_Pattern using the same `DiscriminatorAnalyzer.DeterminePatternStrategy` method used for primary discriminator patterns
6. WHEN `DiscriminatorAnalyzer.DeterminePatternStrategy` returns a Complex strategy for a Cross_Key_Pattern (indicating the pattern cannot be reduced to a single string operation), THE Compound_Promotion_Pass SHALL NOT use that pattern for compound promotion and SHALL treat the entity as having a null Cross_Key_Pattern for disambiguation purposes

