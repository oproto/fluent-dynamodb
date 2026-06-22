# Bugfix Requirements Document

## Introduction

The source generator's `EntityAnalyzer.IsSupportedPropertyType()` rejects user-defined enum types with diagnostic DYNDB009 ("type not supported for DynamoDB mapping"), even though the downstream `MapperGenerator` already handles enum serialization correctly. This prevents any entity with an enum property from compiling, blocking a common and expected usage pattern.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN an entity property has a user-defined enum type (e.g., `Status EntityStatus`) THEN the system emits DYNDB009 error and fails the build

1.2 WHEN an entity property has a nullable enum type (e.g., `Status? EntityStatus`) THEN the system emits DYNDB009 error and fails the build

1.3 WHEN an entity property is a collection of enums (e.g., `List<Status>` or `HashSet<Status>`) THEN the system emits DYNDB009 error and fails the build

### Expected Behavior (Correct)

2.1 WHEN an entity property has a user-defined enum type THEN the system SHALL accept the property without error and serialize it as a DynamoDB String attribute using the enum member name (e.g., `"Success"`)

2.2 WHEN an entity property has a nullable enum type THEN the system SHALL accept the property without error and serialize non-null values as a DynamoDB String attribute using the enum member name, and handle null values according to existing nullable semantics

2.3 WHEN an entity property is a collection of enums THEN the system SHALL accept the property without error and serialize each element as a string within the appropriate DynamoDB collection type (List)

2.4 WHEN an entity property has an enum type with `[DynamoDbAttribute("attr", Format = "D")]` THEN the system SHALL serialize the enum as a DynamoDB Number attribute using the underlying integer value (e.g., `200` for `Status.Success` where `Success = 200`)

### Unchanged Behavior (Regression Prevention)

3.1 WHEN an entity property has a supported primitive type (string, int, bool, DateTime, etc.) THEN the system SHALL CONTINUE TO accept the property and serialize it correctly

3.2 WHEN an entity property has an unsupported type that is genuinely not mappable (e.g., a delegate, Span, or arbitrary class without `[DynamoDbEntity]`) THEN the system SHALL CONTINUE TO emit DYNDB009 error

3.3 WHEN an entity property has a `[DynamoDbMap]` nested entity type THEN the system SHALL CONTINUE TO accept and serialize it as a DynamoDB Map attribute

3.4 WHEN enum properties are used in the MapperGenerator serialization/deserialization path THEN the system SHALL CONTINUE TO serialize via `.ToString()` and deserialize via `Enum.Parse<T>()` for string format
