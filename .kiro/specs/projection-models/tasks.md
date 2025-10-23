# Implementation Plan

- [x] 1. Create projection model attributes and core infrastructure
  - Create DynamoDbProjectionAttribute class with SourceEntityType property
  - Create UseProjectionAttribute class for GSI projection enforcement
  - Add projection-related diagnostic descriptors (PROJ001-PROJ006, PROJ101-PROJ102)
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 2. Implement projection model analysis in source generator
  - [x] 2.1 Create ProjectionModel and ProjectionPropertyModel data classes
    - Define ProjectionModel with ClassName, Namespace, SourceEntityType, Properties, ProjectionExpression, DiscriminatorProperty, DiscriminatorValue
    - Define ProjectionPropertyModel with PropertyName, PropertyType, AttributeName, IsNullable, SourceProperty
    - _Requirements: 1.1, 1.2_
  
  - [x] 2.2 Create ProjectionModelAnalyzer class
    - Implement AnalyzeProjection method to detect [DynamoDbProjection] attributes
    - Implement ValidateProjectionProperties to ensure all properties exist on source entity
    - Implement ValidatePropertyTypes to ensure type compatibility
    - Emit PROJ001 diagnostic for missing properties
    - Emit PROJ002 diagnostic for type mismatches
    - Emit PROJ003 diagnostic for invalid source entity types
    - Emit PROJ004 diagnostic for non-partial projection classes
    - _Requirements: 1.2, 1.3, 1.4, 5.1, 5.2, 5.3, 5.4, 5.5_
  
  - [x] 2.3 Integrate ProjectionModelAnalyzer into DynamoDbSourceGenerator
    - Add syntax provider for [DynamoDbProjection] attribute detection
    - Call ProjectionModelAnalyzer during source generation pipeline
    - Collect and report diagnostics
    - _Requirements: 1.1, 1.5_

- [x] 3. Implement projection expression generation
  - [x] 3.1 Create ProjectionExpressionGenerator class
    - Implement GenerateProjectionExpression to create DynamoDB projection strings
    - Map property names to DynamoDB attribute names using [DynamoDbAttribute]
    - Include discriminator property in projection if source entity uses discriminators
    - Handle nullable properties correctly
    - _Requirements: 2.1, 2.2, 2.3, 4.2_
  
  - [x] 3.2 Generate projection metadata classes
    - Create static metadata class for each projection model
    - Include projection expression as string constant
    - Include property mapping information
    - Include discriminator information if applicable
    - _Requirements: 2.1, 2.2, 4.2_
  
  - [x] 3.3 Generate FromDynamoDb methods for projection models
    - Generate partial class with FromDynamoDb static method
    - Map DynamoDB AttributeValues to projection properties
    - Handle nullable properties and missing attributes
    - Use generated mapping code (no reflection)
    - _Requirements: 2.2, 2.3, 6.1, 6.2, 6.3_

- [ ] 4. Implement GSI index property generation
  - [ ] 4.1 Create GsiDefinition model class
    - Define GsiDefinition with IndexName, EntityTypes, ProjectionType, ProjectionExpression, PartitionKeyProperty, SortKeyProperty
    - _Requirements: 3.1, 3.2, 3a.1, 3a.2_
  
  - [ ] 4.2 Create TableIndexGenerator class
    - Implement GroupGsiDefinitions to aggregate GSI definitions across entities
    - Detect [UseProjection] attributes on GSI properties
    - Resolve projection expressions for GSIs with [UseProjection]
    - Handle multiple entities sharing the same GSI name
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3a.1, 3a.2, 3a.3, 3a.4, 3a.5_
  
  - [ ] 4.3 Generate index properties on table classes
    - Generate DynamoDbIndex<TProjection> properties for GSIs with [UseProjection]
    - Generate non-generic DynamoDbIndex properties for GSIs without projection
    - Include projection expression in constructor call
    - Ensure one property per unique GSI name
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 7.1, 7.2, 7.3, 7.4, 7.5_
  
  - [ ] 4.4 Integrate TableIndexGenerator into source generation pipeline
    - Detect table classes with GSI definitions
    - Generate partial table class with index properties
    - Handle tables with no GSI definitions
    - _Requirements: 7.1, 7.2, 7.5_

