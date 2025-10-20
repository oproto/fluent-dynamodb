# Implementation Plan

- [x] 1. Create new documentation structure
  - Create docs/getting-started/ directory with placeholder files
  - Create docs/core-features/ directory with placeholder files
  - Create docs/advanced-topics/ directory with placeholder files
  - Create docs/reference/ directory with placeholder files
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 9.1, 9.3, 9.4_

- [x] 2. Create documentation templates and navigation components
  - Create front matter template for metadata
  - Create breadcrumb navigation template
  - Create code example template with recommended pattern first
  - Create "See Also" section template
  - _Requirements: 6.2, 6.3, 7.1, 7.2_

- [x] 3. Restructure and update main README.md
  - [x] 3.1 Write library overview section (2-3 paragraphs)
    - Explain what the library does
    - Highlight key benefits (AOT-compatible, type-safe, fluent)
    - Describe target use cases
    - _Requirements: 8.1_
  
  - [x] 3.2 Create Quick Start section with source generation
    - Installation command
    - Complete entity definition example with attributes
    - Basic CRUD operations using expression formatting
    - Link to detailed getting started guide
    - _Requirements: 1.1, 1.2, 8.2_
  
  - [x] 3.3 Create Key Features section with links
    - Source generation benefits
    - Expression formatting
    - Composite entities
    - Custom client support
    - Batch operations and transactions
    - _Requirements: 8.3_
  
  - [x] 3.4 Create Documentation Guide navigation section
    - Link to getting-started/
    - Link to core-features/
    - Link to advanced-topics/
    - Link to reference/
    - _Requirements: 6.1, 8.3_
  
  - [x] 3.5 Create Approaches comparison section
    - Recommended: Source Generation + Expression Formatting
    - Also Available: Manual Patterns (with use cases)
    - Link to ManualPatterns.md
    - _Requirements: 5.1, 8.5, 10.2_

