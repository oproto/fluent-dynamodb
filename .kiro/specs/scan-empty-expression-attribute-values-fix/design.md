# Scan Empty ExpressionAttributeValues Bugfix Design

## Overview

An unfiltered `Scan()` fails against DynamoDB because `ScanRequestBuilder<TEntity>.ToScanRequest()` assigns an empty `Dictionary<string, AttributeValue>` to `ScanRequest.ExpressionAttributeValues` when no values were added. AWSSDK.DynamoDBv2 4.x treats an empty, non-null dictionary as "set" and marshals it as `"ExpressionAttributeValues":{}`. DynamoDB rejects the empty map with a validation error. This is GitHub issue #229.

The same failure occurs when a caller supplies a pre-built `ScanRequest` (through `WithRequest` or raw SDK access) whose `ExpressionAttributeValues` is already an empty dictionary and adds no values through the builder. The current code passes that empty dictionary through unchanged.

The decision for the fix is that `ToScanRequest()` must never emit an empty `ExpressionAttributeValues` map. The method now has three outcomes:

1. The builder holds values (`Count > 0`): assign them, as before.
2. The builder holds no values and the underlying request's `ExpressionAttributeValues` is a non-null empty dictionary: set it to `null`.
3. Otherwise (the property is `null`, or a non-empty caller-supplied dictionary with no builder values): leave it unchanged.

The existing `else if (_req.ExpressionAttributeValues == null)` branch that creates an empty dictionary is removed and replaced with the normalizing branch from outcome 2. This matches the intent of `QueryRequestBuilder`, `PutItemRequestBuilder`, `UpdateItemRequestBuilder`, and `DeleteItemRequestBuilder`, which never emit an empty map from builder state.

Side effect: `_req` is the caller's `ScanRequest` instance after `WithRequest(ScanRequest)`, so normalizing the property mutates the caller's object. `ToScanRequest()` already mutates that object (it assigns names and values onto `_req`), so this is consistent with existing behavior and is intended.

Two existing unit tests encode the buggy behavior (`NotBeNull()` + `HaveCount(0)`) and must change. New tests cover the regression (including the marshalled JSON), the empty caller-supplied dictionary, and the preserved behaviors.

## Glossary

- **Bug_Condition (C)**: A call to `ToScanRequest()` where the builder holds no expression attribute values and the underlying `ScanRequest.ExpressionAttributeValues` is either `null` (the builder would create an empty dictionary today) or a non-null empty dictionary (the caller supplied one and it passes through today).
- **Property (P)**: After `ToScanRequest()`, `ExpressionAttributeValues` is `null`, so the marshalled request omits the property.
- **Preservation**: Behavior for every input outside C, including filtered scans, pre-built requests carrying a non-empty dictionary with no builder values, and all other request properties.
- **`ToScanRequest()`**: Method in `Oproto.FluentDynamoDb/Requests/ScanRequestBuilder.cs` (~lines 335-352) that copies builder state (`_attrN`, `_attrV`) onto the internal `ScanRequest` (`_req`) and returns it.
- **`_attrV`**: The builder's `AttributeValueInternal` collection that accumulates values added through `WithValue`, `WithFilter`, and the expression translators.
- **`_req`**: The internal `ScanRequest`. It is replaced by the caller's instance when `WithRequest(ScanRequest)` is used, so changes to `_req` are visible on the caller's object.
- **`ScanRequestMarshaller`**: `Amazon.DynamoDBv2.Model.Internal.MarshallTransformations.ScanRequestMarshaller`, the SDK component that serializes a `ScanRequest` to the JSON body sent to DynamoDB.

## Bug Details

### Bug Condition

The bug manifests whenever `ToScanRequest()` runs on a builder with no expression attribute values and the underlying request's `ExpressionAttributeValues` is not a non-empty dictionary. There are two sources of the empty map:

- **Builder-created**: `ExpressionAttributeValues` is `null`, and the builder's `else if` branch replaces `null` with a new empty dictionary.
- **Caller-supplied**: `ExpressionAttributeValues` is already an empty dictionary on the pre-built request, and the builder leaves it in place.

The SDK's `IsSetExpressionAttributeValues` check treats a non-null empty dictionary as set (collection initialization is off by default in SDK v4), so the marshaller writes `{}` in both cases.

