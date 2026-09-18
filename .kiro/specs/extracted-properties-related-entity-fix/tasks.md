# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Extracted Properties Missing in Multi-Item Paths
  - **CRITICAL**: This test MUST FAIL on unfixed code — failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior — it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate generated multi-item code is missing extraction logic
  - **Scoped PBT Approach**: Create an `EntityModel` with both `[Extracted]` properties and a `[RelatedEntity]` relationship (i.e., `IsMultiItemEntity = true`), run `MapperGenerator.GenerateEntityImplementation`, and assert the generated code contains the extraction logic (e.g., `entity.CreationDateTime = DateTime.Parse(orderidParts[...])`)
  - Create test file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/ExtractedPropertyWithRelatedEntityBugExplorationTests.cs`
  - Use existing test patterns from `ExtractedPropertyTypeConversionBugExplorationTests.cs` as a reference
  - Build an `EntityModel` with:
    - `IsMultiItemEntity = true`
    - A partition key property (`Pk`) with prefix `"STORE"` and `IsPartitionKey = true`
    - A sort key property (`OrderId`) with `IsSortKey = true`, a `ComputedKey` with `Format = "ORDER#{0:o}"`, and `HasCustomFormat = true`
    - An extracted `DateTime` property (`CreationDateTime`) with `IsExtracted = true`, `ExtractedKey = new ExtractedKeyModel { SourceProperty = "OrderId", Index = 0, Separator = "#" }`
    - A `RelationshipModel` with `PropertyName = "Lines"`, `SortKeyPattern = "*#LINE#*"`, `EntityType = "OrderLine"`, `IsCollection = true`
  - **Test 1 (sync path)**: Assert generated code contains `DateTime.Parse(orderidParts[` — confirms extraction logic is present in multi-item sync `FromDynamoDb`
  - **Test 2 (async path)**: Assert generated code contains extraction logic in the multi-item async `FromDynamoDbAsync` path (verify the extraction comment `"// Extract component properties from composite keys"` appears more than once, covering the multi-item path)
  - Run tests on UNFIXED code — expect FAILURE (this confirms the bug exists: the multi-item paths are missing `GenerateExtractedKeyLogic` calls)
  - Document counterexamples: generated multi-item code does not contain extraction assignments like `entity.CreationDateTime = DateTime.Parse(...)` while single-item code does
  - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Single-Item Extracted Property Behavior Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Create test file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/ExtractedPropertyWithRelatedEntityPreservationTests.cs`
  - Use existing test patterns from `ExtractedPropertyTypeConversionPreservationTests.cs` as a reference
  - **Observe on UNFIXED code**:
    - An entity with `[Extracted]` but NO `[RelatedEntity]` (single-item, `IsMultiItemEntity = false`) correctly generates extraction logic via the single-item sync and async paths
    - An entity with `[RelatedEntity]` but NO `[Extracted]` properties correctly generates multi-item code without unnecessary extraction blocks
    - The async multi-item short-circuit path (`items.Count == 1`) delegates to `FromDynamoDbAsync` which already has extraction
  - **Test 1**: Entity with `[Extracted]` + no `[RelatedEntity]` (`IsMultiItemEntity = false`) — assert generated code contains `DateTime.Parse(orderidParts[` (single-item extraction works)
  - **Test 2**: Entity with `[RelatedEntity]` + no `[Extracted]` (`IsMultiItemEntity = true`, no extracted properties) — assert generated code does NOT contain `"// Extract component properties from composite keys"` (no unnecessary extraction code)
  - **Test 3**: Entity with `[Extracted]` + no `[RelatedEntity]` — assert generated async single-item path contains extraction logic
  - Verify all tests PASS on UNFIXED code (confirms baseline behavior to preserve)
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 3. Fix extracted property population in multi-item deserialization paths

  - [x] 3.1 Add `GenerateExtractedKeyLogic` call to `GenerateMultiItemFromDynamoDb` (sync path)
    - In `MapperGenerator.cs`, method `GenerateMultiItemFromDynamoDb` (~line 4135)
    - Add extracted key logic block AFTER `GeneratePrimaryEntityIdentification` completes and BEFORE collection property population
    - Insert the following pattern (matching the indentation of the sync multi-item context — 12-space string literals):
      ```csharp
      // Generate extracted key logic
      var extractedProperties = entity.Properties.Where(p => p.IsExtracted).ToArray();
      if (extractedProperties.Length > 0)
      {
          sb.AppendLine();
          sb.AppendLine("            // Extract component properties from composite keys");
          foreach (var extractedProperty in extractedProperties)
          {
              GenerateExtractedKeyLogic(sb, extractedProperty, entity);
          }
      }
      ```
    - _Bug_Condition: isBugCondition(entity) where entity.IsMultiItemEntity == true AND entity.Properties.Any(p => p.IsExtracted)_
    - _Expected_Behavior: Generated sync multi-item FromDynamoDb code contains extraction assignments (e.g., `entity.CreationDateTime = DateTime.Parse(...)`)_
    - _Preservation: Single-item sync path extraction unchanged; multi-item entities without extracted properties unchanged_
    - _Requirements: 2.1, 2.3_

  - [x] 3.2 Add `GenerateExtractedKeyLogic` call to `GenerateMultiItemFromDynamoDbAsync` (async path)
    - In `MapperGenerator.cs`, method `GenerateMultiItemFromDynamoDbAsync` (~line 2371)
    - Add extracted key logic block AFTER `GenerateAsyncPrimaryEntityIdentification` completes and BEFORE collection property population
    - Insert the following pattern (matching the indentation of the async multi-item context — 16-space string literals):
      ```csharp
      // Generate extracted key logic
      var extractedPropertiesMulti = entity.Properties.Where(p => p.IsExtracted).ToArray();
      if (extractedPropertiesMulti.Length > 0)
      {
          sb.AppendLine();
          sb.AppendLine("                // Extract component properties from composite keys");
          foreach (var extractedProperty in extractedPropertiesMulti)
          {
              GenerateExtractedKeyLogic(sb, extractedProperty, entity);
          }
      }
      ```
    - Note: Use a different variable name (`extractedPropertiesMulti`) if there's a scope conflict with the single-item async path, or reuse `extractedProperties` if the methods are in separate scopes
    - _Bug_Condition: isBugCondition(entity) where entity.IsMultiItemEntity == true AND entity.Properties.Any(p => p.IsExtracted)_
    - _Expected_Behavior: Generated async multi-item FromDynamoDbAsync code contains extraction assignments_
    - _Preservation: Async single-item path extraction unchanged; async short-circuit (items.Count == 1) delegates to single-item path which already works_
    - _Requirements: 2.2, 2.3_

  - [x] 3.3 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Extracted Properties Present in Multi-Item Paths
    - **IMPORTANT**: Re-run the SAME tests from task 1 — do NOT write new tests
    - The tests from task 1 encode the expected behavior (extraction logic in multi-item generated code)
    - When these tests pass, it confirms the expected behavior is satisfied
    - Run bug condition exploration tests from step 1
    - **EXPECTED OUTCOME**: Tests PASS (confirms bug is fixed — multi-item paths now include `GenerateExtractedKeyLogic` calls)
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 3.4 Verify preservation tests still pass
    - **Property 2: Preservation** - Single-Item Extracted Property Behavior Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 — do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions — single-item paths and entities without extracted properties are unaffected)
    - Confirm all tests still pass after fix (no regressions)

- [x] 4. Checkpoint - Ensure all tests pass
  - Run `dotnet test` across the full solution to ensure no regressions
  - Verify both bug exploration tests and preservation tests pass
  - Verify existing `ExtractedPropertyTypeConversionBugExplorationTests` and `ExtractedPropertyTypeConversionPreservationTests` still pass (unrelated fix, should be unaffected)
  - Ask the user if questions arise
