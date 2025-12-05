# Implementation Plan

- [x] 1. Create documentation changelog infrastructure
  - [x] 1.1 Create `docs/DOCUMENTATION_CHANGELOG.md` with header explaining its purpose for derived documentation synchronization
    - Include explanation that this is separate from repository CHANGELOG.md
    - Add instructions for how to use it when syncing derived documentation
    - _Requirements: 3.1, 3.4_
  - [x] 1.2 Update `.kiro/steering/documentation.md` to add documentation changelog requirements
    - Add section requiring updates to `docs/DOCUMENTATION_CHANGELOG.md` for documentation corrections
    - Specify the entry format (date, file path, before/after patterns, reason)
    - Clarify this is distinct from repository CHANGELOG.md
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 2. Correct ExecuteAsync references in core documentation
  - [x] 2.1 Fix `docs/core-features/BasicOperations.md` - replace ExecuteAsync with correct method names
    - Replace Get builder ExecuteAsync with GetItemAsync
    - Replace Put builder ExecuteAsync with PutAsync
    - Replace Update builder ExecuteAsync with UpdateAsync
    - Replace Delete builder ExecuteAsync with DeleteAsync
    - Record changes in documentation changelog
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 3.2, 3.3_
  - [x] 2.2 Fix `docs/core-features/QueryingData.md` - replace ExecuteAsync with ToListAsync/ToCompositeEntityAsync
    - Replace Query builder ExecuteAsync with ToListAsync or ToCompositeEntityAsync as appropriate
    - Replace Scan builder ExecuteAsync with ToListAsync
    - Record changes in documentation changelog
    - _Requirements: 1.5, 1.6, 3.2, 3.3_
  - [ ]* 2.3 Write property test verifying no ExecuteAsync in corrected files
    - **Property 1: No ExecuteAsync references in documentation**
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6**

- [x] 3. Correct return value access patterns in documentation
  - [x] 3.1 Fix return value examples in `docs/core-features/BasicOperations.md`
    - Update examples showing response.Attributes access to use ToDynamoDbResponseAsync
    - Add alternative example using DynamoDbOperationContext.Current.PreOperationValues
    - Add note about AsyncLocal not being suitable for unit testing
    - Record changes in documentation changelog
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.2, 3.3_
  - [ ]* 3.2 Write property test verifying return value patterns use correct API
    - **Property 4: Return value patterns use correct API**
    - **Validates: Requirements 2.1**

- [x] 4. Correct XML documentation in source code
  - [x] 4.1 Fix XML comments in `Oproto.FluentDynamoDb/Requests/DeleteItemRequestBuilder.cs`
    - Update example code in XML comments to use DeleteAsync instead of ExecuteAsync
    - Record changes in documentation changelog
    - _Requirements: 4.1, 4.2, 3.2, 3.3_
  - [x] 4.2 Fix XML comments in `Oproto.FluentDynamoDb/Requests/UpdateItemRequestBuilder.cs`
    - Update example code in XML comments to use UpdateAsync instead of ExecuteAsync
    - Record changes in documentation changelog
    - _Requirements: 4.1, 4.2, 3.2, 3.3_
  - [x] 4.3 Fix XML comments in `Oproto.FluentDynamoDb/Requests/PutItemRequestBuilder.cs`
    - Update example code in XML comments to use PutAsync instead of ExecuteAsync
    - Record changes in documentation changelog
    - _Requirements: 4.1, 4.2, 3.2, 3.3_
  - [x] 4.4 Search and fix any other source files with ExecuteAsync in XML comments
    - Search all .cs files for ExecuteAsync in XML documentation
    - Update any found references to correct method names
    - Record changes in documentation changelog
    - _Requirements: 4.1, 4.2, 3.2, 3.3_
  - [ ]* 4.5 Write property test verifying no ExecuteAsync in XML documentation
    - **Property 2: No ExecuteAsync references in XML documentation**
    - **Validates: Requirements 4.1, 4.2**

- [x] 5. Scan and correct remaining documentation files
  - [x] 5.1 Search and fix `docs/advanced-topics/*.md` files
    - Search for ExecuteAsync references
    - Search for incorrect return value patterns
    - Apply corrections and record in changelog
    - _Requirements: 1.1-1.6, 2.1, 3.2, 3.3_
  - [x] 5.2 Search and fix `docs/examples/*.md` files
    - Search for ExecuteAsync references
    - Search for incorrect return value patterns
    - Apply corrections and record in changelog
    - _Requirements: 1.1-1.6, 2.1, 3.2, 3.3_
  - [x] 5.3 Search and fix `docs/getting-started/*.md` files
    - Search for ExecuteAsync references
    - Apply corrections and record in changelog
    - _Requirements: 1.1-1.6, 3.2, 3.3_
  - [x] 5.4 Search and fix `docs/reference/*.md` files
    - Search for ExecuteAsync references
    - Search for incorrect return value patterns
    - Apply corrections and record in changelog
    - _Requirements: 1.1-1.6, 2.1, 3.2, 3.3_

  - [x] 5.5 Search and fix `docs/SourceGeneratorGuide.md` files
    - Search for ExecuteAsync references
    - Search for incorrect return value patterns
    - Apply corrections and record in changelog
    - _Requirements: 1.1-1.6, 2.1, 3.2, 3.3_

- [ ] 6. Checkpoint - Verify all corrections
  - Ensure all tests pass, ask the user if questions arise.

- [ ]* 7. Write property test for changelog format validation
  - [ ]* 7.1 Write property test verifying changelog entries contain required fields
    - **Property 3: Changelog entries contain required fields**
    - **Validates: Requirements 3.2, 3.3**

- [ ] 8. Final Checkpoint - Make sure all tests are passing
  - Ensure all tests pass, ask the user if questions arise.

