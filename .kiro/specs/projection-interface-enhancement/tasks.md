# Implementation Plan: Projection Interface Enhancement

## Overview

This implementation plan converts the projection interface enhancement design into a series of coding tasks. The plan focuses on creating the new interface hierarchy, updating projection generation, modifying QueryRequestBuilder constraints, and ensuring comprehensive testing and documentation. Each task builds incrementally to maintain system stability while adding the new projection interface capabilities.

## Tasks

- [x] 1. Create IReadOnlyEntity interface and update IDynamoDbEntity
  - Create new `IReadOnlyEntity<TSelf>` interface in `Oproto.FluentDynamoDb/Entities/`
  - Update `IDynamoDbEntity` to inherit from `IReadOnlyEntity<TSelf>`
  - Ensure all existing method signatures are preserved for backward compatibility
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 1.1 Write unit tests for interface hierarchy
  - Test that `IReadOnlyEntity<TSelf>` has correct method signatures
  - Test that `IDynamoDbEntity` properly inherits from `IReadOnlyEntity<TSelf>`
  - Test backward compatibility of existing method signatures
  - _Requirements: 1.5_
  - **Status: COMPLETE** - Comprehensive tests already exist in `InterfaceHierarchyTests.cs` and `InterfaceHierarchyValidationTest.cs`

- [x] 2. Update QueryRequestBuilder constraint
  - Modify `QueryRequestBuilder<TEntity>` constraint to `where TEntity : class, IReadOnlyEntity<TEntity>`
  - Update all related builder classes that use similar constraints
  - Ensure existing entity types continue to work due to interface inheritance
  - _Requirements: 3.4, 3.5_

- [x] 2.1 Propagate constraint to PaginationExtensions
  - Update `Oproto.FluentDynamoDb/Pagination/PaginationExtensions.cs`
  - Add `IReadOnlyEntity<TEntity>` constraint to extension methods on lines 44, 77
  - _Requirements: 3.5_

- [x] 2.2 Propagate constraint to DynamoDbIndex
  - Update `Oproto.FluentDynamoDb/Storage/DynamoDbIndex.cs`
  - Add `IReadOnlyEntity<TEntity>` constraint to Query methods on lines 92, 127, 208, 227
  - _Requirements: 3.5_

- [x] 2.3 Propagate constraint to GenericTable
  - Update `Oproto.FluentDynamoDb/Storage/GenericTable.cs`
  - Add `IReadOnlyEntity<TEntity>` constraint to Query/Scan/Get methods
  - Lines: 80, 90, 107, 119, 128, 292, 312
  - _Requirements: 3.5_
  - **Status: COMPLETE** - All test files updated with IReadOnlyEntity constraint on Scan methods

- [x] 2.4 Propagate constraint to DynamicTable and DynamicEntity
  - Update `Oproto.FluentDynamoDb/Storage/DynamicTable.cs` lines 130, 151, 582, 600
  - Update `Oproto.FluentDynamoDb/Entities/DynamicEntity.cs` to implement `IReadOnlyEntity<DynamicEntity>`
  - _Requirements: 3.5_
  - **Status: COMPLETE** - DynamicEntity already implements IDynamoDbEntity which inherits from IReadOnlyEntity. DynamicTable uses DynamicEntity directly (not generic), so no changes needed.

- [x] 2.5 Propagate constraint to BatchGetBuilder and TransactionGetBuilder
  - Update `Oproto.FluentDynamoDb/Requests/BatchGetBuilder.cs` line 45
  - Update `Oproto.FluentDynamoDb/Requests/TransactionGetBuilder.cs` line 41
  - _Requirements: 3.5_
  - **Status: COMPLETE** - Both Add<TEntity> methods already have `where TEntity : class, IReadOnlyEntity` constraint

- [x] 2.6 Propagate constraint to EntityExecuteAsyncExtensions
  - Update `Oproto.FluentDynamoDb/Requests/Extensions/EntityExecuteAsyncExtensions.cs`
  - Add constraint to all extension methods (lines 27, 82, 151, 218, 312, 389, 490, 563, 710, 778, 855, 917)
  - _Requirements: 3.5_
  - **Status: COMPLETE** - Extension methods correctly use `IDynamoDbEntity` constraint for full entity mapping operations. The `IReadOnlyEntity` constraint is on the builder classes, not the execution extensions.

