# Implementation Plan: Schema Version Attribute

## Overview

This plan implements a schema versioning mechanism for the source generator. It introduces a `FluentDynamoDbSchemaVersionAttribute` that consumers declare at assembly level, a detection/validation pipeline in the source generator, and diagnostic descriptors FDDB110–FDDB116. The implementation follows incremental steps: attribute class → value objects/constants → detection provider → diagnostics → generator integration → tests.

## Tasks

- [x] 1. Create the attribute class and supporting types
  - [x] 1.1 Create `FluentDynamoDbSchemaVersionAttribute` in `Oproto.FluentDynamoDb/Attributes/`
    - Create `FluentDynamoDbSchemaVersionAttribute.cs` as a sealed class inheriting from `System.Attribute`
    - Target `AttributeTargets.Assembly` with `AllowMultiple = false`
    - Accept constructor parameters `int major` and `int minor`
    - Expose `Major` and `Minor` as read-only int properties
    - Throw `ArgumentOutOfRangeException` when major < 1 or minor < 0
    - Include XML documentation consistent with other attributes in the project
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [x] 1.2 Create `SchemaVersion` value object in `Oproto.FluentDynamoDb.SourceGenerator/Models/`
    - Create `SchemaVersion.cs` as an `internal readonly struct`
    - Implement `IEquatable<SchemaVersion>` and `IComparable<SchemaVersion>`
    - Implement `CompareTo` with major-then-minor comparison
    - Implement `Equals`, `GetHashCode`, `ToString` (format: "major.minor")
    - Implement operator overloads: `<`, `>`, `<=`, `>=`, `==`, `!=`
    - _Requirements: 4.1, 5.1, 6.1_

  - [x] 1.3 Create `SchemaVersionConstants` in `Oproto.FluentDynamoDb.SourceGenerator/Models/`
    - Create `SchemaVersionConstants.cs` as an `internal static class`
    - Define `Current = new SchemaVersion(1, 0)`
    - Define `MinimumSupported = new SchemaVersion(1, 0)`
    - Define `Default = new SchemaVersion(1, 0)`
    - Define `MigrationGuideUrl` and `UpgradeGuideUrl` string constants
    - _Requirements: 2.3, 7.4_

- [x] 2. Implement diagnostic descriptors and schema version provider
  - [x] 2.1 Add diagnostic descriptors FDDB110–FDDB116 to `DiagnosticDescriptors.cs`
    - Add FDDB110 (Warning): Missing schema version attribute, defaulting to 1.0
    - Add FDDB111 (Error): Declared version below minimum supported
    - Add FDDB112 (Error): Declared version above current (unrecognized future version)
    - Add FDDB113 (Info): Older-but-supported version, upgrade available
    - Add FDDB114 (Error): Major version less than 1
    - Add FDDB115 (Error): Minor version less than 0
    - Add FDDB116 (Warning): Multiple attributes detected (IL manipulation)
    - Use `FluentDynamoDb` category, format help links via `DiagnosticHelpLinks.BaseUrlFormat`
    - Use message templates matching those defined in the design document
    - _Requirements: 3.1, 3.2, 4.1, 4.2, 5.1, 5.2, 6.1, 6.2, 9.1, 9.2, 9.4_

  - [x] 2.2 Create `SchemaVersionProvider` in `Oproto.FluentDynamoDb.SourceGenerator/Analysis/`
    - Create `SchemaVersionProvider.cs` as an `internal static class`
    - Define `DetectionResult` as an `internal readonly struct` with fields: `Version`, `Diagnostics`, `ShouldHaltGeneration`, `AttributeLocation`
    - Implement `Detect(Compilation compilation)` method
    - Scan `compilation.Assembly.GetAttributes()` for `FluentDynamoDbSchemaVersionAttribute` by fully-qualified name
    - Handle missing attribute: return Default version + FDDB110 warning
    - Handle multiple attributes: use first occurrence + FDDB116 warning
    - Validate major >= 1 (else FDDB114) and minor >= 0 (else FDDB115)
    - Compare against `SchemaVersionConstants.MinimumSupported` (FDDB111 if below)
    - Compare against `SchemaVersionConstants.Current` (FDDB112 if above)
    - Emit FDDB113 if version is >= MinimumSupported but < Current
    - Set `ShouldHaltGeneration = true` for any Error-severity diagnostic
    - Extract `AttributeLocation` from `AttributeData.ApplicationSyntaxReference`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 3.3, 3.4, 4.1, 4.3, 4.4, 4.5, 5.1, 5.3, 5.4, 5.5, 6.1, 6.3, 6.4, 9.1, 9.2, 9.3, 9.4, 9.5_

