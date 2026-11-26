# Bugfix Requirements Document

## Introduction

An unfiltered `Scan()` fails against DynamoDB with a validation error ("ExpressionAttributeValues must not be empty"). `ScanRequestBuilder<TEntity>.ToScanRequest()` assigns an empty `Dictionary<string, AttributeValue>` to `ExpressionAttributeValues` when no values were added. The AWSSDK.DynamoDBv2 4.0.0 marshaller serializes an empty dictionary as `{"ExpressionAttributeValues":{},"TableName":"t"}`, whereas an unset property serializes as `{"TableName":"t"}`. DynamoDB rejects the empty map, so the property must be omitted when there are no values.

The defect affects every path that builds the request through `ToScanRequest()`: `ToDynamoDbResponseAsync()`, `ToListAsync()`, `ToCompositeEntityListAsync()`, and the FluentResults `ToListAsyncResult()` (which surfaces a `MappingError`). The Query, Put, Update, and Delete builders only set `ExpressionAttributeValues` when the value count is greater than zero, so Scan is the only builder affected. This is GitHub issue #229.

Reproduction: `await table.Entities.Scan().ToListAsync();` on a `[Scannable]` entity with no filter.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a scan is built with no filter and no expression attribute values, and `ToScanRequest()` is called THEN the system sets `ExpressionAttributeValues` to an empty `Dictionary<string, AttributeValue>`, which marshals to `"ExpressionAttributeValues":{}`

1.2 WHEN an unfiltered scan is executed via `ToListAsync()`, `ToDynamoDbResponseAsync()`, or `ToCompositeEntityListAsync()` THEN the system sends the empty `ExpressionAttributeValues` to DynamoDB, which rejects the request with a validation error

1.3 WHEN an unfiltered scan is executed via the FluentResults `ToListAsyncResult()` THEN the system returns a failed result containing a `MappingError` instead of the scanned items

1.4 WHEN a scan is built from a caller-supplied pre-built `ScanRequest` (for example via `WithRequest` or raw SDK access) whose `ExpressionAttributeValues` is an empty dictionary, no values are added by the builder, and `ToScanRequest()` is called THEN the system passes the empty dictionary through unchanged, so the marshalled request contains `"ExpressionAttributeValues":{}` and DynamoDB rejects it with a validation error

### Expected Behavior (Correct)

2.1 WHEN a scan is built with no filter and no expression attribute values, and `ToScanRequest()` is called THEN the system SHALL leave `ExpressionAttributeValues` null, so the marshalled request omits the property

2.2 WHEN an unfiltered scan is executed via `ToListAsync()`, `ToDynamoDbResponseAsync()`, or `ToCompositeEntityListAsync()` THEN the system SHALL send a request without `ExpressionAttributeValues` and return the scanned items without a validation error

2.3 WHEN an unfiltered scan is executed via the FluentResults `ToListAsyncResult()` THEN the system SHALL return a successful result containing the scanned items

2.4 WHEN `ToScanRequest()` is called THEN the system SHALL NOT emit an empty `ExpressionAttributeValues` map, whether the empty map came from the builder or from a caller-supplied pre-built request; an empty `ExpressionAttributeValues` on a caller-supplied request SHALL be normalized to null, correcting the caller's mistake, so the marshalled request omits the property

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a scan is built with a filter expression that adds one or more expression attribute values THEN the system SHALL CONTINUE TO set `ExpressionAttributeValues` to those values on the request

3.2 WHEN a scan is built from a pre-built `ScanRequest` that already has a non-empty `ExpressionAttributeValues` and no further values are added by the builder THEN the system SHALL CONTINUE TO preserve the caller's `ExpressionAttributeValues` unchanged

3.3 WHEN a scan sets other request properties (table name, index name, limit, projection, filter expression, expression attribute names, consistent read, exclusive start key, segment settings) THEN the system SHALL CONTINUE TO apply them to the request unchanged

3.4 WHEN Query, Put, Update, or Delete requests are built with no expression attribute values THEN the system SHALL CONTINUE TO leave `ExpressionAttributeValues` unset