- [x] 2.7 Propagate constraint to other extension files
  - Update `Oproto.FluentDynamoDb/Requests/Extensions/ProjectionExtensions.cs` lines 26, 60, 86-87
  - Update `Oproto.FluentDynamoDb/Requests/Extensions/WithFilterExpressionExtensions.cs` lines 260-261, 295-296
  - Update `Oproto.FluentDynamoDb/Requests/Extensions/WithConditionExpressionExtensions.cs` lines 213-214
  - Update `Oproto.FluentDynamoDb/Requests/Extensions/EncryptionExtensions.cs` lines 82-83, 107-108, 184-185
  - _Requirements: 3.5_
  - **Status: COMPLETE** - All extension files already have correct `IReadOnlyEntity` constraints where appropriate

- [x] 2.8 Update example files and PlaceholderEntity
  - Update `Oproto.FluentDynamoDb/Examples/PlaceholderEntity` to implement `IReadOnlyEntity`
  - Or update example files to use proper entity types
  - Files: CompositeKeyTableExample.cs, SingleKeyTableExample.cs, FormatStringExamples.cs
  - _Requirements: 3.5_
  - **Status: COMPLETE** - PlaceholderEntity already implements IDynamoDbEntity which inherits from IReadOnlyEntity

- [x] 2.9 Build verification checkpoint
  - Run `dotnet build` to verify all constraint propagation is complete
  - Fix any remaining errors
  - _Requirements: 3.5_
  - **Status: COMPLETE** - Build succeeded with 0 warnings and 0 errors

- [x] 2.10 Write unit tests for constraint updates
  - Test that existing entities still work with updated constraints
  - Test that the constraint accepts types implementing `IReadOnlyEntity<T>`
  - _Requirements: 3.5_
  - **Status: COMPLETE** - All 1869 unit tests pass, including InterfaceHierarchyTests that validate the constraint behavior

- [x] 3. Checkpoint - Ensure interface changes compile
  - Ensure all tests pass, ask the user if questions arise.
  - **Status: COMPLETE** - Build passes with 0 errors/warnings, all 1869 unit tests pass

- [x] 4. Implement metadata inheritance for projections
  - Create `MetadataInheritanceStrategy` class for projection metadata creation
  - Update `ProjectionModelAnalyzer` to extract source entity metadata
  - Implement logic to filter metadata to projected attributes only
  - _Requirements: 2.4, 5.1, 5.2, 5.3, 5.4, 5.5_

- [x] 4.1 Write property test for metadata inheritance
  - **Property 2: Projection metadata inheritance consistency**
  - **Validates: Requirements 2.4, 5.1, 5.2, 5.4**

- [x] 4.2 Write property test for write-specific metadata exclusion
  - **Property 10: Write-specific metadata exclusion**
  - **Validates: Requirements 5.5**

- [x] 5. Update projection generation to implement IReadOnlyEntity
  - Modify `ProjectionExpressionGenerator` to generate both interface implementations
  - Generate `FromDynamoDb()` method implementation (existing)
  - Generate `GetPartitionKey()` method that delegates to source entity
  - Generate `GetEntityMetadata()` method using inherited metadata
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 5.1 Write property test for projection interface implementation
  - **Property 1: Generated projections implement both interfaces**
  - **Validates: Requirements 2.1, 6.4**

- [x] 5.2 Write property test for ProjectionExpression preservation
  - **Property 12: ProjectionExpression property preservation**
  - **Validates: Requirements 2.5, 6.5**

- [x] 6. Implement automatic projection expression application
  - Update `QueryRequestBuilder<T>` to detect projection types
  - Automatically apply `ProjectionExpression` when querying with projections
  - Ensure proper result hydration for projection instances
  - _Requirements: 3.2, 3.3, 4.2, 4.3_
  - **Status: COMPLETE** - Added `ToProjectionListAsync<TProjection>` extension method in `ProjectionExtensions.cs`

- [x] 6.1 Write property test for projection expression application
  - **Property 4: Projection query expression application**
  - **Validates: Requirements 3.2, 4.2**
  - **Status: COMPLETE** - Tests in `ProjectionQueryPropertyTests.cs` pass

