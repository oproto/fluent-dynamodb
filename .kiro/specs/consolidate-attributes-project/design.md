# Design Document: Consolidate Attributes Project

## Overview

This design outlines the approach for consolidating the `Oproto.FluentDynamoDb.Attributes` project into the main `Oproto.FluentDynamoDb` library. The consolidation is now feasible because the source generator uses syntax-based attribute matching rather than semantic model resolution, eliminating the need for a separate attributes assembly.

The consolidation will:
- Move 27 attribute files from the separate project into the main library
- Remove the Attributes project and its test project from the solution
- Update all project references and build configurations
- Maintain backward compatibility for library users
- Simplify the build process and project structure

## Architecture

### Current Architecture

```
Oproto.FluentDynamoDb.sln
├── Oproto.FluentDynamoDb/                    (Main library, net8.0)
│   └── References: Oproto.FluentDynamoDb.Attributes
├── Oproto.FluentDynamoDb.Attributes/         (Attributes, netstandard2.0)
├── Oproto.FluentDynamoDb.Attributes.UnitTests/
├── Oproto.FluentDynamoDb.SourceGenerator/    (Source generator, netstandard2.0)
│   ├── References: Oproto.FluentDynamoDb.Attributes
│   └── Custom MSBuild targets to copy Attributes.dll
└── Oproto.FluentDynamoDb.UnitTests/
    └── Includes attribute source files in compilation
```

### Target Architecture

```
Oproto.FluentDynamoDb.sln
├── Oproto.FluentDynamoDb/                    (Main library, net8.0)
│   └── Attributes/                           (Moved from separate project)
│       ├── DynamoDbTableAttribute.cs
│       ├── PartitionKeyAttribute.cs
│       └── ... (25 more files)
├── Oproto.FluentDynamoDb.SourceGenerator/    (Source generator, netstandard2.0)
│   └── No references to Attributes project
└── Oproto.FluentDynamoDb.UnitTests/
    ├── Attributes/                           (Moved from Attributes.UnitTests)
    └── No attribute source files in compilation
```

## Components and Interfaces

### 1. Attribute Files Migration

**Source Location:** `Oproto.FluentDynamoDb.Attributes/*.cs`  
**Target Location:** `Oproto.FluentDynamoDb/Attributes/*.cs`

**Files to Move (27 total):**
- AccessModifier.cs
- BlobProvider.cs
- BlobReferenceAttribute.cs
- ComputedAttribute.cs
- DynamoDbAttributeAttribute.cs
- DynamoDbEntityAttribute.cs
- DynamoDbJsonSerializerAttribute.cs
- DynamoDbMapAttribute.cs
- DynamoDbProjectionAttribute.cs
- DynamoDbTableAttribute.cs
- EncryptedAttribute.cs
- ExtractedAttribute.cs
- GenerateAccessorsAttribute.cs
- GenerateEntityPropertyAttribute.cs
- GlobalSecondaryIndexAttribute.cs
- JsonBlobAttribute.cs
- JsonSerializerType.cs
- PartitionKeyAttribute.cs
- QueryableAttribute.cs
- RelatedEntityAttribute.cs
- ScannableAttribute.cs
- SensitiveAttribute.cs
- SortKeyAttribute.cs
- TableOperation.cs
- TimeToLiveAttribute.cs
- UseProjectionAttribute.cs

**Namespace:** All files will maintain `namespace Oproto.FluentDynamoDb.Attributes;`

### 2. Project File Updates

#### Oproto.FluentDynamoDb.csproj

**Changes:**
- Remove `<ProjectReference>` to `Oproto.FluentDynamoDb.Attributes`
- No other changes needed (attributes will be part of the main assembly)

#### Oproto.FluentDynamoDb.SourceGenerator.csproj

**Changes:**
- Remove `<ProjectReference>` to `Oproto.FluentDynamoDb.Attributes`
- Remove `<Target Name="CopyAttributesAssembly">` MSBuild target
- Remove `<None Include="$(OutputPath)Oproto.FluentDynamoDb.Attributes.dll">` packaging configuration

### 3. Test Migration

**Source:** `Oproto.FluentDynamoDb.Attributes.UnitTests/`  
**Target:** `Oproto.FluentDynamoDb.UnitTests/Attributes/`

