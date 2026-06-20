# Extracted Property Type Conversion - Bugfix Design

## Overview

The `[Extracted]` attribute on entity properties generates code to extract component values from composite keys (e.g., splitting `"Orders#12345"` into an enum `TopicType` and a string `TopicId`). The generated code assigns raw `string` values from `Split()` without type conversion, producing uncompilable code for non-string property types. The fix adds an `IsEnum` flag to `PropertyModel` (set from Roslyn semantic analysis) and applies type conversion in both the `MapperGenerator.GenerateExtractedKeyLogic` and `KeysGenerator.GetExtractionExpression` code paths.

## Glossary

- **ExtractedProperty**: A property decorated with `[Extracted("SourceProp", index)]` that receives its value by splitting a composite key at runtime
- **CompositeKey**: A DynamoDB attribute value composed of multiple segments joined by a separator (e.g., `"ORDER#12345#PENDING"`)
- **MapperGenerator.GenerateExtractedKeyLogic**: Method that emits extraction code inside `FromDynamoDb` deserialization
- **KeysGenerator.GenerateExtractionHelper**: Method that emits the static `Keys.ExtractXComponents` helper method
- **KeysGenerator.GetExtractionExpression**: Method that returns the type-converted expression for a given property type (e.g., `int.Parse(parts[0])`)
- **KeysGenerator.IsEnumType**: Broken name-based heuristic that checks if type name contains "Status", "Type", "Kind", or "State"
- **MapperGenerator.IsEnumType**: Better heuristic using negative match against known primitives (not a known type → assumed enum)
- **PropertyModel.IsEnum**: New flag to be added, set from `ITypeSymbol.TypeKind == TypeKind.Enum` during entity analysis

## Bug Details

### Bug Condition

The bug manifests when an `[Extracted]` property has any non-string type. Two code generation sites are affected:

**Site 1 — MapperGenerator.GenerateExtractedKeyLogic (line ~5341):**

```csharp
// Always emits raw string assignment regardless of property type
sb.AppendLine($"entity.{escapedPropertyName} = {sourceProperty.ToLowerInvariant()}Parts[{index}];");
```

This method never applies any type conversion. For `string` properties this is fine, but for enums, ints, or any other type it produces `CS0029: Cannot implicitly convert type 'string' to 'T'`.

**Site 2 — KeysGenerator.GetExtractionExpression (line ~311):**

```csharp
_ when IsEnumType(propertyType) => $"Enum.Parse<{baseType}>({valueExpression})",
_ => valueExpression  // Falls through here for unrecognized types
```

This method has proper conversion cases for known primitives and a catch-all for enums, but relies on `IsEnumType` (line ~797) which uses a name-based heuristic:

```csharp
return propertyType.Contains("Status") || propertyType.Contains("Type") ||
       propertyType.Contains("Kind") || propertyType.Contains("State");
```

**Formal Specification:**
```
FUNCTION isBugCondition(extractedProperty)
  INPUT: extractedProperty of type PropertyModel where IsExtracted == true
  OUTPUT: boolean

  RETURN extractedProperty.PropertyType != "string"
         AND extractedProperty.PropertyType != "System.String"
END FUNCTION
```

### Existing Semantic Analysis

The `EntityAnalyzer` (line ~1362) already detects enums using proper Roslyn analysis:

```csharp
if (typeSymbol.TypeKind == TypeKind.Enum) { isEnum = true; }
```

But this `isEnum` local variable is only used for diagnostic gating — it is never stored on `PropertyModel`.

## Expected Behavior

### For enum extracted properties:

**FromDynamoDb:**
```csharp
entity.TopicType = Enum.Parse<SnsSubscriptionTopic>(topicParts[0]);
```

**Keys.ExtractTopicComponents:**
```csharp
return Enum.Parse<SnsSubscriptionTopic>(parts[0]);
```

### For numeric extracted properties:

**FromDynamoDb:**
```csharp
entity.Year = int.Parse(pkParts[0]);
```

**Keys.ExtractPkComponents:**
```csharp
return (Year: int.Parse(parts[0]), Month: int.Parse(parts[1]));
```

### For string extracted properties (unchanged):

**FromDynamoDb:**
```csharp
entity.TopicId = topicParts[1];
```

## Hypothesized Root Cause

1. **MapperGenerator.GenerateExtractedKeyLogic** was written assuming all extracted properties are strings. It performs no type dispatch.

2. **KeysGenerator.IsEnumType** uses a fundamentally unreliable detection mechanism (substring matching on type names). The semantic information (`TypeKind.Enum`) is available during analysis but discarded before reaching the generators.

3. **PropertyModel** lacks an `IsEnum` property, forcing generators to infer enum-ness from the type name string at generation time.

## Fix Implementation

### Change 1: Add `IsEnum` to PropertyModel

**File:** `Oproto.FluentDynamoDb.SourceGenerator/Models/PropertyModel.cs`

Add a boolean property:
```csharp
/// <summary>
/// Gets or sets a value indicating whether this property's type is an enum.
/// </summary>
public bool IsEnum { get; set; }
```

### Change 2: Set `IsEnum` during entity analysis

**File:** `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`

