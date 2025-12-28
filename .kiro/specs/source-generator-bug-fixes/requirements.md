# Requirements Document

## Introduction

This specification addresses multiple bugs and improvements in the Oproto.FluentDynamoDb library:

1. **Package Version Constraints**: Microsoft .NET packages need version ranges updated to support .NET 10.x
2. **Composite Entity DynamoDbMap Bug**: Source generator incorrectly deserializes `[DynamoDbMap]` properties in composite entity (multi-item) deserialization
3. **Nested Map Support**: Maps containing nested maps of different record types need proper support
4. **False Performance Warning**: `[RelatedEntity]` collections incorrectly trigger DYNDB023 performance warnings

## Glossary

- **Central_Package_Management**: NuGet feature using `Directory.Packages.props` to define all package versions in one location
- **Source_Generator**: The Roslyn source generator that produces entity mapping code at compile time
- **Composite_Entity**: An entity with `[RelatedEntity]` attributes that spans multiple DynamoDB items
- **DynamoDbMap_Property**: A property marked with `[DynamoDbMap]` attribute representing a nested object stored as a DynamoDB Map (M) type
- **Multi_Item_FromDynamoDb**: The generated `FromDynamoDb` method overload that accepts multiple items for composite entity assembly
- **RelatedEntity_Collection**: A `List<T>` property marked with `[RelatedEntity]` attribute for parent-child relationships
- **DYNDB023_Warning**: Performance warning diagnostic for complex collection types

## Requirements

### Requirement 1: Package Version Range Updates

**User Story:** As a library consumer, I want to use Oproto.FluentDynamoDb with .NET 10.x packages, so that I can upgrade my projects without dependency conflicts.

#### Acceptance Criteria

1. THE System.Text.Json package reference SHALL support versions from 8.0.0 up to but not including 11.0.0
2. THE Microsoft.Extensions.Logging.Abstractions package reference SHALL support versions from 8.0.0 up to but not including 11.0.0
3. WHEN a consuming project references .NET 9.x or 10.x packages, THE library SHALL resolve dependencies without conflicts
4. THE package version ranges SHALL use the format `[minimum,maximum)` for proper NuGet version resolution

### Requirement 2: Composite Entity DynamoDbMap Deserialization Fix

**User Story:** As a developer using composite entities, I want `[DynamoDbMap]` properties to deserialize correctly in multi-item scenarios, so that my nested objects are properly reconstructed.

#### Acceptance Criteria

1. WHEN the Source_Generator generates Multi_Item_FromDynamoDb code for an entity with DynamoDbMap_Property, THE generated code SHALL use the nested type's `FromDynamoDb` method
2. WHEN the Source_Generator generates Multi_Item_FromDynamoDb code for an entity with DynamoDbMap_Property, THE generated code SHALL NOT use `Enum.Parse` for map types
3. WHEN a Composite_Entity has a DynamoDbMap_Property, THE deserialization SHALL produce identical results to single-item deserialization
4. IF a DynamoDbMap_Property type has `[DynamoDbEntity]` attribute, THEN THE Source_Generator SHALL call `{TypeName}.FromDynamoDb<{TypeName}>(value.M, options)` in Multi_Item_FromDynamoDb
5. THE Source_Generator SHALL check the `ComplexType.IsMap` property when generating Multi_Item_FromDynamoDb property assignments

### Requirement 3: Nested Map of Different Record Types

**User Story:** As a developer, I want to use maps containing nested maps of different record types, so that I can model complex hierarchical data structures.

#### Acceptance Criteria

1. WHEN a DynamoDbMap_Property contains another DynamoDbMap_Property of a different type, THE Source_Generator SHALL generate correct serialization code
2. WHEN a DynamoDbMap_Property contains another DynamoDbMap_Property of a different type, THE Source_Generator SHALL generate correct deserialization code
3. THE nested map deserialization SHALL recursively call the appropriate `FromDynamoDb` method for each nested type
4. THE nested map serialization SHALL recursively call the appropriate `ToDynamoDb` method for each nested type

### Requirement 4: RelatedEntity Collection Warning Suppression

**User Story:** As a developer using composite entities, I want `[RelatedEntity]` collections to not trigger false performance warnings, so that I can use the intended API pattern without noise.

#### Acceptance Criteria

1. WHEN a property has `[RelatedEntity]` attribute, THE Source_Generator SHALL NOT report DYNDB023 performance warning for that property
2. THE Source_Generator SHALL recognize that RelatedEntity_Collection properties are intentionally designed for multi-item entity patterns
3. WHEN analyzing property performance characteristics, THE EntityAnalyzer SHALL skip properties marked with `[RelatedEntity]`
4. THE DYNDB023 warning SHALL continue to be reported for complex collection types that do NOT have `[RelatedEntity]` attribute

### Requirement 5: Central Package Management

**User Story:** As a library maintainer, I want to manage all package versions in a single location, so that version updates are easier and more consistent across all projects.

#### Acceptance Criteria

1. THE solution SHALL use NuGet Central Package Management via `Directory.Packages.props`
2. THE `Directory.Packages.props` file SHALL define all shared package versions in one location
3. WHEN a package version needs updating, THE change SHALL only require modification in `Directory.Packages.props`
4. THE individual `.csproj` files SHALL reference packages without specifying versions (using `<PackageReference Include="..." />`)
5. THE `Directory.Build.props` file SHALL enable Central Package Management with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`

### Requirement 6: Multi-Version Build Testing

**User Story:** As a library maintainer, I want to test builds against different package versions and .NET frameworks, so that I can ensure compatibility across the supported version range.

#### Acceptance Criteria

1. THE build system SHALL support testing against multiple .NET SDK versions
2. THE CI/CD pipeline SHOULD include matrix builds for different dependency versions
3. WHEN package version ranges are updated, THE test suite SHALL verify compatibility with minimum and maximum supported versions

---

## Future Improvements (Out of Scope)

### Hydration Code Consolidation

The source generator currently has separate code paths for single-item and multi-item deserialization:
- `GenerateFromDynamoDbSingleMethod` - handles single item deserialization with full property type support
- `GeneratePrimaryEntityIdentification` - handles multi-item deserialization but duplicates property handling logic

This duplication is the root cause of the DynamoDbMap bug (Requirement 2) - the multi-item path doesn't handle all the same property types as the single-item path.

**Recommended Future Refactoring:**
1. Extract property deserialization logic into a shared helper method
2. Have both single-item and multi-item paths call the same helper
3. This would prevent future bugs where one path handles a property type but the other doesn't

This refactoring is out of scope for this bug fix spec but should be considered for a future improvement to reduce maintenance burden and prevent similar bugs.
