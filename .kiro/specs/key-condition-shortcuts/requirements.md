# Requirements Document

## Introduction

This feature adds convenience methods and an enum to simplify common conditional patterns for Put, Update, and Delete operations. Currently, developers must write verbose `Where(x => x.Pk.AttributeExists() && x.Sk.AttributeExists())` conditions repeatedly. This enhancement provides `IfExists()`, `IfNotExists()`, and `KeyCondition` enum to reduce boilerplate while maintaining full flexibility.

## Glossary

- **KeyCondition**: An enum specifying whether to check for key attribute existence before an operation
- **IfExists**: A builder method that adds `attribute_exists()` conditions for all key attributes
- **IfNotExists**: A builder method that adds `attribute_not_exists()` conditions for all key attributes
- **Composite_Key**: An entity with both partition key and sort key attributes
- **Simple_Key**: An entity with only a partition key attribute

## Requirements

### Requirement 1: KeyCondition Enum Definition

**User Story:** As a developer, I want a clear enum to express key existence conditions, so that I can specify conditions concisely in convenience methods.

#### Acceptance Criteria

1. THERE SHALL be a `KeyCondition` enum with values `None`, `MustExist`, and `MustNotExist`
2. `KeyCondition.None` SHALL be the default value (0) indicating no automatic condition
3. `KeyCondition.MustExist` SHALL indicate that key attributes must exist
4. `KeyCondition.MustNotExist` SHALL indicate that key attributes must not exist

### Requirement 2: Builder Methods for Key Conditions

**User Story:** As a developer, I want `IfExists()` and `IfNotExists()` methods on request builders, so that I can fluently add key existence conditions.

#### Acceptance Criteria

1. `PutItemRequestBuilder<TEntity>` SHALL have `IfExists()` and `IfNotExists()` methods returning the builder for chaining
2. `UpdateItemRequestBuilder<TEntity>` SHALL have `IfExists()` and `IfNotExists()` methods returning the builder for chaining
3. `DeleteItemRequestBuilder<TEntity>` SHALL have `IfExists()` and `IfNotExists()` methods returning the builder for chaining
4. `IfExists()` SHALL be equivalent to calling `WithKeyCondition(KeyCondition.MustExist)`
5. `IfNotExists()` SHALL be equivalent to calling `WithKeyCondition(KeyCondition.MustNotExist)`

### Requirement 3: Automatic Condition Generation for Simple Keys

**User Story:** As a developer, I want key conditions to automatically generate the correct DynamoDB expression for entities with only a partition key, so that I don't have to know the key structure.

#### Acceptance Criteria

1. WHEN `KeyCondition.MustExist` is set on an entity with only a partition key, THEN the builder SHALL generate `attribute_exists(pk)` where `pk` is the partition key attribute name
2. WHEN `KeyCondition.MustNotExist` is set on an entity with only a partition key, THEN the builder SHALL generate `attribute_not_exists(pk)` where `pk` is the partition key attribute name
3. WHEN `KeyCondition.None` is set, THEN the builder SHALL NOT add any automatic condition

### Requirement 4: Automatic Condition Generation for Composite Keys

**User Story:** As a developer, I want key conditions to automatically generate the correct DynamoDB expression for entities with composite keys, so that both key attributes are checked.

#### Acceptance Criteria

1. WHEN `KeyCondition.MustExist` is set on an entity with partition key and sort key, THEN the builder SHALL generate `attribute_exists(pk) AND attribute_exists(sk)` where `pk` and `sk` are the respective attribute names
2. WHEN `KeyCondition.MustNotExist` is set on an entity with partition key and sort key, THEN the builder SHALL generate `attribute_not_exists(pk) AND attribute_not_exists(sk)` where `pk` and `sk` are the respective attribute names
3. THE generated condition SHALL use the actual DynamoDB attribute names from entity metadata, not property names

### Requirement 5: Combining Key Conditions with Existing Where Clauses

**User Story:** As a developer, I want to combine key conditions with additional Where clauses, so that I can add business logic conditions alongside key existence checks.

#### Acceptance Criteria

1. WHEN both a key condition and a `Where()` clause are specified, THEN the builder SHALL combine them with AND
2. THE key condition SHALL be added first, followed by the user's Where clause
3. WHEN only a key condition is specified without a Where clause, THEN only the key condition SHALL be used
4. WHEN only a Where clause is specified without a key condition, THEN only the Where clause SHALL be used (existing behavior)

### Requirement 6: Convenience Method Parameters for Put Operations

**User Story:** As a developer, I want to specify key conditions directly in convenience methods like `PutAsync()`, so that I can write concise one-liners for common patterns.

#### Acceptance Criteria

1. Generated `PutAsync(entity)` convenience methods SHALL have an optional `KeyCondition` parameter defaulting to `None`
2. Generated `PutAsyncResult(entity)` convenience methods SHALL have an optional `KeyCondition` parameter defaulting to `None`
3. WHEN `KeyCondition.MustNotExist` is passed to `PutAsync()`, THEN the operation SHALL fail if the item already exists
4. WHEN `KeyCondition.MustExist` is passed to `PutAsync()`, THEN the operation SHALL fail if the item does not exist

### Requirement 7: Convenience Method Parameters for Update Operations

**User Story:** As a developer, I want to specify key conditions when creating Update builders, so that I can prevent accidental upserts.

#### Acceptance Criteria

1. Generated `Update(pk)` methods for simple key entities SHALL have an optional `KeyCondition` parameter defaulting to `None`
2. Generated `Update(pk, sk)` methods for composite key entities SHALL have an optional `KeyCondition` parameter defaulting to `None`
3. WHEN `KeyCondition.MustExist` is passed to `Update()`, THEN the operation SHALL fail if the item does not exist (preventing upsert)
4. WHEN `KeyCondition.MustNotExist` is passed to `Update()`, THEN the operation SHALL fail if the item already exists

### Requirement 8: Convenience Method Parameters for Delete Operations

**User Story:** As a developer, I want to specify key conditions in Delete convenience methods, so that I can ensure I'm deleting an existing item.

#### Acceptance Criteria

1. Generated `DeleteAsync(pk)` methods for simple key entities SHALL have an optional `KeyCondition` parameter defaulting to `None`
2. Generated `DeleteAsync(pk, sk)` methods for composite key entities SHALL have an optional `KeyCondition` parameter defaulting to `None`
3. WHEN `KeyCondition.MustExist` is passed to `DeleteAsync()`, THEN the operation SHALL fail if the item does not exist
4. WHEN `KeyCondition.None` is passed (default), THEN the delete SHALL be idempotent (existing behavior)

### Requirement 9: Transaction and Batch Compatibility

**User Story:** As a developer, I want key conditions to work within transactions and batch operations, so that I can use the same patterns everywhere.

#### Acceptance Criteria

1. WHEN a builder with a key condition is added to a transaction via `DynamoDbTransactions.Write`, THEN the condition SHALL be included in the transaction item
2. WHEN a builder with a key condition is added to a batch via `DynamoDbBatch.Write`, THEN the condition SHALL be included in the batch item
3. THE builder methods (`IfExists()`, `IfNotExists()`) SHALL work identically whether used standalone or within transactions/batches

### Requirement 10: Error Handling

**User Story:** As a developer, I want clear error messages when key conditions fail, so that I can handle failures appropriately.

#### Acceptance Criteria

1. WHEN a key condition fails, DynamoDB SHALL throw `ConditionalCheckFailedException` (existing SDK behavior)
2. WHEN using FluentResults, the failure SHALL be mapped to `OptimisticLockingError` (existing mapping)
3. THE error message SHALL clearly indicate that a key condition check failed
