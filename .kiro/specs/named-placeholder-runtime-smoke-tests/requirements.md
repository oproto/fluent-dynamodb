# Requirements Document

## Introduction

The named property placeholder feature (`[Computed("{PropertyName}")]` syntax) was implemented with solid normalizer and diagnostic unit tests, but the integration test suite has critical runtime verification gaps. Existing integration tests verify that named-placeholder entities produce generated code, and in some cases that the code compiles, but most do not load the compiled assembly and invoke the generated methods at runtime. This spec defines end-to-end runtime smoke tests that exercise the full generate → compile → execute pipeline for named-placeholder entities, covering FromDynamoDb deserialization, ExtractComponents helpers, composite entity assembly, multi-entity table discrimination, GSI indexes, and 3+ property computed keys.

## Glossary

- **Source_Generator**: The Roslyn incremental source generator in `Oproto.FluentDynamoDb.SourceGenerator` that analyzes entity class declarations and emits implementation code at compile time.
- **Runtime_Smoke_Test**: An integration test that dynamically compiles an entity definition with the source generator, loads the resulting assembly, and invokes generated methods via reflection to verify correct behavior.
- **DynamicCompilationHelper**: The test utility class (`Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers.DynamicCompilationHelper`) that compiles source code with the source generator, emits an in-memory assembly, and returns a `DynamicCompilationResult` containing the loaded `Assembly` and diagnostics.
- **CompileAndLoad**: The method on DynamicCompilationHelper that runs the full pipeline: parse source → run source generator → compile → emit → load assembly. Returns a `DynamicCompilationResult`.
- **ToDynamoDb**: A generated static method on each entity that serializes an entity instance into a `Dictionary<string, AttributeValue>` for DynamoDB storage.
- **FromDynamoDb**: A generated static method on each entity that deserializes a `Dictionary<string, AttributeValue>` from DynamoDB into an entity instance, including populating `[Extracted]` properties from computed key components.
- **Keys_Class**: A nested static class generated on each entity with computed keys, containing `Pk()`, `Sk()`, `ExtractPkComponents()`, and `ExtractSkComponents()` methods.
- **MatchesEntity**: A generated static method on each entity that determines whether a raw DynamoDB item belongs to that entity type, based on discriminator patterns derived from key prefixes and computed key formats.
- **Named_Placeholder**: A `{PropertyName}` or `{PropertyName:format}` token in a `[Computed]` format string or `[RelatedEntity]` sort key pattern that is normalized to positional `{N}` form by the source generator before code generation.
- **Composite_Entity**: An entity with `[RelatedEntity]` collections that spans multiple DynamoDB items sharing the same partition key, assembled via `ToCompositeEntityAsync`.

## Requirements

### Requirement 1: FromDynamoDb Round-Trip with Extracted Properties

**User Story:** As a developer using named-placeholder `[Computed]` syntax with `[Extracted]` properties, I want the generated `FromDynamoDb` method to correctly populate extracted properties from the computed key value, so that I can read back the individual components of a composite key after deserialization.

#### Acceptance Criteria

1. WHEN an entity uses named-placeholder `[Computed]` syntax on a sort key with two `[Extracted]` properties (e.g., `[Computed("INVOICE#{InvoiceNumber}#LINE#{LineNumber}")]` with `[Extracted("Sk", 0)]` on `InvoiceNumber` and `[Extracted("Sk", 1)]` on `LineNumber`), THE Runtime_Smoke_Test SHALL CompileAndLoad the entity, invoke `ToDynamoDb` on an instance, invoke `FromDynamoDb` on the resulting dictionary, and assert that both `[Extracted]` properties on the deserialized instance contain the correct values matching the original input.
2. WHEN an entity uses named-placeholder `[Computed]` syntax with format specifiers on a partition key with `[Extracted]` properties (e.g., `[Computed("ENTRY#{Date:yyyy-MM-dd}#{Sequence:D4}")]` with `[Extracted("Pk", 0)]` on `Date` and `[Extracted("Pk", 1)]` on `Sequence`), THE Runtime_Smoke_Test SHALL CompileAndLoad the entity, invoke `ToDynamoDb`, invoke `FromDynamoDb`, and assert that both `[Extracted]` properties round-trip correctly through the formatted key value.