- [x] 6.2 Write property test for projection result hydration
  - **Property 5: Projection query result hydration**
  - **Validates: Requirements 3.3, 4.3**
  - **Status: COMPLETE** - Tests in `ProjectionQueryPropertyTests.cs` pass

- [x] 7. Update index generation for projection support
  - Modify index class generation to support projection types
  - Generate non-generic `Query()` methods for indexes with projection types
  - Ensure projections are excluded from table entity accessors
  - _Requirements: 4.1, 4.4_
  - **Status: COMPLETE** - Added non-generic `Query()` and `Query(expression, values)` methods to `DynamoDbIndex<TDefault>` class

- [x] 7.1 Write property test for index projection methods
  - **Property 7: Index projection method generation**
  - **Validates: Requirements 4.1**
  - **Status: COMPLETE** - Tests in `ProjectionQueryPropertyTests.cs` pass

- [x] 7.2 Write property test for projection exclusion from entity accessors
  - **Property 8: Projection exclusion from entity accessors**
  - **Validates: Requirements 4.4**
  - **Status: COMPLETE** - Tests in `ProjectionQueryPropertyTests.cs` verify projections are excluded from entity accessors by design

- [x] 8. Checkpoint - Ensure projection generation works
  - **Status: COMPLETE** - All 1887 unit tests pass, all 662 source generator tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Implement error handling and diagnostics
  - Add new diagnostic IDs: FDDB060, FDDB061, FDDB062
  - Implement source entity validation in projection analyzer
  - Add clear error messages for projection interface violations
  - Include helpful suggestions in error messages
  - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

- [x] 9.1 Write property test for metadata inheritance error handling
  - **Property 13: Metadata inheritance error handling**
  - **Validates: Requirements 8.1**

- [x] 9.2 Write property test for source entity validation
  - **Property 14: Source entity validation**
  - **Validates: Requirements 8.3**

- [x] 9.3 Write property test for interface violation error clarity
  - **Property 15: Interface violation error clarity**
  - **Validates: Requirements 8.4, 8.5**

- [x] 10. Ensure backward compatibility
  - Verify existing `IProjectionModel<TSelf>` interface remains functional
  - Test that existing projection extension methods work with new implementation
  - Ensure generated projections implement both interfaces
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

- [x] 10.1 Write property test for backward compatibility preservation
  - **Property 6: Backward compatibility preservation**
  - **Validates: Requirements 3.5, 6.2**

- [x] 10.2 Write property test for backward compatibility interface preservation
  - **Property 11: Backward compatibility interface preservation**
  - **Validates: Requirements 6.1, 6.2, 6.3**

- [x] 11. Add comprehensive integration tests
  - Create end-to-end projection query scenarios
  - Test projection usage with index queries
  - Test mixed entity and projection queries
  - Test error handling scenarios
  - _Requirements: 7.5_
  - **Status: COMPLETE** - Created `ProjectionQueryIntegrationTests.cs` with 10 integration tests and 6 property tests

- [x] 11.1 Write property test for projection query pattern compatibility
  - **Property 9: Projection query pattern compatibility**
  - **Validates: Requirements 4.5**
  - **Status: COMPLETE** - Tests in `ProjectionQueryIntegrationTests.cs` pass (6 property tests)

- [x] 12. Update API consistency tests
  - Add projection interface compatibility tests to `ApiConsistencyTests` project
  - Verify all documented projection patterns compile and work
  - Test projection usage with all QueryRequestBuilder patterns
  - _Requirements: 7.4_

- [x] 13. Update documentation
  - Update `fluentdynamodb.md` steering document with projection interface examples
  - Add projection usage patterns with QueryRequestBuilder
  - Include index projection query examples
  - Document error handling scenarios 
  - _Requirements: 7.1_

- [x] 14. Update CHANGELOG.md
  - Add entry for projection interface enhancement feature
  - Document new capabilities and improvements
  - Note any breaking changes (should be none due to backward compatibility)
  - _Requirements: 7.2_

- [x] 15. Update DOCUMENTATION_CHANGELOG.md
  - Track corrections to existing projection documentation
  - Document new projection interface patterns
  - Record changes to API examples and usage patterns
  - _Requirements: 7.3_

- [x] 16. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks are required for comprehensive implementation
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation throughout implementation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- Integration tests verify end-to-end functionality
- Documentation tasks ensure comprehensive coverage of the new feature