**Test Files to Move:**
- All test files from the Attributes.UnitTests project
- Update namespaces from `Oproto.FluentDynamoDb.Attributes.UnitTests` to `Oproto.FluentDynamoDb.UnitTests.Attributes`

#### Oproto.FluentDynamoDb.UnitTests.csproj

**Changes:**
- Remove any `<Compile Include>` directives that include attribute source files from the Attributes project
- Tests will reference attributes from the main library assembly

### 4. Solution File Updates

**Changes to Oproto.FluentDynamoDb.sln:**
- Remove `Oproto.FluentDynamoDb.Attributes` project entry
- Remove `Oproto.FluentDynamoDb.Attributes.UnitTests` project entry
- Update project dependencies to remove references to these projects

### 5. Example Project Updates

#### examples/BasicUsage/BasicUsage.csproj

**Current Configuration:**
```xml
<ProjectReference Include="../../Oproto.FluentDynamoDb/Oproto.FluentDynamoDb.csproj" />
<ProjectReference Include="../../Oproto.FluentDynamoDb.Attributes/Oproto.FluentDynamoDb.Attributes.csproj" />
<ProjectReference Include="../../Oproto.FluentDynamoDb.SourceGenerator/Oproto.FluentDynamoDb.SourceGenerator.csproj" 
                  OutputItemType="Analyzer" 
                  ReferenceOutputAssembly="false" />
```

**Target Configuration:**
```xml
<ProjectReference Include="../../Oproto.FluentDynamoDb/Oproto.FluentDynamoDb.csproj" />
<ProjectReference Include="../../Oproto.FluentDynamoDb.SourceGenerator/Oproto.FluentDynamoDb.SourceGenerator.csproj" 
                  OutputItemType="Analyzer" 
                  ReferenceOutputAssembly="false" />
```

## Data Models

### Attribute Class Structure

All attribute classes will remain unchanged in their implementation. They will simply be relocated to the main library with the same:
- Class names
- Namespaces (`Oproto.FluentDynamoDb.Attributes`)
- Public APIs
- Attribute usage declarations

### Target Framework Considerations

**Main Library:** net8.0  
**Attributes (after move):** Will be compiled as part of net8.0 assembly

**Note:** The attributes were previously compiled for netstandard2.0 for broader compatibility. After consolidation, they will be part of the net8.0 assembly. This is acceptable because:
1. The library already targets net8.0
2. Users of the library must be on .NET 8.0 or later
3. The source generator uses syntax-based matching and doesn't load the assembly

## Error Handling

### Build Errors

**Potential Issue:** Projects referencing the old Attributes assembly  
**Solution:** Update all project references before building

**Potential Issue:** Cached build artifacts  
**Solution:** Clean solution before building (`dotnet clean`)

### Runtime Errors

**Potential Issue:** NuGet package consumers expecting separate Attributes package  
**Solution:** 
- Mark `Oproto.FluentDynamoDb.Attributes` package as deprecated
- Add package dependency from Attributes to main library for transition period
- Document migration in release notes

## Testing Strategy

### Unit Tests

1. **Attribute Tests**
   - Move all tests from `Oproto.FluentDynamoDb.Attributes.UnitTests` to `Oproto.FluentDynamoDb.UnitTests/Attributes/`
   - Update test namespaces
   - Verify all tests pass after migration

