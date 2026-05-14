# Requirements Document

## Introduction

This specification addresses the generation of Global Secondary Index (GSI) and Local Secondary Index (LSI) properties for multi-entity tables in Oproto.FluentDynamoDb. Currently, index properties are only generated from the default entity or first entity in a multi-entity table. This means indexes defined on non-default entities are silently ignored, which can lead to confusion and missing functionality.

The solution will consolidate indexes from all entities sharing a table, detect conflicting definitions, and report clear errors when conflicts occur.

## Glossary

- **Multi_Entity_Table**: A DynamoDB table that stores multiple entity types, identified by entities sharing the same table name or table type reference.
- **Index_Consolidation**: The process of collecting index definitions from all entities in a multi-entity table and merging them into a single set.
- **Conflicting_Index**: Two or more index definitions with the same DynamoDB index name but different configurations (different keys, projections, or types).
- **Source_Generator**: The Roslyn-based code generator that produces table classes and entity mappers.
- **Index_Property**: A generated C# property on the table class that provides access to query a specific GSI or LSI.

## Requirements

### Requirement 1

**User Story:** As a developer, I want indexes defined on any entity in a multi-entity table to be available on the generated table class, so that I can query all indexes regardless of which entity defines them.

#### Acceptance Criteria

1. WHEN a multi-entity table has entities with GSI definitions THEN the Source_Generator SHALL collect indexes from all entities sharing that table.
2. WHEN multiple entities define the same index name with identical configurations THEN the Source_Generator SHALL generate a single index property for that index.
3. WHEN an index is defined on a non-default entity THEN the Source_Generator SHALL still generate the index property on the table class.

### Requirement 2

**User Story:** As a developer, I want to receive clear error messages when I define conflicting indexes across entities, so that I can fix configuration issues at compile time.

#### Acceptance Criteria

1. WHEN two entities define indexes with the same DynamoDB index name but different partition keys THEN the Source_Generator SHALL report a diagnostic error.
2. WHEN two entities define indexes with the same DynamoDB index name but different sort keys THEN the Source_Generator SHALL report a diagnostic error.
3. WHEN two entities define indexes with the same DynamoDB index name but different index types (GSI vs LSI) THEN the Source_Generator SHALL report a diagnostic error.
4. WHEN a conflict is detected THEN the diagnostic message SHALL identify both entities and the conflicting property.

### Requirement 3

**User Story:** As a developer, I want indexes with the same name to have consistent C# property names, so that there is no ambiguity in the generated code.

#### Acceptance Criteria

1. WHEN two entities define the same index name with different custom C# property names THEN the Source_Generator SHALL report a diagnostic error.
2. WHEN two entities define the same index name with the same custom C# property name THEN the Source_Generator SHALL generate a single index property.
3. WHEN one entity specifies a custom name and other entities use the default THEN the Source_Generator SHALL use the custom name for the generated property.
4. WHEN multiple entities specify different custom names for the same index THEN the Source_Generator SHALL report a diagnostic error identifying both entities and their conflicting custom names.

### Requirement 4

**User Story:** As a developer, I want the index consolidation to preserve all index metadata, so that typed index classes and projections work correctly.

#### Acceptance Criteria

1. WHEN consolidating indexes THEN the Source_Generator SHALL preserve GSI discriminator configurations.
2. WHEN consolidating indexes THEN the Source_Generator SHALL preserve projection type associations.
3. WHEN generating typed index classes THEN the Source_Generator SHALL generate them for all consolidated indexes with projections.

### Requirement 5

**User Story:** As a developer, I want backward compatibility with existing single-entity tables, so that my current code continues to work without changes.

#### Acceptance Criteria

1. WHEN a table has only one entity THEN the Source_Generator SHALL generate index properties as it does today.
2. WHEN upgrading to the new version THEN existing single-entity table code SHALL compile and function without modification.
3. WHEN upgrading to the new version THEN existing multi-entity table code with non-conflicting indexes SHALL compile and function without modification.

### Requirement 6

**User Story:** As a developer, I want comprehensive test coverage for index consolidation, so that I can trust the feature works correctly.

#### Acceptance Criteria

1. WHEN implementing index consolidation THEN the implementation SHALL include unit tests for conflict detection.
2. WHEN implementing index consolidation THEN the implementation SHALL include unit tests for successful consolidation scenarios.
3. WHEN implementing index consolidation THEN the implementation SHALL include tests verifying backward compatibility with single-entity tables.

### Requirement 7

**User Story:** As a developer, I want documentation updated to reflect the index consolidation behavior, so that I understand how to define indexes in multi-entity tables.

#### Acceptance Criteria

1. WHEN the feature is complete THEN the CHANGELOG.md SHALL be updated with the new functionality.
2. WHEN the feature is complete THEN the docs/DOCUMENTATION_CHANGELOG.md SHALL be updated for external documentation synchronization.
3. WHEN the feature is complete THEN the fluentdynamodb.md steering document SHALL be updated with multi-entity index patterns.
4. WHEN the feature is complete THEN the docs folder SHALL contain updated guidance on multi-entity index definitions.
