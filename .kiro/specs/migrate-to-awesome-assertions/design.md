# Design Document

## Overview

This design outlines the migration strategy from FluentAssertions to AwesomeAssertions 9.3.0 across all test projects in the solution. The migration involves updating NuGet package references in 9 test projects and updating namespace declarations in GlobalUsings.cs files. The API surface remains identical, so no test code changes are required beyond namespace updates.

## Architecture

### Affected Projects

The solution contains 9 test projects that require migration:

**Unit Test Projects:**
1. `Oproto.FluentDynamoDb.UnitTests`
2. `Oproto.FluentDynamoDb.SourceGenerator.UnitTests`
3. `Oproto.FluentDynamoDb.BlobStorage.S3.UnitTests`
4. `Oproto.FluentDynamoDb.Encryption.Kms.UnitTests`
5. `Oproto.FluentDynamoDb.FluentResults.UnitTests`
6. `Oproto.FluentDynamoDb.Logging.Extensions.UnitTests`
7. `Oproto.FluentDynamoDb.NewtonsoftJson.UnitTests`
8. `Oproto.FluentDynamoDb.SystemTextJson.UnitTests`

**Integration Test Projects:**
9. `Oproto.FluentDynamoDb.IntegrationTests`

### Migration Scope

**Package References:**
- Current: `FluentAssertions` version 6.12.0 or 6.12.1
- Target: `AwesomeAssertions` version 9.3.0

**Namespace References:**
- Current: `FluentAssertions` and sub-namespaces
- Target: `AwesomeAssertions` and equivalent sub-namespaces

**Files Requiring Updates:**
- 9 `.csproj` files (package references)
- 8 `GlobalUsings.cs` files (namespace declarations)
- Note: `Oproto.FluentDynamoDb.BlobStorage.S3.UnitTests/GlobalUsings.cs` does not currently import FluentAssertions

## Components and Interfaces

### Package Reference Updates

Each test project's `.csproj` file contains a `PackageReference` element that must be updated:

```xml
<!-- Before -->
<PackageReference Include="FluentAssertions" Version="6.12.1" />

<!-- After -->
<PackageReference Include="AwesomeAssertions" Version="9.3.0" />
```

### GlobalUsings.cs Updates

Eight GlobalUsings.cs files contain `global using FluentAssertions;` statements that must be updated:

```csharp
// Before
global using FluentAssertions;

// After
global using AwesomeAssertions;
```

### Namespace Mapping

AwesomeAssertions 9.0+ maintains API compatibility but renames namespaces:

| FluentAssertions Namespace | AwesomeAssertions Namespace |
|---------------------------|----------------------------|
| `FluentAssertions` | `AwesomeAssertions` |
| `FluentAssertions.Execution` | `AwesomeAssertions.Execution` |
| `FluentAssertions.Extensions` | `AwesomeAssertions.Extensions` |
| `FluentAssertions.Primitives` | `AwesomeAssertions.Primitives` |

Based on the codebase analysis, only the root `FluentAssertions` namespace is currently used via global usings.

## Data Models

### Project File Structure

Each `.csproj` file follows the standard MSBuild format:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <IsTestProject>true</IsTestProject>
    </PropertyGroup>
    
    <ItemGroup>
        <PackageReference Include="AwesomeAssertions" Version="9.3.0" />
        <!-- Other packages -->
    </ItemGroup>
</Project>
```

### GlobalUsings.cs Structure

Each GlobalUsings.cs file contains global using directives:

```csharp
global using Xunit;
global using AwesomeAssertions;
// Other global usings specific to the project
```

## Error Handling

### Compilation Errors

**Scenario:** Missing namespace after package update
- **Cause:** Package restored but namespace not updated
- **Resolution:** Update all GlobalUsings.cs files with new namespace
- **Detection:** Build will fail with namespace not found errors

**Scenario:** API incompatibility
- **Cause:** AwesomeAssertions API differs from FluentAssertions
- **Resolution:** Review AwesomeAssertions documentation for API changes
- **Detection:** Build will fail with method not found errors
- **Likelihood:** Low - AwesomeAssertions maintains API compatibility

### Package Restore Errors

**Scenario:** AwesomeAssertions package not found
- **Cause:** NuGet source configuration issue
- **Resolution:** Verify NuGet.org is configured as a package source
- **Detection:** Package restore will fail with package not found error

## Testing Strategy

### Verification Steps

1. **Build Verification**
   - Build all test projects after migration
   - Verify no compilation errors
   - Confirm all projects target .NET 8.0

2. **Test Execution**
   - Run all unit tests: `dotnet test`
   - Verify all tests pass with same results as before migration
   - Check for any runtime assertion errors

3. **Package Verification**
   - Verify FluentAssertions is not referenced in any test project
   - Confirm AwesomeAssertions 9.3.0 is referenced in all test projects
   - Check that no transitive dependencies pull in FluentAssertions

### Rollback Strategy

If issues are encountered:
1. Revert `.csproj` changes to restore FluentAssertions references
2. Revert GlobalUsings.cs changes to restore FluentAssertions namespaces
3. Run `dotnet restore` to restore original packages
4. Investigate compatibility issues before reattempting migration

## Implementation Notes

### Order of Operations

The migration must follow this sequence to avoid broken builds:

1. Update all `.csproj` files with AwesomeAssertions package reference
2. Remove FluentAssertions package references from all `.csproj` files
3. Update all GlobalUsings.cs files with AwesomeAssertions namespace
4. Run `dotnet restore` to fetch new packages
5. Build and test to verify migration

### Atomic Changes

All changes should be made atomically (in a single commit) to avoid intermediate broken states where:
- Packages are updated but namespaces are not
- Namespaces are updated but packages are not

### Documentation Updates

The `tech.md` steering file should be updated to reflect the new assertion library:

```markdown
## Testing Framework
- **xUnit**: Primary testing framework
- **AwesomeAssertions**: For readable test assertions (version 9.3.0+)
- **NSubstitute**: Mocking framework for unit tests
- **Coverlet**: Code coverage collection
```

## Design Decisions

### Why AwesomeAssertions?

AwesomeAssertions is a fork of FluentAssertions created in response to license changes. Version 9.3.0 provides:
- Identical API surface to FluentAssertions
- Preferred licensing terms
- Active maintenance and updates
- Drop-in replacement requiring only namespace changes

### Version Selection

Version 9.3.0 was chosen because:
- It's the latest stable release
- Namespace changes are complete (introduced in 9.0)
- Maintains full API compatibility with FluentAssertions 6.x
- Supports .NET 8.0

### Scope Limitation

The migration is limited to test projects only because:
- Assertion libraries are only used in test code
- Main library projects do not reference FluentAssertions
- Minimizes risk and scope of changes
