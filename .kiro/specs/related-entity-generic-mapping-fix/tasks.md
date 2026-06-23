# Tasks: Related Entity Generic Mapping Fix

## Task 1: Fix sync collection mapping in GenerateRelatedEntityCollectionMapping
- [x] 1.1 Replace the TODO stub in the else branch of `GenerateRelatedEntityCollectionMapping` with proper `FromDynamoDb` call wrapped in try/catch with warning logging
  - In `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`, find the else block at ~line 5120 that creates `new {elementType}()` with TODO comment
  - Replace with: try/catch calling `{elementType}.FromDynamoDb<{elementType}>(item, options)` with `LogWarning` on failure using `RelatedEntityMappingFailed` event ID
  - _Requirements: 2.1, 3.4_

## Task 2: Fix async collection mapping in GenerateRelatedEntityCollectionMappingAsync
- [x] 2.1 Replace the TODO-equivalent stub in the else branch of `GenerateRelatedEntityCollectionMappingAsync` with proper `FromDynamoDbAsync` call wrapped in try/catch with warning logging
  - In `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`, find the else block at ~line 2555 that creates `new {elementType}()` without deserialization
  - Replace with: try/catch calling `await {elementType}.FromDynamoDbAsync<{elementType}>(item, blobProvider, fieldEncryptor, options, cancellationToken).ConfigureAwait(false)` with `LogWarning` on failure
  - _Requirements: 2.2, 3.4_

## Task 3: Fix single entity mapping in GenerateRelatedEntitySingleMapping
- [x] 3.1 Replace the TODO stub in the else branch of `GenerateRelatedEntitySingleMapping` with proper `FromDynamoDb` call wrapped in try/catch with warning logging
  - In `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`, find the else block at ~line 5219 that creates `new {propertyType}()` with TODO comment
  - Replace with: try/catch calling `{propertyType}.FromDynamoDb<{propertyType}>(item, options)` with `LogWarning` on failure, followed by `break;`
  - _Requirements: 2.3, 3.4_

## Task 4: Add unit tests for inferred type mapping
- [x] 4.1 Add a test verifying that generated sync collection mapping code uses `FromDynamoDb` when `EntityType` is null
  - In `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/RelatedEntityMappingTests.cs`, add a test with a `RelationshipModel` where `EntityType = null` and `IsCollection = true`, verify generated output contains `FromDynamoDb` call and does NOT contain TODO comment
  - _Requirements: 2.1_
- [x] 4.2 Add a test verifying that generated single entity mapping code uses `FromDynamoDb` when `EntityType` is null
  - Add a test with `EntityType = null` and `IsCollection = false`, verify generated output contains `FromDynamoDb` call and does NOT contain TODO comment
  - _Requirements: 2.3_
- [x] 4.3 Verify existing explicit EntityType tests still pass (regression check)
  - Run existing tests in `RelatedEntityMappingTests.cs` that use explicit `EntityType` and confirm they pass unchanged
  - _Requirements: 3.1, 3.2, 3.3_

## Task 5: Build verification
- [x] 5.1 Run `dotnet build-server shutdown` and then `dotnet build` to verify the source generator compiles correctly
  - _Requirements: 2.1, 2.2, 2.3_
- [x] 5.2 Run `dotnet test` on the source generator unit test project to verify all tests pass
  - _Requirements: 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 3.4_