- [x] 3. Checkpoint - Ensure build succeeds
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Integrate schema version detection into the generator pipeline
  - [x] 4.1 Modify `DynamoDbSourceGenerator.Initialize` to include `CompilationProvider`
    - Add `context.CompilationProvider` to the incremental pipeline
    - Combine it with existing entity and projection inputs using `.Combine(compilationProvider)`
    - Update the `Execute` method signature to receive the compilation
    - _Requirements: 2.1_

  - [x] 4.2 Add schema version gate logic in `DynamoDbSourceGenerator.Execute`
    - Call `SchemaVersionProvider.Detect(compilation)` at the start of Execute
    - If `ShouldHaltGeneration` is true, report all diagnostics and return early (no code generation)
    - Otherwise report non-fatal diagnostics (FDDB110, FDDB113, FDDB116) and continue
    - Store the resolved `SchemaVersion` for future version-aware generation
    - _Requirements: 2.1, 3.4, 4.4, 5.4, 6.4, 7.1, 7.2, 7.3_

- [x] 5. Checkpoint - Ensure build succeeds and existing tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Unit tests for attribute and value object
  - [x] 6.1 Create `FluentDynamoDbSchemaVersionAttributeTests` in `Oproto.FluentDynamoDb.UnitTests/Attributes/`
    - Test attribute is sealed
    - Test attribute targets `Assembly` with `AllowMultiple = false`
    - Test constructor stores Major and Minor correctly for valid inputs
    - Test constructor throws `ArgumentOutOfRangeException` for major < 1
    - Test constructor throws `ArgumentOutOfRangeException` for minor < 0
    - Test namespace is `Oproto.FluentDynamoDb.Attributes`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

  - [x] 6.2 Create `SchemaVersionTests` in `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/`
    - Test `CompareTo` ordering: (1,0) < (1,1) < (2,0)
    - Test `Equals` for same and different values
    - Test `GetHashCode` consistency with Equals
    - Test `ToString` produces "major.minor" format
    - Test operator overloads: `<`, `>`, `<=`, `>=`, `==`, `!=`
    - _Requirements: 4.1, 5.1_

  - [x] 6.3 Create `SchemaVersionConstantsTests` in `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/`
    - Test `Current` equals (1,0)
    - Test `MinimumSupported` equals (1,0)
    - Test `Default` equals (1,0)
    - Test `MinimumSupported` <= `Current`
    - Test `MigrationGuideUrl` and `UpgradeGuideUrl` are non-empty valid URLs
    - _Requirements: 2.3, 7.4_

- [x] 7. Unit tests for SchemaVersionProvider
  - [x] 7.1 Create `SchemaVersionProviderTests` in `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/`
    - Test missing attribute returns Default version and FDDB110 warning
    - Test valid attribute (1,0) returns correct version with no error diagnostics
    - Test version below minimum returns FDDB111 error and ShouldHaltGeneration = true
    - Test version above current returns FDDB112 error and ShouldHaltGeneration = true
    - Test version equal to current returns version with no diagnostics
    - Test version between minimum and current returns FDDB113 info
    - Test major < 1 returns FDDB114 error
    - Test minor < 0 returns FDDB115 error
    - Test both major < 1 and minor < 0 returns both FDDB114 and FDDB115
    - Test multiple attributes returns first version + FDDB116 warning
    - Use Roslyn in-memory compilation to create test assemblies
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 4.1, 4.4, 4.5, 5.1, 5.4, 5.5, 6.1, 6.4, 9.1, 9.2, 9.3, 9.4, 9.5_

