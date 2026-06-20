# Implementation Plan

- [x] 1. Write bug condition exploration tests
  - **Property 1 & 2: Bug Condition** — Enum and numeric extracted properties generate uncompilable code
  - **IMPORTANT**: Write these tests BEFORE implementing the fix
  - **CRITICAL**: These tests MUST FAIL on unfixed code — failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **GOAL**: Surface counterexamples demonstrating the generated code is broken
  - Create test file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/ExtractedPropertyTypeConversionBugExplorationTests.cs`
  - Test 1: Entity with `[Extracted("Topic", 0)]` on a property of type `SnsSubscriptionTopic` (enum). Assert generated `FromDynamoDb` contains `Enum.Parse<SnsSubscriptionTopic>(topicParts[0])` rather than bare `topicParts[0]`
  - Test 2: Entity with `[Extracted("Topic", 0)]` on enum property. Assert generated `ExtractTopicComponents` returns `Enum.Parse<SnsSubscriptionTopic>(parts[0])` rather than bare `parts[0]`
  - Test 3: Entity with `[Extracted("Pk", 0)]` on a property of type `int`. Assert generated `FromDynamoDb` contains `int.Parse(pkParts[0])` rather than bare `pkParts[0]`
  - Test 4: Entity with `[Extracted("Pk", 0)]` on int property. Assert generated `ExtractPkComponents` returns `int.Parse(parts[0])` rather than bare `parts[0]`
  - Test 5: Entity with multiple extracted properties from one source — `int Year` at index 0, `int Month` at index 1, `string Label` at index 2. Assert the tuple return has `int.Parse(parts[0])`, `int.Parse(parts[1])`, and `parts[2]` (string untouched)
  - Test 6: Entity with nullable enum `[Extracted]` property (e.g., `SnsSubscriptionTopic?`). Assert proper nullable handling in generated code
  - Use `MapperGenerator.GenerateEntityImplementation(entity)` and `KeysGenerator` methods to get generated code strings, then assert on content
  - Follow same pattern as `NonStringKeyAccessorBugExplorationTests.cs` — construct `EntityModel` programmatically
  - Run tests — **EXPECTED OUTCOME**: Tests FAIL (confirms bug exists)
  - Document counterexamples (e.g., "Generated code contains `entity.TopicType = topicParts[0]` instead of `entity.TopicType = Enum.Parse<SnsSubscriptionTopic>(topicParts[0])`")
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4_

- [x] 2. Write preservation tests (before fix)
  - **Property 3, 4, 5: Preservation** — String extraction and non-extracted enum serialization unchanged
  - **IMPORTANT**: Write BEFORE implementing fix. These must PASS on unfixed code
  - Create test file: `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/ExtractedPropertyTypeConversionPreservationTests.cs`
  - Test 1: Entity with string `[Extracted("Pk", 0)]` property. Assert generated `FromDynamoDb` contains `entity.Component = pkParts[0]` (direct assignment, no conversion)
  - Test 2: Entity with string `[Extracted("Pk", 0)]` property. Assert generated `ExtractPkComponents` returns `parts[0]` directly
  - Test 3: Entity with multiple string extracted properties. Assert all assignments are direct string assignments
  - Test 4: Entity with non-extracted enum property (regular `[DynamoDbAttribute]`). Assert `ToDynamoDb` still generates `new AttributeValue { S = ... .ToString() }` for the enum
  - Test 5: Entity with non-extracted enum property. Assert `FromDynamoDb` still generates `Enum.Parse<T>(attr.S)` for the enum
  - Test 6: Entity with `[Computed]` property alongside `[Extracted]` properties. Assert computed key generation logic is unchanged
  - Run tests — **EXPECTED OUTCOME**: Tests PASS (confirms baseline behavior)
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 3. Add `IsEnum` property to PropertyModel and set it during analysis
  - [x] 3.1 Add `IsEnum` property to PropertyModel
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Models/PropertyModel.cs`
    - Add: `public bool IsEnum { get; set; }`
    - Add XML documentation explaining it's set from Roslyn semantic analysis
    - _Requirements: 2.5_
  - [x] 3.2 Set `IsEnum` from semantic analysis in EntityAnalyzer
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`
    - After the existing `isEnum` local variable is computed (~line 1376), add: `propertyModel.IsEnum = isEnum;`
    - The analyzer already has the correct detection logic (`typeSymbol.TypeKind == TypeKind.Enum` and nullable enum detection) — just persist it
    - _Requirements: 2.5_
  - [x] 3.3 Verify EntityAnalyzer tests still pass
    - Run: `dotnet test --filter "FullyQualifiedName~EntityAnalyzer"`
    - Ensure no regressions from adding the property
    - _Requirements: 3.5, 3.6_

- [x] 4. Fix MapperGenerator.GenerateExtractedKeyLogic
  - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` (line ~5341)
  - Replace the unconditional assignment:
    ```csharp
    sb.AppendLine($"entity.{escapedPropertyName} = {sourceProperty.ToLowerInvariant()}Parts[{index}];");
    ```
  - With type-aware conversion using the `PropertyModel` that is already the method parameter:
    - If `extractedProperty.IsEnum`: emit `entity.Prop = Enum.Parse<{baseType}>({parts}[{index}]);`
    - If type is int/long/decimal/double/float/bool/DateTime/DateTimeOffset/Guid/Ulid: emit `entity.Prop = T.Parse({parts}[{index}]);`
    - If type is string: emit `entity.Prop = {parts}[{index}];` (unchanged)
    - Handle nullable types: unwrap nullable before matching, use null-conditional or explicit null check if needed
  - Consider extracting a helper method (e.g., `GetExtractedPropertyConversionExpression`) that mirrors `KeysGenerator.GetExtractionExpression` logic but reuses `PropertyModel.IsEnum`
  - _Requirements: 2.1, 2.3, 3.1_

