# Tasks: Collection Complex Type Reconstruction Fix

## Task 1: Fix complex type deserialization in GenerateCollectionPropertyFromItems
- [x] 1.1 Replace the TODO stub with proper Map and List-of-Maps deserialization using `FromDynamoDb`
  - In `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`, find the `if (IsComplexType(elementType))` branch in `GenerateCollectionPropertyFromItems` (~line 4097)
  - Replace the `new {elementType}()` stub with code that checks `.M` first (single Map) and `.L` second (List of Maps), calling `{elementType}.FromDynamoDb<{elementType}>()` with try/catch and `LogWarning` on failure
  - _Requirements: 2.1, 2.2, 2.3_

## Task 2: Add unit tests
- [x] 2.1 Add a test verifying generated code deserializes complex types from Map AttributeValues
  - Create or extend test in `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/` that uses an `EntityModel` with a complex-type collection property (IsCollection=true, complex element type) and verifies generated output contains `FromDynamoDb` call and does NOT contain TODO
  - _Requirements: 2.1, 2.2_
- [x] 2.2 Verify primitive collection path is unchanged (regression)
  - Ensure existing tests for primitive collection properties still pass
  - _Requirements: 3.1_

## Task 3: Build verification
- [x] 3.1 Run `dotnet build-server shutdown` and `dotnet build` to verify compilation
  - _Requirements: 2.1, 2.2, 2.3_
- [x] 3.2 Run `dotnet test` on the source generator unit test project
  - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 3.4_
