# Implementation Tasks: Unsigned Integer Types Support

## Task 1: Update EntityAnalyzer to Accept New Types

- [x] Open `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`
- [x] Locate the `IsSupportedPropertyType` method (around line 1686)
- [x] Add the following types to the `supportedTypes` array:
  - `"ulong"`, `"System.UInt64"`
  - `"uint"`, `"System.UInt32"`
  - `"ushort"`, `"System.UInt16"`
  - `"byte"`, `"System.Byte"`
  - `"sbyte"`, `"System.SByte"`
  - `"short"`, `"System.Int16"`

## Task 2: Update GetToAttributeValueExpressionForCollectionElement (no valueExpression)

- [x] Open `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`
- [x] Locate `GetToAttributeValueExpressionForCollectionElement(string elementType)` (around line 1415)
- [x] Add switch cases for the new types after the existing numeric types

## Task 3: Update GetToAttributeValueExpression

- [x] Locate `GetToAttributeValueExpression(PropertyModel property, string valueExpression)` (around line 1439)
- [x] Add switch cases for the new types after the existing numeric types

## Task 4: Update GetFromAttributeValueExpressionForCollectionElement

- [x] Locate `GetFromAttributeValueExpressionForCollectionElement(string elementType)` (around line 3051)
- [x] Add switch cases for the new types after the existing numeric types

## Task 5: Update GetFromAttributeValueExpression

- [x] Locate `GetFromAttributeValueExpression(PropertyModel property, string valueExpression)` (around line 3075)
- [x] Add switch cases for the new types after the existing numeric types

## Task 6: Update GetToAttributeValueExpressionForCollectionElement (with valueExpression)

- [x] Locate `GetToAttributeValueExpressionForCollectionElement(string elementType, string valueExpression)` (around line 4083)
- [x] Add switch cases for the new types after the existing numeric types

## Task 7: Update GenerateFormattedToAttributeValue (Optional)

- [x] Locate `GenerateFormattedToAttributeValue` method (around line 1510)
- [x] Add the new types to the numeric type check

## Task 8: Write Unit Tests for EntityAnalyzer

- [x] Create test file `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Analysis/UnsignedIntegerTypeTests.cs`
- [x] Add tests verifying each new type is accepted without DYNDB009 diagnostic:
  - `AnalyzeEntity_WithUlongProperty_AcceptsWithoutError`
  - `AnalyzeEntity_WithUintProperty_AcceptsWithoutError`
  - `AnalyzeEntity_WithUshortProperty_AcceptsWithoutError`
  - `AnalyzeEntity_WithByteProperty_AcceptsWithoutError`
  - `AnalyzeEntity_WithSbyteProperty_AcceptsWithoutError`
  - `AnalyzeEntity_WithShortProperty_AcceptsWithoutError`
  - `AnalyzeEntity_WithNullableUlongProperty_AcceptsWithoutError`

## Task 9: Write Integration Tests for Round-Trip Serialization

- [x] Create test file `Oproto.FluentDynamoDb.UnitTests/Entities/UnsignedIntegerPropertyTests.cs`
- [x] Add round-trip tests for each type:
  - Test serialization produces correct AttributeValue with N property
  - Test deserialization parses N property correctly
  - Test nullable types handle null correctly
  - Test collection types (List, HashSet) serialize/deserialize correctly

## Task 10: Write Property-Based Tests

- [x] Create test file `Oproto.FluentDynamoDb.UnitTests/Entities/UnsignedIntegerPropertyTests.cs`
- [x] Add FsCheck property-based tests:
  - Round-trip consistency for all new types
  - Boundary value handling (0, max values)
  - Collection round-trip consistency

## Task 11: Rebuild and Verify

- [x] Run `dotnet build-server shutdown` to clear source generator cache
- [x] Run `dotnet build` to verify compilation
- [x] Run `dotnet test` to verify all tests pass
- [x] Verify no DYNDB009 errors for entities using new types
