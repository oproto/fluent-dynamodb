# Implementation Plan

- [x] 1. Create core logging interfaces and implementations
  - [x] 1.1 Define IDynamoDbLogger interface
    - Create interface with methods for all log levels (Trace, Debug, Information, Warning, Error, Critical)
    - Add IsEnabled method for performance optimization
    - Add overloads for logging with and without exceptions
    - Define LogLevel enum
    - _Requirements: 1.1, 1.2, 1.3_
  
  - [x] 1.2 Create NoOpLogger implementation
    - Implement IDynamoDbLogger with no-op methods
    - Make it a singleton with Instance property
    - Ensure IsEnabled always returns false
    - Ensure all methods have zero overhead
    - _Requirements: 1.4, 4.1_
  
  - [x] 1.3 Define LogEventIds constants
    - Create static class with event ID constants
    - Organize by category (1000-1999 mapping, 2000-2999 conversions, 3000-3999 operations, 9000-9999 errors)
    - Add XML documentation for each event ID
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [x] 2. Enhance DynamoDbTableBase with logger support
  - [x] 2.1 Add Logger property to DynamoDbTableBase
    - Add protected IDynamoDbLogger Logger property
    - Update constructor to accept optional logger parameter
    - Default to NoOpLogger.Instance when null
    - Maintain existing constructor for backward compatibility
    - _Requirements: 6.1, 6.2, 6.3, 14.1, 14.3_
  
  - [x] 2.2 Update generated table classes to pass logger
    - Modify source generator to include logger parameter in generated constructors
    - Pass logger from table instance to generated mapping methods
    - Ensure backward compatibility with existing constructors
    - _Requirements: 6.4, 6.5_

- [x] 3. Add logging to request builders
  - [x] 3.1 Add logger field to request builder base classes
    - Add private IDynamoDbLogger field to each request builder
    - Accept logger in constructor
    - Default to NoOpLogger.Instance when null
    - _Requirements: 9.1, 9.2_
  
  - [x] 3.2 Add logging to ExecuteAsync methods
    - Log operation start with table name and operation type at Information level
    - Log parameters at Trace level
    - Log operation completion with consumed capacity at Information level
    - Log errors at Error level with full context
    - Wrap in conditional compilation directives
    - _Requirements: 9.3, 9.4, 9.5, 10.1, 10.4_


- [x] 4. Enhance source generator for logging code generation
  - [x] 4.1 Create LoggingCodeGenerator class
    - Create class to generate logging code snippets
    - Add methods for generating entry/exit logging
    - Add methods for generating property mapping logging
    - Add methods for generating error logging
    - Support conditional compilation directives
    - _Requirements: 2.1, 2.2, 2.3, 10.1, 10.4_
  
  - [x] 4.2 Add logger parameter to generated mapping methods
    - Update ToDynamoDb signature to include optional IDynamoDbLogger parameter
    - Update FromDynamoDb signature to include optional IDynamoDbLogger parameter
    - Update async variants for blob references
    - _Requirements: 2.1, 2.4, 6.4_
  
  - [x] 4.3 Generate entry/exit logging in ToDynamoDb
    - Log method entry at Trace level with entity type
    - Log method exit at Trace level with attribute count
    - Wrap in conditional compilation directives
    - Use null-conditional operators for logger calls
    - _Requirements: 2.1, 2.3, 10.1, 10.4, 12.2_
  
  - [x] 4.4 Generate entry/exit logging in FromDynamoDb
    - Log method entry at Trace level with entity type and attribute count
    - Log method exit at Trace level with entity type
    - Wrap in conditional compilation directives
    - Use null-conditional operators for logger calls
    - _Requirements: 2.4, 2.5, 10.1, 10.4, 12.2_

