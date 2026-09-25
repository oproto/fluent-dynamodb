# Implementation Plan: Bare `[RelatedEntity]` Inference

## Overview

Add a parameterless `[RelatedEntity]` overload that infers the sort key matching pattern from the child entity's `DerivedDiscriminatorPattern`. This eliminates manual pattern duplication by making the child entity's key definition the single source of truth. The implementation adds a deferred Pattern Resolution Pass to the source generator pipeline that runs after all entities are analyzed, resolves bare patterns from child entity metadata, and feeds the resolved patterns into the existing MapperGenerator code paths unchanged.

## Tasks

- [x] 1. Modify `RelatedEntityAttribute` to support parameterless construction
  - [x] 1.1 Add parameterless constructor and make `SortKeyPattern` nullable
    - In `Oproto.FluentDynamoDb/Attributes/RelatedEntityAttribute.cs`:
    - Change `SortKeyPattern` property type from `string` to `string?`
    - Add parameterless constructor `RelatedEntityAttribute()` that sets `SortKeyPattern = null`
    - Add `ArgumentNullException.ThrowIfNull(sortKeyPattern)` guard to existing `string` constructor
    - Preserve the existing `EntityType` named property unchanged
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 7.4_

- [x] 2. Enhance `RelationshipModel` with inference tracking properties
  - [x] 2.1 Add `IsPatternInferred`, `ResolvedEntityType`, and make `SortKeyPattern` nullable
    - In `Oproto.FluentDynamoDb.SourceGenerator/Models/RelationshipModel.cs`:
    - Change `SortKeyPattern` type from `string` to `string?`, default to `null`
    - Add `bool IsPatternInferred { get; set; }` defaulting to `false`
    - Add `string? ResolvedEntityType { get; set; }` defaulting to `null`
    - Update `IsWildcardPattern` to `SortKeyPattern?.Contains('*') ?? false` for null safety
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

- [x] 3. Register new diagnostic descriptors
  - [x] 3.1 Add FDDB130, FDDB131, FDDB132 diagnostic descriptors
    - In `Oproto.FluentDynamoDb.SourceGenerator/Diagnostics/DiagnosticDescriptors.cs`:
    - Add a `// Bare RelatedEntity Inference Diagnostics (FDDB130-FDDB132)` section comment
    - FDDB130 (Error): Bare `[RelatedEntity]` unresolved entity type — message template: `"[RelatedEntity] on property '{0}' in entity '{1}' references type '{2}' which is not a known [DynamoDbTable] entity. Specify EntityType explicitly or provide an explicit pattern string."`
    - FDDB131 (Error): Bare `[RelatedEntity]` trivial sort key pattern — message template: `"[RelatedEntity] on property '{0}' in entity '{1}' cannot infer a matching pattern because child entity '{2}' has a bare sort key with no distinguishing structure (NormalizedKeyFormat is '{{0}}'). Provide an explicit pattern string."`
    - FDDB132 (Error): Bare `[RelatedEntity]` table mismatch — message template: `"[RelatedEntity] on property '{0}' in entity '{1}' (table '{2}') references child entity '{3}' which is on table '{4}'. Related entities must share the same DynamoDB table."`
    - Each with `helpLinkUri: string.Format(DiagnosticHelpLinks.BaseUrlFormat, "FDDB13N")` and `isEnabledByDefault: true`
    - _Requirements: 5.1, 5.2, 5.3, 5.5_

- [x] 4. Checkpoint — Build and verify no regressions
  - Run `dotnet build-server shutdown && dotnet build` to verify attribute and model changes compile cleanly
  - Run `dotnet test` to verify all existing tests pass with nullable `SortKeyPattern`
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Update `EntityAnalyzer.ExtractRelationships()` for bare `[RelatedEntity]` and entity type inference
  - [x] 5.1 Handle absent constructor argument and infer entity type from property type
    - In `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`, modify `ExtractRelationships()`:
    - When the `[RelatedEntity]` attribute has no positional constructor arguments (bare form), leave `SortKeyPattern` as `null` on the `RelationshipModel` — skip the pattern extraction block entirely
    - Add a new helper method `InferEntityTypeFromProperty(IPropertySymbol propertySymbol)` that:
      - For generic types implementing `IEnumerable<T>` (e.g., `List<T>`, `IList<T>`, `ICollection<T>`) — extract first generic type argument `T` via `INamedTypeSymbol.TypeArguments[0].ToDisplayString()`
      - For nullable reference types `T?` — unwrap via `NullableAnnotation` check and use `OriginalDefinition`
      - For non-nullable, non-collection types `T` — use `T.ToDisplayString()` directly
      - For non-generic collections (`ArrayList`, raw `IEnumerable`) — report a diagnostic error and return null
    - When the attribute is bare AND no explicit `EntityType` named argument is present, call `InferEntityTypeFromProperty` and set `relationship.ResolvedEntityType` to the result
    - When explicit `EntityType` is present, set `relationship.ResolvedEntityType` from the explicit `EntityType` value (regardless of bare vs explicit form)
    - Existing explicit pattern + entity type extraction logic must remain unchanged for `[RelatedEntity("pattern")]`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

