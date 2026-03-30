# Requirements Document

## Introduction

This feature adds native support for table-spanning operations in FluentDynamoDb, enabling queries and operations across multiple DynamoDB tables that share the same entity schema. The primary use case is time-series data where entities are partitioned across tables by time period (e.g., monthly, quarterly, yearly tables), but the architecture supports other spanning strategies such as active/inactive partitioning or geographic distribution.

The feature introduces a repository-level abstraction that sits above the generated table classes, providing a unified interface for cross-table operations while leveraging the existing source-generated table infrastructure.

## Glossary

- **Spanned_Table**: A logical table that spans multiple physical DynamoDB tables sharing the same entity schema
- **Table_Span**: A single physical DynamoDB table that is part of a spanned table collection
- **Span_Index**: A user-defined DynamoDB table that maps span identifiers to physical table names
- **Span_Resolver**: A component that determines which physical table(s) contain data for a given query
- **Cross_Table_Query**: A query operation that transparently executes across multiple table spans and aggregates results
- **Span_Strategy**: The partitioning strategy used to distribute data across table spans (e.g., time-series, active/inactive)
- **Table_Instance_Factory**: A factory that creates instances of generated table classes for specific physical table names
- **Span_Cache**: An optional cache layer for span index lookups to reduce DynamoDB reads
- **Cross_Table_Response**: An aggregated response from a cross-table query including items, pagination, and capacity metrics

## Requirements

### Requirement 1: Spanned Table Definition

**User Story:** As a developer, I want to define a spanned table that manages multiple physical DynamoDB tables sharing the same entity schema, so that I can work with partitioned data through a unified interface.

#### Acceptance Criteria

1. THE Source_Generator SHALL generate a partial spanned table class when an entity is decorated with a spanning attribute
2. WHEN a spanned table is instantiated, THE Spanned_Table SHALL accept a span resolver and table instance factory
3. THE Spanned_Table SHALL implement an interface that exposes the underlying table type for type-safe operations
4. WHEN accessing entity operations, THE Spanned_Table SHALL provide the same fluent API as single-table operations
5. THE Spanned_Table SHALL support generic type parameters for both the index entity type and the table entity type

### Requirement 2: Span Index Table Integration

**User Story:** As a developer, I want to use my own DynamoDB table to store span index information, so that I can control the schema and access patterns for span lookups.

#### Acceptance Criteria

1. THE Span_Resolver SHALL accept a user-defined entity type for the span index table
2. WHEN resolving spans, THE Span_Resolver SHALL query the user's index table using the standard FluentDynamoDb query API
3. THE Span_Resolver SHALL support a configurable mapping function to extract table names from index entities
4. IF the span index query returns no results, THEN THE Span_Resolver SHALL return an empty span list rather than throwing
5. THE Span_Resolver SHALL support both single-span and multi-span resolution based on query parameters

### Requirement 3: Time-Series Span Strategy

**User Story:** As a developer, I want to partition my data by time periods (monthly, quarterly, yearly), so that I can efficiently manage and query time-series data.

#### Acceptance Criteria

1. THE Time_Series_Strategy SHALL support configurable period lengths (1, 3, 6, or 12 months)
2. WHEN given a date, THE Time_Series_Strategy SHALL calculate the correct period start date
3. WHEN given a date range, THE Time_Series_Strategy SHALL enumerate all periods that overlap the range
4. THE Time_Series_Strategy SHALL support both ascending and descending period enumeration
5. IF a date is in the future and future tables are disabled, THEN THE Time_Series_Strategy SHALL throw a descriptive exception

### Requirement 4: Table Instance Factory

**User Story:** As a developer, I want the spanned table to create instances of my generated table class for each physical table, so that I can leverage the full type-safe API for each span.

#### Acceptance Criteria

1. THE Table_Instance_Factory SHALL create table instances using the same DynamoDB client and options as the spanned table
2. WHEN creating a table instance, THE Table_Instance_Factory SHALL use the resolved physical table name
3. THE Table_Instance_Factory SHALL support caching of table instances to avoid repeated instantiation
4. THE Table_Instance_Factory SHALL be injectable to allow custom table creation logic
5. WHEN the factory creates a table instance, THE instance SHALL be fully functional with all generated methods

### Requirement 5: Single-Span Operations

**User Story:** As a developer, I want to perform CRUD operations on a specific span, so that I can read and write data to the correct physical table.

#### Acceptance Criteria

1. WHEN performing a Put operation, THE Spanned_Table SHALL resolve the target span and delegate to the appropriate table instance
2. WHEN performing a Get operation with a known span identifier, THE Spanned_Table SHALL query only that span's table
3. WHEN performing an Update operation, THE Spanned_Table SHALL resolve the target span and delegate to the appropriate table instance
4. WHEN performing a Delete operation, THE Spanned_Table SHALL resolve the target span and delegate to the appropriate table instance
5. THE Spanned_Table SHALL expose a method to get a specific table instance by span identifier for direct access

