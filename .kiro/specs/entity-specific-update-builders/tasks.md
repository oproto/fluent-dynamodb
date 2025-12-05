# Implementation Plan

- [x] 1. Create entity-specific update builder classes
  - Generate specialized update builder classes that inherit from UpdateItemRequestBuilder<TEntity>
  - Implement simplified Set() method that only requires TUpdateModel generic parameter
  - Add covariant return type overrides for all fluent methods (ForTable, ReturnAllNewValues, etc.)
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Modify source generator to produce entity-specific builders
  - Update source generator to create entity-specific update builder classes for each entity type
  - Modify accessor class Update() methods to return entity-specific builders instead of base UpdateItemRequestBuilder
  - Ensure generated code includes all necessary covariant return type overrides
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 3. Add convenience method convenience methods to accessor classes
  - [x] 3.1 Implement GetAsync() convenience method method
    - Add GetAsync() method that combines Get() and GetItemAsync()
    - Support cancellation tokens
    - _Requirements: 2.1, 2.5_

  - [x] 3.2 Implement PutAsync() convenience method method
    - Add PutAsync() method that combines Put() and PutAsync()
    - Support cancellation tokens
    - _Requirements: 2.2, 2.5_

  - [x] 3.3 Implement DeleteAsync() convenience method method
    - Add DeleteAsync() method that combines Delete() and DeleteAsync()
    - Support cancellation tokens
    - _Requirements: 2.3, 2.5_

  - [x] 3.4 Implement UpdateAsync() convenience method method
    - Add UpdateAsync() method that accepts configuration action
    - Support cancellation tokens
    - _Requirements: 2.4, 2.5_

- [x] 4. Add raw attribute dictionary support
  - [x] 4.1 Add Put(Dictionary<string, AttributeValue>) overload to accessor classes
    - Implement Put() overload that accepts raw attribute dictionary
    - Return configured PutItemRequestBuilder
    - _Requirements: 3.1, 3.3_

  - [x] 4.2 Add PutAsync(Dictionary<string, AttributeValue>) overload to accessor classes
    - Implement convenience method PutAsync() for raw dictionaries
    - Support cancellation tokens
    - _Requirements: 3.2, 3.3, 3.4_

- [x] 5. Add convenience method methods to DynamoDbTableBase
  - [x] 5.1 Implement GetAsync<TEntity>() on base class
    - Support cancellation tokens
    - _Requirements: 4.3_

  - [x] 5.2 Implement PutAsync<TEntity>() overloads on base class
    - Add overload for entity parameter
    - Add overload for Dictionary<string, AttributeValue> parameter
    - Support cancellation tokens
    - _Requirements: 4.3_

  - [x] 5.3 Implement DeleteAsync<TEntity>() on base class
    - Support cancellation tokens
    - _Requirements: 4.3_

  - [x] 5.4 Implement UpdateAsync<TEntity>() on base class
    - Accept configuration action for update builder
    - Support cancellation tokens
    - _Requirements: 4.3_

- [x] 6. Update generated code structure
  - Modify source generator templates to include entity-specific builders
  - Update accessor class templates to include convenience method methods
  - Update accessor class templates to include raw dictionary overloads
  - Ensure consistent code generation across all entity types
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 4.1, 4.2, 4.3, 4.4, 4.5_

- [x] 7. Implement GenerateWrapper attribute and discovery system
  - [x] 7.1 Create GenerateWrapper attribute class
    - Define attribute with RequiresSpecialization and SpecializationNotes properties
    - Add XML documentation explaining usage
    - Place in Oproto.FluentDynamoDb.SourceGeneration namespace
    - _Requirements: 6.1, 6.2_

  - [x] 7.2 Mark extension methods with GenerateWrapper attribute
    - Add [GenerateWrapper] to simple wrapper methods (Where(string), WithValue, WithAttribute, etc.)
    - Add [GenerateWrapper(RequiresSpecialization = true)] to methods requiring specialization
    - Document specialization requirements in SpecializationNotes
    - _Requirements: 6.1, 6.3_

  - [x] 7.3 Implement extension method discovery in source generator
    - Scan Oproto.FluentDynamoDb.Requests.Extensions namespace for [GenerateWrapper] attributes
    - Group discovered methods by interface they extend
    - Validate that base builder implements the interface
    - Report errors for missing or inaccessible methods
    - _Requirements: 6.1, 6.4_