- [x] 5. Generate property-level logging
  - [x] 5.1 Generate logging for basic property mapping
    - Log each property mapping at Debug level with property name and type
    - Check IsEnabled before logging to avoid parameter evaluation
    - Include property name and type in structured logging
    - Wrap in conditional compilation directives
    - _Requirements: 2.2, 2.5, 4.2, 7.2, 8.2_
  
  - [x] 5.2 Generate logging for skipped properties
    - Log when null or empty values are skipped at Debug level
    - Include property name and reason for skipping
    - Wrap in conditional compilation directives
    - _Requirements: 2.2, 8.3_
  
  - [x] 5.3 Generate logging for Map conversions
    - Log Map conversion start at Debug level with property name and element count
    - Include property name and element count in structured logging
    - Wrap in conditional compilation directives
    - _Requirements: 2.2, 7.2_
  
  - [x] 5.4 Generate logging for Set conversions
    - Log Set conversion start at Debug level with property name and element count
    - Include property name, set type, and element count in structured logging
    - Wrap in conditional compilation directives
    - _Requirements: 2.2, 7.2_
  
  - [x] 5.5 Generate logging for List conversions
    - Log List conversion start at Debug level with property name and element count
    - Include property name and element count in structured logging
    - Wrap in conditional compilation directives
    - _Requirements: 2.2, 7.2_
  
  - [x] 5.6 Generate logging for TTL conversions
    - Log TTL conversion at Debug level with property name
    - Include property name and conversion direction in structured logging
    - Wrap in conditional compilation directives
    - _Requirements: 2.2, 7.2_
  
  - [x] 5.7 Generate logging for JSON blob operations
    - Log JSON serialization/deserialization at Debug level with property name and serializer type
    - Include property name, type, and serializer in structured logging
    - Wrap in conditional compilation directives
    - _Requirements: 2.2, 7.2_
  
  - [x] 5.8 Generate logging for blob reference operations
    - Log blob storage operations at Debug level with property name and provider type
    - Include property name, reference key, and provider in structured logging
    - Wrap in conditional compilation directives
    - _Requirements: 2.2, 7.2_


- [x] 6. Generate error handling with logging
  - [x] 6.1 Generate try-catch blocks for mapping operations
    - Wrap entire ToDynamoDb/FromDynamoDb in try-catch
    - Log exceptions at Error level with entity type
    - Include entity type in structured logging
    - Re-throw exception after logging
    - Wrap in conditional compilation directives
    - _Requirements: 3.1, 13.1, 13.2, 13.5_
  
  - [x] 6.2 Generate try-catch blocks for type conversions
    - Wrap Map/Set/List conversions in try-catch
    - Log exceptions at Error level with property name, source type, and target type
    - Include property name, types, and value in structured logging
    - Re-throw as DynamoDbMappingException with context
    - Wrap in conditional compilation directives
    - _Requirements: 3.2, 3.3, 13.2, 13.3, 13.4_
  
  - [x] 6.3 Generate try-catch blocks for JSON serialization
    - Wrap JSON operations in try-catch
    - Log exceptions at Error level with property name, type, and serializer
    - Include property name, type, and serializer in structured logging
    - Re-throw as DynamoDbMappingException with context
    - Wrap in conditional compilation directives
    - _Requirements: 3.4, 13.2, 13.3, 13.4_
  
  - [x] 6.4 Generate try-catch blocks for blob storage
    - Wrap blob operations in try-catch
    - Log exceptions at Error level with reference key, provider type, and operation
    - Include reference key, provider, and operation in structured logging
    - Re-throw as DynamoDbMappingException with context
    - Wrap in conditional compilation directives
    - _Requirements: 3.5, 13.2, 13.3, 13.4_

- [x] 7. Create Microsoft.Extensions.Logging adapter package
  - [x] 7.1 Create Oproto.FluentDynamoDb.Logging.Extensions project
    - Create new .NET 8 project
    - Reference Microsoft.Extensions.Logging.Abstractions
    - Reference core Oproto.FluentDynamoDb project
    - _Requirements: 5.1, 13.1_
  
  - [x] 7.2 Implement MicrosoftExtensionsLoggingAdapter
    - Implement IDynamoDbLogger interface
    - Accept ILogger in constructor
    - Map log levels between IDynamoDbLogger and ILogger
    - Preserve event IDs in ILogger calls
    - Preserve structured logging parameters
    - _Requirements: 5.1, 5.2, 5.3, 5.5, 11.5_
  
  - [x] 7.3 Create extension methods for easy adapter creation
    - Add ToDynamoDbLogger extension on ILogger
    - Add ToDynamoDbLogger extension on ILoggerFactory
    - _Requirements: 5.1_
  
  - [x] 7.4 Support ILogger scopes
    - Implement scope support in adapter
    - Ensure scopes flow through to ILogger
    - _Requirements: 5.4_

