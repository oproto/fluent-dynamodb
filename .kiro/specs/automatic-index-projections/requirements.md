# Requirements Document

## Introduction

This feature enhances the GSI/LSI generation in Oproto.FluentDynamoDb to automatically create projection types for single-entity tables and support Keys Only projections without requiring custom projection classes. Currently, indexes without explicit `[UseProjection]` attributes generate simple `DynamoDbIndex` properties that require generic type parameters for queries. This enhancement will:

1. Automatically use the main entity type as the default projection for indexes in single-entity tables
2. Add support for `ProjectionType` property on `[GlobalSecondaryIndex]` and `[LocalSecondaryIndex]` attributes
3. Auto-generate read-only Keys Only projection records when `ProjectionType = KeysOnly` is specified

## Glossary

- **Source_Generator**: The Roslyn-based code generator that produces entity mapping, table classes, and index accessors
- **Single_Entity_Table**: A DynamoDB table design where only one entity type is stored (one `[DynamoDbTable]` with `IsDefault = true` and no other entities sharing the table)
- **Multi_Entity_Table**: A DynamoDB table design where multiple entity types share the same table
- **GSI**: Global Secondary Index - a secondary index with a different partition key than the base table
- **LSI**: Local Secondary Index - a secondary index that shares the partition key with the base table
- **Projection_Type**: The DynamoDB projection configuration (ALL, KEYS_ONLY, or INCLUDE)
- **Keys_Only_Projection**: A DynamoDB index projection that only includes the partition key and sort key attributes
- **Entity_Accessor**: The generated property on a table class that provides typed access to entity operations (e.g., `table.Users`)
- **Index_Accessor**: The generated property on a table class that provides access to index query operations (e.g., `table.StatusIndex`)

## Requirements

### Requirement 1: Automatic Entity Projection for Single-Entity Tables

**User Story:** As a developer, I want indexes in single-entity tables to automatically use the main entity type for queries, so that I don't need to specify the type parameter for every index query.

#### Acceptance Criteria

1. WHEN a table has exactly one entity (single-entity design) AND an index has no explicit `[UseProjection]` attribute, THE Source_Generator SHALL generate a `DynamoDbIndex<TEntity>` property using the entity type as the default projection
2. WHEN a single-entity table index is generated with the entity as default projection, THE Index_Accessor SHALL provide non-generic `Query()` methods that return `QueryRequestBuilder<TEntity>`
3. WHEN a table has multiple entities (multi-entity design) AND an index has no explicit `[UseProjection]` attribute, THE Source_Generator SHALL generate a simple `DynamoDbIndex` property (existing behavior unchanged)
4. WHEN an index has an explicit `[UseProjection]` attribute, THE Source_Generator SHALL use the specified projection type regardless of single-entity or multi-entity design

### Requirement 2: ProjectionType Property on Index Attributes

**User Story:** As a developer, I want to specify the DynamoDB projection type (ALL, KEYS_ONLY, INCLUDE) on my index attributes, so that the generated code and metadata accurately reflect my index configuration.

#### Acceptance Criteria

1. THE `[GlobalSecondaryIndex]` attribute SHALL have a `ProjectionType` property of type `Oproto.FluentDynamoDb.Metadata.ProjectionType`
2. THE `[LocalSecondaryIndex]` attribute SHALL have a `ProjectionType` property of type `Oproto.FluentDynamoDb.Metadata.ProjectionType`
3. WHEN `ProjectionType` is not specified, THE Source_Generator SHALL default to `ProjectionType.All`
4. WHEN `ProjectionType` is specified, THE Source_Generator SHALL include the value in the generated `IndexMetadata`
5. WHEN `ProjectionType = KeysOnly` is specified, THE Source_Generator SHALL generate a Keys Only projection record (see Requirement 3)

### Requirement 3: Auto-Generated Keys Only Projection Records

**User Story:** As a developer, I want the source generator to automatically create a read-only projection type when I specify `ProjectionType = KeysOnly`, so that I don't need to manually define a projection class for keys-only indexes.

#### Acceptance Criteria

1. WHEN an index has `ProjectionType = KeysOnly`, THE Source_Generator SHALL generate a read-only record type named `{IndexPropertyName}KeysProjection`
2. FOR GSI indexes, THE generated Keys Only projection record SHALL include the GSI partition key, GSI sort key (if any), AND the base table partition key and sort key
3. FOR LSI indexes, THE generated Keys Only projection record SHALL include the base table partition key, the LSI sort key, and the base table sort key (if different from LSI sort key)
4. THE generated Keys Only projection record SHALL implement `IReadOnlyEntity<TSelf>` interface
5. THE generated Keys Only projection record SHALL be generated as a nested type within the table class
6. WHEN a Keys Only projection is generated, THE Index_Accessor SHALL use `DynamoDbIndex<{IndexPropertyName}KeysProjection>` as the index type
7. THE generated Keys Only projection record SHALL include a `FromDynamoDb` method for deserialization
8. THE generated Keys Only projection record SHALL NOT include `ToDynamoDb` method (read-only)
9. THE `GetPartitionKey()` and `GetSortKey()` methods on the generated record SHALL return the base table keys (for entity lookup)

### Requirement 4: Index Metadata Enhancement

**User Story:** As a developer, I want the generated index metadata to accurately reflect the projection type configuration, so that schema validation and table creation work correctly.

#### Acceptance Criteria

1. WHEN an index is analyzed, THE Source_Generator SHALL populate `IndexMetadata.ProjectionType` based on the attribute's `ProjectionType` property
2. WHEN `ProjectionType = KeysOnly` is specified, THE Source_Generator SHALL set `IndexMetadata.HasProjectionModel = true`
3. WHEN a Keys Only projection is auto-generated, THE Source_Generator SHALL populate `IndexMetadata.ProjectedProperties` with all key attribute names (GSI keys + table keys for GSI, table PK + LSI SK + table SK for LSI)
4. WHEN `TableCreator.CreateAsync()` is called, THE table creation SHALL use `IndexMetadata.ProjectionType` to configure the DynamoDB index projection
5. WHEN `ValidateSchemaAsync()` is called, THE schema validation SHALL compare the actual DynamoDB index projection type against `IndexMetadata.ProjectionType`

### Requirement 5: Documentation and Steering Updates

**User Story:** As a developer, I want clear documentation on how to use the new projection features, so that I can effectively configure my indexes.

#### Acceptance Criteria

1. THE `fluentdynamodb.md` steering file SHALL be updated with examples of automatic entity projections for single-entity tables
2. THE `fluentdynamodb.md` steering file SHALL be updated with examples of `ProjectionType` usage on index attributes
3. THE `fluentdynamodb.md` steering file SHALL be updated with examples of Keys Only projection generation
4. THE documentation SHALL explain when to use each projection type (ALL, KEYS_ONLY, INCLUDE)

### Requirement 6: Backward Compatibility

**User Story:** As a developer with existing code, I want the new features to be backward compatible, so that my existing index configurations continue to work without changes.

#### Acceptance Criteria

1. WHEN an existing index has no `ProjectionType` specified, THE Source_Generator SHALL maintain existing behavior (default to ALL projection)
2. WHEN an existing multi-entity table has indexes without `[UseProjection]`, THE Source_Generator SHALL continue generating simple `DynamoDbIndex` properties
3. WHEN an existing index has `[UseProjection]` attribute, THE Source_Generator SHALL continue using the specified projection type
