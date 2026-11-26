# Implementation Plan

Work happens on branch `fix/229-scan-empty-expression-attribute-values`. Create it from the current base branch before starting task 1 (`git checkout -b fix/229-scan-empty-expression-attribute-values`).

Marshalling note for all tests below: construct the SDK marshaller directly with `new ScanRequestMarshaller().Marshall(request)` (namespace `Amazon.DynamoDBv2.Model.Internal.MarshallTransformations`). It was verified to work with AWSSDK.DynamoDBv2 4.0.0 and returns an `IRequest` whose `Content` is the UTF-8 JSON body. Do not rely on a static `Instance` property.

Behavior matrix for `ToScanRequest()` (builder values = `_attrV.AttributeValues.Count`), used by the tests below:

| Builder values | `_req.ExpressionAttributeValues` before | After `ToScanRequest()` | In bug condition? |
|----------------|------------------------------------------|--------------------------|-------------------|
| > 0 | any | builder values | No |
| 0 | `null` | `null` | Yes |
| 0 | empty dictionary | `null` (caller's instance is mutated, intended) | Yes |
| 0 | non-empty dictionary | same dictionary (reference-equal) | No |

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Unfiltered Scan Omits ExpressionAttributeValues
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: The bug is deterministic, so scope the property to the concrete failing cases from `isBugCondition`: zero builder-held values and an underlying `ExpressionAttributeValues` that is either `null` (builder-created empty map) or an empty dictionary (caller-supplied empty map)
  - Add the tests to `Oproto.FluentDynamoDb.UnitTests/Requests/ScanRequestBuilderTests.cs` (xUnit + FluentAssertions + NSubstitute, following the existing naming style `{MethodName}{Scenario}`)
  - Test cases (all assert the Expected Behavior from design, `ExpressionAttributeValues == null`):
    - `new ScanRequestBuilder<T>(client).ForTable("t").ToScanRequest()` has null `ExpressionAttributeValues`
    - Marshal the request with `new ScanRequestMarshaller().Marshall(request)`, decode `Content` as UTF-8, and assert the body does not contain `ExpressionAttributeValues`
    - `WithValue(":x", "v", conditionalUse: false)` then `ToScanRequest()` yields null
    - `WithValue(":x", (string?)null)` with default conditional behavior then `ToScanRequest()` yields null
    - Execution path: call `ToListAsync()` with a substituted `IAmazonDynamoDB`, capture the request with `Arg.Do<ScanRequest>`, and assert null `ExpressionAttributeValues`
    - **Pre-built empty dictionary case**: `WithRequest(callerRequest)` where `callerRequest = new ScanRequest { TableName = "t", ExpressionAttributeValues = new Dictionary<string, AttributeValue>() }`, no further values, then `ToScanRequest()`:
      - the result has null `ExpressionAttributeValues`
      - the marshalled body does not contain `ExpressionAttributeValues`
      - `ReferenceEquals(result, callerRequest)` is true and `callerRequest.ExpressionAttributeValues` is null afterwards (documents the intended mutation of the caller's instance)
  - Run on UNFIXED code: `dotnet test Oproto.FluentDynamoDb.UnitTests --filter "FullyQualifiedName~ScanRequestBuilderTests"`
  - **EXPECTED OUTCOME**: Tests FAIL (this is correct - it proves the bug exists)
  - Document counterexamples found (expected: non-null dictionary with `Count == 0`; marshalled body `{"ExpressionAttributeValues":{},"TableName":"t"}`; for the pre-built case the caller's empty dictionary passes through unchanged; causes are the `else if` branch in `ToScanRequest()` and the missing normalization of caller-supplied empties)
  - Mark task complete when the tests are written, run, and the failures are documented
  - _Requirements: 1.1, 1.2, 1.4, 2.1, 2.2, 2.4_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Scans With Values or Non-Empty Caller-Supplied Values
  - **IMPORTANT**: Follow observation-first methodology
  - Observe on UNFIXED code: a filtered scan (`WithFilter("#status = :status").WithValue(":status", "active")`) returns a dictionary with one entry and its marshalled body contains `ExpressionAttributeValues`
  - Observe on UNFIXED code: `WithRequest(new ScanRequest { ExpressionAttributeValues = { [":cat"] = ... } })` (non-empty) then `ToScanRequest()` returns the same dictionary instance with the same entries
  - Observe on UNFIXED code: `WithRequest(new ScanRequest { TableName = "t" })` (null dictionary) with a filter that adds a value yields the builder's values
  - Observe on UNFIXED code: `WithRequest` with an empty dictionary followed by a filter that adds a value yields the builder's values (the `Count > 0` branch wins; this input is outside the bug condition because the builder holds a value)
  - Observe on UNFIXED code: table name, index name, limit, projection, filter expression, expression attribute names, consistent read, exclusive start key, and segment settings are carried onto the request
  - Write example-based tests in `ScanRequestBuilderTests.cs`:
    - Filtered scan keeps its values, and the marshalled body contains `ExpressionAttributeValues` (this paired assertion keeps the JSON check from passing vacuously)
    - **Non-empty preserved case**: pre-built request with a non-empty dictionary and no builder values is preserved (reference-equal, same entries)
    - Builder values replace an empty caller-supplied dictionary (pre-built request with an empty dictionary, then a filter that adds a value, yields exactly the builder's values)
    - Unfiltered scan with table name, index, limit, projection, segment, and consistent read set still returns those values
  - Write property-based tests with FsCheck (already used in `DirectSdkRequestPropertyTests` and `EmptyConditionalExpressionPropertyTests`) in a new file `Oproto.FluentDynamoDb.UnitTests/Requests/ScanExpressionAttributeValuesPropertyTests.cs`:
    - For any generated set of N distinct values added via `WithValue`, `ToScanRequest().ExpressionAttributeValues` has exactly N entries when N > 0 (the N == 0 case belongs to the bug condition and is asserted after the fix in task 3.2)
    - For any generated `ScanRequest` whose `ExpressionAttributeValues` is a non-empty dictionary, passed to `WithRequest` with no builder values, `ToScanRequest()` returns that same dictionary reference with the same entries. Empty and null dictionaries are excluded here because the empty case is in the bug condition. `GenerateScanRequest()` in `DirectSdkRequestPropertyTests` is private, so write a small local generator rather than changing its visibility
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when the tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 3. Fix for unfiltered Scan sending empty ExpressionAttributeValues (GitHub issue #229)

  - [x] 3.1 Implement the fix
    - In `Oproto.FluentDynamoDb/Requests/ScanRequestBuilder.cs`, `ToScanRequest()` (~lines 335-352), replace the `else if (_req.ExpressionAttributeValues == null) { _req.ExpressionAttributeValues = new Dictionary<string, AttributeValue>(); }` block with:
      ```csharp
      else if (_req.ExpressionAttributeValues is { Count: 0 })
      {
          // Do NOT emit an empty ExpressionAttributeValues dictionary.
          // DynamoDB rejects requests with an empty ExpressionAttributeValues map,
          // so normalize an empty caller-supplied map (WithRequest / raw SDK access) to null.
          // This mutates the caller's ScanRequest, as ToScanRequest() already does for other properties.
          _req.ExpressionAttributeValues = null;
      }
      ```
    - The `is { Count: 0 }` pattern is false for `null` (left null) and for a non-empty dictionary (caller's dictionary preserved by reference), so only an empty dictionary is normalized
    - Keep the `_attrV.AttributeValues.Count > 0` assignment and the `_attrN` assignment unchanged
    - Do not touch other builders; no async code is modified, so no `.ConfigureAwait(false)` changes
    - Restart the build server if needed so the library rebuilds cleanly: `dotnet build-server shutdown`
    - _Bug_Condition: isBugCondition(input) where input.attrV.Count == 0 AND (input.reqValues == null OR input.reqValues.Count == 0)_
    - _Expected_Behavior: ToScanRequest().ExpressionAttributeValues == null AND marshalled JSON omits "ExpressionAttributeValues"_
    - _Preservation: Scans with values, pre-built requests carrying a non-empty dictionary, other request properties, and Query/Put/Update/Delete builders behave as before_
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 3.4_

  - [x] 3.2 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Unfiltered Scan Omits ExpressionAttributeValues
    - **IMPORTANT**: Re-run the SAME tests from task 1 - do NOT write new tests
    - The tests from task 1 encode the expected behavior, including the pre-built empty dictionary case
    - Run: `dotnet test Oproto.FluentDynamoDb.UnitTests --filter "FullyQualifiedName~ScanRequestBuilderTests"`
    - **EXPECTED OUTCOME**: Tests PASS (confirms bug is fixed)
    - Also add, now that the fix is in, the N == 0 case to the property test from task 2 (zero values yields null)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.3 Update the existing tests that encode or cover the bug
    - In `Oproto.FluentDynamoDb.UnitTests/Requests/ScanRequestBuilderTests.cs`, `WithValueStringNullSuccess` (~lines 551-553): replace `NotBeNull()` + `HaveCount(0)` with `req.ExpressionAttributeValues.Should().BeNull();` and keep the explanatory comment (null string values are not added when conditionalUse is true)
    - In the same file, `WithValueConditionalUseFalseSuccess` (~lines 645-647): replace the same pair with `req.ExpressionAttributeValues.Should().BeNull();` and keep the comment (conditionalUse: false means nothing is added)
    - In `Oproto.FluentDynamoDb.UnitTests/Requests/DirectSdkRequestPropertyTests.cs`, extend `GenerateScanRequest()` (~line 415) so `ExpressionAttributeValues` is randomly `null`, an empty dictionary, or a non-empty dictionary (it currently never sets it). Keep the helper private
    - In the same file, update `ScanRequest_WithRequest_PreservesAllProperties` (~line 102): keep every existing property assertion and add an `ExpressionAttributeValues` assertion. **Capture "was empty" (and the original dictionary reference and entries for the non-empty case) BEFORE calling `ToScanRequest()`**, because the call mutates the generated input. Expected result: a `null` input stays `null`, an empty input becomes `null`, and a non-empty input is returned as the same reference with the same entries
    - Confirm no other test asserts a non-null or empty `ExpressionAttributeValues` on a Scan request: `grep -rn "ExpressionAttributeValues" Oproto.FluentDynamoDb.UnitTests` and review Scan-related hits
    - Run: `dotnet test Oproto.FluentDynamoDb.UnitTests --filter "FullyQualifiedName~ScanRequestBuilderTests|FullyQualifiedName~DirectSdkRequestPropertyTests"`
    - _Requirements: 2.1, 2.4, 3.2, 3.3_

  - [x] 3.4 Add FluentResults regression test (optional)
    - In `Oproto.FluentDynamoDb.FluentResults.UnitTests/FluentResultsExtensionsTests.cs`, add a test next to `ToListAsyncResult_Scan_Success_ReturnsOkResult`: `ToListAsyncResult` on an unfiltered scan builder returns a successful result, and the `ScanRequest` captured via `Arg.Do` has null `ExpressionAttributeValues`
    - Run: `dotnet test Oproto.FluentDynamoDb.FluentResults.UnitTests`
    - _Requirements: 2.3_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** - Scans With Values or Non-Empty Caller-Supplied Values
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run the preservation tests from task 2 (including the non-empty preserved case) plus the existing filtered-scan tests unchanged (`ScanRequestBuilderTests`, `ScannableTableTests`, `BuilderIntegrationTests`, `ComprehensiveBuilderIntegrationTests`, `LambdaExpressionApiGapsPropertyTests`)
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 4. Update CHANGELOG.md
  - Add this entry verbatim from `design.md` (File: `CHANGELOG.md` section) to the existing `### Fixed` list under `## [Unreleased]` in `CHANGELOG.md`:
    - **Unfiltered `Scan()` Rejected by DynamoDB Because of Empty `ExpressionAttributeValues`** — `ScanRequestBuilder<TEntity>.ToScanRequest()` no longer emits an empty `Dictionary<string, AttributeValue>` for `ExpressionAttributeValues`. AWSSDK.DynamoDBv2 4.x marshalled an empty dictionary as `"ExpressionAttributeValues":{}`, which DynamoDB rejects, so `Scan().ToListAsync()`, `ToDynamoDbResponseAsync()`, `ToCompositeEntityListAsync()`, and the FluentResults `ToListAsyncResult()` failed for scans without a filter. When the builder has no values, the property is now `null` and omitted from the request, matching the Query, Put, Update, and Delete builders. An empty `ExpressionAttributeValues` supplied by the caller on a pre-built `ScanRequest` (through `WithRequest` or raw SDK access) is also normalized to `null`, which updates that `ScanRequest` instance. Values added through the builder and a non-empty `ExpressionAttributeValues` on a pre-built `ScanRequest` are unchanged. Fixes #229.
  - Do not reword the entry. If `design.md` changes, copy the updated wording
  - No change is needed to `docs/DOCUMENTATION_CHANGELOG.md` or `.kiro/steering/fluentdynamodb.md` because no documented API pattern changes
  - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [x] 5. Checkpoint - Run the full test suite and ensure all tests pass
  - Run `dotnet build` and then `dotnet test` for the whole solution
  - Ensure all tests pass, ask the user if questions arise
  - If `Oproto.FluentDynamoDb.IntegrationTests` (DynamoDB Local) is runnable in this environment and contains a scan test, confirm an unfiltered `Scan().ToListAsync()` case is covered. This is the only place the server-side rejection reproduces
