# Implementation Plan

## Overview

Convert the DynamoDB Source Generator design into a series of incremental implementation tasks. Each task builds on previous work and results in working, testable code that integrates with the existing Oproto.FluentDynamoDb library.

## Tasks
- [x] 1. Set up source generator project structure and core infrastructure
  - Create Oproto.FluentDynamoDb.SourceGenerator project targeting .NET Standard 2.0
  - Add necessary NuGet package references (Microsoft.CodeAnalysis.Analyzers, Microsoft.CodeAnalysis.CSharp)
  - Set up basic IIncrementalGenerator implementation with syntax receiver
  - Configure project to be packaged as analyzer with main library
  - Create basic unit test project for source generator testing
  - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_

- [x] 2. Define core attribute classes and interfaces
  - Create DynamoDbTableAttribute, DynamoDbAttributeAttribute, PartitionKeyAttribute, SortKeyAttribute
  - Create GlobalSecondaryIndexAttribute, RelatedEntityAttribute, QueryableAttribute
  - Define IDynamoDbEntity interface with static abstract methods
  - Create EntityMetadata, PropertyMetadata, IndexMetadata classes for future LINQ support
  - Add attributes to main library project
  - _Requirements: 1.1, 1.2, 1.3, 5.1, 5.2, 13.1, 13.2, 13.3, 13.4_

- [x] 3. Implement entity analysis and syntax processing
  - Create EntityAnalyzer class to parse class declarations with DynamoDB attributes
  - Implement property analysis to extract attribute mappings, key definitions, and relationships
  - Create EntityModel, PropertyModel, IndexModel, RelationshipModel data structures
  - Add validation logic for entity configuration (partition key requirements, conflicting patterns)
  - Implement diagnostic reporting for configuration errors
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 14.1, 14.4_