Every execution path calls `ToScanRequest()`: `ToDynamoDbResponseAsync()`, `ToListAsync()` and `ToCompositeEntityListAsync()` in `EntityExecuteAsyncExtensions`, and the FluentResults `ToListAsyncResult()` (which wraps the resulting `AmazonDynamoDBException` as a `MappingError`).

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type ScanBuilderState
         (attrV: count of builder-held expression attribute values,
          reqValues: ExpressionAttributeValues on the underlying ScanRequest, nullable)
  OUTPUT: boolean

  RETURN input.attrV.Count == 0
         AND (input.reqValues == null OR input.reqValues.Count == 0)
END FUNCTION
```

### Examples

- `table.Entities.Scan().ToListAsync()` on a `[Scannable]` entity. Expected: request body `{"TableName":"t"}`. Actual: `{"ExpressionAttributeValues":{},"TableName":"t"}`, rejected by DynamoDB.
- `new ScanRequestBuilder<T>(client).ForTable("t").ToScanRequest()`. Expected: `ExpressionAttributeValues == null`. Actual: empty dictionary.
- `builder.WithValue(":status", "active", conditionalUse: false).ToScanRequest()`. No value is added, so this is in C. Expected: `null`. Actual: empty dictionary. (This is the second existing test that encodes the bug.)
- `builder.WithValue(":status", (string?)null).ToScanRequest()` with default conditional behavior adds nothing. Expected: `null`. Actual: empty dictionary. (The first existing test that encodes the bug.)
- `builder.WithRequest(new ScanRequest { TableName = "t", ExpressionAttributeValues = new Dictionary<string, AttributeValue>() }).ToScanRequest()` with no further values. In C. Expected: `null`, and the marshalled body omits the property. Actual: the empty dictionary is passed through and marshals as `{}`. The caller's `ScanRequest` instance also has `ExpressionAttributeValues == null` afterwards.
- Edge case, not in C: `builder.WithRequest(new ScanRequest { ExpressionAttributeValues = { [":cat"] = ... } }).ToScanRequest()` with no further values. Expected and actual: the caller's non-empty dictionary is preserved (same reference).

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- A scan with a filter that adds one or more values sets `ExpressionAttributeValues` to those values (the `_attrV.AttributeValues.Count > 0` assignment stays).
- A pre-built `ScanRequest` with a non-empty `ExpressionAttributeValues`, and no values added through the builder, keeps the caller's dictionary unchanged (same reference, same entries).
- Table name, index name, limit, projection, filter expression, expression attribute names, consistent read, exclusive start key, and segment settings are applied exactly as before.
- Query, Put, Update, and Delete builders keep leaving `ExpressionAttributeValues` unset when empty.

**Scope:**
All inputs where `_attrV` holds at least one value, or where the underlying request carries a non-empty `ExpressionAttributeValues` and the builder holds no values, are unaffected. The only observable change is for inputs in C: the property goes from an empty dictionary (builder-created or caller-supplied) to `null`.

## Hypothesized Root Cause

The cause is confirmed by reading the code; no further hypothesis is needed.

1. **Defensive initialization**: `ToScanRequest()` contains `else if (_req.ExpressionAttributeValues == null) { _req.ExpressionAttributeValues = new Dictionary<string, AttributeValue>(); }`. The branch appears intended to avoid a null reference for consumers, but the other four request builders never did this.
2. **No normalization of caller-supplied empties**: when the property is already a non-null empty dictionary, no branch touches it, so an empty map supplied through `WithRequest` or raw SDK access reaches the marshaller.
3. **SDK v4 marshalling semantics**: `ScanRequest.IsSetExpressionAttributeValues()` returns true for a non-null empty dictionary when `AWSConfigs.InitializeCollections` is false, so the empty map is serialized.
4. **Test coverage gap**: Two tests assert `NotBeNull()` and `HaveCount(0)`, which locked in the defect. No test inspected the marshalled request, and no test covered a caller-supplied empty dictionary.

Consumer audit (searched `Oproto.FluentDynamoDb`, `.FluentResults`, `.Streams`, `.Logging.Extensions`, and all test projects):
- `ScanRequestBuilder.ToDynamoDbResponseAsync()` logs `request.TableName`, `IndexName`, `FilterExpression`, `Segment`, and `TotalSegments`, and the trace log reads `_attrV.AttributeValues.Count`. It never reads `request.ExpressionAttributeValues`, so a null value is safe.
- `EntityExecuteAsyncExtensions.ToListAsync` and `ToCompositeEntityListAsync` (Scan overloads, ~lines 367 and 445) pass the request straight to `ScanAsync`. They do not read the property.
- FluentResults wraps those same extension methods and only reads the response, so it needs no change. It will return success once the DynamoDB error disappears.
- The encryption and blob-storage code that reads `request.ExpressionAttributeValues` lives in `UpdateItemRequestBuilder` and `PutItemRequestBuilder`, not in Scan.
- No test other than the two named below asserts a non-null or empty `ExpressionAttributeValues` on a Scan request. `LambdaExpressionApiGapsPropertyTests` already null-coalesces the property.

## Correctness Properties

Property 1: Bug Condition - Scan Never Emits an Empty ExpressionAttributeValues Map

_For any_ scan builder state where the bug condition holds (isBugCondition returns true), that is, the builder holds no values and the underlying request's `ExpressionAttributeValues` is `null` or an empty dictionary, the fixed `ToScanRequest()` SHALL return a request whose `ExpressionAttributeValues` is `null`, and the request marshalled by `ScanRequestMarshaller` SHALL NOT contain an `ExpressionAttributeValues` property.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

Property 2: Preservation - Scans With Values or Non-Empty Caller-Supplied Values

_For any_ scan builder state where the bug condition does NOT hold (isBugCondition returns false), that is, the builder holds at least one value, or the builder holds none and the underlying request carries a non-empty `ExpressionAttributeValues`, the fixed `ToScanRequest()` SHALL produce the same request as the original function. This preserves values added through the builder, a non-empty caller-supplied `ExpressionAttributeValues` on a pre-built request, and all other request properties.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4**

## Fix Implementation

### Changes Required

**File**: `Oproto.FluentDynamoDb/Requests/ScanRequestBuilder.cs`

**Function**: `ToScanRequest()`

**Specific Changes**:
1. **Replace the empty-dictionary branch**: delete the `else if (_req.ExpressionAttributeValues == null) { ... = new Dictionary<string, AttributeValue>(); }` block and add `else if (_req.ExpressionAttributeValues is { Count: 0 }) { _req.ExpressionAttributeValues = null; }`. The pattern `is { Count: 0 }` is false for `null`, so a null property is left alone, and false for a non-empty dictionary, so a caller-supplied non-empty dictionary is left alone.
2. **Keep the `Count > 0` assignment** unchanged.
3. **Add a comment** mirroring `DeleteItemRequestBuilder` ("Do NOT emit an empty ExpressionAttributeValues dictionary; DynamoDB rejects it"), noting that an empty caller-supplied map is normalized to null.

Resulting method:
```csharp
public ScanRequest ToScanRequest()
{
    if (_attrN.AttributeNames.Count > 0)
    {
        _req.ExpressionAttributeNames = _attrN.AttributeNames;
    }

    if (_attrV.AttributeValues.Count > 0)
    {
        _req.ExpressionAttributeValues = _attrV.AttributeValues;
    }
    else if (_req.ExpressionAttributeValues is { Count: 0 })
    {
        // Do NOT emit an empty ExpressionAttributeValues dictionary.
        // DynamoDB rejects requests with an empty ExpressionAttributeValues map,
        // so normalize an empty caller-supplied map (WithRequest / raw SDK access) to null.
        // This mutates the caller's ScanRequest, as ToScanRequest() already does for other properties.
        _req.ExpressionAttributeValues = null;
    }
    return _req;
}
```

**Behavior matrix** (builder values = `_attrV.AttributeValues.Count`):

| Builder values | `_req.ExpressionAttributeValues` before | After `ToScanRequest()` | Changed from today? |
|----------------|------------------------------------------|--------------------------|---------------------|
| > 0 | any | builder values | No |
| 0 | `null` | `null` | Yes (was empty dictionary) |
| 0 | empty dictionary | `null` | Yes (was passed through) |
| 0 | non-empty dictionary | same dictionary | No |

**File**: `Oproto.FluentDynamoDb.UnitTests/Requests/ScanRequestBuilderTests.cs`

1. **`WithValueStringNullSuccess` (~lines 551-553)**: replace `NotBeNull()` + `HaveCount(0)` with `req.ExpressionAttributeValues.Should().BeNull();` and keep the explanatory comment.
2. **`WithValueConditionalUseFalseSuccess` (~lines 645-647)**: same replacement.
3. **Add new tests** as listed under Testing Strategy.

**File**: `Oproto.FluentDynamoDb.UnitTests/Requests/DirectSdkRequestPropertyTests.cs`

1. **`GenerateScanRequest()` (~line 415)**: extend the generator so `ExpressionAttributeValues` is randomly `null`, an empty dictionary, or a non-empty dictionary. The current generator never sets it.
2. **`ScanRequest_WithRequest_PreservesAllProperties` (~line 102)**: keep the existing property assertions and add an `ExpressionAttributeValues` assertion. Capture whether the input was empty before calling `ToScanRequest()` (the call mutates the input). Expected result: a `null` input stays `null`, an empty input becomes `null`, and a non-empty input is returned as the same reference with the same entries.

**File**: `CHANGELOG.md`

Add an entry to the existing `### Fixed` list under `## [Unreleased]`:

