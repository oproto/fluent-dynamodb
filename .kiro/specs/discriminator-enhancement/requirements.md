# Requirements Document

## Introduction

This feature enhances the discriminator system in the Oproto.FluentDynamoDb source generator with most-specific pattern matching for overlapping discriminator patterns on multi-entity tables. When multiple entities share a table and have overlapping sort key patterns (e.g., parent/child composite entities), the source generator automatically determines which entity's pattern is the best match and generates exclusion logic so that items are matched to the most-specific entity. This eliminates the need for a dedicated `entity_type` attribute in hierarchical sort key designs.

## Glossary

- **Source_Generator**: The Roslyn-based C# source generator (`DynamoDbSourceGenerator`) that analyzes entity attributes at compile time and emits mapper code
- **Discriminator**: A configured property and matching rule used to identify which entity type a DynamoDB item belongs to in a multi-entity table
- **DiscriminatorPattern**: A wildcard-based pattern using `*` characters to express StartsWith, EndsWith, Contains, or Complex matching strategies
- **Pattern_Specificity**: A compile-time ordering of discriminator patterns from most-specific to least-specific based on the number of literal character segments in the pattern
- **MatchesEntity_Method**: The generated static method `MatchesEntity(Dictionary<string, AttributeValue> item)` on each entity mapper that determines whether a DynamoDB item belongs to that entity type
- **Table_Entity_Group**: The set of all entities sharing the same DynamoDB table name, as determined by `GroupEntitiesByTableName` at compile time
- **Overlapping_Patterns**: Two or more discriminator patterns on entities in the same Table_Entity_Group where a single discriminator value could match more than one pattern

## Requirements

### Requirement 1: Most-Specific Pattern Matching in MatchesEntity

**User Story:** As a developer using hierarchical sort key designs, I want the source generator to automatically disambiguate overlapping discriminator patterns using most-specific match logic, so that I do not need a separate `entity_type` attribute to distinguish parent and child entities.

#### Acceptance Criteria

1. WHEN multiple entities in the same Table_Entity_Group have Overlapping_Patterns on the same DiscriminatorProperty, THE Source_Generator SHALL generate MatchesEntity_Method code that returns true only for the entity whose pattern is the most-specific match for a given discriminator value
2. WHEN an entity has a DiscriminatorPattern of "INVOICE#*" and another entity in the same Table_Entity_Group has a DiscriminatorPattern of "INVOICE#*#LINE#*" on the same DiscriminatorProperty, THE Source_Generator SHALL treat "INVOICE#*#LINE#*" as more specific than "INVOICE#*"
3. THE Source_Generator SHALL determine Pattern_Specificity at compile time by splitting the pattern on wildcard characters (`*`) and counting the number of resulting non-empty literal segments, where a higher count indicates higher specificity (e.g., "INVOICE#*" has 1 literal segment "INVOICE#", while "INVOICE#*#LINE#*" has 2 literal segments "INVOICE#" and "#LINE#")
4. WHEN an entity has an overlapping pattern that is less specific than another entity's pattern in the same Table_Entity_Group, THE Source_Generator SHALL generate MatchesEntity_Method code for the less-specific entity that first verifies the discriminator value matches the entity's own pattern, and then returns false if the value also matches any more-specific overlapping pattern in the same group
5. WHEN entities in the same Table_Entity_Group have non-overlapping discriminator patterns, THE Source_Generator SHALL generate MatchesEntity_Method code identical to the current behavior without any exclusion logic
6. WHEN entities in the same Table_Entity_Group use different DiscriminatorProperty values, THE Source_Generator SHALL treat those patterns as non-overlapping regardless of pattern content
7. WHEN three or more entities in the same Table_Entity_Group have overlapping patterns on the same DiscriminatorProperty with distinct specificity scores, THE Source_Generator SHALL generate exclusion logic for each entity that excludes all patterns with a higher specificity score than its own

### Requirement 2: Compile-Time Pattern Specificity Analysis

**User Story:** As a developer, I want the source generator to analyze all entity discriminator patterns at compile time and determine their relative specificity, so that most-specific matching is resolved without any runtime overhead.

#### Acceptance Criteria

1. THE Source_Generator SHALL perform pattern overlap detection during the source generation phase by comparing all DiscriminatorPattern and DiscriminatorValue entries within each Table_Entity_Group that share the same DiscriminatorProperty, where two patterns overlap if a single string value could match both patterns
2. THE Source_Generator SHALL compute a specificity score for each DiscriminatorPattern by counting the number of literal (non-wildcard) segments separated by wildcard characters, where a pattern with no wildcards scores equal to 1 segment and a pattern like "A#*#B#*" scores as 3 literal segments
3. WHEN two patterns in the same Table_Entity_Group have the same specificity score and overlap on the same DiscriminatorProperty, THE Source_Generator SHALL emit a compile-time diagnostic error that includes both entity names, the conflicting patterns, and the shared DiscriminatorProperty name
4. WHEN an ExactMatch discriminator (DiscriminatorValue) exists in the same Table_Entity_Group as a pattern-based discriminator on the same property, THE Source_Generator SHALL assign the ExactMatch a specificity score numerically higher than any wildcard pattern's score, ensuring ExactMatch always takes precedence
5. WHEN overlapping patterns are detected and resolved by specificity ordering, THE Source_Generator SHALL emit an informational diagnostic that includes the less-specific entity name, the more-specific entity name, and the pattern being excluded from the less-specific entity's MatchesEntity_Method

### Requirement 3: Generated Exclusion Logic

**User Story:** As a developer, I want the generated MatchesEntity code for a less-specific entity to exclude items that match a more-specific entity's pattern, so that each item is claimed by exactly one entity type.

#### Acceptance Criteria

1. WHEN the Source_Generator generates exclusion logic for a less-specific entity, THE generated MatchesEntity_Method SHALL emit additional string operation checks (StartsWith, EndsWith, Contains) that correspond to the more-specific entity's pattern and return false if those checks pass
2. THE exclusion checks SHALL use the same string operation type that the more-specific pattern would use (e.g., if the more-specific pattern is "INVOICE#*#LINE#*" with strategy Contains for "#LINE#", the exclusion check uses Contains("#LINE#"))
3. THE generated exclusion logic SHALL be placed after the less-specific entity's own positive match check and before the `return true` statement
4. WHEN one or more more-specific patterns need to be excluded, THE Source_Generator SHALL generate an exclusion check for each one, where any match causes an immediate return false (early return logic applies for both single and multiple exclusion patterns)

### Requirement 4: Backward Compatibility

**User Story:** As a developer with existing entities using non-overlapping discriminator patterns, I want the enhanced discriminator system to produce identical generated code for my existing entities, so that upgrading does not change runtime behavior.

#### Acceptance Criteria

1. WHEN entities in the same Table_Entity_Group have non-overlapping DiscriminatorPattern values, THE Source_Generator SHALL generate MatchesEntity_Method code identical to the code generated by the previous version
2. WHEN an entity uses DiscriminatorValue (ExactMatch) and no other entity in the Table_Entity_Group has an overlapping pattern, THE Source_Generator SHALL generate MatchesEntity_Method code identical to the current behavior
3. WHEN a Table_Entity_Group contains only a single entity, THE Source_Generator SHALL generate MatchesEntity_Method code identical to the current behavior regardless of pattern type
4. WHEN entities in the same Table_Entity_Group have non-overlapping patterns (e.g., "USER#*" and "ORDER#*"), THE Source_Generator SHALL NOT emit any informational diagnostics about pattern overlap