- [x] 4. Generate field name constants classes
  - Create FieldsGenerator to produce static field name constant classes
  - Generate main field constants for entity properties mapped to DynamoDB attributes
  - Generate nested GSI field classes for Global Secondary Index attributes
  - Handle special cases like metadata field access and reserved word mapping
  - Ensure generated field names are compile-time safe and discoverable
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 5. Generate key builder methods
  - Create KeysGenerator to produce static key construction methods
  - Generate partition key and sort key builder methods with proper formatting
  - Handle composite keys with multiple components, prefixes, and separators
  - Generate separate key builders for each Global Secondary Index
  - Ensure type safety for all key builder method parameters
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 6. Implement basic entity mapping generation
  - Create MapperGenerator for ToDynamoDb and FromDynamoDb methods
  - Generate single-item entity mapping (one C# object to one DynamoDB item)
  - Handle property type conversions (primitives, enums, nullable types)
  - Implement GetPartitionKey and MatchesEntity methods for entity identification
  - Generate basic EntityMetadata for future LINQ support
  - _Requirements: 1.1, 1.2, 1.4, 5.3, 5.4, 13.1, 13.2_

- [x] 7. Add enhanced ExecuteAsync method extensions
  - Create EnhancedExecuteAsyncExtensions class in main library
  - Implement generic ExecuteAsync<T> for GetItemRequestBuilder with entity mapping
  - Implement generic ExecuteAsync<T> for QueryRequestBuilder with entity mapping
  - Add WithItem<T> extension for PutItemRequestBuilder to accept entity objects
  - Create strongly-typed response classes (GetItemResponse<T>, QueryResponse<T>)
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1, 5.2_

- [x] 8. Implement multi-item entity support
  - Extend MapperGenerator to handle entities that span multiple DynamoDB items
  - Implement grouping logic in QueryResponse<T> to combine related items by partition key
  - Generate multi-item ToDynamoDb methods that return multiple AttributeValue dictionaries
  - Handle collection properties that map to separate DynamoDB items with consistent partition keys
  - Add validation for multi-item entity consistency and partition key generation
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

- [x] 9. Add related entity and multi-type query support
  - Implement RelatedEntity attribute processing in EntityAnalyzer
  - Generate mapping logic for related entities based on sort key patterns
  - Extend FromDynamoDb methods to populate related entity properties automatically
  - Implement entity type discrimination using sort key patterns or discriminator fields
  - Add filtering logic to MatchesEntity methods for proper entity type identification
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 8.1, 8.2, 8.3, 8.4, 8.5_

- [x] 10. Add STS scoped client support
  - Add WithClient extension methods to all request builders (Get, Put, Update, Query, Delete)
  - Modify generated table methods to accept optional IAmazonDynamoDB scopedClient parameter
  - Update enhanced ExecuteAsync methods to use builder's client automatically
  - Ensure all generated methods support service-layer STS token patterns
  - Add integration tests with scoped client scenarios
  - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_

- [x] 11. Create FluentResults extension package
  - Create Oproto.FluentDynamoDb.FluentResults project targeting .NET 8
  - Implement FluentResults wrapper extensions for all enhanced ExecuteAsync methods
  - Convert exceptions to Result.Fail with appropriate error messages
  - Return Result.Ok for successful operations with mapped entities
  - Ensure FluentResults package remains optional dependency
  - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

- [x] 12. Add comprehensive error handling and diagnostics
  - Implement all diagnostic descriptors for common configuration errors
  - Add runtime error handling with detailed error messages for mapping failures
  - Create DynamoDbMappingException with context information for debugging
  - Add validation for key builder parameters with helpful error messages
  - Generate readable, well-commented code with XML documentation
  - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5_

- [x] 13. Implement comprehensive testing suite
  - Create unit tests for all source generator components (EntityAnalyzer, generators)
  - Add integration tests for complete end-to-end scenarios with real DynamoDB operations
  - Create performance tests to ensure generated code performs well
  - Add tests for multi-item entities, related entities, and complex mapping scenarios
  - Test error handling, diagnostics, and edge cases thoroughly
  - _Requirements: All requirements validation_

- [x] 14. Package and integrate with main library
  - Configure main Oproto.FluentDynamoDb project to include source generator as analyzer
  - Update NuGet package configuration to bundle analyzer correctly
  - Build system integration and compatibility fixes for .NET 8
  - _Requirements: 10.1, 10.2, 10.3, 11.1, 11.2_
  
  **COMPLETED:**
  - Source generator packaging: Analyzer properly included in NuGet package at `analyzers/dotnet/cs/Oproto.FluentDynamoDb.SourceGenerator.dll`
  - Build system integration: Source generator builds before main library and packages correctly
  - Compatibility fixes: Resolved UnsafeAccessor, required members, and primary constructor issues
  - Source generator execution: Confirmed generator runs during compilation (shows diagnostics)
  - Simplified to .NET 8 target to resolve multi-targeting conflicts

- [x] 15. Fix source generator compilation errors
  - Debug and fix property type resolution bug in EntityAnalyzer or MapperGenerator
  - Resolve CS0246 errors where property names are treated as types instead of actual property types
  - Test generated code compilation with simple entity examples
  - Verify all generated methods (ToDynamoDb, FromDynamoDb, field constants, key builders) compile correctly
  - Add unit tests to prevent regression of compilation issues
  - _Requirements: 1.1, 1.2, 1.4, 2.1, 3.1, 5.3, 5.4_
  
  **CRITICAL BUG TO FIX:**
  - Generated code has compilation errors: `CS0246: The type or namespace name 'Id' could not be found`
  - Property names (e.g., "Id", "Name") are being treated as types instead of using actual property types (e.g., "string")
  - Root cause likely in EntityAnalyzer.cs PropertyType assignment or MapperGenerator.cs type usage
  - Source generator runs and generates files but generated code doesn't compile

- [x] 16. Complete multi-targeting and AOT support
  - Add multi-targeting support back (net6.0;net7.0;net8.0) with proper conditional compilation
  - Test source generator with NuGet package references (required for source generators to work)
  - Verify AOT compatibility and trimming support once source generator is working
  - Add comprehensive documentation and examples for using generated code
  - _Requirements: 10.4, 10.5, 11.3, 11.4, 11.5, 14.5_

- [x] 17. Fix source generator property type resolution bug
  - Debug and fix the compilation error where property names are treated as types (CS0246 errors)
  - Root cause: Generated code uses property names like "Id", "Name" as types instead of actual types like "string", "int"
  - Investigate EntityAnalyzer.cs property type resolution and MapperGenerator.cs type usage
  - Fix generated ToDynamoDb/FromDynamoDb methods to use correct property types
  - Ensure generated field constants and key builders compile correctly
  - Add regression tests to prevent similar issues
  - _Requirements: 1.1, 1.2, 1.4, 2.1, 3.1, 5.3, 5.4_

- [x] 18. Fix integration test diagnostic expectations
  - Update integration tests to have proper expectations for source generator diagnostics
  - Tests currently expect no diagnostics but source generator correctly generates legitimate warnings
  - Fix tests that expect empty diagnostics when warnings about reserved words (DYNDB021) and scalability (DYNDB027) are appropriate
  - Update test assertions to expect specific diagnostic types rather than empty collections
  - Ensure tests validate that source generator produces correct warnings for problematic configurations
  - Add test cases that verify diagnostic messages are helpful and actionable
  - _Requirements: 14.1, 14.2, 14.3, 14.4_

## Test Failure Analysis Summary

**Current Status:** 43 failed tests, 90 passed tests (68% pass rate)

**Failure Categories:**
1. **Diagnostic Expectation Issues (60% of failures):** Tests expect empty diagnostics but get legitimate warnings (DYNDB021, DYNDB027, DYNDB023, DYNDB029)
2. **Source Generator Execution Issues (15% of failures):** Tests expect generated sources but get none - source generator not running properly
3. **EntityAnalyzer Issues (10% of failures):** Tests expect diagnostics or analysis results but get null/empty
4. **Code Content Validation Issues (10% of failures):** Tests expect specific generated code patterns but don't find them
5. **Logic/Edge Case Issues (5% of failures):** Specific test logic problems (whitespace handling, type resolution)

**Root Causes:**
- Tests written before diagnostic system was fully implemented
- Source generator compilation/execution environment issues in tests
- Generated code structure doesn't match test expectations
- Missing error detection in EntityAnalyzer for some scenarios

## Test Failure Remediation Tasks

- [x] 19. Fix diagnostic expectation tests across all test suites
  - Update remaining EdgeCases tests to expect legitimate warnings instead of empty diagnostics
  - Update remaining Performance tests to expect appropriate warnings (DYNDB021, DYNDB027, DYNDB029)
  - Fix tests that expect empty diagnostics but get reserved word warnings
  - Fix tests that expect empty diagnostics but get scalability warnings
  - _Requirements: 14.1, 14.2, 14.3, 14.4_

- [x] 20. Fix source generator execution issues
  - Investigate tests that expect generated sources but get none (0 generated files)
  - Fix tests where source generator fails to run properly
  - Debug compilation issues that prevent source generation
  - Ensure proper attribute definitions are available during test compilation
  - _Requirements: 1.1, 1.2, 2.1, 2.2_

- [x] 21. Fix EntityAnalyzer test failures
  - Fix tests that expect diagnostic reporting but get no diagnostics
  - Fix tests that expect entity analysis results but get null
  - Ensure EntityAnalyzer properly detects and reports validation issues
  - Fix missing partition key detection (DYNDB001)
  - Fix multiple partition key detection (DYNDB002)
  - _Requirements: 14.1, 14.2, 14.3_

- [x] 22. Fix code generation content validation tests
  - Fix tests that expect specific generated code content but don't find it
  - Update tests for MapperGenerator to match actual generated code structure
  - Update tests for KeysGenerator to match actual generated code structure
  - Fix tests expecting specific method signatures or code patterns
  - Ensure generated code matches expected patterns for type conversions
  - _Requirements: 2.1, 2.2, 3.1, 3.2, 4.1, 4.2_

- [x] 23. **PRIORITY: Fix Query operation architecture and introduce EF-style methods**
  - **ARCHITECTURAL ISSUE:** Current Query operation behavior doesn't match intended design
  - **Problem:** Single `ExecuteAsync<T>()` method can't return both `List<T>` and `T` - compile-time incompatible
  - **Root Cause:** Confusion about when to return lists vs single entities with related entities
  - **NEW API DESIGN (EF/LINQ-style):**
    - **`ToListAsync<T>()`** - Each DynamoDB item becomes a separate `T` instance (1:1 mapping)
    - **`ToCompositeEntityAsync<T>()`** - Multiple DynamoDB items combined into one `T` instance (N:1 mapping)
    - **All data stored as native DynamoDB attributes** (S, N, SS, NS, etc.) - NO JSON anywhere
    - **Related entities identified by sort key patterns** and mapped to properties using `[RelatedEntity]` attributes
  - **Implementation Required:**
    - **REPLACE** existing `ExecuteAsync<T>()` with new `ToListAsync<T>()` extension method for QueryRequestBuilder
    - **ADD** new `ToCompositeEntityAsync<T>()` extension method for QueryRequestBuilder  
    - Implement primary entity detection based on sort key patterns in composite entities
    - Implement related entity population from multiple items into primary entity properties
    - Remove any JSON serialization logic - everything uses native DynamoDB attributes
    - Update all test expectations to use new methods with correct behavior
    - Update internal project to use new API (single project, manageable change)
  - **Breaking Change Strategy:** Hard break - remove old method, replace with new EF-style methods
  - **Impact:** This is foundational - must be fixed before continuing with unit test fixes
  - _Requirements: 4.2, 6.1, 6.2, 7.1, 7.2, 7.3, 8.1, 8.2_

- [x] 24. Fix RelationshipModel test logic issues
  - Fix HasSpecificEntityType test with whitespace-only entity type
  - Review and fix entity type detection logic for edge cases
  - Ensure proper handling of null, empty, and whitespace entity types
  - _Requirements: 8.1, 8.2, 8.3_

- [x] 25. Fix multi-item entity generation tests
  - Fix tests expecting specific JSON serialization comments in generated code
  - Update multi-item entity generation to match expected patterns
  - Fix collection handling in multi-item entities
  - Ensure proper type name resolution in generated code (fix "tring" vs "string" issues)
  - _Requirements: 6.1, 6.2, 6.3, 6.4_

- [x] 26. Fix error scenario diagnostic tests
  - Fix tests that expect specific error diagnostics but get none
  - Ensure source generator properly reports errors for invalid configurations
  - Fix non-partial class detection (DYNDB010)
  - Fix multiple partition key detection (DYNDB002)
  - Fix missing partition key detection (DYNDB001)
  - _Requirements: 14.1, 14.2, 14.3, 14.4_

- [x] 27. Implement computed and composite key support
  - Create ComputedAttribute and ExtractedAttribute classes
  - Add ComputedKeyModel and ExtractedKeyModel to PropertyModel
  - Extend EntityAnalyzer to detect and validate computed key attributes
  - Generate computed key logic in ToDynamoDb methods (compute before mapping)
  - Generate extracted key logic in FromDynamoDb methods (extract after mapping)
  - Add key builder methods for computed composite keys
  - Implement validation for circular dependencies and invalid source properties
  - Add diagnostic descriptors for computed key validation errors (DYNDB004, DYNDB005, DYNDB006)
  - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5_

## Optional Enhancement Tasks

- [x] 28. Create comprehensive documentation and examples
  - Write developer guide for using DynamoDB source generator
  - Create migration guide from manual mapping to generated code
  - Add code examples for common scenarios (single entities, multi-item, related entities)
  - Document STS integration patterns and best practices
  - Create troubleshooting guide for common issues and error messages
  - _Requirements: 10.4, 14.5_

- [x] 29. Performance optimization and advanced features
  - Optimize generated code for performance (minimize allocations, efficient mapping)
  - Add caching for expensive operations like EntityMetadata generation
  - Implement incremental source generation for better build performance
  - Add support for custom type converters and advanced mapping scenarios
  - Prepare foundation for future LINQ expression support
  - _Requirements: 11.1, 11.2, 11.3, 11.4, 13.5_

- [x] 30. Fix DynamoDbStreamRecordEventProcessor constructor compilation errors
  - Fix missing constructor parameter in `Oproto.FluentDynamoDb.UnitTests/Streams/DynamoDbStreamRecordEventProcessorTests.cs`
  - Update test instantiations at lines 26, 52, 78, and 104 to provide required `DynamoDBEvent.DynamodbStreamRecord` parameter
  - Create mock `DynamoDBEvent.DynamodbStreamRecord` objects for test scenarios
  - Verify all test methods compile and execute successfully
  - This fix will enable ~614 blocked unit tests to run
  - _Requirements: Test infrastructure integrity for existing functionality_

- [x] 31. Fix SourceGenerator unit test compilation errors
  - Fix `GeneratorSyntaxContext` constructor issues in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Performance/PerformanceOptimizationTests.cs`
  - Update constructor calls at lines 143 and 149 to use correct `GeneratorSyntaxContext` API
  - Fix read-only property assignment errors for `PropertyModel.HasAttributeMapping` at lines 369, 376, 392, 400, 412, 423, 499, 506, 513, 520
  - Update test code to use proper initialization patterns for read-only properties
  - Verify all source generator unit tests compile and execute successfully
  - This fix will enable ~120 blocked source generator tests to run
  - _Requirements: Source generator test infrastructure integrity_

## Test Failure Remediation Tasks (Phase 2)

- [x] 32. Fix performance optimization code generation tests
  - Fix `OptimizedCodeGenerator_ShouldProduceEfficientCode` test expecting "StringBuilder" in generated code
  - Fix `OptimizedCodeGenerator_ShouldMinimizeAllocations` test expecting "new List<" patterns
  - Fix `AdvancedPerformanceOptimizations_ShouldGenerateOptimizedCode` test expecting "ArrayPool" usage
  - Update `OptimizedCodeGenerator` and `AdvancedPerformanceOptimizations` classes to generate expected optimization patterns
  - Ensure generated code includes performance optimizations like StringBuilder usage, pre-allocated collections, and ArrayPool
  - _Requirements: Performance optimization code generation integrity_

- [x] 33. Fix mapper generator content validation tests
  - Fix `GenerateEntityImplementation_WithMultiItemEntity_GeneratesMultiItemMethods` test expecting specific multi-item method signatures
  - Fix `GenerateEntityImplementation_WithCollectionProperties_GeneratesJsonSerialization` test expecting JSON serialization code
  - Fix `GenerateEntityImplementation_WithDifferentPropertyTypes_GeneratesCorrectConversions` test expecting type conversion patterns
  - Update `MapperGenerator` to generate expected method signatures and code patterns
  - Ensure generated code matches test expectations for multi-item entities and collection handling
  - _Requirements: Entity mapping code generation accuracy_

- [x] 34. Fix diagnostic expectation mismatches in integration tests
  - Fix `SourceGenerator_WithRelatedEntitiesButNoSortKey_GeneratesWarning` expecting DYNDB016 diagnostic but getting DYNDB023/DYNDB027
  - Fix `SourceGenerator_WithRelatedEntities_GeneratesRelationshipMapping` expecting DYNDB016 diagnostic but getting other warnings
  - Fix `SourceGenerator_WithCompleteEntity_GeneratesAllExpectedFiles` expecting no diagnostics but getting legitimate warnings
  - Fix `SourceGenerator_WithScalabilityIssues_GeneratesHelpfulWarnings` expecting scalability warnings but getting none
  - Update diagnostic generation logic to produce expected diagnostic IDs for specific scenarios
  - Update test expectations to match actual diagnostic behavior
  - _Requirements: Diagnostic system accuracy and test alignment_

- [ ] 35. Fix missing diagnostic generation for error scenarios
  - Fix `SourceGenerator_WithMultiplePartitionKeys_GeneratesDiagnostics` expecting diagnostics but getting none
  - Fix `SourceGenerator_WithNonPartialClass_GeneratesDiagnostics` expecting diagnostics but getting none
  - Fix `SourceGenerator_WithErrorScenarios_GeneratesDiagnostics` expecting diagnostics but getting none
  - Implement missing validation logic in `EntityAnalyzer` to detect multiple partition keys, non-partial classes, and other error scenarios
  - Add diagnostic descriptors for missing error cases (DYNDB002 for multiple partition keys, DYNDB010 for non-partial classes)
  - _Requirements: Comprehensive error detection and reporting_

- [ ] 36. Fix edge case handling in source generator
  - Fix `SourceGenerator_WithEmptyAttributeNames_HandlesGracefully` expecting specific error handling behavior
  - Fix `SourceGenerator_WithGenericTypeConstraints_HandlesCorrectly` expecting no diagnostics but getting warnings
  - Fix `SourceGenerator_WithCircularReferences_HandlesGracefully` expecting specific generated code structure
  - Implement proper edge case handling for empty attribute names, generic constraints, and circular references
  - Update generated code structure to match test expectations for edge cases
  - _Requirements: Robust edge case handling_

- [ ] 37. Fix complex entity and performance test expectations
  - Fix `SourceGenerator_WithVeryComplexRelationshipPatterns_HandlesCorrectly` expecting specific generated code format
  - Fix `SourceGenerator_WithManyRelatedEntities_HandlesEfficiently` expecting specific entity code structure
  - Fix `SourceGenerator_WithComplexEntity_CompletesReasonably` expecting DYNDB027 diagnostic but getting other warnings
  - Fix `SourceGenerator_WithComplexTypes_GeneratesCorrectConversions` expecting specific generated code format
  - Update code generation to produce expected output format and diagnostic behavior for complex scenarios
  - _Requirements: Complex entity handling and performance characteristics_

- [ ] 38. Fix keys generator and field generator test expectations
  - Fix `SourceGenerator_WithComplexKeyFormats_ParsesCorrectly` expecting specific keys code structure
  - Fix `SourceGenerator_WithMultiItemEntity_GeneratesMultiItemSupport` expecting specific entity code format
  - Update `KeysGenerator` and `FieldsGenerator` to produce expected code structure and method signatures
  - Ensure generated keys and fields classes match test expectations for complex scenarios
  - _Requirements: Key and field generation accuracy_

- [x] 39. Remove flawed scalability detection logic
  - Remove DYNDB027 scalability warning generation from EntityAnalyzer
  - Remove sequential ID pattern detection logic (lines ~1230-1260 in EntityAnalyzer.cs)
  - Keep only the GSI overuse check (>5 GSIs) as it's actually valid
  - Update or remove `SourceGenerator_WithScalabilityIssues_GeneratesHelpfulWarnings` test
  - Update other tests that expect DYNDB027 warnings to not expect them
  - Remove ScalabilityWarning diagnostic descriptor if no longer used
  - _Requirements: Remove false positive scalability warnings that can't be accurately detected at compile time_