> - **Unfiltered `Scan()` Rejected by DynamoDB Because of Empty `ExpressionAttributeValues`** — `ScanRequestBuilder<TEntity>.ToScanRequest()` no longer emits an empty `Dictionary<string, AttributeValue>` for `ExpressionAttributeValues`. AWSSDK.DynamoDBv2 4.x marshalled an empty dictionary as `"ExpressionAttributeValues":{}`, which DynamoDB rejects, so `Scan().ToListAsync()`, `ToDynamoDbResponseAsync()`, `ToCompositeEntityListAsync()`, and the FluentResults `ToListAsyncResult()` failed for scans without a filter. When the builder has no values, the property is now `null` and omitted from the request, matching the Query, Put, Update, and Delete builders. An empty `ExpressionAttributeValues` supplied by the caller on a pre-built `ScanRequest` (through `WithRequest` or raw SDK access) is also normalized to `null`, which updates that `ScanRequest` instance. Values added through the builder and a non-empty `ExpressionAttributeValues` on a pre-built `ScanRequest` are unchanged. Fixes #229.

No change to `docs/DOCUMENTATION_CHANGELOG.md` or `.kiro/steering/fluentdynamodb.md` is needed because no documented API pattern changes.

## Testing Strategy

### Validation Approach

First surface the bug on unfixed code with the new regression tests, then apply the fix and confirm both the fix checks and the preservation checks pass.