- [x] 6. Create `PatternResolutionPass` — the deferred pattern resolution logic
  - [x] 6.1 Implement `PatternResolutionPass.Resolve()` static method
    - Create new file `Oproto.FluentDynamoDb.SourceGenerator/Analysis/PatternResolutionPass.cs`
    - Follow the `PatternOverlapAnalyzer` static class pattern
    - Method signature: `public static List<Diagnostic> Resolve(Dictionary<string, List<EntityModel>> entitiesByTable)`
    - For each entity's relationships where `SortKeyPattern` is null or empty:
      1. Resolve child entity type name from `relationship.EntityType` (if set), else `relationship.ResolvedEntityType`, else parse from `relationship.PropertyType` string
      2. Search same table group for `EntityModel` with matching `ClassName`
      3. If not found in any table group → emit FDDB130 with property location
      4. If found in different table → emit FDDB132 with both table names
      5. If found but sort key `DerivedDiscriminatorPattern` is null (trivial `{0}` key format) → emit FDDB131
      6. On success: set `relationship.SortKeyPattern = childSortKey.DerivedDiscriminatorPattern`, set `relationship.IsPatternInferred = true`, set `relationship.EntityType` to child class name if not already set
    - Skip relationships where `SortKeyPattern` is already non-null, non-empty (explicit patterns)
    - Return all accumulated diagnostics
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

- [x] 7. Wire `PatternResolutionPass` into the source generator pipeline
  - [x] 7.1 Integrate resolution pass, exclusion, and re-validation into `DynamoDbSourceGenerator.Execute()`
    - In `Oproto.FluentDynamoDb.SourceGenerator/DynamoDbSourceGenerator.cs`:
    - After the `ValidateTableNamespaceConsistency` loop and before the overlap analysis pass, invoke `PatternResolutionPass.Resolve(entitiesByTable)` and report all returned diagnostics
    - Add `ExcludeFailedRelationships()` — for each entity, filter `Relationships` array to remove entries where `SortKeyPattern` is still null after resolution (prevents MapperGenerator from generating code for broken relationships)
    - Add `RevalidateResolvedRelationships()` — for entities that had bare patterns resolved, call `ValidateRelatedEntityConfiguration()` to check for ambiguous or conflicting patterns from inferred values (Note: `ValidateRelatedEntityConfiguration` is currently an instance method on `EntityAnalyzer`; may need to extract validation logic into a static helper or invoke it through the existing analyzer instance)
    - _Requirements: 3.1, 5.4, 8.1, 8.2, 8.3_

- [x] 8. Update `MapperGenerator` for null-safe `SortKeyPattern` handling
  - [x] 8.1 Add null guards in sort key pattern usage throughout `MapperGenerator`
    - In `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`:
    - In `GenerateSortKeyPatternMatching()`: add null/empty check at the top — if `sortKeyPattern` is null or empty, skip the entire pattern matching block
    - In composite entity methods (`GenerateRelatedEntityCollectionMappingAsync`, `GenerateRelatedEntitySingleMappingAsync`): skip generating mapping code for relationships where `SortKeyPattern` is null
    - Verify existing `.Where(p => !string.IsNullOrEmpty(p))` filters on `relatedPatterns` for `MatchesEntity` exclusion conditions handle null safely (they should via the null-coalescing behavior of `Select(r => r.SortKeyPattern)` producing nulls that are filtered out)
    - The `IsPatternInferred` flag must NOT influence any code emission path — resolved patterns produce identical output to explicit ones
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 9. Checkpoint — Build and verify full pipeline
  - Run `dotnet build-server shutdown && dotnet build` to verify all source generator changes compile
  - Run `dotnet test` to verify all existing tests pass with the new pipeline step
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Unit tests for attribute, model, and entity type inference
  - [x] 10.1 Write unit tests for `RelatedEntityAttribute` constructors
    - Create `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/BareRelatedEntityTests.cs`
    - Test parameterless constructor yields `SortKeyPattern == null`
    - Test string constructor preserves pattern value and `SortKeyPattern` is non-null
    - Test string constructor with `null` argument throws `ArgumentNullException`
    - Test `EntityType` named property is independent of constructor form
    - _Requirements: 1.1, 1.2, 1.3, 1.5, 9.5_

  - [x] 10.2 Write unit tests for `RelationshipModel` state tracking
    - In the same test file or a companion:
    - Test `IsPatternInferred` defaults to `false`
    - Test `IsWildcardPattern` returns `false` when `SortKeyPattern` is null
    - Test `ResolvedEntityType` defaults to null
    - Test after resolution: `IsPatternInferred == true`, `SortKeyPattern` set, `ResolvedEntityType` set
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 10.3 Write source generator tests for entity type inference from property types
    - Test `List<OrderLine>` → extracts `OrderLine` as child type
    - Test `IList<OrderLine>` → extracts `OrderLine`
    - Test `ICollection<OrderLine>?` → extracts `OrderLine` (unwrap nullable + collection)
    - Test `OrderLine?` → extracts `OrderLine` (unwrap nullable)
    - Test `OrderLine` (non-collection, non-nullable) → uses `OrderLine` directly
    - Test explicit `EntityType = typeof(OrderLine)` overrides property type inference
    - Test non-generic collection (e.g., `ArrayList`) → triggers diagnostic error
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

  - [x] 10.4 Write backward compatibility test for explicit `[RelatedEntity("pattern")]`
    - Compile an entity using explicit `[RelatedEntity("ORDER#*#LINE#*")]` through the full source generator pipeline
    - Verify `RelationshipModel` has `IsPatternInferred == false` and `SortKeyPattern == "ORDER#*#LINE#*"`
    - Verify generated output is unchanged by the new code paths
    - _Requirements: 7.1, 7.2, 7.3, 7.5, 9.5, 9.7_