### Requirement 2: ExtractComponents Runtime Verification

**User Story:** As a developer using named-placeholder `[Computed]` syntax, I want `Keys.ExtractSkComponents()` and `Keys.ExtractPkComponents()` to correctly decompose a computed key string back into its component values, so that I can extract individual values from composite keys at runtime.

#### Acceptance Criteria

1. WHEN an entity uses named-placeholder `[Computed]` syntax on a sort key (e.g., `[Computed("INVOICE#{InvoiceNumber}#LINE#{LineNumber}")]`), THE Runtime_Smoke_Test SHALL CompileAndLoad the entity, invoke `Keys.Sk("INV-001", 1)` to build the key string, invoke `Keys.ExtractSkComponents()` on that string, and assert the extracted components match the original input values.
2. WHEN an entity uses named-placeholder `[Computed]` syntax with format specifiers on a partition key (e.g., `[Computed("ENTRY#{Date:yyyy-MM-dd}")]`), THE Runtime_Smoke_Test SHALL CompileAndLoad the entity, invoke `Keys.Pk()` with a `DateOnly` value, invoke `Keys.ExtractPkComponents()` on the resulting string, and assert the extracted date value matches the original input.

### Requirement 3: Composite Entity Assembly

**User Story:** As a developer using named-placeholder `[RelatedEntity]` syntax, I want the generated MatchesEntity discrimination logic to correctly identify child items based on normalized sort key patterns, so that composite entities can be assembled from multi-item query results.

#### Acceptance Criteria

1. WHEN a parent entity uses `[RelatedEntity("{InvoiceNumber}#LINE#*")]` with named placeholders and a child entity has sort keys matching the pattern (e.g., `"INV-001#LINE#1"`, `"INV-001#LINE#2"`), THE Runtime_Smoke_Test SHALL CompileAndLoad both entities, construct DynamoDB items representing a parent and two children, invoke the child entity's `MatchesEntity` method on each item, and assert that items with sort keys matching the child's discriminator pattern return true and items with the parent's sort key return false.

### Requirement 4: Multi-Entity Table Discriminator Verification

**User Story:** As a developer with two entities on the same table using named-placeholder `[Computed]` sort keys with different prefixes, I want `MatchesEntity` to correctly distinguish between them, so that query results are mapped to the correct entity types.

#### Acceptance Criteria

1. WHEN two entities share the same table and both use named-placeholder `[Computed]` sort keys with different literal prefixes (e.g., Entity A with `[Computed("ORDER#{OrderId}")]` and Entity B with `[Computed("SHIPMENT#{ShipmentId}")]`), THE Runtime_Smoke_Test SHALL CompileAndLoad both entities, construct a DynamoDB item for each entity type, invoke `MatchesEntity` on each entity type for each item, and assert that each entity type matches only its own items (Entity A matches `ORDER#...` but not `SHIPMENT#...`, and vice versa).

### Requirement 5: Three-Plus Property Computed Key

**User Story:** As a developer using named-placeholder `[Computed]` syntax with three or more source properties, I want `Keys.Pk()` and `Keys.ExtractPkComponents()` to handle all properties correctly, so that entities with complex composite keys work at runtime.

#### Acceptance Criteria

