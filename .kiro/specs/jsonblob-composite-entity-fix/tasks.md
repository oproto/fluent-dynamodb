# Implementation Plan: JsonBlob Composite Entity Fix

## Overview

This implementation plan addresses the bug where `[JsonBlob]` properties are incorrectly deserialized in composite entities. The fix involves updating the source generator's `MapperGenerator.cs` to ensure proper JSON deserialization is used for all `[JsonBlob]` properties, including those in related entities.

## Tasks

- [x] 1. Investigate and document the exact bug location
  - [x] 1.1 Create a minimal reproduction test case with a composite entity containing JsonBlob properties
    - Create test entities: parent with `[RelatedEntity]` and child with `[JsonBlob]` property
    - Verify the bug manifests (Enum.Parse instead of JSON deserialization)
    - _Requirements: 1.1, 1.2_
    - **Result:** Created `JsonBlobCompositeEntityTests.cs` with test that reproduces the bug
  - [x] 1.2 Examine generated code output to identify the incorrect deserialization pattern
    - Enable source generator output to disk
    - Locate the generated FromDynamoDb method for the related entity
    - Document the incorrect code pattern
    - _Requirements: 1.1_
    - **Result:** Bug found in `GeneratePrimaryEntityIdentification` method - see findings below

## Investigation Findings (Task 1)

**Key Finding: The bug was found in `GeneratePrimaryEntityIdentification` method in `MapperGenerator.cs`.**

### Bug Location

The bug was in the `GeneratePrimaryEntityIdentification` method (around line 2737) in `MapperGenerator.cs`. When generating the multi-item `FromDynamoDb` method for composite entities (entities with `[RelatedEntity]` attributes), the code directly called `GetFromAttributeValueExpression()` for ALL non-collection properties WITHOUT checking if the property has `[JsonBlob]` attribute first.

### Root Cause

The `GetFromAttributeValueExpression` method has a fallback case that uses `IsEnumType()` to determine if a type should be parsed as an enum. The `IsEnumType()` method returns `true` for any type that's not a known primitive or collection type, which incorrectly matches complex types like `AddressValue`.

This caused the generator to emit:
```csharp
entity.Address = Enum.Parse<TestNamespace.AddressValue>(addressValue.S);
```

Instead of:
```csharp
entity.Address = options.JsonSerializer.Deserialize<TestNamespace.AddressValue>(addressValue.S);
```

- [x] 2. Fix the source generator code
  - [x] 2.1 Update GeneratePrimaryEntityIdentification in MapperGenerator.cs
    - Added check for `property.ComplexType?.IsJsonBlob == true` before calling `GetFromAttributeValueExpression`
    - For JsonBlob properties, generate JSON deserialization code similar to `GenerateJsonBlobPropertyFromAttributeValue`
    - _Requirements: 1.1, 1.2, 1.4_
    - **Result:** Fixed in `MapperGenerator.cs` around line 2737
  - [x] 2.2 Update test to include both generated files
    - The test was only compiling `LocationEntity.g.cs` but it references `ContactEntity.g.cs`
    - Updated test to include both generated files in compilation verification
    - _Requirements: 1.1, 1.2, 1.3_
  - [x] 2.3 Verify GenerateJsonBlobPropertyFromAttributeValue generates correct code
    - Reviewed the method - it correctly handles JsonBlob properties
    - The fix reuses the same pattern for the multi-item FromDynamoDb method
    - _Requirements: 1.3, 1.4_

### Fix Applied

Updated `GeneratePrimaryEntityIdentification` method to check for JsonBlob properties:

```csharp
foreach (var property in nonCollectionProperties)
{
    var varName = property.PropertyName.ToLowerInvariant() + "Value";
    var escapedPropertyName = EscapePropertyName(property.PropertyName);
    
    // Check if property is a JsonBlob - requires special JSON deserialization handling
    if (property.ComplexType?.IsJsonBlob == true)
    {
        // Generate JSON deserialization code
        var baseType = GetBaseType(property.PropertyType);
        sb.AppendLine($"            // Deserialize JSON blob property {property.PropertyName}");
        // ... full JSON deserialization with error handling
    }
    else
    {
        // Use existing GetFromAttributeValueExpression for non-JsonBlob properties
        sb.AppendLine($"            if (primaryItem.TryGetValue(\"{property.AttributeName}\", out var {varName}))");
        sb.AppendLine("            {");
        sb.AppendLine($"                entity.{escapedPropertyName} = {GetFromAttributeValueExpression(property, varName)};");
        sb.AppendLine("            }");
    }
}
```

- [x] 3. Checkpoint - Verify source generator changes
  - Rebuilt the source generator project
  - Ran `dotnet build-server shutdown` to clear cached generator
  - Verified generated code now contains correct JSON deserialization
  - All 5 tests pass ✅

## Test Results

All 5 tests in `JsonBlobCompositeEntityTests.cs` pass:
1. `Generator_WithParentEntityContainingJsonBlobAndRelatedEntity_GeneratesCorrectJsonDeserialization` ✅
2. `Generator_WithRelatedEntityContainingJsonBlob_GeneratesCorrectJsonDeserialization` ✅
3. `Generator_WithRelatedEntityContainingJsonBlob_PassesOptionsToChildFromDynamoDb` ✅
4. `Generator_WithNullableJsonBlobInRelatedEntity_HandlesNullGracefully` ✅
5. `Generator_WithListJsonBlobInRelatedEntity_DeserializesCorrectly` ✅

- [x] 4. Add unit tests for the fix
  - [x] 4.1 Add source generator output test for JsonBlob in related entities
    - Test that generated code contains JsonSerializer.Deserialize calls
    - Test both collection and single related entity scenarios
    - _Requirements: 1.1_
  - [x] 4.2 Write property test for JsonBlob round-trip consistency
    - **Property 2: JsonBlob Round-Trip Consistency**
    - **Validates: Requirements 1.3, 1.4, 2.1, 4.1**
  - [x] 4.3 Write property test for composite entity JsonBlob round-trip
    - **Property 3: Composite Entity JsonBlob Round-Trip**
    - **Validates: Requirements 1.2, 4.2**

- [x] 5. Add error handling tests
  - [x] 5.1 Add test for missing JSON serializer error
    - Verify InvalidOperationException is thrown with correct message
    - _Requirements: 3.1_
  - [x] 5.2 Add test for JSON deserialization failure error
    - Verify DynamoDbMappingException contains property context
    - _Requirements: 3.2_
  - [x] 5.3 Add test for related entity deserialization error
    - Verify error message includes related entity type
    - _Requirements: 3.3_

- [x] 6. Checkpoint - Ensure all tests pass
  - Run full test suite
  - Verify no regressions in existing functionality
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Add integration test for end-to-end verification
  - [x] 7.1 Create integration test with composite entity and JsonBlob properties
    - Create parent entity with [RelatedEntity] attribute
    - Create child entity with [JsonBlob] properties (nullable and collection)
    - Save to DynamoDB Local, load via ToCompositeEntityAsync
    - Verify all JsonBlob properties are correctly deserialized
    - _Requirements: 1.2, 4.2_

- [x] 8. Final checkpoint - Complete verification
  - Run all unit tests
  - Run integration tests if DynamoDB Local is available
  - Verify the original reproduction case now works correctly
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- All tasks are required for comprehensive testing
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties
- Unit tests validate specific examples and edge cases
- The source generator must be rebuilt and the build server shutdown to test changes
