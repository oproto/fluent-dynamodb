# Implementation Plan

- [x] 1. Write bug condition exploration tests
  - **Property 1: Bug Condition** - Non-String Key Generates Uncompilable WithKey Code
  - **IMPORTANT**: Write these tests BEFORE implementing the fix
  - **CRITICAL**: These tests MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the tests or the code when they fail**
  - **NOTE**: These tests encode the expected behavior - they will validate the fix when they pass after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Scope the property to concrete failing cases: entities with non-string key types that have no prefix and are not computed
  - Write source generator unit tests in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests` that cover ALL of the following key type scenarios:
    - **Enum sort key (default string serialization)**: Entity with `[SortKey] [DynamoDbAttribute("SK")] SnsSubscriptionTopic Topic` (no prefix, no format) — expects `new AttributeValue { S = sK.ToString() }`
    - **Enum sort key (integer serialization via Format="D")**: Entity with `[SortKey] [DynamoDbAttribute("SK", Format = "D")] SnsSubscriptionTopic Topic` (no prefix, Format="D") — expects `new AttributeValue { S = sK.ToString("D", System.Globalization.CultureInfo.InvariantCulture) }` (numeric representation stored as string in DynamoDB)
    - **Int partition key**: Entity with `[PartitionKey] [DynamoDbAttribute("pk")] int UserId` (no prefix) — expects `new AttributeValue { N = pK.ToString() }`
    - **Long partition key**: Entity with `[PartitionKey] [DynamoDbAttribute("pk")] long Id` — expects `new AttributeValue { N = pK.ToString() }`
    - **Guid partition key**: Entity with `[PartitionKey] [DynamoDbAttribute("pk")] Guid Id` — expects `new AttributeValue { S = pK.ToString() }`
    - **DateTime sort key (default ISO 8601)**: Entity with `[SortKey] [DynamoDbAttribute("sk")] DateTime CreatedAt` — expects `new AttributeValue { S = sK.ToString("O") }`
    - **DateTime sort key (custom format)**: Entity with `[SortKey] [DynamoDbAttribute("sk", Format = "yyyy-MM-dd")] DateTime CreatedAt` — expects format string applied with CultureInfo.InvariantCulture
    - **DateTime sort key (DateTimeKind=Utc)**: Entity with `[SortKey] [DynamoDbAttribute("sk", DateTimeKind = DateTimeKind.Utc)] DateTime CreatedAt` — expects `.ToUniversalTime()` before formatting
    - **DateOnly sort key**: Entity with `[SortKey] [DynamoDbAttribute("sk")] DateOnly EventDate` — expects `new AttributeValue { S = sK.ToString("O", System.Globalization.CultureInfo.InvariantCulture) }`
    - **TimeOnly sort key**: Entity with `[SortKey] [DynamoDbAttribute("sk")] TimeOnly StartTime` — expects same pattern as DateOnly
    - **Nullable int partition key**: Entity with `[PartitionKey] [DynamoDbAttribute("pk")] int? Score` — expects `.Value` accessor used before `.ToString()`
    - **Nullable DateTime sort key**: Entity with `[SortKey] [DynamoDbAttribute("sk")] DateTime? ExpiresAt` — expects `.Value` accessor before formatting
    - **Mixed composite key (string PK with prefix + enum SK no prefix)**: Entity with prefixed string PK and non-string SK — verifies composite SetKey generation handles the mixed case correctly
    - **Both keys non-string (int PK + enum SK)**: Entity with `int` PK and enum SK, both without prefix — verifies both keys go through SetKey
  - For each test case:
    - Create appropriate `EntityModel` instance with correct `PropertyType`, `KeyFormat`, `Format`, `DateTimeKind`, and `IsComputed` settings
    - Run `TableGenerator` code generation (both accessor methods and table-level overloads)
    - Assert the generated code uses `.SetKey(k => { ... })` with correct `AttributeValue` construction matching `MapperGenerator.GetToAttributeValueExpression` output
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests FAIL (this is correct - proves the bug exists because current code emits `.WithKey()` with non-string parameters instead of `.SetKey()`)
  - Document counterexamples found
  - Mark task complete when tests are written, run, and failures documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.4_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - String/Prefixed/Computed Keys Continue Using WithKey
  - **IMPORTANT**: Follow observation-first methodology
  - Observe behavior on UNFIXED code for non-buggy inputs (entities where all keys are string-typed, have a prefix, or are computed)
  - Write source generator unit tests in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests` that cover:
    - **String PK, no prefix, not computed**: Entity with `[PartitionKey] string Id` — verify `.WithKey("id", id)` with string parameter
    - **String PK with prefix**: Entity with `[PartitionKey(Prefix = "USER")] string Pk` — verify `string` parameter type and `.WithKey("pk", pK)`
    - **String SK with prefix**: Entity with `[SortKey(Prefix = "ORDER")] string Sk` — verify `string` parameter type and composite `.WithKey()`
    - **Computed PK**: Entity with computed key (`IsComputed == true`) — verify `string` parameter type and `.WithKey()`
    - **Composite string keys (both prefixed)**: Entity with string PK + string SK both with prefix — verify `.WithKey("PK", pK, "SK", sK)`
    - **Composite string keys (no prefix)**: Entity with string PK + string SK, no prefix — verify `.WithKey("pk", pK, "sk", sK)`
    - **Non-string key WITH prefix (should still be string parameter)**: Entity with `[PartitionKey(Prefix = "ID")] int Id` — verify parameter type is `string` and `.WithKey()` used (prefix forces string)
    - **Non-string key WITH computed (should still be string parameter)**: Entity with computed non-string key — verify parameter type is `string` and `.WithKey()` used
  - Test all six affected methods for each scenario: `GenerateAccessorGetMethod`, `GenerateAccessorUpdateMethod`, `GenerateAccessorDeleteMethod`, `GenerateAccessorConditionCheckMethod`, `GenerateSingleKeyOverloads`, `GenerateCompositeKeyOverloads`
  - Property assertion: for all entity configurations where NO key satisfies `isBugCondition(key)`, the generated code MUST contain `.WithKey(` and MUST NOT contain `.SetKey(`
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 3. Fix for non-string key accessor compilation errors

  - [x] 3.1 Add `NeedsSetKeyApproach` helper method to `TableGenerator.cs`
    - Add `private static bool NeedsSetKeyApproach(PropertyModel key)` that returns `true` when:
      - `GetCSharpType(key.PropertyType)` is NOT `"string"` or `"System.String"`
      - AND `key.KeyFormat?.Prefix` is null or empty (`string.IsNullOrEmpty(key.KeyFormat?.Prefix)`)
      - AND `key.IsComputed == false`
    - _Bug_Condition: isBugCondition(key) where key.PropertyType is non-string AND NOT hasPrefix AND NOT isComputed_
    - _Requirements: 1.1, 2.1_

  - [x] 3.2 Add helper method to generate `AttributeValue` expression for key properties
    - Either make `MapperGenerator.GetToAttributeValueExpression` accessible to `TableGenerator` (change from `private static` to `internal static`) OR create a shared helper/duplicate the logic in `TableGenerator`
    - The method must handle all key-relevant types: string, int, long, double, float, decimal, ulong, uint, ushort, byte, sbyte, short, bool, DateTime, DateTimeOffset, DateOnly, TimeOnly, Guid, Ulid, byte[], and enum types
    - Must respect `PropertyModel.Format` and `PropertyModel.DateTimeKind` for DateTime types
    - _Expected_Behavior: expectedBehavior(result) — generated code uses correct AttributeValue construction matching MapperGenerator serialization logic_
    - _Requirements: 2.4_

  - [x] 3.3 Add helper to generate SetKey lambda code for single and composite keys
    - Create helper method(s) that generate the `.SetKey(k => { k["attrName"] = <AV expression>; })` code string
    - For single key: `.SetKey(k => { k["attributeName"] = new AttributeValue { N = paramName.ToString() }; })`
    - For composite key: `.SetKey(k => { k["pkAttr"] = <AV expr for pk>; k["skAttr"] = <AV expr for sk>; })`
    - For composite keys where one key is string and the other is non-string: BOTH must go through SetKey since we can't mix approaches
    - _Bug_Condition: isBugCondition(key) — when any key in the composite pair satisfies the condition, use SetKey for all keys_
    - _Preservation: When no key satisfies isBugCondition, continue to use .WithKey() unchanged_
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 3.4 Modify `GenerateAccessorGetMethod` to use conditional SetKey logic
    - In the single key branch: if `NeedsSetKeyApproach(partitionKey)`, emit `.SetKey(k => { ... })` instead of `.WithKey("attrName", paramName)`
    - In the composite key branch: if either key satisfies `NeedsSetKeyApproach`, emit `.SetKey(k => { ... })` for both keys
    - Preserve existing behavior (`.WithKey()`) when no key satisfies the condition
    - _Bug_Condition: isBugCondition(partitionKey) OR isBugCondition(sortKey)_
    - _Expected_Behavior: Generated code compiles and uses SetKey with correct AttributeValue construction_
    - _Preservation: String/prefixed/computed keys produce identical .WithKey() output_
    - _Requirements: 1.1, 1.2, 1.3, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 3.4_

  - [x] 3.5 Modify `GenerateAccessorUpdateMethod` to use conditional SetKey logic
    - In the single key branch: if `NeedsSetKeyApproach(partitionKey)`, change `builder.WithKey("attrName", paramName)` to `builder.SetKey(k => { ... })`
    - In the composite key branch: if either key satisfies `NeedsSetKeyApproach`, change `builder.WithKey(...)` to `builder.SetKey(k => { ... })`
    - Note: Update method uses multi-statement body (not expression-bodied), so change `builder.WithKey(...)` line directly
    - _Bug_Condition: isBugCondition(partitionKey) OR isBugCondition(sortKey)_
    - _Expected_Behavior: Generated Update method compiles with SetKey lambda_
    - _Preservation: String/prefixed/computed keys produce identical builder.WithKey() output_
    - _Requirements: 1.1, 1.4, 2.1, 3.1, 3.2, 3.3_

  - [x] 3.6 Modify `GenerateAccessorDeleteMethod` to use conditional SetKey logic
    - Same pattern as Get method (expression-bodied): if `NeedsSetKeyApproach` applies, emit `.SetKey(k => { ... })` instead of `.WithKey(...)`
    - Handle both single key and composite key branches
    - _Bug_Condition: isBugCondition(partitionKey) OR isBugCondition(sortKey)_
    - _Expected_Behavior: Generated Delete method compiles with SetKey lambda_
    - _Preservation: String/prefixed/computed keys produce identical .WithKey() output_
    - _Requirements: 1.1, 1.4, 2.1, 3.1, 3.2, 3.3_

  - [x] 3.7 Modify `GenerateAccessorConditionCheckMethod` to use conditional SetKey logic
    - Same pattern as Get method: if `NeedsSetKeyApproach` applies, emit `.SetKey(k => { ... })` instead of `.WithKey(...)`
    - Handle both single key and composite key branches
    - _Bug_Condition: isBugCondition(partitionKey) OR isBugCondition(sortKey)_
    - _Expected_Behavior: Generated ConditionCheck method compiles with SetKey lambda_
    - _Preservation: String/prefixed/computed keys produce identical .WithKey() output_
    - _Requirements: 1.1, 1.4, 2.1, 3.1, 3.2, 3.3_

  - [x] 3.8 Modify `GenerateSingleKeyOverloads` to use conditional SetKey logic
    - If `NeedsSetKeyApproach(partitionKey)`, emit all four overloads (Get, Update, Delete, ConditionCheck) using `.SetKey(k => { ... })`
    - Note: Update overload in table-level is expression-bodied (unlike accessor Update), same pattern as Get/Delete/ConditionCheck
    - _Bug_Condition: isBugCondition(partitionKey)_
    - _Expected_Behavior: Table-level single key overloads compile with SetKey_
    - _Preservation: String/prefixed/computed keys produce identical .WithKey() output_
    - _Requirements: 1.3, 1.4, 2.1, 2.3, 3.1, 3.4_

  - [x] 3.9 Modify `GenerateCompositeKeyOverloads` to use conditional SetKey logic
    - If either `NeedsSetKeyApproach(partitionKey)` OR `NeedsSetKeyApproach(sortKey)`, emit all four overloads using `.SetKey(k => { ... })` for both keys
    - When one key is string and the other is non-string, both must go through SetKey since we can't mix approaches in a single call
    - _Bug_Condition: isBugCondition(partitionKey) OR isBugCondition(sortKey)_
    - _Expected_Behavior: Table-level composite key overloads compile with SetKey_
    - _Preservation: String/prefixed/computed keys produce identical .WithKey() output_
    - _Requirements: 1.2, 1.4, 2.1, 2.2, 3.1, 3.5_

  - [x] 3.10 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Non-String Key Generates Compilable SetKey Code
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior (generated code uses `.SetKey()` with correct `AttributeValue` construction)
    - When this test passes, it confirms the expected behavior is satisfied for all non-string key types
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.11 Verify preservation tests still pass
    - **Property 2: Preservation** - String/Prefixed/Computed Keys Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix (no regressions for string, prefixed, and computed key entities)

- [x] 4. Checkpoint - Ensure all tests pass
  - Run `dotnet build-server shutdown` then `dotnet build` to verify full solution compiles
  - Run `dotnet test` to confirm all existing and new tests pass
  - Ensure all tests pass, ask the user if questions arise