### Exploratory Bug Condition Checking

**Goal**: Show the bug on the UNFIXED code and confirm the root cause (the `else if` branch, the lack of normalization for caller-supplied empties, and SDK marshalling of empty collections).

**Test Plan**: Add the regression tests below before changing `ScanRequestBuilder.cs` and run them. They are expected to fail.

**Test Cases**:
1. **Unfiltered request is null**: `ForTable("t").ToScanRequest()` asserts `ExpressionAttributeValues` is null (fails on unfixed code: empty dictionary).
2. **Marshalled JSON omits the property**: marshal the request and assert the body does not contain `ExpressionAttributeValues` (fails on unfixed code: body contains `"ExpressionAttributeValues":{}`).
3. **Conditional skip paths**: `WithValue(..., conditionalUse: false)` and `WithValue(":x", (string?)null)` assert null (fail on unfixed code).
4. **Execution path**: call `ToListAsync()` with a substituted `IAmazonDynamoDB` and use `Arg.Do<ScanRequest>` to capture the request. Assert `ExpressionAttributeValues` is null (fails on unfixed code).
5. **Pre-built request with an empty dictionary**: `WithRequest(new ScanRequest { TableName = "t", ExpressionAttributeValues = new Dictionary<string, AttributeValue>() }).ToScanRequest()` asserts `ExpressionAttributeValues` is null and the marshalled JSON omits the property (fails on unfixed code: the empty dictionary is passed through and marshals as `{}`). Also assert that the caller's original `ScanRequest` instance now has a null property, to document the intended mutation.

**Expected Counterexamples**:
- `ExpressionAttributeValues` is a non-null dictionary with `Count == 0`.
- The marshalled body is `{"ExpressionAttributeValues":{},"TableName":"t"}`.
- The cause is the `else if` branch in `ToScanRequest()` (builder-created case) and the missing normalization (caller-supplied case).

### Fix Checking

