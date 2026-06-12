# Bugfix Requirements Document

## Introduction

Two bugs in the table provisioning logic cause Global Secondary Indexes (GSIs) to be missing when tables are created programmatically. The first bug is in `IntegrationTestBase.CreateTableAsync<TEntity>()`, which manually constructs a `CreateTableRequest` from entity metadata properties but never includes indexes. The second bug is in the source-generated `CreateTableAsync` for multi-entity tables, which only uses the default entity's metadata — omitting GSIs declared on non-default entities. Together, these bugs result in incomplete table schemas during integration testing and in consuming applications that rely on the generated table creation method.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN `IntegrationTestBase.CreateTableAsync<TEntity>()` is called for an entity that declares one or more GSIs or LSIs THEN the system creates a table without any Global Secondary Indexes or Local Secondary Indexes because it only inspects `metadata.Properties` for key schema and never consults `metadata.Indexes`

1.2 WHEN the source-generated `CreateTableAsync` method is called on a multi-entity table class where non-default entities declare GSIs THEN the system creates a table that only includes GSIs from the default entity's metadata, omitting all indexes declared on non-default entities

1.3 WHEN an integration test queries a GSI on a table created via `IntegrationTestBase.CreateTableAsync<TEntity>()` THEN the system throws a `ResourceNotFoundException` or validation error because the GSI does not exist on the table

1.4 WHEN a consuming application queries a GSI declared on a non-default entity after using the generated multi-entity `CreateTableAsync` THEN the system throws a `ResourceNotFoundException` because the GSI was never provisioned

### Expected Behavior (Correct)

2.1 WHEN `IntegrationTestBase.CreateTableAsync<TEntity>()` is called for an entity that declares one or more GSIs or LSIs THEN the system SHALL create a table that includes all GSIs and LSIs defined in the entity's metadata, using `TableCreator.CreateAsync()` which already handles indexes, TTL, and billing mode

2.2 WHEN the source-generated `CreateTableAsync` method is called on a multi-entity table class where non-default entities declare GSIs THEN the system SHALL create a table that includes all GSIs aggregated from every entity sharing the table, consistent with how `TableGenerator` already aggregates indexes via `IndexAggregator`

2.3 WHEN an integration test queries a GSI on a table created via `IntegrationTestBase.CreateTableAsync<TEntity>()` THEN the system SHALL successfully execute the query against the provisioned GSI

2.4 WHEN a consuming application queries a GSI declared on a non-default entity after using the generated multi-entity `CreateTableAsync` THEN the system SHALL successfully execute the query against the provisioned GSI

### Unchanged Behavior (Regression Prevention)

3.1 WHEN `IntegrationTestBase.CreateTableAsync<TEntity>()` is called for an entity with no GSIs or LSIs THEN the system SHALL CONTINUE TO create a table with only the partition key and optional sort key, as before

3.2 WHEN the source-generated `CreateTableAsync` is called on a single-entity table THEN the system SHALL CONTINUE TO create a table using that entity's full metadata including any indexes it declares

3.3 WHEN `TableCreator.CreateAsync()` is called directly with entity metadata THEN the system SHALL CONTINUE TO create tables with all indexes, TTL, and billing mode as currently implemented

3.4 WHEN a multi-entity table has only one entity declaring GSIs (the default entity) THEN the generated `CreateTableAsync` SHALL CONTINUE TO produce the same result as before the fix

3.5 WHEN `IntegrationTestBase.CreateTableWithGsiAsync<TEntity>()` is called THEN the system SHALL CONTINUE TO work as before since it is a separate code path unaffected by this fix