- [x] 4. Create Getting Started documentation
  - [x] 4.1 Write docs/getting-started/QuickStart.md
    - Prerequisites section
    - Installation instructions
    - First entity definition (complete example)
    - Basic operations (Put, Get, Query, Update, Delete)
    - Next steps with links
    - _Requirements: 1.1, 1.2, 7.3, 7.4_
  
  - [x] 4.2 Write docs/getting-started/Installation.md
    - NuGet package installation
    - Project requirements (.NET 8+, C# 12)
    - AWS SDK setup
    - Verifying source generator
    - IDE-specific notes
    - _Requirements: 1.2_
  
  - [x] 4.3 Write docs/getting-started/FirstEntity.md
    - Entity class requirements (partial keyword)
    - Property mapping with [DynamoDbAttribute]
    - Partition and sort keys
    - Generated code overview (Fields, Keys, Mapper)
    - Common patterns (composite keys, computed keys)
    - _Requirements: 1.2, 7.3_

- [x] 5. Create Core Features documentation
  - [x] 5.1 Write docs/core-features/EntityDefinition.md
    - Basic entity structure
    - Attribute mapping
    - Key definitions (partition, sort)
    - Computed keys with format strings
    - Extracted keys
    - Global Secondary Indexes
    - Queryable attributes
    - Best practices
    - _Requirements: 1.1, 1.2, 7.3_
  
  - [x] 5.2 Write docs/core-features/BasicOperations.md
    - Put operations (simple, conditional, batch)
    - Get operations (single, batch, projection)
    - Update operations (SET, ADD, REMOVE, DELETE)
    - Delete operations (simple, conditional, batch)
    - All examples use expression formatting
    - Note about manual patterns with link
    - _Requirements: 1.1, 1.2, 1.3, 4.1, 7.1, 7.2_
  
  - [x] 5.3 Write docs/core-features/QueryingData.md
    - Basic queries with expression formatting
    - Key condition expressions
    - Filter expressions
    - Pagination
    - GSI queries
    - Scan operations (with cost warnings)
    - Query optimization tips
    - _Requirements: 1.1, 1.2, 7.1, 7.2_
  
  - [x] 5.4 Write docs/core-features/ExpressionFormatting.md
    - Overview and benefits
    - Format specifier reference table
    - DateTime formatting examples
    - Numeric formatting examples
    - Enum handling
    - Reserved word handling with WithAttributeName
    - Complex expressions
    - Error handling and debugging
    - Mixing with manual parameters
    - _Requirements: 1.1, 2.3, 7.1, 7.2, 7.5_
  
  - [x] 5.5 Write docs/core-features/BatchOperations.md
    - Batch get operations (single/multiple tables)
    - Batch write operations (mixed put/delete)
    - Handling unprocessed keys/items
    - Performance considerations
    - Error handling and retries
    - _Requirements: 1.1, 1.2, 7.1_
  
  - [x] 5.6 Write docs/core-features/Transactions.md
    - Write transactions (Put, Update, Delete, ConditionCheck)
    - Expression formatting in transactions
    - Read transactions
    - Transaction limits
    - Error handling (TransactionCanceledException)
    - _Requirements: 1.1, 1.2, 7.1, 7.5_

- [x] 6. Create Advanced Topics documentation
  - [x] 6.1 Write docs/advanced-topics/CompositeEntities.md
    - Concept and use cases
    - Multi-item entities (collections)
    - Related entities ([RelatedEntity] attribute)
    - Sort key pattern matching
    - Single vs collection relationships
    - ToCompositeEntityAsync<T>() method
    - Performance considerations
    - Real-world examples (Order with items, Customer with addresses)
    - _Requirements: 1.1, 1.2, 7.1, 7.3_
  
  - [x] 6.2 Write docs/advanced-topics/GlobalSecondaryIndexes.md
    - GSI attribute configuration
    - Generated GSI field constants
    - Generated GSI key builders
    - Querying GSIs with expression formatting
    - Projection considerations
    - GSI design patterns
    - _Requirements: 1.1, 1.2, 7.1_
  
  - [x] 6.3 Write docs/advanced-topics/STSIntegration.md
    - Overview of .WithClient() method
    - Use cases (STS, custom configs, multi-region)
    - Creating custom DynamoDB client
    - Using .WithClient() in operations
    - Example: STS-scoped credentials
    - Performance considerations (client reuse)
    - _Requirements: 1.1, 1.2, 7.1_
  
  - [x] 6.4 Write docs/advanced-topics/PerformanceOptimization.md
    - Source generator performance benefits
    - Query optimization
    - Projection expressions
    - Batch operations vs individual calls
    - Pagination strategies
    - Consistent reads vs eventual consistency
    - Monitoring consumed capacity
    - Hot partition avoidance
    - _Requirements: 1.1, 7.1_
  
  - [x] 6.5 Write docs/advanced-topics/ManualPatterns.md
    - Introduction (when to use, recommended approach reminder)
    - Manual table pattern (without source generation)
    - Manual parameter binding (.WithValue() approach)
    - When manual patterns might be necessary
    - Examples for dynamic scenarios
    - Mixing approaches
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 5.1, 5.5, 10.1, 10.2_

- [x] 7. Create Reference documentation
  - [x] 7.1 Write docs/reference/AttributeReference.md
    - [DynamoDbTable] attribute
    - [DynamoDbAttribute] attribute
    - [PartitionKey] attribute
    - [SortKey] attribute
    - [GlobalSecondaryIndex] attribute
    - [Computed] attribute
    - [Extracted] attribute
    - [RelatedEntity] attribute
    - [QueryableAttribute] attribute
    - Each with purpose, parameters, examples
    - _Requirements: 6.4, 7.1_
  
  - [x] 7.2 Write docs/reference/FormatSpecifiers.md
    - Standard .NET format specifiers
    - Custom format strings
    - DateTime formats
    - Numeric formats
    - Examples for each specifier
    - Error messages and troubleshooting
    - _Requirements: 2.3, 6.4, 7.1_
  
  - [x] 7.3 Write docs/reference/ErrorHandling.md
    - Common DynamoDB exceptions
    - Conditional check failures
    - Throughput exceptions
    - Validation errors
    - Retry strategies
    - FluentResults integration (optional)
    - _Requirements: 7.1, 7.5_
  
  - [x] 7.4 Write docs/reference/Troubleshooting.md
    - Source generator issues
    - Runtime errors
    - Performance issues
    - Build and compilation issues
    - Each with error message, cause, solution, see also
    - _Requirements: 6.4, 7.1_

- [x] 8. Update existing documentation files
  - [x] 8.1 Update docs/README.md as documentation hub
    - Overview of documentation structure
    - Links to all major sections
    - Quick navigation guide
    - _Requirements: 6.1, 8.3_
  
  - [x] 8.2 Review and update docs/MigrationGuide.md
    - Rename to docs/reference/AdoptionGuide.md
    - Update to focus on choosing approaches
    - Side-by-side comparisons
    - When to use each pattern
    - Mixing approaches
    - _Requirements: 5.1, 5.2, 5.3, 5.4_
  
  - [x] 8.3 Review and consolidate docs/CodeExamples.md
    - Extract examples to appropriate topic files
    - Keep only unique examples not covered elsewhere
    - Update all examples to use recommended patterns first
    - Add notes about manual patterns with links
    - _Requirements: 1.3, 2.1, 2.2, 4.2_
  
  - [x] 8.4 Review and update docs/DeveloperGuide.md
    - Ensure it focuses on source generation
    - Update all examples to use expression formatting
    - Add links to new documentation structure
    - Remove redundant content now in other files
    - _Requirements: 1.1, 1.2, 2.1, 2.5_
  
  - [x] 8.5 Review and update docs/SourceGeneratorGuide.md
    - Ensure consistency with new structure
    - Update examples to use expression formatting
    - Add cross-references to new files
    - _Requirements: 1.1, 1.2, 2.1_
  
  - [x] 8.6 Archive or remove docs/STSIntegrationGuide.md
    - Extract minimal .WithClient() content to new STSIntegration.md
    - Remove application-level patterns
    - _Requirements: 2.1, 2.5_
  
  - [x] 8.7 Review and update docs/PerformanceOptimizationGuide.md
    - Consolidate with new PerformanceOptimization.md
    - Remove redundant content
    - _Requirements: 2.1, 2.5_

- [x] 9. Update USAGE_EXAMPLES.md
  - Remove file or consolidate into appropriate topic files
  - Content is redundant with new structure
  - _Requirements: 2.1, 2.2, 2.5_

- [x] 10. Add navigation and cross-references
  - [x] 10.1 Add breadcrumb navigation to all new files
    - Use consistent format
    - Link back to parent sections
    - _Requirements: 6.2_
  
  - [x] 10.2 Add "See Also" sections to all files
    - Link to related topics
    - Use consistent format
    - _Requirements: 6.3_
  
  - [x] 10.3 Add front matter metadata to all files
    - Title, category, order, keywords, related
    - _Requirements: 6.4_

- [x] 11. Create documentation index and search aids
  - [x] 11.1 Create docs/INDEX.md with comprehensive topic list
    - Alphabetical index of all topics
    - Links to relevant sections
    - _Requirements: 6.1, 6.4_
  
  - [x] 11.2 Create docs/QUICK_REFERENCE.md
    - Common operations with syntax
    - Quick lookup table
    - Links to detailed docs
    - _Requirements: 6.5_

- [x] 12. Validation and cleanup
  - [x] 12.1 Validate all internal links
    - Create script to check all markdown links
    - Fix broken links
    - _Requirements: 6.3_
  
  - [x] 12.2 Review terminology consistency
    - Ensure consistent use of terms throughout
    - Update glossary if needed
    - _Requirements: 7.1_
  
  - [x] 12.3 Review code example consistency
    - Ensure all examples follow template
    - Verify syntax highlighting
    - Check for completeness
    - _Requirements: 7.1, 7.2, 7.3, 7.4_
  
  - [x] 12.4 Final review of documentation hierarchy
    - Verify logical flow
    - Check navigation works
    - Ensure no orphaned files
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 6.1, 6.2_