- [x] 11. Diagnostic tests for FDDB130, FDDB131, FDDB132
  - [x] 11.1 Write diagnostic emission tests for all three error conditions
    - Create `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Diagnostics/BareRelatedEntityDiagnosticTests.cs`
    - Following the pattern in existing diagnostic test files (e.g., `ConstantKeyDiagnosticTests.cs`)
    - Test FDDB130: Bare `[RelatedEntity]` on `List<UnknownType>` where `UnknownType` is not a `[DynamoDbTable]` entity → diagnostic code `FDDB130`, severity `Error`
    - Test FDDB131: Bare `[RelatedEntity]` on `List<ChildEntity>` where `ChildEntity` has a bare sort key (`NormalizedKeyFormat` = `"{0}"`) → diagnostic code `FDDB131`, severity `Error`
    - Test FDDB132: Bare `[RelatedEntity]` on `List<ChildEntity>` where `ChildEntity` has `[DynamoDbTable("other-table")]` (different table) → diagnostic code `FDDB132`, severity `Error`
    - Each test: define entities in source string, compile with `DynamicCompilationHelper`, run source generator, assert diagnostics contain the expected code and severity
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 9.4_

- [x] 12. Code generation equivalence test
  - [x] 12.1 Write test comparing generated output from bare vs explicit patterns
    - In `BareRelatedEntityTests.cs` or a separate test class:
    - Define a parent+child entity pair where child has `[Computed("INVOICE#{InvoiceId}#LINE#{LineNumber}")]` sort key → `DerivedDiscriminatorPattern` would be `INVOICE#*#LINE#*`
    - Compile version A: parent uses `[RelatedEntity]` (bare)
    - Compile version B: parent uses `[RelatedEntity("INVOICE#*#LINE#*")]` (explicit, same pattern)
    - Use `DynamicCompilationHelper.GetFluentDynamoDbReferences()` and `DynamoDbSourceGenerator` for both
    - Compare generated source files — the relationship mapping code for the parent entity should be character-for-character identical
    - _Requirements: 6.1, 6.3, 9.1_

- [x] 13. Checkpoint — Verify all unit and diagnostic tests pass
  - Run `dotnet build-server shutdown && dotnet test`
  - Ensure all tests pass, ask the user if questions arise.

- [x] 14. Integration tests — full pipeline through compilation and reflection
  - [x] 14.1 Write full pipeline integration test with `ToCompositeEntityAsync` invocation
    - Create `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/BareRelatedEntityIntegrationTests.cs`
    - Following the pattern in `NamedPlaceholderRuntimeSmokeTests.cs` and `DiscriminatorHydrationCorrectnessTests.cs`
    - Define parent entity with bare `[RelatedEntity]` on `List<InvoiceLine>` and child `InvoiceLine` with `[Computed]`-based sort key, both on the same table
    - Compile via `DynamicCompilationHelper.CompileAndLoad()` with `DynamoDbSourceGenerator`
    - Verify zero `DiagnosticSeverity.Error` diagnostics in compilation result
    - Create mock DynamoDB items: 1 parent item + 2+ child items with matching sort key patterns
    - Invoke `ToCompositeEntityAsync` via reflection on the compiled assembly
    - Assert the returned parent entity's `Lines` collection contains exactly the child items whose sort keys match the child entity's `DerivedDiscriminatorPattern`
    - _Requirements: 9.2, 9.3, 9.6_

  - [x] 14.2 Write generated code compilation test (no runtime invocation)
    - In the same integration test file:
    - Define entities using bare `[RelatedEntity]` with various collection types (`List<T>`, `IList<T>`, nullable `T?`)
    - Compile through full source generator pipeline via `DynamicCompilationHelper.CompileAndLoad()`
    - Assert compilation produces zero `DiagnosticSeverity.Error` diagnostics
    - _Requirements: 9.2_