- [x] 5. Fix KeysGenerator.GetExtractionExpression and remove heuristic
  - [x] 5.1 Pass `PropertyModel` (or `bool isEnum`) to GetExtractionExpression
    - File: `Oproto.FluentDynamoDb.SourceGenerator/Generators/KeysGenerator.cs`
    - Current signature: `GetExtractionExpression(string valueExpression, string propertyType)`
    - Change to: `GetExtractionExpression(string valueExpression, string propertyType, bool isEnum)`
    - Or pass the full `PropertyModel` if preferred
    - Update caller in `GenerateExtractionHelper` to pass `extractedProperty.IsEnum`
    - _Requirements: 2.2, 2.4, 2.5_
  - [x] 5.2 Replace `IsEnumType` check in switch expression
    - Change `_ when IsEnumType(propertyType) =>` to `_ when isEnum =>`
    - _Requirements: 2.2, 2.5_
  - [x] 5.3 Delete `KeysGenerator.IsEnumType` method (line ~797)
    - Remove the entire method — it's the broken name-based heuristic
    - Verify no other callers exist in KeysGenerator (check `IsNumericType` call site at line ~751 which also calls `IsEnumType`)
    - If `IsEnumType` is also called from `GetBuildKeyExpression` or similar methods, update those callers to accept and use `PropertyModel.IsEnum`
    - _Requirements: 2.5_

- [x] 6. Update MapperGenerator.IsEnumType call sites (1-3) to use PropertyModel.IsEnum
  - [x] 6.1 Update `GetToAttributeValueExpression` (~line 1571)
    - This method already receives `PropertyModel property` as a parameter
    - Change `_ when IsEnumType(property.PropertyType)` to `_ when property.IsEnum`
    - _Requirements: 2.5, 3.2_
  - [x] 6.2 Update `GenerateFormattedToAttributeValue` (~line 1632)
    - This method already receives `PropertyModel property` as a parameter
    - Change `if (IsEnumType(property.PropertyType))` to `if (property.IsEnum)`
    - _Requirements: 2.5, 3.2_
  - [x] 6.3 Update `GetFromAttributeValueExpression` (~line 3764)
    - This method already receives `PropertyModel property` as a parameter
    - Change `_ when IsEnumType(property.PropertyType)` to `_ when property.IsEnum`
    - _Requirements: 2.5, 3.3_
  - [x] 6.4 Leave `GetToAttributeValueExpressionForCollectionElement` (~line 5022) unchanged
    - This call site only has the element type string, no `PropertyModel`
    - The generated code is functionally correct regardless (both branches produce `.ToString()`)
    - Optionally: leave as-is, or replace with the negative-match heuristic from current `MapperGenerator.IsEnumType`
    - _Requirements: 3.4_
  - [x] 6.5 Optionally simplify or rename `MapperGenerator.IsEnumType`
    - If only call site 4 remains, consider renaming to `IsLikelyEnumType` or `IsNonPrimitiveType` to clarify it's a heuristic used only for collection elements
    - Or inline the logic at the single remaining call site
    - _Requirements: 2.5_

- [x] 7. Verify bug condition exploration tests now pass
  - **IMPORTANT**: Re-run the SAME tests from task 1 — do NOT write new tests
  - Run: `dotnet test --filter "FullyQualifiedName~ExtractedPropertyTypeConversionBugExploration"`
  - **EXPECTED OUTCOME**: Tests PASS (confirms bug is fixed)
  - If any tests fail, diagnose whether the generated conversion expression doesn't match expectations and adjust
  - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [x] 8. Verify preservation tests still pass
  - **IMPORTANT**: Re-run the SAME tests from task 2 — do NOT write new tests
  - Run: `dotnet test --filter "FullyQualifiedName~ExtractedPropertyTypeConversionPreservation"`
  - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

- [x] 9. Run full test suite and verify compilation
  - Run: `dotnet build` on the full solution to verify no compile errors introduced
  - Run: `dotnet test` on the full solution to verify no regressions
  - Pay special attention to:
    - `KeysGeneratorTests.cs` — existing extraction helper tests
    - `MapperGeneratorTests.cs` — existing mapper tests
    - `MapperGeneratorBugFixTests.cs` — prior bug fixes
    - `DayOfWeekSerializationTests.cs` — existing enum serialization tests
    - Any tests in ApiConsistencyTests that exercise Computed/Extracted patterns
  - If failures arise, investigate whether they indicate additional call sites that need updating
  - _Requirements: All_

- [x] 10. Add integration compilation test for enum extracted property
  - Create a test that defines a full entity source with an enum `[Extracted]` property and runs it through the source generator pipeline, then compiles the output using Roslyn in-memory compilation
  - This catches any edge cases where the generated code string-matches look correct but don't actually compile
  - File: Add to existing integration test location or create `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Integration/ExtractedEnumPropertyCompilationTests.cs`
  - Test input:
    ```csharp
    public enum SnsSubscriptionTopic { Orders, Notifications }

    [DynamoDbTable("Subscriptions")]
    public partial class Subscription
    {
        [PartitionKey]
        [DynamoDbAttribute("pk")]
        [Computed("TopicType", "TopicId", Separator = "#")]
        public string Topic { get; set; } = string.Empty;

        [Extracted("Topic", 0)]
        public SnsSubscriptionTopic TopicType { get; set; }

        [Extracted("Topic", 1)]
        public string TopicId { get; set; } = string.Empty;
    }
    ```
  - Assert: Compilation produces no errors (Diagnostic severity Error count == 0)
  - _Requirements: 2.1, 2.2_

