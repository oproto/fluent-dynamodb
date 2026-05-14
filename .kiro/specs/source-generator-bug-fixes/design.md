# Design Document

## Overview

This design addresses multiple bugs and improvements in the Oproto.FluentDynamoDb library:

1. **Package Version Constraints**: Update Microsoft .NET package version ranges to support .NET 10.x
2. **Central Package Management**: Implement NuGet CPM for easier version management
3. **Composite Entity DynamoDbMap Bug**: Fix source generator to correctly deserialize `[DynamoDbMap]` properties in multi-item scenarios
4. **RelatedEntity Warning Suppression**: Prevent false DYNDB023 warnings for `[RelatedEntity]` collections
5. **Nested Map Support**: Ensure maps containing nested maps of different types work correctly

## Architecture

### Package Version Management

```
Directory.Build.props
├── ManagePackageVersionsCentrally = true
└── Common build settings

Directory.Packages.props
├── PackageVersion: AWSSDK.DynamoDBv2 [4.0.0,5.0.0)
├── PackageVersion: System.Text.Json [8.0.0,11.0.0)
├── PackageVersion: Microsoft.Extensions.Logging.Abstractions [8.0.0,11.0.0)
├── PackageVersion: xunit 2.9.3
├── PackageVersion: FluentAssertions 8.x
└── ... other shared packages

Individual .csproj files
└── PackageReference Include="..." (no Version attribute)
```

### Source Generator Fix Architecture

The fix targets `MapperGenerator.cs` in the `GeneratePrimaryEntityIdentification` method:

```
MapperGenerator.cs
├── GenerateFromDynamoDbSingleMethod()     ← Handles all property types correctly
│   └── Uses GetFromAttributeValueExpression() with ComplexType checks
│
└── GenerateFromDynamoDbMultiMethod()
    └── GenerateMultiItemFromDynamoDb()
        └── GeneratePrimaryEntityIdentification()  ← BUG: Missing ComplexType.IsMap check
            └── Currently uses GetFromAttributeValueExpression() directly
            └── FIX: Add ComplexType.IsMap check before property assignment
```

### EntityAnalyzer Warning Fix

```
EntityAnalyzer.cs
└── CheckPropertyPerformance()
    └── IsComplexCollectionType check
        └── FIX: Skip if property has [RelatedEntity] attribute
```

## Components and Interfaces

### Modified Files

| File | Change Type | Description |
|------|-------------|-------------|
| `Directory.Build.props` | Modify | Add `ManagePackageVersionsCentrally` |
| `Directory.Packages.props` | Create | Central package version definitions |
| `*.csproj` (all projects) | Modify | Remove Version attributes from PackageReference |
| `MapperGenerator.cs` | Modify | Fix `GeneratePrimaryEntityIdentification` for DynamoDbMap |
| `EntityAnalyzer.cs` | Modify | Skip DYNDB023 for RelatedEntity properties |
| `PropertyModel.cs` | Modify | Add `IsRelatedEntity` flag |

### Package Version Ranges

| Package | Current | New |
|---------|---------|-----|
| System.Text.Json | `[8.0.5,9.0.0)` | `[8.0.0,11.0.0)` |
| Microsoft.Extensions.Logging.Abstractions | `[8.0.0,10.0.0)` | `[8.0.0,11.0.0)` |

## Data Models

### PropertyModel Enhancement

```csharp
internal class PropertyModel
{
    // Existing properties...
    
    /// <summary>
    /// Gets or sets a value indicating whether this property has [RelatedEntity] attribute.
    /// Used to suppress DYNDB023 performance warnings for intentional composite entity patterns.
    /// </summary>
    public bool IsRelatedEntity { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: DynamoDbMap Multi-Item Deserialization Correctness

*For any* entity with a `[DynamoDbMap]` property and `[RelatedEntity]` attribute, the generated multi-item `FromDynamoDb` code SHALL use the nested type's `FromDynamoDb` method (not `Enum.Parse`) and produce identical results to single-item deserialization.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**

### Property 2: Nested Map Round-Trip Consistency

*For any* valid nested map structure (a `[DynamoDbMap]` property containing another `[DynamoDbMap]` property of a different type), serializing to DynamoDB format and deserializing back SHALL produce an equivalent object.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

### Property 3: RelatedEntity Warning Suppression

*For any* property with `[RelatedEntity]` attribute, the source generator SHALL NOT emit DYNDB023 performance warning, regardless of the collection's element type.

**Validates: Requirements 4.1, 4.3**

### Property 4: Non-RelatedEntity Warning Preservation

*For any* complex collection property WITHOUT `[RelatedEntity]` attribute, the source generator SHALL continue to emit DYNDB023 performance warning as before.

**Validates: Requirements 4.4**

## Error Handling

### Build Errors

If Central Package Management is enabled but a `.csproj` still specifies a Version attribute, NuGet will report:
```
error NU1008: Projects that use central package version management should not define the version on the PackageReference items
```

### Source Generator Diagnostics

No new diagnostics are added. Existing DYNDB023 behavior is modified to exclude `[RelatedEntity]` properties.

## Testing Strategy

### Unit Tests

1. **Package Configuration Tests** (example-based)
   - Verify `Directory.Packages.props` exists and contains expected packages
   - Verify `Directory.Build.props` enables CPM
   - Verify `.csproj` files don't specify versions

2. **Source Generator Tests** (property-based)
   - Test `GeneratePrimaryEntityIdentification` generates correct code for DynamoDbMap properties
   - Test EntityAnalyzer skips DYNDB023 for RelatedEntity properties
   - Test EntityAnalyzer still reports DYNDB023 for non-RelatedEntity complex collections

### Property-Based Testing

Using FsCheck for C# property-based testing:

```csharp
// Property 1: DynamoDbMap multi-item deserialization
[Property]
public Property DynamoDbMap_MultiItem_UsesFromDynamoDb()
{
    return Prop.ForAll(
        Arb.From<EntityWithDynamoDbMapAndRelatedEntity>(),
        entity => {
            var generatedCode = GenerateMultiItemFromDynamoDb(entity);
            return !generatedCode.Contains("Enum.Parse") &&
                   generatedCode.Contains(".FromDynamoDb<");
        });
}

// Property 3: RelatedEntity suppresses DYNDB023
[Property]
public Property RelatedEntity_SuppressesDYNDB023()
{
    return Prop.ForAll(
        Arb.From<PropertyWithRelatedEntityAttribute>(),
        property => {
            var diagnostics = AnalyzeProperty(property);
            return !diagnostics.Any(d => d.Id == "DYNDB023");
        });
}
```

### Integration Tests

1. Create test entity with `[DynamoDbMap]` and `[RelatedEntity]`
2. Verify generated code compiles
3. Verify deserialization produces correct results
4. Verify no DYNDB023 warning is emitted

### Test Configuration

- Minimum 100 iterations per property test
- Tag format: **Feature: source-generator-bug-fixes, Property {number}: {property_text}**
