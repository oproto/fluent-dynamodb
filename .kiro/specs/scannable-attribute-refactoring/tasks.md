# Implementation Plan

- [x] 1. Create ScannableAttribute
  - Create the `[Scannable]` attribute class in the Attributes project
  - Add XML documentation with warnings about scan operation costs
  - Include code examples in documentation
  - _Requirements: 1.3, 1.4, 1.5, 6.1, 6.2, 6.3_

- [x] 2. Enhance EntityModel to support scannable flag
  - Add `IsScannable` boolean property to `EntityModel` class
  - Add XML documentation for the new property
  - _Requirements: 3.1_

- [x] 3. Update EntityAnalyzer to detect Scannable attribute
  - Add method to detect `[Scannable]` attribute on table classes
  - Set `IsScannable` property on `EntityModel` when attribute is present
  - Integrate detection into existing entity analysis flow
  - _Requirements: 1.1, 1.2, 3.1, 3.2_

- [x] 4. Enhance TableGenerator to generate Scan methods
  - Add `GenerateScanMethods()` private method to `TableGenerator`
  - Generate parameterless `Scan()` method when `IsScannable` is true
  - Generate expression-based `Scan(string, params object[])` method when `IsScannable` is true
  - Include XML documentation with warnings in generated methods
  - Follow same pattern as existing `Query()` method generation
  - Integrate scan method generation into `GenerateTableClass()` method
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.2, 3.3, 3.4, 3.5_

- [x] 5. Remove legacy scannable implementation
  - Delete `IScannableDynamoDbTable.cs` file
  - Delete `ScannableDynamoDbTable.cs` file
  - Remove `AsScannable()` method from `DynamoDbTableBase.cs`
  - _Requirements: 5.1, 5.2, 5.3_

- [x] 6. Update internal code to use new pattern
  - Find all usages of `AsScannable()` in the codebase
  - Add `[Scannable]` attribute to table classes that need scan operations
  - Update code to call `Scan()` directly instead of `AsScannable().Scan()`
  - _Requirements: 1.1, 2.1, 2.2_

- [x] 7. Add source generator unit tests
  - [x] 7.1 Test scannable attribute detection in EntityAnalyzer
    - Test that `IsScannable` is true when `[Scannable]` is present
    - Test that `IsScannable` is false when `[Scannable]` is absent
    - _Requirements: 3.1_
  
  - [x] 7.2 Test scan method generation in TableGenerator
    - Test that both `Scan()` overloads are generated when `IsScannable` is true
    - Test that no `Scan()` methods are generated when `IsScannable` is false
    - Test that generated code includes XML documentation
    - Test that generated code compiles successfully
    - _Requirements: 2.1, 2.2, 3.4_
  
  - [x] 7.3 Test integration with existing features
    - Test `[Scannable]` with single-key tables
    - Test `[Scannable]` with composite-key tables
    - Test `[Scannable]` with tables that have GSIs
    - _Requirements: 3.3_

- [-] 8. Add functional tests
  - [x] 8.1 Test generated Scan() method functionality
    - Test that parameterless `Scan()` returns configured `ScanRequestBuilder`
    - Test that expression-based `Scan()` applies filter correctly
    - Test that generated methods work with method chaining
    - Test that correct table name is passed to builder
    - _Requirements: 2.3, 2.4_
  
  - [ ] 8.2 Test manual implementation support
    - Create test table with manually implemented `Scan()` methods
    - Verify manual implementation works without conflicts
    - _Requirements: 4.1, 4.2, 4.3_

- [ ] 9. Update documentation
  - [ ] 9.1 Add usage examples to ScannableAttribute documentation
    - Show how to apply the attribute
    - Show how to use generated methods
    - Include warnings about scan costs
    - _Requirements: 6.1, 6.2, 6.3, 6.4_
  
  - [ ] 9.2 Document manual implementation pattern
    - Provide example of manually implementing Scan() methods
    - Explain when manual implementation is appropriate
    - _Requirements: 4.3, 4.4, 6.4_
  
  - [ ] 9.3 Update any existing documentation that references AsScannable()
    - Update README if it mentions the old pattern
    - Update any example code
    - _Requirements: 6.5_
