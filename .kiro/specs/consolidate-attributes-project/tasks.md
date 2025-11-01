# Implementation Plan

## Overview
This plan consolidates the `Oproto.FluentDynamoDb.Attributes` project into the main `Oproto.FluentDynamoDb` library. The consolidation is now possible because the source generator uses syntax-based attribute matching rather than semantic model resolution.

---

- [x] 1. Move attribute files to main library
  - Create `Oproto.FluentDynamoDb/Attributes/` directory
  - Copy all 26 attribute class files from `Oproto.FluentDynamoDb.Attributes/` to `Oproto.FluentDynamoDb/Attributes/`
  - Verify all files maintain `namespace Oproto.FluentDynamoDb.Attributes;`
  - _Requirements: 1.1, 1.3_

- [x] 2. Update main library project file
  - Remove `<ProjectReference>` to `Oproto.FluentDynamoDb.Attributes` from `Oproto.FluentDynamoDb.csproj`
  - Build main library to verify attributes compile correctly
  - _Requirements: 1.4, 4.4_

- [x] 3. Update source generator project file
  - Remove `<ProjectReference>` to `Oproto.FluentDynamoDb.Attributes` from `Oproto.FluentDynamoDb.SourceGenerator.csproj`
  - Remove `<Target Name="CopyAttributesAssembly">` MSBuild target
  - Remove `<None Include="$(OutputPath)Oproto.FluentDynamoDb.Attributes.dll">` packaging configuration
  - Build source generator to verify it compiles without the Attributes reference
  - _Requirements: 2.1, 2.2, 2.4, 6.1, 6.3_

- [x] 4. Migrate attribute tests to main test project
  - Create `Oproto.FluentDynamoDb.UnitTests/Attributes/` directory
  - Copy all 7 test files from `Oproto.FluentDynamoDb.Attributes.UnitTests/` to `Oproto.FluentDynamoDb.UnitTests/Attributes/`
  - Update test namespaces from `Oproto.FluentDynamoDb.Attributes.UnitTests` to `Oproto.FluentDynamoDb.UnitTests.Attributes`
  - Run tests to verify they pass with the new structure
  - _Requirements: 3.1, 3.3_

- [x] 5. Update example project references
  - Remove `<ProjectReference>` to `Oproto.FluentDynamoDb.Attributes` from `examples/BasicUsage/BasicUsage.csproj`
  - Build BasicUsage example to verify it compiles correctly
  - _Requirements: 4.3, 4.4_

- [x] 6. Update solution file
  - Remove `Oproto.FluentDynamoDb.Attributes` project entry from `Oproto.FluentDynamoDb.sln`
  - Remove `Oproto.FluentDynamoDb.Attributes.UnitTests` project entry from solution file
  - _Requirements: 4.1_

- [x] 7. Clean up old projects
  - Delete `Oproto.FluentDynamoDb.Attributes/` directory and all its contents
  - Delete `Oproto.FluentDynamoDb.Attributes.UnitTests/` directory and all its contents
  - _Requirements: 1.2, 3.2_

- [x] 8. Verify complete solution build
  - Run `dotnet clean` on entire solution
  - Run `dotnet build` on entire solution
  - Verify no build errors or warnings about missing assemblies
  - Verify no orphaned Attributes.dll files in output directories
  - _Requirements: 4.4, 6.2, 6.4_

- [x] 9. Run comprehensive test suite
  - Run all unit tests in `Oproto.FluentDynamoDb.UnitTests`
  - Run all source generator tests in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests`
  - Verify all tests pass
  - _Requirements: 3.4, 5.4_

- [ ] 10. Verify example projects
  - Build and run `examples/BasicUsage` project
  - Verify generated code is identical to pre-consolidation output
  - Verify attributes are properly recognized by the source generator
  - _Requirements: 5.1, 5.2, 5.3, 5.4_

- [ ] 11. Verify NuGet package generation
  - Run `dotnet pack` on main library
  - Verify NuGet package contains attributes in the main assembly
  - Verify source generator package doesn't include separate Attributes.dll
  - _Requirements: 5.2, 6.2, 6.3_
