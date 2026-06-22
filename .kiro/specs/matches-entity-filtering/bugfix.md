# Bugfix Requirements Document

## Introduction

The source-generated `MatchesEntity` method silently drops legitimate items from Query, Scan, and Get results. It uses attribute-presence checks on ALL non-nullable properties as a heuristic for entity type discrimination, which causes false negatives when any non-nullable property is missing from a DynamoDB item — even when the item legitimately belongs to that entity type. This affects empty collections (not persisted by DynamoDB), schema evolution (new properties added after items were written), and sparse writes (items written with partial attributes). The existing discriminator configuration (`DiscriminatorProperty`/`DiscriminatorPattern`/`DiscriminatorValue`) is never used by the generated `MatchesEntity` method.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN an entity has a discriminator configured (DiscriminatorProperty + DiscriminatorValue or DiscriminatorPattern) AND a DynamoDB item matches the discriminator but is missing a non-nullable data attribute (e.g., empty collection not persisted) THEN the system returns false from MatchesEntity and silently drops the item from results

1.2 WHEN an entity has a non-nullable collection property (List<T>, Dictionary<K,V>) initialized to `new()` AND the collection is empty when persisted (DynamoDB omits empty collections) THEN the system returns false from MatchesEntity because it checks for the attribute's presence

1.3 WHEN an entity class adds a new non-nullable property after existing items were written to the table THEN the system returns false from MatchesEntity for those existing items, causing them to silently vanish from all queries

1.4 WHEN an item is written with only key attributes and a subset of data attributes (sparse write pattern) THEN the system returns false from MatchesEntity and silently drops the item from results

1.5 WHEN a multi-entity table has entities WITHOUT discriminator configuration AND entities share similar schemas THEN the system may return true from MatchesEntity for wrong-type items (false positive), because attribute-presence alone cannot reliably distinguish entity types

### Expected Behavior (Correct)

2.1 WHEN an entity has a discriminator configured (DiscriminatorProperty + DiscriminatorValue or DiscriminatorPattern) THEN the system SHALL use only the discriminator check to determine if an item matches the entity type, ignoring data attribute presence

2.2 WHEN an entity has a discriminator with an exact value (DiscriminatorValue) THEN the system SHALL check that the discriminator property exists in the item and its string value equals the configured DiscriminatorValue

2.3 WHEN an entity has a discriminator with a pattern (DiscriminatorPattern using wildcard *) THEN the system SHALL check that the discriminator property exists in the item and its string value matches the configured pattern (e.g., StartsWith for "PREFIX#*" patterns)

2.4 WHEN an entity is the only entity on a single-entity table (no other entities share the table name) THEN the system SHALL use a minimal structural check (key attribute presence only) rather than checking all non-nullable properties

2.5 WHEN multiple entities share a table WITHOUT discriminator configuration THEN the system SHALL check only key attribute presence (partition key and sort key if applicable) rather than all non-nullable data attributes

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a DynamoDB item does not contain the required key attributes (partition key, sort key) for an entity THEN the system SHALL CONTINUE TO return false from MatchesEntity

3.2 WHEN a discriminator is configured and an item's discriminator value does NOT match the configured value/pattern THEN the system SHALL CONTINUE TO return false from MatchesEntity (correct filtering of wrong-type items)

3.3 WHEN MatchesEntity returns true for an item THEN the system SHALL CONTINUE TO pass that item to FromDynamoDb for hydration

3.4 WHEN the legacy EntityDiscriminator property is used (deprecated exact-match strategy) THEN the system SHALL CONTINUE TO support it for backward compatibility

3.5 WHEN call sites invoke MatchesEntity (EntityExecuteAsyncExtensions, CompoundEntityResult, PartiQLRequestBuilder) THEN the system SHALL CONTINUE TO use the same method signature without requiring call site changes
