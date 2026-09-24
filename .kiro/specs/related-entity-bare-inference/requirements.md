# Requirements Document

## Introduction

This feature enables bare `[RelatedEntity]` attribute usage on composite entity properties, where the source generator infers the sort key matching pattern from the child entity's `DerivedDiscriminatorPattern` metadata instead of requiring an explicit pattern string. Currently, `[RelatedEntity("pattern")]` requires the developer to manually specify the sort key pattern — a string that must be kept in sync with the child entity's key structure. The bare form eliminates this redundancy by making the child entity's key definition the single source of truth.

## Glossary

- **Source_Generator**: The Roslyn incremental source generator (`DynamoDbSourceGenerator`) that analyzes entity declarations and emits mapping code at compile time
- **Entity_Analyzer**: The `EntityAnalyzer` class that extracts `EntityModel` metadata from a single entity type declaration, including properties, key formats, and relationships
- **Relationship_Model**: The `RelationshipModel` class representing a `[RelatedEntity]` relationship, including its `SortKeyPattern` used for sort key matching during composite entity assembly
- **Derived_Discriminator_Pattern**: The `PropertyModel.DerivedDiscriminatorPattern` property computed by replacing `{N}` placeholders in a key's `NormalizedKeyFormat` with `*` wildcards, producing a glob-like pattern (e.g., `ORDER#*#LINE#*`)
- **Entity_Model**: The `EntityModel` class representing the fully analyzed metadata of a DynamoDB entity, including its properties, key structure, indexes, and relationships
- **Bare_RelatedEntity**: A `[RelatedEntity]` attribute applied without a constructor argument (no explicit sort key pattern), signaling the generator to infer the pattern from the child entity
- **Explicit_RelatedEntity**: A `[RelatedEntity("pattern")]` attribute applied with an explicit sort key pattern string
- **Table_Group**: The set of all `EntityModel` instances sharing the same DynamoDB table name, used for cross-entity analysis in the `Execute` method
- **Mapper_Generator**: The `MapperGenerator` class that emits sort key pattern matching code for composite entity assembly using `RelationshipModel.SortKeyPattern`
- **Pattern_Resolution_Pass**: A new post-analysis phase in `DynamoDbSourceGenerator.Execute()` that resolves Bare_RelatedEntity patterns by looking up child entity metadata within a Table_Group

## Requirements

### Requirement 1: Parameterless RelatedEntity Attribute Constructor

**User Story:** As a developer defining composite entities, I want to use `[RelatedEntity]` without specifying a pattern string, so that the sort key matching pattern is automatically inferred from the child entity's key structure.

#### Acceptance Criteria

1. THE `RelatedEntityAttribute` class SHALL provide a parameterless constructor in addition to the existing `RelatedEntityAttribute(string sortKeyPattern)` constructor
2. WHEN the parameterless constructor is used, THE `RelatedEntityAttribute.SortKeyPattern` property SHALL return `null`
3. WHEN the existing `RelatedEntityAttribute(string sortKeyPattern)` constructor is called, THE `RelatedEntityAttribute` SHALL set `SortKeyPattern` to the provided value, including empty string
4. THE `SortKeyPattern` property type SHALL be changed from `string` to `string?` (nullable) to accommodate the parameterless constructor
5. IF the existing `RelatedEntityAttribute(string sortKeyPattern)` constructor is called with a `null` argument, THEN THE `RelatedEntityAttribute` SHALL throw an `ArgumentNullException`

### Requirement 2: Entity Type Extraction from Property Type

**User Story:** As a developer, I want the generator to automatically determine the child entity type from my property's declared type, so that I don't need to specify `EntityType` redundantly.

#### Acceptance Criteria