- [x] 8. Checkpoint - Ensure all unit tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Property-based tests for correctness properties
  - [x] 9.1 Write property test for constructor value round-trip
    - **Property 1: Constructor value round-trip**
    - **Validates: Requirements 1.3, 1.4**
    - For any valid major (>= 1) and minor (>= 0), constructing the attribute yields matching Major/Minor
    - Use FsCheck generators for (major in 1..1000, minor in 0..1000)
    - Create in `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/SchemaVersionAttributePropertyTests.cs`

  - [x] 9.2 Write property test for constructor invalid input rejection
    - **Property 2: Constructor invalid input rejection**
    - **Validates: Requirements 1.6**
    - For any major < 1 or minor < 0, constructing the attribute throws `ArgumentOutOfRangeException`
    - Use FsCheck generators for (major in int.MinValue..0) and (minor in int.MinValue..-1)
    - Add to `SchemaVersionAttributePropertyTests.cs`

  - [x] 9.3 Write property test for generator version extraction round-trip
    - **Property 3: Generator version extraction round-trip**
    - **Validates: Requirements 2.1, 2.2**
    - For any valid major/minor pair declared in an assembly-level attribute within a Roslyn compilation, `SchemaVersionProvider.Detect` returns matching version
    - Use in-memory Roslyn compilations with random valid versions
    - Create in `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/SchemaVersionDetectionPropertyTests.cs`

  - [x] 9.4 Write property test for missing attribute diagnostic exclusivity
    - **Property 4: Missing attribute diagnostic exclusivity**
    - **Validates: Requirements 3.1, 3.3**
    - For any compilation without the attribute, FDDB110 is emitted exactly once
    - For any compilation with the attribute, FDDB110 is NOT emitted
    - Add to `SchemaVersionDetectionPropertyTests.cs`

  - [x] 9.5 Write property test for unsupported old version halts generation
    - **Property 5: Unsupported old version halts generation**
    - **Validates: Requirements 4.1, 4.4, 4.5**
    - For any version < MinimumSupported, exactly one FDDB111 Error is emitted and ShouldHaltGeneration is true
    - Create in `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/SchemaVersionDiagnosticPropertyTests.cs`

  - [x] 9.6 Write property test for unrecognized future version halts generation
    - **Property 6: Unrecognized future version halts generation**
    - **Validates: Requirements 5.1, 5.4, 5.5**
    - For any version > Current, exactly one FDDB112 Error is emitted and ShouldHaltGeneration is true
    - Add to `SchemaVersionDiagnosticPropertyTests.cs`

  - [x] 9.7 Write property test for older-but-supported version emits info
    - **Property 7: Older-but-supported version emits info diagnostic**
    - **Validates: Requirements 6.1, 6.4**
    - For any version >= MinimumSupported and < Current, FDDB113 Info is emitted and generation proceeds
    - Add to `SchemaVersionDiagnosticPropertyTests.cs`

  - [x] 9.8 Write property test for invalid version validation halts generation
    - **Property 9: Invalid version validation halts generation**
    - **Validates: Requirements 9.1, 9.2, 9.3, 9.5**
    - For any major < 1 and/or minor < 0, appropriate FDDB114/FDDB115 diagnostics are emitted and ShouldHaltGeneration is true
    - Add to `SchemaVersionDiagnosticPropertyTests.cs`

- [x] 10. Integration tests for end-to-end generation scenarios
  - [x] 10.1 Create end-to-end test: generation with valid schema version attribute
    - Compile a test assembly with `[assembly: FluentDynamoDbSchemaVersion(1, 0)]` and a DynamoDB entity
    - Verify that entity source is generated successfully with no errors
    - Create in `Oproto.FluentDynamoDb.UnitTests/SourceGenerator/SchemaVersionIntegrationTests.cs`
    - _Requirements: 7.1_

  - [x] 10.2 Create end-to-end test: generation without schema version attribute
    - Compile a test assembly with a DynamoDB entity but no schema version attribute
    - Verify FDDB110 warning is emitted and entity code is still generated
    - Add to `SchemaVersionIntegrationTests.cs`
    - _Requirements: 3.1, 3.4_

  - [x] 10.3 Create end-to-end test: generation halted with unsupported version
    - Compile a test assembly with a schema version below minimum
    - Verify FDDB111 error is emitted and no entity source is generated
    - Add to `SchemaVersionIntegrationTests.cs`
    - _Requirements: 4.1, 4.4_

  - [x] 10.4 Create end-to-end test: generation halted with future version
    - Compile a test assembly with a schema version above current
    - Verify FDDB112 error is emitted and no entity source is generated
    - Add to `SchemaVersionIntegrationTests.cs`
    - _Requirements: 5.1, 5.4_

