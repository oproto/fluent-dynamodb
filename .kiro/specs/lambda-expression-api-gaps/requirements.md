# Requirements Document

## Introduction

This document specifies requirements for adding missing lambda expression support to FluentDynamoDb request builders and entity accessors. Currently, lambda expressions work for Query and Update operations but are missing from Put, Delete, and Scan operations on entity accessors. This creates an inconsistent API where some operations support type-safe lambda conditions while others require format strings.

## Glossary

- **Entity Accessor**: Generated type-safe property on a table class providing direct access to entity operations (e.g., `table.Orders`)
- **Lambda Expression**: C# expression using `x => x.Property` syntax for type-safe property access
- **Request Builder**: Fluent builder class for constructing DynamoDB requests (e.g., `PutItemRequestBuilder`, `DeleteItemRequestBuilder`)
- **Where Clause**: Condition expression that must be satisfied for the operation to succeed
- **AttributeExists/AttributeNotExists**: DynamoDB condition functions checking if an attribute exists or doesn't exist

## Requirements

### Requirement 1

**User Story:** As a developer using entity accessors, I want to use Scan() on entity accessors, so that I have consistent API access to all DynamoDB operations.

#### Acceptance Criteria

1. WHEN a developer calls `table.Orders.Scan()` THEN the System SHALL return a `ScanRequestBuilder<Order>` configured for the Orders entity
2. WHEN a developer calls `table.Orders.Scan().WithFilter(x => x.Status == "active")` THEN the System SHALL compile and execute successfully

### Requirement 2

**User Story:** As a developer using Put operations, I want to use lambda expressions in Where() conditions, so that I can write type-safe conditional puts.

#### Acceptance Criteria

1. WHEN a developer calls `table.Put<Order>().Where(x => x.Pk.AttributeNotExists())` THEN the System SHALL compile and generate the correct condition expression
2. WHEN a developer calls `table.Orders.Put(order).Where(x => x.Pk.AttributeNotExists())` THEN the System SHALL compile and generate the correct condition expression
3. WHEN a developer uses `x.Property.AttributeExists()` in a Where lambda THEN the System SHALL generate `attribute_exists(propertyName)` condition
4. WHEN a developer uses `x.Property.AttributeNotExists()` in a Where lambda THEN the System SHALL generate `attribute_not_exists(propertyName)` condition

### Requirement 3

**User Story:** As a developer using Delete operations, I want to use lambda expressions in Where() conditions, so that I can write type-safe conditional deletes.

#### Acceptance Criteria

1. WHEN a developer calls `table.Delete<Order>().WithKey(...).Where(x => x.Pk.AttributeExists())` THEN the System SHALL compile and generate the correct condition expression
2. WHEN a developer calls `table.Orders.Delete(pk, sk).Where(x => x.Pk.AttributeExists())` THEN the System SHALL compile and generate the correct condition expression
3. WHEN a developer uses comparison operators like `x.Status == "active"` in a Where lambda THEN the System SHALL generate the correct comparison condition

### Requirement 4

**User Story:** As a developer, I want consistent lambda expression support across all request builders, so that I can use the same patterns regardless of operation type.

#### Acceptance Criteria

1. WHEN lambda Where() is available on UpdateItemRequestBuilder THEN the System SHALL provide equivalent lambda Where() on PutItemRequestBuilder
2. WHEN lambda Where() is available on UpdateItemRequestBuilder THEN the System SHALL provide equivalent lambda Where() on DeleteItemRequestBuilder
3. WHEN lambda WithFilter() is available on QueryRequestBuilder THEN the System SHALL provide equivalent lambda WithFilter() on ScanRequestBuilder via entity accessors

### Requirement 5

**User Story:** As a developer, I want the lambda expression extensions to work with both generic table methods and entity accessor methods, so that I have flexibility in how I write my code.

#### Acceptance Criteria

1. WHEN a developer uses `table.Put<Order>().Where(x => ...)` THEN the System SHALL behave identically to `table.Orders.Put().Where(x => ...)`
2. WHEN a developer uses `table.Delete<Order>().WithKey(...).Where(x => ...)` THEN the System SHALL behave identically to `table.Orders.Delete(...).Where(x => ...)`

### Requirement 6

**User Story:** As a developer, I want Scan operations to require explicit opt-in, so that I avoid accidentally performing expensive table scans.

#### Acceptance Criteria

1. WHEN an entity does not have the `[Scannable]` attribute THEN the System SHALL NOT generate Scan methods for that entity
2. WHEN an entity has the `[Scannable]` attribute THEN the System SHALL generate Scan methods on the entity accessor
3. WHEN a developer attempts to call `table.Scan<Order>()` without `[Scannable]` on Order THEN the System SHALL produce a compile-time error
4. WHEN a developer adds `[Scannable]` to an entity THEN the System SHALL generate `table.Entitys.Scan()` and `table.Scan()` (if default entity) methods