1. WHEN a property has Bare_RelatedEntity and the property type is a generic type whose unconstructed form implements `System.Collections.Generic.IEnumerable<T>` (excluding `string`), such as `List<T>`, `IList<T>`, `ICollection<T>`, or `IEnumerable<T>`, THE Entity_Analyzer SHALL extract the first generic type argument `T` as the child entity type and set `RelationshipModel.EntityType` to the fully qualified display string of `T`
2. WHEN a property has Bare_RelatedEntity and the property type is a nullable reference type `T?` that is not a collection type per criterion 1, THE Entity_Analyzer SHALL unwrap the nullable annotation and extract the underlying type `T` as the child entity type, setting `RelationshipModel.EntityType` to the fully qualified display string of `T`
3. WHEN a property has Bare_RelatedEntity and the property type is a non-nullable, non-collection type `T` (as determined by failing both criteria 1 and 2), THE Entity_Analyzer SHALL use `T` directly as the child entity type, setting `RelationshipModel.EntityType` to the fully qualified display string of `T`
4. WHEN a property has Bare_RelatedEntity and an explicit `EntityType` named argument is also provided, THE Entity_Analyzer SHALL use the explicit `EntityType` value for `RelationshipModel.EntityType`, ignoring the type inferred from the property declaration
5. WHEN a property has Explicit_RelatedEntity and no `EntityType` named argument, THE Entity_Analyzer SHALL continue to use the existing entity type extraction logic unchanged, without applying property-type inference from criteria 1–3
6. IF a property has Bare_RelatedEntity and the property type is a non-generic type that implements `IEnumerable` (such as `ArrayList` or raw `IEnumerable`), THEN THE Entity_Analyzer SHALL report a diagnostic error indicating that the element type cannot be inferred from a non-generic collection, and the developer must specify `EntityType` explicitly

### Requirement 3: Deferred Pattern Resolution Pass

**User Story:** As a developer, I want the generator to resolve inferred patterns correctly regardless of entity analysis order, so that bare `[RelatedEntity]` works reliably in all compilation scenarios.

#### Acceptance Criteria

1. THE Source_Generator SHALL execute a Pattern_Resolution_Pass in `DynamoDbSourceGenerator.Execute()` after all entities have been individually analyzed and grouped into Table_Groups via `GroupEntitiesByTableName()`, but before the overlap analysis pass and code generation begins
2. WHEN the Pattern_Resolution_Pass encounters a Relationship_Model whose `SortKeyPattern` is null or empty (indicating a bare `[RelatedEntity]` with no explicit pattern), THE Source_Generator SHALL resolve the child entity type name from the Relationship_Model's `EntityType` property if set, or by extracting the element type from the `PropertyType` string if `EntityType` is null, and then look up a matching Entity_Model within the same Table_Group by comparing that resolved type name to each Entity_Model's `ClassName`
3. WHEN the child Entity_Model is found in the same Table_Group, THE Pattern_Resolution_Pass SHALL read the child entity's sort key property's `DerivedDiscriminatorPattern` and assign it to the Relationship_Model's `SortKeyPattern`
4. WHEN the child Entity_Model is found but its sort key property has no `DerivedDiscriminatorPattern` (i.e., the value is null, which occurs when `NormalizedKeyFormat` is `"{0}"` indicating a bare key with no prefix or computed structure), THE Pattern_Resolution_Pass SHALL emit a compile-time diagnostic at Error severity indicating that the child entity's sort key has no distinguishing pattern for inference, and leave the `SortKeyPattern` unchanged (null or empty)
5. WHEN no Entity_Model with a matching `ClassName` is found in any Table_Group (the resolved type name does not correspond to a class annotated with `[DynamoDbTable]`), THE Pattern_Resolution_Pass SHALL emit a compile-time diagnostic at Error severity indicating the referenced type is not a known DynamoDB table entity, and leave the `SortKeyPattern` unchanged (null or empty)
6. WHEN the child Entity_Model is found but its `TableName` differs from the parent Entity_Model's `TableName`, THE Pattern_Resolution_Pass SHALL emit a compile-time diagnostic at Error severity identifying both table names and stating that related entities must share the same DynamoDB table, and leave the `SortKeyPattern` unchanged (null or empty)
7. WHEN a Relationship_Model already has a non-null, non-empty `SortKeyPattern` (set from an explicit `[RelatedEntity("pattern")]` constructor argument), THE Pattern_Resolution_Pass SHALL skip that relationship without modification

### Requirement 4: RelationshipModel Enhancement

**User Story:** As a maintainer of the source generator, I want the RelationshipModel to clearly indicate whether its pattern was inferred or explicitly provided, so that diagnostics and debugging can distinguish between the two.

#### Acceptance Criteria

