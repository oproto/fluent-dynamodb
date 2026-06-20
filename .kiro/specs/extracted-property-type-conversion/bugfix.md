# Bugfix Requirements Document

## Introduction

The `[Extracted]` attribute generates uncompilable code when the target property is a non-string type (enum, int, long, etc.). Both the `FromDynamoDb` deserialization path (`MapperGenerator.GenerateExtractedKeyLogic`) and the `Keys.ExtractXComponents` helper method (`KeysGenerator.GenerateExtractionHelper`) are affected. The root cause is that neither code path reliably converts the `string` result of `Split()` to the property's declared type.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN an entity has an `[Extracted]` property with an enum type THEN the `FromDynamoDb` method generates `entity.EnumProp = parts[N]` which is a string-to-enum assignment and causes a compile error CS0029

1.2 WHEN an entity has an `[Extracted]` property with an enum type THEN the `Keys.ExtractXComponents` method declares the return type as the enum but returns `parts[N]` (a string) directly, causing a compile error CS0029

1.3 WHEN an entity has an `[Extracted]` property with a numeric type (int, long, decimal) THEN the `FromDynamoDb` method generates `entity.NumericProp = parts[N]` which is a string-to-numeric assignment and causes a compile error CS0029

1.4 WHEN an entity has an `[Extracted]` property with a numeric type THEN the `Keys.ExtractXComponents` method may or may not convert correctly depending on whether the type name matches a hardcoded list — inconsistent behavior

1.5 WHEN the `KeysGenerator.IsEnumType` heuristic is used to detect enums THEN it only matches type names containing "Status", "Type", "Kind", or "State" — any enum with a name not containing these substrings (e.g., `SnsSubscriptionTopic`) silently falls through and generates broken code

### Expected Behavior (Correct)

2.1 WHEN an entity has an `[Extracted]` property with an enum type THEN the `FromDynamoDb` method SHALL generate `entity.EnumProp = Enum.Parse<EnumType>(parts[N])` to correctly convert the string component to the enum type

2.2 WHEN an entity has an `[Extracted]` property with an enum type THEN the `Keys.ExtractXComponents` method SHALL return `Enum.Parse<EnumType>(parts[N])` to correctly convert the string component to the declared return type

2.3 WHEN an entity has an `[Extracted]` property with a numeric type (int, long, decimal, etc.) THEN the `FromDynamoDb` method SHALL generate `entity.NumericProp = T.Parse(parts[N])` to correctly convert the string component

2.4 WHEN an entity has an `[Extracted]` property with a numeric type THEN the `Keys.ExtractXComponents` method SHALL return `T.Parse(parts[N])` to correctly convert the string component

2.5 WHEN enum detection is needed for code generation THEN the generator SHALL use a reliable `IsEnum` flag derived from Roslyn semantic analysis (`ITypeSymbol.TypeKind == TypeKind.Enum`) rather than name-based heuristics

2.6 WHEN an `[Extracted]` property has an unsupported type that cannot be parsed from a string THEN the source generator SHOULD emit a compile-time diagnostic rather than generating uncompilable code

### Unchanged Behavior (Regression Prevention)

3.1 WHEN an `[Extracted]` property has a string type THEN both the `FromDynamoDb` method and `Keys.ExtractXComponents` SHALL CONTINUE TO assign/return the string value directly without conversion

3.2 WHEN `IsEnumType` is called from `GetToAttributeValueExpression` for property serialization (e.g., `entity.EnumProp.ToString()`) THEN the serialization behavior SHALL CONTINUE TO produce `new AttributeValue { S = value.ToString() }` for enum properties

3.3 WHEN `IsEnumType` is called from `GetFromAttributeValueExpression` for property deserialization (e.g., `Enum.Parse<T>(value.S)`) THEN the deserialization behavior SHALL CONTINUE TO correctly parse enum values from DynamoDB string attributes

3.4 WHEN `IsEnumType` is called from `GetToAttributeValueExpressionForCollectionElement` for collection element serialization THEN the serialization behavior SHALL CONTINUE TO produce `.ToString()` for enum elements in lists

3.5 WHEN `[Computed]` properties generate key-building logic THEN the computed key generation SHALL CONTINUE TO work correctly and remain unaffected by this fix

3.6 WHEN entities have only string-typed `[Extracted]` properties THEN all generated code SHALL CONTINUE TO compile and behave identically to current behavior