- [x] 8. Implement wrapper generation logic
  - [x] 8.1 Implement simple wrapper generation
    - Generate wrappers for methods with RequiresSpecialization = false
    - Maintain method signature but change return type to entity-specific builder
    - Delegate to extension method and return this
    - _Requirements: 5.1, 5.2, 5.3, 6.2_

  - [x] 8.2 Implement specialization pattern matching
    - Detect methods with Expression<Func<TEntity, bool>> parameter pattern
    - Detect methods with Expression<Func<TUpdateExpressions, TUpdateModel>> parameter pattern
    - Apply appropriate generic parameter substitution rules
    - _Requirements: 5.5, 6.2_

  - [x] 8.3 Generate specialized wrappers for Where() methods
    - Generate Where(string) simple wrapper
    - Generate Where(string, params object[]) simple wrapper
    - Generate Where(Expression<Func<TEntity, bool>>) specialized wrapper with TEntity fixed
    - _Requirements: 5.1, 5.5_

  - [x] 8.4 Generate specialized wrapper for Set() method
    - Fix TEntity and TUpdateExpressions generic parameters
    - Keep TUpdateModel as open generic parameter
    - Maintain where TUpdateModel : new() constraint
    - _Requirements: 1.2, 5.5_

  - [x] 8.5 Generate wrappers for all other marked extension methods
    - WithValue() overloads
    - WithAttribute() / WithAttributeName() overloads
    - Any other marked methods on implemented interfaces
    - _Requirements: 5.2, 5.3, 5.4, 6.5_

- [x] 9. Update entity-specific update builder generation
  - [x] 9.1 Remove hardcoded covariant return type overrides
    - Keep only base class method overrides (ForTable, ReturnAllNewValues, etc.)
    - Remove manual Where(), WithValue(), WithAttribute() implementations
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 9.2 Integrate wrapper generation into builder generation
    - Call wrapper generation logic for each entity-specific builder
    - Ensure wrappers are generated after constructor and Set() method
    - Maintain consistent code organization
    - _Requirements: 6.2, 6.5_

  - [x] 9.3 Add validation for generated wrappers
    - Verify all generated wrappers compile
    - Check for naming conflicts
    - Validate generic parameter substitution
    - _Requirements: 6.4_

- [x] 10. Verify backward compatibility
  - Test that existing code using base UpdateItemRequestBuilder still compiles
  - Test that existing code using builder pattern still works
  - Verify no breaking changes to public API surface
  - _Requirements: 4.4, 4.5_

- [x] 11. Create integration tests for new features
  - [x] 11.1 Test entity-specific update builders with real DynamoDB
    - Test simplified Set() method with various property types
    - Test fluent chaining with covariant return types
    - Test encryption support with entity-specific builders
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 11.2 Test convenience method methods on accessors with real DynamoDB
    - Test GetAsync() retrieves entities correctly
    - Test PutAsync() stores entities correctly (both entity and dictionary overloads)
    - Test DeleteAsync() removes entities correctly
    - Test UpdateAsync() applies updates correctly
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.2, 3.4_

  - [x] 11.3 Test convenience method methods on base class with real DynamoDB
    - Test GetAsync<TEntity>() with key dictionary
    - Test PutAsync<TEntity>() with both entity and dictionary
    - Test DeleteAsync<TEntity>() with key dictionary
    - Test UpdateAsync<TEntity>() with configuration action
    - _Requirements: 4.3_

  - [x] 11.4 Test raw dictionary overloads
    - Test Put(Dictionary<...>) builder pattern
    - Test PutAsync(Dictionary<...>) convenience method
    - Verify all PutItem options work with raw dictionaries
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x] 11.5 Test comprehensive wrapper generation
    - Test that all marked extension methods have generated wrappers
    - Test fluent chaining works through all wrapper methods
    - Test specialized wrappers correctly fix generic parameters
    - Verify Where() with LINQ expressions doesn't require TEntity parameter
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 6.2, 6.5_

- [x] 12. Update documentation
  - Update README with new API patterns
  - Add code examples demonstrating entity-specific builders
  - Add code examples demonstrating convenience method methods
  - Add code examples demonstrating raw dictionary support
  - Document migration path from old to new API
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 4.3_
