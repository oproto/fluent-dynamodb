# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Non-String Key Types With Prefix Generate Non-Compilable Code
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Scope the property to concrete failing cases — non-string key types (enum, DateTime, Guid, int) with a configured prefix
  - Bug Condition: `isBugCondition(property)` returns true when `property.PropertyType NOT IN ["string", "String", "System.String"] AND (property.IsPartitionKey OR property.IsSortKey) AND NOT property.IsComputed AND NOT property.IsConstantKey AND property.KeyFormat.Prefix IS NOT empty`
  - Test that `GenerateKeyPrefixApplication` emits code passing the raw typed value directly to `ApplyKeyPrefix(string, ...)` without conversion for:
    - Enum key with prefix: `[SortKey(Prefix = "TOPIC")] public SnsSubscriptionTopic Topic` → expects `ApplyKeyPrefix(typedEntity.Topic.ToString(), ...)` but gets `ApplyKeyPrefix(typedEntity.Topic, ...)`
    - DateTime key with prefix: `[SortKey(Prefix = "DATE")] public DateTime CreatedAt` → expects `ApplyKeyPrefix(typedEntity.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), ...)` but gets `ApplyKeyPrefix(typedEntity.CreatedAt, ...)`
    - Guid key with prefix: `[PartitionKey(Prefix = "ID")] public Guid EntityId` → expects `ApplyKeyPrefix(typedEntity.EntityId.ToString(), ...)` but gets `ApplyKeyPrefix(typedEntity.EntityId, ...)`
    - Numeric key with prefix: `[SortKey(Prefix = "NUM")] public int Sequence` → expects `ApplyKeyPrefix(typedEntity.Sequence.ToString(), ...)` but gets `ApplyKeyPrefix(typedEntity.Sequence, ...)`
  - The test assertions should verify the generated code contains the correct `GetValueExpression`-produced string expression in the `ApplyKeyPrefix` call
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (this is correct - it proves the bug exists by showing raw typed values passed without conversion)
  - Document counterexamples found to understand root cause
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - String Key Properties and No-Prefix Keys Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Observe behavior on UNFIXED code for non-buggy inputs (cases where `isBugCondition` returns false):
    - Observe: String key with prefix `[PartitionKey(Prefix = "USER")] public string UserId` generates `ApplyKeyPrefix(typedEntity.UserId, ...)` — passes directly without conversion
    - Observe: Key without prefix `[PartitionKey] public Guid Id` does not enter `GenerateKeyPrefixApplication` at all
    - Observe: Computed key properties use their existing separate code path
    - Observe: Constant key properties are excluded from prefix application
  - Write property-based tests capturing observed behavior:
    - For all string-typed key properties with a prefix, the generated output passes `typedEntity.{PropertyName}` directly to `ApplyKeyPrefix` (no `.ToString()` wrapper)
    - For all key properties without a prefix configured, `GenerateKeyPrefixApplication` does not emit any `ApplyKeyPrefix` call for that property
    - For computed and constant key properties, the prefix application path is not invoked regardless of type
  - Verify tests pass on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3_