1. THE Relationship_Model SHALL include a boolean property named `IsPatternInferred` that indicates whether its `SortKeyPattern` was inferred from the child entity's Derived_Discriminator_Pattern (`true`) or explicitly provided via Explicit_RelatedEntity (`false`), defaulting to `false`
2. WHEN the Entity_Analyzer extracts a relationship with Explicit_RelatedEntity, THE Relationship_Model SHALL set `IsPatternInferred` to `false`
3. WHEN the Pattern_Resolution_Pass resolves a Bare_RelatedEntity pattern from child entity metadata, THE Relationship_Model SHALL set `IsPatternInferred` to `true`
4. THE Relationship_Model SHALL include a string property named `ResolvedEntityType` that holds the fully resolved child entity type name, populated from the property type extraction (Requirement 2) when the `EntityType` named argument is absent, or from the explicit `EntityType` argument when present, so that the resolved type name is available regardless of how it was determined
5. WHEN neither property type extraction nor an explicit `EntityType` argument yields a child entity type name, THE Relationship_Model SHALL leave `ResolvedEntityType` as null

### Requirement 5: Compile-Time Diagnostics

**User Story:** As a developer, I want clear compile-time error messages when bare `[RelatedEntity]` cannot infer a pattern, so that I can fix the issue or fall back to an explicit pattern.

#### Acceptance Criteria

1. WHEN Bare_RelatedEntity is used and the property's element type (extracted from `List<T>`, `IList<T>`, or `T` for non-collection properties) cannot be resolved to a `[DynamoDbTable]` entity within the same Table_Group, THE Source_Generator SHALL emit a diagnostic with code FDDB130, severity Error, reported on the `[RelatedEntity]` property's source location, with a message identifying the unresolved type name and suggesting either specifying `EntityType` on the attribute or providing an explicit pattern string as the constructor argument
2. WHEN Bare_RelatedEntity is used and the resolved child entity has a trivial Derived_Discriminator_Pattern (its NormalizedKeyFormat is `{0}` with no literal segments or additional placeholders), THE Source_Generator SHALL emit a diagnostic with code FDDB131, severity Error, reported on the `[RelatedEntity]` property's source location, with a message naming the child entity type and explaining that its sort key format has no distinguishing literal structure from which to derive a matching pattern, and suggesting an explicit pattern string
3. WHEN Bare_RelatedEntity is used and the resolved child entity's table name differs from the parent entity's table name, THE Source_Generator SHALL emit a diagnostic with code FDDB132, severity Error, reported on the `[RelatedEntity]` property's source location, with a message identifying the parent entity's table name and the child entity's table name and stating that related entities must share the same DynamoDB table
4. IF the Pattern_Resolution_Pass emits any of FDDB130, FDDB131, or FDDB132 for a Bare_RelatedEntity property, THEN THE Source_Generator SHALL exclude that `[RelatedEntity]` relationship from all subsequent code generation phases so that no matching code, `ToCompositeEntityAsync` mapping logic, or `FromDynamoDb` population code is emitted for the excluded relationship
5. THE Source_Generator SHALL assign diagnostic codes FDDB130, FDDB131, and FDDB132 to the three error conditions defined in criteria 1, 2, and 3 respectively, each registered as a unique `DiagnosticDescriptor` in `DiagnosticDescriptors.cs` with a help link URI following the existing `DiagnosticHelpLinks.BaseUrlFormat` pattern

### Requirement 6: Code Generation Compatibility

**User Story:** As a developer using composite entities, I want inferred patterns to produce identical generated code as equivalent explicit patterns, so that runtime behavior is unchanged.

#### Acceptance Criteria

1. WHEN the Pattern_Resolution_Pass successfully resolves a Bare_RelatedEntity pattern to a `SortKeyPattern` string value, THE Mapper_Generator SHALL produce character-for-character identical generated C# source code for that relationship's sort key matching, parent-entity exclusion, and composite assembly logic as if the same `SortKeyPattern` string had been provided explicitly in `[RelatedEntity("pattern")]`; the `IsPatternInferred` flag on the Relationship_Model SHALL NOT influence any code emission path in the Mapper_Generator
2. IF a Relationship_Model has a null `SortKeyPattern` after the Pattern_Resolution_Pass completes, THEN THE Mapper_Generator SHALL omit the entire sort key matching block, item collection, and entity assembly code for that relationship, producing no composite entity assembly code referencing that relationship's `PropertyName`
3. WHEN the Mapper_Generator generates the parent entity's `MatchesEntity` method, THE generated exclusion conditions that prevent the parent from matching child entity sort key patterns SHALL use the resolved `SortKeyPattern` from Bare_RelatedEntity relationships identically to how they use patterns from Explicit_RelatedEntity relationships
4. THE generated `ToCompositeEntityAsync` and `ToCompositeEntityListAsync` methods SHALL match the same set of child items and populate the same related entity collections in the same order for a given set of DynamoDB items, regardless of whether the relationship's `SortKeyPattern` was inferred via Bare_RelatedEntity or provided via Explicit_RelatedEntity with the same pattern string

