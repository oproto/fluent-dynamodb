# Implementation Plan

- [x] 1. Phase 1: Project Configuration Fixes
  - [x] 1.1 Fix NuGet package version constraints
    - Update AWSSDK.KeyManagementService to version compatible with AWSSDK.Core 4.0.0
    - Or pin AWSSDK.Core to compatible version range
    - _Requirements: 8.1_
  - [x] 1.2 Disable AOT/trimming analysis for Source Generator project
    - Source generators run at compile time in Roslyn, not in user binaries - they don't need AOT compatibility
    - Add `<IsAotCompatible>false</IsAotCompatible>` and `<EnableTrimAnalyzer>false</EnableTrimAnalyzer>` to Oproto.FluentDynamoDb.SourceGenerator.csproj to override Directory.Build.props
    - This will eliminate NETSDK1210 warnings
    - _Requirements: 4.4_

- [x] 2. Phase 2: Source Generator Roslyn Analyzer Fixes
  - [x] 2.1 Fix RS2008 analyzer release tracking warnings
    - Add `AnalyzerReleaseTrackingAnalyzers` package or configure release tracking
    - Create AnalyzerReleases.Shipped.md and AnalyzerReleases.Unshipped.md files
    - _Requirements: 1.1_
  - [x] 2.2 Fix RS1032 diagnostic category warnings
    - Ensure all diagnostics have proper category assignments
    - _Requirements: 1.2_

- [x] 3. Checkpoint - Verify Phase 1-2 fixes
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Phase 3: Main Library AOT Fixes
  - [x] 4.1 Fix DynamoDbMappingException.ToString() override mismatch
    - Remove `RequiresDynamicCode` and `RequiresUnreferencedCode` attributes from ToString() override
    - Or restructure to avoid the override pattern
    - File: Oproto.FluentDynamoDb/Storage/DynamoDbMappingException.cs
    - _Requirements: 4.1_
  - [x] 4.2 Fix UpdateExpressionTranslator Array.CreateInstance usage
    - Replace `Array.CreateInstance(Type, Int32)` with AOT-compatible alternative
    - Consider using generic array creation or pre-defined array types
    - File: Oproto.FluentDynamoDb/Expressions/UpdateExpressionTranslator.cs line 1104
    - _Requirements: 4.2_
  - [x] 4.3 Add JSON source generation for SpatialContinuationToken
    - Create JsonSerializerContext with `[JsonSerializable(typeof(SpatialContinuationToken))]`
    - Update ToBase64() and FromBase64() to use source-generated serializer
    - File: Oproto.FluentDynamoDb.Geospatial/SpatialContinuationToken.cs
    - _Requirements: 4.1_
  - [x] 4.4 Add JSON source generation for SpatialQueryExtensions
    - Add `[JsonSerializable(typeof(Dictionary<string, object>))]` to context
    - Update SerializeLastEvaluatedKey() and DeserializeLastEvaluatedKey() to use source-generated serializer
    - File: Oproto.FluentDynamoDb.Geospatial/SpatialQueryExtensions.cs
    - _Requirements: 4.1_

- [x] 5. Checkpoint - Verify Main Library AOT fixes
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 6. Phase 4: Main Library Other Fixes
  - [ ] 6.1 Fix obsolete AesGcm constructor usage
    - Update to use AesGcm constructor that accepts tag size parameter
    - File: Oproto.FluentDynamoDb.IntegrationTests/Infrastructure/MockFieldEncryptor.cs
    - _Requirements: 6.1_

- [x] 7. Phase 5: Test Project Warning Suppressions
  - [x] 7.1 Suppress AOT/trimming warnings in test projects
    - Add `<NoWarn>$(NoWarn);IL2026;IL2060;IL2070;IL2072;IL2075;IL2090;IL3000;IL3050</NoWarn>` to test project PropertyGroups
    - Projects: UnitTests, SourceGenerator.UnitTests, IntegrationTests, Geospatial.UnitTests
    - _Requirements: 4.1, 4.2, 4.3_
  - [x] 7.2 Fix nullable reference type warnings in UnitTests
    - Fix CS8604 (possible null reference argument) warnings
    - Fix CS8625 (null literal to non-nullable) warnings
    - Fix CS8602 (dereference of possibly null) warnings
    - _Requirements: 2.1, 2.2, 2.3_
  - [x] 7.3 Fix nullable reference type warnings in IntegrationTests
    - Fix CS8602 dereference warnings
    - Fix CS8604 possible null argument warnings
    - _Requirements: 2.1, 2.3_
  - [x] 7.4 Fix nullable reference type warnings in ApiConsistencyTests
    - Fix CS8625 null literal warnings
    - Fix CS8618 uninitialized property warnings
    - _Requirements: 2.2, 2.4_
  - [x] 7.5 Fix nullable reference type warnings in Geospatial.UnitTests
    - Fix CS8600, CS8601, CS8602, CS8604 warnings
    - _Requirements: 2.1, 2.3, 2.5_
  - [x] 7.6 Fix async method warnings (CS1998)
    - Convert async methods without await to synchronous, or add await
    - Files in IntegrationTests with CS1998 warnings
    - _Requirements: 3.1_
  - [x] 7.7 Fix xUnit warnings
    - Fix xUnit1031 (blocking task operations) - convert to async/await
    - Fix xUnit1026 (unused theory parameters) - use or remove parameters
    - _Requirements: 3.2, 7.2_
  - [x] 7.8 Fix unused variable warnings (CS0219)
    - Remove or use assigned but unused variables
    - _Requirements: 7.1_

- [x] 8. Checkpoint - Verify Test Project fixes
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Phase 6: Custom Analyzer Warning Resolution
  - [x] 9.1 Address DYNDB021 reserved word warnings in test entities
    - Option A: Add explicit `[DynamoDbAttribute("different_name")]` attributes
    - Option B: Suppress with `#pragma warning disable DYNDB021` with comment
    - Files: Various test entities using Name, Status, Location, etc.
    - _Requirements: 5.1_
  - [x] 9.2 Address DYNDB023 complex type warnings in test entities
    - Suppress with `#pragma warning disable DYNDB023` for test entities
    - These are intentional test scenarios
    - _Requirements: 5.2_
  - [x] 9.3 Address SEC001 encryption warnings
    - Suppress with `#pragma warning disable SEC001` for test entities
    - Or add package reference if encryption testing is needed
    - _Requirements: 5.3_

- [x] 10. Phase 7: Source Generator Output Fixes
  - [x] 10.1 Fix nullable warnings in generated code
    - Update source generator templates to include proper nullable annotations
    - Fix CS8604 warnings in generated table classes (sortKeyValue parameters)
    - File: Oproto.FluentDynamoDb.SourceGenerator/Generators/*.cs
    - _Requirements: 9.1_
  - [x] 10.2 Fix generated EncryptedTestEntity warnings
    - Update generator to handle nullable string in Encoding.GetBytes
    - _Requirements: 9.1_

- [x] 11. Final Checkpoint - Verify all fixes
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet build 2>&1 | grep -c "warning"` to verify warning count < 100