- [x] 3. Fix for non-string key types generating non-compilable prefix application code

  - [x] 3.1 Change `GetValueExpression` visibility from `private static` to `internal static`
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/KeysGenerator.cs`
    - Change the method signature from `private static string GetValueExpression(...)` to `internal static string GetValueExpression(...)`
    - This allows `MapperGenerator` (same assembly) to call it directly
    - No logic or signature change required — only visibility modifier
    - _Bug_Condition: isBugCondition(property) where property.PropertyType is non-string AND has prefix configured_
    - _Expected_Behavior: GetValueExpression becomes callable from MapperGenerator to produce correct string conversion expressions_
    - _Preservation: All existing callers within KeysGenerator continue to work identically_
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.2 Update `GenerateKeyPrefixApplication` to use `KeysGenerator.GetValueExpression`
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - In the `GenerateKeyPrefixApplication` method, replace direct `typedEntity.{escapedPropertyName}` usage with the result of `KeysGenerator.GetValueExpression($"typedEntity.{escapedPropertyName}", property.PropertyType)`
    - Updated emission pattern:
      ```csharp
      var valueExpr = KeysGenerator.GetValueExpression($"typedEntity.{escapedPropertyName}", property.PropertyType);
      sb.AppendLine($"                ArgumentNullException.ThrowIfNull(typedEntity.{escapedPropertyName}, nameof(typedEntity.{escapedPropertyName}));");
      sb.AppendLine($"                item[\"{attributeName}\"] = new AttributeValue {{ S = Oproto.FluentDynamoDb.Utility.KeyPrefixHelper.ApplyKeyPrefix({valueExpr}, \"{prefix}\", \"{separator}\", resolvedMode) }};");
      ```
    - For string properties, `GetValueExpression` returns the parameter name unchanged (e.g., `typedEntity.UserId`), so output is identical to before
    - For non-string properties, `GetValueExpression` wraps with appropriate conversion (`.ToString()`, `.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")`, etc.)
    - _Bug_Condition: isBugCondition(property) where property.PropertyType is non-string AND has prefix_
    - _Expected_Behavior: Generated code calls ApplyKeyPrefix with a string expression produced by GetValueExpression_
    - _Preservation: String key properties produce identical generated code (GetValueExpression returns parameter name unchanged for strings)_
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.1_

  - [x] 3.3 Fix `GenerateComputedKeyLogic` separator-based concatenation to use `GetValueExpression`
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
    - In `GenerateComputedKeyLogic`, the separator-based concatenation path (non-custom-format) currently uses raw string concatenation for source properties
    - This means DateTime source properties in computed keys without custom format specifiers produce a default `.ToString()` representation instead of the ISO 8601 format that `GetValueExpression` applies
    - Update the separator-based concatenation to call `KeysGenerator.GetValueExpression` for each source property expression, ensuring consistency with `Keys.BuildPk()`/`Keys.BuildSk()` output
    - This fixes a silent data corruption risk where Put operations write different key values than what `Keys.Build*()` generates for lookups
    - _Bug_Condition: Computed key with non-string source properties using separator-based concatenation (no custom format)_
    - _Expected_Behavior: Source property values in separator-based computed keys use the same string conversion as GetValueExpression (matching Keys.Build* output)_
    - _Preservation: Computed keys with custom format specifiers are unaffected (they use their own format path)_
    - _Requirements: 2.2, 3.3_

  - [x] 3.4 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Non-String Key Types With Prefix Generate Compilable Code
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior (correct string conversion in ApplyKeyPrefix calls)
    - When this test passes, it confirms:
      - Enum keys generate `ApplyKeyPrefix(typedEntity.Topic.ToString(), ...)`
      - DateTime keys generate `ApplyKeyPrefix(typedEntity.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), ...)`
      - Guid keys generate `ApplyKeyPrefix(typedEntity.EntityId.ToString(), ...)`
      - Numeric keys generate `ApplyKeyPrefix(typedEntity.Sequence.ToString(), ...)`
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** - String Key Properties and No-Prefix Keys Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix:
      - String key properties with prefix generate identical code (no conversion wrapper added)
      - Key properties without prefix are unaffected
      - Computed keys with custom format specifiers are unaffected
      - Constant keys remain excluded from prefix application

- [x] 4. Checkpoint - Ensure all tests pass
  - Run full test suite with `dotnet test`
  - Ensure all property-based tests pass (both bug condition and preservation)
  - Ensure existing unit tests in the solution still pass (no regressions)
  - Shut down build server with `dotnet build-server shutdown` to clear cached source generator
  - Rebuild and confirm generated code compiles correctly for entities with non-string key types and prefixes
  - Ensure all tests pass, ask the user if questions arise.