### Requirement 7: Backward Compatibility

**User Story:** As a developer with existing composite entity code, I want all existing `[RelatedEntity("pattern")]` usages to continue working without any changes, so that the upgrade is non-breaking.

#### Acceptance Criteria

1. WHEN the Source_Generator processes an Explicit_RelatedEntity using a wildcard-only pattern (e.g., `[RelatedEntity("INVOICE#*#LINE#*")]`, `[RelatedEntity("audit#*")]`, `[RelatedEntity("summary")]`), THE Source_Generator SHALL produce byte-identical generated C# source output compared to the output produced by the Source_Generator version immediately prior to the change
2. WHEN the Source_Generator processes an Explicit_RelatedEntity using a named placeholder pattern (e.g., `[RelatedEntity("{InvoiceNumber}#LINE#*")]`), THE Source_Generator SHALL produce byte-identical generated C# source output compared to the output produced by the Source_Generator version immediately prior to the change, since named placeholder normalization for RelatedEntity patterns is already implemented
3. WHEN an Explicit_RelatedEntity provides a pattern containing named placeholders, THE Entity_Analyzer SHALL pass the pattern through the existing NamedPlaceholderNormalizer.Normalize method with the same entity property name set, and SHALL use the resulting positional format as the SortKeyPattern on the RelationshipModel, identical to the current processing flow
4. THE `RelatedEntityAttribute` class SHALL expose both the existing single-parameter constructor `RelatedEntityAttribute(string sortKeyPattern)` that sets `SortKeyPattern` to the provided non-null value, and a new parameterless constructor `RelatedEntityAttribute()` that sets `SortKeyPattern` to null, without removing or altering the signature of the existing constructor
5. IF a project references the updated library and contains only Explicit_RelatedEntity usages with no bare `[RelatedEntity]` attributes, THEN THE Source_Generator SHALL compile and produce output without requiring any source code changes to the consuming project

### Requirement 8: Validation of Bare RelatedEntity Relationships

**User Story:** As a developer, I want the generator to validate inferred relationships with the same rigor as explicit ones, so that I'm alerted to ambiguous or conflicting patterns.

#### Acceptance Criteria

1. WHEN the Pattern_Resolution_Pass has resolved all Bare_RelatedEntity patterns for a parent entity, THE Source_Generator SHALL run the full `ValidateRelatedEntityConfiguration` validation suite on that entity's relationships, including ambiguous pattern detection, conflicting pattern detection, sort key existence check, and scalability warnings
2. WHEN a resolved Bare_RelatedEntity pattern equals `"*"` or is null or whitespace-only after inference, THE Source_Generator SHALL emit the existing ambiguous pattern diagnostic referencing the property name and the resolved pattern value
3. WHEN a resolved Bare_RelatedEntity pattern conflicts with any other relationship pattern on the same parent entity, whether that other pattern is an explicitly declared pattern or another resolved Bare_RelatedEntity pattern, THE Source_Generator SHALL emit the existing conflicting pattern diagnostic for each conflicting pair

### Requirement 9: Testing

**User Story:** As a maintainer, I want comprehensive tests covering the bare `[RelatedEntity]` feature at multiple levels, so that regressions are caught early.

#### Acceptance Criteria