2. **Source Generator Tests**
   - Verify source generator tests in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests` still pass
   - Ensure tests don't include attribute source files in compilation context
   - Validate syntax-based attribute matching works correctly

3. **Integration Tests**
   - Build and run BasicUsage example project
   - Verify generated code is identical to pre-consolidation output
   - Test that attributes are properly recognized by the source generator

### Build Verification

1. **Clean Build Test**
   ```bash
   dotnet clean
   dotnet build
   ```
   - Verify no build errors
   - Verify no warnings about missing assemblies

2. **Package Build Test**
   ```bash
   dotnet pack
   ```
   - Verify NuGet package contains attributes in main assembly
   - Verify source generator package doesn't include separate Attributes.dll

3. **Example Project Test**
   ```bash
   dotnet build examples/BasicUsage
   dotnet run --project examples/BasicUsage
   ```
   - Verify example builds and runs successfully

## Migration Steps

### Phase 1: Prepare Main Library

1. Create `Oproto.FluentDynamoDb/Attributes/` directory
2. Copy all 27 attribute files from `Oproto.FluentDynamoDb.Attributes/` to `Oproto.FluentDynamoDb/Attributes/`
3. Verify all files maintain `namespace Oproto.FluentDynamoDb.Attributes;`

### Phase 2: Update Main Library Project

1. Remove `<ProjectReference>` to `Oproto.FluentDynamoDb.Attributes` from `Oproto.FluentDynamoDb.csproj`
2. Build main library to verify attributes compile correctly

### Phase 3: Update Source Generator

1. Remove `<ProjectReference>` to `Oproto.FluentDynamoDb.Attributes` from `Oproto.FluentDynamoDb.SourceGenerator.csproj`
2. Remove `<Target Name="CopyAttributesAssembly">` MSBuild target
3. Remove `<None Include="$(OutputPath)Oproto.FluentDynamoDb.Attributes.dll">` packaging configuration
4. Build source generator to verify it compiles without the Attributes reference

### Phase 4: Migrate Tests

1. Create `Oproto.FluentDynamoDb.UnitTests/Attributes/` directory
2. Copy test files from `Oproto.FluentDynamoDb.Attributes.UnitTests/` to `Oproto.FluentDynamoDb.UnitTests/Attributes/`
3. Update test namespaces from `Oproto.FluentDynamoDb.Attributes.UnitTests` to `Oproto.FluentDynamoDb.UnitTests.Attributes`
4. Remove any `<Compile Include>` directives for attribute source files from `Oproto.FluentDynamoDb.UnitTests.csproj`
5. Run tests to verify they pass

### Phase 5: Update Solution and Examples

1. Remove `Oproto.FluentDynamoDb.Attributes` project from solution file
2. Remove `Oproto.FluentDynamoDb.Attributes.UnitTests` project from solution file
3. Update `examples/BasicUsage/BasicUsage.csproj` to remove Attributes project reference
4. Update any other example projects similarly

### Phase 6: Clean Up

1. Delete `Oproto.FluentDynamoDb.Attributes/` directory
2. Delete `Oproto.FluentDynamoDb.Attributes.UnitTests/` directory
3. Clean and rebuild entire solution
4. Run all tests
5. Build and test example projects

### Phase 7: Verification

1. Verify all projects build successfully
2. Verify all tests pass
3. Verify example projects run correctly
4. Verify NuGet packages are generated correctly
5. Verify no orphaned Attributes.dll files in output directories

## Backward Compatibility

### For Library Users

**No Breaking Changes:**
- Attributes remain in the same namespace: `Oproto.FluentDynamoDb.Attributes`
- All attribute APIs remain unchanged
- Existing code will compile without modifications

**NuGet Package Changes:**
- Attributes are now included in the main `Oproto.FluentDynamoDb` package
- The separate `Oproto.FluentDynamoDb.Attributes` package can be deprecated
- Users who explicitly reference the Attributes package should be guided to remove that reference

### For Source Generator

**No Changes Required:**
- Source generator uses syntax-based matching
- Attribute detection works identically before and after consolidation
- Generated code output remains the same

## Performance Considerations

### Build Performance

**Expected Improvement:**
- Fewer projects to build (2 fewer projects)
- No MSBuild target overhead for copying Attributes.dll
- Simpler dependency graph

### Runtime Performance

**No Impact:**
- Attributes are compile-time only
- No runtime performance difference

## Security Considerations

**No Security Impact:**
- Attributes are metadata only
- No security-sensitive code in attributes
- Same security posture before and after consolidation

## Documentation Updates

### README.md

Update to reflect:
- Single package installation (`Oproto.FluentDynamoDb` only)
- Remove references to separate Attributes package

### Release Notes

Document:
- Attributes consolidated into main library
- Separate Attributes package deprecated
- Migration guide for users explicitly referencing Attributes package

### API Documentation

**No Changes Required:**
- Attribute APIs remain unchanged
- Namespace remains the same
