# Implementation Plan: Source Generator Bug Fixes

## Overview

This implementation plan addresses package version management, source generator bugs, and warning suppression in a systematic order that allows incremental validation.

## Tasks

- [ ] 1. Set up Central Package Management
  - [ ] 1.1 Create Directory.Packages.props with all shared package versions
    - Extract all PackageReference versions from existing .csproj files
    - Define PackageVersion elements for each shared package
    - Use version ranges `[8.0.0,11.0.0)` for System.Text.Json and Microsoft.Extensions packages
    - _Requirements: 1.1, 1.2, 1.4, 5.1, 5.2_
  - [ ] 1.2 Update Directory.Build.props to enable Central Package Management
    - Add `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`
    - _Requirements: 5.5_
  - [ ] 1.3 Update all .csproj files to remove Version attributes
    - Remove Version attribute from all PackageReference elements
    - Keep Include attribute only
    - _Requirements: 5.4_
  - [ ] 1.4 Verify build succeeds with Central Package Management
    - Run `dotnet build` to verify all projects compile
    - _Requirements: 1.3_

- [ ] 2. Checkpoint - Verify Central Package Management
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 3. Fix RelatedEntity warning suppression
  - [ ] 3.1 Add IsRelatedEntity flag to PropertyModel
    - Add `public bool IsRelatedEntity { get; set; }` property
    - Add XML documentation
    - _Requirements: 4.1_
  - [ ] 3.2 Update EntityAnalyzer to set IsRelatedEntity flag
    - In property analysis, check for RelatedEntityAttribute
    - Set IsRelatedEntity = true when attribute is present
    - _Requirements: 4.1, 4.3_
  - [ ] 3.3 Update CheckPropertyPerformance to skip RelatedEntity properties
    - Add early return in IsComplexCollectionType check if IsRelatedEntity is true
    - _Requirements: 4.1, 4.3_
  - [ ] 3.4 Write property test for RelatedEntity warning suppression
    - **Property 3: RelatedEntity Warning Suppression**
    - **Validates: Requirements 4.1, 4.3**
  - [ ] 3.5 Write property test for non-RelatedEntity warning preservation
    - **Property 4: Non-RelatedEntity Warning Preservation**
    - **Validates: Requirements 4.4**

- [ ] 4. Checkpoint - Verify warning suppression
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 5. Fix DynamoDbMap multi-item deserialization
  - [ ] 5.1 Analyze GeneratePrimaryEntityIdentification for missing ComplexType checks
    - Review current code path for property assignment
    - Identify where ComplexType.IsMap check is missing
    - _Requirements: 2.5_
  - [ ] 5.2 Add ComplexType.IsMap handling in GeneratePrimaryEntityIdentification
    - Check if property.ComplexType?.IsMap is true
    - Generate `{TypeName}.FromDynamoDb<{TypeName}>(value.M, options)` pattern
    - Handle nullable map properties correctly
    - _Requirements: 2.1, 2.4_
  - [ ] 5.3 Add ComplexType.IsJsonBlob handling in GeneratePrimaryEntityIdentification
    - Ensure JsonBlob properties also use correct deserialization
    - This may already be handled but verify
    - _Requirements: 2.1_
  - [ ] 5.4 Write property test for DynamoDbMap multi-item deserialization
    - **Property 1: DynamoDbMap Multi-Item Deserialization Correctness**
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**

- [ ] 6. Checkpoint - Verify DynamoDbMap fix
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 7. Verify nested map support
  - [ ] 7.1 Create test entity with nested maps of different types
    - Create OuterMap type with [DynamoDbEntity]
    - Create InnerMap type with [DynamoDbEntity]
    - Create entity with OuterMap property containing InnerMap
    - _Requirements: 3.1, 3.2_
  - [ ] 7.2 Verify generated code handles nested maps correctly
    - Check ToDynamoDb generates recursive calls
    - Check FromDynamoDb generates recursive calls
    - _Requirements: 3.3, 3.4_
  - [ ] 7.3 Write property test for nested map round-trip
    - **Property 2: Nested Map Round-Trip Consistency**
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4**

- [ ] 8. Final checkpoint - Run full test suite
  - Ensure all tests pass, ask the user if questions arise.
  - Run `dotnet test` across all test projects
  - Verify no regressions in existing functionality

## Notes

- All tasks are required for comprehensive testing
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