1. THE test suite SHALL include code generation tests that verify the Source_Generator produces a `RelationshipModel` with `IsPatternInferred` set to `true`, `SortKeyPattern` equal to the child entity's `DerivedDiscriminatorPattern`, and `EntityType` equal to the child entity's type name, WHEN processing a Bare_RelatedEntity declaration on a property of type `List<C>` where `C` is a `[DynamoDbTable]` entity on the same table with a non-null `DerivedDiscriminatorPattern`
2. THE test suite SHALL include generated code compilation tests that compile entities using Bare_RelatedEntity through the full source generator pipeline (source generation → Roslyn compilation) using `DynamicCompilationHelper.CompileAndLoad()` and verify the compilation produces zero `DiagnosticSeverity.Error` diagnostics
3. THE test suite SHALL include at least 1 integration test that exercises the full pipeline: define a parent entity with Bare_RelatedEntity on `List<C>` and a child entity `C` with a `[Computed]`-based sort key on the same table, run the Source_Generator, compile via `DynamicCompilationHelper.CompileAndLoad()`, invoke `ToCompositeEntityAsync` via reflection with a DynamoDB item set containing 1 parent item and at least 2 child items, and verify the returned parent entity's collection property contains exactly the child items whose sort keys match the child entity's `DerivedDiscriminatorPattern`
4. THE test suite SHALL include 1 diagnostic test per error condition (3 total) that verify the Source_Generator emits the correct diagnostic code and `DiagnosticSeverity.Error` severity for each: (a) Bare_RelatedEntity on a property whose element type is not a known `[DynamoDbTable]` entity, (b) Bare_RelatedEntity where the child entity has a null `DerivedDiscriminatorPattern`, and (c) Bare_RelatedEntity where the child entity is declared on a different table than the parent
5. THE test suite SHALL include at least 1 backward compatibility test that compiles an entity using Explicit_RelatedEntity with a pattern string through the full source generator pipeline and verifies the resulting `RelationshipModel` has `IsPatternInferred` set to `false` and `SortKeyPattern` equal to the explicitly provided pattern string
6. FOR ALL entities with Bare_RelatedEntity on a property of type `List<C>`, THE inferred `SortKeyPattern` in the generated `RelationshipModel` SHALL equal the child entity `C`'s sort key `DerivedDiscriminatorPattern` value
7. FOR ALL entities with Explicit_RelatedEntity, THE `SortKeyPattern` in the generated `RelationshipModel` SHALL equal the explicitly provided pattern string, and `IsPatternInferred` SHALL be `false`, regardless of whether the child entity has a `DerivedDiscriminatorPattern`

### Requirement 10: Documentation

**User Story:** As a developer reading the documentation, I want to learn about the bare `[RelatedEntity]` feature with clear examples and migration guidance, so that I can adopt it in my projects.

#### Acceptance Criteria

1. WHEN the bare `[RelatedEntity]` feature is implemented, THE documentation in `docs/advanced-topics/CompositeEntities.md` SHALL be updated to include a new subsection titled "Bare RelatedEntity (Recommended)" that presents Bare_RelatedEntity as the recommended approach with at least one complete entity definition example (parent entity with `[RelatedEntity]` on a `List<T>` property and corresponding child entity), followed by a "Fallback: Explicit Pattern" subsection showing Explicit_RelatedEntity with the same parent-child relationship, and a comparison table listing the three tiers (bare, explicit pattern, named placeholder) with a one-line description and when-to-use guidance for each
2. WHEN the bare `[RelatedEntity]` feature is implemented, THE documentation in `docs/reference/AttributeReference.md` SHALL be updated in the `[RelatedEntity]` section to document the parameterless constructor `RelatedEntityAttribute()`, the nullable `SortKeyPattern` property (type `string?`, default `null`), the inference behavior when `SortKeyPattern` is null (child entity type inferred from property generic type argument, sort key pattern inferred from child entity's `DerivedDiscriminatorPattern`), and at least one code example showing the bare syntax alongside the existing explicit-pattern constructor
3. WHEN the bare `[RelatedEntity]` feature is implemented, THE steering document `.kiro/steering/fluentdynamodb.md` SHALL be updated in the "Composite Entity Definition" section to show the Bare_RelatedEntity syntax as the first example with a comment indicating it is recommended, retain the existing Explicit_RelatedEntity example with a comment indicating it is the fallback, and update the `RelatedEntity Attribute` properties table to reflect that `Pattern (positional)` is now optional with a default of `null` (inferred from child entity metadata)
4. WHEN the bare `[RelatedEntity]` feature is implemented, THE `docs/DOCUMENTATION_CHANGELOG.md` SHALL include one entry per documentation file updated (at minimum entries for `docs/advanced-topics/CompositeEntities.md`, `docs/reference/AttributeReference.md`, and `.kiro/steering/fluentdynamodb.md`), each entry containing the date, file path, before code example, after code example, and reason for the change
5. WHEN the bare `[RelatedEntity]` feature is implemented, THE `CHANGELOG.md` SHALL include an entry in the `[Unreleased]` > `Added` section describing the bare `[RelatedEntity]` inference feature, including a code example of the bare syntax on a `List<T>` property and a note that existing explicit-pattern usage is unaffected
6. IF the `docs/reference/AttributeReference.md` update documents the bare `[RelatedEntity]` constructor, THEN THE documentation SHALL list the compile-time diagnostic codes and their trigger conditions for the 4 error scenarios defined in the feature design: property type not a known `[DynamoDbTable]` entity, child entity has no `DerivedDiscriminatorPattern`, child entity is on a different table, and `EntityType` override type not found