1. WHEN an entity uses named-placeholder `[Computed]` syntax with three source properties and a separator-based format (e.g., `[Computed("{Year}#{Month}#{Day}")]`), THE Runtime_Smoke_Test SHALL CompileAndLoad the entity, invoke `Keys.Pk(2024, 12, 25)`, assert the result is `"2024#12#25"`, invoke `Keys.ExtractPkComponents("2024#12#25")`, and assert the extracted components match the original values.
2. WHEN an entity uses named-placeholder `[Computed]` syntax with three source properties and format specifiers (e.g., `[Computed("{Year:D4}#{Month:D2}#{Day:D2}")]`), THE Runtime_Smoke_Test SHALL CompileAndLoad the entity, invoke `Keys.Pk(2024, 1, 5)`, assert the result is `"2024#01#05"`, invoke `Keys.ExtractPkComponents("2024#01#05")`, and assert the extracted components match the original values.

### Requirement 6: GSI with Named-Placeholder Computed Key

**User Story:** As a developer using a named-placeholder `[Computed]` key that also serves as a `[GsiPartitionKey]`, I want the generated index accessor and Keys class to work correctly at runtime, so that I can use named placeholders for GSI keys without issues.

#### Acceptance Criteria

1. WHEN an entity has a property decorated with both `[GsiPartitionKey("status-index")]` and `[Computed("{Status}#{Category}")]`, THE Runtime_Smoke_Test SHALL CompileAndLoad the entity, invoke the Keys class method for that property with sample values, and assert the generated key string is correct (e.g., `Keys.Gsi1Pk("active", "electronics")` returns `"active#electronics"`).
2. WHEN an entity has a `[GsiPartitionKey]` property with named-placeholder `[Computed]` syntax, THE Runtime_Smoke_Test SHALL CompileAndLoad the entity, invoke `ToDynamoDb` on an instance with source properties set, and assert the GSI key attribute in the resulting dictionary contains the correctly computed value.

### Requirement 7: Backward Compatibility Runtime Round-Trip

**User Story:** As a developer with existing entities using positional `[Computed]` syntax, I want a full runtime round-trip test (ToDynamoDb → FromDynamoDb → Extracted properties) to confirm that the named placeholder changes did not break existing positional behavior at runtime.

#### Acceptance Criteria

1. WHEN an entity uses existing positional `[Computed("Year", "Month", Format = "{0}#{1}")]` syntax with `[Extracted]` properties, THE Runtime_Smoke_Test SHALL CompileAndLoad the entity, create an instance with known values, invoke `ToDynamoDb`, invoke `FromDynamoDb` on the resulting dictionary, and assert that the deserialized instance has correct values for all properties including `[Extracted]` properties.
2. WHEN an entity uses existing `[Computed]` syntax with `Separator` parameter and `[Extracted]` properties, THE Runtime_Smoke_Test SHALL CompileAndLoad the entity, invoke `Keys.Pk()` to build a key, invoke `Keys.ExtractPkComponents()`, and assert the extracted components match the original values.

### Requirement 8: Test Infrastructure and Organization

**User Story:** As a developer maintaining the test suite, I want all runtime smoke tests in a single, well-organized test class within the existing source generator test project, so that the tests are easy to find, run, and maintain.

#### Acceptance Criteria

1. THE Runtime_Smoke_Test class SHALL be located at `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/NamedPlaceholderRuntimeSmokeTests.cs` within the existing source generator test project.
2. THE Runtime_Smoke_Test class SHALL use the existing `DynamicCompilationHelper.CompileAndLoad()` method for compile-and-load operations, `DynamicCompilationHelper.GetFluentDynamoDbReferences()` for metadata references, and reflection for runtime method invocation, consistent with the patterns established in the existing `NamedPlaceholderCodeGenerationTests.cs`.
3. THE Runtime_Smoke_Test class SHALL use xUnit `[Fact]` attributes for test methods and FluentAssertions for assertions, consistent with the project conventions.
4. WHEN any Runtime_Smoke_Test is executed, THE test SHALL fail with a descriptive assertion message if the entity fails to compile, rather than throwing an unhandled exception from the compilation step.
5. THE Runtime_Smoke_Test class SHALL be marked with `[Trait("Category", "Integration")]` consistent with existing integration test conventions.
