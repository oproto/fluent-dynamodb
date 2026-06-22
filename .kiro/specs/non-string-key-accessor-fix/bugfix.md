# Bugfix Requirements Document

## Introduction

When a partition key or sort key property is defined with a non-string .NET type (e.g., an enum like `SnsSubscriptionTopic` or a numeric type like `int`), the source-generated entity accessor methods (Get, Delete, Update, ConditionCheck) produce code that does not compile. The generated code passes the raw non-string type directly to `.WithKey()`, which only has overloads accepting `string` or `AttributeValue`. This results in type mismatch compilation errors (CS1503) across all accessor methods for affected entities.

The bug only manifests when the key has **no prefix** and is **not computed** — in those cases the generator correctly uses `string` as the parameter type since the caller provides the fully-formed prefixed value. When the key is a bare non-string type with no prefix/computed transformation, the generator should use the native .NET type as the parameter but must construct the `AttributeValue` inline rather than calling `WithKey(string, string)`.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a key property (partition key or sort key) has a non-string .NET type AND has no prefix AND is not computed THEN the system generates accessor methods that pass the non-string parameter value directly to `.WithKey()`, causing CS1503 compilation errors because `WithKey` only accepts `string` or `AttributeValue` parameters

1.2 WHEN an entity has a non-string sort key with no prefix (e.g., `SnsSubscriptionTopic` enum) THEN the system generates composite key accessor methods like `Get(string pK, SnsSubscriptionTopic sK)` that call `.WithKey("PK", pK, "SK", sK)` where the second key value is not a string, failing to compile

1.3 WHEN an entity has a non-string partition key with no prefix (e.g., `int`) THEN the system generates single-key accessor methods like `Get(int pK)` that call `.WithKey("pk", pK)` where the key value is not a string, failing to compile

1.4 WHEN a non-string key type triggers the bug THEN all four accessor method families (Get, Delete, Update, ConditionCheck) and both table-level overload families (GenerateSingleKeyOverloads, GenerateCompositeKeyOverloads) produce uncompilable code for that entity

### Expected Behavior (Correct)

2.1 WHEN a key property has a non-string .NET type AND has no prefix AND is not computed THEN the system SHALL generate accessor methods that use the native .NET type as the parameter type AND use `.SetKey(k => { ... })` with inline `AttributeValue` construction to build the key dictionary

2.2 WHEN an entity has a non-string sort key with no prefix (e.g., `SnsSubscriptionTopic` enum) THEN the system SHALL generate composite key accessor methods like `Get(string pK, SnsSubscriptionTopic sK)` that call `.SetKey(k => { k["PK"] = new AttributeValue { S = pK }; k["SK"] = new AttributeValue { S = sK.ToString() }; })` using the same serialization logic as `MapperGenerator.GetToAttributeValueExpression`

2.3 WHEN an entity has a non-string partition key with no prefix (e.g., `int`) THEN the system SHALL generate single-key accessor methods like `Get(int pK)` that call `.SetKey(k => { k["pk"] = new AttributeValue { N = pK.ToString() }; })` using the correct `AttributeValue` construction for the type

2.4 WHEN the fix generates `AttributeValue` construction for non-string key parameters THEN the system SHALL respect `PropertyModel.Format`, `PropertyModel.DateTimeKind`, enum-as-string conventions, and numeric types as `N` — consistent with how `ToDynamoDb` serializes those same properties

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a key property is of type `string` (regardless of prefix or computed status) THEN the system SHALL CONTINUE TO generate accessor methods using `.WithKey()` with string parameters as it does today

3.2 WHEN a key property has a prefix configured (regardless of underlying .NET type) THEN the system SHALL CONTINUE TO generate accessor methods with `string` parameter type using `.WithKey()` because the caller supplies the fully-formed prefixed value

3.3 WHEN a key property is computed (using `[Computed(...)]`) THEN the system SHALL CONTINUE TO generate accessor methods with `string` parameter type using `.WithKey()` because computed keys are always string-typed

3.4 WHEN a key property is a string with no prefix and not computed THEN the system SHALL CONTINUE TO generate accessor methods using `.WithKey()` with string parameters identical to current behavior

3.5 WHEN the fix is applied THEN the system SHALL CONTINUE TO generate correct accessor methods for all existing entities that currently compile successfully (entities with string keys, prefixed keys, and computed keys)