After the existing `isEnum` local variable is computed (line ~1362), persist it to the model:
```csharp
propertyModel.IsEnum = isEnum;
```

### Change 3: Fix MapperGenerator.GenerateExtractedKeyLogic

**File:** `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` (line ~5341)

Replace the unconditional string assignment with type-aware conversion:

- If `extractedProperty.IsEnum`: emit `Enum.Parse<{baseType}>(parts[index])`
- If type is a known parseable primitive (int, long, decimal, etc.): emit `T.Parse(parts[index])`
- If type is string: emit `parts[index]` (current behavior)
- Otherwise: emit `parts[index]` with a TODO or diagnostic

The method already has access to the `PropertyModel`, so `extractedProperty.IsEnum` is directly available.

### Change 4: Fix KeysGenerator.GetExtractionExpression

**File:** `Oproto.FluentDynamoDb.SourceGenerator/Generators/KeysGenerator.cs` (line ~311)

Change the method signature to accept `PropertyModel` (or at minimum a `bool isEnum` parameter) instead of relying on `IsEnumType(propertyType)`. The caller (`GenerateExtractionHelper`) already has access to the `PropertyModel[]`.

### Change 5: Remove KeysGenerator.IsEnumType heuristic

**File:** `Oproto.FluentDynamoDb.SourceGenerator/Generators/KeysGenerator.cs` (line ~797)

Delete the name-based heuristic method entirely. All usages should be replaced by `PropertyModel.IsEnum`.

### Change 6: Evaluate MapperGenerator.IsEnumType call sites

**File:** `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` (line ~4860)

Four call sites exist:
1. `GetToAttributeValueExpression` (line ~1571) — has `PropertyModel`, use `property.IsEnum`
2. `GenerateFormattedToAttributeValue` (line ~1632) — has `PropertyModel`, use `property.IsEnum`
3. `GetFromAttributeValueExpression` (line ~3764) — has `PropertyModel`, use `property.IsEnum`
4. `GetToAttributeValueExpressionForCollectionElement` (line ~5022) — only has element type string, no `PropertyModel`

For call sites 1-3: replace `IsEnumType(property.PropertyType)` with `property.IsEnum`.

For call site 4: keep the existing heuristic (or the negative-match approach) since there's no `PropertyModel` for collection elements and the generated code is functionally correct either way (both branches produce `.ToString()`).

## Correctness Properties

**Property 1: Bug Condition — Enum Extracted Properties Compile**

_For any_ entity with an `[Extracted]` property whose type is an enum, the generated `FromDynamoDb` method and `Keys.ExtractXComponents` method SHALL produce compilable code using `Enum.Parse<T>()`.

**Validates: Requirements 2.1, 2.2**

**Property 2: Bug Condition — Numeric Extracted Properties Compile**

_For any_ entity with an `[Extracted]` property whose type is a numeric type (int, long, decimal, etc.), the generated `FromDynamoDb` method and `Keys.ExtractXComponents` method SHALL produce compilable code using `T.Parse()`.

**Validates: Requirements 2.3, 2.4**

**Property 3: Preservation — String Extracted Properties Unchanged**

_For any_ entity with an `[Extracted]` property whose type is `string`, the generated code SHALL be identical to the current output (direct assignment, no conversion).

**Validates: Requirements 3.1, 3.6**

**Property 4: Preservation — Enum Serialization/Deserialization Unchanged**

_For any_ entity with enum properties (non-extracted), the `ToDynamoDb` and `FromDynamoDb` methods SHALL continue to serialize enums as `{ S = value.ToString() }` and deserialize as `Enum.Parse<T>(attr.S)`.

**Validates: Requirements 3.2, 3.3, 3.4**

**Property 5: Preservation — Computed Key Logic Unchanged**

_For any_ entity with `[Computed]` properties, the generated key-building logic SHALL remain unaffected by this fix.

**Validates: Requirements 3.5**

## Testing Strategy

### Bug Condition Tests (before fix)

Write tests that generate code for entities with enum and numeric `[Extracted]` properties and assert the generated code contains proper type conversion. These tests will FAIL on unfixed code (confirming the bug) and PASS after the fix.

**Test entities:**
1. Entity with enum `[Extracted]` property (e.g., `SnsSubscriptionTopic TopicType`)
2. Entity with int `[Extracted]` property (e.g., `int Year`)
3. Entity with multiple mixed-type extracted properties from one source (e.g., `int Year`, `int Month`, `string Label`)

**Assertions:**
- Generated `FromDynamoDb` contains `Enum.Parse<SnsSubscriptionTopic>(...)` not bare `parts[0]`
- Generated `FromDynamoDb` contains `int.Parse(...)` not bare `parts[0]`
- Generated `ExtractXComponents` return statement contains proper conversions

### Preservation Tests (before fix)

Write tests for string-typed extracted properties and non-extracted enum properties, confirming current behavior. These tests PASS on both unfixed and fixed code.

**Test entities:**
1. Entity with only string `[Extracted]` properties (current working case)
2. Entity with non-extracted enum property (verifying `ToDynamoDb`/`FromDynamoDb` serialization is unchanged)

### Integration Compilation Test

Create a test entity in the unit test project with an enum extracted property and verify the entire source generator output compiles successfully via Roslyn compilation in the test harness.