- [x] 8. Add conditional compilation support
  - [x] 8.1 Wrap all generated logging code in #if !DISABLE_DYNAMODB_LOGGING
    - Add conditional compilation directives around all logging calls
    - Ensure code compiles with and without the directive
    - Test that logging is completely removed when disabled
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_
  
  - [x] 8.2 Update project templates to support conditional compilation
    - Add example of defining DISABLE_DYNAMODB_LOGGING in .csproj
    - Document how to enable/disable logging per configuration
    - _Requirements: 10.1, 10.4_


- [x] 9. Write unit tests for core logging
  - [x] 9.1 Test NoOpLogger implementation
    - Test IsEnabled always returns false
    - Test all log methods do not throw
    - Test singleton instance
    - _Requirements: 1.4, 4.1_
  
  - [x] 9.2 Test IDynamoDbLogger interface
    - Test interface can be implemented
    - Test all methods are callable
    - _Requirements: 1.1, 1.2, 1.3_
  
  - [x] 9.3 Test LogEventIds constants
    - Test all event IDs are unique
    - Test event IDs are in correct ranges
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [x] 10. Write unit tests for MEL adapter
  - [x] 10.1 Test MicrosoftExtensionsLoggingAdapter log level mapping
    - Test each log level maps correctly
    - Test IsEnabled delegates to ILogger
    - _Requirements: 5.2_
  
  - [x] 10.2 Test MicrosoftExtensionsLoggingAdapter log methods
    - Test LogTrace calls ILogger.LogTrace
    - Test LogDebug calls ILogger.LogDebug
    - Test LogInformation calls ILogger.LogInformation
    - Test LogWarning calls ILogger.LogWarning
    - Test LogError calls ILogger.LogError
    - Test LogCritical calls ILogger.LogCritical
    - _Requirements: 5.1, 5.3_
  
  - [x] 10.3 Test MicrosoftExtensionsLoggingAdapter event ID preservation
    - Test event IDs are passed to ILogger
    - _Requirements: 5.3, 11.5_
  
  - [x] 10.4 Test MicrosoftExtensionsLoggingAdapter exception logging
    - Test exceptions are passed to ILogger
    - Test exception context is preserved
    - _Requirements: 5.5_
  
  - [x] 10.5 Test extension methods
    - Test ToDynamoDbLogger on ILogger
    - Test ToDynamoDbLogger on ILoggerFactory
    - _Requirements: 5.1_

- [x] 11. Write integration tests for generated logging
  - [x] 11.1 Test generated ToDynamoDb with logger
    - Test entry logging is generated
    - Test property logging is generated
    - Test exit logging is generated
    - Test structured properties are included
    - _Requirements: 2.1, 2.2, 2.3, 7.1, 7.2_
  
  - [x] 11.2 Test generated FromDynamoDb with logger
    - Test entry logging is generated
    - Test property logging is generated
    - Test exit logging is generated
    - Test structured properties are included
    - _Requirements: 2.4, 2.5, 7.1, 7.2_
  
  - [x] 11.3 Test generated error logging
    - Test mapping errors are logged
    - Test conversion errors are logged
    - Test exceptions include full context
    - Test structured properties are included
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 7.4_
  
  - [x] 11.4 Test logging with null logger
    - Test generated code works with null logger
    - Test no NullReferenceException is thrown
    - _Requirements: 12.1, 12.2, 12.3, 12.4_
  
  - [x] 11.5 Test logging with NoOpLogger
    - Test generated code works with NoOpLogger
    - Test no logging output is produced
    - _Requirements: 1.4, 12.5_
  
  - [x] 11.6 Test conditional compilation
    - Test code compiles with DISABLE_DYNAMODB_LOGGING defined
    - Test no logging calls are present when disabled
    - Test code functions identically with logging disabled
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_