- [ ] 5. Implement generic DynamoDbIndex class
  - [ ] 5.1 Create DynamoDbIndex<TDefault> class
    - Add constructor accepting table, indexName, and optional projectionExpression
    - Implement Name property
    - Implement Query property that returns QueryRequestBuilder with auto-applied projection
    - Implement QueryAsync method for default type
    - Implement QueryAsync<TResult> method for type override
    - _Requirements: 2.4, 2.5, 3.1, 3.2, 3.3, 6.1, 6.2, 6.3_
  
  - [ ] 5.2 Update non-generic DynamoDbIndex class
    - Add constructor overload accepting projectionExpression
    - Auto-apply projection in Query property if configured
    - Maintain backward compatibility with existing constructor
    - _Requirements: 6.1, 6.2, 6.3, 7a.1, 7a.2, 7a.3, 7a.4, 7a.5_

- [ ] 6. Implement query builder extensions for projection
  - [ ] 6.1 Create ToListAsync<TResult> extension method
    - Detect if TResult is a projection model using generated metadata
    - Auto-apply projection expression if TResult is a projection model
    - Skip auto-projection if manual .WithProjection() was called
    - Execute query and return List<TResult>
    - _Requirements: 2.1, 2.2, 2.4, 2.5, 6.1, 6.2, 6.3_
  
  - [ ] 6.2 Implement projection hydration logic
    - Call generated FromDynamoDb method for projection models
    - Handle DynamoDB response items
    - Map AttributeValues to projection properties
    - Handle missing attributes gracefully
    - _Requirements: 2.2, 2.3, 6.1, 6.2, 6.3_
  
  - [ ] 6.3 Implement GSI projection validation
    - Check if query is using a GSI with [UseProjection]
    - Validate TResult matches the required projection type (if enforced)
    - Throw ProjectionValidationException if validation fails
    - Include GSI name, expected type, and actual type in error message
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [ ] 7. Implement discriminator support for multi-entity projections
  - [ ] 7.1 Detect discriminator properties in projection analysis
    - Check if source entity has EntityDiscriminator configured
    - Include discriminator property in projection expression
    - Store discriminator value in ProjectionModel
    - _Requirements: 4.1, 4.2, 4.3_
  
  - [ ] 7.2 Implement discriminator-based routing in hydration
    - Read discriminator value from DynamoDB item
    - Route to correct projection type based on discriminator
    - Handle items with unknown discriminator values
    - Throw DiscriminatorMismatchException if discriminator doesn't match
    - _Requirements: 4.1, 4.3, 4.4_
  
  - [ ] 7.3 Support multiple projection types in single query
    - Handle query results containing multiple entity types
    - Route each item to its corresponding projection type
    - Return heterogeneous list if needed (or skip incompatible items)
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

- [ ] 8. Add validation and diagnostics
  - [ ] 8.1 Implement compilation-time validations
    - Validate projection properties exist on source entity (PROJ001)
    - Validate property type compatibility (PROJ002)
    - Validate source entity type exists (PROJ003)
    - Validate projection class is partial (PROJ004)
    - Validate UseProjection references valid projection (PROJ005)
    - Detect conflicting UseProjection attributes (PROJ006)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_
  
  - [ ] 8.2 Implement warnings for suboptimal configurations
    - Warn if projection includes all properties (PROJ101)
    - Warn if projection has many properties (PROJ102)
    - _Requirements: 5.5, 8.1, 8.2, 8.3, 8.4, 8.5_
  
  - [ ] 8.3 Implement runtime validation
    - Validate GSI projection constraints at query time
    - Provide clear error messages with GSI name and type information
    - Handle validation failures gracefully
    - _Requirements: 3.5, 7a.4_

- [ ] 9. Implement manual configuration support
  - [ ] 9.1 Add manual projection configuration API
    - Update DynamoDbIndex constructor to accept projection expression
    - Update DynamoDbIndex<TDefault> constructor for manual use
    - Document manual configuration patterns
    - _Requirements: 7a.1, 7a.2, 7a.3, 7a.4, 7a.5_
  
  - [ ] 9.2 Implement precedence rules
    - Manual .WithProjection() overrides automatic projection
    - Manual DynamoDbIndex configuration overrides generated
    - Document precedence behavior
    - _Requirements: 6.4, 7a.5_

- [ ] 10. Add comprehensive documentation
  - Create code examples for projection model definition
  - Document GSI projection enforcement with [UseProjection]
  - Document manual configuration for non-source-generation users
  - Document type override patterns with ToListAsync<TResult>
  - Document discriminator support for multi-entity queries
  - Document projection application rules and precedence
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

- [ ] 11. Implement backward compatibility measures
  - Ensure existing manual DynamoDbIndex instantiation continues to work
  - Ensure existing .WithProjection() calls continue to work
  - Ensure existing ToListAsync() usage with full entities is unchanged
  - Verify no breaking changes to existing APIs
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_
