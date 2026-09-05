# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Constant SK Included in Typed Overloads
  - **CRITICAL**: This test MUST FAIL on unfixed code — failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior — it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists (compilation errors CS7036, CS1503)
  - **Scoped PBT Approach**: Scope the property to the concrete failing case — an entity with `[Computed]` PK (2 source properties of type `Ulid`) and a constant SK (expression-body `=> "CONSTANT_VALUE"`)
  - **Step 1 — Create test entity** `ComputedPkConstantSkTable.cs` in `Oproto.FluentDynamoDb.ApiConsistencyTests/Entities/`:
    - Define a `[DynamoDbTable(typeof(ComputedPkConstantSkTable))]` entity named `ComputedPkConstantSkEntity`
    - PK: `[PartitionKey]` + `[DynamoDbAttribute("pk")]` + `[Computed("PkTenantId", "PkCompanyId", Format = "{0}#COMPANY#{1}")]` with `string Pk { get; set; }`
    - Two `[Extracted]` properties: `PkTenantId` (Ulid) and `PkCompanyId` (Ulid)
    - SK: `[SortKey]` + `[DynamoDbAttribute("sk")]` with expression-body constant: `public string Sk => "COUNTER#CUSTOMER_NUMBER";`
    - Declare `ComputedPkConstantSkTable` as a separate partial class (type-based table reference pattern)
    - Include a data attribute, e.g., `[DynamoDbAttribute("value")] public int Value { get; set; }`
  - **Step 2 — Create API surface compile test** `ComputedPkConstantSkApiSurface.cs` in `Oproto.FluentDynamoDb.ApiConsistencyTests/SingleEntityTables/`:
    - Pattern after existing `ComputedKeyTypedOverloadsApiSurface.cs` but with constant SK omitted from all typed overloads
    - Test methods (all `[Fact(Skip = "API Surface Validation")]`):
      - `TypedOverloads_ComputedPkConstantSk_Get_ShouldCompile`: typed `Get(Ulid pkTenantId, Ulid pkCompanyId)` (no `sK` param), delegates to `Get(computedPk)` single-arg
      - `TypedOverloads_ComputedPkConstantSk_Delete_ShouldCompile`: typed `Delete(Ulid, Ulid)` (no `sK`), delegates to `Delete(computedPk)`
      - `TypedOverloads_ComputedPkConstantSk_Update_ShouldCompile`: typed `Update(Ulid, Ulid)` (no `sK`), delegates to `Update(computedPk)`
      - `TypedOverloads_ComputedPkConstantSk_ConditionCheck_ShouldCompile`: typed `ConditionCheck(Ulid, Ulid)` (no `sK`), delegates to `ConditionCheck(computedPk)`
      - `StandardOverloads_ComputedPkConstantSk_ShouldCompile`: verify standard `Get("pk_value")`, `Delete("pk_value")`, `Update("pk_value")` (single string PK, no SK) still compile
    - Each typed overload test asserts the return type (e.g., `GetItemRequestBuilder<ComputedPkConstantSkEntity>`) to ensure correct method resolution
  - **Step 3 — Build and observe failure on UNFIXED code**:
    - Run `dotnet build` on the `Oproto.FluentDynamoDb.ApiConsistencyTests` project
    - **EXPECTED OUTCOME**: Build FAILS with CS7036 (Get, Delete, ConditionCheck) and CS1503 (Update) because the generated typed overloads incorrectly include `sK` parameter and delegate to 2-arg raw methods that don't exist
    - Document the exact compilation errors as counterexamples
  - Mark task complete when test entity + API surface tests are written, build attempted, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Non-Constant Key Entity Typed Overloads Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Observe: existing `ComputedKeyTypedOverloadsApiSurface.cs` compiles successfully on unfixed code — entity `ComputedKeyEntity` has computed PK (Year, Month, Day) + normal (non-constant) SK and typed overloads correctly include `sK` parameter with 4-arg signatures `Get(int, int, int, string)`
  - Observe: `dotnet build` of `Oproto.FluentDynamoDb.ApiConsistencyTests` succeeds for all existing API surface tests (excluding the new constant-SK entity tests from task 1 which are expected to fail)
  - Observe: `dotnet test` passes for all existing unit tests in `Oproto.FluentDynamoDb.UnitTests`
  - **Preservation baseline**: Record that existing tests pass, confirming the computed-PK + normal-SK path is correct
  - Verify `ComputedKeyTypedOverloadsApiSurface.cs` tests all compile (Get, Delete, Update, ConditionCheck with 4 typed params + `sK`)
  - Verify existing unit tests pass: `dotnet test --filter "FullyQualifiedName!~ComputedPkConstantSk"`
  - **EXPECTED OUTCOME**: All existing tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when existing tests are verified passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 3. Fix for typed overload constant SK not omitted

  - [x] 3.1 Fix `OverloadParameterResolver.cs` — both `GetTypedOverloadParameters()` and `GetStandardOverloadParameters()`
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/OverloadParameterResolver.cs`
    - **Change 1 — `GetTypedOverloadParameters()` SK branch** (line ~87): Change `else if (sk != null)` to `else if (sk != null && !sk.IsConstantKey)` — prevents constant SK from being added to typed parameter list
    - **Change 2 — `GetTypedOverloadParameters()` PK branch** (line ~78): Change `else if (pk != null)` to `else if (pk != null && !pk.IsConstantKey)` — defensive fix for symmetric constant PK case
    - **Change 3 — `GetStandardOverloadParameters()` PK guard** (line ~102): Change `if (entity.PartitionKeyProperty != null)` to `if (entity.PartitionKeyProperty != null && !entity.PartitionKeyProperty.IsConstantKey)` — defensive consistency
    - **Change 4 — `GetStandardOverloadParameters()` SK guard** (line ~104): Change `if (entity.SortKeyProperty != null)` to `if (entity.SortKeyProperty != null && !entity.SortKeyProperty.IsConstantKey)` — defensive consistency
    - _Bug_Condition: isBugCondition(entity) where entity has computed key AND constant key with IsConstantKey == true_
    - _Expected_Behavior: GetTypedOverloadParameters and GetStandardOverloadParameters omit constant keys from parameter lists_
    - _Preservation: Entities without constant keys produce identical parameter lists as before_
    - _Requirements: 2.1, 2.6, 2.7, 3.1, 3.2, 3.3_

  - [x] 3.2 Fix `TableGenerator.cs` — all 4 `GenerateTyped*Overload` methods
    - **File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/TableGenerator.cs`
    - **Change pattern** (identical in all 4 methods): In each `GenerateTyped*Overload` method, change the SK delegation block from `if (sortKey != null)` to `if (sortKey != null && !sortKey.IsConstantKey)`. When the SK is constant, the entire SK delegation block is skipped — no `computedSk` computation, no `"sK"` delegation argument. The raw method auto-fills the constant SK value.
    - **Methods to fix**:
      - `GenerateTypedGetOverload` — wrap SK block with `!sortKey.IsConstantKey`
      - `GenerateTypedDeleteOverload` — wrap SK block with `!sortKey.IsConstantKey`
      - `GenerateTypedUpdateOverload` — wrap SK block with `!sortKey.IsConstantKey`
      - `GenerateTypedConditionCheckOverload` — wrap SK block with `!sortKey.IsConstantKey`
    - **Defensive PK guard** (all 4 methods): In the `else` branch for non-computed PK, add `&& !partitionKey.IsConstantKey` so constant PKs are not added to delegation args. Currently defensive-only since constant PK + computed SK is blocked by FDDB120.
    - After making changes, run `dotnet build-server shutdown` to clear the cached source generator
    - _Bug_Condition: isBugCondition(entity) where sortKey.IsConstantKey == true in typed overload delegation_
    - _Expected_Behavior: Delegation args omit constant keys, producing calls like Get(computedPk) instead of Get(computedPk, sK)_
    - _Preservation: Entities with non-constant SK continue to include sK in delegation args_
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.5_

  - [x] 3.3 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Constant SK Omitted from Typed Overloads
    - **IMPORTANT**: Re-run the SAME build from task 1 — do NOT write a new test
    - The API surface compile tests from task 1 encode the expected behavior (typed overloads with only PK source params, no `sK`)
    - Run `dotnet build-server shutdown` then `dotnet build` on `Oproto.FluentDynamoDb.ApiConsistencyTests`
    - **EXPECTED OUTCOME**: Build PASSES — confirms the generated typed overloads now correctly omit constant SK from parameters and delegation calls
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 3.4 Verify preservation tests still pass
    - **Property 2: Preservation** - Non-Constant Key Entity Typed Overloads Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 — do NOT write new tests
    - Run `dotnet test` on `Oproto.FluentDynamoDb.UnitTests` to verify all existing unit tests pass
    - Verify `ComputedKeyTypedOverloadsApiSurface.cs` still compiles (computed PK + normal SK entity still gets typed overloads with `sK` param)
    - **EXPECTED OUTCOME**: All tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix (no regressions)

- [x] 4. Checkpoint — Ensure all tests pass
  - Run full build: `dotnet build` on entire solution
  - Run full test suite: `dotnet test` on all test projects
  - Verify new `ComputedPkConstantSkApiSurface.cs` compiles (bug is fixed)
  - Verify existing `ComputedKeyTypedOverloadsApiSurface.cs` compiles (no regression)
  - Verify all unit tests pass
  - Ensure all tests pass, ask the user if questions arise.