- [x] 11. Documentation and changelog updates
  - [x] 11.1 Create `docs/advanced-topics/SchemaVersioning.md` documentation page
    - Document the `FluentDynamoDbSchemaVersionAttribute` and its purpose
    - Include consumer usage examples (`[assembly: FluentDynamoDbSchemaVersion(1, 0)]`)
    - Document all diagnostic codes FDDB110–FDDB116 with severity, message, and resolution steps
    - Explain versioning semantics (major = breaking, minor = additive)
    - Explain the support window (N and N-1 major versions)
    - Provide migration guidance template for future version bumps
    - Include a "Getting Started" section recommending users add the attribute immediately
    - _Requirements: 3.2, 4.2, 5.2, 6.2, 8.1, 8.2, 8.3, 8.4_

  - [x] 11.2 Create per-code diagnostic pages in `docs/diagnostics/`
    - Create `docs/diagnostics/FDDB110.md` — Missing schema version attribute warning
    - Create `docs/diagnostics/FDDB111.md` — Unsupported old version error
    - Create `docs/diagnostics/FDDB112.md` — Unrecognized future version error
    - Create `docs/diagnostics/FDDB113.md` — Older-but-supported version info
    - Create `docs/diagnostics/FDDB114.md` — Invalid major version error
    - Create `docs/diagnostics/FDDB115.md` — Invalid minor version error
    - Create `docs/diagnostics/FDDB116.md` — Duplicate attribute warning
    - Each page includes: code, severity, message format, description, triggering example, fix example
    - Update `docs/diagnostics/README.md` index to include the new FDDB110–FDDB116 entries
    - _Requirements: 3.2, 4.2, 5.2, 6.2, 9.1, 9.2, 9.4_

  - [x] 11.3 Update `docs/DOCUMENTATION_CHANGELOG.md`
    - Add entry dated with the implementation date
    - Category: "New Feature Documentation"
    - Reference the new `docs/advanced-topics/SchemaVersioning.md` file
    - Document the new diagnostic codes FDDB110–FDDB116
    - Include before/after examples showing usage without and with the attribute
    - Reason: New schema versioning mechanism for graceful generated code evolution

  - [x] 11.4 Update `CHANGELOG.md` under `[Unreleased]` → `### Added`
    - Add entry for `FluentDynamoDbSchemaVersionAttribute` assembly-level attribute
    - Document consumer usage pattern
    - Document diagnostic codes FDDB110–FDDB116 with brief descriptions
    - Document versioning semantics (schema version independent of NuGet version)
    - Document the support window policy (N and N-1 major versions)

  - [x] 11.5 Update `docs/reference/AttributeReference.md` (if it exists)
    - Add `[FluentDynamoDbSchemaVersion(major, minor)]` to the attribute reference
    - Include target (Assembly), AllowMultiple (false), and parameter descriptions

- [x] 12. Final checkpoint - Ensure all tests pass and documentation is complete
  - Ensure all tests pass, ask the user if questions arise.
  - Verify documentation files are well-formed markdown
  - Verify CHANGELOG entry follows Keep a Changelog format

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The source generator project must be rebuilt with `dotnet build-server shutdown` after modifications
- Integration tests use Roslyn in-memory compilation (no file I/O) consistent with existing test patterns
- The test project already references FsCheck.Xunit, Microsoft.CodeAnalysis.CSharp, and the source generator project

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "6.1", "6.2", "6.3"] },
    { "id": 2, "tasks": ["2.2"] },
    { "id": 3, "tasks": ["4.1"] },
    { "id": 4, "tasks": ["4.2", "7.1"] },
    { "id": 5, "tasks": ["9.1", "9.2", "9.3", "9.4", "9.5", "9.6", "9.7", "9.8"] },
    { "id": 6, "tasks": ["10.1", "10.2", "10.3", "10.4"] },
    { "id": 7, "tasks": ["11.1", "11.2", "11.3", "11.4", "11.5"] }
  ]
}
```