- [x] 15. Checkpoint — Full test suite green
  - Run `dotnet build-server shutdown && dotnet test`
  - Ensure all tests pass, ask the user if questions arise.

- [x] 16. Documentation updates
  - [x] 16.1 Update `docs/advanced-topics/CompositeEntities.md`
    - Add a "Bare RelatedEntity (Recommended)" subsection before or alongside the existing explicit pattern section
    - Show a complete entity definition example: parent with bare `[RelatedEntity]` on `List<T>` and child entity with computed sort key
    - Add a "Fallback: Explicit Pattern" subsection with the existing explicit pattern example
    - Add a comparison table listing three tiers: bare (recommended), explicit pattern, named placeholder — with description and when-to-use for each
    - _Requirements: 10.1_

  - [x] 16.2 Update `docs/reference/AttributeReference.md`
    - In the `[RelatedEntity]` section, document the parameterless constructor `RelatedEntityAttribute()`
    - Document the nullable `SortKeyPattern` property (type `string?`, default `null`)
    - Document inference behavior: child entity type inferred from property generic type argument, sort key pattern inferred from child entity's `DerivedDiscriminatorPattern`
    - Add a code example showing bare syntax alongside existing explicit-pattern constructor
    - Document the compile-time diagnostic codes (FDDB130, FDDB131, FDDB132) and their trigger conditions
    - _Requirements: 10.2, 10.6_

  - [x] 16.3 Update `.kiro/steering/fluentdynamodb.md`
    - In the "Composite Entity Definition" section, show bare `[RelatedEntity]` syntax first with a comment indicating it is recommended
    - Retain the existing explicit-pattern example with a comment indicating it is the fallback
    - Update the `RelatedEntity Attribute` properties table: mark `Pattern (positional)` as optional with default `null` (inferred from child entity metadata)
    - _Requirements: 10.3_

  - [x] 16.4 Update `CHANGELOG.md` and `docs/DOCUMENTATION_CHANGELOG.md`
    - In `CHANGELOG.md`: add entry in `[Unreleased]` > `Added` section describing bare `[RelatedEntity]` inference with a code example of bare syntax on `List<T>` and note that explicit-pattern usage is unaffected
    - In `docs/DOCUMENTATION_CHANGELOG.md`: add one entry per documentation file updated (CompositeEntities.md, AttributeReference.md, fluentdynamodb.md) with date, file path, before/after examples, and reason
    - _Requirements: 10.4, 10.5_

- [x] 17. Final checkpoint — Full build and test suite
  - Run `dotnet build-server shutdown && dotnet build && dotnet test`
  - Verify all tests pass and no regressions
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP — no tasks in this plan are marked optional because the user explicitly requested all three testing levels and documentation
- Tests live in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/` (not `Oproto.FluentDynamoDb.UnitTests/`) since they test source generator behavior
- After any source generator modification, run `dotnet build-server shutdown` before building to avoid stale cached generators
- The `PatternResolutionPass` follows the same static class pattern as `PatternOverlapAnalyzer` and `CompoundPromotionPass` in the `Analysis/` folder
- `ValidateRelatedEntityConfiguration` is an instance method on `EntityAnalyzer` — re-validation may require extracting validation logic to a static helper or creating a minimal analyzer instance
- All library `await` calls require `.ConfigureAwait(false)` per tech steering — this feature is compile-time only, so no async code is being added to library projects
- The `DynamicCompilationHelper.CompileAndLoad()` pattern from `NamedPlaceholderRuntimeSmokeTests.cs` is the reference for integration tests

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1", "3.1"] },
    { "id": 1, "tasks": ["5.1"] },
    { "id": 2, "tasks": ["6.1"] },
    { "id": 3, "tasks": ["7.1", "8.1"] },
    { "id": 4, "tasks": ["10.1", "10.2", "10.3", "10.4"] },
    { "id": 5, "tasks": ["11.1", "12.1"] },
    { "id": 6, "tasks": ["14.1", "14.2"] },
    { "id": 7, "tasks": ["16.1", "16.2", "16.3", "16.4"] }
  ]
}
```
