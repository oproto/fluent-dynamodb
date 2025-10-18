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

- [ ]* 19. Create comprehensive documentation and examples
  - Write developer guide for using DynamoDB source generator
  - Create migration guide from manual mapping to generated code
  - Add code examples for common scenarios (single entities, multi-item, related entities)
  - Document STS integration patterns and best practices
  - Create troubleshooting guide for common issues and error messages
  - _Requirements: 10.4, 14.5_

- [ ]* 20. Performance optimization and advanced features
  - Optimize generated code for performance (minimize allocations, efficient mapping)
  - Add caching for expensive operations like EntityMetadata generation
  - Implement incremental source generation for better build performance
  - Add support for custom type converters and advanced mapping scenarios
  - Prepare foundation for future LINQ expression support
  - _Requirements: 11.1, 11.2, 11.3, 11.4, 13.5_