- [x] 12. Write performance tests
  - [x] 12.1 Test NoOpLogger has zero overhead
    - Benchmark mapping with NoOpLogger vs null logger
    - Verify performance difference is negligible (< 5%)
    - _Requirements: 4.1, 12.5_
  
  - [x] 12.2 Test IsEnabled check prevents parameter evaluation
    - Benchmark logging with IsEnabled check vs without
    - Verify expensive parameters are not evaluated when disabled
    - _Requirements: 4.2, 4.5_
  
  - [x] 12.3 Test conditional compilation eliminates overhead
    - Benchmark code with DISABLE_DYNAMODB_LOGGING vs without
    - Verify compiled code has no logging overhead when disabled
    - _Requirements: 10.2, 10.3_
  
  - [x] 12.4 Test logging allocation overhead
    - Measure allocations with logging enabled
    - Verify allocations are minimal and predictable
    - _Requirements: 4.3, 4.4_

- [x] 13. Write integration tests for request builders
  - [x] 13.1 Test QueryRequestBuilder logging
    - Test operation start is logged
    - Test parameters are logged at Trace level
    - Test operation completion is logged
    - Test consumed capacity is logged
    - Test errors are logged
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_
  
  - [x] 13.2 Test GetItemRequestBuilder logging
    - Test operation logging
    - Test error logging
    - _Requirements: 9.3, 9.5_
  
  - [x] 13.3 Test PutItemRequestBuilder logging
    - Test operation logging
    - Test error logging
    - _Requirements: 9.3, 9.5_
  
  - [x] 13.4 Test UpdateItemRequestBuilder logging
    - Test operation logging
    - Test error logging
    - _Requirements: 9.3, 9.5_
  
  - [x] 13.5 Test TransactWriteItemsRequestBuilder logging
    - Test operation logging
    - Test error logging
    - _Requirements: 9.3, 9.5_

- [x] 14. Update documentation
  - [x] 14.1 Create logging configuration guide
    - Document how to configure IDynamoDbLogger
    - Provide examples with no logger, custom logger, and MEL
    - Document logger parameter in constructors
    - _Requirements: 15.1, 15.2_
  
  - [x] 14.2 Document log levels and event IDs
    - Explain when each log level is used
    - Document event ID ranges and meanings
    - Provide examples of filtering by event ID
    - _Requirements: 15.3, 11.1, 11.2, 11.3, 11.4_
  
  - [x] 14.3 Document structured logging
    - Explain structured properties in log messages
    - Provide examples of querying structured logs
    - Show integration with Serilog, NLog, etc.
    - _Requirements: 15.4, 7.1, 7.2, 7.3, 7.4_
  
  - [x] 14.4 Document conditional compilation
    - Explain how to disable logging for production
    - Provide .csproj examples
    - Document performance implications
    - _Requirements: 15.5, 10.1, 10.4_
  
  - [x] 14.5 Create troubleshooting guide
    - Document common logging issues
    - Explain how to use logs for debugging AOT issues
    - Provide examples of log analysis
    - _Requirements: 15.1, 15.3_
  
  - [x] 14.6 Add code examples to README
    - Add basic usage example
    - Add MEL integration example
    - Add custom logger example
    - Add conditional compilation example
    - _Requirements: 15.1, 15.2, 15.5_

- [ ] 15. Update existing code for backward compatibility
  - [ ] 15.1 Ensure existing constructors still work
    - Test all existing constructor signatures compile
    - Test existing code works without logger parameter
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5_
  
  - [ ] 15.2 Ensure existing methods still work
    - Test all existing method signatures compile
    - Test existing code works without logger parameter
    - _Requirements: 14.4, 14.5_
  
  - [ ] 15.3 Test migration scenarios
    - Test upgrading from previous version
    - Test adding logger to existing code
    - Test removing logger from code
    - _Requirements: 14.1, 14.2, 14.3_