### Requirement 6: Cross-Table Query Operations

**User Story:** As a developer, I want to query across multiple table spans and receive aggregated results, so that I can retrieve data that spans multiple time periods or partitions.

#### Acceptance Criteria

1. WHEN executing a cross-table query, THE Spanned_Table SHALL resolve all relevant spans and query each in sequence
2. THE Cross_Table_Query SHALL aggregate results from multiple tables into a single response
3. THE Cross_Table_Query SHALL support a page size limit that applies across all spans
4. WHEN a span query reaches the page size limit, THE Cross_Table_Query SHALL stop and return a pagination token
5. THE Cross_Table_Query SHALL track and aggregate consumed capacity from all queried tables
6. THE Cross_Table_Query SHALL support both ascending and descending sort order across spans

### Requirement 7: Cross-Table Pagination

**User Story:** As a developer, I want to paginate through cross-table query results, so that I can efficiently retrieve large result sets spanning multiple tables.

#### Acceptance Criteria

1. THE Cross_Table_Response SHALL include a pagination token that encodes the current span and position
2. WHEN resuming a paginated query, THE Spanned_Table SHALL decode the token and continue from the correct span and position
3. THE pagination token SHALL be opaque to consumers and encode span identifier and DynamoDB LastEvaluatedKey
4. IF the pagination token is invalid or corrupted, THEN THE Spanned_Table SHALL throw a descriptive exception
5. THE Cross_Table_Response SHALL indicate whether more results are available across all spans

### Requirement 8: Span Caching

**User Story:** As a developer, I want to cache span index lookups, so that I can reduce DynamoDB reads and improve query performance.

#### Acceptance Criteria

1. THE Span_Cache SHALL be an optional component that can be injected into the span resolver
2. WHEN a span is resolved, THE Span_Resolver SHALL first check the cache before querying the index table
3. THE Span_Cache SHALL support configurable TTL for cached entries
4. THE Span_Cache SHALL support cache invalidation by span identifier
5. IF no cache is provided, THEN THE Span_Resolver SHALL query the index table directly without caching

### Requirement 9: Capacity Tracking

**User Story:** As a developer, I want to track consumed capacity across all tables in a cross-table operation, so that I can monitor and optimize my DynamoDB costs.

#### Acceptance Criteria

1. THE Cross_Table_Response SHALL include aggregated consumed capacity across all queried tables
2. THE Cross_Table_Response SHALL include per-table consumed capacity breakdown
3. THE Cross_Table_Response SHALL track the number of query operations executed
4. THE Cross_Table_Response SHALL track scanned count and result count separately
5. WHEN capacity tracking is disabled in options, THE Spanned_Table SHALL not request capacity information

### Requirement 10: Error Handling

**User Story:** As a developer, I want clear error messages when span operations fail, so that I can diagnose and fix issues quickly.

#### Acceptance Criteria

1. IF a span cannot be resolved, THEN THE Spanned_Table SHALL throw a SpanNotFoundException with the span identifier
2. IF a table instance cannot be created, THEN THE Spanned_Table SHALL throw a TableInstanceCreationException with details
3. IF a cross-table query fails on one span, THEN THE Spanned_Table SHALL include the span identifier in the exception
4. THE Spanned_Table SHALL support a configurable error handling strategy (fail-fast or continue-on-error)
5. WHEN using continue-on-error strategy, THE Cross_Table_Response SHALL include a list of failed spans with their exceptions

### Requirement 11: Source Generator Integration

**User Story:** As a developer, I want the source generator to create the necessary infrastructure for spanned tables, so that I get compile-time type safety and IntelliSense support.

#### Acceptance Criteria

1. THE Source_Generator SHALL recognize a SpannedTable attribute on entity classes
2. WHEN an entity has the SpannedTable attribute, THE Source_Generator SHALL generate a partial spanned table class
3. THE generated spanned table class SHALL include typed accessors for the underlying entity operations
4. THE Source_Generator SHALL generate appropriate interfaces for span resolution and table factory
5. THE Source_Generator SHALL emit diagnostics for invalid spanned table configurations

### Requirement 12: Extensible Span Strategies

**User Story:** As a developer, I want to implement custom span strategies beyond time-series, so that I can partition data according to my application's needs.

#### Acceptance Criteria

1. THE Span_Strategy SHALL be defined as an interface that can be implemented by users
2. THE interface SHALL define methods for resolving spans from query parameters
3. THE interface SHALL define methods for determining the target span for write operations
4. THE Spanned_Table SHALL accept any implementation of the span strategy interface
5. THE library SHALL provide built-in implementations for time-series and active/inactive strategies