**Goal**: For all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := ToScanRequest_fixed(input)
  ASSERT result.ExpressionAttributeValues == null
  ASSERT marshalledJson(result) does not contain "ExpressionAttributeValues"
END FOR
```

### Preservation Checking

**Goal**: For all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT ToScanRequest_original(input) = ToScanRequest_fixed(input)
END FOR
```

**Testing Approach**: Property-based testing (FsCheck, already used in `DirectSdkRequestPropertyTests` and `EmptyConditionalExpressionPropertyTests`) suits preservation because it varies the number and types of values and the combination of other request properties.

**Test Plan**: The existing filtered-scan tests (`ScanRequestBuilderTests` ~lines 509-700 and 787, `ScannableTableTests`, `BuilderIntegrationTests`, `ComprehensiveBuilderIntegrationTests`) already pass on unfixed code. Re-run them unchanged after the fix, and add the tests below.

**Test Cases**:
1. **Filtered scan keeps values**: `WithFilter("#status = :status").WithValue(":status", "active")` yields a dictionary with one entry. The marshalled JSON contains `ExpressionAttributeValues`.
2. **Pre-built request with a non-empty dictionary**: `WithRequest(new ScanRequest { ExpressionAttributeValues = { [":cat"] = ... } })` then `ToScanRequest()` returns the same dictionary (reference-equal, same entries). This is the narrowed requirement 3.2.
3. **Pre-built request without values**: `WithRequest(new ScanRequest { TableName = "t" })` leaves the property null.
4. **Builder values replace an empty caller-supplied dictionary**: `WithRequest` with an empty dictionary, then a filter that adds a value, yields the builder's values (the `Count > 0` branch wins, as before).
5. **Other properties unchanged**: an unfiltered scan with table name, index, limit, projection, segment, and consistent read set still returns those values.
6. **Other builders unchanged**: existing Query/Put/Update/Delete tests continue to pass (no code touched).

### Unit Tests

- Update the two existing assertions in `ScanRequestBuilderTests.cs` (~551-553 and ~645-647) to `BeNull()`.
- New in `ScanRequestBuilderTests.cs`: unfiltered `ToScanRequest()` returns null `ExpressionAttributeValues`.
- New: marshalled-JSON test. Use `new ScanRequestMarshaller().Marshall(request)` (the marshaller is constructed directly; it has no static `Instance` property in AWSSDK.DynamoDBv2 4.0.0), which returns an `IRequest` whose `Content` is UTF-8 JSON. Assert the decoded body does not contain `ExpressionAttributeValues`. Add a paired assertion that a filtered scan's body does contain it, so the check cannot pass vacuously.
- New: pre-built request with an empty dictionary yields null from `ToScanRequest()`, and the marshalled JSON omits the property. Also assert the caller's `ScanRequest` instance is the one mutated (`ReferenceEquals(result, callerRequest)` and `callerRequest.ExpressionAttributeValues` is null).
- New: pre-built request with a non-empty dictionary is preserved (same reference), and a pre-built request with none stays null.
- New: `ToListAsync()` through a substituted client sends a `ScanRequest` with null `ExpressionAttributeValues` (captured via NSubstitute `Arg.Do`).

### Property-Based Tests

- For any generated set of 0..N values added via `WithValue`, `ToScanRequest().ExpressionAttributeValues` is null when N == 0 and has exactly N entries otherwise.
- For any generated `ScanRequest` (reuse `GenerateScanRequest()` from `DirectSdkRequestPropertyTests`, extended so `ExpressionAttributeValues` is null, empty, or non-empty) passed to `WithRequest`, the result's `ExpressionAttributeValues` is null when the input was null or empty, and is the same non-empty dictionary otherwise. The existing `ScanRequest_WithRequest_PreservesAllProperties` property is updated accordingly: empty dictionaries are expected to become null, and every other property is still expected to be preserved.

### Integration Tests

- The FluentResults test project already mocks `ScanAsync`. Add one test that `ToListAsyncResult()` on an unfiltered builder succeeds and the captured request has null `ExpressionAttributeValues`.
- `Oproto.FluentDynamoDb.IntegrationTests` (DynamoDB Local) is the only place the server-side rejection is reproducible. If a scan test already exists there, make sure an unfiltered `Scan().ToListAsync()` case is included. No new infrastructure is needed.
